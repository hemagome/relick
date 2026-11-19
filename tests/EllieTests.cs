using System.Text.Json;
using Relick.Core;

static class EllieTests
{
    private static readonly string Ellie = CardCatalog.Ellie.Id;
    private static readonly string Bonfire = CardCatalog.Bonfire.Id;
    private static readonly string ExtraLife = CardCatalog.ExtraLife.Id;
    private static readonly string Control = CardCatalog.VictoriousManualControl.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Ellie: las palabras completas cuentan en el nombre y en las reglas", () =>
        {
            var card = CardCatalog.Attack;
            Check(EllieEffect.Matches(card with { Name = "VIDAS extra" }) &&
                EllieEffect.Matches(card with { Text = "Paga (ViDa)." }) &&
                !EllieEffect.Matches(card with { Text = "Actividad, salvavidas y vitalidad." }) &&
                !EllieEffect.Matches(CardCatalog.Ellie with { Id = "ellie-alternate" }),
                "El filtro usa palabras completas y excluye cualquier copia de Ellie.");
        }),
        ("Ellie: elige dos copias distintas, conserva marcas y permanece privada al reconectar", () =>
        {
            var (game, random) = Fixture(Ellie, Bonfire, Attack, ExtraLife, Control, Bonfire, Attack);
            var player = game.State.Active;
            player.MarkForCorruption(CardZone.Deck, 5);
            Check(new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 0 }.Apply(game, 0).Success,
                "Ellie abre la búsqueda pagando una acción.");
            Check(game.DecisionChoices(0).Select(choice => choice.Value).SequenceEqual([1, 3, 4, 5]) &&
                !game.ResolveDecision(1, 0).Success, "Solo el jugador activo ve las copias elegibles.");
            Choose(game, Bonfire, slot: 5);
            Check(player.Hand.SequenceEqual([Bonfire]) && player.CorruptsOnMemory(CardZone.Hand, 0) &&
                game.DecisionChoices(0).Select(choice => choice.Value).SequenceEqual([1, 3, 4]),
                "La segunda elección descuenta exactamente la primera copia.");

            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            foreach (var seat in new[] { 1, -1 })
            {
                var json = JsonSerializer.Serialize(DuelSnapshot.For(game, seat, 1), DuelJson.Default.DuelSnapshot);
                Check(!new[] { Bonfire, ExtraLife, Control }.Any(json.Contains), "El rival no ve opciones ni la carta elegida.");
            }
            var visuals = DuelVisualEvents.Capture(game);
            Choose(game, ExtraLife, slot: 3);
            player = game.State.Active;
            Check(player.Hand.SequenceEqual([Bonfire, ExtraLife]) && player.Deck.SequenceEqual([Ellie, Bonfire, Attack, Control, Attack]) &&
                player.Memory.Single() == Ellie && player.ActionsLeft == 1 && game.State.Phase == Phase.Actions &&
                visuals.Single().Kind == DuelVisualKind.Draw, "La búsqueda termina tras dos cartas y conserva el orden restante.");
        }),
        ("Ellie: una coincidencia se añade sola; sin objetivos el Código no se activa", () =>
        {
            var (game, _) = Fixture(Attack, Control, Ellie, Attack);
            Check(game.PlayCode(0, 0).Success && game.State.Active.Hand.SequenceEqual([Control]) && game.State.Phase == Phase.Actions,
                "Una coincidencia basta aunque se pidan dos.");
            (game, _) = Fixture(Attack, Ellie, CardCatalog.Senshuni.Id);
            var before = game.SaveSnapshot();
            Check(!game.PlayCode(0, 0).Success && game.SaveSnapshot() == before, "El intento sin coincidencias no paga nada.");
        }),
        ("Ellie: Error de Carga cancela antes de la búsqueda y Caja de Cartón queda equipada", () =>
        {
            var (game, _) = Fixture(Bonfire, ExtraLife, Attack);
            var rival = game.State.Players[1];
            rival.Equipment = CardCatalog.CardboardBox.Id;
            rival.Hand.Add(CardCatalog.LoadError.Id);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.ErrorResponse && game.PlayCode(1, 0).Success &&
                game.State.Active.Hand.Count == 0 && game.State.Active.ActionsLeft == 2 &&
                game.State.Active.Deck.SequenceEqual([Bonfire, ExtraLife, Attack]), "La anulación devuelve la acción antes de revelar objetivos.");
            Check(game.State.Active.Memory.Single() == Ellie && rival.Equipment == CardCatalog.CardboardBox.Id, "Ellie queda en memoria y Caja no responde.");
            game.State.Active.Hand.Add(Ellie);
            Check(game.PlayCode(0, 0).Success && game.State.PendingDecision?.Source == Ellie, "Un Código Personaje pasa a su búsqueda con Caja equipada.");
        }),
        ("Ellie: Relikia corrompe la copia y agotar el mazo recicla tras completar la búsqueda", () =>
        {
            var (game, random) = Fixture(Bonfire, ExtraLife);
            var player = game.State.Active;
            player.Hand.Clear();
            player.Relikia = 400;
            player.Memory.AddRange([Ellie, Control, Attack]);
            random.EnqueueDie(2);
            Check(game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success &&
                player.ActionsLeft == 0 && player.Relikia == 0 && player.CorruptedMemory.Single() == Ellie, "Se pagan los costes ordinarios de Relikia.");
            Choose(game, ExtraLife);
            Check(player.Hand.SequenceEqual([ExtraLife, Bonfire]) && game.State.Phase == Phase.RecycleChoice, "Ambas cartas se añaden antes del reciclaje.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0, 1]).Success && game.State.Phase == Phase.Actions &&
                game.State.Active.Hand.SequenceEqual([ExtraLife, Bonfire]) && game.State.Active.Deck.Contains(Control),
                "El mazo repuesto queda disponible para las próximas jugadas.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(params string[] deck)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[Ellie], []], Decks = [deck.ToList(), [Attack]], Bonuses = [null, null]
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        game.State.Active.Dexterity = 0;
        game.State.Active.AttackRerollUsedThisTurn = true;
        return (game, random);
    }

    private static void Choose(GameEngine game, string id, int? slot = null)
    {
        var choices = game.DecisionChoices(0);
        var index = Enumerable.Range(0, choices.Count).Single(i => choices[i].CardId == id && (slot == null || choices[i].Value == slot));
        Check(new DuelMove { Kind = DuelMoveKind.Choose, Index = index }.Apply(game, 0).Success, "Debe poder elegir la carta autorizada.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

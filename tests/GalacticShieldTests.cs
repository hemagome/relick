using System.Text.Json;
using Relick.Core;

static class GalacticShieldTests
{
    private static readonly string Shield = CardCatalog.GalacticShield.Id;
    private static readonly string[] Charges = [CardCatalog.FloppyDisk.Id, CardCatalog.TimeBender.Id, CardCatalog.QuickLoad.Id];

    public static (string Name, Action Run)[] Cases =>
    [
        ("Escudo: las tres cartas del tope quedan boca abajo y sobreviven al guardado", () =>
        {
            var (game, random) = Fixture();
            var player = game.State.Active;
            player.Hand.Add(Shield);
            Check(game.PlayCode(0, 0).Success && game.State.ActionsLeft == 1 && player.Memory.Count == 0,
                "El Código gasta una acción y pasa al campo.");
            Check(player.Deck.Count == 5 && !player.Hand.Any() &&
                player.GalacticShields!.Single().Cards.Select(card => card.CardId).SequenceEqual(Charges), "Se apartan las tres cartas del tope en orden.");
            var copy = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            foreach (var seat in new[] { 0, 1, -1 })
            {
                var view = DuelSnapshot.For(copy, seat, 1);
                var json = JsonSerializer.Serialize(view, DuelJson.Default.DuelSnapshot);
                Check(view.Players[0].GalacticShieldCharges!.SequenceEqual([3]) && !Charges.Any(json.Contains),
                    "La vista pública y las privadas contienen solo el conteo.");
            }
        }),
        ("Escudo: cada copia reduce un golpe una vez sin elegir entre cartas boca abajo", () =>
        {
            var (game, random) = Fixture();
            var defender = game.State.Active;
            defender.Hand.AddRange([Shield, Shield]);
            Check(game.PlayCode(0, 0).Success && game.PlayCode(0, 0).Success, "Ambas copias entran en campo.");
            defender.AutoPassCombos = true;
            var visual = DuelVisualEvents.Capture(game);
            Attack(game, random, 6);
            Check(game.State.PendingDecision?.Source != Shield,
                "Las cartas están boca abajo, así que cada copia entrega su carta del tope sin abrir un menú.");
            Check(defender.Hand.SequenceEqual([Charges[0], CardCatalog.Attack.Id]) &&
                defender.GalacticShields!.Select(shield => shield.Cards.Count).SequenceEqual([2, 2]),
                "Cada copia entrega exactamente una carta.");
            Check(visual.Where(cue => cue is { Kind: DuelVisualKind.Pile, Source: CardZone.ShieldCharge, Destination: CardZone.Hand })
                    .Select(cue => (cue.SourceIndex, cue.Value, cue.DestinationPlayer)).SequenceEqual([(0, 0, 0), (1, 1, 0)]),
                "Cada carta viaja desde su Escudo hasta su lugar en la mano.");
            Check(defender.Health == 7 && game.State.Active.Relikia == CardCatalog.Destiny.Reward &&
                visual.Single(cue => cue.Kind == DuelVisualKind.Hit) is { Value: 1, AttackDie: 6 },
                "Un crítico de 5 baja a 1 y conserva su premio.");
        }),
        ("Escudo: el daño de un efecto se reduce a cero y la última carta se añade automáticamente", () =>
        {
            var (game, random) = Fixture();
            var defender = game.State.Active;
            defender.GalacticShields = [new() { Cards = [new(Charges[0], true)] }];
            game.State.CurrentPlayer = 1;
            game.State.Phase = Phase.Combat;
            game.State.Active.Bonus = CardCatalog.Jessica.Id;
            random.EnqueueDie(5);
            var visuals = DuelVisualEvents.Capture(game);
            Check(game.UseFieldCode(1, FieldZone.Bonus).Success && game.State.Phase == Phase.Combat &&
                defender.Health == 8 && defender.Hand.Single() == Charges[0] && defender.CorruptsOnMemory(CardZone.Hand, 0) &&
                defender.GalacticShields[0].Cards.Count == 0 && visuals.All(cue => cue.Kind != DuelVisualKind.Hit),
                "La carta marcada conserva su marca y evitar todo el daño no produce un golpe.");
        }),
        ("Escudo: en la cadena de daño, Robotizar resta antes de que el escudo ofrezca su carta", () =>
        {
            var (game, random) = Fixture();
            var defender = game.State.Active;
            defender.GalacticShields = [new() { Cards = [new(Charges[0], false)] }];
            defender.AttachedCards.Add(CardCatalog.Robotize.Id);
            game.State.CurrentPlayer = 1;
            game.State.Phase = Phase.Combat;
            game.State.Active.Bonus = CardCatalog.Jessica.Id;
            random.EnqueueDie(5);
            Check(game.UseFieldCode(1, FieldZone.Bonus).Success && defender.Health == 8 && defender.Hand.Single() == Charges[0],
                "Robotizar y el escudo anulan juntos los 2 de daño.");
            var robot = game.State.Log.FindIndex(line => line.Contains("de 2 a 1 con Robotizar"));
            var shield = game.State.Log.FindIndex(line => line.Contains("de 1 a 0 con Escudo Galáctico"));
            Check(robot >= 0 && shield > robot, "Robotizar reduce primero y el escudo recibe el daño ya reducido.");
        }),
        ("Escudo: otra copia protege a la elegida y las cartas restantes salen con su escudo", () =>
        {
            var (game, random) = Fixture();
            var player = game.State.Active;
            player.Hand.AddRange([Shield, Shield]);
            Check(game.PlayCode(0, 0).Success && game.PlayCode(0, 0).Success, "Prepara ambos Escudos.");
            game.State.CurrentPlayer = 1;
            game.State.Active.Hand.AddRange([CardCatalog.BlueEgg.Id, CardCatalog.BlueEgg.Id]);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.GalacticShield, 0).Success,
                "El Huevo elige una copia sobre el Relikario.");
            Check(game.DecisionChoices(0) is [{ CardId: null }, { Value: -1 }],
                "Solo la otra copia y la opción de pasar están disponibles, sin elegir carta boca abajo.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveDecision(0, 0).Success, "La protección se puede recuperar.");
            player = game.State.Players[0];
            Check(player.GalacticShields!.Count == 2 && player.Memory.Single() == CardCatalog.Attack.Id,
                "La carta elegida va a memoria y se conserva el Escudo objetivo.");
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.GalacticShield, 0).Success &&
                game.ResolveDecision(0, game.DecisionChoices(0).Count - 1).Success, "Es posible rechazar la protección.");
            Check(player.GalacticShields.Count == 1 && player.GalacticShields[0].Cards.Count == 2 &&
                player.Memory.SequenceEqual(new[] { CardCatalog.Attack.Id, Shield }.Concat(Charges)), "Las cargas anexadas salen con la copia destruida.");
        }),
        ("Escudo: Zahira puede suspender el Escudo o quedar protegida sobre el Relikario", () =>
        {
            var (game, random) = Fixture();
            var player = game.State.Active;
            player.Hand.Add(Shield);
            Check(game.PlayCode(0, 0).Success, "Prepara el Escudo.");
            var rival = game.State.Players[1];
            var shield = new FieldTarget(0, FieldZone.GalacticShield, 0, Shield);
            Check(game.ZahiraTargets(1).Contains(shield), "Zahira puede cubrir el Escudo.");
            rival.ZahiraHost = shield;
            Attack(game, random, 5);
            Check(player.Health == 5 && player.Hand.Count == 0 && player.GalacticShields![0].Cards.Count == 3,
                "Un Escudo cubierto pierde el efecto antes del daño.");

            game.State.Phase = Phase.Actions;
            rival.ZahiraHost = new(0, FieldZone.Relikario, -1, player.Relikario.Id);
            rival.Hand.Add(CardCatalog.BlueEgg.Id);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 1, FieldZone.Zahira).Success &&
                game.State.DecisionPlayer == 0 && game.ResolveDecision(0, 0).Success, "Decide el dueño del Relikario que aloja la carta.");
            Check(rival.ZahiraHost != null && player.Memory.Single() == Charges[0], "El Escudo conserva la carta sobre el Relikario.");
        }),
        ("Escudo: los dos anuladores normales conservan el mazo y devuelven la acción", () =>
        {
            foreach (var useBox in new[] { false, true })
            {
                var (game, random) = Fixture();
                game.State.Active.Hand.Add(Shield);
                if (useBox) game.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
                else game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                var visuals = DuelVisualEvents.Capture(game);
                Check(game.PlayCode(0, 0).Success && game.State.Active.Memory.Count == 0 && visuals.Count == 0,
                    "El Escudo sigue en activación mientras espera la respuesta.");
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                visuals = DuelVisualEvents.Capture(game);
                Check((useBox ? game.ResolveCodeResponse(1, true) : game.PlayCode(1, 0)).Success,
                    "La activación admite ambas respuestas tras reconectar.");
                var player = game.State.Active;
                Check(player.GalacticShields == null && player.Deck.Count == 8 && player.Memory.Single() == Shield &&
                    player.ActionsLeft == 2 && game.State.Phase == Phase.Actions, "La cancelación no aparta cargas.");
                Check(visuals.Single(cue => cue is { Kind: DuelVisualKind.Pile, Player: 0 }) is
                    { Source: CardZone.Activation, Destination: CardZone.Memory }, "El Escudo viaja a Memoria al anularse.");
            }
        }),
        ("Escudo: un mazo corto recicla antes de completar la carga", () =>
        {
            var (game, random) = Fixture();
            var player = game.State.Active;
            player.Hand.Add(Shield);
            player.Deck.RemoveRange(2, 6);
            player.Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Attack.Id]);
            random.EnqueueDie(2);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.RecycleChoice &&
                player.GalacticShields!.Single().Cards.Count == 2, "El agotamiento interrumpe la colocación.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0, 1]).Success, "El reciclaje continúa después de reconectar.");
            player = game.State.Active;
            Check(game.State.Phase == Phase.Actions && player.Deck.Count == 1 &&
                player.GalacticShields!.Single().Cards.Select(card => card.CardId).SequenceEqual([.. Charges.Take(2), CardCatalog.Attack.Id]),
                "La colocación continúa con la tercera carta del mazo reciclado.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[], []],
            Decks = [[.. Charges, .. Enumerable.Repeat(CardCatalog.Attack.Id, 5)], [CardCatalog.Attack.Id]],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        foreach (var player in game.State.Players) player.Dexterity = 0;
        return (game, random);
    }

    private static void Attack(GameEngine game, ScriptedRandom random, int die)
    {
        game.State.CurrentPlayer = 1;
        game.State.Phase = Phase.Combat;
        game.State.Active.AttackRerollUsedThisTurn = true;
        game.State.Active.Hand.Add(CardCatalog.Attack.Id);
        game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
        random.EnqueueDie(die);
        Check(game.Attack(1, 0, 0).Success, "El crítico de prueba debe resolverse.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

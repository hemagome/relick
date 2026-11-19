using System.Text.Json;
using Relick.Core;

static class RobotizeTests
{
    private static readonly string Robotize = CardCatalog.Robotize.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Pendant = CardCatalog.Pendant.Id;
    private static readonly string Senshuni = CardCatalog.Senshuni.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Robotizar: se coloca sobre el Relikario y actúa al abrir Acciones, incluidas las repetidas", () =>
        {
            var (game, _) = Fixture(Pendant, Attack, Attack, Attack);
            var p = game.State.Active;
            p.Hand.Add(Robotize);
            Check(game.PlayCode(0, 0).Success && p.AttachedCards.Single() == Robotize && p.Memory.Count == 0 &&
                p.Deck.Count == 4 && p.ActionsLeft == 1, "Instalar no repite el comienzo de la etapa actual.");
            p.MarkForCorruption(CardZone.Deck, 0);
            game.State.Phase = Phase.Combat;
            Check(game.Advance(0).Success && p.Exchange.Single() == Pendant && p.Deck.Count == 3 &&
                p.CorruptsOnMemory(CardZone.Exchange, 0) && p.ActionsLeft == 1, "El tope no-Código entra al canje sin gastar acción.");
            game.State.ExtraActionStages = 1;
            Check(game.Advance(0).Success && p.Exchange.Count == 0 && p.Hand.Single() == Senshuni &&
                p.Bonus == Attack && p.Memory.Single() == Attack && p.CorruptedMemory.Single() == Pendant &&
                p.ActionsLeft == 2, "Repetir Acciones activa Robotizar y completa el canje de forma normal.");
        }),
        ("Robotizar: un Código del tope ofrece una decisión privada que persiste al reconectar", () =>
        {
            foreach (var toHand in new[] { true, false })
            {
                var (game, random) = Fixture(CardCatalog.Ellie.Id, Attack, Attack);
                game.State.Active.AttachedCards.Add(Robotize);
                game.State.Phase = Phase.Combat;
                Check(game.Advance(0).Success && game.State.PendingDecision?.Source == Robotize && !game.ResolveDecision(1, 0).Success,
                    "Solo quien controla Robotizar elige el destino.");
                Check(!JsonSerializer.Serialize(DuelSnapshot.For(game, 1, 1), DuelJson.Default.DuelSnapshot).Contains(CardCatalog.Ellie.Id),
                    "La identidad del Código no sale en la vista rival durante la decisión.");
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                var option = DuelOptions.For(game, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose &&
                    o.Label.StartsWith(toHand ? "Añadir Ellie" : "Enviar Ellie", StringComparison.Ordinal));
                Check(option.Move.Apply(game, 0).Success && game.State.Phase == Phase.Actions, "La opción guardada se ejecuta.");
                var p = game.State.Active;
                Check((toHand ? p.Hand : p.Exchange).Single() == CardCatalog.Ellie.Id && p.ActionsLeft == 2 && p.Deck.Count == 2,
                    "La carta va directamente al destino elegido.");
                if (toHand)
                    Check(!JsonSerializer.Serialize(DuelSnapshot.For(game, 1, 2), DuelJson.Default.DuelSnapshot).Contains(CardCatalog.Ellie.Id),
                        "El historial público tampoco revela el Código que fue a mano.");
            }
        }),
        ("Robotizar: el tope final se coloca antes de reciclar y después se completa el canje", () =>
        {
            var (game, random) = Fixture(Pendant);
            var p = game.State.Active;
            p.AttachedCards.Add(Robotize);
            p.Exchange.Add(Attack);
            p.Memory.AddRange([Attack, Attack]);
            game.State.Phase = Phase.Combat;
            random.EnqueueDie(2);
            Check(game.Advance(0).Success && game.State.Phase == Phase.RecycleChoice && p.Exchange.SequenceEqual([Attack, Pendant]),
                "El agotamiento se comprueba en cuanto sale la última carta.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0, 1]).Success && game.State.Phase == Phase.Actions &&
                game.State.Active.Exchange.Count == 0 && game.State.Active.Hand.Single() == Senshuni && game.State.Active.Bonus == Attack,
                "El canje pendiente se completa después de reponer el mazo.");
        }),
        ("Robotizar: dos copias procesan dos topes consecutivos con una pausa entre ellos", () =>
        {
            var (game, random) = Fixture(Senshuni, Pendant, Attack, Attack);
            var p = game.State.Active;
            p.AttachedCards.AddRange([Robotize, Robotize]);
            game.State.Phase = Phase.Combat;
            Check(game.Advance(0).Success && game.State.PendingDecision?.Source == Robotize && p.Deck.Count == 4,
                "Las copias esperan la decisión sobre el primer tope.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveDecision(0, 0).Success, "La decisión se recupera.");
            p = game.State.Active;
            Check(p.Hand.Single() == Senshuni && p.Exchange.Single() == Pendant &&
                p.Deck.SequenceEqual([Attack, Attack]) && game.State.Phase == Phase.Actions && p.ActionsLeft == 2,
                "Cada copia actúa una vez con el tope actual.");
        }),
        ("Robotizar: reduce daño de ataque y varias copias pueden dejar en cero el daño de un efecto", () =>
        {
            var (game, random) = Fixture(Attack, Attack, Attack);
            var defender = game.State.Players[1];
            defender.AttachedCards.Add(Robotize);
            var visuals = DuelVisualEvents.Capture(game);
            AttackWithDestiny(game, random, 5);
            Check(defender.Health == 6 && game.State.Active.Relikia == CardCatalog.Destiny.Reward &&
                visuals.Any(cue => cue.Kind == DuelVisualKind.Guard && cue is { GuardCard: "robotizar", Original: 3, Value: 2 }),
                "Se reduce un punto y se conserva el premio.");
            defender.AttachedCards.Add(Robotize);
            game.State.Players[0].Bonus = CardCatalog.Jessica.Id;
            visuals.Clear();
            random.EnqueueDie(5);
            Check(game.UseFieldCode(0, FieldZone.Bonus).Success && defender.Health == 6 &&
                visuals.All(cue => cue.Kind != DuelVisualKind.Hit), "Las dos copias reducen a cero el daño y no se aplica un impacto.");
        }),
        ("Robotizar: un 1 de ataque destruye las copias propias después de confirmar el dado", () =>
        {
            foreach (var modify in new[] { true, false })
            {
                var (game, random) = Fixture(Attack, Attack);
                var p = game.State.Active;
                var rival = game.State.Players[1];
                p.AttachedCards.AddRange([Robotize, Attack, Robotize]);
                rival.AttachedCards.Add(Robotize);
                p.Equipment = CardCatalog.Pc.Id;
                p.EquipmentDie = 5;
                AttackWithDestiny(game, random, 1);
                Check(game.State.Phase == Phase.AttackRerollChoice && p.AttachedCards.Count == 3, "Antes de confirmar se puede modificar el 1.");
                Check((modify ? game.UsePc(0) : game.ResolveAttackReroll(0, false)).Success, "El dado se resuelve.");
                Check(p.AttachedCards.SequenceEqual(modify ? [Robotize, Attack, Robotize] : [Attack]) &&
                    rival.AttachedCards.Single() == Robotize && p.Memory.Count(id => id == Robotize) == (modify ? 0 : 2),
                    "Se comprueba el valor final y solo sobre el jugador que tiró.");
            }
        }),
        ("Robotizar: Reverse puede protegerlo tras un 1 propio de efecto y la tirada se reanuda", () =>
        {
            var (game, random) = Fixture(Attack, Attack);
            var p = game.State.Active;
            p.Health = 4;
            p.AttachedCards.Add(Robotize);
            p.Skills.Add(CardCatalog.Reverse.Id);
            p.Hand.Add(CardCatalog.ExtraLife.Id);
            random.EnqueueDie(1);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.DestructionResponse && p.Health == 4,
                "El efecto del dado espera a que se resuelva la destrucción.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveDestructionProtection(0, 0).Success && game.State.Active.AttachedCards.Single() == Robotize &&
                game.State.Active.Health == 5 && game.State.Active.Skills.Count == 0,
                "Reverse sustituye la destrucción y se aplica la curación después de reconectar.");
        }),
        ("Robotizar: Zahira suspende sus efectos y Error de Carga puede anular la colocación", () =>
        {
            var (game, _) = Fixture(Attack, Attack);
            var p = game.State.Active;
            var rival = game.State.Players[1];
            p.Hand.Add(Robotize);
            rival.Hand.Add(CardCatalog.LoadError.Id);
            Check(game.PlayCode(0, 0).Success && game.PlayCode(1, 0).Success && p.AttachedCards.Count == 0 &&
                p.Memory.Single() == Robotize && p.ActionsLeft == 2, "La activación anulada queda en memoria y devuelve la acción.");
            p.Hand.Add(Robotize);
            Check(game.PlayCode(0, 0).Success, "Entra la segunda copia.");
            rival.ZahiraHost = new(0, FieldZone.Attached, 0, Robotize);
            game.State.Phase = Phase.Combat;
            Check(game.Advance(0).Success && p.Deck.Count == 2 && p.Exchange.Count == 0,
                "La copia cubierta no dispara su efecto de inicio.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(params string[] deck)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[], []], Decks = [deck.ToList(), [Attack]],
            Bonuses = [Senshuni, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        foreach (var p in game.State.Players)
        {
            p.Dexterity = 0;
            p.AttackRerollUsedThisTurn = true;
        }
        return (game, random);
    }

    private static void AttackWithDestiny(GameEngine game, ScriptedRandom random, int die)
    {
        game.State.Phase = Phase.Combat;
        game.State.Active.Hand.Add(Attack);
        game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
        random.EnqueueDie(die);
        Check(game.Attack(0, 0, 0).Success, "Debe declararse el ataque de prueba.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

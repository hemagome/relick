using Relick.Core;

static class EquipmentRaidTests
{
    private static readonly string Area = CardCatalog.EquipmentRaid.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Take = CardCatalog.TakeEquipment.Id;
    private static readonly string Helmet = CardCatalog.DiamondHelmet.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Área: desde la mano cuesta una acción, se eligen ambos lados y no se reemplaza un Área activa", () =>
        {
            var g = Fixture();
            var p = g.State.Active;
            p.Hand.AddRange([Area, Area]);
            g.State.Phase = Phase.Combat;
            var before = g.SaveSnapshot();
            Check(!g.ActivateArea(0, 0).Success && g.SaveSnapshot() == before, "Se activa fuera de Acciones.");
            g.State.Phase = Phase.Actions;
            var move = DuelOptions.For(g, 0).First(o => o.Move.Kind == DuelMoveKind.ActivateArea).Move;
            Check(move.Apply(g, 0).Success && p.ActionsLeft == 1 && p.Area == Area, "Falla la activación desde la mano.");
            Check(p.AreaSideA == null && g.State.DecisionPlayer == 0 && g.DecisionChoices(0).Count == 2, "Falta la elección de lados.");
            Choose(g, 0, 1);
            before = g.SaveSnapshot();
            Check(p.AreaSideA == 1 && g.State.Phase == Phase.Actions, "La elección no asigna A al rival.");
            Check(!g.ActivateArea(0, 0).Success && !DuelOptions.For(g, 0).Any(o => o.Move.Kind == DuelMoveKind.ActivateArea) &&
                g.SaveSnapshot() == before, "Se reemplaza un Área con otra de la mano.");
        }),
        ("Área: en el Bonus inicial, otra Área sigue como Bonus y asciende al salir la activa", () =>
        {
            var g = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["A", "B"], Hands = [[], [CardCatalog.TimeBomb.Id]],
                Decks = [[Area, Attack, CardCatalog.Dodge.Id], [Attack]], Bonuses = [Area, Attack], StartingSeat = 1
            }, new DuelRandom());
            Check(g.State.Players[0].Area == Area && g.State.Players[0].Bonus == Area && g.State.DecisionPlayer == 0, "No se separan las dos cartas iniciales.");
            var rng = new DuelRandom(); rng.Replay([]);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Choose(g, 0);
            g.State.Phase = Phase.Actions;
            Check(g.PlayCode(1, 0).Success, "No abre Bomba de Tiempo.");
            Check(g.DecisionChoices(1).Any(c => c.Field is { Owner: 0, Zone: FieldZone.Bonus }) &&
                g.DecisionChoices(1).Any(c => c.Field is { Owner: 0, Zone: FieldZone.Area }), "Bomba no distingue Bonus y Área.");
            Check(g.ResolveDestruction(1, 0, FieldZone.Area).Success, "No se puede destruir el Área por la zona de Bonus.");
            Check(g.State.Players[0].Memory.Single() == Area && g.State.Players[0].Area == Area &&
                g.State.Players[0].Bonus == Attack && g.State.Players[0].AreaSideA == null && g.State.DecisionPlayer == 0, "No se promueve el Área que seguía en Bonus.");
            rng.Finish();
        }),
        ("Área: un Área en el Bonus puede canjearse y Magnet distingue Bonus de Área", () =>
        {
            var g = Fixture();
            var p = g.State.Players[0];
            p.Area = Area; p.AreaSideA = 0; p.Bonus = Area;
            p.Hand.AddRange([CardCatalog.Dodge.Id, CardCatalog.ExtraLife.Id]);
            Check(g.Exchange(0, 0).Success && g.Exchange(0, 0).Success && p.Hand.Single() == Area && p.Area == Area, "No se puede tomar el segundo Área por canje.");
            g.State.CurrentPlayer = 1; g.State.Phase = Phase.MagnetChoice;
            var options = DuelOptions.For(g, 1).Where(o => o.Move.Kind == DuelMoveKind.Magnet && o.Move.Flag).ToArray();
            Check(options.Length == 2, "Magnet no ofrece las dos cartas en la zona de Bonus.");
            Check(options.Single(o => o.Move.Value == (int)FieldZone.Area).Move.Apply(g, 1).Success, "Magnet no puede tomar el Área.");
            Check(p.Area == null && p.Bonus == Attack && g.State.Players[1].Hand.Single() == Area &&
                g.State.Players[1].IsForeignHandCard(0), "La carta tomada conserva su dueño y el Bonus queda disponible.");
        }),
        ("Área: un Bonus posterior no puede hacerse pasar por la segunda carta del inicio", () =>
        {
            var g = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["A", "B"], Hands = [[], [CardCatalog.TimeBomb.Id]], Decks = [[Area, Area, Attack], [Attack, Attack]],
                Bonuses = [Area, Attack], StartingSeat = 1
            }, new SeededRandom(31));
            Choose(g, 0);
            g.State.Phase = Phase.MagnetChoice;
            Check(g.ResolveMagnet(1, true).Success && g.State.Players[0].Bonus == Area &&
                !g.State.Players[0].InitialAreaWaitingInBonus, "Tomar el Bonus debe cancelar la promoción de esa copia.");
            g.State.Phase = Phase.Actions;
            Check(g.PlayCode(1, 0).Success && g.ResolveDestruction(1, 0, FieldZone.Area).Success, "No se destruye el Área activa.");
            Check(g.State.Players[0].Area == null && g.State.Players[0].Bonus == Area,
                "Un Área que llegó después no es la segunda carta del Bonus inicial.");
        }),
        ("Área: los dos lados enlazan con ¡Dame ese equipo!, la mano privada y la salida del Área", () =>
        {
            var g = Fixture();
            var p = g.State.Players[0]; var rival = g.State.Players[1];
            p.Hand.Add(Area);
            p.Deck.Insert(0, Take);
            p.Memory.Add(Take);
            rival.Memory.Add(Attack);
            rival.Deck.Insert(0, Helmet);
            rival.Hand.AddRange([CardCatalog.ExtraLife.Id, CardCatalog.Bonfire.Id]);
            Check(g.ActivateArea(0, 0).Success, "No se activa.");
            Choose(g, 0); Choose(g, 0);
            Check(p.AttachedCards.Single() == Attack && rival.Memory.Count == 0 && p.ForeignFieldCards!.Single() == Attack,
                "A no anexa el Ataque de su dueño original.");
            Check(g.DecisionChoices(0).Count == 2 && g.DecisionChoices(1).Count == 0, "La búsqueda debe ser privada y ofrecer mazo y memoria.");
            Choose(g, 0);
            Check(p.Hand.Single() == Take && p.Memory.Single() == Take, "A no recupera del mazo.");
            Check(rival.Equipment == Helmet && rival.ActionsLeft == 2 && g.State.Phase == Phase.Actions, "B no se equipa gratis.");
            Check(g.PlayCode(0, 0).Success, "No puede jugar el Código recuperado.");
            Check(p.Equipment == Helmet && p.AttachedCards.Count == 2 && p.Relikia == 1500 && p.Area == Area, "La transferencia no inicia el premio.");
            Check(DuelSnapshot.For(g, 0, 4).Players[1].Hand.SequenceEqual(rival.Hand) &&
                DuelSnapshot.For(g, -1, 4).Players[1].Hand.All(id => id == null) &&
                DuelSnapshot.For(g, 1, 4).Options.All(o => o.Move.Kind != DuelMoveKind.Choose), "La mirada debe ser privada.");
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), new SeededRandom(78));
            Choose(g, 0, 1);
            p = g.State.Players[0]; rival = g.State.Players[1];
            Check(p.Area == null && p.Memory.Contains(Area) && p.Hand.Single() == CardCatalog.Bonfire.Id &&
                p.IsForeignHandCard(0) && p.Relikia == 1500 && rival.Hand.Single() == CardCatalog.ExtraLife.Id, "El premio no toma una carta y destruye el Área.");
        }),
        ("Área: si se toma equipo de la mano del dueño, paga aunque ya no haya cartas para mirar", () =>
        {
            var g = Fixture();
            var p = g.State.Players[0]; var rival = g.State.Players[1];
            rival.Area = Area; rival.AreaSideA = 0; rival.Hand.Add(Helmet);
            g.State.Phase = Phase.MagnetChoice;
            Check(g.ResolveMagnet(0, false, 0).Success && p.Hand.Single() == Helmet && p.IsForeignHandCard(0), "No se toma el equipo de mano.");
            Check(p.Relikia == 1500 && rival.Area == null && rival.Memory.Single() == Area && g.State.Phase == Phase.Combat,
                "La mano vacía debería conservar el premio y permitir destruir el Área.");
        }),
        ("Área: en la preparación local se resuelve después de colocar el Bonus y antes de las primeras jugadas", () =>
        {
            var g = TestGame.Create(11);
            var p = g.State.Players[0];
            p.Deck.Insert(0, Area);
            p.Deck.Insert(1, Area);
            Check(g.Start([]).Success && p.Area == Area && p.Bonus == Area && g.State.DecisionPlayer == 0, "El flujo local omitió el Área inicial.");
        })
    ];

    private static GameEngine Fixture()
    {
        var g = GameEngine.FromOpening(new DuelOpening { Names = ["A", "B"], Hands = [[], []],
            Decks = [[Attack, Attack, CardCatalog.Dodge.Id], [Attack, Attack]], Bonuses = [Attack, Attack] }, new SeededRandom(32));
        g.State.Phase = Phase.Actions;
        return g;
    }

    private static void Choose(GameEngine g, int actor, int index = 0) =>
        Check(new DuelMove { Kind = DuelMoveKind.Choose, Index = index }.Apply(g, actor).Success, "No se aplicó una elección autorizada.");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

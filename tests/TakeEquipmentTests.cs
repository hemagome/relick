using Relick.Core;

static class TakeEquipmentTests
{
    public static (string Name, Action Run)[] Cases =>
    [
        ("Dame ese equipo: elige solo al rival, transfiere el equipo y anexa un Ataque antes de barajar", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(actor);
                var p = g.State.Active;
                var rival = g.State.Players[1 - actor];
                p.Equipment = CardCatalog.Pendant.Id;
                rival.EquipmentCounters = 2;
                p.MarkForCorruption(CardZone.Deck, 0);
                rival.MarkForCorruption(CardZone.Equipment, 0);
                Check(g.PlayCode(actor, 0).Success && g.State.DecisionPlayer == actor);
                var option = Choice(g, actor);
                Check(option.Field == new FieldTarget(1 - actor, FieldZone.Equipment, -1, CardCatalog.Sniper.Id));
                Check(DuelSnapshot.For(g, 1 - actor, 2).Options.All(o => o.Move.Kind != DuelMoveKind.Choose));
                var saved = g.SaveSnapshot();
                Check(!option.Move.Apply(g, 1 - actor).Success && !g.ResolveDecision(actor, 1).Success && g.SaveSnapshot() == saved);
                var events = DuelVisualEvents.Capture(g);
                rng.EnqueueDie(1);
                Check(option.Move.Apply(g, actor).Success && g.State.Phase == Phase.Actions);
                Check(p.Equipment == CardCatalog.Sniper.Id && p.EquipmentCounters == 2 && rival.Equipment == null && rival.EquipmentCounters == 0);
                Check(p.Memory.SequenceEqual([CardCatalog.TakeEquipment.Id, CardCatalog.Pendant.Id]) && rival.Memory.Count == 0);
                Check(p.ForeignFieldCards!.Single() == CardCatalog.Sniper.Id && p.CorruptsOnMemory(CardZone.Equipment, 0));
                Check(p.Deck.SequenceEqual([CardCatalog.Dodge.Id, CardCatalog.Bonfire.Id]) && p.AttachedCards.Single() == CardCatalog.Attack.Id);
                Check(p.CorruptsOnMemory(CardZone.Attached, 0) && p.CorruptionMarks?.ContainsKey(CardZone.Deck) != true && p.ActionsLeft == 1);
                Check(events.Any(e => e is { Source: CardZone.Equipment, Destination: CardZone.Equipment } && e.Player == 1 - actor && e.DestinationPlayer == actor));
                var attach = events.FindIndex(e => e is { Source: CardZone.Deck, Destination: CardZone.Attached, Value: -1 });
                Check(attach >= 0 && events.FindIndex(e => e.Kind == DuelVisualKind.Shuffle) > attach);
                p.Hand.Add(CardCatalog.Pendant.Id);
                Check(g.Equip(actor, 0).Success && rival.CorruptedMemory.Single() == CardCatalog.Sniper.Id && p.ForeignFieldCards == null);
            }
        }),
        ("Dame ese equipo: la carta se puede anular y exige los dos objetivos de su texto", () =>
        {
            var (g, _) = Fixture();
            var p = g.State.Active;
            var rival = g.State.Players[1];
            rival.Equipment = null;
            var saved = g.SaveSnapshot();
            Check(!g.PlayCode(0, 0).Success && g.SaveSnapshot() == saved);
            rival.Equipment = CardCatalog.Sniper.Id;
            p.Deck.Remove(CardCatalog.Attack.Id);
            saved = g.SaveSnapshot();
            Check(!g.PlayCode(0, 0).Success && g.SaveSnapshot() == saved);
            p.Deck.Add(CardCatalog.Attack.Id);
            rival.Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse && g.PlayCode(1, 0).Success);
            Check(p.ActionsLeft == 2 && p.Equipment == null && rival.Equipment == CardCatalog.Sniper.Id && p.AttachedCards.Count == 0);
        }),
        ("Dame ese equipo: al recuperar la partida, Reverse puede conservar el equipo anterior", () =>
        {
            foreach (var protect in new[] { false, true })
            {
                var (g, rng) = Fixture();
                g.State.Active.Equipment = CardCatalog.Pendant.Id;
                g.State.Active.Skills.Add(CardCatalog.Reverse.Id);
                Check(g.PlayCode(0, 0).Success && Choice(g, 0).Move.Apply(g, 0).Success && g.State.Phase == Phase.DestructionResponse);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                Check(g.ResolveDestructionProtection(0, protect ? 0 : -1).Success && g.State.Phase == Phase.Actions);
                Check(g.State.Active.Equipment == (protect ? CardCatalog.Pendant.Id : CardCatalog.Sniper.Id));
                Check(g.State.Players[1].Equipment == (protect ? CardCatalog.Sniper.Id : null) && g.State.Active.AttachedCards.Count == (protect ? 0 : 1));
            }
        }),
        ("Dame ese equipo: Súper Imán sustituye el efecto antes de reemplazar el equipo propio", () =>
        {
            var (g, _) = Fixture();
            var p = g.State.Active;
            var rival = g.State.Players[1];
            p.Equipment = CardCatalog.Pendant.Id;
            rival.Equipment = CardCatalog.SuperMagnet.Id;
            Check(g.PlayCode(0, 0).Success && Choice(g, 0).Move.Apply(g, 0).Success);
            Check(rival.EquipmentCounters == 1 && rival.Equipment == CardCatalog.SuperMagnet.Id && p.Equipment == CardCatalog.Pendant.Id);
            Check(p.AttachedCards.Count == 0 && p.Deck.Count == 3 && g.State.Phase == Phase.Actions);
        }),
        ("Dame ese equipo: conserva las cartas anexadas al transferir el último Tótem y recupera equipo propio", () =>
        {
            var (g, _) = Fixture();
            var p = g.State.Active;
            var rival = g.State.Players[1];
            rival.Equipment = CardCatalog.Totem.Id;
            rival.MarkForeignFieldCard(CardCatalog.Totem.Id);
            rival.EquipmentCovers = [CardCatalog.GoldenTotem.Id];
            rival.ZahiraHost = new(1, FieldZone.Equipment, 0, CardCatalog.Totem.Id);
            Check(g.PlayCode(0, 0).Success && Choice(g, 0).Move.Apply(g, 0).Success);
            Check(p.Equipment == CardCatalog.Totem.Id && rival.Equipment == null && rival.ForeignFieldCards == null);
            Check(p.EquipmentCovers!.Single() == CardCatalog.GoldenTotem.Id && rival.EquipmentCovers == null);
            Check(p.ForeignFieldCards!.Single() == CardCatalog.GoldenTotem.Id && rival.ZahiraHost == new FieldTarget(0, FieldZone.Equipment, 0, CardCatalog.Totem.Id));
        }),
        ("Dame ese equipo: PC guarda un dado antes de anexar el Ataque y el último Ataque agota el mazo", () =>
        {
            var (g, rng) = Fixture();
            g.State.Active.Relikario = RelikarioCatalog.YunoLv2;
            g.State.Active.Deck = [CardCatalog.Attack.Id];
            g.State.Players[1].Equipment = CardCatalog.Pc.Id;
            rng.EnqueueDie(4);
            Check(g.PlayCode(0, 0).Success && Choice(g, 0).Move.Apply(g, 0).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.State.Active.Equipment == CardCatalog.Pc.Id && g.State.Active.AttachedCards.Count == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            rng.EnqueueDie(1);
            Check(g.ResolveDieReroll(0, false).Success && g.State.PendingDieRoll?.Purpose == DiePurpose.Recycling);
            Check(g.State.Active.EquipmentDie == 4 && g.State.Active.AttachedCards.Single() == CardCatalog.Attack.Id);
            Check(g.ResolveDieReroll(0, false).Success && g.State.Phase == Phase.RecycleChoice);
        }),
        ("Dame ese equipo: se puede activar con Relikia y consume el Código de la memoria", () =>
        {
            var (g, _) = Fixture();
            var p = g.State.Active;
            p.Hand.Clear(); p.Memory.Add(CardCatalog.TakeEquipment.Id); p.Relikia = 400;
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && Choice(g, 0).Move.Apply(g, 0).Success);
            Check(p.Relikia == 0 && p.ActionsLeft == 0 && p.CorruptedMemory.Single() == CardCatalog.TakeEquipment.Id && p.AttachedCards.Count == 1);
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[], []], Decks = [[], []], Bonuses = [null, null], StartingSeat = actor
        }, rng);
        game.State.Phase = Phase.Actions;
        game.State.Active.Dexterity = 0;
        game.State.Active.Hand.Add(CardCatalog.TakeEquipment.Id);
        game.State.Active.Deck.AddRange([CardCatalog.Attack.Id, CardCatalog.Bonfire.Id, CardCatalog.Dodge.Id]);
        game.State.Players[1 - actor].Equipment = CardCatalog.Sniper.Id;
        return (game, rng);
    }

    private static DuelOption Choice(GameEngine game, int actor) => DuelOptions.For(game, actor).Single(o => o.Move.Kind == DuelMoveKind.Choose);

    private static void Check(bool value)
    {
        if (!value) throw new Exception("Regla de ¡Dame ese equipo! incorrecta.");
    }
}

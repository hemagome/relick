using Relick.Core;
using System.Text.Json;

// A card taken from the rival stays the rival's: whenever it reaches a memory, it is its owner's.
static class CardOwnershipTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Dueño: la proyección solo revela dueños de ranuras visibles y el paquete privado los conserva", () =>
        {
            var (g, _) = Fixture([CardCatalog.Attack.Id], [CardCatalog.Senshuni.Id, CardCatalog.Pendant.Id, CardCatalog.Bonfire.Id]);
            Steal(g, 0, 0);
            var own = DuelSnapshot.For(g, 0, 1);
            Check(own.Players[0].HandOwners is { } ownOwners && ownOwners.SequenceEqual(new int?[] { 0, 1 }));
            var rival = DuelSnapshot.For(g, 1, 1);
            Check(rival.Players[0].HandOwners == null && rival.Players[0].Hand.All(id => id == null));
            var publicBoard = DuelSnapshot.For(g, -1, 1);
            Check(publicBoard.Players.All(p => p.HandOwners == null));
            var packet = DuelPackets.Combine(new DuelBoardPacket { Board = publicBoard },
                new DuelPrivateData { MySeat = 0, OwnHand = own.Players[0].Hand, OwnHandOwners = own.Players[0].HandOwners }, []);
            Check(packet.Players[0].HandOwners!.SequenceEqual(new int?[] { 0, 1 }));
            g.State.Players[1].RevealHandCard(0);
            var revealed = DuelSnapshot.For(g, -1, 2);
            Check(revealed.Players[1].HandOwners is { } visible && visible[0] == 1 && visible[1] == null);
            var json = JsonSerializer.Serialize(own, DuelJson.Default.DuelSnapshot);
            var restored = JsonSerializer.Deserialize(json, DuelJson.Default.DuelSnapshot)!;
            Check(restored.Players[0].HandOwners!.SequenceEqual(new int?[] { 0, 1 }));
        }),
        ("Dueño: un código tomado con Magnet vuelve a la memoria del rival al jugarlo", () =>
        {
            var (g, rng) = Fixture([CardCatalog.Attack.Id], [CardCatalog.Attack.Id, CardCatalog.Senshuni.Id]);
            var total = Count(g); Steal(g, 0, 1);
            Check(g.State.Players[0].IsForeignHandCard(1) && !g.State.Players[0].IsForeignHandCard(0));
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); g.State.Phase = Phase.Actions;
            var cues = DuelVisualEvents.Capture(g);
            Check(g.PlayCode(0, 1).Success && g.State.Phase == Phase.Actions);
            Check(g.State.Players[1].Memory.Single() == CardCatalog.Senshuni.Id && g.State.Players[0].Memory.Count == 0);
            Check(cues.Single(c => c.Kind == DuelVisualKind.Pile).ToRivalMemory && g.State.Players[0].ForeignHandSlots == null && Count(g) == total);
        }),
        ("Dueño: distingue la copia tomada de la propia al descartar", () =>
        {
            var (g, _) = Fixture([CardCatalog.Attack.Id], [CardCatalog.Attack.Id]);
            Steal(g, 0, 0); var p = g.State.Players[0]; var rival = g.State.Players[1];
            Discard(g, 0, 0); Check(p.Memory.Single() == CardCatalog.Attack.Id && rival.Memory.Count == 0 && p.IsForeignHandCard(0));
            Discard(g, 0, 0); Check(p.Memory.Count == 1 && rival.Memory.Single() == CardCatalog.Attack.Id && p.ForeignHandSlots == null);
        }),
        ("Dueño: una carta recuperada por su dueño deja de ser ajena", () =>
        {
            var (g, _) = Fixture([], [CardCatalog.Senshuni.Id]);
            Steal(g, 0, 0); g.State.CurrentPlayer = 1; Steal(g, 1, 0);
            var owner = g.State.Players[1]; Check(owner.ForeignHandSlots == null && g.State.Players[0].ForeignHandSlots == null);
            Discard(g, 1, 0); Check(owner.Memory.Single() == CardCatalog.Senshuni.Id && g.State.Players[0].Memory.Count == 0);
        }),
        ("Dueño: un equipo o habilidad ajenos vuelven a la memoria del rival al salir del campo", () =>
        {
            var (g, rng) = Fixture([CardCatalog.Pendant.Id], [CardCatalog.DiamondHelmet.Id, CardCatalog.Reverse.Id]);
            Steal(g, 0, 0); Steal(g, 0, 0); var p = g.State.Players[0]; var rival = g.State.Players[1];
            g.State.Phase = Phase.Combat; Check(g.Learn(0, 2).Success && p.ForeignFieldCards!.Single() == CardCatalog.Reverse.Id);
            g.State.Phase = Phase.Actions; Check(g.Equip(0, 1).Success && p.Equipment == CardCatalog.DiamondHelmet.Id && rival.Memory.Count == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Players[0]; rival = g.State.Players[1];
            Check(g.Equip(0, 0).Success && g.State.Phase == Phase.DestructionResponse);
            Check(g.ResolveDestructionProtection(0, 0).Success && p.Equipment == CardCatalog.DiamondHelmet.Id);
            Check(rival.Memory.Single() == CardCatalog.Reverse.Id && p.Memory.Single() == CardCatalog.Pendant.Id);
            p.Hand.Add(CardCatalog.Pendant.Id); p.ActionsLeft = 2;
            Check(g.Equip(0, 0).Success && p.Equipment == CardCatalog.Pendant.Id && p.ForeignFieldCards == null);
            Check(rival.Memory.SequenceEqual([CardCatalog.Reverse.Id, CardCatalog.DiamondHelmet.Id]) && p.Memory.Single() == CardCatalog.Pendant.Id);
        }),
        ("Dueño: Cambio de Mentes conserva el dueño de cada mano", () =>
        {
            var (g, _) = Fixture([CardCatalog.MindSwap.Id, CardCatalog.Senshuni.Id], [CardCatalog.Pendant.Id]);
            g.State.Turn = 2; var p = g.State.Players[0]; var rival = g.State.Players[1];
            Check(g.PlayCode(0, 0).Success && p.Hand.Single() == CardCatalog.Pendant.Id && rival.Hand.Single() == CardCatalog.Senshuni.Id);
            Check(p.IsForeignHandCard(0) && rival.IsForeignHandCard(0) && p.Memory.Single() == CardCatalog.MindSwap.Id);
            Discard(g, 0, 0); Discard(g, 1, 0);
            Check(rival.Memory.Single() == CardCatalog.Pendant.Id && p.Memory.SequenceEqual([CardCatalog.MindSwap.Id, CardCatalog.Senshuni.Id]));
        }),
        ("Dueño: Disquete toma una carta de la memoria rival y esta regresa allí", () =>
        {
            var (g, _) = Fixture([CardCatalog.FloppyDisk.Id], []);
            var p = g.State.Players[0]; var rival = g.State.Players[1]; rival.Memory.Add(CardCatalog.Senshuni.Id);
            Check(g.PlayCode(0, 0).Success && g.ResolveMemoryRecovery(0, 1, 0).Success && p.IsForeignHandCard(0));
            Check(g.PlayCode(0, 0).Success && rival.Memory.Single() == CardCatalog.Senshuni.Id && p.Memory.Single() == CardCatalog.FloppyDisk.Id);
        }),
        ("Dueño: un Disquete ajeno se excluye a sí mismo en la memoria de su dueño", () =>
        {
            var (g, _) = Fixture([], [CardCatalog.FloppyDisk.Id]);
            Steal(g, 0, 0); g.State.Phase = Phase.Actions; var p = g.State.Players[0]; var rival = g.State.Players[1];
            p.Memory.Add(CardCatalog.Attack.Id);
            Check(g.PlayCode(0, 0).Success && rival.Memory.Single() == CardCatalog.FloppyDisk.Id);
            Check(!g.ResolveMemoryRecovery(0, 1, 0).Success && g.ResolveMemoryRecovery(0, 0, 0).Success);
            Check(p.Hand.Single() == CardCatalog.Attack.Id && p.ForeignHandSlots == null);
        }),
        ("Dueño: una Mira de Sniper ajena descartada puede equiparse y vuelve a su dueño", () =>
        {
            foreach (var equip in new[] { true, false })
            {
                var (g, _) = Fixture([CardCatalog.Pendant.Id], [CardCatalog.Sniper.Id]);
                Steal(g, 0, 0); var p = g.State.Players[0]; var rival = g.State.Players[1];
                g.State.Phase = Phase.Discard; g.State.DiscardsRemaining = 1; g.State.DiscardPlayer = 0;
                Check(g.Discard(0, 1).Success && g.State.Phase == Phase.SniperChoice && rival.Memory.Single() == CardCatalog.Sniper.Id);
                Check(g.ResolveSniper(0, equip).Success && rival.Memory.Count == 0 && p.ForeignFieldCards!.Single() == CardCatalog.Sniper.Id);
                Check(equip ? p.Equipment == CardCatalog.Sniper.Id : p.Exchange.Single() == CardCatalog.Sniper.Id);
                if (!equip) continue;
                g.State.Phase = Phase.Actions; p.ActionsLeft = 2;
                Check(g.Equip(0, 0).Success && rival.Memory.Single() == CardCatalog.Sniper.Id && p.Memory.Count == 0 && p.ForeignFieldCards == null);
            }
        }),
        ("Dueño: Cartucho de Re-carga devuelve las cartas ajenas a la memoria del rival", () =>
        {
            var (g, _) = Fixture([CardCatalog.ReloadCartridge.Id, CardCatalog.Attack.Id], [CardCatalog.Senshuni.Id, CardCatalog.Pendant.Id]);
            Steal(g, 0, 0); g.State.Phase = Phase.Actions; g.State.Turn = 2; var total = Count(g);
            var p = g.State.Players[0]; var rival = g.State.Players[1];
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && p.Hand.Count == 4 && rival.Hand.Count == 4);
            Check(p.Memory.SequenceEqual([CardCatalog.ReloadCartridge.Id, CardCatalog.Attack.Id]));
            Check(rival.Memory.Order().SequenceEqual(new[] { CardCatalog.Senshuni.Id, CardCatalog.Pendant.Id }.Order()));
            Check(p.ForeignHandSlots == null && Count(g) == total);
        }),
        ("Dueño: Resetear la Mente devuelve una carta ajena al mazo de su dueño", () =>
        {
            var (g, _) = Fixture([CardCatalog.MindReset.Id], [CardCatalog.Senshuni.Id]);
            Steal(g, 0, 0); g.State.Phase = Phase.Actions; var p = g.State.Players[0]; var rival = g.State.Players[1];
            p.Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Attack.Id]); var deck = p.Deck.Count;
            Check(g.PlayCode(0, 0).Success && g.ChooseMindResetTarget(0, 0).Success);
            Check(g.ChooseMindResetMemory(0, 0, 0).Success && g.ChooseMindResetMemory(0, 0, 1).Success && g.ResolveMindReset(0, 0).Success);
            Check(rival.Deck.Contains(CardCatalog.Senshuni.Id) && !p.Deck.Contains(CardCatalog.Senshuni.Id) && p.Deck.Count == deck + 2 - 1);
            Check(p.ForeignHandSlots == null);
        }),
    };

    static (GameEngine, ScriptedRandom) Fixture(string[] hand, string[] rivalHand)
    {
        var rng = new ScriptedRandom(); var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = 0, Hands = [hand.ToList(), rivalHand.ToList()],
            Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList()],
            Bonuses = [null, null]
        }, rng);
        g.State.Phase = Phase.Actions;
        foreach (var player in g.State.Players) player.Dexterity = 0;
        return (g, rng);
    }
    // The critical effect of Magnet Shuffle is the simplest way to take a card from the rival's hand.
    static void Steal(GameEngine g, int actor, int handIndex)
    {
        var resume = g.State.Phase; g.State.Phase = Phase.MagnetChoice;
        Check(g.ResolveMagnet(actor, false, handIndex).Success); g.State.Phase = resume;
    }
    static void Discard(GameEngine g, int actor, int handIndex)
    {
        var resume = g.State.Phase; g.State.Phase = Phase.Discard; g.State.DiscardsRemaining = 1; g.State.DiscardPlayer = actor;
        Check(g.Discard(actor, handIndex).Success); g.State.Phase = resume;
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count);
    static void Check(bool value) { if (!value) throw new Exception("Regla de dueño de carta incorrecta."); }
}

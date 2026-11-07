using Relick.Core;
using System.Text.Json;

static class MindSwapTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Cambio de Mentes: Jugada, sin COMBO, una acción y una copia en veinte cartas", () =>
        {
            var c = CardCatalog.MindSwap;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Play && c.ActionCost == 1 && c.PrCost == 0 && !c.IsCombo && c.EarliestTurn == 2);
            Check(CardCatalog.TrainingDeck().Count == 20 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
        }),
        ("Cambio de Mentes: prohíbe el primer turno global, también desde memoria, sin cobrar costes", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var g = Fixture(actor); var p = g.State.Active; p.Memory.Add(CardCatalog.MindSwap.Id); p.Relikia = 400;
                g.State.Turn = 1; var before = g.SaveSnapshot();
                Check(!g.PlayCode(actor, 0).Success && !g.CanReplayMemoryCode(actor, 0) && !g.UseRelikiaEffect(actor, RelikiaEffect.ReplayCode, 0).Success);
                Check(g.SaveSnapshot() == before && DuelOptions.For(g, actor).All(o => o.Move.Kind != DuelMoveKind.PlayCode &&
                    !(o.Move.Kind == DuelMoveKind.Relikia && o.Move.Value == (int)RelikiaEffect.ReplayCode)));
                g.State.Turn = 2; Check(g.PlayCode(actor, 0).Success); // Second player's first personal turn is legal.
            }
        }),
        ("Cambio de Mentes: intercambia manos completas y desiguales, incluidas vacías, sin tocar otras zonas", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            foreach (var (ownSize, rivalSize) in new[] { (0, 0), (0, 3), (4, 0), (2, 5), (40, 60) })
            {
                var g = Fixture(actor); var p = g.State.Active; var rival = g.State.Players[1 - actor];
                p.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, ownSize)); rival.Hand.AddRange(Enumerable.Repeat(CardCatalog.Destiny.Id, rivalSize));
                p.Equipment = CardCatalog.DiamondHelmet.Id; rival.Bonus = CardCatalog.Senshuni.Id; p.Skills.Add(CardCatalog.Magnet.Id);
                p.Relikia = 777; p.Health = 4; var total = Count(g); var ownDeck = p.Deck.ToArray(); var rivalDeck = rival.Deck.ToArray();
                var cues = DuelVisualEvents.Capture(g);
                Check(g.PlayCode(actor, 0).Success && p.Hand.Count == rivalSize && p.Hand.All(id => id == CardCatalog.Destiny.Id));
                Check(rival.Hand.Count == ownSize && rival.Hand.All(id => id == CardCatalog.Attack.Id));
                Check(p.Memory.Single() == CardCatalog.MindSwap.Id && p.ActionsLeft == 1 && rival.ActionsLeft == 2 && p.Relikia == 777 && p.Health == 4);
                Check(p.Deck.SequenceEqual(ownDeck) && rival.Deck.SequenceEqual(rivalDeck) && p.Equipment == CardCatalog.DiamondHelmet.Id &&
                    rival.Bonus == CardCatalog.Senshuni.Id && p.Skills.Single() == CardCatalog.Magnet.Id && total == Count(g) && g.State.Phase == Phase.Actions);
                Check(!p.Recycled && !rival.Recycled && cues.All(c => c.Kind is not (DuelVisualKind.Draw or DuelVisualKind.Shuffle or DuelVisualKind.Die)));
                var transfers = cues.Where(c => c.Kind == DuelVisualKind.HandTransfer).ToArray();
                Check(transfers.Length == (ownSize > 0 ? 1 : 0) + (rivalSize > 0 ? 1 : 0));
                if (ownSize > 0) Check(transfers.Any(c => c.Player == actor && c.Target == 1 - actor && c.Value == ownSize));
                if (rivalSize > 0) Check(transfers.Any(c => c.Player == 1 - actor && c.Target == actor && c.Value == rivalSize));
            }
        }),
        ("Cambio de Mentes: valida propietario, fase y acciones antes de intercambiar", () =>
        {
            var g = Fixture(); var before = g.SaveSnapshot();
            Check(!g.PlayCode(1, 0).Success && !g.PlayCode(0, -1).Success && g.SaveSnapshot() == before);
            g.State.Phase = Phase.Combat; before = g.SaveSnapshot(); Check(!g.PlayCode(0, 0).Success && g.SaveSnapshot() == before);
            g.State.Phase = Phase.Actions; g.State.ActionsLeft = 0; before = g.SaveSnapshot(); Check(!g.PlayCode(0, 0).Success && g.SaveSnapshot() == before);
        }),
        ("Cambio de Mentes: las revelaciones siguen las copias, sin revelar duplicados ni robos posteriores", () =>
        {
            var g = Fixture(); var p = g.State.Active; var rival = g.State.Players[1];
            p.Hand.Clear(); p.Hand.AddRange(new[] { CardCatalog.Attack.Id, CardCatalog.Magnet.Id, CardCatalog.MindSwap.Id, CardCatalog.Attack.Id, CardCatalog.Destiny.Id });
            rival.Hand.AddRange(new[] { CardCatalog.ExtraLife.Id, CardCatalog.ExtraLife.Id, CardCatalog.Magnet.Id });
            p.RevealHandCard(3); rival.RevealHandCard(1);
            Check(g.PlayCode(0, 2).Success && p.RevealedHandSlots.SequenceEqual(new[] { 1 }) && rival.RevealedHandSlots.SequenceEqual(new[] { 2 }));
            var ownView = DuelSnapshot.For(g, 0, 1); var rivalView = DuelSnapshot.For(g, 1, 1);
            Check(ownView.Players[1].Hand.SequenceEqual(new string?[] { null, null, CardCatalog.Attack.Id, null }));
            Check(rivalView.Players[0].Hand.SequenceEqual(new string?[] { null, CardCatalog.ExtraLife.Id, null }));
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), new SeededRandom(1)); p = g.State.Active;
            p.Hand.Add(CardCatalog.ExtraLife.Id); Check(!p.IsHandCardRevealed(3));
            g.State.Phase = Phase.Discard; g.State.DiscardPlayer = 0; g.State.DiscardsRemaining = 1;
            Check(g.Discard(0, 1).Success && p.RevealedHandSlots.Count == 0);
        }),
        ("Cambio de Mentes: Error cancela sin intercambiar; Caja de Cartón no responde a Jugada", () =>
        {
            var g = Fixture(); var p = g.State.Active; var rival = g.State.Players[1]; p.Hand.Add(CardCatalog.Attack.Id);
            rival.Hand.AddRange(new[] { CardCatalog.LoadError.Id, CardCatalog.Magnet.Id }); rival.Equipment = CardCatalog.CardboardBox.Id;
            var cues = DuelVisualEvents.Capture(g);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse && g.PlayCode(1, 0).Success);
            Check(p.ActionsLeft == 2 && p.Hand.Single() == CardCatalog.Attack.Id && rival.Hand.Single() == CardCatalog.Magnet.Id &&
                rival.Equipment == CardCatalog.CardboardBox.Id && cues.All(c => c.Kind != DuelVisualKind.HandTransfer));
            g = Fixture(); g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && !g.ResolveCodeResponse(1, true).Success);
        }),
        ("Cambio de Mentes: contraerror intercambia solo las cartas restantes y mantiene cada memoria", () =>
        {
            var g = Fixture(); var p = g.State.Active; var rival = g.State.Players[1];
            p.Hand.AddRange(new[] { CardCatalog.Attack.Id, CardCatalog.LoadError.Id });
            rival.Hand.AddRange(new[] { CardCatalog.LoadError.Id, CardCatalog.ExtraLife.Id, CardCatalog.Magnet.Id });
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success && g.PlayCode(0, 1).Success);
            Check(p.Hand.SequenceEqual(new[] { CardCatalog.ExtraLife.Id, CardCatalog.Magnet.Id }) && rival.Hand.Single() == CardCatalog.Attack.Id);
            Check(p.Memory.SequenceEqual(new[] { CardCatalog.MindSwap.Id, CardCatalog.LoadError.Id }) && rival.Memory.Single() == CardCatalog.LoadError.Id);
            Check(p.ActionsLeft == 0 && rival.ActionsLeft == 2 && g.State.Phase == Phase.Actions);
        }),
        ("Cambio de Mentes: Relikia permite mano vacía, corrompe el código y cobra dos acciones y 400 PR", () =>
        {
            foreach (var cancel in new[] { false, true })
            {
                var g = Fixture(); var p = g.State.Active; var rival = g.State.Players[1]; p.Hand.Clear(); p.Memory.Add(CardCatalog.MindSwap.Id); p.Relikia = 400;
                rival.Hand.Add(CardCatalog.ExtraLife.Id); if (cancel) rival.Hand.Add(CardCatalog.LoadError.Id);
                Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success);
                if (cancel)
                {
                    Check(g.PlayCode(1, 1).Success && p.Hand.Count == 0 && rival.Hand.Single() == CardCatalog.ExtraLife.Id);
                    Check(p.Relikia == 400 && p.ActionsLeft == 2 && p.Memory.Single() == CardCatalog.MindSwap.Id && p.CorruptedMemory.Count == 0);
                }
                else Check(p.Hand.Single() == CardCatalog.ExtraLife.Id && rival.Hand.Count == 0 && p.CorruptedMemory.Single() == CardCatalog.MindSwap.Id && p.Relikia == 0 && p.ActionsLeft == 0);
            }
        }),
        ("Cambio de Mentes: recuperación pendiente y ejecución no consumen azar ni filtran cartas en señales", () =>
        {
            var g = Fixture(); g.State.Active.Hand.Add(CardCatalog.Destiny.Id); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success); var pending = g.SaveSnapshot();
            var random = new DuelRandom(); random.Record(_ => throw new Exception("El intercambio no utiliza azar."));
            var clone = GameEngine.FromSnapshot(pending, random); var cues = DuelVisualEvents.Capture(clone);
            Check(clone.PassErrorResponse(1).Success && g.PassErrorResponse(1).Success && g.SaveSnapshot() == clone.SaveSnapshot() && random.Finish().Count == 0);
            var json = JsonSerializer.Serialize(cues);
            Check(!json.Contains(CardCatalog.Destiny.Id) && !json.Contains(CardCatalog.LoadError.Id));
            Check(DuelSnapshot.For(clone, 0, 2).Players[1].Hand.Single() == null && DuelSnapshot.For(clone, 1, 2).Players[0].Hand.Single() == null);
        }),
        ("Cambio de Mentes: la mano vacía recibida roba tres al comenzar el siguiente turno", () =>
        {
            var g = Fixture(); g.State.Players[1].Hand.Add(CardCatalog.ExtraLife.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Players[1].Hand.Count == 0 && g.Advance(0).Success);
            Check(g.State.CurrentPlayer == 1 && g.State.Active.Hand.Count == 3 && g.State.Phase == Phase.Combat);
        })
    };
    static GameEngine Fixture(int actor = 0)
    {
        var g = new GameEngine(42); g.State.CurrentPlayer = actor; g.State.Phase = Phase.Actions; g.State.Turn = 2;
        g.State.Active.Hand.Add(CardCatalog.MindSwap.Id); return g;
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count +
        p.AttachedCards.Count + p.Exchange.Count + (p.Equipment == null ? 0 : 1) + (p.Bonus == null ? 0 : 1));
    static void Check(bool value) { if (!value) throw new Exception("Regla de Cambio de Mentes no satisfecha."); }
}

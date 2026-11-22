using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// `--cosmetics-only`: each player's sleeve backs their deck, face-down corrupted cards and, for the rival,
    /// the hidden hand; each roll shows the roller's die. Both seats, desktop and a compact layout.
    /// </summary>
    internal async Task CheckCosmetics()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
            Hands = [[CardCatalog.Attack.Id, CardCatalog.Senshuni.Id], [CardCatalog.Attack.Id, CardCatalog.Senshuni.Id, CardCatalog.Bonfire.Id]],
            Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null],
            Sleeves = ["oro", "violeta"], Dice = ["obsidiana", "coral"] }, new SeededRandom(37));
        game.State.Turn = 2; game.State.Phase = Phase.Actions;
        foreach (var p in game.State.Players) p.CorruptedMemory.Add(CardCatalog.Attack.Id);
        foreach (var size in new[] { new Vector2I(1820, 1024), new(1024, 768) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var snapshot = DuelSnapshot.For(game, seat, 1200);
            ResetAnimations(); PresentTestSnapshot(snapshot); await Layout();
            string Own(int relative) => snapshot.Players[relative == 0 ? seat : 1 - seat].Sleeve;
            for (var relative = 0; relative < 2; relative++)
            {
                var sleeve = SleeveArt.Back(Own(relative));
                Check(_root!.FindChild("DeckStack" + relative, true, false) is DeckStack { BackTexture: var deck } && deck == sleeve,
                    $"El mazo no muestra la mica de su dueño ({size}, asiento {seat}).");
                Check(_root.FindChild("MemoryButton" + relative, true, false) is MemoryZoneButton { DisplayedFaceUp: 0, DisplayedFaceDown: 1, BackTexture: var corrupt } && corrupt == sleeve,
                    "Las cartas corruptas no muestran la mica de su dueño.");
            }
            var rivalBacks = _opponentCards.Select(card => card.BackTexture).ToArray();
            Check(rivalBacks.Length > 0 && rivalBacks.All(back => back == SleeveArt.Back(Own(1))), "La mano rival no muestra la mica del rival.");
            Check(SleeveArt.Back("oro") != SleeveArt.Back("violeta") && SleeveArt.Back(CosmeticCatalog.DefaultSleeve) != SleeveArt.Back("oro"),
                "Las micas no se distinguen entre sí.");

            // Each roll: the roller's die, even when the other player rolls during the turn.
            foreach (var roller in new[] { 0, 1 })
            {
                ResetAnimations();
                PresentDieCue(new DuelVisualEvent { Kind = DuelVisualKind.Die, Player = roller, Value = 4 });
                Check(_die.IsRolling && _die.Style == game.State.Players[roller].Dice, $"La tirada de {roller} no usa su dado ({_die.Style}).");
            }
            // The opening initiative: both players roll at once, each with their own die.
            ResetAnimations();
            _openingInitiative = new(3, 5, 1, snapshot.Players.Select(p => p.Name).ToArray(), snapshot.MySeat,
                snapshot.Players.Select(p => p.Dice).ToArray());
            _initiativeStarted = false; _initiativeLanded = false; _initiativeResultRemaining = 1.5;
            UpdateOpeningInitiative(0);
            Check(Enumerable.Range(0, 2).All(i => _initiativeDice[i].IsRolling && _initiativeDice[i].Style == game.State.Players[i].Dice),
                $"La iniciativa no usa el dado de cada jugador ({_initiativeDice[0].Style}, {_initiativeDice[1].Style}).");
            ResetOpeningInitiative();
            ResetAnimations();
            if (seat == 0 && size.X == 1820) await Capture($"cosmetics-board-{size.X}x{size.Y}");
        }
        GD.Print("PVP_COSMETICS_PASS · mica de cada dueño en mazo, corruptas y mano rival, dado de quien tira y de cada jugador en la iniciativa; dos tamaños y ambos asientos.");
    }
}

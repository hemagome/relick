using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// `--board-reading-only`: hand counts beside their own fans and clear of every field zone, the final
    /// blow and the second fall to 0 said in words, and the next teachable space lit only while teaching is
    /// authorized. Desktop widths from 4:3 to the widest canvas, a compact layout, both seats.
    /// </summary>
    internal async Task CheckBoardReading()
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
        GameEngine Fixture(int ownCards, int rivalCards)
        {
            var game = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
                Hands = [Enumerable.Repeat(CardCatalog.Attack.Id, ownCards - 1).Prepend(CardCatalog.Magnet.Id).ToList(),
                    Enumerable.Repeat(CardCatalog.Attack.Id, rivalCards).ToList()],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null] }, new SeededRandom(17));
            game.State.Turn = 2; game.State.Phase = Phase.Combat;
            game.State.Players[0].Skills.Add(CardCatalog.Destiny.Id);
            game.State.Players[0].Relikia = 300;
            return game;
        }
        Rect2 Union(IEnumerable<Control> controls) => controls.Select(c => c.GetGlobalRect()).Aggregate((a, b) => a.Merge(b));
        IEnumerable<Control> FieldZones() => _root!.FindChildren("*", "Control", true, false).OfType<Control>()
            .Where(c => c is ZoneCardButton or MemoryZoneButton or SkillTile or DeckStack or AttachmentStack && c.IsVisibleInTree());

        foreach (var size in new[] { new Vector2I(1440, 1024), new(1638, 1024), new(1820, 1024), new(2280, 1024), new(1024, 768) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var desktop = ArenaViewport.Desktop(this);
            foreach (var seat in new[] { 0, 1 })
            foreach (var (own, rival) in new[] { (7, 5), (3, 0) })
            {
                var game = Fixture(own, rival);
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 800)); await Layout();
                var screen = new Rect2(Vector2.Zero, Size);
                var counters = new[] { ("OwnHandCounter", "HandCard"), ("OpponentHandCounter", "OpponentHandCard") };
                foreach (var (name, cardName) in counters)
                {
                    var counter = (Control)_root!.FindChild(name, true, false);
                    var rect = counter.GetGlobalRect();
                    Check(screen.Encloses(rect), $"{name} queda fuera de la pantalla en {size}: {rect}.");
                    var overlap = FieldZones().FirstOrDefault(zone => zone.GetGlobalRect().Intersects(rect));
                    Check(overlap == null, $"{name} se dibuja sobre {overlap?.Name} en {size}, asiento {seat}.");
                    if (!desktop) continue;
                    var cards = _root.FindChildren(cardName + "*", "Button", true, false).OfType<Control>().ToArray();
                    if (cards.Length == 0) continue;
                    // Global rects ignore the fan's tilt, so the measured edge can differ by a few units.
                    var fan = Union(cards);
                    Check(rect.Position.X >= fan.End.X - 10 && rect.Position.X <= fan.End.X + 30,
                        $"{name} no queda junto a su abanico en {size}: contador {rect.Position.X}, cartas hasta {fan.End.X}.");
                }
            }

            // Lives: the final blow at 0 and, once healed, the warning that a second fall ends the duel.
            var lives = Fixture(5, 4);
            lives.State.Players[0].Health = 0; lives.State.Players[0].HasReachedZero = true;
            lives.State.Players[1].Health = 3; lives.State.Players[1].HasReachedZero = true;
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(lives, 0, 801)); await Layout();
            foreach (var meter in _life) meter.AdvancePlayback(10);
            Check(_life[0].FinalBlow && _life[0].TooltipText.Contains("Golpe final") && _life[0].Stacked == desktop,
                $"El golpe final no se anuncia en {size}.");
            Check(!_life[1].FinalBlow && _life[1].ReachedZero && _life[1].TooltipText.Contains("si vuelve a 0"),
                $"No se avisa de que el rival ya llegó a 0 en {size}.");
            if (size.X is 1820 or 2280) await Capture($"board-reading-{size.X}x{size.Y}");

            // The next teachable space is lit only while the server offers teaching.
            foreach (var pr in new[] { 300, 0 })
            {
                var lesson = Fixture(5, 4); lesson.State.Players[0].Relikia = pr;
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(lesson, 0, 802)); await Layout();
                var offered = _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Learn);
                Check(offered == (pr > 0), "El fixture no controla si se puede enseñar.");
                var tiles = _root!.FindChildren("Skill*_*", "Button", true, false).OfType<SkillTile>().ToArray();
                var lit = tiles.Where(t => t.NextLesson).Select(t => t.Name.ToString()).ToArray();
                Check(offered ? lit.SequenceEqual(["Skill0_1"]) : lit.Length == 0,
                    $"Espacios iluminados incorrectos en {size} con {pr} PR: {string.Join(",", lit)}.");
            }
            var rivalView = Fixture(5, 4);
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(rivalView, 1, 803)); await Layout();
            Check(!_root!.FindChildren("Skill*_*", "Button", true, false).OfType<SkillTile>().Any(t => t.NextLesson),
                $"El rival ve iluminado un espacio que no puede usar en {size}.");
        }
        GD.Print("PVP_BOARD_READING_PASS · contadores junto a su abanico y fuera de las zonas, golpe final y segunda caída anunciados, siguiente espacio enseñable solo si se autoriza; cinco tamaños y ambos asientos.");
    }
}

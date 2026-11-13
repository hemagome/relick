using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// Reports and captures every card chooser at the supported canvas sizes:
    /// `./jugar.sh -- --pvp-ui-test --choice-preview-dir=DIR`. Each line tells whether the
    /// confirming button is fully on screen.
    /// </summary>
    internal async Task<int> CaptureChoicePreview(string? directory, Vector2I[]? only = null)
    {
        async Task Layout() { for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        if (directory != null) System.IO.Directory.CreateDirectory(directory);
        var rendering = directory != null && DisplayServer.GetName() != "headless";
        var failures = 0;
        async Task Report(string name, Func<Control?> confirm)
        {
            await Layout();
            var button = confirm();
            var screen = new Rect2(Vector2.Zero, Size);
            var rect = button?.GetGlobalRect() ?? new Rect2();
            var ok = button != null && button.IsVisibleInTree() && screen.Grow(1).Encloses(rect) && RectVisible(button, rect);
            var minimum = _root!.GetCombinedMinimumSize();
            if (!ok) failures++;
            if (directory != null || !ok) GD.Print($"CHOICE {(ok ? "OK  " : "FAIL")} {name} · botón {rect} · pantalla {Size} · mínimo {minimum}");
            if (!rendering) return;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory!, name + ".png"));
        }
        var sizes = only ?? (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--choice-sizes=")) is { } list
            ? list.Split('=', 2)[1].Split(',').Select(v => v.Split('x')).Select(v => new Vector2I(int.Parse(v[0]), int.Parse(v[1]))).ToArray()
            : new[] { new Vector2I(2280, 1024), new(1820, 1024), new(1440, 1024), new(1099, 700), new(1024, 768),
                new(1024, 600), new(960, 540), new(844, 390), new(700, 390) });
        foreach (var size in sizes)
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var tag = $"{size.X}x{size.Y}";

            foreach (var count in new[] { 2, 6 })
            {
                var room = new RoomInfo("UITEST00", 0, "Ana", "Beto", true);
                var skills = new InitialSkillInfo(false, "", CardCatalog.All.Values.Where(c => c.Kind == CardKind.Skill).Select(c => c.Id).Take(count).ToList());
                ResetAnimations();
                PresentInitialSkillChoice(room, skills, true, false, new PreparationInfo { DeckCountOne = 15, DeckCountTwo = 15 });
                await Layout();
                SelectInitialSkillCandidate(skills.Options[0]);
                await Report($"habilidad-inicial-{count}-{tag}", () => InitialSkillConfirmButton);
            }
            _preparing = _choosingOpeningSkill = false;

            foreach (var memory in new[] { 4, 12 })
            {
                var disk = GameEngine.FromOpening(new DuelOpening
                {
                    Names = ["Ana", "Beto"], StartingSeat = 0,
                    Hands = [[CardCatalog.Attack.Id, CardCatalog.Bonfire.Id], [CardCatalog.Attack.Id]],
                    Decks = [Enumerable.Repeat("attack", 12).ToList(), Enumerable.Repeat("attack", 12).ToList()], Bonuses = [null, null]
                }, new SeededRandom(43));
                disk.State.Phase = Phase.Actions; disk.State.Active.Hand[0] = CardCatalog.FloppyDisk.Id;
                var pool = new[] { CardCatalog.Bonfire.Id, CardCatalog.Dodge.Id, CardCatalog.Attack.Id, CardCatalog.Reroll.Id, CardCatalog.TimeBomb.Id, CardCatalog.Pendant.Id };
                for (var i = 0; i < memory; i++) { disk.State.Players[0].Memory.Add(pool[i % pool.Length]); disk.State.Players[1].Memory.Add(pool[(i + 3) % pool.Length]); }
                if (!disk.PlayCode(0, 0).Success) throw new Exception("No comienza la recuperación de Disquete.");
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(disk, 0, 950)); await Layout();
                SelectCard(0);
                await Report($"disquete-{memory}-{tag}", () => _moves.GetChildren().OfType<Button>().FirstOrDefault());
            }

            var recycle = new GameEngine(43); recycle.State.CurrentPlayer = 0; recycle.State.Phase = Phase.RecycleChoice;
            recycle.State.PendingDieRoll = new(0, DiePurpose.Recycling, 6, Phase.Combat);
            foreach (var p in recycle.State.Players) { p.Hand.Clear(); p.Hand.Add(CardCatalog.Attack.Id); }
            recycle.State.Players[0].Memory.AddRange(Enumerable.Repeat(new[] { CardCatalog.ExtraLife.Id, CardCatalog.Destiny.Id, CardCatalog.DiamondHelmet.Id,
                CardCatalog.Magnet.Id, CardCatalog.Pendant.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id }, 2).SelectMany(x => x));
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(recycle, 0, 960)); await Layout();
            await Report($"reciclaje-{tag}", () => _moves.GetChildren().OfType<Button>().FirstOrDefault());
        }
        if (directory != null) GD.Print($"CHOICE_PREVIEW_DONE · {failures} fallos");
        _preparing = _choosingOpeningSkill = false;
        return failures;
    }

    /// <summary>Every chooser keeps its confirming button on screen, including the wide board that has to scroll.</summary>
    internal async Task CheckChoiceBounds()
    {
        var sizes = new[] { new Vector2I(1820, 1024), new(1600, 900), new(1366, 768), new(1280, 720), new(1024, 768), new(844, 390), new(700, 390) };
        var failures = await CaptureChoicePreview(null, sizes);
        if (failures > 0) throw new Exception($"{failures} elecciones dejan su botón de confirmación fuera de la pantalla.");
        GD.Print("PVP_CHOICE_BOUNDS_PASS · habilidad inicial, Disquete y reciclaje con su botón visible en siete tamaños, incluido el tablero ancho con desplazamiento.");
    }
}

using Godot;
using Relick.Core;
using System.Text.Json;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckPublicHistory()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            var dir = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (dir == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(dir);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png"));
        }
        GameEngine Fixture() => GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
            Hands = [[CardCatalog.Senshuni.Id, CardCatalog.LoadError.Id], [CardCatalog.LoadError.Id, CardCatalog.LoadError.Id]],
            Decks = [Enumerable.Repeat(CardCatalog.DiamondHelmet.Id, 20).ToList(), Enumerable.Repeat(CardCatalog.Agility.Id, 20).ToList()], Bonuses = [null, null] }, new SeededRandom(61));
        var embedded = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(1280, 480), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                var g = Fixture(); g.State.Phase = Phase.Actions; g.State.Turn = 2;
                var steps = new[] { (0, new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 0 }),
                    (1, new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 0 }),
                    (0, new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 0 }),
                    (1, new DuelMove { Kind = DuelMoveKind.PassError }) };
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
                for (var i = 0; i < steps.Length; i++)
                {
                    Check(steps[i].Item2.Apply(g, steps[i].Item1).Success, "No avanza la cadena de la prueba de historial.");
                    var s = DuelSnapshot.For(g, seat, (ulong)(700 + i));
                    // Exercise the same JSON projection as the online board, including recovery.
                    s = JsonSerializer.Deserialize(JsonSerializer.Serialize(s, DuelJson.Default.DuelSnapshot), DuelJson.Default.DuelSnapshot)!;
                    ResetAnimations(); PresentTestSnapshot(s); await Layout();
                    Check(_resolutionSource!.Text == "Ana · Senshuni", "La resolución atribuye la carta a quien respondió o al asiento equivocado.");
                    Check(i < 3 ? _resolutionStage!.Text.StartsWith("RESPUESTA") : _resolutionStage!.Text == "RESULTADO", "La resolución confunde respuesta y resultado.");
                    if (i == 2) Check(_resolutionResponses!.Text.Contains("Beto · Error de Carga") && _resolutionResponses.Text.Contains("Ana · Error de Carga"), "Se pierde el contraerror de la cadena.");
                    CheckPendingCodeBounds();
                    if (seat == 0 && i is 2 or 3)
                    {
                        _arenaScroll?.EnsureControlVisible(i == 2 ? _resolutionResponses! : _resolutionMessage!); await Layout();
                        await Capture($"resolution-{i}-{size.X}x{size.Y}");
                    }
                }
                var before = g.SaveSnapshot(); var ids = _snapshot!.History!.Select(e => e.Id).ToArray();
                SelectCard(0); await Layout(); var selected = _selected.ToArray();
                _arenaScroll?.EnsureControlVisible(_historyButton!); await Layout(); GuiInputTestDriver.Click(_historyButton!); await Layout();
                var dialog = _historyDialog;
                Check(dialog is { Visible: true }, "El botón no abre el historial.");
                var rows = dialog!.FindChildren("HistoryEntry*", "PanelContainer", true, false).OfType<PanelContainer>().ToArray();
                Check(rows.Length == 4 && rows[0].Name == "HistoryEntry4", "El historial duplica filas o no presenta primero la última jugada.");
                var allText = string.Join("\n", dialog.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text));
                Check(allText.Contains("anula Error de Carga") && allText.Contains("roba 2") && !allText.Contains(CardCatalog.DiamondHelmet.Name) && !allText.Contains(CardCatalog.Agility.Name), "El historial pierde resultados o revela robos/manos privadas.");
                if (seat == 0 && size.X is 1440 or 700) await Capture($"history-{size.X}x{size.Y}");
                GuiInputTestDriver.Click(dialog.GetOkButton()); await Layout();
                Check(!GodotObject.IsInstanceValid(dialog) && _selected.SetEquals(selected) && g.SaveSnapshot() == before, "Consultar o cerrar el historial ejecuta una jugada o pierde la selección.");
                var recovered = GameEngine.FromSnapshot(before, new SeededRandom(61));
                PresentTestSnapshot(DuelSnapshot.For(recovered, seat, 703)); await Layout(); Render(); await Layout();
                Check(_snapshot!.History!.Select(e => e.Id).SequenceEqual(ids), "Reconectar o reconstruir duplica las jugadas públicas.");
                _die.RollTo(6); UpdateResolutionPresentation();
                Check(!_resolutionMessage!.Visible && _historyButton!.Disabled && _resolutionStage!.Text == "RESOLVIENDO", "Se muestran resultados antes de terminar el dado.");
                ShowDuelHistory(); Check(_historyDialog == null, "Se abre un resultado futuro durante la animación.");
                _die.AdvancePlayback(DiceFace.RollSeconds + 2); UpdateResolutionPresentation();
                Check(!_historyButton!.Disabled && _resolutionMessage!.Visible, "La resolución no vuelve a habilitar el historial al terminar el dado.");
                ResetAnimations();
            }
        }
        finally { GetViewport().GuiEmbedSubwindows = embedded; }
        GD.Print("PVP_PUBLIC_HISTORY_PASS · activación, Error/contraerror, resultados, privacidad, entrada real, recuperación sin duplicados y espera de animaciones; cinco tamaños y ambos asientos.");
    }
}

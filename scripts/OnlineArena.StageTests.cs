using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckStageAnnouncements()
    {
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
        async Task Layout()
        {
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var game = PendingCodeFixture(0, CardCatalog.Bonfire.Id);
        var revision = 1000UL;
        DuelSnapshot Snapshot(Phase phase)
        {
            game.State.Phase = phase;
            return DuelSnapshot.For(game, 0, revision++);
        }

        var sizes = new[] { new Vector2I(1440, 1024), new(1024, 768), new(1280, 480), new(700, 390) };
        var stages = new[] { (Phase.Draw, "ROBO"), (Phase.Combat, "COMBATE"), (Phase.Actions, "ACCIONES"), (Phase.Final, "FINAL") };
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        var originalSize = GetWindow().Size;
        var originalCanvas = GetWindow().ContentScaleSize;
        var originalMotion = _stageBanner.ReducedMotion;
        _stageBanner.ReducedMotion = false;
        foreach (var size in sizes)
        {
            GetWindow().Size = size;
            GetWindow().ContentScaleSize = size;
            await Layout();
            var previous = Snapshot(Phase.Preparation);
            foreach (var (stage, title) in stages)
            {
                var snapshot = Snapshot(stage);
                PresentTestSnapshot(snapshot);
                await Layout();
                var indicator = (Control)_root!.FindChild("StageIndicator", true, false);
                var label = (Label)indicator.FindChild("CurrentStageTitle", true, false);
                Check(label.Text == title && new Rect2(Vector2.Zero, Size).Encloses(indicator.GetGlobalRect()),
                    $"El indicador no cabe o muestra otra etapa en {size}.");
                QueueStageAnnouncement(previous, snapshot, live: true);
                PresentQueuedStageAnnouncement();
                Check(_stageBanner.Current?.Stage == stage && _stageBanner.Visible && !IsAnimating,
                    $"El anuncio de {title} no aparece o bloquea la partida.");
                _stageBanner.SetProcess(false);
                _stageBanner._Process(0.32);
                if (capture != null && DisplayServer.GetName() != "headless" && size.X is 1440 or 700)
                {
                    DirAccess.MakeDirRecursiveAbsolute(capture);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"{capture}/stage-{stage.ToString().ToLowerInvariant()}-{size.X}x{size.Y}.png");
                }
                if (stage == Phase.Actions)
                {
                    var card = _handCards[0];
                    _hand!.EnsureControlVisible(card);
                    await Layout();
                    card.Disabled = false;
                    GuiInputTestDriver.Click(card);
                    Check(_selected.Contains(0), "El anuncio captura el clic que iba a la mano.");
                }
                _stageBanner._Process(StageBanner.Duration);
                Check(!_stageBanner.Visible && !_stageBanner.IsProcessing(), "El anuncio se queda visible o procesando tras terminar.");
                previous = snapshot;
            }
        }

        var actions = Snapshot(Phase.Actions);
        var response = Snapshot(Phase.Actions);
        response.Phase = Phase.ErrorResponse;
        QueueStageAnnouncement(actions, response, live: true);
        PresentQueuedStageAnnouncement();
        Check(!_stageBanner.Visible, "Una respuesta dentro de la misma etapa repite el anuncio.");

        var final = Snapshot(Phase.Final);
        QueueStageAnnouncement(actions, final, live: false);
        PresentQueuedStageAnnouncement();
        Check(!_stageBanner.Visible, "La sincronización inicial reproduce una transición anterior.");
        _die.RollTo(4);
        QueueStageAnnouncement(actions, final, live: true);
        PresentQueuedStageAnnouncement();
        Check(!_stageBanner.Visible, "El anuncio tapa una tirada en curso.");
        _die.Value = 4;
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current?.Stage == Phase.Final, "Se pierde la etapa pendiente después de la tirada.");

        actions.ExtraActionStages = 1;
        _stageBanner.ReducedMotion = true;
        QueueStageAnnouncement(actions, Snapshot(Phase.Actions), live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Actions, Repeated: true }, "Doblador de Tiempo no anuncia la repetición.");
        _stageBanner._Process(StageBanner.Duration);
        Check(!_stageBanner.Visible, "El aviso con movimiento reducido no se cierra.");
        _stageBanner.ReducedMotion = originalMotion;
        ResetAnimations();
        GetWindow().Size = originalSize;
        GetWindow().ContentScaleSize = originalCanvas;
        await Layout();
        GD.Print("PVP_STAGE_PASS · cuatro etapas, clic durante el aviso, respuestas, reconexión, dados y repetición.");
    }
}

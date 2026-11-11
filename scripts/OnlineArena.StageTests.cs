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
        DuelSnapshot Snapshot(Phase phase, int seat = 0)
        {
            game.State.Phase = phase;
            return DuelSnapshot.For(game, seat, revision++);
        }

        var sizes = new[] { new Vector2I(1440, 1024), new(1024, 768), new(1280, 480), new(700, 390) };
        var stages = new[] { (Phase.Draw, "ROBO"), (Phase.Combat, "COMBATE"), (Phase.Actions, "ACCIONES"), (Phase.Final, "FINAL") };
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        var originalSize = GetWindow().Size;
        var originalCanvas = GetWindow().ContentScaleSize;
        var originalMotion = _stageBanner.ReducedMotion;
        _stageBanner.ReducedMotion = false;
        foreach (var seat in new[] { 0, 1 })
        foreach (var size in sizes)
        {
            var ownTurn = seat == game.State.CurrentPlayer;
            var accent = ownTurn ? RelickVisuals.State.Own : RelickVisuals.State.Rival;
            var owner = ownTurn ? "own" : "rival";
            GetWindow().Size = size;
            GetWindow().ContentScaleSize = size;
            await Layout();
            ResetStageBanner();
            var previous = Snapshot(Phase.Preparation, seat);
            foreach (var (stage, title) in stages)
            {
                var snapshot = Snapshot(stage, seat);
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
                Check(_stageBanner.Current is { } announcement && announcement.OwnTurn == ownTurn && announcement.Accent == accent,
                    $"El anuncio de {title} usa el color incorrecto para el asiento {seat}.");
                _stageBanner.SetProcess(false);
                _stageBanner._Process(0.32);
                if (capture != null && DisplayServer.GetName() != "headless" && size.X is 1440 or 700)
                {
                    DirAccess.MakeDirRecursiveAbsolute(capture);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"{capture}/stage-{stage.ToString().ToLowerInvariant()}-{owner}-{size.X}x{size.Y}.png");
                }
                if (stage == Phase.Actions && ownTurn)
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

        ResetStageBanner();
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
        _stageBanner._Process(StageBanner.Duration);

        actions.ExtraActionStages = 1;
        _stageBanner.ReducedMotion = true;
        QueueStageAnnouncement(actions, Snapshot(Phase.Actions), live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Actions, Repeated: true }, "Doblador de Tiempo no anuncia la repetición.");
        _stageBanner._Process(StageBanner.Duration);
        Check(!_stageBanner.Visible, "El aviso con movimiento reducido no se cierra.");
        ResetAnimations();
        GetWindow().Size = new(1440, 1024);
        GetWindow().ContentScaleSize = new(1440, 1024);
        await Layout();
        var turn = PendingCodeFixture(0, CardCatalog.Bonfire.Id);
        turn.State.Phase = Phase.Actions;
        var beforeAdvance = DuelSnapshot.For(turn, 0, revision++);
        var cues = DuelVisualEvents.Capture(turn);
        Check(turn.Advance(0).Success && turn.State.Phase == Phase.Combat && turn.State.CurrentPlayer == 1,
            "La prueba no reproduce un cierre y un robo automáticos.");
        var afterAdvance = DuelSnapshot.For(turn, 0, revision++);
        afterAdvance.Visuals = cues;
        PresentTestSnapshot(afterAdvance);
        Animate(cues, []);
        await Layout();
        QueueStageAnnouncement(beforeAdvance, afterAdvance, live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Final, OwnTurn: true },
            "Un cambio de turno automático omite Final y Robo.");
        for (var i = 0; i < 4; i++) UpdateFlights(0);
        Check(_activeFlight == null && _flights.Any(f => f.Event.Kind == DuelVisualKind.Draw),
            "Las cartas del nuevo turno salen antes del anuncio de Robo.");
        _stageBanner._Process(StageBanner.Duration);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Draw, OwnTurn: false } draw && draw.Turn == afterAdvance.Turn,
            "El robo automático no anuncia el turno del rival.");
        UpdateFlights(0.1);
        Check(_activeFlight?.Event.Kind == DuelVisualKind.Draw, "El anuncio de Robo retiene la carta.");
        _stageBanner._Process(StageBanner.Duration);
        PresentQueuedStageAnnouncement();
        Check(!_stageBanner.Visible, "Combate aparece antes de que llegue la carta.");
        UpdateFlights(1);
        Check(turn.Advance(1).Success, "No se puede continuar después del robo.");
        var nextActions = DuelSnapshot.For(turn, 0, revision++);
        QueueStageAnnouncement(afterAdvance, nextActions, live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Combat, OwnTurn: false }, "Avanzar rápido elimina el anuncio de Combate pendiente.");
        _stageBanner._Process(StageBanner.Duration);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Actions, OwnTurn: false }, "El orden de anuncios se pierde al avanzar.");

        ResetAnimations();
        var fullHand = PendingCodeFixture(0, CardCatalog.Attack.Id);
        var hand = fullHand.State.Active.Hand;
        hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, GameEngine.MaxHandSize + 1 - hand.Count));
        fullHand.State.Phase = Phase.Actions;
        var beforeFinal = DuelSnapshot.For(fullHand, 0, revision++);
        Check(fullHand.Advance(0).Success && fullHand.State.Phase == Phase.Final, "La prueba no abre el descarte final.");
        var needsDiscard = DuelSnapshot.For(fullHand, 0, revision++);
        QueueStageAnnouncement(beforeFinal, needsDiscard, live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Final, OwnTurn: true }, "El descarte no anuncia Final.");
        _stageBanner._Process(StageBanner.Duration);
        Check(fullHand.Discard(0, 0).Success && fullHand.State.CurrentPlayer == 1, "El descarte no cierra el turno.");
        QueueStageAnnouncement(needsDiscard, DuelSnapshot.For(fullHand, 0, revision++), live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Draw, OwnTurn: false }, "Terminar el descarte vuelve a anunciar Final.");

        ResetStageBanner();
        var recycling = PendingCodeFixture(0, CardCatalog.Attack.Id);
        recycling.State.Players[1].Deck.Clear();
        recycling.State.Players[1].Deck.Add(CardCatalog.Attack.Id);
        recycling.State.Players[1].Memory.Add(CardCatalog.Attack.Id);
        var beforeRecycling = DuelSnapshot.For(recycling, 0, revision++);
        Check(recycling.Advance(0).Success && recycling.State.Phase == Phase.RecycleChoice, "No se prepara el reciclaje al iniciar el turno.");
        var recycleChoice = DuelSnapshot.For(recycling, 0, revision++);
        QueueStageAnnouncement(beforeRecycling, recycleChoice, live: true);
        PresentQueuedStageAnnouncement();
        _stageBanner._Process(StageBanner.Duration);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Draw }, "Reciclar el mazo oculta el comienzo de Robo.");
        _stageBanner._Process(StageBanner.Duration);
        PresentQueuedStageAnnouncement();
        Check(!_stageBanner.Visible, "Se anuncia Combate mientras todavía se debe reciclar.");
        Check(recycling.ResolveRecycling(1, [0]).Success && recycling.State.Phase == Phase.Combat, "No se reanuda el turno después de reciclar.");
        QueueStageAnnouncement(recycleChoice, DuelSnapshot.For(recycling, 0, revision++), live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Combat }, "Se pierde el anuncio de Combate al completar el reciclaje.");

        ResetStageBanner();
        var preparation = Snapshot(Phase.Preparation);
        preparation.Turn = 0;
        var firstCombat = Snapshot(Phase.Combat);
        firstCombat.Turn = 1;
        QueueStageAnnouncement(preparation, firstCombat, live: true);
        PresentQueuedStageAnnouncement();
        Check(_stageBanner.Current is { Stage: Phase.Draw, Turn: 1 }, "El primer turno omite el robo posterior a la iniciativa.");
        _stageBanner.ReducedMotion = originalMotion;
        ResetAnimations();
        GetWindow().Size = originalSize;
        GetWindow().ContentScaleSize = originalCanvas;
        await Layout();
        GD.Print("PVP_STAGE_PASS · cuatro etapas, ambos asientos, turnos automáticos, descarte final, reciclaje, iniciativa, reconexión y repetición.");
    }
}

using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private StageBanner _stageBanner = null!;
    private readonly Queue<StageAnnouncement> _stageAnnouncements = new();
    private int _lastDrawAnnouncementTurn = -1, _lastFinalAnnouncementTurn = -1;
    private int? _turnAwaitingCombat;

    private void BuildStageBanner()
    {
        _stageBanner = new StageBanner
        {
            Name = "StageBanner",
            ReducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
                ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool()
        };
        AddChild(_stageBanner);
        _stageBanner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void QueueStageAnnouncement(DuelSnapshot? previous, DuelSnapshot current, bool live)
    {
        if (!live || current.Winner != null)
        {
            ResetStageBanner();
            return;
        }
        if (previous == null) return;

        var previousStage = StageAppearance.Current(previous);
        var stage = StageAppearance.Current(current);
        if (current.Turn == previous.Turn + 1)
        {
            // The engine can resolve Final and the next turn's draw in a single revision.
            if (previousStage is not (Phase.Preparation or Phase.Final) && _lastFinalAnnouncementTurn != previous.Turn)
                EnqueueStageAnnouncement(Phase.Final, previous);
            if (stage != Phase.Draw) EnqueueStageAnnouncement(Phase.Draw, current);
            _turnAwaitingCombat = current.Phase is Phase.Draw or Phase.Combat ? null : current.Turn;
        }

        var resumedCombat = false;
        if (_turnAwaitingCombat == current.Turn)
        {
            // A recycling or equipment decision can pause the turn before combat.
            if (current.Phase is not (Phase.Draw or Phase.Combat)) return;
            resumedCombat = current.Phase == Phase.Combat;
            _turnAwaitingCombat = null;
        }
        if (!stage.IsTurnStage()) return;

        var sameStage = previousStage == stage;
        var sameTurn = previous.Turn == current.Turn && previous.CurrentPlayer == current.CurrentPlayer;
        var repeated = sameStage && sameTurn && ExtraStages(current, stage) < ExtraStages(previous, stage);
        if (!repeated && !resumedCombat && sameStage && sameTurn) return;
        if (!repeated && (stage == Phase.Draw && _lastDrawAnnouncementTurn == current.Turn ||
            stage == Phase.Final && _lastFinalAnnouncementTurn == current.Turn)) return;

        EnqueueStageAnnouncement(stage, current, repeated);
    }

    private void EnqueueStageAnnouncement(Phase stage, DuelSnapshot snapshot, bool repeated = false)
    {
        if (stage == Phase.Draw) _lastDrawAnnouncementTurn = snapshot.Turn;
        if (stage == Phase.Final) _lastFinalAnnouncementTurn = snapshot.Turn;
        _stageAnnouncements.Enqueue(new(stage, snapshot.Turn, snapshot.CurrentPlayer == snapshot.MySeat, repeated));
    }

    private static int ExtraStages(DuelSnapshot snapshot, Phase stage) => stage switch
    {
        Phase.Draw => snapshot.ExtraDrawStages,
        Phase.Combat => snapshot.ExtraCombatStages,
        Phase.Actions => snapshot.ExtraActionStages,
        _ => snapshot.ExtraFinalStages
    };

    private void PresentQueuedStageAnnouncement()
    {
        if (_openingInitiative != null || _die.IsBusy || _die.IsPresented)
        {
            if (_stageBanner.Visible) _stageBanner.Stop();
            return;
        }
        if (_stageBanner.Visible || !_stageAnnouncements.TryPeek(out var announcement)) return;
        if (announcement.Stage == Phase.Combat && announcement.Turn == _snapshot?.Turn &&
            (_activeFlight?.Event.Kind == DuelVisualKind.Draw || _flights.Any(f => f.Event.Kind == DuelVisualKind.Draw))) return;
        _stageBanner.Play(_stageAnnouncements.Dequeue());
    }

    private bool WaitForDrawAnnouncement(DuelVisualEvent cue) => cue.Kind == DuelVisualKind.Draw &&
        _snapshot is { Turn: > 1 } current && cue.Player == current.CurrentPlayer &&
        _stageAnnouncements.Any(a => a.Stage == Phase.Draw && a.Turn == current.Turn);

    private void ResetStageBanner()
    {
        _stageAnnouncements.Clear();
        _lastDrawAnnouncementTurn = _lastFinalAnnouncementTurn = -1;
        _turnAwaitingCombat = null;
        _stageBanner?.Stop();
    }
}

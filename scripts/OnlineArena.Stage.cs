using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private StageBanner _stageBanner = null!;
    private StageAnnouncement? _pendingStageAnnouncement;

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
        var stage = StageAppearance.Current(current);
        if (!live || stage is not (Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final))
        {
            ResetStageBanner();
            return;
        }
        if (previous == null) return;

        var sameStage = StageAppearance.Current(previous) == stage;
        var sameTurn = previous.Turn == current.Turn && previous.CurrentPlayer == current.CurrentPlayer;
        var repeated = sameStage && sameTurn && ExtraStages(current, stage) < ExtraStages(previous, stage);
        if (sameStage && sameTurn && !repeated) return;

        _stageBanner.Stop();
        _pendingStageAnnouncement = new(stage, current.Turn, current.CurrentPlayer == current.MySeat, repeated);
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
        if (_pendingStageAnnouncement is not { } announcement) return;
        _pendingStageAnnouncement = null;
        _stageBanner.Play(announcement);
    }

    private void ResetStageBanner()
    {
        _pendingStageAnnouncement = null;
        _stageBanner?.Stop();
    }
}

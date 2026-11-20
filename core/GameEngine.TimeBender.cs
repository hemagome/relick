namespace Relick.Core;

public sealed partial class GameEngine
{
    private Phase? RepeatableStage(int actor)
    {
        if (actor != State.CurrentPlayer || State.IsOver || State.PendingTurnBuff != null || State.PendingReload != null ||
            State.PendingSniper != null || State.PendingDestruction != null || MindResetPending() ||
            State.PendingDieRoll?.Purpose == DiePurpose.Recycling) return null;
        return State.Phase switch
        {
            var stage when stage.IsTurnStage() => stage,
            Phase.AttackRerollChoice or Phase.DefenseChoice or Phase.MagnetChoice or Phase.EffectDamageChoice => Phase.Combat,
            Phase.DieRerollChoice => State.PendingDieRoll?.ResumePhase is { } resume && resume.IsStageBeforeFinal() ? resume : null,
            Phase.CodeResponse or Phase.PointsChoice or Phase.MemoryRecoveryChoice => Phase.Actions,
            Phase.Decision => State.PendingDecision is { AllowsCombos: true } decision && decision.ResumePhase.IsStageBeforeFinal() ? decision.ResumePhase : null,
            _ => null
        };
    }

    private bool HasTimeBenderForDraw()
    {
        var p = State.Active;
        return !p.AutoPassCombos && p.Hand.Any(id => CardCatalog.All.TryGetValue(id, out var card) && card.RepeatStage &&
            AvailableActions(State.CurrentPlayer) >= card.ActionCost && p.Relikia >= card.PrCost && p.Hand.Count >= card.DiscardCost + 1);
    }

    private void FinishDrawStage(bool explicitStage)
    {
        State.Phase = explicitStage || HasTimeBenderForDraw() ? Phase.Draw : Phase.Combat;
        PresentPendingArea();
    }

    private void ResolveRepeatedStage()
    {
        var frame = _activeActivation!; var stage = State.PendingTurnBuff!.RepeatedStage!.Value;
        switch (stage)
        {
            case Phase.Draw: State.ExtraDrawStages++; break;
            case Phase.Combat: State.ExtraCombatStages++; break;
            case Phase.Actions: State.ExtraActionStages++; break;
            case Phase.Final: State.ExtraFinalStages++; break;
        }
        State.LastCombat = $"{State.Players[frame.Actor].Name} repetirá su etapa de {StageName(stage)} al terminarla con Doblador de Tiempo.";
        Log(State.LastCombat);
        FinishTurnBuff(frame);
    }

    private bool ConsumeRepeatedStage(Phase stage)
    {
        var p = State.Active;
        switch (stage)
        {
            case Phase.Draw when State.ExtraDrawStages > 0:
                State.ExtraDrawStages--; Log($"{p.Name} repite su etapa de robo."); DrawAtTurnStart(explicitStage: true); return true;
            case Phase.Combat when State.ExtraCombatStages > 0:
                State.ExtraCombatStages--; State.AttackedThisTurn = false; State.LearnedThisTurn = false;
                p.BonusEffectUsed = false; p.ExchangeEffectsUsed = null;
                break;
            case Phase.Actions when State.ExtraActionStages > 0:
                State.ExtraActionStages--; p.ActionsLeft = 2; break;
            case Phase.Final when State.ExtraFinalStages > 0:
                State.ExtraFinalStages--; State.RepeatedFinalAwaitingAdvance = true; break;
            default: return false;
        }
        State.Phase = stage;
        State.LastCombat = $"{p.Name} repite su etapa de {StageName(stage)} sin cambiar de turno."; Log(State.LastCombat);
        if (stage == Phase.Actions) BeginActionStartEffects(State.CurrentPlayer);
        return true;
    }

    private static string StageName(Phase stage) => stage switch
    { Phase.Draw => "robo", Phase.Combat => "combate", Phase.Actions => "acciones", _ => "final" };
}

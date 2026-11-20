namespace Relick.Core;

/// <summary>
/// Duels saved in a per-card phase that a card's decision has since replaced wait on that decision once loaded, so the
/// former phase needs no code of its own. A turn buff (Estrella del Fontanero, Doblador de Tiempo) sets the interrupted
/// state aside, so a former phase may also wait there; both places are converted.
/// </summary>
public sealed partial class GameEngine
{
    private void ConvertLegacyPhases()
    {
        // The former Tótem de Oro phase kept an empty placement record; the decision opens the placement itself.
        var formerGoldenTotem = State.Phase == Phase.GoldenTotemChoice || State.PendingTurnBuff?.ResumePhase == Phase.GoldenTotemChoice;
        if (LegacyDecision(State.Phase, _activeActivation, State.DestroyActor, State.PendingMindReset) is { } decision)
        {
            State.PendingDecision = decision;
            State.Phase = Phase.Decision;
            State.PendingTotemDamage = null; State.DestroyActor = null; State.PendingMindReset = null; State.PendingZahiraPlacement = null;
        }
        if (State.PendingTurnBuff is { } buff && LegacyDecision(buff.ResumePhase, TurnBuffParentFrame(), buff.DestroyActor, buff.MindReset) is { } stashed)
        {
            buff.Decision = stashed;
            buff.ResumePhase = Phase.Decision;
            buff.DestroyActor = null; buff.MindReset = null;
        }
        if (formerGoldenTotem) State.PendingGoldenTotem = null;
    }

    /// <summary>The activation a turn buff interrupted, which owned the state the buff set aside.</summary>
    private EngineActivation? TurnBuffParentFrame()
    {
        var frame = _activeActivation;
        while (frame != null && frame.Kind != ActivationKind.TurnBuff) frame = frame.Parent;
        return frame?.Parent;
    }

    /// <summary>The decision that replaced <paramref name="phase"/>, for the activation that was waiting in it; null for any other phase.</summary>
    private PendingDecision? LegacyDecision(Phase phase, EngineActivation? frame, int? destroyActor, PendingMindReset? mindReset) => phase switch
    {
        Phase.TotemChoice when State.PendingTotemDamage is { } totem => SalvationTotemEffect.LethalDecision(totem),
        Phase.DeckSearchChoice when frame is { Kind: ActivationKind.Code } search && search.CardId == CardCatalog.KonamyCode.Id =>
            KonamyCodeEffect.SearchDecision(search.Actor, search.ResumePhase),
        Phase.GoldenTotemChoice when frame is { Kind: ActivationKind.Code } gold && gold.CardId == CardCatalog.GoldenTotem.Id =>
            GoldenTotemEffect.ShowDecision(gold.Actor, gold.ResumePhase),
        Phase.DestroyChoice when destroyActor is int actor && frame is { Kind: ActivationKind.Code } code && code.CardId == CardCatalog.BlueEgg.Id =>
            BlueEggEffect.TargetDecision(actor, code.ResumePhase),
        Phase.DestroyChoice when destroyActor is int actor && frame is { Kind: ActivationKind.Code } code && code.CardId == CardCatalog.TimeBomb.Id =>
            TimeBombEffect.TargetDecision(actor, code.ResumePhase),
        Phase.MindResetChoice when mindReset != null => MindResetEffect.LegacyDecision(State, mindReset),
        Phase.ZahiraTarget when State.PendingZahiraPlacement is { } zahira =>
            ZahiraFlairEffect.PlacementDecision(zahira.Actor, zahira.Creating, zahira.Slot, zahira.ResumePhase),
        _ => null
    };
}

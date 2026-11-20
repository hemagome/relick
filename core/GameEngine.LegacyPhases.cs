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
        if (LegacyDecision(State.Phase, _activeActivation) is { } decision)
        {
            State.PendingDecision = decision;
            State.Phase = Phase.Decision;
            State.PendingTotemDamage = null;
        }
        if (State.PendingTurnBuff is { } buff && LegacyDecision(buff.ResumePhase, TurnBuffParentFrame()) is { } stashed)
        {
            buff.Decision = stashed;
            buff.ResumePhase = Phase.Decision;
        }
    }

    /// <summary>The activation a turn buff interrupted, which owned the state the buff set aside.</summary>
    private EngineActivation? TurnBuffParentFrame()
    {
        var frame = _activeActivation;
        while (frame != null && frame.Kind != ActivationKind.TurnBuff) frame = frame.Parent;
        return frame?.Parent;
    }

    /// <summary>The decision that replaced <paramref name="phase"/>, for the activation that was waiting in it; null for any other phase.</summary>
    private PendingDecision? LegacyDecision(Phase phase, EngineActivation? frame) => phase switch
    {
        Phase.TotemChoice when State.PendingTotemDamage is { } totem => SalvationTotemEffect.LethalDecision(totem),
        Phase.DeckSearchChoice when frame is { Kind: ActivationKind.Code } search && search.CardId == CardCatalog.KonamyCode.Id =>
            KonamyCodeEffect.SearchDecision(search.Actor, search.ResumePhase),
        _ => null
    };
}

namespace Relick.Core;

public sealed class TargetResponse
{
    public int SourcePlayer { get; set; }
    public DecisionChoice? Selected { get; set; }
    public bool Played { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool LostTarget { get; set; }
}

public sealed partial class GameEngine
{
    private bool WaitingForTargetResponse(int actor) => State.Phase == Phase.Decision &&
        State.PendingDecision is { Decider: var decider, TargetResponse: { Played: false } } && decider == actor;

    private bool TryOfferTargetResponse(int actor, int target, string source, DecisionChoice? choice = null,
        EngineContinuation? continuation = null)
    {
        if (target == actor || !ComboResponsesEnabled(target) || AvailableActions(target) == 0) return false;
        var responseCards = PlayableCards(target).Where(card => CardEffects.For(card.Id) is ITargetResponse).ToArray();
        if (responseCards.Length == 0) return false;

        var previous = State.PendingDecision;
        var resumePhase = State.Phase;
        State.PendingDecision = new PendingDecision
        {
            Source = source, Kind = DecisionKind.Confirm, Decider = target, AllowsCombos = true,
            ResumePhase = resumePhase, Suspended = previous, Continuation = continuation,
            TargetResponse = new TargetResponse { SourcePlayer = actor, Selected = choice },
            Instruction = "Responde con un COMBO o deja que el efecto rival se resuelva."
        };
        State.Phase = Phase.Decision;
        if (responseCards.Any(card => CodeUnavailableReason(target, card.Index) == null)) return true;

        State.PendingDecision = previous;
        State.Phase = resumePhase;
        return false;
    }

    private bool FinishPlayedTargetResponse()
    {
        if (State.Phase != Phase.Decision || State.PendingDecision is not { TargetResponse.Played: true } decision)
            return false;
        for (var frame = _activeActivation; frame != null; frame = frame.Parent)
            if (frame.RespondsToTarget) { _activeActivation = frame.Parent; break; }
        ResumeTargetedEffect(decision);
        return true;
    }

    private void ResumeTargetedEffect(PendingDecision response)
    {
        State.PendingDecision = response.Suspended;
        State.Phase = response.ResumePhase;
        if (response.TargetResponse!.Selected is not { } selected)
        {
            Continue(response.Continuation);
            return;
        }

        var original = response.Suspended!;
        var choices = DecisionChoices(original.Decider);
        var live = response.TargetResponse.LostTarget ? null : choices.FirstOrDefault(choice => selected.Field is { } field
            ? field == choice.Field
            : choice.Value == selected.Value && choice.Owner == selected.Owner && choice.CardId == selected.CardId);
        if (live != null) ApplyDecision(original, live, allowTargetResponse: false);
        else if (choices.Count == 0) RecheckDecision();
        else State.LastCombat = $"{original.Title()} · Elige otro objetivo disponible.";
    }

    private void ShiftTargetedHandChoice(int owner, int removed)
    {
        for (var decision = State.PendingDecision; decision != null; decision = decision.Suspended)
        {
            if (decision.TargetResponse is not { } response) continue;
            var field = response.Selected?.Field ?? decision.Continuation?.Target;
            if (field is not { Zone: FieldZone.Hand } || field.Owner != owner || field.Index < removed) continue;

            var remaining = field.Index == removed ? null : field with { Index = field.Index - 1 };
            if (response.Selected is { } choice)
            {
                if (remaining == null) response.LostTarget = true;
                else response.Selected = choice with { Field = remaining };
            }
            else decision.Continuation!.Target = remaining;
        }
    }
}

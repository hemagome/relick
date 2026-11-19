namespace Relick.Core;

public sealed partial class GameEngine
{
    private bool OfferGhostGift(int actor, EngineContinuation? rest)
    {
        if (ActiveEquipment(actor)?.Id != CardCatalog.ThiefGhost.Id) return false;
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.ThiefGhost.Id, Step = "gift", Kind = DecisionKind.ChooseCard, Decider = actor,
            Instruction = "Tu mano se revela al adversario. Entrégale un Objeto.",
            Continuation = rest
        });
        return true;
    }

    private IReadOnlyList<DecisionChoice> GhostGiftChoices(int actor)
    {
        var hand = State.Players[actor].Hand;
        var choices = hand.Select((id, index) => (id, index)).Where(c => CardCatalog.Get(c.id).CodeType == CodeType.Object)
            .Select(c => new DecisionChoice
            {
                CardId = c.id, Value = c.index, Label = $"Entregar {CardCatalog.Get(c.id).Name} · carta {c.index + 1}",
                Button = "Entregar", RequiresAcknowledgement = true, Public = $"entrega {CardCatalog.Get(c.id).Name} al adversario."
            }).ToArray();
        return choices.Length > 0 ? choices : [new()
        {
            Value = -1, Label = "Continuar sin entregar", RequiresAcknowledgement = true,
            Public = "muestra su mano sin Objetos."
        }];
    }

    private void ResolveGhostGift(PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value >= 0)
            MoveCardBetweenHands(State.Players[decision.Decider], State.Players[1 - decision.Decider], choice.Value);
        Continue(decision.Continuation);
    }

    private void FinishEffectDieWithGhost(DieEffect effect, int die)
    {
        if (die is >= 1 and <= 3 && OfferGhostGift(effect.Actor, new EngineContinuation
            { Kind = ContinuationKind.GhostEffectDie, Actor = effect.Actor, Value = die, SavedDieEffect = effect })) return;
        ResolveDieAfterGhost(effect, die);
    }

    private void ResolveDieAfterGhost(DieEffect effect, int die)
    {
        if (!TryOpenErrorAfterDie(effect, die)) ResolveDieEffect(effect, die);
    }
}

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

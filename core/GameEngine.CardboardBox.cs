namespace Relick.Core;

public sealed partial class GameEngine
{
    public bool CanCancelAttackWithBox(int actor) => actor >= 0 && actor < State.Players.Length &&
        State.Phase == Phase.DefenseChoice && State.DecisionPlayer == actor && State.PendingAttack?.Damage > 0 &&
        State.Players[actor].Equipment == CardCatalog.CardboardBox.Id;

    public CommandResult CancelAttackWithBox(int actor)
    {
        if (!CanCancelAttackWithBox(actor)) return CommandResult.Fail("No puedes cancelar este daño con Caja de Cartón.");
        var p = State.Players[actor];
        DestroyEquipment(p, new EngineContinuation { Kind = ContinuationKind.BoxDefense, Actor = actor });
        return CommandResult.Ok();
    }

    private void ActivateCode(int actor, CardDefinition card, bool fromMemory, int memoryIndex)
    {
        var frame = new EngineActivation(actor, card.Id, fromMemory ? 2 : card.ActionCost,
            card.PrCost + (fromMemory ? 400 : 0), State.Phase)
        { Kind = ActivationKind.Code, FromMemory = fromMemory, MemoryIndex = memoryIndex };
        if (card.DiscardForTurnBuff || card.RepeatStage) { BeginTurnBuff(frame); return; }
        BeginActivation(frame);
    }

    private void ActivateCodeAfterErrors(int actor, CardDefinition card, bool fromMemory, int memoryIndex)
    {
        if (card.CodeType == CodeType.Object && State.Players[1 - actor].Equipment == CardCatalog.CardboardBox.Id)
        {
            State.PendingCode = new(actor, card.Id, fromMemory, memoryIndex, State.Phase);
            State.Phase = Phase.CodeResponse;
            Log($"{State.Players[1 - actor].Name} puede responder a {card.Name} con Caja de Cartón.");
        }
        else
        {
            if (fromMemory) CorruptActivationCode(actor, memoryIndex);
            ResolveCode(State.Players[actor], card);
        }
    }

    public CommandResult ResolveCodeResponse(int actor, bool cancel)
    {
        if (State.Phase != Phase.CodeResponse || State.DecisionPlayer != actor || State.PendingCode == null)
            return CommandResult.Fail("No te corresponde responder a este código.");
        var pending = State.PendingCode;
        var card = CardCatalog.Get(pending.CardId);
        if (cancel && (State.Players[actor].Equipment != CardCatalog.CardboardBox.Id || card.Kind != CardKind.Code || card.CodeType != CodeType.Object))
            return CommandResult.Fail("Caja de Cartón solo puede cancelar códigos de tipo objeto.");
        State.Phase = pending.ResumePhase;
        if (cancel)
        {
            DestroyEquipment(State.Players[actor], new EngineContinuation { Kind = ContinuationKind.BoxCode, Actor = actor });
        }
        else
        {
            State.PendingCode = null;
            if (pending.FromMemory) CorruptActivationCode(pending.Actor, pending.MemoryIndex);
            ResolveCode(State.Players[pending.Actor], card);
        }
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }

    private void FinishBoxCode(int actor)
    {
        var pending = State.PendingCode!; var card = CardCatalog.Get(pending.CardId);
        State.PendingCode = null;
        RefundActivation(pending.Actor, pending.FromMemory ? 2 : card.ActionCost, card.PrCost + (pending.FromMemory ? 400 : 0));
        if (_activeActivation is { Kind: ActivationKind.TurnBuff } star)
        {
            Log($"{State.Players[actor].Name} cancela {card.Name} con Caja de Cartón; el descarte no vuelve a la mano.");
            FinishTurnBuff(star, returnDiscard: false); return;
        }
        _activeActivation = null;
        Log($"{State.Players[actor].Name} usa Caja de Cartón: cancela {card.Name}, que queda en la memoria de {State.Players[pending.Actor].Name}.");
        CompleteActivationIfFinished();
    }
}

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
        DestroyEquipment(p);
        Log($"{p.Name} usa Caja de Cartón y cancela el daño del ataque.");
        ResolveAttackDamage(true, "Caja de Cartón");
        return CommandResult.Ok();
    }

    private void ActivateCode(int actor, CardDefinition card, bool fromMemory, int memoryIndex)
    {
        var frame = new ActivationFrame(actor, card.Id, fromMemory ? 2 : card.ActionCost,
            card.PrCost + (fromMemory ? 400 : 0), State.Phase);
        frame.Resume = () => ActivateCodeAfterErrors(actor, card, fromMemory, memoryIndex);
        frame.Cancel = () =>
        {
            if (frame.ReplayCorrupted)
            {
                NotifyPileMove(State.Players[actor], card.Id, CardZone.Corrupted, frame.CorruptedIndex);
                State.Players[actor].CorruptedMemory.RemoveAt(frame.CorruptedIndex);
                State.Players[actor].Memory.Insert(Math.Min(memoryIndex, State.Players[actor].Memory.Count), card.Id);
            }
            ClearCodeDecisions(); State.Phase = frame.ResumePhase;
        };
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
        State.PendingCode = null;
        State.Phase = pending.ResumePhase;
        if (cancel)
        {
            DestroyEquipment(State.Players[actor]);
            RefundActivation(pending.Actor, pending.FromMemory ? 2 : card.ActionCost, card.PrCost + (pending.FromMemory ? 400 : 0));
            _activeActivation = null;
            // Played codes already entered their owner's memory. A memory activation
            // is left there, instead of being corrupted, when the activation is canceled.
            Log($"{State.Players[actor].Name} usa Caja de Cartón: cancela {card.Name}, que queda en la memoria de {State.Players[pending.Actor].Name}.");
        }
        else
        {
            if (pending.FromMemory) CorruptActivationCode(pending.Actor, pending.MemoryIndex);
            ResolveCode(State.Players[pending.Actor], card);
        }
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
}

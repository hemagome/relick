namespace Relick.Core;

public sealed record ActivationDiscard(string CardId, int MemoryIndex);

public sealed class PendingTurnBuff
{
    public string CardId { get; set; } = CardCatalog.PlumberStar.Id;
    public int RequiredDiscards { get; set; } = 1;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<ActivationDiscard>? PaidDiscards { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Phase? RepeatedStage { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int DiscardsPaid => PaidDiscards?.Count ?? (DiscardIndex >= 0 ? 1 : 0);
    public int Actor { get; set; }
    public Phase ResumePhase { get; set; }
    public PendingAttackRoll? AttackRoll { get; set; }
    public PendingDieRoll? DieRoll { get; set; }
    public DieEffect? DieEffect { get; set; }
    public PendingCode? Code { get; set; }
    public PendingPoints? Points { get; set; }
    public int? DestroyActor { get; set; }
    public PendingMindReset? MindReset { get; set; }
    public RelikiaChoice? Relikia { get; set; }
    public int Corruptions { get; set; }
    public int DiscardIndex { get; set; } = -1;
    public string DiscardId { get; set; } = "";
    public bool Completed { get; set; }
}

public sealed partial class GameEngine
{
    private bool HasTurnBuffInHand(int actor) => State.PendingTurnBuff == null && State.Players[actor].ActionsLeft > 0 &&
        State.Players[actor].Hand.Count >= 2 && State.Players[actor].Hand.Any(id => CardCatalog.Get(id).DiscardForTurnBuff);

    private void BeginTurnBuff(EngineActivation frame)
    {
        State.PendingTurnBuff = new()
        {
            CardId = frame.CardId, RequiredDiscards = Math.Max(1, CardCatalog.Get(frame.CardId).DiscardCost),
            RepeatedStage = CardCatalog.Get(frame.CardId).RepeatStage ? RepeatableStage(frame.Actor) : null,
            Actor = frame.Actor, ResumePhase = State.Phase, AttackRoll = State.PendingAttackRoll,
            DieRoll = State.PendingDieRoll, DieEffect = _pendingDieEffect, Code = State.PendingCode,
            Points = State.PendingPoints, DestroyActor = State.DestroyActor, MindReset = State.PendingMindReset,
            Relikia = State.PendingRelikia, Corruptions = State.CorruptionsRemaining
        };
        frame.Kind = ActivationKind.TurnBuff; frame.Parent = _activeActivation; frame.ResumePhase = Phase.TurnBuffResolving;
        _activeActivation = frame;
        State.PendingAttackRoll = null; State.PendingDieRoll = null; _pendingDieEffect = null;
        State.PendingCode = null; State.PendingPoints = null; State.DestroyActor = null;
        State.PendingMindReset = null; State.PendingRelikia = null; State.CorruptionsRemaining = 0;
        State.Phase = Phase.TurnBuffDiscard;
        State.LastCombat = $"{State.Players[frame.Actor].Name} · {CardCatalog.Get(frame.CardId).Name} · Elige {State.PendingTurnBuff.RequiredDiscards} carta(s) para descartar como coste.";
    }

    public CommandResult ResolveTurnBuffDiscard(int actor, int handIndex)
    {
        if (State.Phase != Phase.TurnBuffDiscard || State.PendingTurnBuff?.Actor != actor ||
            _activeActivation is not { Kind: ActivationKind.TurnBuff } frame || !InHand(State.Players[actor], handIndex))
            return CommandResult.Fail("Elige una carta de tu mano para pagar la activación pendiente.");
        var p = State.Players[actor]; var pending = State.PendingTurnBuff;
        var discarded = new ActivationDiscard(p.Hand[handIndex], p.Memory.Count);
        pending.PaidDiscards ??= []; pending.PaidDiscards.Add(discarded);
        if (pending.DiscardIndex < 0) { pending.DiscardId = discarded.CardId; pending.DiscardIndex = discarded.MemoryIndex; }
        SendHandToMemory(p, handIndex);
        Log($"{p.Name} descarta {CardCatalog.Get(discarded.CardId).Name} para activar {CardCatalog.Get(frame.CardId).Name}.");
        if (pending.DiscardsPaid < pending.RequiredDiscards)
        {
            State.LastCombat = $"{p.Name} · {CardCatalog.Get(frame.CardId).Name} · Faltan {pending.RequiredDiscards - pending.DiscardsPaid} descarte(s) del coste.";
            return CommandResult.Ok();
        }
        State.Phase = Phase.TurnBuffResolving;
        BeginActivation(frame);
        return CommandResult.Ok();
    }

    private void ResolveTurnBuff()
    {
        var frame = _activeActivation!; var p = State.Players[frame.Actor];
        p.StarEffects++;
        Log($"{p.Name} activa Estrella del Fontanero: no recibe daño y su daño se multiplica por {1 << p.StarEffects} hasta el final de este turno.");
        FinishTurnBuff(frame, returnDiscard: false);
    }

    private void FinishTurnBuff(EngineActivation frame, bool returnDiscard)
    {
        var pending = State.PendingTurnBuff!; var p = State.Players[pending.Actor];
        _activeActivation = frame; frame.CanInterrupt = false; State.Phase = Phase.TurnBuffResolving;
        State.PendingCode = null; State.PendingDestruction = null;
        pending.Completed = true;
        var discards = pending.PaidDiscards ?? [new ActivationDiscard(pending.DiscardId, pending.DiscardIndex)];
        if (returnDiscard)
        {
            foreach (var card in discards.OrderByDescending(card => card.MemoryIndex))
            { p.Memory.RemoveAt(card.MemoryIndex); ShiftSniperIndices(pending.Actor, card.MemoryIndex); }
            foreach (var card in discards)
            {
                p.Hand.Add(card.CardId);
                MemoryCardClaimed?.Invoke(pending.Actor, pending.Actor, card.MemoryIndex, p.Hand.Count - 1);
                Log($"Error de Carga devuelve {CardCatalog.Get(card.CardId).Name} a la mano de {p.Name}.");
            }
        }
        else
            BeginSniperChoices(pending.Actor, discards.Where(card => CardCatalog.Get(card.CardId).EquipOnDiscard)
                .Select(card => card.MemoryIndex - (frame.ReplayCorrupted && frame.MemoryIndex < card.MemoryIndex ? 1 : 0)));
        CompleteActivationIfFinished();
    }

    private void CompleteTurnBuff()
    {
        var pending = State.PendingTurnBuff!; var frame = _activeActivation;
        while (frame != null && frame.Kind != ActivationKind.TurnBuff) frame = frame.Parent;
        if (frame == null) throw new InvalidOperationException("Falta la activación con coste de descarte.");
        _activeActivation = frame.Parent;
        State.PendingTurnBuff = null; State.Phase = pending.ResumePhase;
        State.PendingAttackRoll = pending.AttackRoll; State.PendingDieRoll = pending.DieRoll; _pendingDieEffect = pending.DieEffect;
        State.PendingCode = pending.Code; State.PendingPoints = pending.Points; State.DestroyActor = pending.DestroyActor;
        State.PendingMindReset = pending.MindReset; State.PendingRelikia = pending.Relikia; State.CorruptionsRemaining = pending.Corruptions;
        CompleteAttackIfReady(); CompleteEndTurnIfReady();
    }
}

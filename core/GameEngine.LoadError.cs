namespace Relick.Core;
public sealed partial class GameEngine
{
    private readonly List<EngineActivation> _activationChain = new();
    private EngineActivation? _activeActivation;
    private Phase _errorResumePhase;
    private bool HasLoadError(int actor) => State.Players[actor].ActionsLeft > 0 && State.Players[actor].Hand.Any(id => CardCatalog.Get(id).CancelActivation);
    public bool CanPlayLoadError(int actor)
    {
        if (actor < 0 || actor >= 2) return false;
        if (State.Phase == Phase.ErrorResponse) return State.ErrorResponder == actor && _activationChain.Count > 0 && _activationChain[^1].Actor != actor;
        if (State.PendingDieRoll?.Purpose == DiePurpose.Recycling) return false;
        return _activeActivation is { CanInterrupt: true } frame && frame.Actor != actor &&
            State.Phase is Phase.DieRerollChoice or Phase.PointsChoice or Phase.DestroyChoice or Phase.CodeResponse;
    }
    private void BeginActivation(EngineActivation frame)
    {
        _activeActivation = frame;
        if (HasLoadError(1 - frame.Actor)) OpenErrorChain(frame, frame.ResumePhase);
        else { ResumeActivation(frame); CompleteActivationIfFinished(); }
    }
    private void OpenErrorChain(EngineActivation frame, Phase resume)
    {
        _activationChain.Clear(); _activationChain.Add(frame); _errorResumePhase = resume;
        State.ActivationChain.Clear(); State.ActivationChain.Add(new(frame.Actor, frame.CardId, frame.Actions, frame.Pr));
        State.ErrorResponder = 1 - frame.Actor; State.Phase = Phase.ErrorResponse;
    }
    private CommandResult PlayLoadError(int actor, int index)
    {
        if (!CanPlayLoadError(actor)) return CommandResult.Fail("No hay una activación enemiga que anular.");
        if (State.Phase != Phase.ErrorResponse)
        {
            var original = _activeActivation!;
            var resume = State.Phase;
            var suspended = new EngineActivation(original.Actor, original.CardId, original.Actions, original.Pr, resume)
            { ResumeMode = ActivationResume.RestorePhase, CancelPhase = original.CancelPhase ?? original.ResumePhase, Kind = original.Kind, Parent = original.Parent,
              ReplayCorrupted = original.ReplayCorrupted, CorruptedIndex = original.CorruptedIndex,
              FromMemory = original.FromMemory, MemoryIndex = original.MemoryIndex };
            OpenErrorChain(suspended, resume);
        }
        var p = State.Players[actor]; var card = CardCatalog.Get(p.Hand[index]);
        SendHandToMemory(p, index); p.ActionsLeft -= card.ActionCost; p.Relikia -= card.PrCost;
        var frame = new EngineActivation(actor, card.Id, card.ActionCost, card.PrCost, State.Phase) { Kind = ActivationKind.Counter };
        _activationChain.Add(frame); State.ActivationChain.Add(new(actor, card.Id, card.ActionCost, card.PrCost));
        Log($"{p.Name} responde con Error de Carga (1 acción).");
        State.ErrorResponder = 1 - actor;
        if (!HasLoadError(State.ErrorResponder)) ResolveErrorChain();
        return CommandResult.Ok();
    }
    public CommandResult PassErrorResponse(int actor)
    {
        if (State.Phase != Phase.ErrorResponse || State.ErrorResponder != actor) return CommandResult.Fail("No te corresponde responder.");
        ResolveErrorChain(); return CommandResult.Ok();
    }
    private void ResolveErrorChain()
    {
        for (var i = _activationChain.Count - 1; i >= 0; i--)
        {
            var frame = _activationChain[i];
            if (frame.Canceled) { RefundActivation(frame.Actor, frame.Actions, frame.Pr); continue; }
            if (i > 0) _activationChain[i - 1].Canceled = true;
        }
        var root = _activationChain[0]; _activationChain.Clear(); State.ActivationChain.Clear(); State.Phase = _errorResumePhase;
        if (root.Canceled) { CancelActivation(root); _activeActivation = root.Parent; State.LastCombat = $"{CardCatalog.Get(root.CardId).Name} · Activación anulada · Costes devueltos"; }
        else { ResumeActivation(root); CompleteActivationIfFinished(); }
    }
    private void RefundActivation(int actor, int actions, int pr)
    {
        var p = State.Players[actor]; p.ActionsLeft += actions; p.Relikia += pr;
        Log($"{p.Name} recupera {actions} acción(es) y {pr} PR por activación anulada.");
    }
    private void ClearCodeDecisions()
    {
        State.PendingCode = null; State.PendingDieRoll = null; State.PendingPoints = null; State.DestroyActor = null; _pendingDieEffect = null;
    }
    private void CorruptActivationCode(int actor, int index)
    {
        if (_activeActivation != null) { _activeActivation.ReplayCorrupted = true; _activeActivation.CorruptedIndex = State.Players[actor].CorruptedMemory.Count; }
        Corrupt(actor, actor, index);
    }
    private void CompleteActivationIfFinished()
    {
        if (State.Phase is Phase.Actions or Phase.Combat or Phase.Finished) _activeActivation = null;
    }
}

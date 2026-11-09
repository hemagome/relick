namespace Relick.Core;
public sealed partial class GameEngine
{
    private readonly List<EngineActivation> _activationChain = new();
    private EngineActivation? _activeActivation;
    private Phase _errorResumePhase;
    private bool HasLoadError(int actor) => ComboResponsesEnabled(actor) && State.Players[actor].ActionsLeft > 0 && !HandReservedForActivation(actor) &&
        State.Players[actor].Hand.Any(id => CardCatalog.Get(id).CancelActivation);
    public bool CanPlayLoadError(int actor)
    {
        if (actor < 0 || actor >= 2) return false;
        // An activation can only be canceled before its effect starts. Passing closes
        // this window permanently; choices made during resolution do not reopen it.
        return HasLoadError(actor) && State.Phase == Phase.ErrorResponse && State.ErrorResponder == actor &&
            _activationChain.Count > 0 && _activationChain[^1].Actor != actor;
    }
    private void BeginActivation(EngineActivation frame)
    {
        _activeActivation = frame;
        if (frame.CanInterrupt && HasLoadError(1 - frame.Actor)) OpenErrorChain(frame, frame.ResumePhase);
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
        if (root.Canceled && root.FinishSniperExchange) CompleteExchange(State.Players[root.Actor]);
        if (State.Phase == Phase.TurnBuffResolving && State.PendingTurnBuff is { Completed: true }) CompleteActivationIfFinished();
        if (State.Phase == Phase.ReloadResolving && State.PendingReload != null) CompleteActivationIfFinished();
        CompleteAttackIfReady();
        CompleteEndTurnIfReady();
    }
    private void RefundActivation(int actor, int actions, int pr)
    {
        var p = State.Players[actor]; p.ActionsLeft += actions; p.Relikia += pr;
        Log($"{p.Name} recupera {actions} acción(es) y {pr} PR por activación anulada.");
    }
    private void CorruptActivationCode(int actor, int index)
    {
        if (_activeActivation != null) _activeActivation.ReplayCorrupted = true;
        Corrupt(actor, actor, index);
    }
    private void CompleteActivationIfFinished()
    {
        if (State.Phase == Phase.ReloadResolving && State.PendingReload != null) { ContinueReloadHands(); return; }
        if (State.Phase == Phase.TurnBuffResolving && State.PendingTurnBuff is { Completed: true })
        {
            CompleteTurnBuff(); return;
        }
        if (State.Phase is Phase.Draw or Phase.Actions or Phase.Combat or Phase.Final or Phase.Finished) _activeActivation = null;
        CompleteAttackIfReady();
        CompleteEndTurnIfReady();
    }
}

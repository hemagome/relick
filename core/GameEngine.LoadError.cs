namespace Relick.Core;
public sealed partial class GameEngine
{
    private readonly List<EngineActivation> _activationChain = new();
    private EngineActivation? _activeActivation;
    private Phase _errorResumePhase;
    /// <summary>Raised once per resolved Error de Carga chain with every link in activation order and its outcome.</summary>
    public event Action<List<PublicChainLink>>? ChainResolved;
    // The attack still pending under an open chain: its skill is public on the field and the chain cannot cancel it.
    private PublicChainLink? ChainAttack()
    {
        var root = _activationChain.FirstOrDefault();
        if (root == null) return null;
        if (State.PendingAttack is { } attack) return new() { Actor = attack.Attacker, CardId = attack.SkillId, Attack = true };
        if ((State.PendingAttackRoll ?? State.PendingTurnBuff?.AttackRoll) is { } roll) return new() { Actor = roll.Attacker, CardId = roll.SkillId, Attack = true };
        return null;
    }
    private bool HasLoadError(int actor) => ComboResponsesEnabled(actor) && AvailableActions(actor) > 0 && !HandReservedForActivation(actor) &&
        PlayableCards(actor).Any(card => CardCatalog.Get(card.Id).CancelActivation);
    public bool CanPlayLoadError(int actor)
    {
        if (actor < 0 || actor >= 2) return false;
        // An activation can be canceled before its effect starts and, when it rolls a die, once more after seeing the
        // final result (6.5, 6.6). Choices made during resolution do not reopen the window.
        return HasLoadError(actor) && State.Phase == Phase.ErrorResponse && State.ErrorResponder == actor &&
            _activationChain.Count > 0 && _activationChain[^1].Actor != actor;
    }
    private void BeginActivation(EngineActivation frame)
    {
        _activeActivation = frame;
        if (frame.CanInterrupt && (HasLoadError(1 - frame.Actor) || HasComboParry(1 - frame.Actor) || HasInitialCopyResponse(frame) || HasLetterRamResponse(1 - frame.Actor, frame.PlaceLetter, opening: true))) OpenErrorChain(frame, frame.ResumePhase);
        else { ResumeActivation(frame); CompleteActivationIfFinished(); }
    }
    private void OpenErrorChain(EngineActivation frame, Phase resume)
    {
        _activationChain.Clear(); _activationChain.Add(frame); _errorResumePhase = resume;
        State.ActivationChain.Clear(); State.ActivationChain.Add(new(frame.Actor, frame.CardId, frame.Actions, frame.Pr, frame.PlaceLetter));
        State.ErrorResponder = 1 - frame.Actor; State.Phase = Phase.ErrorResponse;
    }
    private CommandResult PlayLoadError(int actor, int index)
    {
        if (!CanPlayLoadError(actor)) return CommandResult.Fail("No hay una activación enemiga que anular.");
        var p = State.Players[actor]; var card = CardCatalog.Get(PlayableHandCard(p, index)!);
        SendHandToMemory(p, index); p.ActionsLeft -= card.ActionCost; p.Relikia -= card.PrCost;
        var frame = new EngineActivation(actor, card.Id, card.ActionCost, card.PrCost, State.Phase) { Kind = ActivationKind.Counter };
        _activationChain.Add(frame); State.ActivationChain.Add(new(actor, card.Id, card.ActionCost, card.PrCost));
        Log($"{p.Name} responde con Error de Carga (1 acción).");
        GiveRamPriority(1 - actor);
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
            if (i > 0 && frame.Kind == ActivationKind.Counter) _activationChain[i - 1].Canceled = true;
        }
        if (ChainResolved != null)
        {
            var links = new List<PublicChainLink>();
            if (ChainAttack() is { } attack) links.Add(attack);
            links.AddRange(_activationChain.Select(f => new PublicChainLink { Actor = f.Actor, CardId = IsCopyHelmetFrame(f) ? CardCatalog.CopyHelmet.Id : f.CardId, Canceled = f.Canceled, PlaceLetter = f.PlaceLetter }));
            ChainResolved(links);
        }
        var frames = _activationChain.ToArray();
        _activationChain.Clear(); State.ActivationChain.Clear(); State.Phase = _errorResumePhase;
        if (frames.Skip(1).Any(frame => frame.Kind != ActivationKind.Counter)) ResolveRamWithCopies(frames);
        else ResolveRamRoot(frames[0]);

        static bool IsCopyHelmetFrame(EngineActivation frame) => frame.FieldCardId is { } id && CardCatalog.PhysicalId(id) == CardCatalog.CopyHelmet.Id;
    }

    private void ResolveRamRoot(EngineActivation root)
    {
        if (State.ErrorAfterDie) { ResolveErrorAfterDie(root); return; }
        if (root.Canceled) { CancelActivation(root); _activeActivation = root.Parent; State.LastCombat = $"{CardCatalog.Get(root.CardId).Name} · Activación anulada · Costes devueltos"; }
        else { ResumeActivation(root); CompleteActivationIfFinished(); }
        if (root.Canceled && root.FinishSniperExchange) CompleteExchange(State.Players[root.Actor]);
        if (State.Phase == Phase.TurnBuffResolving && State.PendingTurnBuff is { Completed: true }) CompleteActivationIfFinished();
        if (State.Phase == Phase.ReloadResolving && State.PendingReload != null) CompleteActivationIfFinished();
        CompletePendingPlay();
    }
    /// <summary>
    /// After a Code's die is final (its owner's rerolls and modifiers included), the rival may answer with Error de Carga
    /// before the rest of the effect applies (6.5, 6.6). Only the activating Code's own roll opens it, and only while that
    /// activation can still be canceled: a replay with Relikia, or a roll that is not the Code's (recycling), never does.
    /// </summary>
    private bool TryOpenErrorAfterDie(DieEffect effect, int die)
    {
        if (_activeActivation is not { Kind: ActivationKind.Code, CanInterrupt: true } frame || effect.SkillId != frame.CardId ||
            effect.Actor != frame.Actor || (!HasLoadError(1 - frame.Actor) && !HasInitialCopyResponse(frame))) return false;
        _pendingDieEffect = effect;
        State.PendingDieRoll = new(effect.Actor, effect.Purpose, die, State.Phase);
        State.ErrorAfterDie = true;
        OpenErrorChain(frame, State.Phase);
        State.LastCombat = $"{State.Players[frame.Actor].Name} · {CardCatalog.Get(frame.CardId).Name} · D6 = {die} · Puede responderse con Error de Carga";
        Log($"{State.Players[1 - frame.Actor].Name} puede responder con Error de Carga después de ver el dado ({die}).");
        return true;
    }

    /// <summary>
    /// Re-lanza rerolls a die that already exists, so its window follows the new result and comes before its draw. A
    /// canceled Re-lanza keeps the rerolled die, which already happened, and skips the draw.
    /// </summary>
    private bool TryOpenErrorAfterReroll(EngineContinuation draw)
    {
        if (draw.Text.Length == 0 || !HasLoadError(1 - draw.Actor)) return false;
        var frame = new EngineActivation(draw.Actor, draw.Text, 0, 0, State.Phase) { Kind = ActivationKind.Reroll, Parent = _activeActivation };
        State.PendingAfterError = draw; State.ErrorAfterDie = true; State.LastDie = draw.Value;
        OpenErrorChain(frame, State.Phase);
        State.LastCombat = $"{State.Players[draw.Actor].Name} · {CardCatalog.Get(draw.Text).Name} · D6 = {draw.Value} · Puede responderse con Error de Carga";
        Log($"{State.Players[1 - draw.Actor].Name} puede responder con Error de Carga después de ver el dado ({draw.Value}).");
        return true;
    }

    private void ResolveErrorAfterDie(EngineActivation root)
    {
        if (State.PendingAfterError is { } rest)
        {
            State.PendingAfterError = null; State.ErrorAfterDie = false; _activeActivation = root.Parent;
            if (root.Canceled)
            {
                State.LastCombat = $"{CardCatalog.Get(root.CardId).Name} · Anulada tras el dado ({rest.Value}) · Sin robo";
                Continue(rest.Next);
            }
            else { rest.Flag = true; Continue(rest); }
            CompleteActivationIfFinished();
            CompletePendingPlay();
            return;
        }
        var effect = _pendingDieEffect!; var die = State.PendingDieRoll!.Die;
        State.ErrorAfterDie = false; _pendingDieEffect = null; State.PendingDieRoll = null;
        if (root.Canceled)
        {
            // The text is void: the rest of the effect never applies. What already happened (cards drawn first) stays.
            CancelActivation(root); _activeActivation = root.Parent;
            State.LastCombat = $"{CardCatalog.Get(root.CardId).Name} · Anulada tras el dado ({die}) · Acción devuelta";
            if (die == 1) TryBreakCardsOnOwnOne(effect.Actor, null);
        }
        else
        {
            ResolveDieEffect(effect, die);
            CompleteActivationIfFinished();
        }
        CompletePendingPlay();
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
    private void ReleaseActiveActivation()
    {
        if (_activeActivation is { } frame && !State.IsOver)
        {
            if (frame.HeldCard != null) FinishHeldCard(frame);
            else CorruptWaitingFieldCard(frame);
        }
        _activeActivation = null;
    }

    private void CompleteActivationIfFinished()
    {
        if (State.IsOver) return;
        if (TryRunDiscardEffects()) return;
        if (FinishPlayedTargetResponse()) return;
        if (State.Phase == Phase.ReloadResolving && State.PendingReload != null) { ContinueReloadHands(); return; }
        if (State.Phase == Phase.TurnBuffResolving && State.PendingTurnBuff is { Completed: true })
        {
            CompleteTurnBuff(); return;
        }
        if (_activeActivation is { } letter && CardCatalog.Get(letter.CardId).Letter.HasValue &&
            State.Phase == letter.ResumePhase && State.Phase != Phase.ErrorResponse)
            ReleaseActiveActivation();
        if (State.Phase is Phase.Draw or Phase.Actions or Phase.Combat or Phase.Final or Phase.Finished ||
            _activeActivation?.FieldCardId is { } id && CardCatalog.PhysicalId(id) == CardCatalog.CopyHelmet.Id &&
            _ramResolutions.LastOrDefault()?.PlayPhase == State.Phase)
        {
            ReleaseActiveActivation();
        }
        CompletePendingPlay();
    }
}

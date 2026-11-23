namespace Relick.Core;

public sealed class RamResolution
{
    public int NextLink { get; set; }
    public Phase Phase { get; set; }
    public PendingTurnBuff? TurnBuff { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Phase PlayPhase => TurnBuff?.ResumePhase ?? Phase;
    public PendingDieRoll? Die { get; set; }
    public DieEffect? DieEffect { get; set; }
    public EngineContinuation? AfterError { get; set; }
    public bool AfterDie { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public List<EngineActivation> Links { get; } = [];
}

public sealed partial class GameEngine
{
    private readonly List<RamResolution> _ramResolutions = [];

    private static bool CanCopyActivation(EngineActivation frame) =>
        frame.Kind is ActivationKind.Code or ActivationKind.TurnBuff && !frame.FromMemory &&
        CardCatalog.Get(frame.CardId).CodeType is CodeType.Object or CodeType.Character;

    private bool CanCopyInRam(int actor) => State.Phase == Phase.ErrorResponse && State.ErrorResponder == actor &&
        _activationChain.Any(CanCopyActivation);

    private bool HasCopyHelmet(int actor) => ComboResponsesEnabled(actor) && AvailableActions(actor) >= CardCatalog.CopyHelmet.ActionCost &&
        PlayableCards(actor).Any(card => card.Id == CardCatalog.CopyHelmet.Id) &&
        _activationChain.Any(CanCopyActivation);

    private bool HasInitialCopyResponse(EngineActivation frame) => CanCopyActivation(frame) &&
        ComboResponsesEnabled(1 - frame.Actor) && AvailableActions(1 - frame.Actor) >= CardCatalog.CopyHelmet.ActionCost &&
        PlayableCards(1 - frame.Actor).Any(card => card.Id == CardCatalog.CopyHelmet.Id);

    private CommandResult PlayCopyHelmet(int actor, int handIndex)
    {
        // Target selection is part of playing the helmet. No card or action is spent until it is selected.
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.CopyHelmet.Id, Step = "copy", Kind = DecisionKind.ChooseCard, Decider = actor,
            Value = handIndex, Instruction = "Elige un Objeto o Personaje en la secuencia de RAM para copiarlo."
        });
        return CommandResult.Ok();
    }

    private IReadOnlyList<DecisionChoice> CopyHelmetChoices(PendingDecision decision) => decision.Step == "letter-copy"
        ? LetterModeChoices(_activationChain[decision.Picks![0]].CardId, decision.Picks[0])
            .Where(choice => choice.Button == "Colocar" || _activationChain[choice.Value].CardId != CardCatalog.LetterR.Id ||
                decision.Decider == State.CurrentPlayer).ToArray() :
        _activationChain.Select((frame, index) => (frame, index)).Where(link => CanCopyActivation(link.frame))
            .Select(link => new DecisionChoice
            {
                Label = $"{CardCatalog.Get(link.frame.CardId).Name} · {State.Players[link.frame.Actor].Name} · RAM {link.index + 1}",
                Button = "Copiar", CardId = link.frame.CardId, Value = link.index,
                Public = $"elige copiar {CardCatalog.Get(link.frame.CardId).Name}."
            }).ToArray();

    private void AddCopyHelmetToRam(PendingDecision decision, DecisionChoice choice)
    {
        var actor = decision.Decider;
        var source = _activationChain[choice.Value].CardId;
        if (choice.Button == "Copiar" && CardCatalog.Get(source).Letter != null)
        {
            AskDecision(new PendingDecision { Source = decision.Source, Step = "letter-copy", Kind = DecisionKind.Confirm,
                Decider = actor, Value = decision.Value, Picks = [choice.Value],
                Instruction = "Elige cómo activar la copia de la letra." });
            return;
        }
        var frame = new EngineActivation(actor, source, CardCatalog.CopyHelmet.ActionCost, 0, _errorResumePhase)
        {
            Kind = ActivationKind.Code, PlaceLetter = choice.Button == "Colocar", FieldCardId = FieldInstance(source, CardCatalog.CopyHelmet.Id)
        };
        HoldActivationCard(frame, decision.Value);
        State.Players[actor].ActionsLeft -= frame.Actions;
        _activationChain.Add(frame);
        State.ActivationChain.Add(new(actor, CardCatalog.CopyHelmet.Id, frame.Actions, 0, frame.PlaceLetter));
        Log($"{State.Players[actor].Name} responde con Casco de Copias para clonar {CardCatalog.Get(source).Name} y {(frame.PlaceLetter ? "colocarla encima de su Relikario" : "resolver su efecto")}.");
        GiveRamPriority(1 - actor);
    }

    private void GiveRamPriority(int actor)
    {
        State.ErrorResponder = actor;
        if (!HasLoadError(actor) && !HasCopyHelmet(actor) && !HasLetterRamResponse(actor, _activationChain.Any(frame => frame.PlaceLetter))) ResolveErrorChain();
    }

    private void ResolveRamWithCopies(EngineActivation[] links)
    {
        var batch = new RamResolution
        {
            NextLink = links.Length - 1, Phase = _errorResumePhase, AfterDie = State.ErrorAfterDie,
            TurnBuff = State.PendingTurnBuff,
            Die = State.PendingDieRoll, DieEffect = _pendingDieEffect, AfterError = State.PendingAfterError
        };
        batch.Links.AddRange(links);
        _ramResolutions.Add(batch);
        State.PendingTurnBuff = null;
        State.ErrorAfterDie = false;
        State.PendingDieRoll = null;
        State.PendingAfterError = null;
        _pendingDieEffect = null;
        _activeActivation = null;
        ResumeRamResolution();
    }

    private void ResumeRamResolution()
    {
        var batch = _ramResolutions[^1];
        State.Phase = batch.PlayPhase;
        while (batch.NextLink > 0)
        {
            var clone = batch.Links[batch.NextLink--];
            if (clone.Kind == ActivationKind.Counter) continue;
            if (clone.Canceled) { FinishHeldCard(clone); continue; }
            var card = CardCatalog.Get(clone.CardId);
            clone.ResumePhase = batch.PlayPhase;
            var reason = clone.PlaceLetter ? null : CodeEffectUnavailable(State.Players[clone.Actor], card, excludedHand: int.MaxValue);
            if (reason != null)
            {
                FinishHeldCard(clone);
                Log($"El clon de {card.Name} no puede resolverse. {reason}");
                continue;
            }
            if (card.DiscardForTurnBuff || card.RepeatStage)
            {
                clone.CanInterrupt = false;
                BeginTurnBuff(clone);
                return;
            }
            _activeActivation = clone;
            ActivateCodeAfterErrors(clone.Actor, card, false, clone.MemoryIndex);
            CompleteActivationIfFinished();
            return;
        }

        _ramResolutions.RemoveAt(_ramResolutions.Count - 1);
        var root = batch.Links[0];
        _activeActivation = root;
        State.Phase = batch.Phase;
        State.PendingTurnBuff = batch.TurnBuff;
        State.ErrorAfterDie = batch.AfterDie;
        State.PendingDieRoll = batch.Die;
        State.PendingAfterError = batch.AfterError;
        _pendingDieEffect = batch.DieEffect;
        ResolveRamRoot(root);
    }
}

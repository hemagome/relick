namespace Relick.Core;

public sealed partial class GameEngine
{
    private bool HasComboParry(int actor) => ComboResponsesEnabled(actor) &&
        PlayableCards(actor).Any(card => card.Id == CardCatalog.ComboParry.Id && CodeUnavailableReason(actor, card.Index) == null);

    private bool CanParryActivation(int actor) => !State.ErrorAfterDie &&
        State.ErrorResponder == actor && _activationChain.Count == 1;

    private void FinishComboParry(int actor)
    {
        var opponent = 1 - actor;
        var rival = State.Players[opponent];
        var returned = 0;
        var shuffleOtherDeck = false;
        for (var slot = rival.Hand.Count - 1; slot >= 0; slot--)
        {
            if (!CardCatalog.Get(rival.Hand[slot]).IsCombo) continue;
            shuffleOtherDeck |= ReturnHandCardToDeck(opponent, slot) == actor;
            returned++;
        }
        Shuffle(rival);
        CardsReturnedToDeck?.Invoke(opponent);
        if (shuffleOtherDeck)
        {
            Shuffle(State.Players[actor]);
            CardsReturnedToDeck?.Invoke(actor);
        }
        State.LastCombat = $"Combo Parry · {rival.Name} devuelve {returned} carta(s) COMBO y baraja su mazo.";
        Log(State.LastCombat);
        _activeActivation = _activeActivation?.Parent;

        switch (State.Phase)
        {
            case Phase.ErrorResponse: ResolveErrorChain(); break;
            case Phase.Decision: RecheckDecision(); break;
            case Phase.DefenseChoice when !CanDefend(State.DecisionPlayer, State.PendingAttack!.Critical):
                PassDefense(State.DecisionPlayer); break;
            case Phase.EffectDamageChoice when !HasTurnBuffInHand(State.DecisionPlayer) && !HasComboParry(State.DecisionPlayer):
                ResolveEffectDamage(State.DecisionPlayer); break;
            case Phase.AttackRerollChoice when !CanRespondToAttackDie():
                ResolveAttackReroll(State.DecisionPlayer, false); break;
            case Phase.DieRerollChoice when !CanRespondToEffectDie():
                ResolveDieReroll(State.DecisionPlayer, false); break;
            case Phase.MagnetChoice when rival.Hand.Count == 0 && rival.Bonus == null && rival.Area == null && actor == State.CurrentPlayer:
                SkipMagnet(actor); break;
        }
    }
}

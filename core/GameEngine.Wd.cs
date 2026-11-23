namespace Relick.Core;

public sealed partial class GameEngine
{
    private const string WdEvolution = "wd-evolution", WdBoost = "wd-boost", MultiRetry = "multi-retry", WildRedirect = "wild-redirect";

    private static bool IsWdDecision(PendingDecision decision) =>
        decision.Step is WdEvolution or WdBoost or MultiRetry or WildRedirect;

    private IEnumerable<(int Index, string Id)> WdBoostCards(int actor) => PlayableCards(actor)
        .Where(card => State.Players[actor].Level == 2 || card.Id == CardCatalog.Bullet.Id || CardCatalog.Get(card.Id).Kind == CardKind.Attack)
        .Select(card => (card.Index, card.Id));

    public bool CanBoostWdDie(int actor)
    {
        if (!OwnDiePending(actor)) return false;
        var p = State.Players[actor];
        if (p.Relikario.Family != RelikarioCatalog.Wd.Id || EffectsSuppressed(actor, FieldZone.Relikario)) return false;
        if (p.Level == 1 && State.Phase != Phase.AttackRerollChoice) return false;
        var die = State.Phase == Phase.AttackRerollChoice ? State.PendingAttackRoll!.Die : State.PendingDieRoll!.Die;
        var max = p.Level == 1 ? 5 : CurrentDieIsD10 ? 10 : 6;
        return die < max && WdBoostCards(actor).Any();
    }

    public CommandResult UseWdBoost(int actor)
    {
        if (!CanBoostWdDie(actor)) return CommandResult.Fail("WD no puede aumentar este dado.");
        AskDecision(new PendingDecision { Source = State.Players[actor].Relikario.Id, Step = WdBoost, Decider = actor,
            Kind = DecisionKind.ChooseCard, Instruction = "Descarta una carta para sumar +1 al dado." });
        return CommandResult.Ok();
    }

    private void AskWdEvolution(int actor, List<int> picks) => AskDecision(new PendingDecision
    {
        Source = RelikarioCatalog.Wd.Id, Step = WdEvolution, Decider = actor, Picks = picks,
        Kind = DecisionKind.ChooseCard, Instruction = $"Elige la carta {picks.Count + 1} de {RelikarioCatalog.WdLv2.EvolutionDiscards} para evolucionar."
    });

    private bool OfferMultiDriveRetry(PendingAttackRoll attack)
    {
        if (attack.SkillId != CardCatalog.MultiDrive.Id || attack.EffectsSuppressed || attack.Die is < 1 or > 3 ||
            !PlayableCards(attack.Attacker).Any()) return false;
        AskDecision(new PendingDecision { Source = attack.SkillId, Step = MultiRetry, Decider = attack.Attacker,
            Kind = DecisionKind.ChooseCard, Attack = attack, Instruction = "Puedes descartar una carta para relanzar el ataque fallido." });
        return true;
    }

    private IReadOnlyList<DecisionChoice> WdChoices(PendingDecision decision)
    {
        var actor = decision.Decider;
        if (decision.Step == WildRedirect)
            return State.DiscardTriggers![0].Redirectable.Select((card, index) => new DecisionChoice
            {
                Label = $"Enviar {CardCatalog.Get(card.CardId).Name} a canje · carta {index + 1}", CardId = card.CardId,
                Owner = card.MemoryOwner, Value = index, Button = "Enviar al canje", Public = "envía una carta descartada a su canje."
            }).Append(WdPass("Conservar el descarte")).ToArray();

        var cards = decision.Step == WdBoost ? WdBoostCards(actor) : PlayableCards(actor)
            .Where(card => decision.Step != WdEvolution || card.Field == null && decision.Picks?.Contains(card.Index) != true)
            .Select(card => (card.Index, card.Id));
        var (label, history, pass) = decision.Step switch
        {
            WdEvolution => ("Elegir carta", "elige una carta para el coste de evolución de WD.", "Cancelar evolución"),
            WdBoost => ("Descartar para +1", "descarta una carta para sumar +1 al dado con WD.", "Volver al dado"),
            _ => ("Descartar y relanzar", "descarta una carta para relanzar con Multi Drive.", "Aceptar el fallo")
        };
        return cards.Select(card => new DecisionChoice { Label = $"{label} · {CardCatalog.Get(card.Id).Name}", Button = label,
            CardId = card.Id, Value = card.Index, Public = history }).Append(WdPass(pass)).ToArray();
    }

    private static DecisionChoice WdPass(string label) => new() { Label = label, Value = -1, Public = "declina el efecto." };

    private void ResolveWdChoice(PendingDecision decision, DecisionChoice choice)
    {
        var actor = decision.Decider;
        switch (decision.Step)
        {
            case WildRedirect:
                ResolveWildRedirect(actor, choice.Value);
                break;
            case WdEvolution when choice.Value >= 0:
                List<int> selected = [.. decision.Picks ?? [], choice.Value];
                if (selected.Count < RelikarioCatalog.WdLv2.EvolutionDiscards) AskWdEvolution(actor, selected);
                else
                {
                    var player = State.Players[actor];
                    var snipers = DiscardHandCardsForCost(actor, selected);
                    ApplyEvolution(actor, RelikarioCatalog.WdLv2);
                    BeginSniperChoices(actor, snipers);
                }
                break;
            case WdBoost when choice.Value >= 0:
                var attack = State.PendingAttackRoll;
                var die = State.PendingDieRoll;
                BeginWdPayment(choice.Value, new EngineContinuation { Kind = ContinuationKind.WdBoostPaid, Actor = actor,
                    Attack = attack == null ? null : attack with { Die = attack.Die + 1, Modified = true },
                    Die = attack != null ? null : die! with { Die = die.Die + 1, Modified = true },
                    SavedDieEffect = _pendingDieEffect, Flag = CurrentDieIsD10 });
                break;
            case MultiRetry:
                if (choice.Value < 0) ResolveAfterSkillRetry(decision.Attack!);
                else BeginWdPayment(choice.Value, new EngineContinuation { Kind = ContinuationKind.MultiDrivePaid, Actor = actor, Attack = decision.Attack });
                break;
        }
    }

    private void ResolveWildRedirect(int actor, int index)
    {
        var batch = State.DiscardTriggers![0];
        if (index < 0)
        {
            batch.DeclinedRedirect = true;
            return;
        }
        var card = batch.Redirectable[index];
        var player = State.Players[actor];
        var source = new MemorySelection(card.MemoryOwner!.Value, card.MemoryIndex);
        player.WildDriveRedirects++;
        MoveMemoryCardToField(source, player);
        NotifyPileMove(State.Players[source.Owner], card.CardId, CardZone.Memory, player.Exchange.Count, CardZone.Exchange,
            destinationPlayer: actor, sourceIndex: source.Index);
        PlaceInExchange(player, card.CardId);
    }

    private void BeginWdPayment(int index, EngineContinuation after)
    {
        after.Next = State.WdPayment;
        State.WdPayment = after;
        State.PendingAttackRoll = null;
        State.PendingDieRoll = null;
        _pendingDieEffect = null;
        State.Phase = Phase.WdResolving;
        BeginSniperChoices(after.Actor, DiscardHandCardsForCost(after.Actor, [index]));
        CompletePendingPlay();
    }

    private bool TryFinishWdPayment()
    {
        if (State.Phase != Phase.WdResolving || State.PendingSniper != null || State.WdPayment is not { } payment) return false;
        while (_activeActivation is { FinishSniperExchange: true } sniper) _activeActivation = sniper.Parent;
        if (TryPresentExchangeTrigger()) return true;
        State.WdPayment = payment.Next;
        Continue(payment);
        return true;
    }

    private void ResumeWdDie(EngineContinuation payment)
    {
        State.PendingAttackRoll = payment.Attack;
        State.PendingDieRoll = payment.Die;
        _pendingDieEffect = payment.SavedDieEffect;
        State.Phase = payment.Attack != null ? Phase.AttackRerollChoice : Phase.DieRerollChoice;
        State.LastDie = payment.Attack?.Die ?? payment.Die!.Die;
        if (payment.Attack != null) State.LastAttackDie = State.LastDie;
        CurrentDieIsD10 = payment.Flag;
        CurrentDieCaption = $"WD · +1 al dado · {State.LastDie}";
        DieAdjusted?.Invoke(State.LastDie);
        Log($"{State.Players[payment.Actor].Name} suma +1 a su dado con WD ({State.LastDie}).");
        CompletePendingPlay();
    }

    private void RerollMultiDrive(PendingAttackRoll attack)
    {
        OnOwnDieRerolled(attack.Attacker);
        var die = RollAttack(out _, "Relick Multi Drive · Relanzamiento", superAttack: attack.SuperAttack);
        State.LastAttackDie = die;
        State.PendingAttackRoll = attack with { Die = die, WasRerolled = true, Modified = true, RerollStopped = die == 1 };
        State.Phase = Phase.AttackRerollChoice;
        ProcessAttackDie(die, new EngineContinuation { Kind = ContinuationKind.AttackDieReady, Actor = attack.Attacker });
    }
}

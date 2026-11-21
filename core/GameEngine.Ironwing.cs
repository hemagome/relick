namespace Relick.Core;

public sealed partial class GameEngine
{
    private const string IronwingRecycle = "ironwing-recycle";
    private const string IronwingBonus = "ironwing-bonus";
    private const string FireDamage = "fire-damage";
    private const string FireDestroy = "fire-destroy";
    private const int RollLimit = CardCatalog.IronwingRollCounterLimit;

    private static bool IsDeckAttack(string id) => CardCatalog.All.TryGetValue(id, out var card) && DeckRules.IsAttack(card);
    private static int AttackCopies(PlayerState p) => p.Deck.Count(IsDeckAttack);
    private bool CanBoostIronwingEffect(int actor, int die) => die < 6 &&
        State.Players[actor].Relikario == RelikarioCatalog.IronwingLv2 && !EffectsSuppressed(actor, FieldZone.Relikario) &&
        AttackCopies(State.Players[actor]) > 0;

    public int IronwingDieCostLimit(int actor)
    {
        if (actor is < 0 or > 1 || !OwnDiePending(actor)) return 0;
        var p = State.Players[actor];
        var attack = State.PendingAttackRoll;
        var die = attack?.Die ?? State.PendingDieRoll!.Die;
        if (CanBoostIronwingEffect(actor, die)) return 1;
        return p.Relikario == RelikarioCatalog.Ironwing && attack is { IronwingPaid: false, Die: < 5 } &&
            !EffectsSuppressed(actor, FieldZone.Relikario)
            ? Math.Min(4, AttackCopies(p)) : 0;
    }

    internal int IronwingDieIncrease(int actor, int count) =>
        State.Players[actor].Relikario == RelikarioCatalog.Ironwing
            ? IronwingAttackDieIncrease(State.PendingAttackRoll!.Die, count) : count;

    internal static int IronwingAttackDieIncrease(int die, int count) => Math.Min(5 - die, count / 2);

    public CommandResult BoostIronwingDie(int actor, int count)
    {
        if (count < 1 || count > IronwingDieCostLimit(actor)) return CommandResult.Fail("Ese dado no admite ese coste de Ironwing.");
        var p = State.Players[actor];
        var attack = State.PendingAttackRoll;
        var effect = State.PendingDieRoll;
        var previous = attack?.Die ?? effect!.Die;
        var die = previous + IronwingDieIncrease(actor, count);
        var resume = new EngineContinuation
        {
            Kind = ContinuationKind.IronwingDiePaid, Actor = actor, Phase = State.Phase,
            Attack = attack == null ? null : attack with { Die = die, IronwingPaid = true, Modified = attack.Modified || die != previous },
            Die = effect == null ? null : effect with { Die = die, Modified = true },
            SavedDieEffect = _pendingDieEffect
        };
        State.LastDie = die;
        if (attack != null) State.LastAttackDie = die;
        State.LastCombat = $"{p.Name} · Ironwing · Dado {previous} → {die}";
        Log($"{p.Name} destruye {count} Ataque(s) de su mazo con Ironwing. Dado: {previous} → {die}.");
        if (die != previous)
        {
            CurrentDieCaption = $"Ironwing · +{die - previous}";
            DieAdjusted?.Invoke(die);
        }
        State.PendingAttackRoll = null;
        State.PendingDieRoll = null;
        _pendingDieEffect = null;
        State.Phase = effect?.ResumePhase ?? Phase.Combat;
        PayIronwingAttacks(actor, count, resume);
        return CommandResult.Ok();
    }

    private void PayIronwingAttacks(int actor, int count, EngineContinuation next)
    {
        var p = State.Players[actor];
        for (var n = 0; n < count; n++)
        {
            var index = p.Deck.FindIndex(IsDeckAttack);
            SendToMemory(p, p.Deck[index], CardZone.Deck, -1, corruptionIndex: index);
            p.Deck.RemoveAt(index);
            p.RemoveCorruptionSlots(CardZone.Deck, index);
        }
        if (p.Deck.Count == 0) Exhaust(p, next);
        else Continue(next);
    }

    private void ResumeIronwingDie(EngineContinuation next)
    {
        State.LastDie = next.Attack?.Die ?? next.Die!.Die;
        next.Kind = ContinuationKind.ResumeCombo;
        Continue(next);
    }

    private void RecoverIronwingAttacks(int actor, List<int>? chosen = null)
    {
        var p = State.Players[actor];
        var attacks = Enumerable.Range(0, p.Memory.Count).Where(i => IsDeckAttack(p.Memory[i])).ToArray();
        if (attacks.Length > 5 && (chosen?.Count ?? 0) < 5)
        {
            AskDecision(new PendingDecision
            {
                Source = p.Relikario.Id, Step = IronwingRecycle, Decider = actor, Kind = DecisionKind.ChooseCard, Picks = chosen,
                Instruction = $"Devuelve 5 Ataques de tu memoria al mazo · {(chosen?.Count ?? 0) + 1}/5."
            });
            return;
        }
        var slots = chosen?.ToArray() ?? attacks;
        var returning = slots.Order().Select(i => p.Memory[i]).ToArray();
        foreach (var index in slots.OrderDescending()) RemoveMemoryCard(new(actor, index));
        foreach (var id in returning)
        {
            p.Deck.Add(id);
            NotifyPileMove(p, id, CardZone.Memory, destination: CardZone.Deck);
        }
        Log($"{p.Name} devuelve {returning.Length} Ataque(s) de su memoria al mazo y lo baraja al evolucionar.");
        Shuffle(p);
        if (returning.Length > 0) CardsReturnedToDeck?.Invoke(actor);
        CompletePendingPlay();
    }

    private void OfferIronwingBonus(PendingAttackRoll attack, int used = 0)
    {
        var p = State.Players[attack.Attacker];
        if (attack.Die >= 4 && used < 3 && p.Relikario == RelikarioCatalog.Ironwing &&
            !EffectsSuppressed(attack.Attacker, FieldZone.Relikario) && (p.Bonus != null || p.Area != null))
            AskDecision(new PendingDecision
            {
                Decider = attack.Attacker, Source = p.Relikario.Id, Step = IronwingBonus, Kind = DecisionKind.Confirm,
                Value = used, Attack = attack, Instruction = $"Puedes cambiar las cartas de tu zona de Bonus · {3 - used} vez/veces restantes."
            });
        else ResolveSkillAttack(attack);
    }

    private void RefreshIronwingBonus(PendingDecision decision)
    {
        var actor = decision.Decider;
        var p = State.Players[actor];
        if (p.Bonus is { } bonus)
        {
            var borrowed = ReleaseMovedFieldCard(p, -1) ?? p.ReleaseForeignFieldCard(bonus);
            ReturnIronwingFieldCard(p, bonus, CardZone.Bonus, borrowed);
            ChangeZahiraHosts(actor, FieldZone.Bonus, -1);
            ForgetFieldCodeSource(p, FieldZone.Bonus);
            p.Bonus = null;
            p.InitialAreaWaitingInBonus = false;
            p.ClearCorruptionMarks(CardZone.Bonus);
        }
        if (p.Area is { } area)
        {
            ReturnIronwingFieldCard(p, area, CardZone.Area, p.AreaIsBorrowed);
            ClearArea(p);
        }
        Log($"{p.Name} envía las cartas de su zona de Bonus al fondo del mazo con Ironwing.");
        RefillBonus(p, new EngineContinuation { Kind = ContinuationKind.IronwingBonus, Actor = actor,
            Count = decision.Value + 1, Attack = decision.Attack });
    }

    private void ReturnIronwingFieldCard(PlayerState p, string id, CardZone source, bool borrowed)
    {
        var owner = borrowed ? 1 - Seat(p) : Seat(p);
        PutFieldCardInDeck(p, id, source, -1, owner, p.CorruptsOnMemory(source, -1));
        CardsReturnedToDeck?.Invoke(owner);
    }

    public int IronwingRollCost => State.PendingAttack?.Critical == true ? 4 : 2;

    public IEnumerable<int> IronwingRollDefenses(int actor)
    {
        var p = State.Players[actor];
        return AttackCopies(p) < IronwingRollCost ? [] : Enumerable.Range(0, p.Skills.Count).Where(i =>
            p.Skills[i] == CardCatalog.IronwingRoll.Id && p.CountersOnSkill(i) < RollLimit &&
            !EffectsSuppressed(actor, FieldZone.Skill, i));
    }

    public CommandResult UseIronwingRoll(int actor, int slot)
    {
        if (State.Phase != Phase.DefenseChoice || State.DecisionPlayer != actor || !IronwingRollDefenses(actor).Contains(slot))
            return CommandResult.Fail("Elige una copia de Ironwing Roll y conserva los Ataques requeridos en el mazo.");
        var p = State.Players[actor];
        var counters = p.SkillCounters[slot] = p.CountersOnSkill(slot) + 1;
        Log($"{p.Name} usa Ironwing Roll: destruye {IronwingRollCost} Ataques de su mazo y coloca el contador {counters}/{RollLimit}.");
        PayIronwingAttacks(actor, IronwingRollCost, new EngineContinuation { Kind = ContinuationKind.IronwingRoll, Actor = actor,
            Value = p.SkillPosition(slot), Flag = counters >= RollLimit });
        return CommandResult.Ok();
    }

    private void FinishIronwingRoll(EngineContinuation next)
    {
        var p = State.Players[next.Actor];
        var slot = Enumerable.Range(0, p.Skills.Count).FirstOrDefault(i => p.SkillPosition(i) == next.Value &&
            p.Skills[i] == CardCatalog.IronwingRoll.Id, -1);
        if (next.Flag && slot >= 0)
        {
            next.Flag = false;
            RequestDestruction(new(next.Actor, FieldZone.Skill, slot, CardCatalog.IronwingRoll.Id), next);
            return;
        }
        ResolveAttackDamage(canceled: true, source: "Ironwing Roll", cardId: CardCatalog.IronwingRoll.Id);
    }

    private bool IronwingFireReady(int actor, int slot)
    {
        var p = State.Players[actor];
        return p.Skills.ElementAtOrDefault(slot) == CardCatalog.IronwingFire.Id && CanLearnSkill(p, CardCatalog.IronwingFire) &&
            p.IronwingFireUsed?.Contains(slot) != true && !EffectsSuppressed(actor, FieldZone.Skill, slot);
    }

    // A saved duel may already have Fire on the field under its former universal classification.
    internal static bool CanAttackWithTaughtSkill(PlayerState player, string skillId) =>
        skillId != CardCatalog.IronwingFire.Id || CanLearnSkill(player, CardCatalog.IronwingFire);

    public IEnumerable<int> IronwingFireDestructions(int actor)
    {
        if (ValidateTurn(actor) != null || !State.Phase.IsTurnStage()) return [];
        var p = State.Players[actor];
        return p.Skills.Contains(CardCatalog.IronwingFire.Id) && AttackCopies(p) >= 3 && NonSkillFieldCards().Any()
            ? Enumerable.Range(0, p.Skills.Count).Where(slot => IronwingFireReady(actor, slot)) : [];
    }

    public CommandResult UseIronwingFire(int actor, int slot)
    {
        if (!IronwingFireDestructions(actor).Contains(slot)) return CommandResult.Fail("No puedes activar la destrucción de Ironwing Fire.");
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.IronwingFire.Id, Step = FireDestroy, Kind = DecisionKind.ChooseTarget, Value = slot,
            Decider = actor, Instruction = "Destruye 3 Ataques de tu mazo para destruir una no-habilidad del campo."
        });
        return CommandResult.Ok();
    }

    private bool OfferFireDamage(int actor)
    {
        var p = State.Players[actor];
        if (!Enumerable.Range(0, p.Skills.Count).Any(i => IronwingFireReady(actor, i)) || AttackCopies(p) < 2)
            return false;
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.IronwingFire.Id, Step = FireDamage, Decider = actor,
            Kind = DecisionKind.Confirm, Instruction = "Puedes destruir 2 Ataques de tu mazo para sumar 1 al daño."
        });
        return true;
    }

    private static DecisionChoice PassIronwing(string label, string history) => new() { Label = label, Public = history, Value = -1 };

    private IReadOnlyList<DecisionChoice> IronwingChoices(PendingDecision decision)
    {
        var actor = decision.Decider;
        var p = State.Players[actor];
        return decision.Step switch
        {
            IronwingRecycle => p.Memory.Select((id, index) => (id, index)).Where(c =>
                IsDeckAttack(c.id) && decision.Picks?.Contains(c.index) != true).Select(c => new DecisionChoice
                {
                    Label = $"Devolver {CardCatalog.Get(c.id).Name} · memoria {c.index + 1}", Button = "Devolver al mazo", Value = c.index,
                    CardId = c.id, Owner = actor, Public = "selecciona un Ataque de su memoria."
                }).ToArray(),
            IronwingBonus => [
                new DecisionChoice { Label = "Enviar la zona de Bonus al fondo", Primary = true, Public = "cambia su zona de Bonus." },
                PassIronwing("Conservar el Bonus", "conserva su Bonus.") ],
            FireDamage => Enumerable.Range(0, p.Skills.Count).Where(slot => IronwingFireReady(actor, slot)).Select(slot => new DecisionChoice
                {
                    Label = $"Destruir 2 Ataques · +1 daño · espacio {p.SkillPosition(slot) + 1}", Value = slot, Primary = true,
                    Public = "elige aumentar el daño con Ironwing Fire."
                }).Append(PassIronwing("Conservar el daño", "conserva el daño del ataque.")).ToArray(),
            FireDestroy => NonSkillFieldCards().Select(target => FieldDestruction.Card(EffectFor(actor, decision.Source), target))
                .Append(PassIronwing("Cancelar", "omite la destrucción.")).ToArray(),
            _ => []
        };
    }

    private void ResolveIronwingChoice(PendingDecision decision, DecisionChoice choice)
    {
        var actor = decision.Decider;
        var p = State.Players[actor];
        switch (decision.Step)
        {
            case IronwingRecycle:
                RecoverIronwingAttacks(actor, [.. decision.Picks ?? [], choice.Value]);
                break;
            case IronwingBonus:
                if (choice.Value < 0) ResolveSkillAttack(decision.Attack!);
                else RefreshIronwingBonus(decision);
                break;
            case FireDamage:
                if (choice.Value < 0) PresentResolvedAttack();
                else
                {
                    (p.IronwingFireUsed ??= []).Add(choice.Value);
                    Log($"{p.Name} destruye 2 Ataques de su mazo con Ironwing Fire para sumar 1 al daño.");
                    PayIronwingAttacks(actor, 2, new EngineContinuation { Kind = ContinuationKind.IronwingFireDamage, Actor = actor });
                }
                break;
            case FireDestroy:
                if (choice.Field is not { } target) break;
                (p.IronwingFireUsed ??= []).Add(decision.Value);
                Log($"{p.Name} destruye 3 Ataques de su mazo con Ironwing Fire.");
                PayIronwingAttacks(actor, 3, new EngineContinuation { Kind = ContinuationKind.IronwingFireDestroy, Actor = actor, Target = target });
                break;
        }
    }

    private void FinishIronwingFireDamage(int actor)
    {
        var attack = State.PendingAttack!;
        State.PendingAttack = attack with { Damage = attack.Damage + 1 };
        State.LastCombat = $"{State.Players[actor].Name} · Ironwing Fire · {attack.Damage + 1} de daño";
        if (!OfferFireDamage(actor)) PresentResolvedAttack();
    }

    private void DestroyWithIronwingFire(EngineContinuation next)
    {
        var target = next.Target!;
        if (!NonSkillFieldCards().Contains(target)) { CompletePendingPlay(); return; }
        next.Kind = ContinuationKind.IronwingFieldDestroyed;
        if (!TryReplaceOpponentFieldEffect(next.Actor, target, next)) RequestDestruction(target, next);
    }

    private void FinishIronwingFieldDestruction(EngineContinuation next)
    {
        var target = next.Target!;
        if (next.Prevented)
        {
            Log($"{CardCatalog.IronwingFire.Name}: {FieldCardName(target)} queda protegida.");
            CompletePendingPlay();
        }
        else
        {
            Log($"{State.Players[next.Actor].Name} destruye {FieldCardName(target)} con {CardCatalog.IronwingFire.Name}.");
            if (target.Zone == FieldZone.Bonus) RefillBonus(State.Players[target.Owner]);
            else CompletePendingPlay();
        }
    }
}

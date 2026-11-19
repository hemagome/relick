namespace Relick.Core;

public sealed partial class GameEngine
{
    private const string OsamuSuccessfulAttack = "osamu-success";
    private const string OsamuFailedAttack = "osamu-miss";
    private const string OsamuEvolutionSearch = "osamu-evolution";
    private const string OsamuActionSearch = "osamu-action";
    private const string OsamuAgilitySearch = "osamu-agility";
    private const string OverlapDamageChoice = "overlap-damage";

    public bool CanUseOsamuSearch(int actor) =>
        ValidateAction(actor) == null && State.Active.Relikario == RelikarioCatalog.OsamuLv2 &&
        !State.Active.RelikarioSearchUsedThisTurn && !EffectsSuppressed(actor, FieldZone.Relikario) &&
        (State.Active.Deck.Count > 0 || State.Active.Memory.Count > 0);

    public CommandResult UseOsamuSearch(int actor)
    {
        if (!CanUseOsamuSearch(actor))
            return CommandResult.Fail("Osamu Lv 2 requiere una acción y al menos una carta en tu mazo o memoria. Solo una vez por turno.");
        var p = State.Active;
        p.ActionsLeft--;
        p.RelikarioSearchUsedThisTurn = true;
        OfferOsamuSearch(actor, p.Relikario.Id, OsamuActionSearch);
        return CommandResult.Ok();
    }

    private bool OfferOsamuAttackSearch(PendingAttackRoll attack)
    {
        var actor = attack.Attacker;
        var p = State.Players[actor];
        if (p.Relikario != RelikarioCatalog.Osamu || EffectsSuppressed(actor, FieldZone.Relikario) || attack.Die == 1) return false;
        return OfferOsamuSearch(actor, p.Relikario.Id, attack.Die >= 4 ? OsamuSuccessfulAttack : OsamuFailedAttack, attack);
    }

    private bool OfferOsamuSearch(int actor, string source, string step, PendingAttackRoll? attack = null)
    {
        var decision = new PendingDecision
        {
            Source = source, Step = step, Decider = actor, Kind = DecisionKind.ChooseCard, Attack = attack,
            Instruction = step switch
            {
                OsamuSuccessfulAttack => "Busca Bomba de Tiempo o Agilidad al 100% en tu mazo.",
                OsamuFailedAttack => "Recupera una Agilidad al 100% de tu memoria.",
                OsamuActionSearch => "Elige una carta de tu mazo o memoria. Se corromperá al ir a la memoria.",
                _ => "Elige una Agilidad al 100% de tu mazo o memoria."
            }
        };
        if (!OsamuSearchChoices(decision).Any(choice => choice.CardId != null)) return false;
        AskDecision(decision);
        return true;
    }

    private IReadOnlyList<DecisionChoice> OsamuDecisionChoices(PendingDecision decision) => decision.Step switch
    {
        OverlapDamageChoice => decision.Picks!.Select((damage, index) => new DecisionChoice
        {
            Label = $"Dado {index + 1} · {damage} de daño crítico", Public = $"elige {damage} de daño crítico.", Value = damage, Primary = true
        }).ToArray(),
        OsamuSuccessfulAttack or OsamuFailedAttack or OsamuActionSearch or OsamuEvolutionSearch or OsamuAgilitySearch =>
            OsamuSearchChoices(decision).ToArray(),
        _ => []
    };

    private IEnumerable<DecisionChoice> OsamuSearchChoices(PendingDecision decision)
    {
        var p = State.Players[decision.Decider];
        bool Eligible(string id) => decision.Step == OsamuActionSearch || id == CardCatalog.Agility.Id ||
            decision.Step == OsamuSuccessfulAttack && id == CardCatalog.TimeBomb.Id;

        if (decision.Step != OsamuFailedAttack)
            for (var index = 0; index < p.Deck.Count; index++)
                if (Eligible(p.Deck[index]))
                    yield return SearchChoice(p.Deck[index], index, "mazo", "del mazo");

        if (decision.Step != OsamuSuccessfulAttack)
            for (var index = 0; index < p.Memory.Count; index++)
                if (Eligible(p.Memory[index]))
                    yield return SearchChoice(p.Memory[index], index, "memoria", "de la memoria", decision.Decider);

        if (decision.Step == OsamuSuccessfulAttack)
            yield return new DecisionChoice { Label = "Pasar", Public = "decide no buscar una carta." };
    }

    private static DecisionChoice SearchChoice(string id, int index, string zone, string fromZone, int? owner = null) => new()
    {
        Label = $"Añadir {CardCatalog.Get(id).Name} · {zone} · carta {index + 1}",
        Button = owner.HasValue ? "Recuperar" : "Añadir a la mano", CardId = id, Owner = owner, Value = index,
        Public = $"añade una carta {fromZone} a su mano."
    };

    private void ResolveOsamuDecision(PendingDecision decision, DecisionChoice choice)
    {
        var actor = decision.Decider;
        if (decision.Step == OverlapDamageChoice)
        {
            ResolveTurboAttack(decision.Attack!, choice.Value);
            return;
        }
        var next = decision.Attack != null ? new EngineContinuation { Kind = ContinuationKind.ResolveSkillAttack, Actor = actor, Attack = decision.Attack } : null;
        if (choice.CardId == null)
        {
            Continue(next);
            return;
        }
        var mark = decision.Step == OsamuActionSearch;
        var p = State.Players[actor];
        if (choice.Owner is int owner)
        {
            TakeFromMemory(actor, owner, choice.Value, decision.Title(), mark);
            Continue(next);
        }
        else
        {
            Log($"{p.Name} añade una carta de su mazo a su mano con {decision.Title()}.");
            MoveDeckCard(p, choice.Value, false, next, mark);
        }
    }

    internal bool ReplaySequentialOverlap { get; set; }

    private bool RollTurboDamage(PendingAttackRoll attack)
    {
        var critical = attack.Die == 6;
        var normal = attack.Die >= 4 || HasStackedTotem(attack.Attacker);
        if (!normal) return false;
        var purpose = attack.SkillId == CardCatalog.TurboSword.Id ? DiePurpose.TurboSwordDamage :
            attack.SkillId == CardCatalog.TurboOverlap.Id && critical ? DiePurpose.TurboOverlapDamage : (DiePurpose?)null;
        if (purpose is not { } diePurpose) return false;
        RollWithRelikario(State.Players[attack.Attacker], diePurpose, new DieEffect
        {
            Purpose = diePurpose, Actor = attack.Attacker, SkillId = attack.SkillId, SkillAttack = attack
        });
        return true;
    }

    private void ResolveTurboDamageDie(DieEffect effect, int die)
    {
        var attack = effect.SkillAttack!;
        if (effect.Purpose == DiePurpose.TurboSwordDamage)
        {
            var damage = attack.Die == 6 ? Math.Max(4, die + 1) : Math.Max(2, die - 1);
            ResolveTurboAttack(attack, damage);
        }
        else if (effect.Slot == 0)
        {
            effect.Slot = die;
            RollWithRelikario(State.Players[effect.Actor], effect.Purpose, effect, effect.OverlapSecondDie);
        }
        else if (effect.OverlapSecondDie == null && effect.Slot == die) ResolveTurboAttack(attack, die);
        else AskDecision(new PendingDecision
        {
            Source = attack.SkillId, Step = OverlapDamageChoice, Decider = attack.Attacker, Kind = DecisionKind.Confirm,
            Instruction = $"Elige un dado para el daño crítico ({effect.Slot} o {die}).",
            Picks = [effect.Slot, die], Attack = attack
        });
    }

    private void ResolveTurboAttack(PendingAttackRoll attack, int damage)
    {
        var skill = CardCatalog.Get(attack.SkillId);
        if (attack.Die == 6) skill = skill with { CriticalDamage = damage };
        ResolveAttackResult(attack.Attacker, skill, attack.Die, attack.Die == 6 ? skill.NormalDamage : damage,
            attack.WasRerolled, attack.EffectsSuppressed, attack.SuperAttack ? CardCatalog.SuperAttack.DamageBonus : 0);
    }

    private bool TryTurboSwordDefense(int target, int amount, int attackDie, EngineContinuation? completed, int afterPosition = -1)
    {
        var p = State.Players[target];
        var slot = Enumerable.Range(0, p.Skills.Count).OrderBy(p.SkillPosition).FirstOrDefault(i =>
            p.SkillPosition(i) > afterPosition && p.Skills[i] == CardCatalog.TurboSword.Id &&
            p.CountersOnSkill(i) < 2 && !EffectsSuppressed(target, FieldZone.Skill, i), -1);
        if (slot < 0) return false;
        RollWithRelikario(p, DiePurpose.TurboSwordDefense, new DieEffect
        {
            Purpose = DiePurpose.TurboSwordDefense, Actor = target, SkillId = CardCatalog.TurboSword.Id,
            Slot = p.SkillPosition(slot), AttackDie = attackDie, IncomingDamage = amount, Next = completed
        });
        return true;
    }

    private void ResolveTurboSwordDefense(DieEffect effect, int die)
    {
        var p = State.Players[effect.Actor];
        int? attacker = effect.AttackDie > 0 ? 1 - effect.Actor : null;
        if (PreventDamageWithStar(p, effect.IncomingDamage, effect.Next, attacker)) return;
        var slot = Enumerable.Range(0, p.Skills.Count).FirstOrDefault(i =>
            p.SkillPosition(i) == effect.Slot && p.Skills[i] == CardCatalog.TurboSword.Id && !EffectsSuppressed(effect.Actor, FieldZone.Skill, i), -1);
        if (slot >= 0 && die == 5)
        {
            p.SkillCounters[slot] = p.CountersOnSkill(slot) + 1;
            State.LastCombat += " · Turbo Sword cancela el daño";
            NotifyGuard(effect.Actor, attacker, CardCatalog.TurboSword.Id, effect.IncomingDamage, 0, blocked: true);
            Log($"{p.Name} cancela {effect.IncomingDamage} de daño con {CardCatalog.TurboSword.Name} y coloca un contador sobre esa copia.");
            Continue(effect.Next);
        }
        else if (slot >= 0 && die == 6)
        {
            var reflected = effect.IncomingDamage / 2;
            State.LastCombat += $" · Turbo Sword devuelve {reflected} de daño";
            Log($"{p.Name} cancela {effect.IncomingDamage} de daño con {CardCatalog.TurboSword.Name} y devuelve {reflected}.");
            DealDamage(1 - effect.Actor, reflected, completed: effect.Next, source: effect.Actor);
        }
        else if (!TryTurboSwordDefense(effect.Actor, effect.IncomingDamage, effect.AttackDie, effect.Next, effect.Slot))
            ApplyIncomingDamage(effect.Actor, effect.IncomingDamage, effect.AttackDie, effect.Next);
    }
}

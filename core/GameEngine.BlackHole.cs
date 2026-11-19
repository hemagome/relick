namespace Relick.Core;

public sealed partial class GameEngine
{
    private const string BlackHoleSides = "black-hole-sides";
    private const string BlackHoleOrient = "black-hole-orient";
    private const string BlackHoleStart = "black-hole-start";
    private const string BlackHoleKeep = "black-hole-keep";

    private void AskAreaOrientation(int holder, bool moved = false)
    {
        if (State.Players[holder].Area == CardCatalog.BlackHole.Id)
            AskBlackHole(holder, holder, moved ? BlackHoleOrient : BlackHoleSides, DecisionKind.Confirm, "Elige qué lado del Área se aplica a ti");
        else AskRaid(holder, holder, moved ? RaidOrientMovedArea : RaidSides, DecisionKind.Confirm, "Elige qué lado del Área se aplica a ti");
    }

    private void AskBlackHole(int holder, int actor, string step, DecisionKind kind, string instruction, EngineContinuation? completed = null) =>
        AskDecision(new PendingDecision { Source = CardCatalog.BlackHole.Id, Step = step, Decider = actor, Value = holder,
            Kind = kind, Instruction = instruction, Continuation = completed });

    private IEnumerable<FieldTarget> BlackHoleFieldCards(int holder, int actor) =>
        FieldCards().Where(target => target.Owner == actor && target.Zone != FieldZone.Relikario &&
            !(target.Zone == FieldZone.Area && actor == holder && target.CardId == CardCatalog.BlackHole.Id));

    private IReadOnlyList<DecisionChoice> BlackHoleChoices(PendingDecision decision)
    {
        var actor = decision.Decider;
        return decision.Step switch
        {
            BlackHoleSides or BlackHoleOrient => AreaSideChoices(actor),
            BlackHoleStart => [
                new DecisionChoice { Label = "Enviar Agujero Negro a mi mano", Value = 1, Public = "envía Agujero Negro a su mano." },
                new DecisionChoice { Label = "Lanzar 1D6", Primary = true, Public = "decide lanzar el dado de Agujero Negro." }
            ],
            BlackHoleKeep => BlackHoleFieldCards(decision.Value, actor).Select(target => new DecisionChoice
            {
                Label = $"Conservar {FieldCardName(target)} · {FieldCardPlace(State, target)}", Button = "Conservar", Field = target,
                Public = $"elige conservar {FieldCardName(target)}."
            }).ToArray(),
            _ => []
        };
    }

    private void ResolveBlackHoleChoice(PendingDecision decision, DecisionChoice choice)
    {
        var holder = decision.Value;
        var p = State.Players[holder];
        switch (decision.Step)
        {
            case BlackHoleOrient:
                p.AreaSideA = choice.Value;
                CompletePendingPlay();
                break;
            case BlackHoleSides:
                p.AreaSideA = choice.Value;
                if (EffectsSuppressed(holder, FieldZone.Area)) CompletePendingPlay();
                else AskBlackHole(holder, choice.Value, BlackHoleStart, DecisionKind.Confirm,
                    "Lado A · Envía Agujero Negro a tu mano o lanza un dado.");
                break;
            case BlackHoleStart:
                if (choice.Value == 1)
                {
                    TakeArea(p, State.Players[decision.Decider]);
                    CompletePendingPlay();
                }
                else RollBlackHole(holder, decision.Decider, sideA: true);
                break;
            case BlackHoleKeep:
                DestroyBlackHoleField(holder, decision.Decider, choice.Field, decision.Continuation!);
                break;
        }
    }

    private void RollBlackHole(int holder, int actor, bool sideA, bool destroyArea = false)
    {
        RollWithRelikario(State.Players[actor], DiePurpose.BlackHole, new DieEffect
        {
            Actor = actor, Purpose = DiePurpose.BlackHole, SkillId = CardCatalog.BlackHole.Id, Slot = holder,
            Next = new EngineContinuation { Kind = sideA ? ContinuationKind.BlackHoleOtherSide : ContinuationKind.BlackHoleFinish,
                Actor = holder, Value = actor, Flag = destroyArea }
        });
    }

    private int AdjustBlackHoleDie(int actor, DiePurpose? purpose, int rolled)
    {
        if (purpose != DiePurpose.BlackHole || rolled == 6 ||
            State.Players[actor].Relikario != RelikarioCatalog.IronwingLv2 || EffectsSuppressed(actor, FieldZone.Relikario)) return rolled;
        State.LastDie = 6;
        CurrentDieCaption = "Ironwing Lv 2 · Agujero Negro";
        DieAdjusted?.Invoke(6);
        Log($"{State.Players[actor].Name} cambia a 6 el dado de Agujero Negro con Ironwing Lv 2.");
        return 6;
    }

    private void ResolveBlackHoleDie(DieEffect effect, int die)
    {
        var completed = effect.Next!;
        var holder = effect.Slot;
        var actor = effect.Actor;
        Log($"{State.Players[actor].Name} resuelve Agujero Negro con un {die}.");
        if (die == 6) { Continue(completed); return; }
        if (completed.Kind == ContinuationKind.BlackHoleOtherSide) completed.Flag = true;
        if (BlackHoleFieldCards(holder, actor).Skip(1).Any())
            AskBlackHole(holder, actor, BlackHoleKeep, DecisionKind.ChooseTarget,
                "Elige la carta de tu campo que vas a conservar.", completed);
        else Continue(completed);
    }

    private void DestroyBlackHoleField(int holder, int actor, FieldTarget? keep, EngineContinuation completed)
    {
        var next = completed;
        // Within each zone remove from the highest slot down. Skills keep their physical board slot if a protector moves.
        foreach (var target in BlackHoleFieldCards(holder, actor).Where(target => target != keep))
        {
            var captured = target.Zone == FieldZone.Skill
                ? target with { Index = State.Players[actor].SkillPosition(target.Index) } : target;
            next = new EngineContinuation { Kind = ContinuationKind.BlackHoleDestroy, Actor = actor,
                Target = captured, Next = next };
        }
        Continue(next);
    }

    private void ContinueBlackHoleDestruction(EngineContinuation next)
    {
        var target = next.Target!;
        if (target.Zone == FieldZone.Skill)
        {
            var p = State.Players[target.Owner];
            var slot = Enumerable.Range(0, p.Skills.Count).FirstOrDefault(i => p.SkillPosition(i) == target.Index, -1);
            target = target with { Index = slot };
        }
        if (!FieldCards().Contains(target)) { Continue(next.Next); return; }
        next.Kind = ContinuationKind.BlackHoleCardDestroyed;
        next.Target = target;
        // Each side resolves the instruction to destroy cards in their own field.
        RequestDestruction(target, next);
    }

    private void FinishBlackHoleDestruction(EngineContinuation next)
    {
        var target = next.Target!;
        if (!next.Prevented) Log($"{State.Players[next.Actor].Name} destruye {FieldCardName(target)} por Agujero Negro.");
        if (!next.Prevented && target.Zone == FieldZone.Bonus) RefillBonus(State.Players[target.Owner], next.Next);
        else Continue(next.Next);
    }

    private void FinishBlackHole(EngineContinuation next)
    {
        if (next.Flag && State.Players[next.Actor].Area == CardCatalog.BlackHole.Id)
        {
            Log("Agujero Negro completa su efecto y se destruye.");
            RequestDestruction(new(next.Actor, FieldZone.Area, -1, CardCatalog.BlackHole.Id));
        }
        else CompletePendingPlay();
    }
}

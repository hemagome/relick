namespace Relick.Core;

internal sealed class WildAimEffect : CardRules, IAttackDamageEffect
{
    private const string SelectDiscards = "select-discards", DrawAfterDamage = "draw-after-damage";
    private const int Confirm = -1, ClearSelection = -2;

    public override string CardId => CardCatalog.WildAim.Id;

    public bool TryPrepareDamage(GameEngine.EffectContext ctx, EngineContinuation applyDamage)
    {
        if (!ctx.DiscardableHandCards().Any()) return false;
        Ask(ctx, [], applyDamage);
        return true;
    }

    private static void Ask(GameEngine.EffectContext ctx, List<int> picks, EngineContinuation applyDamage) =>
        ctx.Ask(SelectDiscards, DecisionKind.ChooseCard,
            $"Elige las cartas de tu mano que quieres descartar. Seleccionadas: {picks.Count}.",
            picks: picks, continuation: applyDamage);

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var picks = decision.Picks!;
        var choices = ctx.DiscardableHandCards().Where(card => !picks.Contains(card.Index))
            .Select(card => new DecisionChoice
            {
                Label = $"Marcar {CardCatalog.Get(card.Id).Name} · carta {card.Index + 1}", CardId = card.Id,
                Button = "Marcar descarte", Value = card.Index, Public = "marca una carta para descartar con Wild Aim."
            }).ToList();
        choices.Add(new DecisionChoice { Label = $"Resolver daño con {picks.Count} descarte(s)", Value = Confirm,
            Public = $"confirma {picks.Count} descarte(s) para Wild Aim.", RequiresAcknowledgement = true });
        if (picks.Count > 0)
            choices.Add(new DecisionChoice { Label = "Borrar selección", Value = ClearSelection, Public = "borra su selección para Wild Aim." });
        return choices;
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        var applyDamage = decision.Continuation!;
        var picks = decision.Picks!;
        if (choice.Value != Confirm)
        {
            Ask(ctx, choice.Value == ClearSelection ? [] : [.. picks, choice.Value], applyDamage);
            return;
        }

        var attack = ctx.State.PendingAttack!;
        var card = ctx.Card;
        var die = attack.NormalFromMiss ? 5 : ctx.State.LastAttackDie;
        var normal = Math.Min(card.MaxNormalDamage, card.NormalDamage + picks.Count);
        var critical = card.CriticalDamage + picks.Count;
        var damageBonus = GameEngine.DamageFor(die, normal, critical) - GameEngine.DamageFor(die, card.NormalDamage, card.CriticalDamage);
        var reward = card.Reward + 100 * picks.Count;
        var afterDamage = picks.Count >= 2 ? ctx.Then(DrawAfterDamage) : null;
        if (afterDamage != null) afterDamage.Count = picks.Count - 1;
        ctx.Announce($"{ctx.Player.Name} · {card.Name} · {attack.Damage + damageBonus} de daño · {picks.Count} descarte(s).");
        ctx.PayAttackDiscards(picks, damageBonus, reward, applyDamage, afterDamage);
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step == DrawAfterDamage) ctx.Draw(next.Count);
    }
}

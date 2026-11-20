namespace Relick.Core;

/// <summary>
/// Tótem de Salvación (stackable equipment), at the last step of the damage chain: if the damage would leave its holder
/// at 0 lives, the holder may destroy a copy to cancel it; with two or more copies, one is destroyed whenever damage
/// arrives. Its other rules (attack misses deal normal damage with a stack) are still in the engine.
/// </summary>
internal sealed class SalvationTotemEffect : CardRules, IDamageTrigger
{
    private const string Lethal = "lethal";

    public override string CardId => CardCatalog.Totem.Id;

    public DamageStep Step => DamageStep.Lethal;

    /// <summary>The equipped copies, bottom first, whose effects are active.</summary>
    internal static IEnumerable<int> ActiveCopies(GameState state, int actor, Func<int, FieldZone, int, bool> suppressed)
    {
        var p = state.Players[actor];
        return p.Equipment == CardCatalog.Totem.Id
            ? Enumerable.Range(0, p.EquipmentStackedCopies + 1).Where(index => !suppressed(actor, FieldZone.Equipment, index))
            : [];
    }

    public bool TryPause(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed)
    {
        var active = ActiveCopies(ctx.State, ctx.Actor, ctx.EffectsSuppressed).ToArray();
        if (active.Length == 0) return false;
        if (damage.Amount >= ctx.Player.Health)
        {
            ctx.Ask(Lethal, DecisionKind.Confirm,
                $"Vas a recibir {damage.Amount} de daño. Decide si destruyes un Tótem de Salvación para cancelarlo.",
                continuation: new EngineContinuation { Actor = ctx.Actor, Count = damage.Amount, Value = damage.AttackDie, Next = completed });
            return true;
        }
        if (active.Length < 2) return false;
        ctx.DestroyEquipment(ctx.HealthDamage(damage, completed), active[^1]);
        return true;
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var amount = decision.Continuation!.Count;
        return
        [
            // Names the equipment zone, so the board shows the Tótem as the card this choice uses.
            new() { Label = $"Destruir un Tótem · cancelar {amount} de daño", Button = "Cancelar daño", Value = 1, Primary = true,
                Field = new(ctx.Actor, FieldZone.Equipment, -1, CardId),
                Public = $"destruye un Tótem de Salvación para cancelar {amount} de daño." },
            new() { Label = $"Recibir {amount} de daño sin protección del Tótem", Button = "Recibir daño",
                Public = $"recibe {amount} de daño sin usar su Tótem de Salvación." }
        ];
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        var pending = decision.Continuation!;
        var damage = new DamageEvent(ctx.Actor, pending.Count, pending.Value);
        var copies = ActiveCopies(ctx.State, ctx.Actor, ctx.EffectsSuppressed).ToArray();
        if (choice.Value == 1)
        {
            ctx.State.LastCombat += " · Daño cancelado por Tótem de Salvación";
            ctx.Log($"{ctx.Player.Name} cancela {pending.Count} de daño destruyendo un Tótem de Salvación.");
            ctx.DestroyEquipment(pending.Next, copies[^1]);
        }
        // A stack still loses a copy to the damage it lets through.
        else if (copies.Length > 1) ctx.DestroyEquipment(ctx.HealthDamage(damage, pending.Next), copies[^1]);
        else ctx.Continue(ctx.HealthDamage(damage, pending.Next));
    }
}

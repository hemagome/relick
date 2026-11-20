namespace Relick.Core;

/// <summary>
/// Anillo Morado (light equipment): before receiving damage, its owner may destroy a copy to receive half the damage or
/// to gain 500 PR, copy by copy. A copy that a replacement preserved has already answered this hit. Using it outside
/// damage (gain PR on your turn) is the engine's command.
/// </summary>
internal sealed class PurpleRingEffect : CardRules, IDamageTrigger
{
    internal const int Points = 500;
    private const string Damage = "damage";
    /// <summary>The ring was destroyed (or preserved) for this hit; duels saved with the old continuation kind resume here too.</summary>
    internal const string Destroyed = "destroyed";

    public override string CardId => CardCatalog.PurpleRing.Id;

    // After Robotizar, before Escudo Galáctico.
    public DamageStep Step => DamageStep.Absorb;

    /// <summary>The player's active Anillo Morado copies.</summary>
    internal static IEnumerable<FieldTarget> Rings(GameState state, int owner, Func<int, FieldZone, int, bool> suppressed)
    {
        if (state.Players[owner].LightEquipment is not { } cards) yield break;
        for (var i = 0; i < cards.Count; i++)
            if (cards[i].CardId == CardCatalog.PurpleRing.Id && !suppressed(owner, FieldZone.LightEquipment, i))
                yield return new(owner, FieldZone.LightEquipment, i, cards[i].CardId);
    }

    internal static void AwardPoints(GameState state, int actor, Action<string> log)
    {
        var player = state.Players[actor];
        player.Relikia += Points;
        state.LastCombat = $"{player.Name} gana {Points} PR con Anillo Morado.";
        log(state.LastCombat);
    }

    public bool TryPause(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed) =>
        Offer(ctx, damage, completed, used: null);

    private static bool Offer(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed, List<int>? used)
    {
        if (damage.Amount == 0 || !Rings(ctx.State, ctx.Actor, ctx.EffectsSuppressed).Any(c => used?.Contains(c.Index) != true)) return false;
        var hit = ctx.Then(Destroyed);
        hit.Count = damage.Amount; hit.Value = damage.AttackDie; hit.Next = completed; hit.UsedLightEquipment = used;
        ctx.Ask(Damage, DecisionKind.ChooseTarget, $"Vas a recibir {damage.Amount} de daño. Elige un Anillo Morado o conserva tus anillos",
            continuation: hit);
        return true;
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var hit = decision.Continuation!;
        var choices = new List<DecisionChoice>
        {
            new() { Label = $"Recibir {hit.Count} de daño", Public = "decide conservar sus anillos." }
        };
        foreach (var ring in Rings(ctx.State, ctx.Actor, ctx.EffectsSuppressed).Where(c => hit.UsedLightEquipment?.Contains(c.Index) != true))
        {
            choices.Add(new DecisionChoice { Label = $"Destruir Anillo Morado · recibir {hit.Count / 2} de daño",
                Button = $"Recibir {hit.Count / 2} de daño", Field = ring, Primary = true, Public = "usa Anillo Morado para recibir la mitad del daño." });
            choices.Add(new DecisionChoice { Label = $"Destruir Anillo Morado · ganar {Points} PR",
                Button = $"Ganar {Points} PR", Field = ring, Value = 1, Public = $"usa Anillo Morado para ganar {Points} PR." });
        }
        return choices;
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        var hit = decision.Continuation!;
        if (choice.Field == null)
        {
            ctx.ContinueDamage(new DamageEvent(hit.Actor, hit.Count, hit.Value), hit.Next, DamageStep.Absorb);
            return;
        }
        hit.Target = choice.Field;
        hit.Flag = choice.Value == 1;
        ctx.RequestDestruction(choice.Field, hit);
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation hit)
    {
        var amount = hit.Count;
        if (hit.Flag) AwardPoints(ctx.State, hit.Actor, ctx.Log);
        else
        {
            amount /= 2;
            ctx.NotifyGuard(hit.Value > 0 ? 1 - hit.Actor : null, hit.Count, amount, blocked: false);
            ctx.State.LastCombat += $" · Anillo Morado: {amount} de daño";
            ctx.Log($"{ctx.Player.Name} reduce el daño de {hit.Count} a {amount} con Anillo Morado.");
        }
        // Reverse Shuffle can pay the destruction cost with a different card. A preserved ring
        // has already responded to this hit, even though it can respond to a later one.
        var index = hit.Target!.Index;
        var used = hit.UsedLightEquipment;
        if (hit.Prevented) (used ??= []).Add(index);
        else if (used != null)
            for (var i = 0; i < used.Count; i++)
                if (used[i] > index) used[i]--;
        var rest = new DamageEvent(hit.Actor, amount, hit.Value);
        if (!Offer(ctx, rest, hit.Next, used)) ctx.ContinueDamage(rest, hit.Next, DamageStep.Absorb);
    }
}

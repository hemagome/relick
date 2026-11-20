namespace Relick.Core;

internal sealed class GalacticShieldEffect : CardEffect, IFieldLeaveReplacement, IDamageTrigger
{
    /// <summary>Face-down cards a shield holds when placed.</summary>
    internal const int Capacity = 3;
    private const int Reduction = 2;
    private const string Damage = "damage";
    private const string Protect = "protect";

    public override string CardId => CardCatalog.GalacticShield.Id;

    // First: the shield answers before Tótem de Oro or Reverse Shuffle.
    public LeaveStep Step => LeaveStep.Protect;

    // After Anillo Morado, before the equipment counters and Tótem de Salvación.
    DamageStep IDamageTrigger.Step => DamageStep.Shield;

    /// <summary>Every shield on the player's Relikario, as a field target that keeps the copy's own instance id.</summary>
    internal static IEnumerable<FieldTarget> Shields(GameState state, int owner) =>
        Enumerable.Range(0, state.Players[owner].GalacticShields?.Count ?? 0)
            .Select(index => new FieldTarget(owner, FieldZone.GalacticShield, index,
                state.Players[owner].GalacticShields![index].Instance ?? CardCatalog.GalacticShield.Id));

    /// <summary>The shields that still hold face-down cards and keep their effects.</summary>
    internal static IEnumerable<FieldTarget> ChargedShields(GameState state, int owner, Func<int, FieldZone, int, bool> suppressed) =>
        Shields(state, owner).Where(shield =>
            state.Players[owner].GalacticShields![shield.Index].Cards.Count > 0 && !suppressed(owner, shield.Zone, shield.Index));

    /// <summary>
    /// Another card on top of the owner's Relikario would leave the field: the owner may send a face-down card to memory
    /// instead. Declining lets the destruction go on from the next step.
    /// </summary>
    public bool TryReplace(GameEngine.EffectContext ctx, FieldTarget target, FieldLeave leave, EngineContinuation? then)
    {
        if (ctx.RelikarioOwnerBelow(target) is not int owner ||
            !ChargedShields(ctx.State, owner, ctx.EffectsSuppressed).Any(shield => shield != target)) return false;
        ctx.Ask(Protect, DecisionKind.Confirm,
            $"Puedes enviar una carta boca abajo a tu memoria para evitar que {ctx.FieldName(target)} salga del campo.",
            decider: owner, continuation: new EngineContinuation { Actor = owner, Target = target, Next = then });
        return true;
    }

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Escudo Galáctico necesita colocarse sobre el Relikario y no puede activarse con Relikia." : null;

    public override void Resolve(GameEngine.EffectContext ctx) => ctx.DeployGalacticShield();

    /// <summary>Before damage: the owner of a charged shield takes its top face-down card to subtract 2, shield by shield.</summary>
    public bool TryPause(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed) =>
        OfferDamage(ctx, damage, completed, start: 0);

    private static bool OfferDamage(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed, int start)
    {
        var shield = damage.Amount <= 0 ? null
            : ChargedShields(ctx.State, ctx.Actor, ctx.EffectsSuppressed).FirstOrDefault(card => card.Index >= start);
        if (shield == null) return false;
        ctx.Ask(Damage, DecisionKind.Confirm,
            $"Vas a recibir {damage.Amount} de daño. Escudo Galáctico añade una carta boca abajo a tu mano y lo reduce en 2.",
            continuation: new EngineContinuation { Actor = ctx.Actor, Target = shield, Count = damage.Amount, Value = damage.AttackDie, Next = completed });
        return true;
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var target = decision.Continuation!.Target!;
        var takingDamage = decision.Step == Damage;
        var shields = ChargedShields(ctx.State, ctx.Actor, ctx.EffectsSuppressed)
            .Where(shield => takingDamage ? shield == target : shield != target).ToArray();
        var choices = new List<DecisionChoice>();
        foreach (var shield in shields)
        {
            // The charges are face down, so the player picks a shield and takes its top card.
            var label = takingDamage ? "Añadir una carta boca abajo a tu mano" : "Enviar una carta boca abajo a memoria";
            if (shields.Length > 1) label += $" · Escudo {shield.Index + 1}";
            choices.Add(new DecisionChoice
            {
                Label = label, Value = shield.Index * Capacity,
                Public = takingDamage ? "añade una carta boca abajo de Escudo Galáctico a su mano."
                    : $"envía una carta boca abajo a memoria para conservar {ctx.FieldName(target)}."
            });
        }
        if (!takingDamage)
            choices.Add(new DecisionChoice { Label = "Pasar sin usar el escudo", Value = -1, Public = "decide no usar Escudo Galáctico." });
        return choices;
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        var pending = decision.Continuation!;
        if (choice.Value < 0)
        {
            if (pending.Next is { Kind: ContinuationKind.FieldReturned } returned)
            {
                ctx.ReturnFieldCardNow(pending.Target!);
                ctx.Continue(returned);
            }
            else ctx.RequestDestruction(pending.Target!, pending.Next, after: LeaveStep.Protect);
            return;
        }
        var player = ctx.Player;
        var slot = choice.Value / Capacity;
        var position = choice.Value % Capacity;
        var cards = player.GalacticShields![slot].Cards;
        var card = cards[position];
        cards.RemoveAt(position);
        if (decision.Step == Protect)
        {
            ctx.SendToMemory(card.CardId, CardZone.ShieldCharge, slot, card.CorruptOnMemory);
            if (pending.Next != null) pending.Next.Prevented = true;
            ctx.Log($"{player.Name} conserva {ctx.FieldName(pending.Target!)} gracias a Escudo Galáctico.");
            ctx.Continue(pending.Next);
            return;
        }
        if (card.CorruptOnMemory) player.MarkForCorruption(CardZone.Hand, player.Hand.Count);
        player.Hand.Add(card.CardId);
        ctx.NotifyPileMove(card.CardId, CardZone.ShieldCharge, player.Hand.Count - 1, CardZone.Hand,
            destinationPlayer: pending.Actor, sourceIndex: slot);
        var damage = new DamageEvent(pending.Actor, pending.Count, pending.Value);
        var reduced = Math.Max(0, pending.Count - Reduction);
        ctx.NotifyGuard(damage.Attacker, pending.Count, reduced, blocked: false);
        ctx.State.LastCombat += $" · Escudo Galáctico: {reduced} de daño";
        ctx.Log($"{player.Name} añade una carta boca abajo a su mano y reduce el daño de {pending.Count} a {reduced} con Escudo Galáctico.");
        var rest = damage with { Amount = reduced };
        if (!OfferDamage(ctx, rest, pending.Next, start: slot + 1)) ctx.ContinueDamage(rest, pending.Next, DamageStep.Shield);
    }
}

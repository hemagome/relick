namespace Relick.Core;

public sealed record PendingEffectDamage(int Actor, int Damage, bool Draw);

public sealed partial class GameEngine
{
    internal bool IsFieldCodeActivation => _activeActivation is { Kind: ActivationKind.FieldCode };
    public IEnumerable<FieldTarget> FieldCombatTargets(int actor)
    {
        if (ValidateTurn(actor, Phase.Combat) != null || _activeActivation != null) yield break;
        var p = State.Players[actor];
        if (p.Bonus is { } bonus && CardCatalog.Get(bonus).FieldCombatDie && !p.BonusEffectUsed)
            yield return new(actor, FieldZone.Bonus, -1, bonus);
        for (var index = 0; index < p.Exchange.Count; index++)
            if (CardCatalog.Get(p.Exchange[index]).FieldCombatDie && !(p.ExchangeEffectsUsed?.Contains(index) ?? false))
                yield return new(actor, FieldZone.Exchange, index, p.Exchange[index]);
    }

    public CommandResult UseFieldCode(int actor, FieldZone zone, int index = -1)
    {
        var target = FieldCombatTargets(actor).FirstOrDefault(t => t.Zone == zone && t.Index == index);
        if (target == null) return CommandResult.Fail("Elige una Jessica Lee disponible de tu canje o Bonus durante tu etapa de combate.");
        var p = State.Players[actor];
        if (zone == FieldZone.Bonus) p.BonusEffectUsed = true;
        else { p.ExchangeEffectsUsed ??= []; p.ExchangeEffectsUsed.Add(index); }
        Log($"{p.Name} activa {CardCatalog.Get(target.CardId).Name} desde {(zone == FieldZone.Bonus ? "su Bonus" : "su canje")} sin gastar acciones ni PR.");
        var placedTurn = zone == FieldZone.Bonus ? p.BonusPlacedTurn : p.ExchangePlacementTurns?.GetValueOrDefault(index) ?? 0;
        BeginActivation(new EngineActivation(actor, target.CardId, 0, 0, Phase.Combat)
            { Kind = ActivationKind.FieldCode, FieldSource = target, CanInterrupt = placedTurn > 0 && placedTurn == State.Turn });
        return CommandResult.Ok();
    }

    private void ResolveFieldCombatDie(DieEffect effect, int die)
    {
        var p = State.Players[effect.Actor]; var damage = die == 5 ? 2 : die == 6 ? 3 : 0;
        if (damage > 0) damage = DamageWithEquipmentBonus(effect.Actor, damage);
        if (_activeActivation is { } frame) frame.CanInterrupt = false;
        State.LastCombat = $"{p.Name} · Jessica Lee · D6 = {die} · {damage} daño(s)" + (die == 6 ? " y roba 1 carta" : "");
        Log(State.LastCombat);
        var pending = new PendingEffectDamage(effect.Actor, damage, die == 6);
        if (damage > 0 && HasTurnBuffInHand(1 - effect.Actor))
        {
            State.PendingEffectDamage = pending; State.Phase = Phase.EffectDamageChoice;
            return;
        }
        ApplyEffectDamage(pending);
    }

    public CommandResult ResolveEffectDamage(int actor)
    {
        if (State.Phase != Phase.EffectDamageChoice || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde decidir este daño de efecto.");
        var pending = State.PendingEffectDamage!;
        State.PendingEffectDamage = null; State.Phase = Phase.Combat;
        ApplyEffectDamage(pending); CompleteActivationIfFinished();
        return CommandResult.Ok();
    }

    private void ApplyEffectDamage(PendingEffectDamage pending)
    {
        DealDamage(1 - pending.Actor, pending.Damage, completed: new EngineContinuation
            { Kind = ContinuationKind.FieldCodeReward, Actor = pending.Actor, Flag = pending.Draw }, source: pending.Actor);
    }

    private void ForgetFieldCodeSource(PlayerState p, FieldZone zone, int index = -1)
    {
        var actor = Array.IndexOf(State.Players, p);
        if (zone == FieldZone.Bonus) { p.BonusEffectUsed = false; p.BonusPlacedTurn = 0; }
        for (var frame = _activeActivation; frame != null; frame = frame.Parent)
            if (frame.FieldSource is { } source && source.Owner == actor && source.Zone == zone && source.Index == index)
                frame.FieldSource = null;
    }

    private void ShiftExchangeEffects(PlayerState p, int removed, int count)
    {
        if (p.ExchangePlacementTurns != null)
        {
            p.ExchangePlacementTurns = p.ExchangePlacementTurns.Where(entry => entry.Key < removed || entry.Key >= removed + count)
                .ToDictionary(entry => entry.Key >= removed + count ? entry.Key - count : entry.Key, entry => entry.Value);
            if (p.ExchangePlacementTurns.Count == 0) p.ExchangePlacementTurns = null;
        }
        if (p.ExchangeEffectsUsed != null)
        {
            p.ExchangeEffectsUsed = p.ExchangeEffectsUsed.Where(i => i < removed || i >= removed + count)
                .Select(i => i >= removed + count ? i - count : i).ToList();
            if (p.ExchangeEffectsUsed.Count == 0) p.ExchangeEffectsUsed = null;
        }
        var actor = Array.IndexOf(State.Players, p);
        for (var frame = _activeActivation; frame != null; frame = frame.Parent)
            if (frame.FieldSource is { Zone: FieldZone.Exchange } source && source.Owner == actor && source.Index >= removed + count)
                frame.FieldSource = source with { Index = source.Index - count };
    }
}

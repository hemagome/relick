namespace Relick.Core;

public sealed class PendingTotemDamage
{
    public int Target { get; set; }
    public int Amount { get; set; }
    public int AttackDie { get; set; }
    public Phase ResumePhase { get; set; }
    public EngineContinuation? Completed { get; set; }
}

public sealed partial class GameEngine
{
    private IEnumerable<int> ActiveTotems(int actor)
    {
        var p = State.Players[actor];
        return p.Equipment == CardCatalog.Totem.Id
            ? Enumerable.Range(0, p.EquipmentStackedCopies + 1).Where(index => !EffectsSuppressed(actor, FieldZone.Equipment, index))
            : [];
    }

    private bool HasStackedTotem(int actor) => ActiveTotems(actor).Count() > 1;

    private bool TryTotemDamage(int target, int amount, int attackDie, EngineContinuation? completed)
    {
        var p = State.Players[target];
        var active = ActiveTotems(target).ToArray();
        if (active.Length == 0) return false;
        if (amount >= p.Health)
        {
            State.PendingTotemDamage = new()
            { Target = target, Amount = amount, AttackDie = attackDie, ResumePhase = State.Phase, Completed = completed };
            State.Phase = Phase.TotemChoice;
            State.LastCombat += $" · {p.Name}: puedes destruir un Tótem de Salvación para cancelar {amount} de daño.";
            return true;
        }
        if (active.Length < 2) return false;
        DestroyEquipment(p, new EngineContinuation
            { Kind = ContinuationKind.ApplyDamage, Actor = target, Count = amount, Value = attackDie, Next = completed }, active[^1]);
        return true;
    }

    public CommandResult ResolveTotem(int actor, bool cancelDamage)
    {
        var pending = State.PendingTotemDamage;
        if (State.Phase != Phase.TotemChoice || pending?.Target != actor)
            return CommandResult.Fail("No te corresponde decidir la protección del Tótem.");
        State.PendingTotemDamage = null; State.Phase = pending.ResumePhase;
        var p = State.Players[actor];
        if (cancelDamage)
        {
            State.LastCombat += " · Daño cancelado por Tótem de Salvación";
            Log($"{p.Name} cancela {pending.Amount} de daño destruyendo un Tótem de Salvación.");
            DestroyEquipment(p, pending.Completed, ActiveTotems(actor).Last());
        }
        else
        {
            var damage = new EngineContinuation { Kind = ContinuationKind.ApplyDamage, Actor = actor,
                Count = pending.Amount, Value = pending.AttackDie, Next = pending.Completed };
            if (HasStackedTotem(actor)) DestroyEquipment(p, damage, ActiveTotems(actor).Last());
            else Continue(damage);
        }
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
}

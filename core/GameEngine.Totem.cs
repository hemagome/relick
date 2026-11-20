namespace Relick.Core;

/// <summary>Only in duels saved before Tótem de Salvación used decisions; converted to one when loaded.</summary>
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
    private bool HasStackedTotem(int actor) => SalvationTotemEffect.ActiveCopies(State, actor, EffectsSuppressed).Count() > 1;

    /// <summary>The command from before Tótem de Salvación used decisions; recorded duels still send it.</summary>
    public CommandResult ResolveTotem(int actor, bool cancelDamage) =>
        ResolveDecisionWhere(actor, CardCatalog.Totem.Id, choice => (choice.Value == 1) == cancelDamage)
        ?? CommandResult.Fail("No te corresponde decidir la protección del Tótem.");
}

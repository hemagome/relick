namespace Relick.Core;

public sealed partial class GameEngine
{
    private bool HasStackedTotem(int actor) => SalvationTotemEffect.ActiveCopies(State, actor, EffectsSuppressed).Count() > 1;

    /// <summary>The command from before Tótem de Salvación used decisions; recorded duels still send it.</summary>
    public CommandResult ResolveTotem(int actor, bool cancelDamage) =>
        ResolveDecisionWhere(actor, CardCatalog.Totem.Id, choice => (choice.Value == 1) == cancelDamage)
        ?? CommandResult.Fail("No te corresponde decidir la protección del Tótem.");
}

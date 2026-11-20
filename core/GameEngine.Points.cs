namespace Relick.Core;

public sealed partial class GameEngine
{
    public CommandResult ResolvePointsChange(int actor, int target, bool increase)
    {
        // Aumento de Relick Points now asks through a decision; this command still answers it.
        if (ResolveDecisionWhere(actor, CardCatalog.PointsIncrease.Id, c => c.Value == target * 2 + (increase ? 1 : 0)) is { } answered) return answered;
        return CommandResult.Fail("No te corresponde resolver este cambio de PR.");
    }
}

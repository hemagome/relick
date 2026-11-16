namespace Relick.Core;

public sealed partial class GameEngine
{
    public CommandResult ResolvePointsChange(int actor, int target, bool increase)
    {
        // Aumento de Relick Points now asks through a decision; this command still answers it.
        if (ResolveDecisionWhere(actor, CardCatalog.PointsIncrease.Id, c => c.Value == target * 2 + (increase ? 1 : 0)) is { } answered) return answered;
        // Legacy: a duel saved while the points phase was pending.
        if (State.Phase != Phase.PointsChoice || State.PendingPoints is not { } pending || pending.Actor != actor || actor != State.CurrentPlayer)
            return CommandResult.Fail("No te corresponde resolver este cambio de PR.");
        if (target < 0 || target >= State.Players.Length) return CommandResult.Fail("Elige un jugador válido.");
        var p = State.Players[target];
        var before = p.Relikia;
        p.Relikia = increase ? p.Relikia + pending.Amount : Math.Max(0, p.Relikia - pending.Amount);
        State.PendingPoints = null;
        State.Phase = Phase.Actions; CompleteActivationIfFinished();
        State.LastCombat = $"{State.Players[actor].Name} · Aumento de Relick Points · {p.Name}: {(increase ? "+" : "−")}{Math.Abs(p.Relikia - before)} PR";
        Log($"{State.Players[actor].Name} elige {(increase ? "aumentar" : "disminuir")} {pending.Amount} PR de {p.Name}: {before} → {p.Relikia}.");
        return CommandResult.Ok();
    }
}

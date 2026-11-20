namespace Relick.Core;

/// <summary>Aumento de Relick Points: roll 1D6 and raise or lower any player's PR by 300 to 1500; PR never drop below zero.</summary>
internal sealed class PointsIncreaseEffect : CardEffect
{
    private const string Roll = "roll", Target = "target";

    public override string CardId => CardCatalog.PointsIncrease.Id;
    public override void Resolve(GameEngine.EffectContext ctx) => ctx.Roll(DiePurpose.PointsChange, Roll);

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        var die = next.Value;
        var points = die switch { 1 => 300, 2 => 400, 3 or 4 => 500, 5 => 1000, 6 => 1500, _ => 0 };
        if (points == 0) return;
        ctx.Announce($"{ctx.Player.Name} · Aumento de Relick Points · D6 = {die} · Elige destino de {points} PR");
        ctx.Ask(Target, DecisionKind.Confirm, $"Elige a quién sumar o restar {points} PR", allowsCombos: true, announce: false, value: points);
    }

    // Value packs the seat and the direction: seat × 2, plus 1 to increase.
    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        Enumerable.Range(0, ctx.State.Players.Length).SelectMany(seat => new[]
        {
            new DecisionChoice { Label = $"Aumentar PR de {ctx.PlayerName(seat)}", Button = seat == ctx.Actor ? $"+{decision.Value} a mis PR" : $"+{decision.Value} al rival",
                Value = seat * 2 + 1, Primary = seat == ctx.Actor, Public = $"aumenta los PR de {ctx.PlayerName(seat)}." },
            new DecisionChoice { Label = $"Disminuir PR de {ctx.PlayerName(seat)}", Button = seat == ctx.Actor ? $"−{decision.Value} a mis PR" : $"−{decision.Value} al rival",
                Value = seat * 2, Public = $"reduce los PR de {ctx.PlayerName(seat)}." }
        }).ToList();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        var target = ctx.State.Players[choice.Value / 2]; var increase = choice.Value % 2 == 1; var before = target.Relikia;
        target.Relikia = increase ? target.Relikia + decision.Value : Math.Max(0, target.Relikia - decision.Value);
        ctx.State.LastCombat = $"{ctx.Player.Name} · Aumento de Relick Points · {target.Name}: {(increase ? "+" : "−")}{Math.Abs(target.Relikia - before)} PR";
        ctx.Log($"{ctx.Player.Name} elige {(increase ? "aumentar" : "disminuir")} {decision.Value} PR de {target.Name}: {before} → {target.Relikia}.");
    }
}

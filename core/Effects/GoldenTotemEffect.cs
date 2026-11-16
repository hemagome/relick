namespace Relick.Core;

/// <summary>
/// Tótem de Oro: show up to 2 Tótems de Salvación from your hand and equip them, then place this card on top of your
/// equipment. It needs an equipment underneath and cannot be activated with Relikia. The placement, with its
/// destruction of the previous equipment and the cover that protects the Tótems, is the engine's own sequence.
/// </summary>
internal sealed class GoldenTotemEffect : ICardEffect
{
    private const string Show = "show";
    private const int MaxTotems = 2;

    public string CardId => CardCatalog.GoldenTotem.Id;

    private static IEnumerable<int> Totems(PlayerState p) =>
        Enumerable.Range(0, p.Hand.Count).Where(i => p.Hand[i] == CardCatalog.Totem.Id);

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand)
    {
        if (excludedHand < 0) return "Tótem de Oro no puede activarse con Relikia.";
        return ctx.Player.Equipment == null && !Totems(ctx.Player).Any() ? "Tótem de Oro necesita un equipo debajo o un Tótem de Salvación en tu mano." : null;
    }

    public void Resolve(GameEngine.EffectContext ctx)
    {
        if (ctx.Player.Equipment == null && !Totems(ctx.Player).Any())
        {
            ctx.Log("Tótem de Oro se resuelve sin un equipo sobre el cual colocarse.");
            return;
        }
        ctx.Ask(Show, DecisionKind.Confirm, "Elige hasta dos Tótems de Salvación de tu mano. Necesitas un equipo debajo.");
    }

    // The copies are identical, so the choice is how many to show; with no equipment at least one is needed.
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var most = Math.Min(MaxTotems, Totems(ctx.Player).Count());
        return Enumerable.Range(0, most + 1).Where(count => count > 0 || ctx.Player.Equipment != null).Select(count => new DecisionChoice
        {
            Label = count == 0 ? "Colocar Tótem de Oro sin mostrar Tótems" : $"Mostrar y colocar {count} Tótem(s) de Salvación",
            Button = count == 0 ? "Colocar sin Tótems" : $"Mostrar {count} Tótem{(count == 1 ? "" : "s")}", Value = count, Primary = count == most,
            Public = $"coloca Tótem de Oro con {count} Tótem(s) de Salvación."
        }).ToList();
    }

    // The first copies in hand order, as the former test bot chose them.
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.DeployGoldenTotem(Totems(ctx.Player).Take(choice.Value).ToArray());

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}

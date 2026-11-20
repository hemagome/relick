namespace Relick.Core;

/// <summary>
/// Tótem de Oro: show up to 2 Tótems de Salvación from your hand and equip them, then place this card on top of your
/// equipment. It needs an equipment underneath and cannot be activated with Relikia. The placement, with its
/// destruction of the previous equipment and the cover that protects the Tótems, is the engine's own sequence.
/// </summary>
internal sealed class GoldenTotemEffect : CardEffect, IFieldLeaveReplacement
{
    private const string Show = "show";
    private const int MaxTotems = 2;
    /// <summary>The cover was destroyed in the Tótem's place; duels saved with the old continuation kind resume here too.</summary>
    internal const string Covered = "covered";

    public override string CardId => CardCatalog.GoldenTotem.Id;

    // After Escudo Galáctico, before Reverse Shuffle.
    public LeaveStep Step => LeaveStep.Cover;

    /// <summary>An equipped Tótem de Salvación would be destroyed: the last active Tótem de Oro on top is destroyed instead.</summary>
    public bool TryReplace(GameEngine.EffectContext ctx, FieldTarget target, FieldLeave leave, EngineContinuation? then)
    {
        if (leave != FieldLeave.Destroy || target.Zone != FieldZone.Equipment || target.CardId != CardCatalog.Totem.Id) return false;
        var covers = ctx.State.Players[target.Owner].EquipmentCovers;
        var cover = Enumerable.Range(0, covers?.Count ?? 0).LastOrDefault(index =>
            CardCatalog.TextId(covers![index]) == CardId && !ctx.EffectsSuppressed(target.Owner, FieldZone.EquipmentCover, index), -1);
        if (cover < 0) return false;
        var covered = ctx.Then(Covered);
        covered.Next = then;
        ctx.RequestDestruction(new(target.Owner, FieldZone.EquipmentCover, cover, CardId), covered);
        return true;
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step != Covered) return;
        if (next.Next != null) next.Next.Prevented = true;
        ctx.Log($"{ctx.Player.Name} conserva su Tótem de Salvación gracias a la sustitución de Tótem de Oro.");
        ctx.Continue(next.Next);
    }

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand)
    {
        if (excludedHand < 0) return "Tótem de Oro no puede activarse con Relikia.";
        return ctx.Player.Equipment == null && !ctx.PlayableTotems().Any() ? "Tótem de Oro necesita un equipo debajo o un Tótem de Salvación en tu mano." : null;
    }

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (ctx.Player.Equipment == null && !ctx.PlayableTotems().Any())
        {
            ctx.Log("Tótem de Oro se resuelve sin un equipo sobre el cual colocarse.");
            return;
        }
        ctx.Ask(Show, DecisionKind.Confirm, "Elige hasta dos Tótems de Salvación de tu mano. Necesitas un equipo debajo.");
    }

    // The copies are identical, so the choice is how many to show; with no equipment at least one is needed.
    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var most = Math.Min(MaxTotems, ctx.PlayableTotems().Count());
        return Enumerable.Range(0, most + 1).Where(count => count > 0 || ctx.Player.Equipment != null).Select(count => new DecisionChoice
        {
            Label = count == 0 ? "Colocar Tótem de Oro sin mostrar Tótems" : $"Mostrar y colocar {count} Tótem(s) de Salvación",
            Button = count == 0 ? "Colocar sin Tótems" : $"Mostrar {count} Tótem{(count == 1 ? "" : "s")}", Value = count, Primary = count == most,
            Public = $"coloca Tótem de Oro con {count} Tótem(s) de Salvación."
        }).ToList();
    }

    // The first copies in hand order, as the former test bot chose them.
    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.DeployGoldenTotem(ctx.PlayableTotems().Take(choice.Value).ToArray());
}

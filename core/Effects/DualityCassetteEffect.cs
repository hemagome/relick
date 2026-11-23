namespace Relick.Core;

internal sealed class DualityCassetteEffect : CardEffect
{
    private const string Rolled = "rolled", Recover = "recover", Extra = "extra";

    public override string CardId => CardCatalog.DualityCassette.Id;
    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.Player.Deck.Count == 0 ? "Necesitas cartas en tu mazo para corromper." : null;

    public override void Resolve(GameEngine.EffectContext ctx) => ctx.Roll(DiePurpose.DualityCassette, Rolled);

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step == Recover)
        {
            AskRecovery(ctx, next, (next.Count + 1) / 2);
            return;
        }

        ctx.CloseErrorWindow();
        var batch = ctx.Then(Recover);
        batch.Value = ctx.Player.CorruptedMemory.Count;
        batch.Count = ctx.CorruptDeckTop(next.Value);
        batch.Flag = next.Value == 6;
        ctx.Announce($"{ctx.Card.Name} · Dado {next.Value} · {ctx.Player.Name} corrompe {batch.Count} carta(s) de su mazo.");
        if (batch.Count > 0 && ctx.Player.Deck.Count == 0) ctx.Exhaust(batch);
        else ctx.Continue(batch);
    }

    private static void AskRecovery(GameEngine.EffectContext ctx, EngineContinuation batch, int remaining)
    {
        if (remaining > 0)
            ctx.Ask(Recover, DecisionKind.ChooseCard, $"Elige {remaining} de las cartas que corrompió el Casete.",
                value: remaining, continuation: batch);
        else if (batch.Flag && ctx.State.Players.Any(player => player.CorruptedMemory.Count > 0))
            ctx.Ask(Extra, DecisionKind.ChooseCard, "Dado 6 · Elige una carta corrupta adicional.");
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        if (decision.Step == Extra)
            return ctx.State.Players.SelectMany((player, owner) => Enumerable.Range(0, player.CorruptedMemory.Count)
                .Select(index => Choice(ctx, owner, index))).ToArray();

        // Earlier picks remove entries from the same contiguous batch, so its start stays fixed.
        var batch = decision.Continuation!;
        return Enumerable.Range(batch.Value, batch.Count).Select(index => Choice(ctx, ctx.Actor, index)).ToArray();
    }

    private static DecisionChoice Choice(GameEngine.EffectContext ctx, int owner, int index)
    {
        var id = ctx.State.Players[owner].CorruptedMemory[index];
        return new DecisionChoice
        {
            Label = $"{CardCatalog.Get(id).Name} · corruptas de {ctx.PlayerName(owner)} · carta {index + 1}",
            CardId = id, Owner = owner, Value = index, Button = "Añadir a mano",
            Public = $"añade una carta corrupta de {ctx.PlayerName(owner)} a su mano."
        };
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.TakeCorruptedCard(choice.Owner!.Value, choice.Value);
        if (decision.Step != Recover) return;

        var batch = decision.Continuation!;
        batch.Count--;
        AskRecovery(ctx, batch, decision.Value - 1);
    }
}

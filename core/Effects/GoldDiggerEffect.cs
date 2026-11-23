namespace Relick.Core;

internal sealed class GoldDiggerEffect : CardRules, ISkillAction
{
    public override string CardId => CardCatalog.GoldDigger.Id;

    public bool CanActivate(GameEngine.EffectContext ctx) => !ctx.Player.GoldDiggerUsed;

    public void Activate(GameEngine.EffectContext ctx)
    {
        ctx.Player.GoldDiggerUsed = true;
        ctx.Roll(DiePurpose.GoldDigger, "dig");
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        var milled = ctx.MillDeckTop(Math.Max(0, next.Value - 2));
        if (milled.Count > 0)
            ctx.Ask("recover", DecisionKind.ChooseCard, "Puedes añadir una de las cartas enviadas a tu memoria.",
                picks: milled);
        else ctx.ExhaustIfEmpty();
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        decision.Picks!.Select(i => LetterEffect.CardChoice(ctx.Player.Memory[i], i, "Añadir a la mano"))
            .Append(new() { Label = "Dejar las cartas en memoria", Value = -1 }).ToArray();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value >= 0)
        {
            var card = CardCatalog.Get(ctx.TakeFromMemory(ctx.Actor, choice.Value));
            if (card.Letter.HasValue)
            {
                ctx.Player.Relikia += 500;
                ctx.Log($"{ctx.Player.Name} gana 500 PR con {card.Name}.");
            }
        }
        ctx.ExhaustIfEmpty();
    }
}

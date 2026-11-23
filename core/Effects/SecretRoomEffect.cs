namespace Relick.Core;

internal sealed class SecretRoomEffect : CardRules, IExchangeTrigger
{
    private const string Recover = "recover", Trap = "trap";
    private const int BonusReward = 3000;

    public override string CardId => CardCatalog.SecretRoom.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        "Habitación Secreta activa su efecto al llegar al canje o al nuevo Bonus de un canje.";

    public bool Fire(GameEngine.EffectContext ctx, ExchangeMoment moment)
    {
        if (moment == ExchangeMoment.EnteredExchange)
        {
            if (ctx.Player.Memory.Count == 0) return false;
            ctx.Ask(Recover, DecisionKind.ChooseCard, "Elige una carta de tu memoria para añadir a tu mano.");
        }
        else
        {
            ctx.Player.Relikia += BonusReward;
            ctx.Announce($"{ctx.Player.Name} gana 3.000 PR con {ctx.Card.Name}.");
            if (!ctx.AvailableTrapCards().Any()) return false;
            ctx.Ask(Trap, DecisionKind.ChooseCard, "Elige un Código Trampa de tu mazo de Códigos Trampa.");
        }
        return true;
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        if (decision.Step == Trap)
            return ctx.AvailableTrapCards().Select(id => new DecisionChoice
            {
                Label = $"Colocar {CardCatalog.Get(id).Name}", CardId = id, Button = "Colocar trampa",
                Public = $"elige {CardCatalog.Get(id).Name} con {ctx.Card.Name}."
            }).ToArray();

        return ctx.Player.Memory.Select((id, index) => new DecisionChoice
        {
            Label = $"Añadir {CardCatalog.Get(id).Name} · memoria {index + 1}", CardId = id, Owner = ctx.Actor, Value = index,
            Button = "Añadir a mano", Public = $"recupera una carta de su memoria con {ctx.Card.Name}."
        }).ToArray();
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (decision.Step == Trap) ctx.PlaceTrap(choice.CardId!);
        else ctx.TakeFromMemory(ctx.Actor, choice.Value);
    }
}

namespace Relick.Core;

/// <summary>
/// Fantasma Ladrón (equipment): when it forces a low die, its rival reveals the hand and hands over an Object.
/// The engine opens the decision for the player who gives the card; this module answers it.
/// </summary>
internal sealed class ThiefGhostEffect : CardRules
{
    public override string CardId => CardCatalog.ThiefGhost.Id;

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var choices = ctx.Player.Hand.Select((id, index) => (id, index)).Where(c => CardCatalog.Get(c.id).CodeType == CodeType.Object)
            .Select(c => new DecisionChoice
            {
                CardId = c.id, Value = c.index, Label = $"Entregar {CardCatalog.Get(c.id).Name} · carta {c.index + 1}",
                Button = "Entregar", RequiresAcknowledgement = true, Public = $"entrega {CardCatalog.Get(c.id).Name} al adversario."
            }).ToArray();
        return choices.Length > 0 ? choices : [new()
        {
            Value = -1, Label = "Continuar sin entregar", RequiresAcknowledgement = true,
            Public = "muestra su mano sin Objetos."
        }];
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value >= 0) ctx.GiveHandCard(choice.Value);
        ctx.Continue(decision.Continuation);
    }
}

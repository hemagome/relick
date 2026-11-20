namespace Relick.Core;

/// <summary>Disquete: take a card from any memory into your hand; never the copy being activated, though another Disquete may be taken.</summary>
internal sealed class FloppyDiskEffect : CardEffect
{
    private const string Pick = "pick";

    public override string CardId => CardCatalog.FloppyDisk.Id;

    // While in hand the activating copy is not in a memory yet, so any card there is a valid target.
    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.State.Players.Sum(player => player.Memory.Count) <= (excludedHand < 0 ? 1 : 0) ? "No hay cartas disponibles en las memorias." : null;

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (!ctx.RecoverableMemoryCards().Any())
        {
            ctx.Log("Disquete se resuelve sin cartas disponibles en las memorias después de las respuestas.");
            return;
        }
        ctx.Ask(Pick, DecisionKind.ChooseCard, "Elige una carta de cualquier memoria para añadirla a tu mano.", allowsCombos: true);
    }

    // Memories are public, so every choice names its card; the gallery groups them by owner.
    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.RecoverableMemoryCards().Select(m =>
        {
            var id = ctx.State.Players[m.Owner].Memory[m.Index];
            return new DecisionChoice
            {
                Label = $"Recuperar {CardCatalog.Get(id).Name} · memoria de {ctx.PlayerName(m.Owner)} · carta {m.Index + 1}", Button = "Recuperar",
                CardId = id, Owner = m.Owner, Value = m.Index, Public = $"recupera una carta de la memoria de {ctx.PlayerName(m.Owner)}."
            };
        }).ToList();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.EndActivation();
        ctx.TakeFromMemory(choice.Owner!.Value, choice.Value);
    }
}

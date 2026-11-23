namespace Relick.Core;

internal sealed class DestinyPickEffect : CardEffect
{
    private const string Mode = "mode";
    private const string Corruption = "corruption";
    private const string Finished = "finished";
    private const int Draw = 0, Damage = 1, Corrupt = 2;

    public override string CardId => CardCatalog.DestinyPick.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.CanPlayDestinyPick() ? null : "Completa la resolución pendiente antes de jugar Púa del Destino.";

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        ctx.CloseErrorWindow();
        ctx.Ask(Mode, DecisionKind.Confirm, "Escoge un efecto de Púa del Destino.");
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        return decision.Step == Mode ? ModeChoices(ctx) : ctx.ResettableMemoryCards().Select(slot =>
        {
            var card = CardCatalog.Get(ctx.State.Players[slot.Owner].Memory[slot.Index]);
            return new DecisionChoice
            {
                Label = $"Corromper {card.Name} · memoria de {ctx.PlayerName(slot.Owner)}",
                Button = "Corromper", Owner = slot.Owner, CardId = card.Id, Value = slot.Index,
                Public = $"corrompe una carta de la memoria de {ctx.PlayerName(slot.Owner)} con Púa del Destino."
            };
        }).ToArray();
    }

    private static IReadOnlyList<DecisionChoice> ModeChoices(GameEngine.EffectContext ctx)
    {
        var choices = new List<DecisionChoice>
        {
            new() { Label = "Robar 1 carta", Value = Draw, Public = "elige robar 1 carta con Púa del Destino." },
            new() { Label = "Hacer 1 daño no letal", Value = Damage, TargetPlayer = 1 - ctx.Actor,
                Public = "elige hacer 1 daño no letal con Púa del Destino." }
        };
        if (ctx.ResettableMemoryCards().Any())
            choices.Add(new() { Label = "Corromper 1 carta", Value = Corrupt, Public = "elige corromper una carta con Púa del Destino." });
        return choices;
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (decision.Step == Corruption)
        {
            ctx.CorruptMemoryCard(choice.Owner!.Value, choice.Value);
            ctx.EndActivation();
            return;
        }

        switch (choice.Value)
        {
            case Draw: ctx.Draw(1, ctx.Then(Finished)); break;
            case Damage: ctx.DealNonlethalDamage(1, ctx.Then(Finished)); break;
            case Corrupt: ctx.Ask(Corruption, DecisionKind.ChooseCard, "Elige una carta de cualquier memoria para corromperla."); break;
        }
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        ctx.EndActivation();
    }
}

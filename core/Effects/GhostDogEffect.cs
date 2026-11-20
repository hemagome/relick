namespace Relick.Core;

/// <summary>
/// Perro Fantasma: revealed in hand, it activates at the start of its owner's next action stage. Roll 1D6 (or choose the
/// result, when the activation allows it). 2: take a Bonus; 3–4: a memory card; 5–6: a corrupted card, locked for the turn.
/// </summary>
internal sealed class GhostDogEffect : CardEffect, ITurnTrigger
{
    private const string Result = "result";
    private const string Rolled = "rolled";
    private const string Recover = "recover";

    public override string CardId => CardCatalog.GhostDog.Id;

    public TurnMoment Moment => TurnMoment.ActionStart;
    // Before Robotizar: the revealed copies resolve first, from the lowest hand slot.
    public int Order => 0;

    /// <summary>Activates the next revealed copy still in hand; each one comes back through the pending play.</summary>
    public bool Fire(GameEngine.EffectContext ctx)
    {
        var reveals = ctx.Player.GhostDogReveals;
        while (reveals is { Count: > 0 })
        {
            var index = reveals.Min();
            if (ctx.PlayableHandCard(index) != CardId)
            {
                reveals.Remove(index);
                continue;
            }
            ctx.ActivateFromHandForFree(index, choosesDieResult: true);
            return true;
        }
        return false;
    }

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (ctx.ChoosesDieResult)
        {
            ctx.Ask(Result, DecisionKind.Confirm, "Escoge el resultado de Perro Fantasma.");
            return;
        }
        ctx.Roll(DiePurpose.GhostDog, Rolled);
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => decision.Step == Result
        ? [
            new() { Label = "1 · Sin efecto", Value = 1 },
            new() { Label = "2 · Tomar un Bonus", Value = 2 },
            new() { Label = "3–4 · Recuperar de la memoria", Value = 3 },
            new() { Label = "5–6 · Recuperar una carta corrompida", Value = 5 }
          ]
        : Cards(ctx, decision.Value).ToArray();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (decision.Step == Result) { AskForCard(ctx, choice.Value); return; }
        var owner = choice.Owner!.Value;
        if (decision.Value == 2)
        {
            ctx.TakeBonus(owner);
            ctx.RefillBonus(owner, null);
        }
        else if (decision.Value <= 4) ctx.TakeFromMemory(owner, choice.Value);
        else
        {
            var index = ctx.TakeCorruptedCard(owner, choice.Value);
            (ctx.Player.LockedGhostCards ??= []).Add(index);
        }
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => AskForCard(ctx, next.Value);

    private static void AskForCard(GameEngine.EffectContext ctx, int die)
    {
        if (Cards(ctx, die).Any())
            ctx.Ask(Recover, DecisionKind.ChooseCard, "Elige la carta que quieres añadir a tu mano.", value: die);
        else ctx.Log($"Perro Fantasma se resuelve sin cartas disponibles para el resultado {die}.");
    }

    private static IEnumerable<DecisionChoice> Cards(GameEngine.EffectContext ctx, int die)
    {
        for (var owner = 0; owner < ctx.State.Players.Length; owner++)
        {
            var p = ctx.State.Players[owner];
            if (die == 2 && p.Bonus is { } bonus)
                yield return Choice(ctx, owner, -1, bonus, "Bonus");
            if (die is 3 or 4)
                for (var i = 0; i < p.Memory.Count; i++)
                    if (!ctx.IsActivatingMemoryCard(owner, i))
                        yield return Choice(ctx, owner, i, p.Memory[i], "memoria");
            if (die is 5 or 6)
                for (var i = 0; i < p.CorruptedMemory.Count; i++)
                    yield return Choice(ctx, owner, i, p.CorruptedMemory[i], "corruptas");
        }
    }

    private static DecisionChoice Choice(GameEngine.EffectContext ctx, int owner, int index, string id, string zone) => new()
    {
        CardId = id, Value = index, Owner = owner, Label = $"{CardCatalog.Get(id).Name} · {zone} de {ctx.PlayerName(owner)}",
        Button = "Añadir a mano", Public = $"añade una carta de {zone} de {ctx.PlayerName(owner)} con Perro Fantasma."
    };
}

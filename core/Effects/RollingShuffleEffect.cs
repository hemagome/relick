namespace Relick.Core;

internal sealed class RollingShuffleEffect : CardRules, ISkillAction, IAttackDamageRoll, IAttackRewardRoll
{
    private const string Action = "action", Damage = "damage", Reward = "reward", ChooseTop = "choose-top", TakeSecond = "take-second";
    private const int ViewedCards = 6, TakenCards = 2, MemoryPosition = 5;

    public override string CardId => CardCatalog.RollingShuffle.Id;

    public void Activate(GameEngine.EffectContext ctx) => ctx.Roll(DiePurpose.RollingShuffleAction, Action);

    public bool TryRollDamage(GameEngine.EffectContext ctx, PendingAttackRoll attack)
    {
        if (attack.Die != 6) return false;
        ctx.RollAttackDamage(DiePurpose.RollingShuffleDamage, Damage, attack);
        return true;
    }

    public void RollReward(GameEngine.EffectContext ctx) => ctx.Roll(DiePurpose.RollingShuffleReward, Reward);

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        switch (step)
        {
            case Damage: ctx.ResolveAttackWithDamage(next.Attack!, next.Value + 1); break;
            case Reward: ctx.SetAttackReward((next.Value + 1) * 100); break;
            case TakeSecond: ctx.MoveDeckCardToHand(next.Value); break;
            case Action: ResolveAction(ctx, next.Value); break;
        }
    }

    private static void ResolveAction(GameEngine.EffectContext ctx, int die)
    {
        var rival = 1 - ctx.Actor;
        switch (die)
        {
            case 1: ctx.DealDamage(ctx.Actor, 1); return;
            case 2: ctx.Draw(2); return;
            case 3: ctx.Heal(rival, 3); break;
            case 4:
                ctx.Player.Relikia += 400;
                ctx.Announce($"{ctx.Player.Name} gana 400 PR con {ctx.Card.Name}.");
                break;
            case 5:
                if (ctx.State.Players[rival].Memory.Count >= MemoryPosition)
                    ctx.TakeFromMemory(rival, rival, ctx.State.Players[rival].Memory.Count - MemoryPosition);
                break;
            case 6 when ctx.Player.Deck.Count > 0: AskForTopCard(ctx, []); return;
        }
        ctx.CompletePendingPlay();
    }

    private static void AskForTopCard(GameEngine.EffectContext ctx, List<int> picks) =>
        ctx.Ask(ChooseTop, DecisionKind.ChooseCard,
            $"Elige la carta {picks.Count + 1} de {Math.Min(TakenCards, ctx.Player.Deck.Count)} entre las primeras {Math.Min(ViewedCards, ctx.Player.Deck.Count)} de tu mazo.",
            picks: picks);

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        Enumerable.Range(0, Math.Min(ViewedCards, ctx.Player.Deck.Count)).Where(index => !decision.Picks!.Contains(index)).Select(index => new DecisionChoice
        {
            Label = $"Añadir {CardCatalog.Get(ctx.Player.Deck[index]).Name} · mazo {index + 1}",
            Button = "Elegir carta", CardId = ctx.Player.Deck[index], Value = index, Public = "elige una carta del tope de su mazo."
        }).ToArray();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (ctx.Player.Deck.Count == 1)
        {
            ctx.MoveDeckCardToHand(choice.Value);
            return;
        }
        if (decision.Picks!.Count == 0)
        {
            AskForTopCard(ctx, [choice.Value]);
            return;
        }
        var first = decision.Picks[0];
        var second = ctx.Then(TakeSecond);
        second.Value = choice.Value > first ? choice.Value - 1 : choice.Value;
        ctx.MoveDeckCardToHand(first, second);
    }
}

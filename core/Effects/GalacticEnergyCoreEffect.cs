namespace Relick.Core;

internal sealed class GalacticEnergyCoreEffect : CardEffect
{
    private const int AttacksRequired = 3;
    private const string Pick = "pick", AfterDraw = "after-draw";

    public override string CardId => CardCatalog.GalacticEnergyCore.Id;
    public override bool HoldsActivationCard => true;

    private static IEnumerable<int> AttackSlots(PlayerState player) => Enumerable.Range(0, player.Memory.Count)
        .Where(index => DeckRules.IsAttack(CardCatalog.Get(player.Memory[index])));

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        AttackSlots(ctx.Player).Take(AttacksRequired).Count() < AttacksRequired
            ? "Necesitas tres cartas de Ataque en tu memoria." : null;

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        ctx.CloseErrorWindow();
        var attacks = AttackSlots(ctx.Player).ToArray();
        if (attacks.Length < AttacksRequired)
        {
            ctx.Log($"{ctx.Card.Name} se resuelve sin suficientes Ataques en la memoria.");
            return;
        }
        if (attacks.Length == AttacksRequired) ResolvePicked(ctx, attacks);
        else AskForAttacks(ctx, []);
    }

    private static void AskForAttacks(GameEngine.EffectContext ctx, List<int> picked) =>
        ctx.Ask(Pick, DecisionKind.ChooseCard, $"Elige el Ataque {picked.Count + 1} de {AttacksRequired} de tu memoria.", picks: picked);

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        AttackSlots(ctx.Player).Where(index => decision.Picks?.Contains(index) != true).Select(index => new DecisionChoice
        {
            Label = $"Devolver {CardCatalog.Get(ctx.Player.Memory[index]).Name} · memoria {index + 1}",
            Button = "Elegir Ataque", CardId = ctx.Player.Memory[index], Owner = ctx.Actor, Value = index,
            Public = "elige un Ataque de su memoria para el Núcleo."
        }).ToArray();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        List<int> picked = [.. decision.Picks ?? [], choice.Value];
        if (picked.Count == AttacksRequired) ResolvePicked(ctx, picked);
        else AskForAttacks(ctx, picked);
    }

    private static void ResolvePicked(GameEngine.EffectContext ctx, IReadOnlyList<int> picked)
    {
        ctx.ReturnMemoryCardsToDeck(picked);
        ctx.Announce($"{ctx.Player.Name} devuelve 3 Ataques de su memoria al mazo, baraja y roba 2 cartas con {ctx.Card.Name}.");
        ctx.Draw(2, ctx.Then(AfterDraw));
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => ctx.CorruptActivationCard();
}

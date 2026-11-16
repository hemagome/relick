namespace Relick.Core;

/// <summary>Vida Extra: roll 1D6 and gain 1, 2, 3 or 4 lives, without passing your Relikario's maximum.</summary>
internal sealed class ExtraLifeEffect : ICardEffect
{
    private const string Heal = "heal";

    public string CardId => CardCatalog.ExtraLife.Id;
    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => null;
    public void Resolve(GameEngine.EffectContext ctx) => ctx.Roll(DiePurpose.ExtraLife, Heal);
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => [];
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) { }

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        var die = next.Value;
        var amount = die switch { 1 => 1, 2 => 2, 6 => 4, _ => 3 };
        var healed = ctx.Heal(amount);
        ctx.State.LastCombat = $"{ctx.Player.Name} · Vida Extra · D6 = {die} · Recupera {healed} vida(s)";
        ctx.Log($"{ctx.Player.Name} resuelve Vida Extra: D6 = {die}, gana {amount} vida(s); recupera {healed} hasta su máximo de {ctx.Player.MaxHealth}.");
    }
}

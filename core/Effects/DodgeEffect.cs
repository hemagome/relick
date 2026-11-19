namespace Relick.Core;

/// <summary>Esquivar (COMBO): if your opponent is attacking you, roll 1D6. 1–3: draw 1 card; 4–6: negate the attack.</summary>
internal sealed class DodgeEffect : ICardEffect
{
    private const string Result = "result";

    public string CardId => CardCatalog.Dodge.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.State.Phase == Phase.DefenseChoice && ctx.State.PendingAttack?.Attacker == 1 - ctx.Actor ? null
            : "Solo puedes esquivar un ataque de tu oponente pendiente de defensa.";

    public void Resolve(GameEngine.EffectContext ctx) => ctx.Roll(DiePurpose.Dodge, Result);
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => [];
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) { }

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        var die = next.Value;
        ctx.EndActivation();
        if (die <= 3)
        {
            ctx.Log($"{ctx.Player.Name} resuelve Esquivar: dado {die}, roba una carta; el ataque sigue pendiente.");
            ctx.Draw(1);
            return;
        }
        if (die > 6) return;
        ctx.NegatePendingAttack();
        ctx.Announce($"{ctx.Player.Name} niega el ataque con Esquivar (dado {die}).");
    }
}

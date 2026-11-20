namespace Relick.Core;

/// <summary>Triple Ataque Cruzado: search your deck for 3 attacks and add them to your hand.</summary>
internal sealed class TripleAttackEffect : CardEffect
{
    private const int Attacks = 3;

    public override string CardId => CardCatalog.TripleAttack.Id;

    private static bool IsAttack(string id) => CardCatalog.Get(id).Kind == CardKind.Attack;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.Player.Deck.Any(IsAttack) ? null : "No hay ataques en tu mazo.";

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        // Freeze the count so recycling cannot add further targets to this effect.
        var count = Math.Min(Attacks, ctx.Player.Deck.Count(IsAttack));
        ctx.CloseErrorWindow();
        ctx.Log($"{ctx.Player.Name} añade {count} ataque(s) de su mazo a su mano con {ctx.Card.Name}.");
        // All current Attack cards are identical, so the first copies in deck order are taken; no choice is needed.
        for (var i = 0; i < count; i++) ctx.MoveDeckCardToHand(ctx.Player.Deck.FindIndex(IsAttack));
    }
}

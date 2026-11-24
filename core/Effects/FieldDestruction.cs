namespace Relick.Core;

/// <summary>Shared by every code that chooses a card to destroy: the choice, its public line and the closing log.</summary>
internal static class FieldDestruction
{
    public const string Target = "target";
    public const string Destroyed = "destroyed";

    private static string Name(string id) => CardCatalog.Get(id).Name;

    /// <summary>The zone as the public history names it, without revealing a hand card.</summary>
    private static string PublicZone(FieldZone zone) => zone switch
    {
        FieldZone.Hand => "mano al azar", FieldZone.Bonus => "Bonus", FieldZone.Area => "Área",
        FieldZone.Equipment => "equipo", FieldZone.LightEquipment => "equipo ligero", _ => "campo"
    };

    public static DecisionChoice Card(GameEngine.EffectContext ctx, FieldTarget t) => new()
    {
        Label = $"Destruir {Name(t.CardId)} de {ctx.PlayerName(t.Owner)} · {GameEngine.FieldCardPlace(ctx.State, t)}",
        Button = "Destruir", Field = t, Public = $"elige destruir una carta de {ctx.PlayerName(t.Owner)} ({PublicZone(t.Zone)})."
    };

    public static DecisionChoice Zone(GameEngine.EffectContext ctx, FieldTarget t) => new()
    {
        Label = GameEngine.FieldPlace(t, ctx.Actor),
        Button = "Destruir", Field = t, Public = $"elige destruir una carta de {ctx.PlayerName(t.Owner)} ({PublicZone(t.Zone)})."
    };

    /// <summary>Destroys the chosen target; a hand is a zone, so its card is drawn at random only now.</summary>
    public static void Destroy(GameEngine.EffectContext ctx, FieldTarget target)
    {
        var chosen = target;
        if (target.Zone == FieldZone.Hand)
        {
            var hand = ctx.State.Players[target.Owner].Hand;
            var index = ctx.Random(hand.Count);
            chosen = target with { Index = index, CardId = hand[index] };
        }
        ctx.Destroy(chosen, ctx.Then(Destroyed, chosen));
    }

    /// <summary>Logs the outcome and refills a destroyed Bonus before <paramref name="then"/>.</summary>
    public static void Finish(GameEngine.EffectContext ctx, EngineContinuation next, EngineContinuation? then)
    {
        var target = next.Target!;
        var card = target.Zone == FieldZone.Hand ? "una carta de la mano" : Name(target.CardId);
        ctx.Log(next.Prevented ? $"{ctx.Card.Name}: la carta original queda protegida por una sustitución." :
            $"{ctx.Player.Name} destruye {card} de {ctx.PlayerName(target.Owner)} con {ctx.Card.Name}.");
        if (!next.Prevented && target.Zone == FieldZone.Bonus) ctx.RefillBonus(target.Owner, then);
        else if (then != null) ctx.Continue(then);
    }
}

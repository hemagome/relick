namespace Relick.Core;

internal sealed class VictoriousManualControlEffect : ICardEffect
{
    private const string Source = "source", Destination = "destination";

    public string CardId => CardCatalog.VictoriousManualControl.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand)
    {
        if (ctx.Player.Health < 1) return "Necesitas 1 punto de vida para mover una carta.";
        return ctx.MovableFieldCards().Any() ? null : "Necesitas una carta en tu campo y una zona compatible vacía.";
    }

    public void Resolve(GameEngine.EffectContext ctx)
    {
        ctx.CloseErrorWindow();
        if (Unavailable(ctx, -1) != null) { ctx.Log($"{ctx.Card.Name} se resuelve sin un movimiento válido."); return; }
        ctx.Ask(Source, DecisionKind.ChooseTarget, "Elige una carta de tu campo para moverla. El traslado cuesta 1 vida.");
    }

    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        if (decision.Step == Source)
            return ctx.MovableFieldCards().Select(card => new DecisionChoice
            {
                Label = $"{CardCatalog.Get(card.CardId).Name} · {GameEngine.FieldCardPlace(ctx.State, card)}",
                Button = card.Zone == FieldZone.Equipment && ctx.Player.EquipmentStackedCopies > 0
                    ? $"Mover copia {card.Index + 1}" : "Mover esta carta",
                Field = card, Public = $"elige mover {CardCatalog.Get(card.CardId).Name}."
            }).ToArray();
        var source = decision.Continuation!.Target!;
        return ctx.CompatibleFieldDestinations(source).Select((destination, index) =>
        {
            var name = DestinationName(destination);
            return new DecisionChoice { Label = name, Value = index, Public = $"paga 1 vida y mueve {CardCatalog.Get(source.CardId).Name} a {name}." };
        }).ToArray();
    }

    internal static string DestinationName(FieldTarget destination) => destination.Zone switch
    {
        FieldZone.Skill => $"Habilidad {destination.Index + 1}",
        FieldZone.Exchange => $"Canje {destination.Index + 1}",
        FieldZone.Equipment => "Equipo",
        FieldZone.LightEquipment => "Equipos ligeros",
        FieldZone.Area => "Área",
        _ => "Bonus"
    };

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (decision.Step == Source) ctx.AskFieldDestination(choice.Field!);
        else
        {
            var source = decision.Continuation!.Target!;
            ctx.PayLifeAndMoveFieldCard(source, ctx.CompatibleFieldDestinations(source).ElementAt(choice.Value));
        }
    }

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}

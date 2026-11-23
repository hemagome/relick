namespace Relick.Core;

internal sealed class TurboAttackEffect : CardRules, IAttackDamageRoll, IDeckSearchTrigger
{
    private const string Damage = "damage", PlayFoundCard = "play-found-card";
    private const int Pass = int.MinValue;

    public override string CardId => CardCatalog.TurboAttack.Id;

    public bool TryRollDamage(GameEngine.EffectContext ctx, PendingAttackRoll attack)
    {
        if (!ctx.DealsNormalDamage(attack)) return false;
        ctx.RollAttackDamage(DiePurpose.TurboAttackDamage, Damage, attack);
        return true;
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step == Damage) ctx.ResolveAttackWithDamage(next.Attack!, 2 + next.Value % 2);
    }

    public bool TryOffer(GameEngine.EffectContext ctx, IReadOnlyList<int> handSlots, EngineContinuation? then)
    {
        if (ctx.Player.SearchActivationUsed) return false;
        var eligible = handSlots.Where(ctx.CanActivateSearchedHandCard).ToList();
        if (eligible.Count == 0) return false;
        ctx.Ask(PlayFoundCard, DecisionKind.ChooseCard, "Puedes activar o equipar una de las cartas que encontraste sin gastar una acción.",
            picks: eligible, continuation: then);
        return true;
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var choices = new List<DecisionChoice>();
        foreach (var index in decision.Picks!)
        {
            var card = CardCatalog.Get(ctx.Player.Hand[index]);
            var equip = card.Kind == CardKind.Equipment;
            var action = equip ? "Equipar" : "Activar";
            choices.Add(new DecisionChoice { Label = $"{action} {card.Name} · carta {index + 1}", Button = $"{action} sin acción",
                CardId = card.Id, Value = index, Public = $"elige {action.ToLowerInvariant()} {card.Name} con Turbo Attack." });
            if (card.Id == CardCatalog.MetalHood.Id)
                choices.Add(new DecisionChoice { Label = $"Equipar {card.Name} al rival · carta {index + 1}", Button = "Equipar al rival",
                    CardId = card.Id, Value = ~index, Public = $"elige equipar {card.Name} al rival con Turbo Attack." });
        }
        choices.Add(new DecisionChoice { Label = "Conservar las cartas en mano", Value = Pass, Public = "conserva en mano la búsqueda." });
        return choices;
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value == Pass) { ctx.Continue(decision.Continuation); return; }
        ctx.Player.SearchActivationUsed = true;
        var onOpponent = choice.Value < 0;
        ctx.ActivateSearchedHandCard(onOpponent ? ~choice.Value : choice.Value, decision.Continuation, onOpponent);
    }
}

namespace Relick.Core;

internal sealed class LetterEffect(CardDefinition definition) : CardEffect
{
    public override string CardId => definition.Id;
    public override bool HoldsActivationCard => true;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => definition.Letter switch
    {
        'R' when ctx.Actor != ctx.State.CurrentPlayer => "El efecto de El Nombre del Mono solo se usa en tu turno.",
        'E' when !ctx.EnergyCodes(excludedHand).Any() => "Necesitas otro Código en tu mano.",
        _ => null
    };

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        switch (definition.Letter)
        {
            case 'R':
                if (ctx.Actor == ctx.State.CurrentPlayer && ctx.State.Players[1 - ctx.Actor].Hand.Count > 0)
                    ctx.Ask("rival-hand", DecisionKind.ChooseCard, "Mira la mano rival y elige una carta que no sea habilidad.",
                        showsHandOf: 1 - ctx.Actor);
                break;
            case 'E':
                if (ctx.EnergyCodes().Any())
                    ctx.Ask("energy", DecisionKind.ChooseCard, "Descarta un Código para activarlo.");
                break;
            case 'I':
                ctx.Player.ActionsLeft++;
                ctx.Log($"{ctx.Player.Name} gana una acción extra.");
                break;
            case 'L': ctx.Roll(DiePurpose.LetterL, "corrupt"); break;
            case 'C': ctx.Roll(DiePurpose.LetterC, "chaos"); break;
        }
    }

    internal static DecisionChoice CardChoice(string id, int index, string button, int? owner = null, string? label = null) =>
        new() { CardId = id, Value = index, Button = button, Owner = owner, Label = label ?? CardCatalog.Get(id).Name,
            RequiresAcknowledgement = true };

    internal static DecisionChoice FieldChoice(GameEngine.EffectContext ctx, FieldTarget target, string button) =>
        new() { CardId = target.Zone == FieldZone.Relikario ? null : target.CardId,
            Field = target, Label = ctx.FieldName(target), Button = button };

    private static DecisionChoice Pass(string label = "Terminar") => new() { Label = label, Button = label, Value = -1 };

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var rival = 1 - ctx.Actor;
        return decision.Step switch
        {
            "rival-hand" => ctx.State.Players[rival].Hand.Select((id, i) => (id, i))
                .Where(card => CardCatalog.Get(card.id).Kind != CardKind.Skill)
                .Select(card => CardChoice(card.id, card.i, "Elegir", rival)).DefaultIfEmpty(Pass("Cerrar mano")).ToArray(),
            "energy" => ctx.EnergyCodes().Select(card => CardChoice(card.Id, card.Index, "Descartar y activar")).ToArray(),
            "corrupt" => CorruptionChoices(ctx, decision.Value).ToArray(),
            "chaos-mode" => ChaosModes(ctx).Where(choice => decision.Value != 1 || choice.Value is 1 or 7).ToArray(),
            "inspect" => [CardChoice(ctx.Player.Deck[0], 1, "Enviar al fondo"), Pass("Dejar arriba")],
            "straighten" => ctx.StraightenableCards().Select(target => FieldChoice(ctx, target, "Enderezar")).ToArray(),
            "return" => MemoryChoices(ctx, "Enviar al fondo").ToArray(),
            "discard" => ctx.DiscardableHandCards().Select(card => CardChoice(card.Id, card.Index, "Descartar")).ToArray(),
            "clone" => ctx.CloneableMemory().Select(card => CardChoice(card, 0, "Clonar")).ToArray(),
            "host" => ctx.FieldCards().Select(target => FieldChoice(ctx, target, "Elegir esta carta")).Append(Pass()).ToArray(),
            "placement" => [new() { Value = 1, Label = "Encima" }, new() { Label = "Debajo" }],
            "overlay" => MemoryChoices(ctx, "Colocar").Append(Pass()).ToArray(),
            _ => []
        };
    }

    private static IEnumerable<DecisionChoice> MemoryChoices(GameEngine.EffectContext ctx, string button) =>
        ctx.ResettableMemoryCards().Select(entry =>
            CardChoice(ctx.State.Players[entry.Owner].Memory[entry.Index], entry.Index, button, entry.Owner));

    private static IEnumerable<DecisionChoice> CorruptionChoices(GameEngine.EffectContext ctx, int location)
    {
        if (location is 1 or 3 or 6)
            foreach (var field in ctx.NonSkillFieldCards().Where(target => location != 1 || target.Owner == ctx.Actor))
                yield return FieldChoice(ctx, field, "Corromper");
        if (location is 2 or 3 or 7)
            foreach (var choice in MemoryChoices(ctx, "Corromper").Where(choice => CardCatalog.Get(choice.CardId!).Kind != CardKind.Skill))
                yield return choice;
        if (location == 8)
            foreach (var owner in new[] { ctx.Actor, 1 - ctx.Actor })
                foreach (var (id, i) in ctx.State.Players[owner].Hand.Select((id, i) => (id, i)))
                    if (CardCatalog.Get(id).Kind != CardKind.Skill)
                        yield return CardChoice(id, i, "Corromper", owner);
    }

    private static IEnumerable<DecisionChoice> ChaosModes(GameEngine.EffectContext ctx)
    {
        yield return new() { Value = 1, Label = "Investigar la carta del tope" };
        if (ctx.StraightenableCards().Any()) yield return new() { Value = 7, Label = "Enderezar una carta" };
        if (MemoryChoices(ctx, "").Any())
        {
            yield return new() { Value = 2, Label = "Devolver tres cartas al fondo del mazo" };
            yield return new() { Value = 5, Label = "Colocar hasta dos cartas de memoria en el campo" };
        }
        if (ctx.DiscardableHandCards().Any()) yield return new() { Value = 3, Label = "Descartar una carta y robar tres" };
        if (ctx.CloneableMemory().Any()) yield return new() { Value = 4, Label = "Clonar una carta de mi memoria" };
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step == "corrupt") AskCorruption(ctx, next.Value is 4 or 5 ? 3 : next.Value);
        else if (step == "after-field")
        {
            if (!next.Prevented && next.Target is { Zone: FieldZone.Bonus } bonus)
            {
                var after = ctx.Then("after-bonus");
                after.Value = next.Value;
                ctx.RefillBonus(bonus.Owner, after);
            }
            else AskCorruption(ctx, next.Value);
        }
        else if (step == "after-bonus") AskCorruption(ctx, next.Value);
        else if (step == "chaos")
        {
            if (next.Value == 6 || ctx.Player.Health == 0)
                ctx.Ask("chaos-mode", DecisionKind.Confirm, "Elige un efecto de Código Caótico.");
            else if (next.Value == 1)
                ctx.Ask("chaos-mode", DecisionKind.Confirm, "Investiga una carta o endereza una carta en el campo.", value: 1);
            else ApplyChaos(ctx, next.Value);
        }
    }

    private static void AskCorruption(GameEngine.EffectContext ctx, int location)
    {
        if (location < 1 || location > 8) return;
        if (!CorruptionChoices(ctx, location).Any())
        {
            if (location is 6 or 7) AskCorruption(ctx, location + 1);
            return;
        }
        ctx.Ask("corrupt", DecisionKind.ChooseCard, "Elige una carta que no sea habilidad para corromper.",
            value: location, showsHandOf: location == 8 ? 1 - ctx.Actor : null);
    }

    private static void ApplyChaos(GameEngine.EffectContext ctx, int effect)
    {
        switch (effect)
        {
            case 1: ctx.InspectDeckTop(); break;
            case 2 when MemoryChoices(ctx, "").Any():
                ctx.Ask("return", DecisionKind.ChooseCard, "Elige cartas de memoria para devolver al fondo de sus mazos.", value: 3);
                break;
            case 3 when ctx.DiscardableHandCards().Any():
                ctx.Ask("discard", DecisionKind.ChooseCard, "Descarta una carta para robar tres.");
                break;
            case 4 when ctx.CloneableMemory().Any():
                ctx.Ask("clone", DecisionKind.ChooseCard, "Elige una carta de tu memoria para clonar.");
                break;
            case 5 when MemoryChoices(ctx, "").Any():
                ctx.Ask("host", DecisionKind.ChooseCard, "Elige la carta del campo sobre la que colocarás las cartas de memoria.");
                break;
            case 7: ctx.Ask("straighten", DecisionKind.ChooseTarget, "Elige la carta que quieres enderezar."); break;
        }
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value < 0) return;
        switch (decision.Step)
        {
            case "rival-hand": ctx.ResolveMonkeyName(choice.Value); break;
            case "energy": ctx.ActivateEnergyCode(choice.Value); break;
            case "corrupt":
                if (choice.Field is { } target)
                {
                    var next = ctx.Then("after-field", target);
                    next.Value = decision.Value == 6 ? 7 : 0;
                    ctx.CorruptField(target, next);
                }
                else if (decision.Value == 8) ctx.CorruptHand(choice.Owner!.Value, choice.Value);
                else
                {
                    ctx.CorruptMemoryCard(choice.Owner!.Value, choice.Value);
                    if (decision.Value == 7) AskCorruption(ctx, 8);
                }
                break;
            case "chaos-mode": ApplyChaos(ctx, choice.Value); break;
            case "inspect": if (choice.Value == 1) ctx.BottomDeckTop(); break;
            case "straighten": ctx.StraightenCard(choice.Field!); break;
            case "return":
                ctx.BottomMemoryCard(choice.Owner!.Value, choice.Value);
                if (decision.Value > 1 && MemoryChoices(ctx, "").Any())
                    ctx.Ask("return", DecisionKind.ChooseCard, "Elige otra carta de memoria.", value: decision.Value - 1);
                break;
            case "discard": ctx.DiscardThenDraw(choice.Value, 3); break;
            case "clone": ctx.CloneMemoryCard(choice.CardId!); break;
            case "host":
                ctx.Ask("placement", DecisionKind.Confirm, "Elige dónde poner las cartas de memoria.",
                    continuation: ctx.Then("overlay", choice.Field));
                break;
            case "placement":
                ctx.Ask("overlay", DecisionKind.ChooseCard, "Elige hasta dos cartas de memoria.", value: 2,
                    continuation: new EngineContinuation { Target = decision.Continuation!.Target, Flag = choice.Value == 1 });
                break;
            case "overlay":
                var placement = decision.Continuation!;
                ctx.OverlayMemoryCard(choice.Owner!.Value, choice.Value, placement.Target!, placement.Flag);
                if (!ctx.State.IsOver && decision.Value > 1 && MemoryChoices(ctx, "").Any())
                    ctx.Ask("overlay", DecisionKind.ChooseCard, "Puedes colocar una segunda carta de memoria.",
                        value: decision.Value - 1, continuation: placement);
                break;
        }
    }
}

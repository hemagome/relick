namespace Relick.Core;

internal sealed class TakeEquipmentEffect : CardEffect
{
    private const string Target = "equipment";
    private const string Equip = "equip";
    private const string Attach = "attach";
    public override string CardId => CardCatalog.TakeEquipment.Id;

    private static int AttackIndex(PlayerState player) => player.Deck.FindIndex(id => DeckRules.IsAttack(CardCatalog.Get(id)));

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => !ctx.RivalEquipment().Any()
        ? "El adversario no tiene un equipo que puedas tomar."
        : AttackIndex(ctx.Player) < 0 ? "No hay una carta de Ataque en tu mazo." : null;

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (Unavailable(ctx, -1) is { } reason) { ctx.Log(reason); return; }
        ctx.Ask(Target, DecisionKind.ChooseTarget, "Selecciona el equipo rival que quieres equiparte", allowsCombos: true);
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        if (AttackIndex(ctx.Player) < 0) return [];
        return ctx.RivalEquipment().Select(target =>
        {
            var name = CardCatalog.Get(target.CardId).Name;
            return new DecisionChoice { Label = $"Equipar {name} del adversario · {GameEngine.FieldCardPlace(ctx.State, target)}",
                Button = "Tomar equipo", Field = target, CardId = target.CardId,
                Public = $"elige equiparse {name} de {ctx.PlayerName(target.Owner)}.", Primary = true };
        }).ToArray();
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.CloseErrorWindow();
        ctx.TakeEquipment(choice.Field!, ctx.Then(Equip, choice.Field));
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step == Equip)
        {
            if (next.Prevented || next.Target is not { } target || !ctx.RivalEquipment().Contains(target) ||
                !ctx.FinishEquipmentTransfer(target, ctx.Then(Attach)))
                ctx.Log($"{ctx.Card.Name} se resuelve sin transferir el equipo.");
            return;
        }
        if (step == Attach)
        {
            var index = AttackIndex(ctx.Player);
            if (index >= 0) ctx.AttachAttackFromDeck(index);
        }
    }
}

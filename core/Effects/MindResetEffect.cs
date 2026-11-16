namespace Relick.Core;

/// <summary>
/// Resetear la Mente (COMBO): look at a player's hand, choose 2 cards from any memory and 1 non-skill from that hand,
/// send them to their owners' decks, shuffle, and the target draws 1 card. Each pick is a step of the same decision;
/// while it waits only the decider sees the chosen hand, and other COMBO may interrupt it as before.
/// </summary>
internal sealed class MindResetEffect : ICardEffect
{
    public const string Target = "target", Memory = "memory", Hand = "hand", Finish = "finish";
    private const int Memories = 2;

    public string CardId => CardCatalog.MindReset.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => ctx.MindResetUnavailable(excludedHand);

    public void Resolve(GameEngine.EffectContext ctx)
    {
        if (ctx.State.Players.All(p => p.Hand.Count == 0) || ctx.ResettableMemoryCards().Count() < Memories)
        {
            ctx.Log($"{ctx.Card.Name} se resuelve sin objetivos suficientes después de las respuestas.");
            ctx.EndMindReset();
            return;
        }
        ctx.Ask(Target, DecisionKind.Confirm, "Elige un jugador para mirar su mano", allowsCombos: true);
    }

    // A pick is packed as memory index × 2 + owner.
    private static List<MemorySelection> Picked(PendingDecision decision) =>
        (decision.Picks ?? []).Select(v => new MemorySelection(v % 2, v / 2)).ToList();

    private static bool HasNonSkill(PlayerState p) => p.Hand.Any(id => CardCatalog.Get(id).Kind != CardKind.Skill);

    private static DecisionChoice End(string label, string why) =>
        new() { Label = label, Button = "Finalizar", Value = -1, Public = $"termina Resetear la Mente {why}." };

    // Computed from the board each time, like the former options: an interruption may have emptied a hand.
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var s = ctx.State;
        if (decision.Step == Target)
            return s.Players.All(p => p.Hand.Count == 0) ? [End("No quedan manos objetivo: finalizar", "sin manos objetivo")]
                : Enumerable.Range(0, s.Players.Length).Where(seat => s.Players[seat].Hand.Count > 0).Select(seat => new DecisionChoice
                {
                    Label = $"Mirar mano de {ctx.PlayerName(seat)}", Button = seat == ctx.Actor ? "Mi mano" : "Mano rival", Value = seat,
                    Primary = seat != ctx.Actor, Public = $"revisa la mano de {ctx.PlayerName(seat)}."
                }).ToList();
        var target = decision.ShowsHandOf!.Value; var hand = s.Players[target].Hand; var picked = Picked(decision);
        if (!HasNonSkill(s.Players[target])) return [End("No hay no-habilidades: finalizar sin devolver cartas", "sin devolver cartas")];
        if (picked.Count < Memories)
            return ctx.ResettableMemoryCards().Where(m => !picked.Contains(m)).Select(m =>
            {
                var id = s.Players[m.Owner].Memory[m.Index];
                return new DecisionChoice
                {
                    Label = $"Memoria {picked.Count + 1}/{Memories}: {CardCatalog.Get(id).Name} · {ctx.PlayerName(m.Owner)} · carta {m.Index + 1}",
                    Button = "Elegir", CardId = id, Owner = m.Owner, Value = m.Index, Public = $"elige una carta de memoria para {ctx.Card.Name}."
                };
            }).ToList();
        // The hand is hidden from the rival: only the decider receives these choices, and the public line names no card.
        return Enumerable.Range(0, hand.Count).Where(i => CardCatalog.Get(hand[i]).Kind != CardKind.Skill).Select(i => new DecisionChoice
        {
            Label = $"Devolver {CardCatalog.Get(hand[i]).Name} · mano de {ctx.PlayerName(target)} · carta {i + 1}", Button = "Devolver al mazo",
            CardId = hand[i], Value = i, Public = $"devuelve una carta al mazo por {ctx.Card.Name}."
        }).ToList();
    }

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value < 0) { ctx.EndMindReset(); return; }
        var s = ctx.State;
        if (decision.Step == Target)
        {
            var target = choice.Value;
            // Only the activating player may see the chosen hand; it is not revealed publicly.
            s.LastCombat = $"{ctx.Player.Name} mira la mano de {ctx.PlayerName(target)} con {ctx.Card.Name}.";
            AskNext(ctx, target, []);
            return;
        }
        var seat = decision.ShowsHandOf!.Value; var picks = decision.Picks ?? [];
        if (picks.Count < Memories) { AskNext(ctx, seat, [.. picks, choice.Value * 2 + choice.Owner!.Value]); return; }
        ctx.ReturnMindResetCards(seat, Picked(decision), choice.Value, decision.ResumePhase);
    }

    private static void AskNext(GameEngine.EffectContext ctx, int target, List<int> picks)
    {
        if (!HasNonSkill(ctx.State.Players[target]))
            ctx.Ask(Finish, DecisionKind.Confirm, $"No hay no-habilidades en la mano de {ctx.PlayerName(target)}", allowsCombos: true, announce: false, picks: picks, showsHandOf: target);
        else if (picks.Count < Memories)
            ctx.Ask(Memory, DecisionKind.ChooseCard, $"Elige una carta de cualquier memoria ({picks.Count + 1}/{Memories})", allowsCombos: true, announce: false, picks: picks, showsHandOf: target);
        else
            ctx.Ask(Hand, DecisionKind.ChooseCard, $"Elige una no-habilidad de la mano de {ctx.PlayerName(target)}", allowsCombos: true, announce: false, picks: picks, showsHandOf: target);
    }

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}

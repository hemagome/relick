namespace Relick.Core;

/// <summary>
/// Zahira Flair (trap placed on a card in play): in the final stage of its owner's rival, the owner rolls 1D6 for each
/// copy. 1–2: destroy Zahira Flair; 3–4: move it onto another card; 5–6: 2 damage to the Relikario under it, or destroy
/// the other card under it. The roll's outcome is still resolved in the engine (GameEngine.Zahira.cs).
/// </summary>
internal sealed class ZahiraFlairEffect : CardRules, ITurnTrigger
{
    private const string Place = "place";

    public override string CardId => CardCatalog.ZahiraFlair.Id;

    /// <summary>
    /// Where a Zahira Flair goes, when it is created from a trap recipe or moved by its 3–4. The engine opens it; duels
    /// saved in the former placement phase become this decision.
    /// </summary>
    internal static PendingDecision PlacementDecision(int actor, bool creating, int slot, Phase resumePhase = default) => new()
    {
        // Confirm lists every host as a button, as the former phase did; each still lights its card on the board.
        Source = CardCatalog.ZahiraFlair.Id, Step = Place, Decider = actor, Kind = DecisionKind.Confirm,
        Instruction = "Elige una carta del campo para colocar a Zahira Flair encima", ResumePhase = resumePhase,
        Continuation = new EngineContinuation { Actor = actor, Flag = creating, Count = slot }
    };

    /// <summary>The cards this player's Zahira Flair in <paramref name="slot"/> may cover: not its current host, nor a rival's copy that already sits on another copy.</summary>
    internal static IEnumerable<FieldTarget> Hosts(GameState state, IEnumerable<FieldTarget> field, int actor, int slot)
    {
        var current = state.Players[actor].Zahira(slot);
        return field.Where(target =>
            !(current is { } host && host.Owner == target.Owner && host.Zone == target.Zone && host.Index == target.Index) &&
            (target.Zone != FieldZone.Zahira || target.Owner != actor &&
                state.Players[target.Owner].Zahira(GameEngine.ZahiraSlot(target.Index)) is not { Zone: FieldZone.Zahira }));
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        Hosts(ctx.State, ctx.FieldCards(), ctx.Actor, decision.Continuation!.Count).Select(host => new DecisionChoice
        {
            Label = $"{ctx.FieldName(host)} · {ctx.PlayerName(host.Owner)}{Position(ctx.State, host)}", Button = "Colocar",
            Field = host, Public = "elige dónde colocar Zahira Flair."
        }).ToList();

    private static string Position(GameState state, FieldTarget host) => host.Zone switch
    {
        FieldZone.Equipment when state.Players[host.Owner].EquipmentStackedCopies > 0 => $" · equipo {host.Index + 1}",
        FieldZone.EquipmentCover => $" · cobertura {host.Index + 1}",
        FieldZone.LightEquipment => $" · equipo ligero {host.Index + 1}",
        FieldZone.Skill => $" · habilidad {state.Players[host.Owner].SkillPosition(host.Index) + 1}",
        FieldZone.Exchange => $" · canje {host.Index + 1}",
        FieldZone.Attached => $" · anexo {host.Index + 1}",
        FieldZone.Bonus => " · Bonus",
        FieldZone.Area => " · Área",
        _ => ""
    };

    /// <summary>Places or moves the copy onto the chosen card; Súper Imán may replace the move on a rival card.</summary>
    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        var pending = decision.Continuation!;
        var placement = new EngineContinuation { Kind = ContinuationKind.PlaceZahira, Actor = ctx.Actor, Target = choice.Field, Flag = pending.Flag, Count = pending.Count };
        if (!ctx.ReplaceOpponentFieldEffect(choice.Field!, placement)) ctx.Continue(placement);
    }

    public TurnMoment Moment => TurnMoment.FinalStage;
    public int Order => 0;

    /// <summary>Rolls for the next copy that has not rolled this final stage; each roll comes back through the pending play.</summary>
    public bool Fire(GameEngine.EffectContext ctx)
    {
        var state = ctx.State;
        var owner = 1 - state.CurrentPlayer;
        var p = state.Players[owner];
        // Snapshots taken before a second copy existed only recorded that the single roll was done.
        for (var slot = Math.Max(state.ZahiraFinalSlot, state.ZahiraRolledThisFinal ? 1 : 0); slot < PlayerState.ZahiraSlots; slot++)
        {
            if (p.Zahira(slot) == null || ctx.EffectsSuppressed(owner, FieldZone.Zahira, GameEngine.ZahiraIndex(slot))) continue;
            state.ZahiraRolledThisFinal = true;
            state.ZahiraFinalSlot = slot + 1;
            ctx.RollResolvedByEngine(owner, DiePurpose.Zahira, slot);
            return true;
        }
        return false;
    }
}

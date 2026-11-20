namespace Relick.Core;

/// <summary>
/// Runs the cards that act at a moment of the turn (<see cref="ITurnTrigger"/>), in the order the rules fix.
/// See docs/CARD_TRIGGERS.md.
/// </summary>
public sealed partial class GameEngine
{
    private void BeginActionStartEffects(int actor)
    {
        // While set, every return to the pending play re-enters the action-start triggers: a Perro Fantasma resolves
        // as a whole activation and comes back that way, not through a continuation.
        State.ActionStartActor = actor;
        RunTurnTriggers(TurnMoment.ActionStart, actor);
    }

    /// <summary>The start of the active player's turn: its triggers, then the draw.</summary>
    private void BeginTurnStartTriggers()
    {
        if (!RunTurnTriggers(TurnMoment.TurnStart, State.CurrentPlayer)) DrawAtTurnStart();
    }

    /// <summary>
    /// Fires the moment's triggers from <paramref name="from"/>; true when one started work that comes back later.
    /// Re-entry always starts from the first trigger, so a trigger that finished must return false when fired again.
    /// </summary>
    private bool RunTurnTriggers(TurnMoment moment, int seat, int from = 0)
    {
        var triggers = CardEffects.TurnTriggers(moment);
        for (var i = from; i < triggers.Count; i++)
        {
            if (((ITurnTrigger)triggers[i]).Fire(EffectFor(seat, triggers[i].CardId))) return true;
            // Only an activation-based trigger needs the re-entry mark; once one has nothing left, the rest run without it.
            if (moment == TurnMoment.ActionStart) State.ActionStartActor = null;
        }
        return false;
    }

    /// <summary>A trigger that came back later has finished: the moment's later triggers run, then the pending play.</summary>
    private void ContinueTurnTriggers(TurnMoment moment, int seat, string finishedCardId)
    {
        var triggers = CardEffects.TurnTriggers(moment);
        var index = triggers.Select(t => t.CardId).ToList().IndexOf(finishedCardId);
        if (RunTurnTriggers(moment, seat, index + 1)) return;
        if (moment == TurnMoment.TurnStart) DrawAtTurnStart();
        else CompletePendingPlay();
    }
}

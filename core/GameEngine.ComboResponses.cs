namespace Relick.Core;

public sealed partial class GameEngine
{
    public CommandResult SetComboResponses(int actor, bool enabled) => SetComboResponses(actor, enabled, out _);

    public CommandResult SetComboResponses(int actor, bool enabled, out bool resumed)
    {
        resumed = false;
        if (actor < 0 || actor >= State.Players.Length || State.IsOver || State.Phase == Phase.Preparation)
            return CommandResult.Fail("No puedes cambiar las respuestas COMBO ahora.");
        State.Players[actor].AutoPassCombos = !enabled;
        if (!enabled && State.DecisionPlayer == actor)
        {
            // Only skip waits whose remaining choices are optional COMBO cards.
            // Already-started effects, skill/equipment decisions and targets remain manual.
            switch (State.Phase)
            {
                case Phase.ErrorResponse: PassErrorResponse(actor); resumed = true; break;
                case Phase.EffectDamageChoice: ResolveEffectDamage(actor); resumed = true; break;
                case Phase.DefenseChoice when !CanDefend(actor, State.PendingAttack!.Critical):
                    PassDefense(actor); resumed = true; break;
                case Phase.AttackRerollChoice when !CanRespondToAttackDie():
                    ResolveAttackReroll(actor, false); resumed = true; break;
                case Phase.DieRerollChoice when !CanRespondToEffectDie():
                    ResolveDieReroll(actor, false); resumed = true; break;
            }
        }
        return CommandResult.Ok();
    }

    private bool ComboResponsesEnabled(int actor) => !State.Players[actor].AutoPassCombos;
}

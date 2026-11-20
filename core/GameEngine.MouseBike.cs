namespace Relick.Core;

public sealed partial class GameEngine
{
    private void BeginBikeTurnStart()
    {
        if (ActiveEquipment(State.CurrentPlayer)?.Id != CardCatalog.MouseBike.Id) { DrawAtTurnStart(); return; }
        State.Phase = Phase.Draw;
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.MouseBike.Id, Step = "turn-start", Kind = DecisionKind.Confirm,
            Decider = State.CurrentPlayer, Instruction = "Elige la bonificación de Moto-Ratón para este turno."
        });
    }
}

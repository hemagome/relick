namespace Relick.Core;

public sealed partial class GameEngine
{
    private static DecisionChoice[] BikeChoices() =>
    [
        new() { Label = "Robar una carta extra", Value = 1, Public = "elige robar una carta extra con Moto-Ratón." },
        new() { Label = "Ganar una acción extra", Public = "elige ganar una acción extra con Moto-Ratón." }
    ];

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

    private void ResolveBikeTurnStart(DecisionChoice choice)
    {
        if (choice.Value == 0) State.ActionsLeft++;
        DrawAtTurnStart(extraCards: choice.Value);
    }
}

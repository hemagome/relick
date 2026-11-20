namespace Relick.Core;

public sealed partial class GameEngine
{
    public bool CanRevealGhostDog(int actor, int index) => ValidateAction(actor) == null &&
        InHand(State.Players[actor], index) && PlayableHandCard(State.Players[actor], index) == CardCatalog.GhostDog.Id &&
        State.Players[actor].GhostDogReveals?.Contains(index) != true;

    public CommandResult RevealGhostDog(int actor, int index)
    {
        if (!CanRevealGhostDog(actor, index)) return CommandResult.Fail("Elige un Perro Fantasma disponible de tu mano.");
        var p = State.Players[actor];
        p.ActionsLeft--;
        p.RevealHandCard(index);
        (p.GhostDogReveals ??= []).Add(index);
        Log($"{p.Name} revela Perro Fantasma para activarlo en su siguiente etapa de acciones.");
        return CommandResult.Ok();
    }
}

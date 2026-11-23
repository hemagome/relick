namespace Relick.Core;

public sealed partial class GameEngine
{
    /// <summary>A card turned face up in its owner's hand by an action: the player and the hand slot.</summary>
    public event Action<int, int>? HandCardRevealed;

    public bool CanRevealGhostDog(int actor, int index) => ValidateAction(actor) == null &&
        InHand(State.Players[actor], index) && PlayableHandCard(State.Players[actor], index) == CardCatalog.GhostDog.Id &&
        State.Players[actor].GhostDogReveals?.Contains(index) != true;

    public CommandResult RevealGhostDog(int actor, int index)
    {
        if (!CanRevealGhostDog(actor, index)) return CommandResult.Fail("Elige un Perro Fantasma disponible de tu mano.");
        var p = State.Players[actor];
        p.ActionsLeft--;
        if (p.RevealHandCard(index)) HandCardRevealed?.Invoke(actor, index);
        (p.GhostDogReveals ??= []).Add(index);
        Log($"{p.Name} revela Perro Fantasma para activarlo en su siguiente etapa de acciones.");
        return CommandResult.Ok();
    }
}

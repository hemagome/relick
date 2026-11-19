namespace Relick.Core;

public sealed partial class GameEngine
{
    private bool HasDestinyPickResponse(int actor) => ComboResponsesEnabled(actor) && _activeActivation == null &&
        AvailableActions(actor) >= CardCatalog.DestinyPick.ActionCost && State.Players[actor].Hand.Contains(CardCatalog.DestinyPick.Id);

    private bool CanPlayDestinyPick(int actor) => State.Phase is Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final ||
        _activeActivation == null && State.DecisionPlayer == actor &&
        State.Phase is Phase.AttackRerollChoice or Phase.DefenseChoice or Phase.MagnetChoice;

    internal IEnumerable<int> DestinyPickMemorySlots(int actor) =>
        ValidateAction(actor) == null
            ? Enumerable.Range(0, State.Players[actor].Memory.Count).Where(index => IsDestinyPickInMemory(actor, index))
            : [];

    private bool IsDestinyPickInMemory(int actor, int index)
    {
        var memory = State.Players[actor].Memory;
        return index >= 0 && index < memory.Count && memory[index] == CardCatalog.DestinyPick.Id;
    }

    public CommandResult UseDestinyPickFromMemory(int actor, int index)
    {
        var error = ValidateAction(actor);
        if (error != null) return CommandResult.Fail(error);
        if (!IsDestinyPickInMemory(actor, index)) return CommandResult.Fail("Elige una Púa del Destino de tu memoria.");
        var card = CardCatalog.DestinyPick;
        var p = State.Players[actor];
        p.ActionsLeft--;
        Log($"{p.Name} usa {card.Name} desde su memoria (1 acción).");
        _activeActivation = new EngineActivation(actor, card.Id, 1, 0, State.Phase)
            { Kind = ActivationKind.Code, FromMemory = true, MemoryIndex = index, CanInterrupt = false };
        CorruptActivationCode(actor, index);
        ResolveCode(p, card);
        return CommandResult.Ok();
    }
}

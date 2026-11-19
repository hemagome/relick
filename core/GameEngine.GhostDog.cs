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

    private void BeginActionStartEffects(int actor)
    {
        State.ActionStartActor = actor;
        ResolveGhostDogsAtActionStart(actor);
    }

    private void ResolveGhostDogsAtActionStart(int actor)
    {
        var p = State.Players[actor];
        if (p.GhostDogReveals is not { Count: > 0 })
        {
            State.ActionStartActor = null;
            ResolveRobotizeAtActionStart(actor);
            return;
        }
        var index = p.GhostDogReveals.Min();
        if (PlayableHandCard(p, index) != CardCatalog.GhostDog.Id)
        {
            p.GhostDogReveals.Remove(index);
            ResolveGhostDogsAtActionStart(actor);
            return;
        }
        var memory = SendHandToMemory(p, index);
        BeginActivation(new EngineActivation(actor, CardCatalog.GhostDog.Id, 0, 0, Phase.Actions)
        {
            MemoryIndex = memory?.Index ?? -1, MemoryOwner = memory?.Owner,
            ChooseDieResult = true
        });
    }

    private void ResolveGhostDog(int actor)
    {
        if (_activeActivation?.ChooseDieResult == true)
        {
            AskDecision(new PendingDecision
            {
                Source = CardCatalog.GhostDog.Id, Step = "result", Kind = DecisionKind.Confirm, Decider = actor,
                Instruction = "Escoge el resultado de Perro Fantasma."
            });
            return;
        }
        RollWithRelikario(State.Players[actor], DiePurpose.GhostDog,
            new DieEffect { Purpose = DiePurpose.GhostDog, Actor = actor, SkillId = CardCatalog.GhostDog.Id, Step = "rolled" });
    }

    private void ResolveGhostDogResult(int actor, int die)
    {
        if (GhostDogCards(actor, die).Any())
            AskDecision(new PendingDecision
            {
                Source = CardCatalog.GhostDog.Id, Step = "recover", Kind = DecisionKind.ChooseCard, Decider = actor,
                Instruction = "Elige la carta que quieres añadir a tu mano.", Value = die
            });
        else Log($"Perro Fantasma se resuelve sin cartas disponibles para el resultado {die}.");
    }

    private IReadOnlyList<DecisionChoice> GhostDogChoices(PendingDecision decision) => decision.Step == "result"
        ? [
            new() { Label = "1 · Sin efecto", Value = 1 },
            new() { Label = "2 · Tomar un Bonus", Value = 2 },
            new() { Label = "3–4 · Recuperar de la memoria", Value = 3 },
            new() { Label = "5–6 · Recuperar una carta corrompida", Value = 5 }
          ]
        : GhostDogCards(decision.Decider, decision.Value).ToArray();

    private IEnumerable<DecisionChoice> GhostDogCards(int actor, int die)
    {
        for (var owner = 0; owner < State.Players.Length; owner++)
        {
            var p = State.Players[owner];
            if (die == 2 && p.Bonus is { } bonus)
                yield return GhostDogChoice(owner, -1, bonus, "Bonus");
            if (die is 3 or 4)
                for (var i = 0; i < p.Memory.Count; i++)
                    if (_activeActivation is not { } frame || frame.MemorySeat != owner || frame.MemoryIndex != i ||
                        frame.HeldCard != null)
                        yield return GhostDogChoice(owner, i, p.Memory[i], "memoria");
            if (die is 5 or 6)
                for (var i = 0; i < p.CorruptedMemory.Count; i++)
                    yield return GhostDogChoice(owner, i, p.CorruptedMemory[i], "corruptas");
        }
    }

    private DecisionChoice GhostDogChoice(int owner, int index, string id, string zone) => new()
    {
        CardId = id, Value = index, Owner = owner, Label = $"{CardCatalog.Get(id).Name} · {zone} de {State.Players[owner].Name}",
        Button = "Añadir a mano", Public = $"añade una carta de {zone} de {State.Players[owner].Name} con Perro Fantasma."
    };

    private void ResolveGhostDogChoice(PendingDecision decision, DecisionChoice choice)
    {
        if (decision.Step == "result") { ResolveGhostDogResult(decision.Decider, choice.Value); return; }
        var actor = decision.Decider;
        var owner = choice.Owner!.Value;
        var player = State.Players[actor];
        if (decision.Value == 2)
        {
            TakeBonus(State.Players[owner], player);
            RefillBonus(State.Players[owner]);
        }
        else if (decision.Value <= 4) TakeFromMemory(actor, owner, choice.Value, CardCatalog.GhostDog.Name);
        else
        {
            var cards = State.Players[owner].CorruptedMemory;
            var index = player.Hand.Count;
            player.Hand.Add(cards[choice.Value]);
            cards.RemoveAt(choice.Value);
            if (owner != actor) MarkBorrowedHandCard(player, index);
            (player.LockedGhostCards ??= []).Add(index);
        }
    }
}

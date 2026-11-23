namespace Relick.Core;

/// <summary>
/// A pending Trap creation. <see cref="CardId"/> is the first choice; <see cref="Choices"/> lists every
/// recipe the two exchanged cards meet (rulebook § 5.3: if they meet several, the player chooses one), and
/// <see cref="Ingredients"/> the two public cards that met it. Older saves carry only the first two fields.
/// </summary>
public sealed record TrapCraft(int Actor, string CardId, string[]? Choices = null, string[]? Ingredients = null)
{
    public IReadOnlyList<string> Options => Choices is { Length: > 0 } choices ? choices : [CardId];
}

public sealed partial class GameEngine
{
    public const int TrainingSuperMagnetCopies = 1;
    public event Action<int, int>? SuperMagnetCharged;

    private static void LoadTraps(PlayerState player, IReadOnlyCollection<string> traps)
    {
        int Copies(CardDefinition card) => Math.Min(DeckRules.MaxTrapCopies, traps.Count(id => id == card.Id));
        player.SuperMagnetCopies = Copies(CardCatalog.SuperMagnet);
        player.ZahiraAvailable = Copies(CardCatalog.ZahiraFlair) > 0;
        player.ZahiraSpares = Math.Max(0, Copies(CardCatalog.ZahiraFlair) - 1);
        player.SuperAttackAvailable = Copies(CardCatalog.SuperAttack) > 0;
        player.SuperAttackSpares = Math.Max(0, Copies(CardCatalog.SuperAttack) - 1);
    }

    private void QueueTrapCraft(int actor)
    {
        var player = State.Players[actor];
        var exchanged = player.Exchange.Take(2).Select(CardCatalog.Get).ToArray();
        // Every current path into these exchange slots places the card for its owner.
        // At most one Trap per exchange, but every recipe it meets is offered (rulebook § 5.3).
        var choices = new List<string>();
        if (player.SuperMagnetCopies > 0 && exchanged.Any(card => card.Kind == CardKind.Equipment))
            choices.Add(CardCatalog.SuperMagnet.Id);
        if (player.ZahiraAvailable && FreeZahiraSlot(player) >= 0 && exchanged.All(card => card.CodeType == CodeType.Character))
            choices.Add(CardCatalog.ZahiraFlair.Id);
        if (player.SuperAttackAvailable && exchanged.All(card => card.Kind == CardKind.Attack))
            choices.Add(CardCatalog.SuperAttack.Id);
        if (choices.Count > 0)
            (State.TrapCraftQueue ??= []).Add(new(actor, choices[0], choices.ToArray(), exchanged.Select(card => card.Id).ToArray()));
    }

    private bool TryPresentTrapCraft()
    {
        if (State.TrapCraftQueue == null || !State.Phase.IsTurnStage() ||
            _activeActivation != null || State.PendingAttack != null || State.PendingSniper != null)
            return false;

        var queue = State.TrapCraftQueue;
        while (queue.Count > 0)
        {
            // A copy may have been used or a Zahira slot filled while the creation waited.
            var available = queue[0].Options.Where(id => HasTrapCopy(queue[0].Actor, id)).ToArray();
            if (available.Length > 0) { queue[0] = queue[0] with { CardId = available[0], Choices = available }; break; }
            queue.RemoveAt(0);
        }
        if (queue.Count == 0)
        {
            State.TrapCraftQueue = null;
            return false;
        }
        State.TrapCraftResumePhase = State.Phase;
        State.Phase = Phase.TrapCraftChoice;
        return true;
    }

    private IEnumerable<string> AvailableTrapCards(int actor)
    {
        foreach (var card in new[] { CardCatalog.SuperMagnet, CardCatalog.ZahiraFlair, CardCatalog.SuperAttack })
            if (HasTrapCopy(actor, card.Id)) yield return card.Id;
    }

    private bool HasTrapCopy(int actor, string cardId)
    {
        var player = State.Players[actor];
        return cardId switch
        {
            var id when id == CardCatalog.ZahiraFlair.Id => player.ZahiraAvailable && FreeZahiraSlot(player) >= 0,
            var id when id == CardCatalog.SuperAttack.Id => player.SuperAttackAvailable,
            var id when id == CardCatalog.SuperMagnet.Id => player.SuperMagnetCopies > 0,
            _ => false
        };
    }

    /// <param name="choice">Which of the offered recipes to create, in <see cref="TrapCraft.Options"/> order.</param>
    public CommandResult ResolveTrapCraft(int actor, bool create, int choice = 0)
    {
        if (State.Phase != Phase.TrapCraftChoice || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde decidir si creas un Código Trampa.");
        var pending = State.TrapCraftQueue![0];
        if (create && (choice < 0 || choice >= pending.Options.Count || !HasTrapCopy(actor, pending.Options[choice])))
            return CommandResult.Fail("Ese Código Trampa no está disponible para este canje.");

        var queue = State.TrapCraftQueue!;
        queue.RemoveAt(0);
        if (queue.Count == 0) State.TrapCraftQueue = null;
        State.Phase = State.TrapCraftResumePhase!.Value;
        State.TrapCraftResumePhase = null;
        if (create) PlaceTrap(actor, pending.Options[choice]);
        else CompletePendingPlay();
        return CommandResult.Ok();
    }

    private void PlaceTrap(int actor, string cardId)
    {
        var player = State.Players[actor];
        if (cardId == CardCatalog.SuperAttack.Id)
        {
            if (player.SuperAttackSpares > 0) player.SuperAttackSpares--; else player.SuperAttackAvailable = false;
            player.Hand.Add(cardId);
            Log($"{player.Name} crea Súper Ataque y lo añade a su mano.");
            CompletePendingPlay();
        }
        else if (cardId == CardCatalog.ZahiraFlair.Id)
        {
            if (player.ZahiraSpares > 0) player.ZahiraSpares--; else player.ZahiraAvailable = false;
            BeginZahiraPlacement(actor, creating: true, FreeZahiraSlot(player));
        }
        else if (cardId == CardCatalog.SuperMagnet.Id)
        {
            player.SuperMagnetCopies--;
            var placement = new EngineContinuation { Kind = ContinuationKind.PlaceSuperMagnet, Actor = actor };
            DestroyEquipment(player, placement);
        }
    }

    private void FinishSuperMagnetPlacement(int actor)
    {
        var player = State.Players[actor];
        if (player.Equipment == null)
        {
            player.Equipment = CardCatalog.SuperMagnet.Id;
            player.EquipmentCounters = 0;
            Log($"{player.Name} crea Súper Imán y lo coloca en su zona de equipos.");
        }
        else Log($"{player.Name} conserva su equipo protegido; Súper Imán queda fuera del juego.");
        CompletePendingPlay();
    }

    private bool TryReplaceOpponentFieldEffect(int actor, FieldTarget target, EngineContinuation? completed = null)
    {
        var player = State.Players[target.Owner];
        if (actor == target.Owner || target.Zone == FieldZone.Hand || ActiveEquipment(target.Owner)?.Id != CardCatalog.SuperMagnet.Id)
            return false;

        if (completed != null) completed.Prevented = true;
        var count = ++player.EquipmentCounters;
        SuperMagnetCharged?.Invoke(target.Owner, count);
        State.LastCombat = $"{player.Name} sustituye el efecto rival por un contador en Súper Imán ({count}/{CardCatalog.SuperMagnetCounterLimit}).";
        Log(State.LastCombat);
        if (count >= CardCatalog.SuperMagnetCounterLimit) DestroyEquipment(player, completed);
        else Continue(completed);
        return true;
    }
}

namespace Relick.Core;

public sealed record TrapCraft(int Actor, string CardId);

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
        if (player.SuperMagnetCopies > 0 && exchanged.Any(card => card.Kind == CardKind.Equipment))
            (State.TrapCraftQueue ??= []).Add(new(actor, CardCatalog.SuperMagnet.Id));
        else if (player.ZahiraAvailable && FreeZahiraSlot(player) >= 0 && exchanged.All(card => card.CodeType == CodeType.Character))
            (State.TrapCraftQueue ??= []).Add(new(actor, CardCatalog.ZahiraFlair.Id));
        else if (player.SuperAttackAvailable && exchanged.All(card => card.Kind == CardKind.Attack))
            (State.TrapCraftQueue ??= []).Add(new(actor, CardCatalog.SuperAttack.Id));
    }

    private bool TryPresentTrapCraft()
    {
        if (State.TrapCraftQueue == null || State.Phase is not (Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final) ||
            _activeActivation != null || State.PendingAttack != null || State.PendingSniper != null)
            return false;

        var queue = State.TrapCraftQueue;
        while (queue.Count > 0 && !HasTrapCopy(queue[0])) queue.RemoveAt(0);
        if (queue.Count == 0)
        {
            State.TrapCraftQueue = null;
            return false;
        }
        State.TrapCraftResumePhase = State.Phase;
        State.Phase = Phase.TrapCraftChoice;
        return true;
    }

    private bool HasTrapCopy(TrapCraft recipe)
    {
        var player = State.Players[recipe.Actor];
        return recipe.CardId switch
        {
            var id when id == CardCatalog.ZahiraFlair.Id => player.ZahiraAvailable && FreeZahiraSlot(player) >= 0,
            var id when id == CardCatalog.SuperAttack.Id => player.SuperAttackAvailable,
            var id when id == CardCatalog.SuperMagnet.Id => player.SuperMagnetCopies > 0,
            _ => false
        };
    }

    public CommandResult ResolveTrapCraft(int actor, bool create)
    {
        if (State.Phase != Phase.TrapCraftChoice || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde decidir si creas un Código Trampa.");

        var player = State.Players[actor];
        var queue = State.TrapCraftQueue!;
        var recipe = queue[0];
        queue.RemoveAt(0);
        if (queue.Count == 0) State.TrapCraftQueue = null;
        State.Phase = State.TrapCraftResumePhase!.Value;
        State.TrapCraftResumePhase = null;
        if (create && recipe.CardId == CardCatalog.SuperAttack.Id)
        {
            if (player.SuperAttackSpares > 0) player.SuperAttackSpares--; else player.SuperAttackAvailable = false;
            player.Hand.Add(recipe.CardId);
            Log($"{player.Name} crea Súper Ataque y lo añade a su mano.");
            CompletePendingPlay();
        }
        else if (create && recipe.CardId == CardCatalog.ZahiraFlair.Id)
        {
            if (player.ZahiraSpares > 0) player.ZahiraSpares--; else player.ZahiraAvailable = false;
            BeginZahiraPlacement(actor, creating: true, FreeZahiraSlot(player));
        }
        else if (create)
        {
            player.SuperMagnetCopies--;
            var placement = new EngineContinuation { Kind = ContinuationKind.PlaceSuperMagnet, Actor = actor };
            DestroyEquipment(player, placement);
        }
        else CompletePendingPlay();
        return CommandResult.Ok();
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

namespace Relick.Core;

public sealed class PendingGoldenTotem
{
    public int Actor { get; set; }
    public int[] HandIndices { get; set; } = [];
}

public sealed partial class GameEngine
{
    /// <summary>
    /// Tótem de Oro now asks how many Tótems to show through a decision (GoldenTotemEffect). This command, which names the
    /// exact copies, still answers it, and finishes duels saved while the former phase, GoldenTotemChoice, was pending.
    /// </summary>
    public CommandResult ResolveGoldenTotem(int actor, IEnumerable<int> handIndices)
    {
        var selected = handIndices.ToArray();
        if (State.Phase == Phase.Decision && State.PendingDecision is { } decision && decision.Source == CardCatalog.GoldenTotem.Id)
        {
            if (decision.Decider != actor) return CommandResult.Fail("No te corresponde colocar Tótem de Oro.");
            if (!ValidGoldenTotemSelection(State.Players[actor], selected))
                return CommandResult.Fail("Muestra hasta dos Tótems de Salvación distintos de tu mano; Tótem de Oro necesita un equipo debajo.");
            State.PendingDecision = decision.Suspended; State.Phase = decision.ResumePhase;
            DeployGoldenTotem(actor, selected);
            return CommandResult.Ok();
        }
        if (State.Phase != Phase.GoldenTotemChoice || State.PendingGoldenTotem?.Actor != actor ||
            _activeActivation is not { Kind: ActivationKind.Code } frame || frame.Actor != actor || frame.CardId != CardCatalog.GoldenTotem.Id)
            return CommandResult.Fail("No te corresponde colocar Tótem de Oro.");
        if (!ValidGoldenTotemSelection(State.Players[actor], selected))
            return CommandResult.Fail("Muestra hasta dos Tótems de Salvación distintos de tu mano; Tótem de Oro necesita un equipo debajo.");
        DeployGoldenTotem(actor, selected);
        return CommandResult.Ok();
    }

    private bool ValidGoldenTotemSelection(PlayerState p, int[] selected) =>
        selected.Length <= 2 && selected.Distinct().Count() == selected.Length &&
        selected.All(i => InHand(p, i) && p.Hand[i] == CardCatalog.Totem.Id) && (p.Equipment != null || selected.Length > 0);

    /// <summary>Shows the chosen Tótems, then places them and Tótem de Oro; destroying the previous equipment may pause it.</summary>
    private void DeployGoldenTotem(int actor, int[] selected)
    {
        var p = State.Players[actor]; var frame = _activeActivation!;
        State.PendingGoldenTotem = new() { Actor = actor, HandIndices = selected.OrderDescending().ToArray() };
        foreach (var index in selected) p.RevealHandCard(index);
        frame.CanInterrupt = false; State.Phase = Phase.GoldenTotemResolving;
        Log($"{p.Name} muestra {selected.Length} Tótem(s) de Salvación para colocarlos con Tótem de Oro.");
        ContinueGoldenTotemPlacement(new EngineContinuation { Kind = ContinuationKind.GoldenTotemPlacement, Actor = actor });
    }

    private void ContinueGoldenTotemPlacement(EngineContinuation next)
    {
        var pending = State.PendingGoldenTotem!; var p = State.Players[pending.Actor];
        if (pending.HandIndices.Length > 0 && p.Equipment != null && p.Equipment != CardCatalog.Totem.Id && !next.Prevented)
        {
            DestroyEquipment(p, next); return;
        }
        foreach (var index in pending.HandIndices)
        {
            if (p.Equipment != null && p.Equipment != CardCatalog.Totem.Id)
            {
                // The previous equipment survived a destruction substitution, as with normal equipping.
                SendHandToMemory(p, index);
                Log($"{p.Name} conserva su equipo protegido; el Tótem de Salvación mostrado queda en memoria.");
                continue;
            }
            NotifyPileMove(p, CardCatalog.Totem.Id, CardZone.Hand, index, CardZone.Equipment);
            MoveHandCardToField(p, index, CardZone.Equipment, p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1);
            if (p.Equipment == null)
            {
                p.Equipment = CardCatalog.Totem.Id; p.EquipmentStackedCopies = 0;
                p.EquipmentDie = 0; p.EquipmentCounters = 0; p.EquipmentExpiresAtTurnStart = false;
            }
            else p.EquipmentStackedCopies++;
        }
        var frame = _activeActivation!;
        NotifyPileMove(p, frame.CardId, CardZone.Memory, frame.MemoryIndex, CardZone.EquipmentCover);
        MoveMemoryCardToField(new(frame.MemorySeat, frame.MemoryIndex), p);
        p.EquipmentCovers ??= [];
        if (frame.CorruptOnMemory)
        {
            p.MarkForCorruption(CardZone.EquipmentCover, p.EquipmentCovers.Count);
            frame.CorruptOnMemory = false;
        }
        p.EquipmentCovers.Add(frame.CardId);
        Log($"{p.Name} coloca Tótem de Oro encima de su equipo.");
        State.PendingGoldenTotem = null; State.Phase = frame.ResumePhase;
        CompleteActivationIfFinished();
    }
}

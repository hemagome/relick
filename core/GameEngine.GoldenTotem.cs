namespace Relick.Core;

public sealed class PendingGoldenTotem
{
    public int Actor { get; set; }
    public int[] HandIndices { get; set; } = [];
}

public sealed partial class GameEngine
{
    private void BeginGoldenTotem(int actor)
    {
        var p = State.Players[actor];
        if (p.Equipment == null && !p.Hand.Contains(CardCatalog.Totem.Id))
        {
            Log("Tótem de Oro se resuelve sin un equipo sobre el cual colocarse.");
            return;
        }
        State.PendingGoldenTotem = new() { Actor = actor };
        State.Phase = Phase.GoldenTotemChoice;
        State.LastCombat = $"{p.Name} · Tótem de Oro · Elige hasta dos Tótems de Salvación de tu mano. Necesitas un equipo debajo.";
    }

    public CommandResult ResolveGoldenTotem(int actor, IEnumerable<int> handIndices)
    {
        if (State.Phase != Phase.GoldenTotemChoice || State.PendingGoldenTotem?.Actor != actor ||
            _activeActivation is not { Kind: ActivationKind.Code } frame || frame.Actor != actor || !CardCatalog.Get(frame.CardId).DeployTotems)
            return CommandResult.Fail("No te corresponde colocar Tótem de Oro.");
        var p = State.Players[actor]; var selected = handIndices.ToArray();
        if (selected.Length > 2 || selected.Distinct().Count() != selected.Length ||
            selected.Any(i => !InHand(p, i) || p.Hand[i] != CardCatalog.Totem.Id) || p.Equipment == null && selected.Length == 0)
            return CommandResult.Fail("Muestra hasta dos Tótems de Salvación distintos de tu mano; Tótem de Oro necesita un equipo debajo.");
        State.PendingGoldenTotem.HandIndices = selected.OrderDescending().ToArray();
        foreach (var index in selected) p.RevealHandCard(index);
        frame.CanInterrupt = false; State.Phase = Phase.GoldenTotemResolving;
        Log($"{p.Name} muestra {selected.Length} Tótem(s) de Salvación para colocarlos con Tótem de Oro.");
        ContinueGoldenTotemPlacement(new EngineContinuation { Kind = ContinuationKind.GoldenTotemPlacement, Actor = actor });
        return CommandResult.Ok();
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
            MoveHandCardToField(p, index);
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
        p.EquipmentCovers ??= []; p.EquipmentCovers.Add(frame.CardId);
        Log($"{p.Name} coloca Tótem de Oro encima de su equipo.");
        State.PendingGoldenTotem = null; State.Phase = frame.ResumePhase;
        CompleteActivationIfFinished();
    }
}

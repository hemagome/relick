namespace Relick.Core;

public sealed record ZahiraPlacement(int Actor, Phase ResumePhase, bool Creating, int Slot = 0);

public sealed partial class GameEngine
{
    // A player's first Zahira Flair keeps the index -1 it always had; the second copy uses index 1.
    public static int ZahiraIndex(int slot) => slot == 0 ? -1 : slot;
    public static int ZahiraSlot(int index) => Math.Clamp(index, 0, PlayerState.ZahiraSlots - 1);

    private IEnumerable<(int Owner, int Slot, FieldTarget Host)> Zahiras()
    {
        for (var owner = 0; owner < State.Players.Length; owner++)
            for (var slot = 0; slot < PlayerState.ZahiraSlots; slot++)
                if (State.Players[owner].Zahira(slot) is { } host) yield return (owner, slot, host);
    }

    private static int FreeZahiraSlot(PlayerState player) =>
        Enumerable.Range(0, PlayerState.ZahiraSlots).FirstOrDefault(slot => player.Zahira(slot) == null, -1);

    public bool EffectsSuppressed(int owner, FieldZone zone, int index = -1)
    {
        if (zone == FieldZone.Equipment && index == -1) index = State.Players[owner].EquipmentStackedCopies;
        if (zone == FieldZone.Zahira) index = ZahiraIndex(ZahiraSlot(index));
        // A covered Zahira Flair loses its own effect, so the card under it works again. Chains never loop:
        // a Zahira Flair can only be placed over another that rests on an ordinary card.
        foreach (var (trapOwner, slot, host) in Zahiras())
            if (host.Owner == owner && host.Zone == zone && host.Index == index &&
                !EffectsSuppressed(trapOwner, FieldZone.Zahira, ZahiraIndex(slot)))
                return true;
        return false;
    }

    private CardDefinition? ActiveEquipment(int owner) =>
        State.Players[owner].Equipment is { } id && !EffectsSuppressed(owner, FieldZone.Equipment) ? CardCatalog.Get(id) : null;

    private IEnumerable<FieldTarget> FieldCards()
    {
        for (var owner = 0; owner < State.Players.Length; owner++)
        {
            var p = State.Players[owner];
            yield return new(owner, FieldZone.Relikario, -1, p.Relikario.Id);
            if (p.Equipment is { } equipment)
                for (var i = 0; i <= p.EquipmentStackedCopies; i++) yield return new(owner, FieldZone.Equipment, i, equipment);
            foreach (var light in LightEquipmentTargets(owner)) yield return light;
            for (var i = 0; i < (p.EquipmentCovers?.Count ?? 0); i++)
                yield return new(owner, FieldZone.EquipmentCover, i, p.EquipmentCovers![i]);
            for (var i = 0; i < p.Skills.Count; i++) yield return new(owner, FieldZone.Skill, i, p.Skills[i]);
            for (var i = 0; i < p.AttachedCards.Count; i++) yield return new(owner, FieldZone.Attached, i, p.AttachedCards[i]);
            if (p.Bonus is { } bonus) yield return new(owner, FieldZone.Bonus, -1, bonus);
            if (p.Area is { } area) yield return new(owner, FieldZone.Area, -1, area);
            for (var i = 0; i < p.Exchange.Count; i++) yield return new(owner, FieldZone.Exchange, i, p.Exchange[i]);
            for (var slot = 0; slot < PlayerState.ZahiraSlots; slot++)
                if (p.Zahira(slot) != null) yield return new(owner, FieldZone.Zahira, ZahiraIndex(slot), CardCatalog.ZahiraFlair.Id);
        }
    }

    /// <summary>Cards the Zahira Flair being placed or moved by <paramref name="actor"/> may cover.</summary>
    public IEnumerable<FieldTarget> ZahiraTargets(int actor)
    {
        var current = State.Players[actor].Zahira(State.PendingZahiraPlacement?.Slot ?? 0);
        return FieldCards().Where(target =>
            !(current is { } host && host.Owner == target.Owner && host.Zone == target.Zone && host.Index == target.Index) &&
            (target.Zone != FieldZone.Zahira || target.Owner != actor &&
                State.Players[target.Owner].Zahira(ZahiraSlot(target.Index)) is not { Zone: FieldZone.Zahira }));
    }

    internal string FieldCardName(FieldTarget target) => target.Zone == FieldZone.Relikario
        ? State.Players[target.Owner].Relikario.Name : CardCatalog.Get(target.CardId).Name;

    private FieldZone? ZahiraRootZone(int owner, int slot) => State.Players[owner].Zahira(slot) switch
    {
        { Zone: FieldZone.Zahira } host => ZahiraRootZone(host.Owner, ZahiraSlot(host.Index)),
        { Zone: FieldZone.Attached } => FieldZone.Relikario,
        { } host => host.Zone,
        _ => null
    };

    private void BeginZahiraPlacement(int actor, bool creating, int slot)
    {
        State.PendingZahiraPlacement = new(actor, State.Phase, creating, slot);
        State.Phase = Phase.ZahiraTarget;
    }

    public CommandResult ResolveZahiraTarget(int actor, int owner, FieldZone zone, int index)
    {
        if (State.Phase != Phase.ZahiraTarget || State.PendingZahiraPlacement is not { } pending || actor != pending.Actor)
            return CommandResult.Fail("No te corresponde elegir dónde colocar Zahira Flair.");
        var target = ZahiraTargets(actor).FirstOrDefault(card => card.Owner == owner && card.Zone == zone && card.Index == index);
        if (target == null) return CommandResult.Fail("Elige otra carta del campo para Zahira Flair.");

        State.PendingZahiraPlacement = null;
        State.Phase = pending.ResumePhase;
        var placement = new EngineContinuation { Kind = ContinuationKind.PlaceZahira, Actor = actor, Target = target, Flag = pending.Creating, Count = pending.Slot };
        if (!TryReplaceOpponentFieldEffect(actor, target, placement)) FinishZahiraPlacement(placement);
        return CommandResult.Ok();
    }

    private void FinishZahiraPlacement(EngineContinuation next)
    {
        var p = State.Players[next.Actor];
        var target = next.Target!;
        // A replacement can remove an equipment card while the attempted move is resolving.
        if (!next.Prevented)
        {
            p.SetZahira(next.Count, target);
            State.LastCombat = $"{p.Name} {(next.Flag ? "coloca" : "mueve")} Zahira Flair sobre {FieldCardName(target)} de {State.Players[target.Owner].Name}.";
        }
        else State.LastCombat += next.Flag ? " · Zahira Flair queda fuera del juego." : " · Zahira Flair sigue en su carta.";
        Log(State.LastCombat);
        CompletePendingPlay();
    }

    // Each Zahira Flair of the waiting player rolls once per final stage, in the order they were created.
    private bool TryRollZahiraAtFinal()
    {
        var owner = 1 - State.CurrentPlayer;
        var p = State.Players[owner];
        // Snapshots taken before a second copy existed only recorded that the single roll was done.
        for (var slot = Math.Max(State.ZahiraFinalSlot, State.ZahiraRolledThisFinal ? 1 : 0); slot < PlayerState.ZahiraSlots; slot++)
        {
            if (p.Zahira(slot) == null || EffectsSuppressed(owner, FieldZone.Zahira, ZahiraIndex(slot))) continue;
            State.ZahiraRolledThisFinal = true;
            State.ZahiraFinalSlot = slot + 1;
            RollWithRelikario(p, DiePurpose.Zahira, new DieEffect { Actor = owner, Purpose = DiePurpose.Zahira, Slot = slot });
            return true;
        }
        return false;
    }

    private void ResolveZahiraDie(int actor, int die, int slot)
    {
        if (State.Players[actor].Zahira(slot) is not { } host) { CompletePendingPlay(); return; }
        State.LastCombat = $"{State.Players[actor].Name} · Zahira Flair · D6 = {die}";
        Log(State.LastCombat);
        if (die <= 2) RequestDestruction(new(actor, FieldZone.Zahira, ZahiraIndex(slot), CardCatalog.ZahiraFlair.Id));
        else if (die <= 4) BeginZahiraPlacement(actor, creating: false, slot);
        else if (host.Zone == FieldZone.Relikario) DealDamage(host.Owner, 2, source: actor);
        else
        {
            var finish = new EngineContinuation { Kind = ContinuationKind.FinishZahiraDestruction, Actor = actor, Target = host };
            if (!TryReplaceOpponentFieldEffect(actor, host, finish)) RequestDestruction(host, finish);
        }
    }

    private void FinishZahiraDestruction(EngineContinuation next)
    {
        var target = next.Target!;
        if (!next.Prevented && target.Zone == FieldZone.Bonus) RefillBonus(State.Players[target.Owner]);
        else CompletePendingPlay();
    }

    private void ChangeZahiraHosts(int owner, FieldZone zone, int index, int count = 1, FieldTarget? movedTo = null)
    {
        if (zone == FieldZone.Zahira) index = ZahiraIndex(ZahiraSlot(index));
        foreach (var (trapOwner, slot, host) in Zahiras().ToList())
        {
            var p = State.Players[trapOwner];
            // Removing one Zahira Flair can already have taken away another that rested on it.
            if (p.Zahira(slot) != host || host.Owner != owner || host.Zone != zone || host.Index < index) continue;
            if (zone == FieldZone.Zahira)
            {
                // The two copies keep their own places, so nothing shifts when one of them leaves.
                if (host.Index == index) RemoveZahira(trapOwner, slot);
            }
            else if (host.Index >= index + count) p.SetZahira(slot, host with { Index = host.Index - count });
            else if (movedTo != null) p.SetZahira(slot, movedTo);
            else RemoveZahira(trapOwner, slot);
        }
    }

    private void RemoveZahira(int owner, int slot)
    {
        var p = State.Players[owner];
        if (p.Zahira(slot) == null) return;
        p.SetZahira(slot, null);
        NotifyPileMove(p, CardCatalog.ZahiraFlair.Id, CardZone.Zahira, destination: CardZone.OutOfGame);
        ChangeZahiraHosts(owner, FieldZone.Zahira, ZahiraIndex(slot));
    }
}

namespace Relick.Core;

public sealed record ZahiraPlacement(int Actor, Phase ResumePhase, bool Creating);

public sealed partial class GameEngine
{
    public bool EffectsSuppressed(int owner, FieldZone zone, int index = -1)
    {
        if (zone == FieldZone.Equipment && index == -1) index = State.Players[owner].EquipmentStackedCopies;
        for (var trapOwner = 0; trapOwner < State.Players.Length; trapOwner++)
            if (State.Players[trapOwner].ZahiraHost is { } host && host.Owner == owner && host.Zone == zone && host.Index == index &&
                !EffectsSuppressed(trapOwner, FieldZone.Zahira))
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
            for (var i = 0; i < (p.EquipmentCovers?.Count ?? 0); i++)
                yield return new(owner, FieldZone.EquipmentCover, i, p.EquipmentCovers![i]);
            for (var i = 0; i < p.Skills.Count; i++) yield return new(owner, FieldZone.Skill, i, p.Skills[i]);
            for (var i = 0; i < p.AttachedCards.Count; i++) yield return new(owner, FieldZone.Attached, i, p.AttachedCards[i]);
            if (p.Bonus is { } bonus) yield return new(owner, FieldZone.Bonus, -1, bonus);
            for (var i = 0; i < p.Exchange.Count; i++) yield return new(owner, FieldZone.Exchange, i, p.Exchange[i]);
            if (p.ZahiraHost != null) yield return new(owner, FieldZone.Zahira, -1, CardCatalog.ZahiraFlair.Id);
        }
    }

    public IEnumerable<FieldTarget> ZahiraTargets(int actor)
    {
        var current = State.Players[actor].ZahiraHost;
        return FieldCards().Where(target =>
            !(current is { } host && host.Owner == target.Owner && host.Zone == target.Zone && host.Index == target.Index) &&
            (target.Zone != FieldZone.Zahira || target.Owner != actor &&
                State.Players[target.Owner].ZahiraHost is not { Zone: FieldZone.Zahira }));
    }

    internal string FieldCardName(FieldTarget target) => target.Zone == FieldZone.Relikario
        ? State.Players[target.Owner].Relikario.Name : CardCatalog.Get(target.CardId).Name;

    private FieldZone? ZahiraRootZone(int owner) => State.Players[owner].ZahiraHost switch
    {
        { Zone: FieldZone.Zahira } host => ZahiraRootZone(host.Owner),
        { Zone: FieldZone.Attached } => FieldZone.Relikario,
        { } host => host.Zone,
        _ => null
    };

    private void BeginZahiraPlacement(int actor, bool creating)
    {
        State.PendingZahiraPlacement = new(actor, State.Phase, creating);
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
        var placement = new EngineContinuation { Kind = ContinuationKind.PlaceZahira, Actor = actor, Target = target, Flag = pending.Creating };
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
            p.ZahiraHost = target;
            State.LastCombat = $"{p.Name} {(next.Flag ? "coloca" : "mueve")} Zahira Flair sobre {FieldCardName(target)} de {State.Players[target.Owner].Name}.";
        }
        else State.LastCombat += next.Flag ? " · Zahira Flair queda fuera del juego." : " · Zahira Flair sigue en su carta.";
        Log(State.LastCombat);
        CompletePendingPlay();
    }

    private bool TryRollZahiraAtFinal()
    {
        var owner = 1 - State.CurrentPlayer;
        var p = State.Players[owner];
        if (State.ZahiraRolledThisFinal || p.ZahiraHost == null || EffectsSuppressed(owner, FieldZone.Zahira)) return false;
        State.ZahiraRolledThisFinal = true;
        RollWithRelikario(p, DiePurpose.Zahira, new DieEffect { Actor = owner, Purpose = DiePurpose.Zahira });
        return true;
    }

    private void ResolveZahiraDie(int actor, int die)
    {
        if (State.Players[actor].ZahiraHost is not { } host) { CompletePendingPlay(); return; }
        State.LastCombat = $"{State.Players[actor].Name} · Zahira Flair · D6 = {die}";
        Log(State.LastCombat);
        if (die <= 2) RequestDestruction(new(actor, FieldZone.Zahira, -1, CardCatalog.ZahiraFlair.Id));
        else if (die <= 4) BeginZahiraPlacement(actor, creating: false);
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
        for (var trapOwner = 0; trapOwner < State.Players.Length; trapOwner++)
        {
            var p = State.Players[trapOwner];
            if (p.ZahiraHost is not { } host || host.Owner != owner || host.Zone != zone || host.Index < index) continue;
            if (host.Index >= index + count) p.ZahiraHost = host with { Index = host.Index - count };
            else if (movedTo != null) p.ZahiraHost = movedTo;
            else RemoveZahira(trapOwner);
        }
    }

    private void RemoveZahira(int owner)
    {
        var p = State.Players[owner];
        if (p.ZahiraHost == null) return;
        p.ZahiraHost = null;
        NotifyPileMove(p, CardCatalog.ZahiraFlair.Id, CardZone.Zahira, destination: CardZone.OutOfGame);
        ChangeZahiraHosts(owner, FieldZone.Zahira, -1);
    }
}

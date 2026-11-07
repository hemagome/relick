namespace Relick.Core;
public enum FieldZone { Equipment, Attached, Bonus, Exchange }
public sealed record FieldTarget(int Owner, FieldZone Zone, int Index, string CardId);
public sealed partial class GameEngine
{
    public IEnumerable<FieldTarget> DestructionTargets()
    {
        for (var owner = 0; owner < 2; owner++)
        {
            var p = State.Players[owner];
            if (p.Equipment != null && CardCatalog.Get(p.Equipment).Kind != CardKind.Skill) yield return new(owner, FieldZone.Equipment, -1, p.Equipment);
            for (var i = 0; i < p.AttachedCards.Count; i++)
                if (CardCatalog.Get(p.AttachedCards[i]).Kind != CardKind.Skill) yield return new(owner, FieldZone.Attached, i, p.AttachedCards[i]);
            if (p.Bonus != null && CardCatalog.Get(p.Bonus).Kind != CardKind.Skill)
                yield return new(owner, FieldZone.Bonus, -1, p.Bonus);
            for (var i = 0; i < p.Exchange.Count; i++)
                if (CardCatalog.Get(p.Exchange[i]).Kind != CardKind.Skill)
                    yield return new(owner, FieldZone.Exchange, i, p.Exchange[i]);
        }
    }
    private string? CodeEffectUnavailable(PlayerState p, CardDefinition card, int extraCost = 0)
    {
        if (p.Relikia < card.PrCost + extraCost) return $"Necesitas {card.PrCost + extraCost} PR.";
        if (card.DestroyNonSkill && !DestructionTargets().Any()) return "No hay no-habilidades en el campo para destruir.";
        if (card.CancelActivation && !CanPlayLoadError(Array.IndexOf(State.Players, p))) return "No hay una activación que puedas anular.";
        if (card.RerollOwnDie && !OwnDiePending(Array.IndexOf(State.Players, p))) return "Requiere un dado propio pendiente.";
        return null;
    }
    public string? CodeUnavailableReason(int actor, int handIndex)
    {
        if (actor < 0 || actor >= 2 || State.IsOver || State.Phase == Phase.Preparation) return "No puedes jugar ahora.";
        var p = State.Players[actor];
        if (!InHand(p, handIndex) || CardCatalog.Get(p.Hand[handIndex]).Kind != CardKind.Code) return "Elige un código.";
        var card = CardCatalog.Get(p.Hand[handIndex]);
        if (!card.IsCombo) { var error = ValidateTurn(actor, Phase.Actions); if (error != null) return error; }
        if (p.ActionsLeft < card.ActionCost) return "No quedan acciones disponibles.";
        return CodeEffectUnavailable(p, card);
    }
    public CommandResult ResolveDestruction(int actor, int owner, bool equipment, int index = -1)
        => ResolveDestruction(actor, owner, equipment ? FieldZone.Equipment : FieldZone.Attached, index);

    public CommandResult ResolveDestruction(int actor, int owner, FieldZone zone, int index = -1)
    {
        if (State.Phase != Phase.DestroyChoice || State.DestroyActor != actor) return CommandResult.Fail("No te corresponde elegir objetivo.");
        var target = DestructionTargets().FirstOrDefault(t => t.Owner == owner && t.Zone == zone && (zone is FieldZone.Equipment or FieldZone.Bonus || t.Index == index));
        if (target == null) return CommandResult.Fail("Elige una no-habilidad del campo.");
        var p = State.Players[owner];
        if (zone != FieldZone.Equipment)
            NotifyPileMove(p, target.CardId, zone switch
            {
                FieldZone.Bonus => CardZone.Bonus, FieldZone.Exchange => CardZone.Exchange, _ => CardZone.Attached
            }, index);
        switch (zone)
        {
            case FieldZone.Equipment: DestroyEquipment(p); break;
            case FieldZone.Attached: p.AttachedCards.RemoveAt(index); p.Memory.Add(target.CardId); break;
            case FieldZone.Exchange: p.Exchange.RemoveAt(index); p.Memory.Add(target.CardId); break;
            case FieldZone.Bonus: p.Bonus = null; p.Memory.Add(target.CardId); break;
        }
        Log($"{State.Players[actor].Name} destruye {CardCatalog.Get(target.CardId).Name} de {p.Name} con Huevo Azul Explosivo.");
        State.DestroyActor = null; State.Phase = Phase.Actions; CompleteActivationIfFinished();
        if (zone == FieldZone.Bonus) RefillBonus(p);
        return CommandResult.Ok();
    }
    private bool OwnDiePending(int actor) => State.Phase == Phase.AttackRerollChoice ? State.PendingAttackRoll?.Attacker == actor
        : State.Phase == Phase.DieRerollChoice && State.PendingDieRoll?.Player == actor;
    private bool HasRerollCombo(int actor) => State.Players[actor].Hand.Any(id => { var c = CardCatalog.Get(id); return c.IsCombo && c.RerollOwnDie && State.Players[actor].ActionsLeft >= c.ActionCost; });
    private CommandResult PlayRerollCombo(int actor, int index)
    {
        var p = State.Players[actor]; var card = CardCatalog.Get(p.Hand[index]);
        SendHandToMemory(p, index); p.ActionsLeft -= card.ActionCost; p.Relikia -= card.PrCost;
        var frame = new EngineActivation(actor, card.Id, card.ActionCost, card.PrCost, State.Phase)
        { Kind = ActivationKind.Reroll, Parent = _activeActivation };
        BeginActivation(frame);
        return CommandResult.Ok();
    }
    private void ResolveRerollComboEffect(int actor, CardDefinition card)
    {
        var p = State.Players[actor];
        var phase = State.Phase; var attack = State.PendingAttackRoll; var effect = State.PendingDieRoll;
        var continuation = _pendingDieEffect;
        var die = Roll("Re-lanza · COMBO");
        if (attack != null) attack = attack with { Die = die, Modified = true, WasRerolled = true };
        if (effect != null) effect = effect with { Die = die, Modified = true, WasRerolled = true };
        State.PendingAttackRoll = null; State.PendingDieRoll = null; _pendingDieEffect = null;
        State.Phase = attack != null ? Phase.Combat : effect!.ResumePhase;
        var resume = new EngineContinuation { Kind = ContinuationKind.ResumeCombo, Actor = actor, Phase = phase,
            Attack = attack, Die = effect, SavedDieEffect = continuation };
        Log($"{p.Name} activa {card.Name}: dado {die}." + (die <= 3 ? " Roba una carta." : ""));
        if (die <= 3) DrawCards(p, 1, resume); else Continue(resume);
    }
}

namespace Relick.Core;

public sealed class PendingDestruction
{
    public FieldTarget Target { get; set; } = new(0, FieldZone.Equipment, -1, "");
    public Phase ResumePhase { get; set; }
    public EngineContinuation? Completed { get; set; }
}

public sealed partial class GameEngine
{
    public IEnumerable<int> DestructionProtectors(int actor)
    {
        if (State.Phase != Phase.DestructionResponse || State.PendingDestruction?.Target.Owner != actor) return [];
        return EligibleDestructionProtectors(State.PendingDestruction.Target);
    }

    private IEnumerable<int> EligibleDestructionProtectors(FieldTarget target) =>
        Enumerable.Range(0, State.Players[target.Owner].Skills.Count).Where(slot =>
            CardCatalog.Get(State.Players[target.Owner].Skills[slot]).ProtectOwnCard &&
            !EffectsSuppressed(target.Owner, FieldZone.Skill, slot) && !(target.Zone == FieldZone.Skill && target.Index == slot));

    private void RequestDestruction(FieldTarget target, EngineContinuation? completed = null)
    {
        if (State.IsOver) return;
        var p = State.Players[target.Owner];
        var cover = Enumerable.Range(0, p.EquipmentCovers?.Count ?? 0).LastOrDefault(index =>
            p.EquipmentCovers![index] == CardCatalog.GoldenTotem.Id && !EffectsSuppressed(target.Owner, FieldZone.EquipmentCover, index), -1);
        if (target.Zone == FieldZone.Equipment && target.CardId == CardCatalog.Totem.Id &&
            cover >= 0)
        {
            RequestDestruction(new(target.Owner, FieldZone.EquipmentCover, cover, CardCatalog.GoldenTotem.Id),
                new EngineContinuation { Kind = ContinuationKind.GoldenTotemReplacement, Actor = target.Owner, Next = completed });
            return;
        }
        if (EligibleDestructionProtectors(target).Any())
        {
            State.PendingDestruction = new() { Target = target, ResumePhase = State.Phase, Completed = completed };
            State.Phase = Phase.DestructionResponse;
            var name = target.Zone == FieldZone.Hand ? "una carta de su mano" : CardCatalog.Get(target.CardId).Name;
            State.LastCombat = $"{p.Name} · Puede sacrificar Reverse Shuffle para proteger {name}.";
            return;
        }
        DestroyCardNow(target);
        Continue(completed);
    }

    public CommandResult ResolveDestructionProtection(int actor, int skillSlot = -1)
    {
        var pending = State.PendingDestruction;
        if (State.Phase != Phase.DestructionResponse || pending?.Target.Owner != actor)
            return CommandResult.Fail("No te corresponde decidir esta destrucción.");
        if (skillSlot != -1 && !DestructionProtectors(actor).Contains(skillSlot))
            return CommandResult.Fail("Elige una Reverse Shuffle enseñada para sustituir la destrucción.");
        State.Phase = pending.ResumePhase;
        if (skillSlot >= 0)
        {
            RemoveLearnedSkill(actor, skillSlot);
            if (pending.Completed != null) pending.Completed.Prevented = true;
            Log($"{State.Players[actor].Name} destruye Reverse Shuffle en lugar de su otra carta.");
        }
        else DestroyCardNow(pending.Target);
        State.PendingDestruction = null;
        Continue(pending.Completed);
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }

    private void DestroyCardNow(FieldTarget target)
    {
        var p = State.Players[target.Owner];
        switch (target.Zone)
        {
            case FieldZone.Equipment: DestroyEquipmentNow(p, target.Index); return;
            case FieldZone.LightEquipment:
                SendToMemory(p, target.CardId, CardZone.LightEquipment, target.Index);
                RemoveLightEquipment(p, target.Index);
                return;
            case FieldZone.Zahira: RemoveZahira(target.Owner, ZahiraSlot(target.Index)); return;
            case FieldZone.EquipmentCover:
                ChangeZahiraHosts(target.Owner, target.Zone, target.Index);
                p.EquipmentCovers!.RemoveAt(target.Index);
                if (p.EquipmentCovers.Count == 0) p.EquipmentCovers = null;
                break;
            case FieldZone.Hand: SendHandToMemory(p, target.Index); return;
            case FieldZone.Skill: RemoveLearnedSkill(target.Owner, target.Index); return;
            case FieldZone.Area:
                SendToMemory(p, target.CardId, CardZone.Area, -1);
                ClearArea(p);
                return;
            case FieldZone.Attached:
                ChangeZahiraHosts(target.Owner, target.Zone, target.Index); p.AttachedCards.RemoveAt(target.Index); break;
            case FieldZone.Exchange:
                ChangeZahiraHosts(target.Owner, target.Zone, target.Index);
                ForgetFieldCodeSource(p, FieldZone.Exchange, target.Index); p.Exchange.RemoveAt(target.Index);
                ShiftExchangeEffects(p, target.Index, 1); break;
            case FieldZone.Bonus:
                ChangeZahiraHosts(target.Owner, target.Zone, -1); ForgetFieldCodeSource(p, FieldZone.Bonus);
                p.Bonus = null; p.InitialAreaWaitingInBonus = false; break;
        }
        var source = target.Zone switch
        {
            FieldZone.Bonus => CardZone.Bonus, FieldZone.Exchange => CardZone.Exchange, FieldZone.EquipmentCover => CardZone.EquipmentCover, _ => CardZone.Attached
        };
        SendToMemory(p, target.CardId, source, target.Index);
        if (source == CardZone.Bonus) p.ClearCorruptionMarks(source);
        else p.RemoveCorruptionSlots(source, target.Index);
    }

    private void DestroyEquipment(PlayerState p, EngineContinuation? completed = null, int index = -1)
    {
        if (p.Equipment == null) { Continue(completed); return; }
        RequestDestruction(new(Array.IndexOf(State.Players, p), FieldZone.Equipment, index, p.Equipment), completed);
    }

    private void DestroyAttackOneEquipment(int owner, EngineContinuation? completed)
    {
        if (owner >= State.Players.Length) { Continue(completed); return; }
        var p = State.Players[owner];
        var next = owner + 1 < State.Players.Length
            ? new EngineContinuation { Kind = ContinuationKind.AttackOneEquipment, Actor = owner + 1, Next = completed } : completed;
        if (ActiveEquipment(owner)?.DestroyOnAttackOne == true) DestroyEquipment(p, next);
        else Continue(next);
    }

    private void ProcessAttackDie(int die, EngineContinuation? completed)
    {
        if (die == 1 && !HasStackedTotem(State.CurrentPlayer)) DestroyAttackOneEquipment(0, completed);
        else Continue(completed);
    }

    private void FinishCodeDestruction(EngineContinuation next)
    {
        var target = next.Target!;
        var card = target.Zone == FieldZone.Hand ? "una carta de la mano" : CardCatalog.Get(target.CardId).Name;
        Log(next.Prevented ? $"{next.Text}: la carta original queda protegida por una sustitución." :
            $"{State.Players[next.Actor].Name} destruye {card} de {State.Players[target.Owner].Name} con {next.Text}.");
        State.DestroyActor = null; State.Phase = Phase.Actions;
        var drawChoice = !LegacyBlueEgg && _activeActivation?.CardId == CardCatalog.BlueEgg.Id
            ? new EngineContinuation { Kind = ContinuationKind.OfferBlueEggDraw, Actor = next.Actor } : null;
        if (!next.Prevented && target.Zone == FieldZone.Bonus) RefillBonus(State.Players[target.Owner], drawChoice);
        else if (drawChoice != null) Continue(drawChoice);
        CompleteActivationIfFinished();
    }
}

namespace Relick.Core;

public sealed partial class GameEngine
{
    private const string RaidSides = "sides", RaidOrientMovedArea = "orient-moved-area", RaidAttack = "attack", RaidSearch = "search",
        RaidEquip = "equip", RaidReward = "reward";

    private void AskRaid(int holder, int actor, string step, DecisionKind kind, string instruction, int? showsHandOf = null) =>
        AskDecision(new PendingDecision { Source = CardCatalog.EquipmentRaid.Id, Decider = actor, Value = holder,
            Step = step, Kind = kind, Instruction = instruction, ShowsHandOf = showsHandOf });

    private IReadOnlyList<DecisionChoice> EquipmentRaidChoices(PendingDecision decision)
    {
        var player = State.Players[decision.Decider];
        var rival = State.Players[1 - decision.Decider];
        switch (decision.Step)
        {
            case RaidSides or RaidOrientMovedArea:
                return AreaSideChoices(decision.Decider);
            case RaidAttack:
                return [
                    new DecisionChoice { Label = "Colocar un Ataque debajo de mi Relikario", Value = 1, Primary = true,
                        Public = "coloca un Ataque de la memoria rival debajo de su Relikario." },
                    new DecisionChoice { Label = "Continuar sin colocar un Ataque", Value = 0, Public = "pasa sin colocar un Ataque." }
                ];
            case RaidSearch:
                return RaidSearchChoices(player).ToArray();
            case RaidEquip:
                return player.Deck.Select((id, index) => (id, index)).Where(c => CardCatalog.Get(c.id).Kind == CardKind.Equipment)
                    .Select(c => new DecisionChoice { Label = $"Equipar {CardCatalog.Get(c.id).Name}", Button = "Equipar",
                        CardId = c.id, Value = c.index, Public = "elige un equipo de su mazo." }).ToArray();
            case RaidReward:
                return rival.Hand.Select((id, index) => new DecisionChoice { Label = $"Tomar {CardCatalog.Get(id).Name} · carta {index + 1}",
                    Button = "Tomar carta", Value = index, Field = new(1 - decision.Decider, FieldZone.Hand, index, id),
                    CardId = id, Public = "toma una carta de la mano rival." }).ToArray();
            default: return [];
        }
    }

    private IEnumerable<DecisionChoice> RaidSearchChoices(PlayerState p)
    {
        var id = CardCatalog.TakeEquipment.Id;
        var deck = p.Deck.IndexOf(id);
        var memory = p.Memory.IndexOf(id);
        if (deck >= 0) yield return new DecisionChoice { Label = $"Añadir {CardCatalog.TakeEquipment.Name} del mazo",
            Button = "Añadir del mazo", CardId = id, Value = deck, Public = "añade ¡Dame ese equipo! de su mazo." };
        if (memory >= 0) yield return new DecisionChoice { Label = $"Añadir {CardCatalog.TakeEquipment.Name} de la memoria",
            Button = "Añadir de memoria", CardId = id, Value = memory, Owner = Seat(p), Public = "añade ¡Dame ese equipo! de su memoria." };
    }

    private void ResolveEquipmentRaidChoice(PendingDecision decision, DecisionChoice choice)
    {
        var holder = decision.Value;
        var actor = decision.Decider;
        var p = State.Players[actor];
        switch (decision.Step)
        {
            case RaidOrientMovedArea:
                State.Players[holder].AreaSideA = choice.Value;
                CompletePendingPlay();
                break;
            case RaidSides:
                State.Players[holder].AreaSideA = choice.Value;
                if (EffectsSuppressed(holder, FieldZone.Area)) { CompletePendingPlay(); break; }
                var rival = State.Players[1 - choice.Value];
                if (rival.Memory.Any(id => DeckRules.IsAttack(CardCatalog.Get(id))))
                    AskRaid(holder, choice.Value, RaidAttack, DecisionKind.Confirm, "Lado A · Decide si colocas un Ataque de la memoria rival bajo tu Relikario");
                else ContinueRaidSideA(holder, choice.Value);
                break;
            case RaidAttack:
                if (choice.Value == 1) AttachRivalMemoryAttack(actor);
                ContinueRaidSideA(holder, actor);
                break;
            case RaidSearch:
                if (choice.Owner != null)
                {
                    TakeFromMemory(actor, actor, choice.Value, CardCatalog.EquipmentRaid.Name);
                    PresentRaidSideB(holder, 1 - actor);
                }
                else
                    MoveSearchedDeckCardToHand(p, choice.Value, new EngineContinuation { Kind = ContinuationKind.AreaRaidSideB, Actor = 1 - actor, Value = holder });
                break;
            case RaidEquip:
                var next = new EngineContinuation { Kind = ContinuationKind.EquipFromDeck, Actor = actor, Value = choice.Value, Text = choice.CardId! };
                if (CanInstallEquipment(p, CardCatalog.Get(next.Text))) Continue(next);
                else DestroyEquipment(p, next);
                break;
            case RaidReward:
                MoveCardBetweenHands(State.Players[1 - actor], p, choice.Value);
                DestroyEquipmentRaid(holder);
                break;
        }
    }

    private void AttachRivalMemoryAttack(int actor)
    {
        var rival = State.Players[1 - actor];
        var index = rival.Memory.FindIndex(id => DeckRules.IsAttack(CardCatalog.Get(id)));
        if (index < 0) return;
        var id = RemoveMemoryCard(new(1 - actor, index));
        State.Players[actor].AttachedCards.Add(id);
        MarkBorrowedFieldCard(State.Players[actor], id);
        NotifyPileMove(rival, id, CardZone.Memory, index, CardZone.Attached, destinationPlayer: actor);
    }

    private void ContinueRaidSideA(int holder, int actor)
    {
        if (RaidSearchChoices(State.Players[actor]).Any())
            AskRaid(holder, actor, RaidSearch, DecisionKind.ChooseCard, "Lado A · Añade ¡Dame ese equipo! de tu mazo o memoria");
        else PresentRaidSideB(holder, 1 - actor);
    }

    private void PresentRaidSideB(int holder, int actor)
    {
        if (State.Players[actor].Deck.Any(id => CardCatalog.Get(id).Kind == CardKind.Equipment))
            AskRaid(holder, actor, RaidEquip, DecisionKind.ChooseCard, "Lado B · Elige un equipo de tu mazo para equiparte");
        else CompletePendingPlay();
    }

    private void FinishEquipFromDeck(EngineContinuation next)
    {
        var p = State.Players[next.Actor];
        var card = CardCatalog.Get(next.Text);
        if (next.Prevented || !CanInstallEquipment(p, card) || p.Deck.ElementAtOrDefault(next.Value) != card.Id)
        {
            Log($"{p.Name} no pudo equiparse {card.Name} con {CardCatalog.EquipmentRaid.Name}.");
            CompletePendingPlay();
            return;
        }
        if (card.LightEquipment)
        {
            if (p.CorruptsOnMemory(CardZone.Deck, next.Value))
                p.MarkForCorruption(CardZone.LightEquipment, p.LightEquipment?.Count ?? 0);
            p.Deck.RemoveAt(next.Value); p.RemoveCorruptionSlots(CardZone.Deck, next.Value);
            InstallLightEquipment(p, card.Id);
            NotifyPileMove(p, card.Id, CardZone.Deck, destination: CardZone.LightEquipment);
            State.LastCombat = $"{p.Name} equipa {card.Name} de su mazo como equipo ligero.";
            Log(State.LastCombat);
            if (p.Deck.Count == 0) Exhaust(p);
            else CompletePendingPlay();
            return;
        }
        var stacked = p.Equipment != null;
        var slot = stacked ? p.EquipmentStackedCopies + 1 : 0;
        if (p.CorruptsOnMemory(CardZone.Deck, next.Value)) p.MarkForCorruption(CardZone.Equipment, slot);
        p.Deck.RemoveAt(next.Value); p.RemoveCorruptionSlots(CardZone.Deck, next.Value);
        p.Equipment = card.Id;
        p.EquipmentStackedCopies = slot;
        NotifyPileMove(p, card.Id, CardZone.Deck, destination: CardZone.Equipment);
        State.LastCombat = $"{p.Name} equipa {card.Name} de su mazo.";
        Log(State.LastCombat);
        var after = p.Deck.Count == 0 ? new EngineContinuation { Kind = ContinuationKind.CheckAreaEquipDeck, Actor = next.Actor } : null;
        if (!stacked)
        {
            p.EquipmentDie = 0;
            p.EquipmentCounters = 0;
            p.EquipmentExpiresAtTurnStart = card.DestroyAtNextTurnStart;
            if (card.Heal > 0) Heal(p, card.Heal);
            if (card.StoreDie) { RollEquipmentStorage(p, after); return; }
        }
        if (!OfferGhostGift(next.Actor, after)) Continue(after);
    }

    private void PresentEquipmentRaidReward(int holder)
    {
        var area = State.Players[holder];
        if (area.Area != CardCatalog.EquipmentRaid.Id || area.AreaSideA is not int actor)
        {
            CompletePendingPlay();
            return;
        }
        var p = State.Players[actor];
        p.Relikia += 1500;
        var rival = 1 - actor;
        Log($"{p.Name} gana 1500 PR con {CardCatalog.EquipmentRaid.Name}.");
        if (State.Players[rival].Hand.Count > 0)
            AskRaid(holder, actor, RaidReward, DecisionKind.ChooseTarget, "Lado B · Mira la mano rival y elige una carta para tomar", rival);
        else DestroyEquipmentRaid(holder);
    }

    private void DestroyEquipmentRaid(int holder)
    {
        if (State.Players[holder].Area == CardCatalog.EquipmentRaid.Id)
            RequestDestruction(new(holder, FieldZone.Area, -1, CardCatalog.EquipmentRaid.Id));
        else CompletePendingPlay();
    }
}

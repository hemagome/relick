namespace Relick.Core;

public sealed record ShieldCharge(string CardId, bool CorruptOnMemory);

public sealed class GalacticShield
{
    public bool Borrowed { get; init; }
    public List<ShieldCharge> Cards { get; init; } = [];
}

public sealed partial class GameEngine
{
    private const int GalacticShieldCapacity = 3;
    private const int GalacticShieldReduction = 2;

    private IEnumerable<FieldTarget> GalacticShieldTargets(int owner) =>
        Enumerable.Range(0, State.Players[owner].GalacticShields?.Count ?? 0)
            .Select(index => new FieldTarget(owner, FieldZone.GalacticShield, index, CardCatalog.GalacticShield.Id));

    private void DeployGalacticShield(int actor)
    {
        var frame = _activeActivation!;
        var player = State.Players[actor];
        var shields = player.GalacticShields ??= [];
        var slot = shields.Count;
        RemoveMemoryCard(new(frame.MemorySeat, frame.MemoryIndex));
        if (frame.CorruptOnMemory)
            player.MarkForCorruption(CardZone.GalacticShield, slot);
        frame.CorruptOnMemory = false;
        frame.CanInterrupt = false;
        shields.Add(new GalacticShield { Borrowed = frame.MemorySeat != actor });
        Log($"{player.Name} coloca Escudo Galáctico encima de su Relikario.");
        LoadGalacticShield(new EngineContinuation { Kind = ContinuationKind.LoadGalacticShield, Actor = actor, Value = slot });
    }

    private void LoadGalacticShield(EngineContinuation load)
    {
        var player = State.Players[load.Actor];
        var cards = player.GalacticShields![load.Value].Cards;
        while (cards.Count < GalacticShieldCapacity && player.Deck.Count > 0)
        {
            cards.Add(new(player.Deck[0], player.CorruptsOnMemory(CardZone.Deck, 0)));
            player.Deck.RemoveAt(0);
            player.RemoveCorruptionSlots(CardZone.Deck, 0);
            if (player.Deck.Count > 0) continue;
            Exhaust(player, load);
            return;
        }
        if (cards.Count < GalacticShieldCapacity)
        {
            Exhaust(player, load);
            return;
        }
        Log($"{player.Name} coloca {cards.Count} carta(s) boca abajo debajo de Escudo Galáctico.");
    }

    private void RemoveGalacticShield(int owner, int slot)
    {
        var player = State.Players[owner];
        var shields = player.GalacticShields!;
        SendToMemory(player, CardCatalog.GalacticShield.Id, CardZone.GalacticShield, slot);
        foreach (var card in shields[slot].Cards)
            SendToMemory(player, card.CardId, CardZone.ShieldCharge, slot, marked: card.CorruptOnMemory);
        ChangeZahiraHosts(owner, FieldZone.GalacticShield, slot);
        player.RemoveCorruptionSlots(CardZone.GalacticShield, slot);
        shields.RemoveAt(slot);
        if (shields.Count == 0) player.GalacticShields = null;
    }

    private IEnumerable<FieldTarget> ChargedGalacticShields(int owner) => GalacticShieldTargets(owner).Where(shield =>
        State.Players[owner].GalacticShields![shield.Index].Cards.Count > 0 && !EffectsSuppressed(owner, shield.Zone, shield.Index));

    private void OfferGalacticShieldDamage(int actor, int amount, int attackDie, EngineContinuation? completed, int start = 0)
    {
        if (amount <= 0) { Continue(completed); return; }
        var shield = ChargedGalacticShields(actor).FirstOrDefault(card => card.Index >= start);
        if (shield == null)
        {
            ApplyDamageAfterShields(actor, amount, attackDie, completed);
            return;
        }
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.GalacticShield.Id, Step = "damage", Decider = actor, Kind = DecisionKind.Confirm,
            Instruction = $"Vas a recibir {amount} de daño. Escudo Galáctico añade una carta boca abajo a tu mano y lo reduce en 2.",
            Continuation = new EngineContinuation { Actor = actor, Target = shield, Count = amount, Value = attackDie, Next = completed }
        });
    }

    private int? AboveRelikarioOwner(FieldTarget target) => target.Zone switch
    {
        FieldZone.Relikario or FieldZone.GalacticShield => target.Owner,
        FieldZone.Attached when !DeckRules.IsAttack(CardCatalog.Get(target.CardId)) => target.Owner,
        FieldZone.Zahira when State.Players[target.Owner].Zahira(ZahiraSlot(target.Index)) is { } host => AboveRelikarioOwner(host),
        _ => null
    };

    private bool TryProtectWithGalacticShield(FieldTarget target, EngineContinuation? completed)
    {
        if (AboveRelikarioOwner(target) is not int owner || !ChargedGalacticShields(owner).Any(shield => shield != target))
            return false;
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.GalacticShield.Id, Step = "protect", Decider = owner, Kind = DecisionKind.Confirm,
            Instruction = $"Puedes enviar una carta boca abajo a tu memoria para evitar que {FieldCardName(target)} salga del campo.",
            Continuation = new EngineContinuation { Actor = owner, Target = target, Next = completed }
        });
        return true;
    }

    private IReadOnlyList<DecisionChoice> GalacticShieldChoices(PendingDecision decision)
    {
        var target = decision.Continuation!.Target!;
        var takingDamage = decision.Step == "damage";
        var shields = ChargedGalacticShields(decision.Decider).Where(shield => takingDamage ? shield == target : shield != target).ToArray();
        var choices = new List<DecisionChoice>();
        foreach (var shield in shields)
        {
            // The charges are face down, so the player picks a shield and takes its top card. Version 1 duels cached
            // one option per charge, so they keep that list for their recorded indices to stay valid.
            var charges = LegacyBlueEgg ? State.Players[decision.Decider].GalacticShields![shield.Index].Cards.Count : 1;
            for (var i = 0; i < charges; i++)
            {
                var label = LegacyBlueEgg ? $"Carta boca abajo {i + 1}"
                    : takingDamage ? "Añadir una carta boca abajo a tu mano" : "Enviar una carta boca abajo a memoria";
                if (shields.Length > 1) label += $" · Escudo {shield.Index + 1}";
                choices.Add(new DecisionChoice
                {
                    Label = label, Value = shield.Index * GalacticShieldCapacity + i,
                    Public = takingDamage ? "añade una carta boca abajo de Escudo Galáctico a su mano."
                        : $"envía una carta boca abajo a memoria para conservar {FieldCardName(target)}."
                });
            }
        }
        if (!takingDamage)
            choices.Add(new DecisionChoice { Label = "Pasar sin usar el escudo", Value = -1, Public = "decide no usar Escudo Galáctico." });
        return choices;
    }

    private void ResolveGalacticShieldChoice(PendingDecision decision, DecisionChoice choice)
    {
        var pending = decision.Continuation!;
        if (choice.Value < 0)
        {
            RequestDestruction(pending.Target!, pending.Next, checkShield: false);
            return;
        }
        var player = State.Players[pending.Actor];
        var slot = choice.Value / GalacticShieldCapacity;
        var position = choice.Value % GalacticShieldCapacity;
        var cards = player.GalacticShields![slot].Cards;
        var card = cards[position];
        cards.RemoveAt(position);
        if (decision.Step == "protect")
        {
            SendToMemory(player, card.CardId, CardZone.ShieldCharge, slot, marked: card.CorruptOnMemory);
            if (pending.Next != null) pending.Next.Prevented = true;
            Log($"{player.Name} conserva {FieldCardName(pending.Target!)} gracias a Escudo Galáctico.");
            Continue(pending.Next);
            return;
        }
        if (card.CorruptOnMemory) player.MarkForCorruption(CardZone.Hand, player.Hand.Count);
        player.Hand.Add(card.CardId);
        var reduced = Math.Max(0, pending.Count - GalacticShieldReduction);
        NotifyGuard(pending.Actor, pending.Value > 0 ? 1 - pending.Actor : null,
            CardCatalog.GalacticShield.Id, pending.Count, reduced, blocked: false);
        State.LastCombat += $" · Escudo Galáctico: {reduced} de daño";
        Log($"{player.Name} añade una carta boca abajo a su mano y reduce el daño de {pending.Count} a {reduced} con Escudo Galáctico.");
        OfferGalacticShieldDamage(pending.Actor, reduced, pending.Value, pending.Next, slot + 1);
    }
}

namespace Relick.Core;

public sealed record ShieldCharge(string CardId, bool CorruptOnMemory);

public sealed class GalacticShield
{
    public bool Borrowed { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Instance { get; init; }
    public List<ShieldCharge> Cards { get; init; } = [];
}

public sealed partial class GameEngine
{
    private const int GalacticShieldCapacity = GalacticShieldEffect.Capacity;

    private IEnumerable<FieldTarget> GalacticShieldTargets(int owner) => GalacticShieldEffect.Shields(State, owner);

    private void DeployGalacticShield(int actor)
    {
        var frame = _activeActivation!;
        var player = State.Players[actor];
        var shields = player.GalacticShields ??= [];
        var slot = shields.Count;
        var id = frame.FieldCardId ?? frame.CardId;
        if (frame.HeldCard == null && frame.FieldCardId == null) RemoveMemoryCard(new(frame.MemorySeat, frame.MemoryIndex));
        else id = InstallActivationCard(frame, player, CardZone.GalacticShield, slot);
        if (frame.CorruptOnMemory)
            player.MarkForCorruption(CardZone.GalacticShield, slot);
        frame.CorruptOnMemory = false;
        frame.CanInterrupt = false;
        shields.Add(new GalacticShield { Borrowed = frame.MemorySeat != actor, Instance = frame.FieldCardId == null ? null : id });
        NotifyPileMove(player, CardCatalog.GalacticShield.Id, CardZone.Activation, slot, CardZone.GalacticShield);
        Log($"{player.Name} coloca Escudo Galáctico encima de su Relikario.");
        LoadGalacticShield(new EngineContinuation { Kind = ContinuationKind.LoadGalacticShield, Actor = actor, Value = slot });
    }

    private void LoadGalacticShield(EngineContinuation load)
    {
        var player = State.Players[load.Actor];
        var cards = player.GalacticShields![load.Value].Cards;
        while (cards.Count < GalacticShieldCapacity && player.Deck.Count > 0)
        {
            var id = player.Deck[0];
            cards.Add(new(id, player.CorruptsOnMemory(CardZone.Deck, 0)));
            player.Deck.RemoveAt(0);
            player.RemoveCorruptionSlots(CardZone.Deck, 0);
            NotifyPileMove(player, id, CardZone.Deck, load.Value, CardZone.ShieldCharge);
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
        SendToMemory(player, shields[slot].Instance ?? CardCatalog.GalacticShield.Id, CardZone.GalacticShield, slot);
        foreach (var card in shields[slot].Cards)
            SendToMemory(player, card.CardId, CardZone.ShieldCharge, slot, marked: card.CorruptOnMemory);
        ChangeZahiraHosts(owner, FieldZone.GalacticShield, slot);
        player.RemoveCorruptionSlots(CardZone.GalacticShield, slot);
        shields.RemoveAt(slot);
        if (shields.Count == 0) player.GalacticShields = null;
    }

    private int? AboveRelikarioOwner(FieldTarget target) => target.Zone switch
    {
        FieldZone.Relikario or FieldZone.GalacticShield => target.Owner,
        FieldZone.TomeCard when TomeHostZone(target.Owner, target.Index) == FieldZone.Attached => target.Owner,
        FieldZone.Attached when !DeckRules.IsAttack(CardCatalog.Get(target.CardId)) => target.Owner,
        FieldZone.Zahira when State.Players[target.Owner].Zahira(ZahiraSlot(target.Index)) is { } host => AboveRelikarioOwner(host),
        _ => null
    };
}

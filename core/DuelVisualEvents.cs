namespace Relick.Core;

public enum DuelVisualKind { Die, Attack, Hit, Draw, Return, Shuffle, BonusPlace, BonusClaim, Pile, Heal, HandTransfer, DieAdjustment, MemoryClaim, HandDiscard }

// Public presentation cues for ONE accepted revision. Never include card identities,
// deck order, random tapes or hidden state. Clients discard cues on recovery/gaps.
public sealed class DuelVisualEvent
{
    public DuelVisualKind Kind { get; set; }
    public int Player { get; set; }
    public int Value { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int SourceIndex { get; set; }
    // Final attack die, separate from damage or a secondary damage roll; zero for non-attack damage.
    public int AttackDie { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool NormalFromMiss { get; set; }
    public string Caption { get; set; } = "";
    public int Target { get; set; }
    public CardZone Source { get; set; }
    public CardZone Destination { get; set; }
}

public static class DuelVisualEvents
{
    public static List<DuelVisualEvent> Capture(GameEngine game)
    {
        var events = new List<DuelVisualEvent>();
        void Add(DuelVisualKind kind, int player, int value = 0, string caption = "")
            => events.Add(new() { Kind = kind, Player = player, Value = value, Caption = caption });
        game.DieRolled += value => Add(DuelVisualKind.Die, -1, value, game.CurrentDieCaption);
        game.DieAdjusted += value => Add(DuelVisualKind.DieAdjustment, -1, value, game.CurrentDieCaption);
        game.AttackPerformed += player => Add(DuelVisualKind.Attack, player);
        game.DamageReceived += (player, amount, attackDie) => events.Add(new()
            { Kind = DuelVisualKind.Hit, Player = player, Value = amount, AttackDie = attackDie,
                NormalFromMiss = attackDie > 0 && game.State.PendingAttack?.NormalFromMiss == true });
        game.HealingReceived += (player, amount) => Add(DuelVisualKind.Heal, player, amount);
        game.HandTransferred += (owner, recipient, count) => events.Add(new()
            { Kind = DuelVisualKind.HandTransfer, Player = owner, Target = recipient, Value = count });
        game.HandDiscarded += (owner, count) => events.Add(new()
            { Kind = DuelVisualKind.HandDiscard, Player = owner, Value = count, Source = CardZone.Hand, Destination = CardZone.Memory });
        game.CardDrawn += (player, slot) => Add(DuelVisualKind.Draw, player, slot);
        game.CardReturnedToDeck += (player, slot, _) => Add(DuelVisualKind.Return, player, slot);
        game.CardsReturnedToDeck += player => Add(DuelVisualKind.Shuffle, player);
        game.BonusPlaced += (player, _) => Add(DuelVisualKind.BonusPlace, player);
        game.BonusClaimed += (owner, recipient, slot, _) => events.Add(new() { Kind = DuelVisualKind.BonusClaim, Player = owner, Target = recipient, Value = slot });
        game.MemoryCardClaimed += (owner, recipient, sourceIndex, slot) => events.Add(new()
            { Kind = DuelVisualKind.MemoryClaim, Player = owner, Target = recipient, SourceIndex = sourceIndex, Value = slot, Source = CardZone.Memory, Destination = CardZone.Hand });
        game.CardMovedToPile += movement => events.Add(new() { Kind = DuelVisualKind.Pile, Player = movement.Owner,
            Source = movement.Source, Destination = movement.Destination, Value = movement.Index });
        return events;
    }
}

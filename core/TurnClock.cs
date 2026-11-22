using System.Text.Json;

namespace Relick.Core;

/// <summary>
/// Decision time of both seats, written by the server with its own timestamps and published on the shared board.
/// It is informative: the engine never reads it and reaching the reference has no consequence.
/// </summary>
public sealed class TurnClock
{
    /// <summary>What each seat may think per turn before the gauge shows extra time.</summary>
    public const long DefaultReference = 90_000_000;

    /// <summary>Microseconds; travels with the board so the server alone decides it.</summary>
    public long Reference { get; set; } = DefaultReference;
    public int Turn { get; set; }
    /// <summary>The seat that must decide, whoever owns the turn; null once the duel ends.</summary>
    public int? Running { get; set; }
    /// <summary>Server time, in Unix microseconds, up to which the totals below are settled.</summary>
    public long UpdatedAt { get; set; }
    public long[] SpentThisTurn { get; set; } = [0, 0];
    public long[] SpentInDuel { get; set; } = [0, 0];

    /// <summary>Charges the time since <paramref name="previous"/> to whoever was deciding and starts the next interval.</summary>
    public static TurnClock Next(TurnClock? previous, int turn, int? deciding, long now)
    {
        var next = new TurnClock { Turn = turn, Running = deciding, UpdatedAt = now };
        if (previous == null) return next;
        next.Reference = previous.Reference;
        next.UpdatedAt = Math.Max(now, previous.UpdatedAt);
        for (var seat = 0; seat < 2; seat++)
        {
            next.SpentInDuel[seat] = previous.DuelTime(seat, now);
            if (previous.Turn == turn) next.SpentThisTurn[seat] = previous.TurnTime(seat, now);
        }
        return next;
    }

    // The serializer writes the clock first on the board (JsonPropertyOrder on DuelSnapshot.Clock).
    private const string BoardPrefix = "{\"Board\":{\"Clock\":{";

    /// <summary>
    /// The clock of a cached board packet, or null from an older one. It reads only the first object: parsing
    /// the whole board on every move cost the server module about a tenth of its throughput.
    /// </summary>
    public static TurnClock? FromBoard(string packet)
    {
        if (!packet.StartsWith(BoardPrefix, StringComparison.Ordinal)) return null;
        // The clock holds only numbers and arrays of numbers, so its first closing brace is its own.
        var start = BoardPrefix.Length - 1;
        var end = packet.IndexOf('}', start);
        return JsonSerializer.Deserialize(packet[start..(end + 1)], DuelJson.Default.TurnClock);
    }

    public long TurnTime(int seat, long now) => SpentThisTurn[seat] + Open(seat, now);
    public long DuelTime(int seat, long now) => SpentInDuel[seat] + Open(seat, now);
    /// <summary>Negative once the seat is past the reference.</summary>
    public long Remaining(int seat, long now) => Reference - TurnTime(seat, now);

    private long Open(int seat, long now) => Running == seat ? Math.Max(0, now - UpdatedAt) : 0;
}

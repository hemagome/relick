using System.Diagnostics;

namespace Relick.Network;

// Server time for the turn clock. The device's wall clock is never used: it may be minutes off.
public sealed partial class LobbyConnection
{
    private (long Micros, long Sampled)? _serverTime;

    /// <summary>The server's time in Unix microseconds, or null until a reply has carried one on this connection.</summary>
    public long? ServerNow => _serverTime is { } time
        ? time.Micros + Stopwatch.GetElapsedTime(time.Sampled).Ticks / TimeSpan.TicksPerMicrosecond
        : null;

    // EnsureDuel answers every connection and reconnection; each play refreshes the sample.
    private void SampleServerTime(long micros) => _serverTime = (micros, Stopwatch.GetTimestamp());
}

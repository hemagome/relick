using Relick.Core;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

/// <summary>Everything the two competitive screens show, taken from the server or handed over by a test.</summary>
public sealed record CompetitionView(
    SeasonInfo? Season = null, StandingInfo? Standing = null, TournamentInfo? Tournament = null, MyTournamentInfo? Mine = null,
    IReadOnlyList<EntrantInfo>? Entrants = null, IReadOnlyList<BracketInfo>? Bracket = null,
    long? Now = null, bool Guest = false, bool Busy = false, string Error = "")
{
    public const byte Signup = 0, Running = 1, Finished = 2, Cancelled = 3;

    public static CompetitionView From(LobbyConnection lobby, bool busy) => new(
        lobby.Season, lobby.Standing, lobby.Tournament, lobby.MyTournament, lobby.Entrants, lobby.Bracket, lobby.ServerNow,
        lobby.Account?.Guest == true, busy || lobby.CompetitionPending || !lobby.Ready,
        lobby.CompetitionError.Length > 0 ? lobby.CompetitionError : lobby.RoomError);

    /// <summary>"12 días", "3 h", "8 min" or "40 s" until a server time.</summary>
    public static string Until(long due, long? now)
    {
        if (now is not { } current) return "";
        var seconds = Math.Max(0, (due - current) / 1_000_000);
        return seconds >= 2 * 86_400 ? $"{seconds / 86_400} días" : seconds >= 2 * 3600 ? $"{seconds / 3600} h"
            : seconds >= 120 ? $"{seconds / 60} min" : $"{seconds} s";
    }

    public (string State, string Detail, bool Calls) RankedCard()
    {
        if (Season is not { } season) return ("", "", false);
        var league = Standing is { } own ? CompetitiveRules.Standing(own.Value, own.Played) : "Juega tu colocación";
        if (!season.Open) return ("Temporada cerrada", Standing is { Place: > 0 } placed ? $"{league} · #{placed.Place}" : season.Name, false);
        var left = Until(season.EndsAt, Now);
        return (league, left.Length > 0 ? $"{season.Name} · quedan {left}" : season.Name, false);
    }

    public (string State, string Detail, bool Calls) TournamentCard()
    {
        if (Tournament is not { } cup) return ("", "", false);
        switch (cup.State)
        {
            case Signup:
                return (Mine != null ? "Ya estás inscrito" : "Inscripción abierta", $"{cup.Name} · {cup.Entered}/{cup.Capacity}", false);
            case Running when Mine is { Match: > 0, Playing: false } mine:
                return (mine.Ready ? "Esperando a tu rival" : "Te toca jugar", $"{cup.Name} · vs {mine.Rival}", !mine.Ready);
            case Running:
                return (Mine is { OutRound: > 0 } out_ ? CompetitiveRules.Placing((int)cup.Rounds, (int)out_.OutRound) : $"Ronda {cup.Round} de {cup.Rounds}", cup.Name, false);
            case Finished:
                return ("Terminado", cup.Champion.Length > 0 ? $"Ganó {cup.Champion}" : $"{cup.Name} · sin campeón", false);
            default:
                return ("Cancelado", cup.Name, false);
        }
    }
}

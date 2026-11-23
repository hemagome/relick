using Relick.Core;

namespace Relick;

// Tapping a player's name, in preparation or in the duel, shows their public profile.
public partial class OnlineArena
{
    /// <summary>Profiles by seat for UI tests, which run without a server.</summary>
    internal Func<int, ProfileView?>? TestProfiles { get; set; }

    private bool ProfilesShown => TestProfiles != null || Connection?.ProfilesAvailable == true;

    /// <summary>A seat's published profile, or the defaults under its name until one arrives.</summary>
    private ProfileView PublicProfile(int seat, string name) =>
        TestProfiles?.Invoke(seat) ?? (Connection?.RoomProfile(seat) is { } info ? ProfileView.From(info) : null) ?? ProfileView.Default(name);

    internal void ShowProfile(int seat, string name, bool own)
    {
        if (GetNodeOrNull("ProfilePopup") != null) return;
        var view = PublicProfile(seat, name);
        if (TestProfiles == null && Connection?.RoomStanding(seat) is { } standing)
            view = view with
            {
                Standing = standing.Rated ? CompetitiveRules.Standing(standing.Value, standing.Played) : "",
                Emblems = string.Join('\n', standing.Emblems.Select(emblem => $"{emblem.Title}: {emblem.Detail}"))
            };
        ProfileCard.Show(this, view, own ? ArenaPresentation.Mint : ArenaPresentation.Coral);
    }
}

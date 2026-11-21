namespace Relick;

// Tapping a player's name, in preparation or in the duel, shows their public profile.
public partial class OnlineArena
{
    /// <summary>Profiles by seat for UI tests, which run without a server.</summary>
    internal Func<int, ProfileView?>? TestProfiles { get; set; }

    private bool ProfilesShown => TestProfiles != null || Connection?.ProfilesAvailable == true;

    internal void ShowProfile(int seat, string name, bool own)
    {
        if (GetNodeOrNull("ProfilePopup") != null) return;
        var view = TestProfiles?.Invoke(seat) ?? (Connection?.RoomProfile(seat) is { } info ? ProfileView.From(info) : null) ?? ProfileView.Default(name);
        ProfileCard.Show(this, view, own ? ArenaPresentation.Mint : ArenaPresentation.Coral);
    }
}

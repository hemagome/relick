using Relick.Network.Types;
using SpacetimeDB;

namespace Relick.Network;

// The public profile: the player's own, and those of both seats while sharing a room. Subscribed apart, like the
// quick search, so a server published before profiles existed still syncs the room and the decks.
public sealed partial class LobbyConnection
{
    /// <summary>The server publishes profiles; without them the lobby hides the profile entry.</summary>
    public bool ProfilesAvailable { get; private set; }
    /// <summary>Changes whenever the player's own profile or a room profile changes, so screens rebuild only then.</summary>
    public int ProfileRevision { get; private set; }
    public bool ProfilePending { get; private set; }
    private string _profileError = "";

    /// <summary>Why the last profile change was refused, reported once.</summary>
    public string TakeProfileError()
    {
        var error = _profileError; _profileError = "";
        return error;
    }

    public ProfileInfo? Profile => _connection?.Db.MyProfile.Iter().SingleOrDefault();

    /// <summary>The profile of a seat in the current room, or null when that seat is empty or belongs to another room.</summary>
    public ProfileInfo? RoomProfile(int seat) =>
        _connection?.Db.MyRoomProfiles.Seat.Find((byte)seat) is { } info && info.RoomCode == Room?.Code ? info : null;

    public void SetProfile(string avatar, string title, string frame, string playmat)
    {
        if (!Ready || !ProfilesAvailable || ProfilePending) return;
        if (Profile is { } current && current.Avatar == avatar && current.Title == title && current.Frame == frame && current.Playmat == playmat) return;
        ProfilePending = true; _profileError = "";
        try { _connection!.Reducers.SetProfile(avatar, title, frame, playmat); }
        catch (Exception) { ProfilePending = false; _profileError = "No se pudo enviar la solicitud. Comprueba tu conexión."; }
    }

    private void SubscribeProfiles(DbConnection connection)
    {
        connection.SubscriptionBuilder().OnApplied(_ => { ProfilesAvailable = true; ProfileRevision++; })
            .OnError((_, _) => ProfilesAvailable = false)
            .Subscribe(["SELECT * FROM my_profile", "SELECT * FROM my_room_profiles"]);
    }

    private void RegisterProfileReplies()
    {
        void Changed() => ProfileRevision++;
        _connection!.Db.MyProfile.OnInsert += (_, _) => Changed();
        _connection.Db.MyProfile.OnUpdate += (_, _, _) => Changed();
        _connection.Db.MyProfile.OnDelete += (_, _) => Changed();
        _connection.Db.MyRoomProfiles.OnInsert += (_, _) => Changed();
        _connection.Db.MyRoomProfiles.OnUpdate += (_, _, _) => Changed();
        _connection.Db.MyRoomProfiles.OnDelete += (_, _) => Changed();
        _connection.Reducers.OnSetProfile += (context, _, _, _, _) =>
        {
            ProfilePending = false;
            _profileError = context.Event.Status switch
            {
                Status.Committed => "",
                Status.Failed(var reason) => reason,
                _ => "El servidor no pudo guardar tu perfil."
            };
            Changed();
        };
        ProfilePending = false; _profileError = ""; Changed();
    }
}

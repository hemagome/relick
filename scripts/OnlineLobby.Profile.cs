using Godot;
using Relick.Core;

namespace Relick;

// The player's own public profile: the framed portrait beside the name opens its editor.
public partial class OnlineLobby
{
    private Button _profileButton = null!;
    private ProfileAvatar _profileAvatar = null!;
    private int _presentedProfileRevision = -1;

    private void PresentProfileButton(bool available)
    {
        _profileButton.Visible = _lobby.ProfilesAvailable;
        _profileButton.Disabled = !available || _lobby.ProfilePending;
        if (_lobby.TakeProfileError() is { Length: > 0 } refused) _localError = refused;
        if (_presentedProfileRevision == _lobby.ProfileRevision) return;
        _presentedProfileRevision = _lobby.ProfileRevision;
        var profile = _lobby.Profile;
        _profileAvatar.Avatar = profile?.Avatar ?? ProfileCatalog.DefaultAvatar;
        _profileAvatar.Frame = profile?.Frame ?? ProfileCatalog.DefaultFrame;
    }

    private void OpenProfile()
    {
        if (_profileButton.Disabled || GetNodeOrNull("ProfileEditor") != null) return;
        _localError = "";
        var current = _lobby.Profile is { } profile ? ProfileView.From(profile) : ProfileView.Default(_name.Text);
        // The field may hold a name not saved yet; the card previews what the rival will read.
        if (_name.Text.Trim().Length > 0) current = current with { Name = _name.Text.Trim() };
        var editor = new ProfileEditor { Current = current, Saved = (avatar, title, frame, playmat) => _lobby.SetProfile(avatar, title, frame, playmat) };
        AddChild(editor);
        MenuDialog.Show(editor);
    }
}

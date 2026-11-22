using Godot;
using Relick.Core;

namespace Relick;

// The player's own profile: what the rival sees, and the name it carries.
public partial class OnlineLobby
{
    private int _presentedProfileRevision = -1;
    private Container _profilePreview = null!;
    private Button _editProfile = null!;

    private Control BuildProfilePage()
    {
        var (page, column) = Page("ProfilePage", "Mi perfil");
        _profilePreview = new MarginContainer { Name = "OwnProfileCard" }; column.AddChild(_profilePreview);

        var identity = SettingsSection(column, "TU NOMBRE");
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); identity.AddChild(row);
        _name = new LineEdit { Name = "PlayerName", PlaceholderText = "¿Cómo te llamas?", Text = "Jugador", MaxLength = 24,
            CustomMinimumSize = new(0, MenuVisuals.TouchTarget), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_name);
        _name.TextSubmitted += _ => { SaveAccountName(); _name.ReleaseFocus(); };
        _name.FocusExited += () => { SaveAccountName(); _presentedProfileRevision = -1; };
        identity.AddChild(Description("Es el nombre que lee tu rival."));

        _editProfile = AddButton(column, "Editar retrato, marco y tapete", OpenProfile);
        _editProfile.Name = "OpenProfile"; _editProfile.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        return page;
    }

    private void PresentProfileEditing(bool available)
    {
        _editProfile.Visible = _lobby.ProfilesAvailable;
        _editProfile.Disabled = !available || _lobby.ProfilePending;
        if (_lobby.TakeProfileError() is { Length: > 0 } refused) _localError = refused;
        if (_page == MenuPage.Profile) PresentProfile();
    }

    private void PresentProfile()
    {
        if (_presentedProfileRevision == _lobby.ProfileRevision) return;
        _presentedProfileRevision = _lobby.ProfileRevision;
        foreach (var child in _profilePreview.GetChildren()) { _profilePreview.RemoveChild(child); child.QueueFree(); }
        _profilePreview.AddChild(new ProfileCard { Name = "ProfileCard", View = OwnProfile(), Accent = RelickVisuals.State.Own });
    }

    private ProfileView OwnProfile()
    {
        var view = _lobby.Profile is { } profile ? ProfileView.From(profile) : ProfileView.Default(_name.Text);
        // The field may hold a name not saved yet; the card previews what the rival will read.
        return _name.Text.Trim().Length > 0 ? view with { Name = _name.Text.Trim() } : view;
    }

    private void OpenProfile()
    {
        if (GetNodeOrNull("ProfileEditor") != null) return;
        _localError = "";
        var editor = new ProfileEditor { Current = OwnProfile(), Saved = (avatar, title, frame, playmat) => _lobby.SetProfile(avatar, title, frame, playmat) };
        AddChild(editor);
        MenuDialog.Show(editor);
    }
}

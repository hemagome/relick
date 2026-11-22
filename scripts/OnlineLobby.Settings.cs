using Godot;

namespace Relick;

// Sound, motion, the table view and the account: one screen instead of a speaker and a menu.
public partial class OnlineLobby
{
    private AudioControls _settingsAudio = null!;
    private CheckButton _reduceMotion = null!, _tableViewSetting = null!;
    private Control _accountSection = null!;
    private Label _accountState = null!;
    private Button _signOut = null!, _deleteAccount = null!;

    private Control BuildSettingsPage()
    {
        var (page, column) = Page("SettingsPage", "Ajustes");

        var sound = SettingsSection(column, "SONIDO");
        _settingsAudio = new AudioControls { Name = "SettingsAudio", Preferences = GetNode<AudioPreferences>("/root/AudioPreferences") };
        sound.AddChild(_settingsAudio);

        var display = SettingsSection(column, "PANTALLA");
        _reduceMotion = Toggle(display, "ReduceMotion", "Reducir animaciones", "Muestra los cambios sin movimiento.", reduced =>
        {
            InterfacePreferences.SetReducedMotion(reduced);
            _matchLink.ReducedMotion = InterfacePreferences.ReducedMotion;
        });
        _tableViewSetting = Toggle(display, "TableViewSetting", "Vista de mesa", "El tablero en perspectiva, en pantallas de escritorio.",
            table => InterfacePreferences.TableView = table);

        var account = SettingsSection(column, "CUENTA");
        _accountSection = (Control)account.GetParent();
        _accountState = Description(""); account.AddChild(_accountState);
        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", 10); actions.AddThemeConstantOverride("v_separation", 10);
        account.AddChild(actions);
        _signOut = AddButton(actions, "Cerrar sesión", SignOut); _signOut.Name = "SignOut";
        _deleteAccount = AddButton(actions, "Eliminar cuenta…", ConfirmDeleteAccount); _deleteAccount.Name = "DeleteAccount";
        _deleteAccount.AddThemeColorOverride("font_color", ArenaPresentation.Coral);
        _accountSection.Visible = false;
        return page;
    }

    private static VBoxContainer SettingsSection(Node parent, string title)
    {
        var panel = Surface(parent, 18);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12); panel.AddChild(column);
        column.AddChild(Caption(title, BoardArt.Muted));
        return column;
    }

    private static CheckButton Toggle(Node parent, string name, string title, string description, Action<bool> changed)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); parent.AddChild(row);
        var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        texts.AddThemeConstantOverride("separation", 2); row.AddChild(texts);
        texts.AddChild(Text(title)); texts.AddChild(Description(description));
        var toggle = new CheckButton { Name = name, AccessibilityName = title, CustomMinimumSize = new(72, MenuVisuals.TouchTarget),
            SizeFlagsVertical = SizeFlags.ShrinkCenter };
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) toggle.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MenuVisuals.Switch(toggle);
        toggle.Toggled += on => changed(on);
        row.AddChild(toggle);
        return toggle;
    }

    private void PresentSettings()
    {
        _settingsAudio.Refresh();
        _reduceMotion.SetPressedNoSignal(InterfacePreferences.ReducedByPlayer);
        _tableViewSetting.SetPressedNoSignal(InterfacePreferences.TableView);
    }

    private void PresentAccountSettings()
    {
        _accountSection.Visible = _auth != null;
        if (_auth == null) return;
        // Leaving the account, either way, waits until no room or search depends on it.
        _signOut.Disabled = _deleteAccount.Disabled = !_lobby.Ready || _lobby.Room != null || _lobby.Searching ||
            _lobby.PendingRoomRequest != Relick.Network.RoomRequest.None || _lobby.AccountPending;
        _accountState.Text = _lobby.Account switch
        {
            { Guest: true } => "Juegas como invitado: no puedes guardar mazos.",
            { Name.Length: > 0 } account => $"Sesión de {account.Name}.",
            _ => ""
        };
    }
}

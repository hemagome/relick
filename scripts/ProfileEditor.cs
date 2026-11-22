using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Edits the player's own profile with a live preview; only the server's answer makes a choice stick.</summary>
public partial class ProfileEditor : ConfirmationDialog
{
    public ProfileView Current { get; init; } = ProfileView.Default(AccountClaims.DefaultName);
    /// <summary>Receives avatar, title, frame and playmat when the player saves.</summary>
    public Action<string, string, string, string>? Saved { get; init; }

    private string _avatar = "", _title = "", _frame = "", _playmat = "";
    private Container _preview = null!;

    public override void _Ready()
    {
        Name = "ProfileEditor"; Title = "Mi perfil"; OkButtonText = "Guardar"; CancelButtonText = "Cancelar";
        _avatar = ProfileCatalog.Avatar(Current.Avatar); _title = ProfileCatalog.Title(Current.Title).Id;
        _frame = ProfileCatalog.Frame(Current.Frame).Id; _playmat = ProfileCatalog.Playmat(Current.Playmat).Id;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        MenuDialog.Fill(this, column, 560);
        _preview = new MarginContainer { Name = "ProfilePreview" }; column.AddChild(_preview);

        var avatars = Section(column, "RETRATO", "ProfileAvatars");
        Choices(avatars, ProfileCatalog.Avatars.Select(id => (id, RelikarioCatalog.Find(id)!.DisplayName)), () => _avatar, id => _avatar = id,
            (button, id) =>
            {
                button.Icon = RelikarioArt.Thumbnail(id); button.ExpandIcon = true;
                button.IconAlignment = HorizontalAlignment.Center; button.VerticalIconAlignment = VerticalAlignment.Top;
                button.CustomMinimumSize = new(96, 112);
            });

        var titles = Section(column, "TÍTULO", "ProfileTitles");
        var picker = new OptionButton { Name = "ProfileTitlePicker", CustomMinimumSize = new(0, 48), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        for (var i = 0; i < ProfileCatalog.Titles.Count; i++)
        {
            picker.AddItem(ProfileCatalog.Titles[i].Name, i);
            if (ProfileCatalog.Titles[i].Id == _title) picker.Select(i);
        }
        picker.ItemSelected += index => { _title = ProfileCatalog.Titles[(int)index].Id; Refresh(); };
        ArenaViewport.TouchMenu(picker.GetPopup());
        titles.AddChild(picker);

        var frames = Section(column, "MARCO", "ProfileFrames");
        Choices(frames, ProfileCatalog.Frames.Select(f => (f.Id, f.Name)), () => _frame, id => _frame = id,
            (button, id) => Swatch(button, new Color(ProfileCatalog.Frame(id).Edge)));

        var playmats = Section(column, "TAPETE · se ve en tus duelos", "ProfilePlaymats");
        Choices(playmats, ProfileCatalog.Playmats.Select(m => (m.Id, m.Name)), () => _playmat, id => _playmat = id,
            (button, id) => Swatch(button, new Color(ProfileCatalog.Playmat(id).Trace)));

        Confirmed += () => Saved?.Invoke(_avatar, _title, _frame, _playmat);
        VisibilityChanged += () => { if (!Visible) QueueFree(); };
        Refresh();
    }

    private void Refresh()
    {
        foreach (var child in _preview.GetChildren()) { _preview.RemoveChild(child); child.QueueFree(); }
        _preview.AddChild(new ProfileCard { Name = "ProfilePreviewCard", Accent = RelickVisuals.State.Own,
            View = Current with { Avatar = _avatar, Title = _title, Frame = _frame, Playmat = _playmat } });
    }

    private static Container Section(Container parent, string caption, string name)
    {
        var label = ArenaPresentation.Text(caption, MenuVisuals.Type.Caption, BoardArt.Muted); parent.AddChild(label);
        var flow = new HFlowContainer { Name = name }; flow.AddThemeConstantOverride("h_separation", 8); flow.AddThemeConstantOverride("v_separation", 8);
        parent.AddChild(flow);
        return flow;
    }

    private void Choices(Container parent, IEnumerable<(string Id, string Name)> options, Func<string> current, Action<string> choose,
        Action<Button, string> decorate)
    {
        var group = new ButtonGroup();
        foreach (var (id, label) in options)
        {
            var button = new Button { Name = id, Text = label, ToggleMode = true, ButtonGroup = group, ButtonPressed = id == current(),
                CustomMinimumSize = new(0, MenuVisuals.TouchTarget), TooltipText = label };
            decorate(button, id);
            button.Pressed += () => { choose(id); Refresh(); };
            parent.AddChild(button);
        }
    }

    private static void Swatch(Button button, Color color)
    {
        var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        image.Fill(color);
        button.Icon = ImageTexture.CreateFromImage(image);
    }
}

using Godot;
using Relick.Core;

namespace Relick;

/// <summary>
/// A duelist's public identity in a corner of the board, as card games show their players beside
/// the hands: framed portrait, name and title, on the colour of the chosen frame. The name opens
/// the full profile when the server publishes profiles.
/// </summary>
public partial class ProfilePlate : PanelContainer
{
    public ProfileView View { get; init; } = ProfileView.Default(AccountClaims.DefaultName);
    public Color Accent { get; init; } = RelickVisuals.State.Own;
    public int Index { get; init; }
    /// <summary>The rival's plate fits the thin band above its field: portrait, name and title in one row.</summary>
    public bool Slim { get; init; }
    public Action? Opened { get; init; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var frame = ProfileCatalog.Frame(View.Frame);
        var box = RelickVisuals.Frame(BoardArt.Tint(RelickVisuals.Playmat.Surface, 0.9f), BoardArt.Tint(new Color(frame.Edge), 0.85f), 10, 2);
        box.ContentMarginLeft = box.ContentMarginRight = Slim ? 8 : 12;
        box.ContentMarginTop = box.ContentMarginBottom = Slim ? 3 : 8;
        AddThemeStyleboxOverride("panel", box);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Slim ? 8 : 12);
        AddChild(row);
        var side = Slim ? 28 : 78;
        row.AddChild(new ProfileAvatar { Name = $"PlateAvatar{Index}", Avatar = View.Avatar, Frame = View.Frame,
            CustomMinimumSize = new(side, side), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var text = Slim ? (BoxContainer)new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin }
            : new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.MouseFilter = MouseFilterEnum.Ignore;
        text.AddThemeConstantOverride("separation", Slim ? 10 : 2);
        row.AddChild(text);
        var name = ArenaPresentation.Heading(View.Name, Slim ? 16 : 22, Accent);
        name.Name = $"PlayerName{Index}"; name.ClipText = !Slim; name.TooltipText = View.Name;
        name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        // In one row the name gives way to the title only past its own width.
        if (Slim) name.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        text.AddChild(name);
        ArenaPresentation.OpensProfile(name, View.Name, Opened);
        var title = ArenaPresentation.Text(ProfileCatalog.Title(View.Title).Name.ToUpperInvariant(), Slim ? 11 : 12, BoardArt.Gold);
        title.Name = $"PlayerTitle{Index}"; title.ClipText = true;
        if (Slim) title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        text.AddChild(title);
    }
}

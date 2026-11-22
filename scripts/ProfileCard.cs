using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

/// <summary>What a profile card shows, independent of where it came from (the server or a preview).</summary>
public sealed record ProfileView(string Name, string Avatar, string Title, string Frame, string Playmat,
    uint Wins = 0, uint Losses = 0, uint Draws = 0, string Favorite = "", string Standing = "", string Emblems = "")
{
    public uint Played => Wins + Losses + Draws;

    public static ProfileView From(ProfileInfo info) =>
        new(info.Name, info.Avatar, info.Title, info.Frame, info.Playmat, info.Wins, info.Losses, info.Draws, info.Favorite);

    /// <summary>A player whose profile has not arrived: the defaults under their name.</summary>
    public static ProfileView Default(string name) =>
        new(name, ProfileCatalog.DefaultAvatar, ProfileCatalog.DefaultTitle, ProfileCatalog.DefaultFrame, ProfileCatalog.DefaultPlaymat);

    /// <summary>"12 V · 8 D · 1 E · 57 %", or a note before the first finished duel.</summary>
    public string Record()
    {
        if (Played == 0) return "Sin duelos terminados todavía";
        var draws = Draws > 0 ? $" · {Draws} E" : "";
        return $"{Wins} V · {Losses} D{draws} · {ProfileCatalog.WinRate(Wins, Played)} % de victorias";
    }
}

public static class ProfilePlaymats
{
    public static RelickPlaymat Of(string? id)
    {
        var mat = ProfileCatalog.Playmat(id);
        return mat.Id == ProfileCatalog.DefaultPlaymat ? RelickPlaymat.Classic
            : new(new(mat.Background), new(mat.Surface), new(mat.Raised), new(mat.Frame), new(mat.Trace));
    }
}

/// <summary>A Relikario portrait inside the profile frame.</summary>
public partial class ProfileAvatar : Control
{
    private string _avatar = ProfileCatalog.DefaultAvatar, _frame = ProfileCatalog.DefaultFrame;
    private Texture2D? _texture;

    public string Avatar
    {
        get => _avatar;
        set { _avatar = ProfileCatalog.Avatar(value); _texture = RelikarioArt.Thumbnail(_avatar); QueueRedraw(); }
    }

    public string Frame
    {
        get => _frame;
        set { _frame = ProfileCatalog.Frame(value).Id; QueueRedraw(); }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _texture ??= RelikarioArt.Thumbnail(_avatar);
        AccessibilityName = $"Retrato de {RelikarioCatalog.NameOf(_avatar)}";
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var frame = ProfileCatalog.Frame(_frame);
        var side = Math.Min(Size.X, Size.Y);
        var rect = new Rect2((Size - new Vector2(side, side)) / 2, new Vector2(side, side));
        var edge = new Color(frame.Edge);
        if (frame.Glow != null) RelickVisuals.Shadow(this, rect, BoardArt.Tint(new Color(frame.Glow), 0.45f), 10, 10);
        DrawRect(rect, RelickVisuals.Playmat.Background);
        if (_texture != null)
        {
            // Cover the square, cropping the longer side around the face at the top third.
            var size = _texture.GetSize();
            var crop = Math.Min(size.X, size.Y);
            var source = new Rect2((size.X - crop) / 2, (size.Y - crop) / 3, crop, crop);
            DrawTextureRectRegion(_texture, rect.Grow(-3), source);
        }
        DrawRect(rect.Grow(-1.5f), edge, false, 3);
    }
}

/// <summary>The public profile: framed portrait, name, title, record and favourite Relikario.</summary>
public partial class ProfileCard : PanelContainer
{
    public ProfileView View { get; init; } = ProfileView.Default(AccountClaims.DefaultName);
    public Color Accent { get; init; } = RelickVisuals.State.Own;

    public override void _Ready()
    {
        var view = View;
        AddThemeStyleboxOverride("panel", Padded(RelickVisuals.Surface(new Color(ProfileCatalog.Frame(view.Frame).Edge)), 18));
        CustomMinimumSize = new(340, 0);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); AddChild(row);
        row.AddChild(new ProfileAvatar { Name = "ProfileAvatar", Avatar = view.Avatar, Frame = view.Frame, CustomMinimumSize = new(96, 96),
            SizeFlagsVertical = SizeFlags.ShrinkBegin });
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 4); row.AddChild(column);
        var name = ArenaPresentation.Text(view.Name, MenuVisuals.Type.Subtitle, Accent);
        name.Name = "ProfileName"; name.AddThemeFontOverride("font", RelickVisuals.Display); name.ClipText = true;
        column.AddChild(name);
        var title = ArenaPresentation.Text(ProfileCatalog.Title(view.Title).Name.ToUpperInvariant(), MenuVisuals.Type.Caption, BoardArt.Gold);
        title.Name = "ProfileTitle"; column.AddChild(title);
        var record = ArenaPresentation.Text(view.Record(), MenuVisuals.Type.Body, BoardArt.Ink);
        // One line: a wrapping label would report its height for zero width and stretch the popup.
        record.Name = "ProfileRecord"; column.AddChild(record);
        if (RelikarioCatalog.Find(view.Favorite) is { } favorite)
        {
            var line = new HBoxContainer { Name = "ProfileFavorite" }; line.AddThemeConstantOverride("separation", 6); column.AddChild(line);
            if (RelikarioArt.Thumbnail(favorite.Id) is { } thumbnail)
                line.AddChild(new TextureRect { Texture = thumbnail, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, CustomMinimumSize = new(22, 22) });
            line.AddChild(ArenaPresentation.Text($"Favorito: {favorite.DisplayName}", MenuVisuals.Type.Caption, BoardArt.Muted));
        }
        if (view.Standing.Length > 0)
        {
            var league = ArenaPresentation.Text($"Clasificatoria: {view.Standing}", MenuVisuals.Type.Caption, BoardArt.Gold);
            league.Name = "ProfileStanding"; column.AddChild(league);
        }
        // One emblem per line, for the reason given above.
        foreach (var emblem in view.Emblems.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 6); column.AddChild(line);
            line.AddChild(new IconView { Icon = GameIcon.Trophy, Color = BoardArt.Gold, CustomMinimumSize = new(14, 14), SizeFlagsVertical = SizeFlags.ShrinkCenter });
            line.AddChild(ArenaPresentation.Text(emblem, MenuVisuals.Type.Caption, MenuVisuals.Tone.Reading));
        }
        var mat = ProfileCatalog.Playmat(view.Playmat);
        var playmat = new HBoxContainer { Name = "ProfilePlaymat" }; playmat.AddThemeConstantOverride("separation", 6); column.AddChild(playmat);
        playmat.AddChild(new ColorRect { Color = new Color(mat.Trace), CustomMinimumSize = new(14, 14), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        playmat.AddChild(ArenaPresentation.Text($"Tapete: {mat.Name}", MenuVisuals.Type.Caption, BoardArt.Muted));
    }

    private static StyleBox Padded(StyleBox box, int padding)
    {
        box.ContentMarginLeft = box.ContentMarginRight = box.ContentMarginTop = box.ContentMarginBottom = padding;
        return box;
    }

    /// <summary>Shows a card over the screen; any click outside closes it.</summary>
    public static PopupPanel Show(Node host, ProfileView view, Color accent)
    {
        var popup = new PopupPanel { Name = "ProfilePopup", Transparent = true, TransparentBg = true };
        popup.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var card = new ProfileCard { Name = "ProfileCard", View = view, Accent = accent };
        popup.AddChild(card);
        popup.PopupHide += popup.QueueFree;
        host.AddChild(popup);
        // The card is built in _Ready, so its size is known only now; the window hugs it.
        popup.PopupCentered((Vector2I)card.GetCombinedMinimumSize().Ceil());
        return popup;
    }
}

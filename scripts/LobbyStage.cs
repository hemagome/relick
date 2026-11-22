using Godot;
using Relick.Core;

namespace Relick;

/// <summary>The player's Relikario, large: the printed card over the logo's chevrons.</summary>
public partial class RelikarioHero : Control
{
    private string _relikario = "";
    private Texture2D? _card;

    public string Relikario
    {
        get => _relikario;
        set
        {
            if (_relikario == value) return;
            _relikario = value;
            _card = RelikarioArt.Card(value, 1) ?? RelikarioArt.Portrait(value);
            AccessibilityName = RelikarioCatalog.Find(value)?.DisplayName ?? "";
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        if (Size.X < 20 || Size.Y < 20) return;
        var h = Size.Y * 0.8f; var w = Math.Min(Size.X * 0.36f, h * 0.45f);
        for (var i = 0; i < 2; i++)
        {
            var color = i == 0 ? RelickVisuals.State.Target : RelickVisuals.State.Rival;
            var x = Size.X - w * (1.85f - i * 0.85f); var y = (Size.Y - h) / 2;
            DrawColoredPolygon([new(x, y), new(x + w * 0.38f, y), new(x + w, y + h / 2), new(x + w * 0.38f, y + h), new(x, y + h), new(x + w * 0.62f, y + h / 2)],
                BoardArt.Tint(color, 0.10f));
        }
        if (_card == null) return;
        // The tilt needs a margin, or the corners leave the stage.
        var area = new Rect2(-Size / 2, Size).GrowIndividual(-Size.X * 0.05f, -Size.Y * 0.04f, -Size.X * 0.05f, -Size.Y * 0.04f);
        var rect = BoardArt.FitArtwork(_card, area);
        DrawSetTransform(Size / 2, Mathf.DegToRad(-2.5f));
        RelickVisuals.Shadow(this, rect, new Color(0, 0, 0, 0.5f), 16, MenuVisuals.Cut.Panel);
        DrawTextureRect(_card, rect, false);
        DrawSetTransform(Vector2.Zero);
    }
}

/// <summary>One side of a match that has not started: portrait, name and title, or a silhouette still waiting.</summary>
public partial class SeatCard : PanelContainer
{
    private ProfileAvatar _avatar = null!;
    private IconView _empty = null!;
    private Label _name = null!, _title = null!;
    private Control _face = null!;
    private StyleBoxFlat _style = null!;
    private string _shown = "";
    public Color Accent { get; init; } = RelickVisuals.State.Own;

    public override void _Ready()
    {
        _style = RelickVisuals.Frame(MenuVisuals.Tone.Surface, BoardArt.Tint(Accent, 0.6f), MenuVisuals.Cut.Panel);
        AddThemeStyleboxOverride("panel", _style);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); AddChild(row);
        _face = new Control { CustomMinimumSize = new(88, 88), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(_face);
        _avatar = new ProfileAvatar { MouseFilter = MouseFilterEnum.Ignore }; _face.AddChild(_avatar);
        _avatar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _empty = new IconView { Icon = GameIcon.Person, Color = MenuVisuals.Tone.Edge }; _face.AddChild(_empty);
        _empty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 12);
        var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        texts.AddThemeConstantOverride("separation", 2); row.AddChild(texts);
        _name = ArenaPresentation.Text("", MenuVisuals.Type.Subtitle); _name.Name = "SeatName";
        _title = ArenaPresentation.Text("", MenuVisuals.Type.Caption, BoardArt.Muted); _title.Name = "SeatTitle";
        _name.AutowrapMode = _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _name.MaxLinesVisible = 2;
        texts.AddChild(_name); texts.AddChild(_title);
    }

    public void Present(string name, string detail, string? avatar, string? frame, bool compact)
    {
        var key = $"{name}\n{detail}\n{avatar}\n{frame}\n{compact}";
        if (key == _shown) return;
        _shown = key;
        _face.CustomMinimumSize = compact ? new(56, 56) : new(88, 88);
        _name.AddThemeFontSizeOverride("font_size", compact ? MenuVisuals.Type.Control : MenuVisuals.Type.Subtitle);
        _avatar.Visible = avatar != null; _empty.Visible = avatar == null;
        if (avatar != null) { _avatar.Avatar = avatar; _avatar.Frame = frame ?? ProfileCatalog.DefaultFrame; }
        _name.Text = name; _title.Text = detail; _title.Visible = detail.Length > 0;
        _style.BorderColor = avatar == null ? MenuVisuals.Tone.Edge : BoardArt.Tint(Accent, 0.6f);
        _name.AddThemeColorOverride("font_color", avatar == null ? BoardArt.Muted : BoardArt.Ink);
    }
}

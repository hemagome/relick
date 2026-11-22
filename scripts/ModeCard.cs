using Godot;

namespace Relick;

/// <summary>A mode that exists: pressable, with a solid outline, in the place its planned slot held.</summary>
public partial class ModeCard : Button
{
    public GameIcon Symbol { get; init; }
    public string Title { get; init; } = "";
    private LobbySymbol _symbol = null!;
    private Label _title = null!, _state = null!, _detail = null!;
    private VBoxContainer _texts = null!;
    private HBoxContainer _row = null!;
    private bool _compact;
    private string _shown = "";

    public bool Compact
    {
        get => _compact;
        set { _compact = value; if (IsNodeReady()) Arrange(); }
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _row.AddThemeConstantOverride("separation", 10); AddChild(_row);
        _symbol = new LobbySymbol { Icon = Symbol, Accent = RelickVisuals.State.Target, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _row.AddChild(_symbol);
        _texts = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _texts.AddThemeConstantOverride("separation", 2); _row.AddChild(_texts);
        _title = ArenaPresentation.Text(Title, MenuVisuals.Type.Subtitle); _title.Name = "ModeTitle";
        _state = ArenaPresentation.Text("", MenuVisuals.Type.Caption); _state.Name = "ModeState";
        _detail = ArenaPresentation.Text("", MenuVisuals.Type.Body, BoardArt.Muted); _detail.Name = "ModeDetail";
        foreach (var label in new[] { _title, _state, _detail })
        {
            label.MouseFilter = MouseFilterEnum.Ignore; label.ClipText = true;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _texts.AddChild(label);
        }
        Arrange();
        Present("", "", false);
    }

    private void Arrange()
    {
        _title.AddThemeFontSizeOverride("font_size", _compact ? MenuVisuals.Type.Body : MenuVisuals.Type.Subtitle);
        _detail.Visible = !_compact;
        _symbol.CustomMinimumSize = _compact ? new(28, 28) : new(44, 44);
        _symbol.Visible = !_compact || GetViewportRect().Size.X >= 800;
        CustomMinimumSize = new(0, _compact ? 50 : 128);
        _row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, _compact ? 8 : 16);
    }

    /// <summary><paramref name="calls"/> lights the card when the player is expected there.</summary>
    public void Present(string state, string detail, bool calls)
    {
        var key = $"{state}\n{detail}\n{calls}";
        if (key == _shown) return;
        _shown = key;
        _state.Text = state; _detail.Text = detail;
        _state.AddThemeColorOverride("font_color", calls ? BoardArt.Gold : RelickVisuals.State.Target);
        foreach (var state_ in new[] { "normal", "hover", "pressed", "disabled" })
            AddThemeStyleboxOverride(state_, RelickVisuals.Frame(state_ == "hover" ? MenuVisuals.Tone.Raised : MenuVisuals.Tone.Surface,
                calls ? BoardArt.Gold : MenuVisuals.Tone.Edge, MenuVisuals.Cut.Panel, calls ? 2 : 1));
        TooltipText = detail;
        AccessibilityName = $"{Title}. {state}. {detail}";
    }
}

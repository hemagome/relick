using Godot;

namespace Relick;

// Modes the server has not opened: visible in the lobby, with nothing to press, focus or send. OnlineLobby.Competitive swaps in their cards.
public partial class OnlineLobby
{
    private HBoxContainer _plannedModes = null!;
    private readonly List<PlannedModeSlot> _plannedSlots = [];

    private void BuildPlannedModes(Node parent)
    {
        _plannedModes = new HBoxContainer { Name = "PlannedModes", MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(_plannedModes);
        AddPlannedMode("RankedSlot", GameIcon.Ranking, "Clasificatoria ELO", "Duelos con puntuación contra rivales de tu nivel.");
        AddPlannedMode("TournamentSlot", GameIcon.Trophy, "Torneo", "Competencias por rondas entre varios duelistas.");
        BuildModeCards();
    }

    private void AddPlannedMode(string name, GameIcon icon, string title, string description)
    {
        var slot = new PlannedModeSlot { Name = name, Icon = icon, Title = title, Description = description };
        _plannedModes.AddChild(slot);
        _plannedSlots.Add(slot);
    }
}

/// <summary>A reserved place for a mode that does not exist yet. It is deliberately not a button.</summary>
public partial class PlannedModeSlot : PanelContainer
{
    internal const string Badge = "Próximamente";
    public GameIcon Icon { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";

    private readonly StyleBoxEmpty _padding = new();
    private LobbySymbol _symbol = null!;
    private Label _title = null!, _description = null!;
    private bool _compact;

    public bool Compact
    {
        get => _compact;
        set { _compact = value; if (IsNodeReady()) Arrange(); }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AccessibilityName = $"{Title}. {Badge}, todavía no disponible. {Description}";
        AddThemeStyleboxOverride("panel", _padding);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        AddChild(row);
        _symbol = new LobbySymbol { Icon = Icon, Accent = BoardArt.Muted, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(_symbol);

        var texts = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center
        };
        texts.AddThemeConstantOverride("separation", 2);
        row.AddChild(texts);
        _title = ArenaPresentation.Text(Title, MenuVisuals.Type.Subtitle, BoardArt.Muted);
        _title.Name = "ModeTitle";
        texts.AddChild(_title);
        _description = ArenaPresentation.Text(Description, MenuVisuals.Type.Body, BoardArt.Muted);
        _description.Name = "ModeDescription";
        _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        texts.AddChild(_description);

        // Two slots share a column, so the badge sits under the name instead of squeezing it.
        var soon = ArenaPresentation.Text(Badge, MenuVisuals.Type.Caption, BoardArt.Ink);
        soon.Name = "Soon";
        soon.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        var plate = RelickVisuals.Frame(MenuVisuals.Tone.Background, MenuVisuals.Tone.Edge, MenuVisuals.Cut.Badge);
        plate.ContentMarginLeft = plate.ContentMarginRight = 8;
        plate.ContentMarginTop = plate.ContentMarginBottom = 2;
        soon.AddThemeStyleboxOverride("normal", plate);
        texts.AddChild(soon);
        _title.ClipText = true;
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        texts.MoveChild(soon, 1);
        foreach (var label in new[] { _title, _description, soon }) label.MouseFilter = MouseFilterEnum.Ignore;

        Resized += QueueRedraw;
        Arrange();
    }

    private void Arrange()
    {
        _description.Visible = !_compact;
        _title.AddThemeFontSizeOverride("font_size", _compact ? MenuVisuals.Type.Body : MenuVisuals.Type.Subtitle);
        _symbol.CustomMinimumSize = _compact ? new(28, 28) : new(44, 44);
        _symbol.Visible = !_compact || GetViewportRect().Size.X >= 800;
        _padding.ContentMarginLeft = _padding.ContentMarginRight = _compact ? 8 : 16;
        _padding.ContentMarginTop = _padding.ContentMarginBottom = _compact ? 4 : 14;
        CustomMinimumSize = new(0, _compact ? 50 : 128);
    }

    public override void _Draw()
    {
        // A broken outline reads as a reserved place; the modes that work have a solid one.
        var points = BoardArt.Octagon(new Rect2(Vector2.Zero, Size).Grow(-1), MenuVisuals.Cut.Panel, MenuVisuals.Cut.Panel);
        DrawColoredPolygon(points, BoardArt.Tint(MenuVisuals.Tone.Surface, 0.72f));
        for (var i = 0; i < points.Length; i++)
            DrawDashedLine(points[i], points[(i + 1) % points.Length], MenuVisuals.Tone.Edge, 1, 6);
    }
}

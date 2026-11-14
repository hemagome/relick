using Godot;
using Relick.Core;

namespace Relick;

/// <summary>First step of a new deck: choose the Relikario it is built for. Only programmed Relikarios can be chosen;
/// the rest of the roster is listed as coming soon. The detail panel shows the Relikario's card and turns it to its next level.</summary>
public partial class RelikarioPicker : VBoxContainer
{
    public bool ShortScreen { get; init; }
    public event Action<string>? Chosen;
    public event Action? Closed;

    internal const double AutoSeconds = 6, PauseSeconds = 15;
    private const float TurnSeconds = 0.22f;

    private readonly Dictionary<string, Button> _tiles = [];
    private GridContainer _grid = null!;
    private ScrollContainer _gallery = null!;
    private PanelContainer _detail = null!;
    private Label _count = null!;
    private bool _availableOnly;
    private RelikarioEntry _selected = RelikarioCatalog.Roster[0];
    private int _level = 1;
    // Detail controls of the selected Relikario, rebuilt on each selection.
    private TextureRect? _card;
    private ProgressBar? _progress;
    private Button[] _levels = [];
    private Tween? _turn;
    private double _elapsed, _paused;
    private bool _reducedMotion;

    internal RelikarioEntry Selected => _selected;
    internal int Level => _level;
    internal IReadOnlyDictionary<string, Button> Tiles => _tiles;
    private float TileWidth => ShortScreen ? 96 : 132;

    /// <summary>The square portrait shown on a Relikario's tile, or null while it has no art.</summary>
    internal static Texture2D? Portrait(string relikario) =>
        relikario == RelikarioCatalog.Yuno.Id ? GD.Load<Texture2D>("res://assets/characters/yuno/portrait.png") : null;

    /// <summary>The printed Relikario card at a level, or null when it has none.</summary>
    internal static Texture2D? Card(string relikario, int level) =>
        relikario == RelikarioCatalog.Yuno.Id && level is 1 or 2 ? GD.Load<Texture2D>($"res://assets/characters/yuno/card-lv{level}.png") : null;

    /// <summary>Initials that stand in for a portrait: "Yuno 8 bits" is Y8B, "Zii Jay" is ZJ.</summary>
    internal static string Monogram(string name)
    {
        var text = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => word.Any(char.IsDigit) ? word : word[..1])).ToUpperInvariant();
        return text.Length > 3 ? text[..3] : text;
    }

    public override void _Ready()
    {
        Name = "RelikarioPicker";
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", ShortScreen ? 8 : 14);
        _reducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
            ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool();

        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 16); AddChild(header);
        var back = ArenaPresentation.Button(header, "← Mazos", () => Closed?.Invoke());
        back.Name = "PickerBack"; back.CustomMinimumSize = new(120, 48);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; titles.AddThemeConstantOverride("separation", 0); header.AddChild(titles);
        var title = ArenaPresentation.Text("Nuevo mazo", ShortScreen ? 26 : 36); title.AddThemeFontOverride("font", RelickVisuals.Display); titles.AddChild(title);
        if (!ShortScreen)
        {
            var subtitle = ArenaPresentation.Text("Elige el Relikario del mazo. Sus habilidades específicas solo funcionan con él.", 15, BoardArt.Muted);
            subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart; titles.AddChild(subtitle);
        }

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", ShortScreen ? 10 : 16); AddChild(body);
        var roster = Panel("RosterPanel"); roster.SizeFlagsHorizontal = SizeFlags.ExpandFill; body.AddChild(roster);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); roster.AddChild(column);
        var filters = new HBoxContainer { Name = "RosterFilters", Visible = !ShortScreen }; filters.AddThemeConstantOverride("separation", 8); column.AddChild(filters);
        var group = new ButtonGroup();
        foreach (var (text, only) in new[] { ("Todos", false), ("Disponibles", true) })
        {
            var filter = new Button { Name = only ? "FilterAvailable" : "FilterAll", Text = text, ToggleMode = true, ButtonGroup = group, ButtonPressed = !only, CustomMinimumSize = new(0, 40) };
            filter.Pressed += () => { _availableOnly = only; ApplyFilter(); };
            filters.AddChild(filter);
        }
        filters.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _count = ArenaPresentation.Text($"{RelikarioCatalog.Roster.Count(r => r.Playable)} de {RelikarioCatalog.Roster.Count} disponibles", 13, BoardArt.Muted);
        _count.Name = "RosterCount"; _count.SizeFlagsVertical = SizeFlags.ShrinkCenter; filters.AddChild(_count);
        _gallery = new ScrollContainer { Name = "RosterGallery", SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        column.AddChild(_gallery);
        _grid = new GridContainer { Name = "RosterGrid", Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", ShortScreen ? 8 : 10); _grid.AddThemeConstantOverride("v_separation", ShortScreen ? 8 : 10);
        _gallery.AddChild(_grid);
        foreach (var entry in RelikarioCatalog.Roster) AddTile(entry);
        _gallery.Resized += () => CallDeferred(nameof(UpdateColumns));

        _detail = Panel("RelikarioDetail");
        _detail.CustomMinimumSize = new(380, 0);
        body.AddChild(_detail);
        Select(_selected);
        CallDeferred(nameof(UpdateColumns));
        _tiles[_selected.Id].CallDeferred(Control.MethodName.GrabFocus);
    }

    private static PanelContainer Panel(string name)
    {
        var panel = new PanelContainer { Name = name, SizeFlagsVertical = SizeFlags.ExpandFill };
        var frame = RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 8);
        frame.ContentMarginLeft = frame.ContentMarginRight = 14; frame.ContentMarginTop = frame.ContentMarginBottom = 12;
        panel.AddThemeStyleboxOverride("panel", frame);
        return panel;
    }

    private void AddTile(RelikarioEntry entry)
    {
        var tile = new Button { Name = "Tile_" + entry.Id, CustomMinimumSize = new(TileWidth, 0), TooltipText = entry.Playable ? entry.Name : $"{entry.Name} · Próximamente",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseDefaultCursorShape = CursorShape.PointingHand };
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; content.AddThemeConstantOverride("separation", 2);
        tile.AddChild(content); content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var plate = new PanelContainer { Name = "Art", MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        plate.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new("0e1529"), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6 });
        content.AddChild(plate);
        if (Portrait(entry.Id) is { } portrait)
            plate.AddChild(new TextureRect { Name = "Portrait", Texture = portrait, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
        else
        {
            var mono = ArenaPresentation.Text(Monogram(entry.Name), ShortScreen ? 24 : 34, new Color("3b4a74"));
            mono.HorizontalAlignment = HorizontalAlignment.Center; mono.VerticalAlignment = VerticalAlignment.Center;
            mono.AddThemeFontOverride("font", RelickVisuals.Strong); plate.AddChild(mono);
        }
        if (!entry.Playable)
        {
            var soon = ArenaPresentation.Text("Próximamente", ShortScreen ? 10 : 11, BoardArt.Muted);
            soon.Name = "Soon"; soon.MouseFilter = MouseFilterEnum.Ignore;
            soon.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; soon.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            var badge = RelickVisuals.Frame(new Color("0b1020cc"), RelickVisuals.Playmat.Frame, 4);
            badge.ContentMarginLeft = badge.ContentMarginRight = 6; badge.ContentMarginTop = badge.ContentMarginBottom = 1;
            soon.AddThemeStyleboxOverride("normal", badge);
            var corner = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            corner.AddThemeConstantOverride("margin_top", 6); corner.AddThemeConstantOverride("margin_right", 6);
            corner.AddChild(soon); plate.AddChild(corner);
            plate.Modulate = new Color(1, 1, 1, 0.55f);
        }
        var name = ArenaPresentation.Text(entry.Name, ShortScreen ? 13 : 15, entry.Playable ? BoardArt.Ink : BoardArt.Muted);
        name.Name = "TileName"; name.ClipText = true; name.MouseFilter = MouseFilterEnum.Ignore; name.AddThemeFontOverride("font", RelickVisuals.Strong);
        var labels = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        labels.AddThemeConstantOverride("margin_left", 8); labels.AddThemeConstantOverride("margin_right", 8); labels.AddThemeConstantOverride("margin_bottom", 6);
        var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; stack.AddThemeConstantOverride("separation", 0); labels.AddChild(stack);
        // «Próximamente» on the art, the filter and the «1 de 18 disponibles» count already tell each tile's state.
        stack.AddChild(name);
        content.AddChild(labels);
        // A button does not measure its children: the art is a square as wide as the tile, with the labels below it.
        void Fit()
        {
            plate.CustomMinimumSize = new(0, tile.Size.X);
            tile.CustomMinimumSize = new(TileWidth, tile.Size.X + 2 + labels.GetCombinedMinimumSize().Y);
        }
        tile.Resized += Fit;
        tile.CustomMinimumSize = new(TileWidth, TileWidth + 40);
        tile.Pressed += () => Select(entry);
        _tiles[entry.Id] = tile;
        _grid.AddChild(tile);
    }

    private static void StyleTile(Button tile, RelikarioEntry entry, bool selected)
    {
        var edge = selected ? RelickVisuals.State.Selected : RelickVisuals.Playmat.Frame;
        foreach (var (state, fill) in new[] { ("normal", RelickVisuals.Playmat.Raised), ("hover", RelickVisuals.Playmat.Raised.Lightened(0.06f)),
                     ("pressed", RelickVisuals.Playmat.Raised.Darkened(0.1f)), ("focus", new Color(0, 0, 0, 0)) })
        {
            var style = RelickVisuals.Frame(state == "focus" ? fill : selected ? new Color("2a2a3e") : fill,
                state == "focus" ? RelickVisuals.State.Focus : state == "hover" && !selected ? BoardArt.Muted : edge, 8, selected || state == "focus" ? 2 : 1);
            style.DrawCenter = state != "focus";
            style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 0;
            tile.AddThemeStyleboxOverride(state, style);
        }
        if (tile.FindChild("TileName", true, false) is Label name)
            name.AddThemeColorOverride("font_color", selected ? RelickVisuals.State.Selected : entry.Playable ? BoardArt.Ink : BoardArt.Muted);
    }

    private void UpdateColumns()
    {
        var width = _gallery.Size.X - 14; // Leave room for the vertical scrollbar.
        var gap = ShortScreen ? 8 : 10;
        _grid.Columns = Math.Clamp((int)((width + gap) / (TileWidth + gap)), 1, 8);
    }

    private void ApplyFilter()
    {
        foreach (var entry in RelikarioCatalog.Roster) _tiles[entry.Id].Visible = entry.Playable || !_availableOnly;
        if (_availableOnly && !_selected.Playable) Select(RelikarioCatalog.Roster.First(r => r.Playable));
    }

    internal void Select(RelikarioEntry entry)
    {
        _selected = entry; _level = 1; _elapsed = 0; _paused = 0;
        _turn?.Kill(); _turn = null;
        foreach (var other in RelikarioCatalog.Roster) StyleTile(_tiles[other.Id], other, other.Id == entry.Id);
        PresentDetail();
    }

    private void PresentDetail()
    {
        foreach (var child in _detail.GetChildren()) { _detail.RemoveChild(child); child.QueueFree(); }
        _card = null; _progress = null; _levels = [];
        // Short screens put the card beside its details; tall ones stack them.
        BoxContainer layout = ShortScreen ? new HBoxContainer() : new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12); _detail.AddChild(layout);
        var cardSize = ShortScreen ? new Vector2(180, 255) : new Vector2(330, 467);
        var side = new VBoxContainer { Name = "DetailSide", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        side.AddThemeConstantOverride("separation", ShortScreen ? 8 : 10);

        var front = Card(_selected.Id, 1);
        if (front != null)
        {
            var stage = new Button { Name = "CardButton", CustomMinimumSize = cardSize, SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                TooltipText = "Girar la carta", MouseDefaultCursorShape = CursorShape.PointingHand, FocusMode = FocusModeEnum.All };
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) stage.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            var focus = RelickVisuals.Frame(Colors.Transparent, RelickVisuals.State.Focus, 10, 2); focus.DrawCenter = false;
            stage.AddThemeStyleboxOverride("focus", focus);
            _card = new TextureRect { Name = "RelikarioCard", Texture = front, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
            stage.AddChild(_card); _card.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _card.Resized += () => _card.PivotOffset = _card.Size / 2;
            if (Card(_selected.Id, 2) != null) stage.Pressed += () => SetLevel(_level == 1 ? 2 : 1, manual: true);
            layout.AddChild(stage);
        }
        else
        {
            var placeholder = new PanelContainer { Name = "CardPlaceholder", CustomMinimumSize = cardSize, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            var dashed = RelickVisuals.Frame(new Color("0e1529"), RelickVisuals.Playmat.Frame, 12);
            placeholder.AddThemeStyleboxOverride("panel", dashed);
            var mono = ArenaPresentation.Text(Monogram(_selected.Name), ShortScreen ? 44 : 72, new Color("3b4a74"));
            mono.HorizontalAlignment = HorizontalAlignment.Center; mono.VerticalAlignment = VerticalAlignment.Center;
            mono.AddThemeFontOverride("font", RelickVisuals.Strong); placeholder.AddChild(mono);
            layout.AddChild(placeholder);
        }
        layout.AddChild(side);

        var name = ArenaPresentation.Text(_selected.Definition?.Name ?? _selected.Name, ShortScreen ? 20 : 24);
        name.Name = "DetailName"; name.AddThemeFontOverride("font", RelickVisuals.Display);
        name.HorizontalAlignment = HorizontalAlignment.Center; name.AutowrapMode = TextServer.AutowrapMode.WordSmart; side.AddChild(name);

        if (_selected.Playable && Card(_selected.Id, 2) != null)
        {
            var levels = new HBoxContainer { Name = "LevelButtons", Alignment = AlignmentMode.Center }; levels.AddThemeConstantOverride("separation", 6); side.AddChild(levels);
            var group = new ButtonGroup();
            _levels = new[] { 1, 2 }.Select(level =>
            {
                var button = new Button { Name = "Level" + level, Text = "Nivel " + level, ToggleMode = true, ButtonGroup = group, ButtonPressed = level == 1, CustomMinimumSize = new(0, 40) };
                button.Pressed += () => SetLevel(level, manual: true);
                levels.AddChild(button);
                return button;
            }).ToArray();
            if (!ShortScreen)
            {
                var auto = new HBoxContainer { Alignment = AlignmentMode.Center }; auto.AddThemeConstantOverride("separation", 8); side.AddChild(auto);
                // The level buttons and the card's own tooltip say how to turn it; the bar only times the next turn.
                _progress = new ProgressBar { Name = "AutoProgress", MaxValue = AutoSeconds, ShowPercentage = false, CustomMinimumSize = new(80, 4), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                    MouseFilter = MouseFilterEnum.Pass, TooltipText = "La carta cambia de nivel sola cada pocos segundos." };
                _progress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = RelickVisuals.Playmat.Frame });
                _progress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = RelickVisuals.State.Selected });
                auto.AddChild(_progress);
            }
        }

        if (_selected.Playable && !ShortScreen)
        {
            var skills = CardCatalog.All.Values.Where(card => card.Kind == CardKind.Skill && card.Relikario == _selected.Id).OrderBy(card => card.Name).ToList();
            if (skills.Count > 0)
            {
                side.AddChild(ArenaPresentation.Text("HABILIDADES ESPECÍFICAS", 11, BoardArt.Muted));
                var flow = new HFlowContainer { Name = "SpecificSkills" }; flow.AddThemeConstantOverride("h_separation", 6); flow.AddThemeConstantOverride("v_separation", 6); side.AddChild(flow);
                foreach (var skill in skills)
                {
                    var chip = ArenaPresentation.Text(skill.Name, 13, RelickVisuals.State.Target);
                    var plate = RelickVisuals.Frame(Colors.Transparent, new Color("3a6f82"), 4);
                    plate.ContentMarginLeft = plate.ContentMarginRight = 8; plate.ContentMarginTop = plate.ContentMarginBottom = 2;
                    chip.AddThemeStyleboxOverride("normal", plate); flow.AddChild(chip);
                }
            }
        }
        if (!_selected.Playable)
        {
            var note = ArenaPresentation.Text("Este Relikario todavía no está programado. Sus cartas y su efecto llegarán en una próxima versión.", 14, BoardArt.Muted);
            note.Name = "ComingSoonNote"; note.AutowrapMode = TextServer.AutowrapMode.WordSmart; side.AddChild(note);
        }

        side.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var create = ArenaPresentation.Button(side, _selected.Playable ? $"Crear mazo de {_selected.Name}" : "Próximamente",
            () => { if (_selected.Playable) Chosen?.Invoke(_selected.Id); }, !_selected.Playable);
        create.Name = "CreateDeck"; create.CustomMinimumSize = new(0, 52);
        create.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (_selected.Playable) RelickVisuals.Primary(create);
    }

    /// <summary>Turns the card to a level. A manual turn holds the automatic one off for a while, so the card can be read.</summary>
    internal void SetLevel(int level, bool manual)
    {
        if (manual) _paused = PauseSeconds;
        _elapsed = 0;
        foreach (var button in _levels) button.SetPressedNoSignal(button.Name == "Level" + level);
        if (level == _level || _card == null || Card(_selected.Id, level) is not { } texture) return;
        _level = level;
        _turn?.Kill();
        if (_reducedMotion || !IsInsideTree()) { _card.Texture = texture; _card.Scale = Vector2.One; _card.Modulate = Colors.White; return; }
        var card = _card;
        _turn = CreateTween();
        // Squeezing the card edge-on and back reads as turning it over; the face changes while it is edge-on.
        _turn.TweenProperty(card, "scale:x", 0.0f, TurnSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        _turn.Parallel().TweenProperty(card, "modulate", new Color(1.25f, 1.25f, 1.25f), TurnSeconds);
        _turn.TweenCallback(Callable.From(() => card.Texture = texture));
        _turn.TweenProperty(card, "scale:x", 1.0f, TurnSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _turn.Parallel().TweenProperty(card, "modulate", Colors.White, TurnSeconds);
    }

    public override void _Process(double delta)
    {
        if (_levels.Length == 0) return;
        // Reading the card holds the automatic turn: while the pointer rests on the panel, and for a while after a manual turn.
        var hovered = _detail.GetGlobalRect().HasPoint(GetGlobalMousePosition());
        if (_paused > 0 || hovered) { _paused = Math.Max(0, _paused - delta); _elapsed = 0; }
        else if ((_elapsed += delta) >= AutoSeconds) SetLevel(_level == 1 ? 2 : 1, manual: false);
        if (_progress != null) _progress.Value = _elapsed;
    }
}

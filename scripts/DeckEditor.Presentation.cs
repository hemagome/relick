using System.Globalization;
using Godot;
using Relick.Core;

namespace Relick;

public partial class DeckEditor
{
    private static readonly Color EditorBackground = new("100e20");
    private static readonly Color EditorSurface = new("171329");
    private static readonly Color EditorEdge = new("3e3357");
    private readonly StyleBoxFlat _copiesBadge = BadgeStyle(BoardArt.Gold);
    private readonly StyleBoxFlat _benchBadge = BadgeStyle(new Color("c0acf7"));
    private readonly StyleBoxFlat _activeTabStyle = TabStyle(true);
    private readonly StyleBoxFlat _inactiveTabStyle = TabStyle(false);
    private const int TileGap = 7;
    private const int AllTypes = 100;

    private readonly record struct Tile(HandCard Face, Label Badge, Control? Bench = null, Label? BenchCount = null);
    private readonly Dictionary<string, Tile> _catalog = [];
    private readonly Dictionary<(string Id, int Index), Tile> _deckTiles = [];
    private readonly Dictionary<Section, Button> _tabs = [];
    private readonly Dictionary<(string Kind, string Id), Button> _styleTiles = [];

    private LineEdit _name = null!;
    private LineEdit _search = null!;
    private OptionButton _filter = null!;
    private Button _back = null!;
    private Button _save = null!;
    private Button _undo = null!;
    private Button _requirements = null!;
    private Label _notice = null!;
    private Label _mainTotal = null!;
    private Label _attackTotal = null!;
    private Label _skillTotal = null!;
    private Label _catalogTotal = null!;
    private Label _catalogEmpty = null!;
    private Label _deckTotal = null!;
    private Label _deckEmpty = null!;
    private PanelContainer _inspectorPanel = null!;
    private PanelContainer _catalogPanel = null!;
    private VBoxContainer _deckContent = null!;
    private ScrollContainer _deckScroll = null!;
    private ScrollContainer _gallery = null!;
    private ScrollContainer _styleScroll = null!;
    private GridContainer _deckGrid = null!;
    private GridContainer _catalogGrid = null!;
    private Button _zoom = null!;
    private Label _inspectorTitle = null!;
    private TextureRect _art = null!;
    private ScrollContainer _reading = null!;
    private Label _classification = null!;
    private Label _cardName = null!;
    private Label _description = null!;
    private CardDefinition? _inspected;
    private HBoxContainer _stepper = null!;
    private Label _copies = null!;
    private Button _minus = null!;
    private Button _plus = null!;
    private Button _move = null!;
    private float _deckScale = 0.60f;
    private float _catalogScale = 0.53f;

    private void BuildView()
    {
        AddThemeConstantOverride("separation", 0);
        BuildHeader();
        BuildOverview();

        var workspace = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        workspace.AddThemeConstantOverride("separation", 0);
        AddChild(workspace);
        BuildInspector(workspace);
        BuildDeck(workspace);
        BuildCatalog(workspace);
        BuildFooter();

        Resized += QueueLayout;
        _deckScroll.Resized += QueueLayout;
        _gallery.Resized += QueueLayout;
        QueueLayout();
    }

    private void BuildHeader()
    {
        var header = Panel(this, EditorBackground, 10, 4);
        var row = Row(header, 10);
        _back = EditorButton(row, "EditorBack", "‹ Mazos", RequestClose, 40);
        var logo = RelickVisuals.Logo(compact: true);
        logo.CustomMinimumSize = ShortScreen ? new(90, 24) : new(120, 30);
        row.AddChild(logo);
        var title = ArenaPresentation.Text("Editor de mazos", ShortScreen ? 13 : 16, BoardArt.Muted);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(title);
        _save = EditorButton(row, "SaveDeck", "Guardar", Save, 40);
        _save.CustomMinimumSize = new(ShortScreen ? 130 : 160, 40);
        RelickVisuals.Primary(_save, BoardArt.Gold);
    }

    private void BuildOverview()
    {
        var hero = new PanelContainer();
        var style = RelickVisuals.Glass(new Color("382750"), new Color("211831"), new Color("6b4e84"), 4);
        style.ContentMarginLeft = style.ContentMarginRight = ShortScreen ? 10 : 16;
        style.ContentMarginTop = style.ContentMarginBottom = ShortScreen ? 5 : 9;
        hero.AddThemeStyleboxOverride("panel", style);
        AddChild(hero);

        var row = Row(hero, ShortScreen ? 8 : 13);
        if (DeckScreen.Thumbnail(Relikario) is { } portrait)
        {
            var side = ShortScreen ? 43 : 61;
            row.AddChild(Artwork(portrait, new(side, side), "DeckRelikario"));
        }

        var identity = Column(row, 0);
        identity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var nameRow = Row(identity, 1);
        var pencil = new IconButton
        {
            Symbol = GameIcon.Pencil, TooltipText = "Cambiar nombre", AccessibilityName = "Cambiar nombre del mazo",
            CustomMinimumSize = new(ShortScreen ? 27 : 32, 27)
        };
        pencil.Pressed += () => { _name.GrabFocus(); _name.SelectAll(); };
        nameRow.AddChild(pencil);
        _name = new LineEdit
        {
            Name = "DeckName", Text = DeckName, PlaceholderText = "Nombre del mazo", MaxLength = 32,
            AccessibilityName = "Nombre del mazo", SizeFlagsHorizontal = SizeFlags.ExpandFill, EmojiMenuEnabled = false
        };
        _name.AddThemeFontOverride("font", RelickVisuals.Strong);
        _name.AddThemeFontSizeOverride("font_size", ShortScreen ? 17 : 25);
        foreach (var state in new[] { "normal", "read_only" })
            _name.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 2, ContentMarginRight = 2 });
        nameRow.AddChild(_name);
        var relikario = RelikarioCatalog.Find(Relikario)?.Name ?? "Relikario";
        var subtitle = ArenaPresentation.Text(relikario, ShortScreen ? 12 : 14, new Color("d3c2e8"));
        subtitle.ClipText = true;
        identity.AddChild(subtitle);

        var totals = Row(row, ShortScreen ? 6 : 13);
        totals.Name = "DeckTotals";
        totals.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Label Metric(string caption)
        {
            totals.AddChild(new VSeparator());
            var metric = Column(totals, 0);
            metric.CustomMinimumSize = new(ShortScreen ? 68 : 91, 0);
            metric.AddChild(ArenaPresentation.Text(caption, ShortScreen ? 11 : 12, new Color("cdbbdf")));
            var value = ArenaPresentation.Text("", ShortScreen ? 14 : 20);
            value.AddThemeFontOverride("font", RelickVisuals.Strong);
            metric.AddChild(value);
            return value;
        }
        _mainTotal = Metric("Principal");
        _attackTotal = Metric("Ataques");
        _skillTotal = Metric("Habilidades");
    }

    private void BuildInspector(HBoxContainer workspace)
    {
        _inspectorPanel = Panel(workspace, EditorSurface, ShortScreen ? 7 : 11, ShortScreen ? 5 : 9);
        _inspectorPanel.Name = "InspectorPanel";
        var column = Column(_inspectorPanel, ShortScreen ? 5 : 9);
        var heading = Row(column, 3);
        _inspectorTitle = Heading(heading, "Inspector");
        _inspectorTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _inspectorTitle.MaxLinesVisible = 2;
        _inspectorTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _move = new FramedIconButton
        {
            Name = "Move", Symbol = GameIcon.Exchange, CustomMinimumSize = new(36, 33), SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        _move.Pressed += () =>
        {
            if (_selected is { } card) Move(card, toSide: !EditingSide);
        };
        heading.AddChild(_move);

        _zoom = EditorButton(column, "InspectArtwork", "", ZoomCard, 76);
        _zoom.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _zoom.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _art = Artwork(null, Vector2.Zero, "InspectorArtwork");
        _zoom.AddChild(_art);
        _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _art.OffsetLeft = _art.OffsetTop = 2;
        _art.OffsetRight = _art.OffsetBottom = -2;

        _reading = Scroll(column, "EditorInspector", reserve: false);
        var text = Column(_reading, ShortScreen ? 2 : 5);
        _classification = Wrapped(text, "InspectorClassification", ShortScreen ? 11 : 13, RelickVisuals.State.Target);
        _cardName = Wrapped(text, "ContextCardName", ShortScreen ? 14 : 18, BoardArt.Ink);
        _cardName.AddThemeFontOverride("font", RelickVisuals.Strong);
        _description = Wrapped(text, "InspectorDescription", ShortScreen ? 12 : 14, new Color("c6bdd9"));

        _stepper = Row(column, 4);
        _minus = EditorButton(_stepper, "Less", "−", () =>
        {
            if (_selected is { } card) Remove(card, _section);
        }, 39, "Quitar una copia de esta sección");
        _minus.CustomMinimumSize = new(36, 39);
        _copies = ArenaPresentation.Text("", ShortScreen ? 12 : 14);
        _copies.Name = "InspectorCopies";
        _copies.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _copies.HorizontalAlignment = HorizontalAlignment.Center;
        _copies.VerticalAlignment = VerticalAlignment.Center;
        _stepper.AddChild(_copies);
        _plus = EditorButton(_stepper, "More", "+", () =>
        {
            if (_selected is { } card) Add(card, _section);
        }, 39, "Añadir una copia a esta sección");
        _plus.CustomMinimumSize = new(36, 39);
    }

    private void BuildDeck(HBoxContainer workspace)
    {
        var panel = Panel(workspace, EditorBackground, ShortScreen ? 7 : 11, ShortScreen ? 5 : 9);
        panel.Name = "DeckPanel";
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var column = Column(panel, ShortScreen ? 5 : 8);
        var title = Row(column, 6);
        Heading(title, "Tu mazo");
        _undo = EditorButton(title, "UndoDeck", "↶ Deshacer", Undo, 33, "Deshacer el último cambio de cartas (Ctrl+Z)");
        _undo.Flat = true;

        var tabs = Row(column, ShortScreen ? 2 : 5);
        tabs.Name = "ListTabs";
        var group = new ButtonGroup();
        foreach (var (section, name, hint) in new[]
        {
            (Section.Main, "DeckTab", "Mazo principal"),
            (Section.Traps, "TrapTab", "Mazo de Códigos Trampa"),
            (Section.Side, "SideTab", "Banquillo"),
            (Section.Style, "StyleTab", "Mica y dado")
        })
        {
            var tab = EditorButton(tabs, name, "", () => SelectSection(section), ShortScreen ? 35 : 39, hint);
            tab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tab.SizeFlagsStretchRatio = section switch { Section.Side => 1.25f, Section.Style => 0.76f, _ => 1 };
            tab.ClipText = true;
            tab.ToggleMode = true;
            tab.ButtonGroup = group;
            tab.AddThemeFontSizeOverride("font_size", ShortScreen ? 11 : 14);
            tab.AddThemeStyleboxOverride("normal", _inactiveTabStyle);
            tab.AddThemeStyleboxOverride("pressed", _activeTabStyle);
            tab.AddThemeColorOverride("font_pressed_color", BoardArt.Gold);
            _tabs.Add(section, tab);
            if (section != Section.Style) SetDropTarget(tab, () => section);
        }

        _deckContent = Column(column, 5);
        _deckContent.SizeFlagsVertical = SizeFlags.ExpandFill;
        var toolbar = Row(_deckContent, 4);
        _deckTotal = ArenaPresentation.Text("", ShortScreen ? 11 : 13, BoardArt.Muted);
        _deckTotal.ClipText = true;
        _deckTotal.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _deckTotal.VerticalAlignment = VerticalAlignment.Center;
        toolbar.AddChild(_deckTotal);
        var groupCopies = new CheckButton { Name = "GroupCopies", Text = "Agrupar copias", ButtonPressed = true, Flat = true };
        var flat = new StyleBoxEmpty();
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) groupCopies.AddThemeStyleboxOverride(state, flat);
        groupCopies.AddThemeFontSizeOverride("font_size", ShortScreen ? 11 : 13);
        groupCopies.Toggled += grouped =>
        {
            _grouped = grouped;
            _deckScroll.ScrollVertical = 0;
            PresentDeck();
        };
        toolbar.AddChild(groupCopies);

        var board = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var gridFrame = RelickVisuals.Frame(new Color("151125"), EditorEdge, 3);
        gridFrame.ContentMarginLeft = gridFrame.ContentMarginRight = 5;
        gridFrame.ContentMarginTop = gridFrame.ContentMarginBottom = 4;
        board.AddThemeStyleboxOverride("panel", gridFrame);
        _deckContent.AddChild(board);

        var guides = new Control { MouseFilter = MouseFilterEnum.Ignore };
        guides.Draw += () =>
        {
            var line = new Color(0.49f, 0.40f, 0.67f, 0.10f);
            const int spacing = 23;
            for (var x = spacing; x < guides.Size.X; x += spacing)
                guides.DrawLine(new(x, 0), new(x, guides.Size.Y), line);
            for (var y = spacing; y < guides.Size.Y; y += spacing)
                guides.DrawLine(new(0, y), new(guides.Size.X, y), line);
        };
        guides.Resized += guides.QueueRedraw;
        board.AddChild(guides);
        _deckScroll = Scroll(board, "DeckList");
        SetDropTarget(_deckScroll, () => _section);
        var deck = Column(_deckScroll, 4);
        _deckGrid = Grid(deck, "DeckCards");
        _deckEmpty = Wrapped(deck, "DeckEmpty", ShortScreen ? 12 : 14, BoardArt.Muted);
        _deckEmpty.HorizontalAlignment = HorizontalAlignment.Center;

        _styleScroll = Scroll(column, "DeckStyle");
        _styleScroll.Hide();
    }

    private void BuildStyle()
    {
        var styleColumn = Column(_styleScroll, 8);
        BuildStyleSection(styleColumn, "Mica", "Sleeve", CosmeticCatalog.Sleeves.Select(sleeve => (sleeve.Id, sleeve.Name)),
            id => Artwork(SleeveArt.Back(id), Vector2.Zero), id => ChooseStyle(sleeve: id));
        BuildStyleSection(styleColumn, "Dado", "Dice", CosmeticCatalog.Dice.Select(dice => (dice.Id, dice.Name)),
            id => new DiceSwatch { Style = id }, id => ChooseStyle(dice: id));
        PresentStyle();
    }

    private void BuildCatalog(HBoxContainer workspace)
    {
        _catalogPanel = Panel(workspace, EditorSurface, ShortScreen ? 7 : 10, ShortScreen ? 5 : 9);
        _catalogPanel.Name = "CatalogPanel";
        var column = Column(_catalogPanel, 6);
        var header = Row(column, 2);
        Heading(header, "Colección");
        _catalogTotal = ArenaPresentation.Text("", ShortScreen ? 10 : 12, BoardArt.Muted);
        _catalogTotal.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(_catalogTotal);

        _search = new LineEdit
        {
            Name = "CatalogSearch", PlaceholderText = "Buscar carta…", ClearButtonEnabled = true, EmojiMenuEnabled = false,
            AccessibilityName = "Buscar carta por nombre", CustomMinimumSize = new(0, 35)
        };
        _search.AddThemeFontSizeOverride("font_size", ShortScreen ? 12 : 14);
        _search.TextChanged += _ => FilterChanged();
        column.AddChild(_search);
        _filter = new OptionButton { Name = "CatalogFilters", AccessibilityName = "Tipo de carta", FitToLongestItem = false,
            CustomMinimumSize = new(0, 34), ClipText = true, MouseDefaultCursorShape = CursorShape.PointingHand };
        _filter.AddThemeFontSizeOverride("font_size", ShortScreen ? 12 : 14);
        _filter.ItemSelected += _ => FilterChanged();
        column.AddChild(_filter);
        if (OS.HasFeature("mobile")) ArenaViewport.TouchMenu(_filter.GetPopup());

        _gallery = Scroll(column, "CatalogGallery");
        var gallery = Column(_gallery, 4);
        _catalogGrid = Grid(gallery, "CatalogCards", 3);
        _catalogEmpty = Wrapped(gallery, "CatalogEmpty", ShortScreen ? 12 : 14, BoardArt.Muted);
        _catalogEmpty.Text = "No hay cartas que coincidan.";
        foreach (var card in OrderedCards.Where(card => DeckRules.Compatible(card, Relikario)))
        {
            var tile = CreateTile(card, "Catalog_" + card.Id, _catalogScale, fromCatalog: true);
            _catalog.Add(card.Id, tile);
            _catalogGrid.AddChild(tile.Face);
        }
        UpdateFilterOptions();
    }

    private void BuildFooter()
    {
        var footer = Panel(this, EditorSurface, 10, 2);
        var row = Row(footer, 8);
        _notice = ArenaPresentation.Text("", ShortScreen ? 11 : 13, BoardArt.Muted);
        _notice.Name = "EditorNotice";
        _notice.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _notice.ClipText = true;
        _notice.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _notice.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_notice);
        _requirements = EditorButton(row, "DeckProblems", "", ShowRequirements, ShortScreen ? 27 : 33);
        _requirements.Flat = true;
        _requirements.Alignment = HorizontalAlignment.Right;
        _requirements.AddThemeFontSizeOverride("font_size", ShortScreen ? 11 : 13);
        _requirements.AddThemeColorOverride("font_disabled_color", ArenaPresentation.Mint);
    }

    private void BuildStyleSection(Node parent, string title, string prefix, IEnumerable<(string Id, string Name)> options,
        Func<string, Control> picture, Action<string> choose)
    {
        parent.AddChild(ArenaPresentation.Text(title, 13, BoardArt.Muted));
        var grid = new HFlowContainer { Name = prefix + "Options" };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        parent.AddChild(grid);
        foreach (var (id, label) in options)
        {
            var tile = EditorButton(grid, prefix + "_" + id, "", () => choose(id), ShortScreen ? 100 : 125, label);
            tile.AccessibilityName = $"{title} {label}";
            tile.CustomMinimumSize = new(ShortScreen ? 79 : 91, ShortScreen ? 100 : 125);
            var content = Column(tile, 4);
            content.MouseFilter = MouseFilterEnum.Ignore;
            content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            content.OffsetTop = 6;
            content.OffsetBottom = -5;
            var art = picture(id);
            art.MouseFilter = MouseFilterEnum.Ignore;
            art.SizeFlagsVertical = SizeFlags.ExpandFill;
            content.AddChild(art);
            var text = ArenaPresentation.Text(label, 12);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.MouseFilter = MouseFilterEnum.Ignore;
            content.AddChild(text);
            _styleTiles.Add((prefix, id), tile);
        }
    }

    private void PresentStyle()
    {
        foreach (var ((kind, id), button) in _styleTiles)
        {
            if (id == (kind == "Sleeve" ? _sleeve : _dice))
                button.AddThemeStyleboxOverride("normal", _activeTabStyle);
            else
                button.RemoveThemeStyleboxOverride("normal");
        }
    }

    private void Refresh()
    {
        PresentRules();
        _undo.Disabled = _history.Count == 0;
        if (EditingStyle && _styleTiles.Count == 0) BuildStyle();
        _deckContent.Visible = !EditingStyle;
        _styleScroll.Visible = EditingStyle;
        PresentDeck();
        PresentInspector();
        PresentCatalogCopies();
        PresentSelection();
    }

    private void PresentRules()
    {
        var main = CountIn(Section.Main);
        var attacks = Count(DeckRules.IsAttack);
        var skills = Count(card => card.Kind == CardKind.Skill && !DeckRules.IsTrap(card) && DeckRules.Compatible(card, Relikario));
        Counter(_mainTotal, $"{main}/{DeckRules.MainDeckSize}", main == DeckRules.MainDeckSize);
        Counter(_attackTotal, $"{attacks}/{DeckRules.AttackCount}", attacks == DeckRules.AttackCount);
        Counter(_skillTotal, $"{skills} · mín. {DeckRules.MinSkills}", skills >= DeckRules.MinSkills);

        _tabs[Section.Main].Text = "Principal";
        _tabs[Section.Traps].Text = $"Trampas {CountIn(Section.Traps)}/{DeckRules.MaxTraps}";
        _tabs[Section.Side].Text = $"Banquillo {CountIn(Section.Side)}/{DeckRules.MaxSide}";
        _tabs[Section.Style].Text = "Estilo";
        foreach (var (section, tab) in _tabs) tab.SetPressedNoSignal(section == _section);

        var problems = Problems;
        var ready = problems.Count == 0;
        _requirements.Text = ready ? "✓ Mazo listo para jugar" : problems.Count == 1 ? "1 requisito pendiente ›" : $"{problems.Count} requisitos pendientes ›";
        _requirements.TooltipText = ready ? "" : string.Join("\n", problems);
        _requirements.Disabled = ready;
        _requirements.AddThemeColorOverride("font_color", ArenaPresentation.Coral);
        if (!Saving) _save.Text = ready ? "Guardar" : "Guardar borrador";
    }

    private static void Counter(Label label, string value, bool valid)
    {
        label.Text = value;
        label.AddThemeColorOverride("font_color", valid ? BoardArt.Ink : new Color("ffb2ad"));
    }

    private void PresentDeck()
    {
        if (EditingStyle) return;
        var cards = OrderedCards.Where(card => Copies(card, _section) > 0).ToArray();
        var wanted = new HashSet<(string Id, int Index)>();
        var position = 0;
        foreach (var card in cards)
        {
            var copies = Copies(card, _section);
            for (var index = 0; index < (_grouped ? 1 : copies); index++)
            {
                var key = (card.Id, index);
                wanted.Add(key);
                if (!_deckTiles.TryGetValue(key, out var tile))
                {
                    tile = CreateTile(card, index == 0 ? "Deck_" + card.Id : $"Deck_{card.Id}_{index + 1}", _deckScale, fromCatalog: false);
                    _deckTiles.Add(key, tile);
                    _deckGrid.AddChild(tile.Face);
                }
                if (tile.Face.GetIndex() != position) _deckGrid.MoveChild(tile.Face, position);
                position++;
                tile.Badge.Text = $"×{copies}";
                tile.Badge.Visible = _grouped && copies > 1;
                var label = CardWithArtwork(card);
                tile.Face.AccessibilityName = _grouped ? $"{label}, {copies} copias" : $"{label}, copia {index + 1} de {copies}";
                PositionBadges(tile);
            }
        }

        var restoreFocus = false;
        foreach (var key in _deckTiles.Keys.Where(key => !wanted.Contains(key)).ToArray())
        {
            var face = _deckTiles[key].Face;
            restoreFocus |= face.HasFocus();
            _deckGrid.RemoveChild(face);
            face.QueueFree();
            _deckTiles.Remove(key);
        }
        if (restoreFocus)
        {
            var next = _deckGrid.GetChildren().OfType<HandCard>().FirstOrDefault();
            if (next != null) next.GrabFocus();
            else _tabs[_section].GrabFocus();
        }
        _deckEmpty.Visible = cards.Length == 0;
        _deckEmpty.Text = EditingSide ? $"Banquillo vacío · Admite hasta {DeckRules.MaxSide} cartas sin Ataques."
            : "Arrastra una carta de la colección para añadirla.";
        var distinct = cards.Select(card => card.Name).Distinct().Count();
        _deckTotal.Text = _grouped ? $"{distinct} {(distinct == 1 ? "carta distinta" : "cartas distintas")}" : "Copias individuales";
        PresentSelection();
    }

    private Tile CreateTile(CardDefinition card, string name, float scale, bool fromCatalog)
    {
        var label = CardWithArtwork(card);
        var face = new HandCard
        {
            Card = card, RelikarioId = Relikario, PresentationScale = scale, Name = name,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin, AccessibilityName = label
        };
        face.Ready += () => face.TooltipText = label;
        var badge = ArenaPresentation.Text("", ShortScreen ? 10 : 12, BoardArt.Gold);
        badge.Name = "Copies";
        badge.MouseFilter = MouseFilterEnum.Ignore;
        badge.AddThemeFontOverride("font", RelickVisuals.Strong);
        badge.AddThemeStyleboxOverride("normal", _copiesBadge);
        face.AddChild(badge);
        Tile tile;
        if (!fromCatalog)
        {
            tile = new(face, badge);
            SetDropTarget(face, () => _section);
        }
        else
        {
            var bench = new PanelContainer { Name = "Bench", MouseFilter = MouseFilterEnum.Ignore };
            bench.AddThemeStyleboxOverride("panel", _benchBadge);
            var row = Row(bench, 2);
            row.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(new IconView { Icon = GameIcon.Bench, Color = new Color("e2d5ff"), CustomMinimumSize = new(11, 11),
                MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter });
            var count = ArenaPresentation.Text("", ShortScreen ? 10 : 12, new Color("e2d5ff"));
            count.Name = "BenchCount";
            count.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(count);
            face.AddChild(bench);
            tile = new(face, badge, bench, count);
            face.SetDragForwarding(Callable.From<Vector2, Variant>(_ => DragCard(face)), default, default);
        }
        face.Resized += () => PositionBadges(tile);
        face.Pressed += () => Inspect(card);
        face.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) return;
            face.AcceptEvent();
            if (EditingStyle) { Inspect(card); return; }
            if (fromCatalog) Add(card, _section);
            else Remove(card, _section);
        };
        return tile;
    }

    private static string CardWithArtwork(CardDefinition card)
    {
        var artwork = card.ArtworkLabel ?? (card.Id == CardCatalog.Attack.Id ? "Arte clásico" : null);
        return artwork == null ? card.Name : $"{card.Name} · {artwork}";
    }

    private static StyleBoxFlat BadgeStyle(Color color)
    {
        var plate = RelickVisuals.Frame(new Color("171022"), color.Darkened(0.38f), 3);
        plate.ContentMarginLeft = plate.ContentMarginRight = 4;
        plate.ContentMarginTop = plate.ContentMarginBottom = 1;
        return plate;
    }

    private static void PositionBadges(Tile tile)
    {
        tile.Badge.ResetSize();
        tile.Badge.Position = tile.Face.Size - tile.Badge.Size - new Vector2(4, 6);
        if (tile.Bench is { } bench)
        {
            bench.ResetSize();
            bench.Position = new(4, 7);
        }
    }

    private void PresentCatalogCopies()
    {
        foreach (var tile in _catalog.Values)
        {
            var card = tile.Face.Card;
            var copies = Copies(card, Home(card));
            var benched = Copies(card, Section.Side);
            tile.Badge.Text = $"×{copies}";
            tile.Badge.Visible = copies > 0;
            tile.BenchCount!.Text = benched.ToString();
            tile.Bench!.Visible = benched > 0;
            PositionBadges(tile);
            tile.Face.TooltipText = $"{CardWithArtwork(card)}\n{copies} en {SectionName(Home(card))} · {benched} en el banquillo";
        }
    }

    private void PresentSelection()
    {
        foreach (var tile in _catalog.Values.Concat(_deckTiles.Values))
        {
            var chosen = tile.Face.Card == _selected;
            if (tile.Face.Chosen == chosen) continue;
            tile.Face.Chosen = chosen;
            tile.Face.QueueRedraw();
        }
    }

    private void PresentInspector()
    {
        var card = _selected;
        _stepper.Visible = !EditingStyle;
        _move.Visible = !EditingStyle;
        if (card == null) return;
        if (_inspected != card)
        {
            _inspected = card;
            _reading.ScrollVertical = 0;
            _art.Texture = BoardArt.LoadCardArtwork(card, Relikario);
            _zoom.Disabled = _art.Texture == null;
            var label = CardWithArtwork(card);
            _zoom.TooltipText = _zoom.AccessibilityName = $"Ampliar {label}";
            _classification.Text = CardInspectorText.Classification(card);
            _cardName.Text = label;
            var cost = ArenaActionText.CardCost(card);
            var stats = card.Kind == CardKind.Skill
                ? $"Daño {card.NormalDamageText()} · Crítico {card.CriticalDamageText()} · {card.Reward} PR\n\n" : "";
            _description.Text = stats + CardInspectorText.Description(card) + (cost.Length == 0 ? "" : $"\n\nCoste: {cost}");
            UpdateInspectorLayout();
        }
        if (EditingStyle) return;

        var copies = Copies(card, _section);
        _copies.Text = $"{copies} {(copies == 1 ? "copia" : "copias")}";
        _copies.TooltipText = SectionName(_section);
        _minus.Disabled = copies == 0;
        var reason = AddBlocked(card, _section);
        _plus.Disabled = reason != null;
        _plus.TooltipText = reason ?? $"Añadir una copia · {SectionName(_section)}";

        var destination = EditingSide ? Home(card) : Section.Side;
        var blockedMove = copies == 0 ? "Selecciona una carta incluida en esta sección." : AddBlocked(card, destination, moving: true);
        _move.Disabled = blockedMove != null;
        _move.AccessibilityName = $"Mover una copia · {SectionName(destination)}";
        _move.TooltipText = blockedMove ?? _move.AccessibilityName;
    }

    private void ZoomCard()
    {
        if (_selected is not { } card || _art.Texture is not { } texture) return;
        var available = GetViewportRect().Size;
        var extent = BoardArt.FitArtwork(texture, new Rect2(Vector2.Zero, new(available.X * 0.83f, available.Y - 128))).Size;
        ShowDetails("DeckCardZoom", CardWithArtwork(card), Artwork(texture, extent, "EnlargedCard"));
    }

    private void ShowRequirements()
    {
        var problems = Problems;
        if (problems.Count == 0) return;
        var label = ArenaPresentation.Text("• " + string.Join("\n\n• ", problems), 15);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new(Math.Min(GetViewportRect().Size.X - 70, 500), Math.Min(Size.Y - 120, 225))
        };
        scroll.AddChild(label);
        ShowDetails("DeckRequirements", "Requisitos del mazo", scroll);
    }

    private void ShowDetails(string name, string title, Control content)
    {
        if (GetChildren().OfType<Window>().Any(window => window.Visible)) return;
        var dialog = new AcceptDialog { Name = name, Title = title, OkButtonText = "Cerrar" };
        dialog.AddChild(content);
        AddChild(dialog);
        dialog.GetOkButton().CustomMinimumSize = new(90, 40);
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        ArenaViewport.FitDialog(dialog, GetViewportRect().Size);
    }

    private bool InCatalogSection(CardDefinition card) => _section switch
    {
        Section.Main => Home(card) == Section.Main,
        Section.Traps => Home(card) == Section.Traps,
        Section.Side => DeckRules.AllowedInSide(card),
        _ => true
    };

    private void UpdateFilterOptions()
    {
        _filter.Clear();
        _filter.AddItem("Todos los tipos", AllTypes);
        for (var index = 0; index < Groups.Length; index++)
        {
            var group = Groups[index];
            if (_catalog.Values.Any(tile => InCatalogSection(tile.Face.Card) && group.Match(tile.Face.Card)))
                _filter.AddItem(group.Title, index);
        }
        _filter.Select(0);
        _filter.Visible = _filter.ItemCount > 2;
        ApplyFilter();
    }

    private void FilterChanged()
    {
        _gallery.ScrollVertical = 0;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = _search.Text.Trim();
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var filterId = _filter.GetSelectedId();
        var visible = 0;
        foreach (var tile in _catalog.Values)
        {
            var card = tile.Face.Card;
            tile.Face.Visible = InCatalogSection(card) && (filterId == AllTypes || Groups[filterId].Match(card)) &&
                compare.IndexOf(CardWithArtwork(card), query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
            if (tile.Face.Visible) visible++;
        }
        _catalogTotal.Text = visible == 1 ? "1 carta" : $"{visible} cartas";
        _catalogEmpty.Visible = visible == 0;
    }

    private string DragPrefix => $"relick-deck:{GetInstanceId()}:";

    private Variant DragCard(HandCard face)
    {
        if (Saving || EditingStyle) return default;
        Inspect(face.Card);
        var preview = Artwork(face.FaceTexture, new(70, 98));
        preview.Size = preview.CustomMinimumSize;
        face.SetDragPreview(preview);
        return DragPrefix + face.Card.Id;
    }

    private CardDefinition? DraggedCard(Variant data)
    {
        if (Saving || data.VariantType != Variant.Type.String) return null;
        var text = data.AsString();
        var prefix = DragPrefix;
        return text.StartsWith(prefix, StringComparison.Ordinal) && CardCatalog.All.TryGetValue(text[prefix.Length..], out var card)
            ? card : null;
    }

    private void SetDropTarget(Control control, Func<Section> section)
    {
        control.SetDragForwarding(default,
            Callable.From<Vector2, Variant, bool>((_, data) => section() != Section.Style && DraggedCard(data) != null),
            Callable.From<Vector2, Variant>((_, data) =>
            {
                if (DraggedCard(data) is not { } card) return;
                var target = section();
                if (AddBlocked(card, target) == null) SelectSection(target);
                Add(card, target);
            }));
    }

    private bool _layoutQueued;

    private void QueueLayout()
    {
        if (_layoutQueued) return;
        _layoutQueued = true;
        CallDeferred(nameof(UpdateLayout));
    }

    private void UpdateLayout()
    {
        _layoutQueued = false;
        if (!IsInsideTree()) return;
        _inspectorPanel.CustomMinimumSize = new(Math.Clamp(Mathf.Floor(Size.X * 0.185f), 141, 235), 0);
        _catalogPanel.CustomMinimumSize = new(Math.Clamp(Mathf.Floor(Size.X * 0.254f), 172, 333), 0);
        UpdateInspectorLayout();
        int Width(ScrollContainer scroll) => (int)Mathf.Floor(scroll.Size.X - scroll.GetVScrollBar().GetCombinedMinimumSize().X - 2);
        var deckWidth = Width(_deckScroll);
        if (deckWidth > 0)
        {
            var minimum = Size.Y < 460 ? 55 : 73;
            var columns = Math.Clamp((deckWidth + TileGap) / (minimum + TileGap), 1, 8);
            _deckGrid.Columns = columns;
            var maxWidth = Size.Y < 460 ? Math.Clamp((int)((_deckScroll.Size.Y - 3) * 126 / 184), 44, 105) : 105;
            _deckScale = ResizeTiles(_deckTiles.Values, deckWidth, columns, maxWidth);
        }
        var catalogWidth = Width(_gallery);
        if (catalogWidth > 0) _catalogScale = ResizeTiles(_catalog.Values, catalogWidth, 3, 103);
    }

    private void UpdateInspectorLayout()
    {
        var compact = Size.Y < 460;
        _inspectorTitle.Text = compact ? _selected?.Name ?? "Inspector" : "Inspector";
        _inspectorTitle.TooltipText = compact ? _selected?.Name ?? "" : "";
        _inspectorTitle.AddThemeFontSizeOverride("font_size", compact ? 12 : ShortScreen ? 14 : 17);
        _cardName.Visible = !compact;
        _classification.Visible = !compact;
        var available = compact ? 77 : Math.Clamp(Size.Y * 0.30f, 130, 285);
        if (_art.Texture is { } texture && _zoom.Size.X > 4)
        {
            var fit = (_zoom.Size.X - 4) * texture.GetHeight() / texture.GetWidth() + 4;
            available = Math.Min(fit, available);
        }
        _zoom.CustomMinimumSize = new(0, available);
    }

    private static float ResizeTiles(IEnumerable<Tile> tiles, int width, int columns, int maxWidth)
    {
        var cardWidth = Math.Clamp((width - TileGap * (columns - 1)) / columns, 30, maxWidth);
        var scale = cardWidth / 126f;
        foreach (var tile in tiles)
        {
            if (Mathf.IsEqualApprox(tile.Face.PresentationScale, scale)) continue;
            tile.Face.PresentationScale = scale;
            tile.Face.CustomMinimumSize = new(cardWidth, 184 * scale);
            tile.Face.QueueRedraw();
        }
        return scale;
    }

    private static PanelContainer Panel(Node parent, Color color, int horizontal, int vertical)
    {
        var panel = new PanelContainer();
        var style = RelickVisuals.Frame(color, EditorEdge, 2);
        style.ContentMarginLeft = style.ContentMarginRight = horizontal;
        style.ContentMarginTop = style.ContentMarginBottom = vertical;
        panel.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(panel);
        return panel;
    }

    private static HBoxContainer Row(Node parent, int separation)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", separation);
        parent.AddChild(row);
        return row;
    }

    private static VBoxContainer Column(Node parent, int separation)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", separation);
        parent.AddChild(column);
        return column;
    }

    private static ScrollContainer Scroll(Node parent, string name, bool reserve = true)
    {
        var scroll = new ScrollContainer
        {
            Name = name, FollowFocus = true,
            HorizontalScrollMode = reserve ? ScrollContainer.ScrollMode.ShowNever : ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = reserve ? ScrollContainer.ScrollMode.Reserve : ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        parent.AddChild(scroll);
        return scroll;
    }

    private static GridContainer Grid(Node parent, string name, int columns = 1)
    {
        var grid = new GridContainer { Name = name, Columns = columns };
        grid.AddThemeConstantOverride("h_separation", TileGap);
        grid.AddThemeConstantOverride("v_separation", TileGap);
        parent.AddChild(grid);
        return grid;
    }

    private static TextureRect Artwork(Texture2D? image, Vector2 minimum, string name = "Artwork") => new()
    {
        Name = name, Texture = image, CustomMinimumSize = minimum, MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
    };

    private Label Heading(Node parent, string title)
    {
        var label = ArenaPresentation.Text(title, ShortScreen ? 14 : 17);
        label.AddThemeFontOverride("font", RelickVisuals.Strong);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        parent.AddChild(label);
        return label;
    }

    private static Label Wrapped(Node parent, string name, int size, Color color)
    {
        var label = ArenaPresentation.Text("", size, color);
        label.Name = name;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(label);
        return label;
    }

    private static Button EditorButton(Node parent, string name, string text, Action pressed, int height, string? hint = null)
    {
        var button = ArenaPresentation.Button(parent, text, pressed);
        button.Name = name;
        button.CustomMinimumSize = new(0, height);
        button.TooltipText = hint ?? "";
        button.AccessibilityName = hint ?? text;
        return button;
    }

    private static StyleBoxFlat TabStyle(bool selected)
    {
        var style = RelickVisuals.Frame(selected ? new Color("302342") : EditorSurface, selected ? BoardArt.Gold : EditorEdge, 2);
        style.BorderWidthBottom = selected ? 2 : 1;
        style.ContentMarginLeft = style.ContentMarginRight = 4;
        style.ContentMarginTop = style.ContentMarginBottom = 3;
        return style;
    }
}

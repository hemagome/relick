using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

// Mis mazos as a showcase: a cover per deck and, beside them, the chosen one with what can be done to it.
public partial class DeckScreen
{
    private HFlowContainer? _tiles;
    private PanelContainer? _detail;
    private ulong? _selectedDeck;
    private Button? _create;
    private Label? _deckCount;
    private readonly Dictionary<ulong, Button> _deckTiles = [];
    private bool _busy;

    /// <summary>«Estilo» asks the shell for the wardrobe, opened on that deck.</summary>
    public event Action<ulong>? StyleRequested;

    private void ShowList()
    {
        Clear();
        FullScreenChanged?.Invoke(false);
        var column = new VBoxContainer { Name = "DeckListScreen" };
        column.AddThemeConstantOverride("separation", ShortScreen ? 6 : 16); _margin.AddChild(column);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); column.AddChild(header);
        var title = ArenaPresentation.Heading("Mis mazos", ShortScreen ? MenuVisuals.Type.Title : MenuVisuals.Type.Hero, BoardArt.Ink);
        header.AddChild(title);
        _deckCount = ArenaPresentation.Text("", MenuVisuals.Type.Body, BoardArt.Muted);
        _deckCount.Name = "DeckCount"; _deckCount.SizeFlagsVertical = SizeFlags.ShrinkCenter; header.AddChild(_deckCount);
        _status = ArenaPresentation.Text("", MenuVisuals.Type.Body, ArenaPresentation.Coral); _status.Name = "DecksStatus";
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart; _status.Visible = false; column.AddChild(_status);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", ShortScreen ? 8 : 20); column.AddChild(body);
        var scroll = new ScrollContainer { Name = "DeckRows", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        body.AddChild(scroll);
        _tiles = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tiles.AddThemeConstantOverride("h_separation", ShortScreen ? 6 : 14);
        _tiles.AddThemeConstantOverride("v_separation", ShortScreen ? 6 : 14);
        scroll.AddChild(_tiles);
        _detail = new PanelContainer { Name = "DeckDetail", CustomMinimumSize = new(ShortScreen ? 272 : GetViewportRect().Size.X < 1200 ? 360 : 440, 0) };
        var style = RelickVisuals.Frame(MenuVisuals.Tone.Surface, MenuVisuals.Tone.Edge, MenuVisuals.Cut.Panel);
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = ShortScreen ? 10 : 20;
        _detail.AddThemeStyleboxOverride("panel", style);
        body.AddChild(_detail);
        _shownRevision = -1;
        PresentList();
    }

    private void PresentList()
    {
        if (_tiles == null) return;
        var pending = Connection.DeckPending; var error = Connection.DeckError;
        if (_shownRevision == Connection.DeckRevision && _shownPending == pending && _shownError == error) return;
        _shownRevision = Connection.DeckRevision; _shownPending = pending; _shownError = error;
        _status!.Text = error; _status.Visible = error.Length > 0;
        foreach (var child in _tiles.GetChildren()) { _tiles.RemoveChild(child); child.QueueFree(); }
        _deckTiles.Clear();
        var decks = Decks;
        _busy = pending || PreviewDecks == null && !Connection.Ready;
        var active = decks.FirstOrDefault(deck => deck.Active);
        _deckCount!.Text = Guest ? "" : $"{decks.Count} de 20";
        if (_selectedDeck is not { } selected || selected != 0 && decks.All(deck => deck.Id != selected)) _selectedDeck = active?.Id ?? 0;

        AddTile(0, "Training", "Entrenamiento", active == null ? "En uso" : "Siempre listo", active == null,
            active == null ? BoardArt.Gold : BoardArt.Muted, CoverArt(null, RelikarioCatalog.Yuno.Id));
        foreach (var deck in decks)
        {
            var cards = DeckRules.NormalizeAttackArt(deck.Relikario, deck.Cards);
            var draft = DeckRules.Validate(deck.Relikario, cards, deck.Traps, deck.Side).Count > 0;
            // A draft reads as progress; the detail says exactly what is missing.
            var state = draft ? $"Borrador {cards.Count}/{DeckRules.MainDeckSize}" : deck.Active ? "En uso" : "Listo";
            AddTile(deck.Id, "Deck" + deck.Id, deck.Name, state, deck.Active,
                draft ? ArenaPresentation.Coral : deck.Active ? BoardArt.Gold : BoardArt.Muted, CoverArt(deck, deck.Relikario));
        }
        AddNewTile(decks.Count);
        PresentDetail();
    }

    private Vector2 TileSize => ShortScreen ? new(140, 148) : new(196, 260);

    private Texture2D? CoverArt(DeckInfo? deck, string relikario)
    {
        if (deck == null) return RelikarioArt.Portrait(relikario);
        var cover = PreviewDecks == null ? Connection.DeckCover(deck.Id) : "";
        return cover.Length > 0 && (deck.Cards.Contains(cover) || deck.Traps.Contains(cover)) && CardCatalog.All.TryGetValue(cover, out var card)
            ? BoardArt.LoadCardArtwork(card, relikario) : RelikarioArt.Portrait(relikario);
    }

    private void AddTile(ulong id, string key, string name, string state, bool active, Color stateColor, Texture2D? cover)
    {
        var tile = new Button { Name = key, CustomMinimumSize = TileSize, TooltipText = name, AccessibilityName = $"{name}. {state}",
            MouseDefaultCursorShape = CursorShape.PointingHand };
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 2); tile.AddChild(content);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, ShortScreen ? 6 : 10);
        content.AddChild(new TextureRect { Name = "RelikarioThumbnail", Texture = cover, MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ExpandFill, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var title = ArenaPresentation.Text(name, ShortScreen ? MenuVisuals.Type.Body : MenuVisuals.Type.Control);
        title.Name = "DeckTitle"; title.ClipText = true; title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        var status = ArenaPresentation.Text(state, MenuVisuals.Type.Caption, stateColor);
        status.Name = active ? "ActiveBadge" : "DeckState";
        foreach (var label in new[] { title, status }) { label.MouseFilter = MouseFilterEnum.Ignore; content.AddChild(label); }
        tile.Pressed += () => SelectDeck(id);
        tile.SetMeta("active", active);
        _tiles!.AddChild(tile);
        _deckTiles.Add(id, tile);
    }

    private void AddNewTile(int count)
    {
        var text = Guest ? "Inicia sesión\npara crear mazos" : "Nuevo mazo";
        _create = new Button { Name = "NewDeck", CustomMinimumSize = TileSize, AccessibilityName = text.Replace('\n', ' '),
            Disabled = Guest || _busy || count >= 20, TooltipText = Guest ? GuestNotice : count >= 20 ? "Puedes guardar hasta 20 mazos." : "" };
        var empty = new DashedSlot { Plus = !Guest, Text = text };
        _create.AddChild(empty);
        empty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) _create.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        _create.Pressed += ChooseRelikario;
        _tiles!.AddChild(_create);
    }

    internal void SelectDeck(ulong id)
    {
        _selectedDeck = id;
        PresentDetail();
    }

    private void PresentDetail()
    {
        if (_detail == null) return;
        foreach (var child in _detail.GetChildren()) { _detail.RemoveChild(child); child.QueueFree(); }
        foreach (var (id, tile) in _deckTiles)
        {
            var chosen = id == _selectedDeck;
            var active = tile.GetMeta("active").AsBool();
            foreach (var state in new[] { "normal", "hover", "pressed" })
            {
                var box = RelickVisuals.Frame(chosen ? MenuVisuals.Tone.Chosen : state == "hover" ? MenuVisuals.Tone.Raised : MenuVisuals.Tone.Surface,
                    active || chosen ? BoardArt.Gold : MenuVisuals.Tone.Edge, MenuVisuals.Cut.Panel, chosen ? 2 : 1);
                tile.AddThemeStyleboxOverride(state, box);
            }
        }

        var saved = Decks.FirstOrDefault(deck => deck.Id == _selectedDeck);
        var relikario = saved?.Relikario ?? RelikarioCatalog.Yuno.Id;
        var cards = saved != null ? DeckRules.NormalizeAttackArt(relikario, saved.Cards) : CardCatalog.TrainingDeck();
        var traps = saved?.Traps ?? CardCatalog.TrainingTraps();
        var side = saved?.Side ?? [];
        var problem = saved != null ? DeckRules.Validate(relikario, cards, traps, side).FirstOrDefault() : null;
        var inUse = saved?.Active ?? Decks.All(deck => !deck.Active);

        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", ShortScreen ? 4 : 12); _detail.AddChild(column);
        var name = ArenaPresentation.Text(saved?.Name ?? "Mazo de entrenamiento", ShortScreen ? MenuVisuals.Type.Control : MenuVisuals.Type.Title);
        name.Name = "DetailTitle"; name.ClipText = true; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(name);

        var sleeve = CosmeticCatalog.Sleeve(saved?.Sleeve);
        var dice = CosmeticCatalog.DiceFor(saved?.Dice);
        var art = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        art.AddThemeConstantOverride("separation", ShortScreen ? 6 : 14); column.AddChild(art);
        static TextureRect Picture(string name, Texture2D? texture, float ratio) => new()
        {
            Name = name, Texture = texture, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = ratio,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        art.AddChild(Picture("DetailCard", RelikarioArt.Card(relikario, 1), 1.4f));
        var pieces = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; art.AddChild(pieces);
        var back = Picture("DeckSleeve", SleeveArt.Back(sleeve.Id), 1); back.SizeFlagsVertical = SizeFlags.ExpandFill;
        pieces.AddChild(back);
        var dieSize = ShortScreen ? 32 : 60;
        pieces.AddChild(new DiceSwatch { Name = "DeckDice", Style = dice.Id, CustomMinimumSize = new(dieSize, dieSize),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter });

        void Line(string key, string text, Color color)
        {
            var label = ArenaPresentation.Text(text, ShortScreen ? MenuVisuals.Type.Caption : MenuVisuals.Type.Body, color);
            label.Name = key; label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(label);
        }
        Line("DeckStyleNames", $"Mica {sleeve.Name} · Dado {dice.Name}", BoardArt.Muted);
        if (Guest) Line("GuestNotice", GuestNotice, MenuVisuals.Tone.Reading);
        else if (problem != null) Line("DeckProblem", $"Borrador: {problem}", ArenaPresentation.Coral);
        else if (!ShortScreen) Line("DeckContents", $"{cards.Count} en el principal · {traps.Count} Trampas · {side.Count} en el banquillo", BoardArt.Muted);

        var main = new HBoxContainer(); main.AddThemeConstantOverride("separation", 8); column.AddChild(main);
        var copyable = !Guest && Decks.Count < 20;
        if (!inUse)
        {
            var use = ArenaPresentation.Button(main, "Usar", () => Connection.SetActiveDeck(saved?.Id ?? 0), _busy || problem != null);
            use.Name = "Use"; use.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            use.TooltipText = problem == null ? "" : "Completa el mazo para poder jugarlo.";
            if (problem == null) RelickVisuals.Primary(use);
        }
        if (saved != null)
        {
            var edit = ArenaPresentation.Button(main, "Editar",
                () => Edit(saved.Id, saved.Name, saved.Cards, saved.Traps, saved.Side, saved.Relikario, saved.Sleeve, saved.Dice), _busy);
            edit.Name = "Edit"; edit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (inUse || problem != null) RelickVisuals.Primary(edit);
        }
        else if (copyable)
        {
            var copy = ArenaPresentation.Button(main, "Duplicar", () => Edit(0, "Copia de entrenamiento", CardCatalog.TrainingDeck(), CardCatalog.TrainingTraps()), _busy);
            copy.Name = "Copy"; copy.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (inUse) RelickVisuals.Primary(copy);
        }
        if (saved == null) return;

        var secondary = new HBoxContainer { Name = "DeckActions" };
        secondary.AddThemeConstantOverride("separation", 8); column.AddChild(secondary);
        void Secondary(string key, string caption, GameIcon symbol, Action pressed, bool disabled)
        {
            // The narrow detail of a short screen keeps the symbols alone.
            Button button;
            if (ShortScreen)
            {
                var icon = new FramedIconButton { Symbol = symbol };
                icon.Pressed += pressed; secondary.AddChild(icon); button = icon;
            }
            else button = ArenaPresentation.Button(secondary, caption, pressed);
            button.Name = key; button.Disabled = _busy || disabled; button.TooltipText = button.AccessibilityName = caption;
            button.CustomMinimumSize = new(MenuVisuals.TouchTarget, MenuVisuals.TouchTarget);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (key == "Delete") button.AddThemeColorOverride("font_color", ArenaPresentation.Coral);
        }
        Secondary("Style", "Estilo", GameIcon.Brush, () => StyleRequested?.Invoke(saved.Id), false);
        Secondary("Copy", "Duplicar", GameIcon.Copy,
            () => Edit(0, Shorten("Copia de " + saved.Name), saved.Cards, saved.Traps, saved.Side, saved.Relikario, saved.Sleeve, saved.Dice), !copyable);
        Secondary("Delete", "Borrar", GameIcon.Trash, () => ConfirmDelete(saved), false);
    }
}

/// <summary>An empty place: dashed outline, a plus and a caption.</summary>
public partial class DashedSlot : Control
{
    public bool Plus { get; init; }
    public string Text { get; init; } = "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        var label = ArenaPresentation.Text(Text, MenuVisuals.Type.Body, MenuVisuals.Tone.Reading);
        label.Name = "SlotText"; label.MouseFilter = MouseFilterEnum.Ignore;
        label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
        AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 8);
        if (Plus) label.OffsetTop = 44;
    }

    public override void _Draw()
    {
        var outline = BoardArt.Octagon(new Rect2(Vector2.Zero, Size).Grow(-1), MenuVisuals.Cut.Panel, MenuVisuals.Cut.Panel);
        DrawColoredPolygon(outline, BoardArt.Tint(MenuVisuals.Tone.Surface, 0.6f));
        for (var i = 0; i < outline.Length; i++) DrawDashedLine(outline[i], outline[(i + 1) % outline.Length], MenuVisuals.Tone.Edge, 1, 6);
        if (!Plus) return;
        var c = new Vector2(Size.X / 2, Size.Y / 2 - 18);
        DrawLine(c - new Vector2(14, 0), c + new Vector2(14, 0), BoardArt.Ink, 3);
        DrawLine(c - new Vector2(0, 14), c + new Vector2(0, 14), BoardArt.Ink, 3);
    }
}

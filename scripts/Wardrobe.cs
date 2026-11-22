using Godot;
using Relick.Core;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

public enum WardrobeCategory { Avatar, Frame, Title, Playmat, Backdrop, Hero, Cover, Sleeve, Dice }

/// <summary>Everything a player can personalize, over a preview of how it looks. Nothing sticks until Guardar.</summary>
public partial class Wardrobe : Control
{
    private sealed record Choices(string Avatar, string Frame, string Title, string Playmat, string Backdrop, string Hero);
    private sealed record DeckChoices(string Cover, string Sleeve, string Dice);

    public LobbyConnection Connection { get; init; } = null!;
    public string PlayerName { get; init; } = "";
    /// <summary>Where it opens: «Estilo» in Mis mazos opens it on a deck's sleeve.</summary>
    public WardrobeCategory StartAt { get; init; } = WardrobeCategory.Avatar;
    public ulong StartDeck { get; init; }
    /// <summary>Profile and decks to show instead of the server's, for tests and previews.</summary>
    internal ProfileView? PreviewProfile { get; init; }
    internal IReadOnlyList<DeckInfo>? PreviewDecks { get; init; }
    /// <summary>Receives avatar, title, frame and playmat instead of the server, for tests.</summary>
    internal Action<string, string, string, string>? SavedProfile { get; init; }
    /// <summary>The backdrop being tried on, so the screen behind can wear it.</summary>
    public event Action<string>? BackdropTried;

    private Choices _saved = null!, _draft = null!;
    private readonly Dictionary<ulong, DeckChoices> _savedDecks = [], _draftDecks = [];
    private readonly Queue<DeckInfo> _styleQueue = [];
    private ulong _deck;
    private WardrobeCategory _category;
    private bool _saving, _savedADeck;
    private string _saveError = "", _hint = "";
    private int _shownRevision = -1;

    private readonly Dictionary<WardrobeCategory, Button> _categories = [];
    private MarginContainer _margin = null!;
    private WardrobeStage _stage = null!;
    private HBoxContainer _deckRow = null!, _options = null!;
    private ScrollContainer _optionScroll = null!;
    private OptionButton _deckPicker = null!;
    private Label _note = null!;
    private Button _save = null!, _discard = null!;

    private bool Short => GetViewportRect().Size.Y < 600;
    private bool Offline => PreviewProfile != null;
    private IReadOnlyList<DeckInfo> Decks => PreviewDecks ?? Connection.Decks;
    internal WardrobeCategory Category => _category;
    internal ProfileView Preview => _stage.View;
    internal bool Dirty => _draft != _saved || _draftDecks.Any(pair => _savedDecks.GetValueOrDefault(pair.Key) != pair.Value);

    public override void _Ready()
    {
        Name = "Wardrobe";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var compact = Short;
        _margin = new MarginContainer(); AddChild(_margin);
        _margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += AdjustMargins; AdjustMargins();

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", compact ? 8 : 18); _margin.AddChild(row);
        var side = new PanelContainer { Name = "WardrobeCategories", CustomMinimumSize = new(compact ? 140 : 250, 0) };
        var sideStyle = RelickVisuals.Frame(MenuVisuals.Tone.Surface, MenuVisuals.Tone.Edge, MenuVisuals.Cut.Panel);
        sideStyle.ContentMarginLeft = sideStyle.ContentMarginRight = sideStyle.ContentMarginTop = sideStyle.ContentMarginBottom = compact ? 6 : 14;
        side.AddThemeStyleboxOverride("panel", sideStyle); row.AddChild(side);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        side.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", compact ? 2 : 6); scroll.AddChild(list);
        var group = new ButtonGroup();
        void Section(string caption, params (WardrobeCategory Category, string Title)[] entries)
        {
            list.AddChild(ArenaPresentation.Text(caption, MenuVisuals.Type.Caption, BoardArt.Muted));
            foreach (var (category, title) in entries)
            {
                var button = new Button { Name = "Category" + category, Text = title, ToggleMode = true, ButtonGroup = group,
                    Alignment = HorizontalAlignment.Left, CustomMinimumSize = new(0, MenuVisuals.TouchTarget), ClipText = true };
                button.AddThemeFontSizeOverride("font_size", compact ? MenuVisuals.Type.Body : MenuVisuals.Type.Control);
                button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginLeft = 12 });
                var chosen = RelickVisuals.Frame(MenuVisuals.Tone.Chosen, BoardArt.Gold, MenuVisuals.Cut.Control);
                button.AddThemeStyleboxOverride("pressed", chosen);
                button.AddThemeStyleboxOverride("hover_pressed", chosen);
                button.AddThemeColorOverride("font_pressed_color", BoardArt.Gold);
                button.AddThemeColorOverride("font_hover_pressed_color", BoardArt.Gold);
                button.Pressed += () => ShowCategory(category);
                list.AddChild(button);
                _categories.Add(category, button);
            }
        }
        Section("DE TU PERFIL", (WardrobeCategory.Avatar, "Retrato"), (WardrobeCategory.Frame, "Marco"), (WardrobeCategory.Title, "Título"), (WardrobeCategory.Playmat, "Tapete"));
        Section("DE TU INICIO", (WardrobeCategory.Hero, "Relikario"), (WardrobeCategory.Backdrop, "Fondo"));
        Section("DEL MAZO", (WardrobeCategory.Cover, "Portada"), (WardrobeCategory.Sleeve, "Mica"), (WardrobeCategory.Dice, "Dado"));

        var main = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        main.AddThemeConstantOverride("separation", compact ? 6 : 14); row.AddChild(main);
        _stage = new WardrobeStage { Compact = compact, SizeFlagsVertical = SizeFlags.ExpandFill };
        main.AddChild(_stage);
        _deckRow = new HBoxContainer { Name = "WardrobeDeckRow" };
        _deckRow.AddThemeConstantOverride("separation", 10); main.AddChild(_deckRow);
        var deckCaption = ArenaPresentation.Text("Mazo", MenuVisuals.Type.Body, BoardArt.Muted);
        deckCaption.SizeFlagsVertical = SizeFlags.ShrinkCenter; _deckRow.AddChild(deckCaption);
        _deckPicker = new OptionButton { Name = "WardrobeDeck", CustomMinimumSize = new(0, MenuVisuals.TouchTarget),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false };
        ArenaViewport.TouchMenu(_deckPicker.GetPopup());
        _deckPicker.ItemSelected += index => { _deck = Decks[(int)index].Id; BuildOptions(); Present(); };
        _deckRow.AddChild(_deckPicker);
        _optionScroll = new ScrollContainer { Name = "WardrobeOptions", VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true, CustomMinimumSize = new(0, compact ? 88 : 150) };
        main.AddChild(_optionScroll);
        _options = new HBoxContainer(); _options.AddThemeConstantOverride("separation", compact ? 6 : 10); _optionScroll.AddChild(_options);
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 8); main.AddChild(footer);
        _note = ArenaPresentation.Text("", MenuVisuals.Type.Body, BoardArt.Muted); _note.Name = "WardrobeNote";
        _note.SizeFlagsHorizontal = SizeFlags.ExpandFill; _note.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _note.ClipText = true; _note.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        footer.AddChild(_note);
        _discard = ArenaPresentation.Button(footer, "Descartar", Discard); _discard.Name = "WardrobeDiscard";
        _save = ArenaPresentation.Button(footer, compact ? "Guardar" : "Guardar cambios", Save); _save.Name = "WardrobeSave";
        _save.CustomMinimumSize = new(compact ? 120 : 220, MenuVisuals.TouchTarget);
        RelickVisuals.Primary(_save);

        _deck = StartDeck;
        _category = StartAt;
        Load();
    }

    private void AdjustMargins()
    {
        var compact = Short;
        var side = (int)Math.Max(compact ? 8 : 24, (Size.X - 1320) / 2);
        foreach (var edge in new[] { "left", "right" }) _margin.AddThemeConstantOverride("margin_" + edge, side);
        foreach (var edge in new[] { "top", "bottom" }) _margin.AddThemeConstantOverride("margin_" + edge, compact ? 8 : 24);
    }

    public override void _Process(double delta)
    {
        if (Offline) return;
        if (_saving) { AdvanceSave(); return; }
        // What the server says replaces the screen only while the player has nothing pending on it.
        var revision = Revision();
        if (revision != _shownRevision && !Dirty) Load();
    }

    private int Revision() => HashCode.Combine(Connection.ProfileRevision, Connection.PersonalizationRevision, Connection.DeckRevision, Connection.UnlockRevision, Connection.Ready);

    private void Load()
    {
        var profile = PreviewProfile ?? (Connection.Profile is { } info ? ProfileView.From(info) : ProfileView.Default(PlayerName));
        _saved = new(ProfileCatalog.Avatar(profile.Avatar), ProfileCatalog.Frame(profile.Frame).Id, ProfileCatalog.Title(profile.Title).Id,
            ProfileCatalog.Playmat(profile.Playmat).Id, Offline ? ProfileCatalog.DefaultPlaymat : Connection.HomeBackdrop, Offline ? "" : Connection.HomeHero);
        _draft = _saved;
        _savedDecks.Clear(); _draftDecks.Clear();
        foreach (var deck in Decks)
            _savedDecks[deck.Id] = new(Offline ? "" : Connection.DeckCover(deck.Id), CosmeticCatalog.Sleeve(deck.Sleeve).Id, CosmeticCatalog.DiceFor(deck.Dice).Id);
        if (Decks.All(deck => deck.Id != _deck)) _deck = Decks.FirstOrDefault(deck => deck.Active)?.Id ?? Decks.FirstOrDefault()?.Id ?? 0;
        _deckPicker.Clear();
        for (var i = 0; i < Decks.Count; i++)
        {
            _deckPicker.AddItem(Decks[i].Name, i);
            if (Decks[i].Id == _deck) _deckPicker.Select(i);
        }
        foreach (var (category, button) in _categories)
        {
            var reason = Unavailable(category);
            button.Disabled = reason.Length > 0;
            button.TooltipText = reason;
        }
        if (Unavailable(_category).Length > 0)
            _category = _categories.Keys.Where(category => Unavailable(category).Length == 0).DefaultIfEmpty(_category).First();
        if (!Offline) _shownRevision = Revision();
        ShowCategory(_category);
    }

    private string Unavailable(WardrobeCategory category)
    {
        const string notYet = "Próximamente: el servidor todavía no ofrece esta opción.";
        if (Offline) return "";
        if (category is WardrobeCategory.Cover or WardrobeCategory.Sleeve or WardrobeCategory.Dice && Decks.Count == 0)
            return "Guarda un mazo para personalizarlo.";
        if (category is WardrobeCategory.Sleeve or WardrobeCategory.Dice) return "";
        if (category is WardrobeCategory.Cover or WardrobeCategory.Backdrop or WardrobeCategory.Hero)
            return Connection.PersonalizationAvailable ? "" : notYet;
        return Connection.ProfilesAvailable ? "" : notYet;
    }

    internal void ShowCategory(WardrobeCategory category)
    {
        _category = category; _hint = "";
        _categories[category].SetPressedNoSignal(true);
        foreach (var (key, button) in _categories) if (key != category) button.SetPressedNoSignal(false);
        _deckRow.Visible = category is WardrobeCategory.Cover or WardrobeCategory.Sleeve or WardrobeCategory.Dice;
        BuildOptions();
        Present();
    }

    private DeckInfo? CurrentDeck => Decks.FirstOrDefault(deck => deck.Id == _deck);
    private DeckChoices StyleOfDeck =>
        _draftDecks.GetValueOrDefault(_deck) ?? _savedDecks.GetValueOrDefault(_deck) ?? new("", CosmeticCatalog.DefaultSleeve, CosmeticCatalog.DefaultDice);

    private string Current => _category switch
    {
        WardrobeCategory.Avatar => _draft.Avatar,
        WardrobeCategory.Frame => _draft.Frame,
        WardrobeCategory.Title => _draft.Title,
        WardrobeCategory.Playmat => _draft.Playmat,
        WardrobeCategory.Backdrop => _draft.Backdrop,
        WardrobeCategory.Hero => _draft.Hero,
        WardrobeCategory.Cover => StyleOfDeck.Cover,
        WardrobeCategory.Sleeve => StyleOfDeck.Sleeve,
        _ => StyleOfDeck.Dice
    };

    private IEnumerable<(string Id, string Name)> Options() => _category switch
    {
        WardrobeCategory.Avatar => ProfileCatalog.Avatars.Select(id => (id, RelikarioCatalog.Find(id)!.DisplayName)),
        WardrobeCategory.Frame => ProfileCatalog.Frames.Select(frame => (frame.Id, frame.Name)),
        WardrobeCategory.Title => ProfileCatalog.Titles.Select(title => (title.Id, title.Name)),
        WardrobeCategory.Playmat or WardrobeCategory.Backdrop => ProfileCatalog.Playmats.Select(mat => (mat.Id, mat.Name)),
        WardrobeCategory.Hero => ProfileCatalog.Avatars.Select(id => (id, RelikarioCatalog.Find(id)!.DisplayName)).Prepend(("", "El del mazo")),
        WardrobeCategory.Cover => CurrentDeck is { } deck
            ? deck.Cards.Concat(deck.Traps).Distinct().Where(CardCatalog.All.ContainsKey).Select(id => (id, CardCatalog.Get(id).Name)).Prepend(("", "Su Relikario"))
            : [],
        WardrobeCategory.Sleeve => CosmeticCatalog.Sleeves.Select(sleeve => (sleeve.Id, sleeve.Name)),
        _ => CosmeticCatalog.Dice.Select(die => (die.Id, die.Name))
    };

    private void BuildOptions()
    {
        foreach (var child in _options.GetChildren()) { _options.RemoveChild(child); child.QueueFree(); }
        var compact = Short;
        foreach (var (id, label) in Options())
        {
            var option = id;
            var tile = new Button { Name = "Option_" + (id.Length > 0 ? id : "default"), TooltipText = label, AccessibilityName = label,
                CustomMinimumSize = new(compact ? 92 : 124, compact ? 76 : 134), MouseDefaultCursorShape = CursorShape.PointingHand };
            var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            content.AddThemeConstantOverride("separation", 2); tile.AddChild(content);
            content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 6);
            if (Picture(id) is { } picture)
            {
                picture.MouseFilter = MouseFilterEnum.Ignore; picture.SizeFlagsVertical = SizeFlags.ExpandFill;
                content.AddChild(picture);
            }
            var text = ArenaPresentation.Text(label, compact ? MenuVisuals.Type.Caption : MenuVisuals.Type.Body);
            text.Name = "OptionName"; text.HorizontalAlignment = HorizontalAlignment.Center; text.MouseFilter = MouseFilterEnum.Ignore;
            if (_category == WardrobeCategory.Title)
            {
                text.SizeFlagsVertical = SizeFlags.ExpandFill; text.VerticalAlignment = VerticalAlignment.Center;
                text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            }
            else { text.ClipText = true; text.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; }
            content.AddChild(text);
            if (Locked(id) != null)
            {
                // Earned ones stay pressable: a press is how a finger learns what they ask for.
                content.Modulate = new Color(1, 1, 1, 0.45f);
                text.Text = "Se gana";
                tile.TooltipText = $"{label}: {Locked(id)!.HowToEarn}";
            }
            tile.Pressed += () => Choose(option);
            _options.AddChild(tile);
        }
        _optionScroll.ScrollHorizontal = 0;
        MarkChosen();
    }

    /// <summary>The reward still to be earned behind an option, or null when it can be worn.</summary>
    private Reward? Locked(string id)
    {
        RewardKind? kind = _category switch
        {
            WardrobeCategory.Title => RewardKind.Title,
            WardrobeCategory.Frame => RewardKind.Frame,
            WardrobeCategory.Sleeve => RewardKind.Sleeve,
            WardrobeCategory.Dice => RewardKind.Dice,
            _ => null
        };
        return kind is { } earned && RewardCatalog.Of(earned, id) is { } reward && (Offline || !Connection.Owns(earned, id)) ? reward : null;
    }

    private Control? Picture(string id)
    {
        static TextureRect Art(Texture2D? texture) => new()
        {
            Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        var deck = CurrentDeck;
        return _category switch
        {
            WardrobeCategory.Avatar => Art(RelikarioArt.Thumbnail(id)),
            WardrobeCategory.Frame => new ProfileAvatar { Avatar = _draft.Avatar, Frame = id },
            WardrobeCategory.Title => null,
            WardrobeCategory.Playmat or WardrobeCategory.Backdrop => new PlaymatSwatch { Playmat = id },
            WardrobeCategory.Hero => Art(RelikarioArt.Thumbnail(id.Length > 0 ? id : deck?.Relikario ?? RelikarioCatalog.Yuno.Id)),
            WardrobeCategory.Cover => Art(id.Length > 0
                ? BoardArt.LoadCardArtwork(CardCatalog.Get(id), deck!.Relikario)
                : RelikarioArt.Thumbnail(deck?.Relikario)),
            WardrobeCategory.Sleeve => Art(SleeveArt.Back(id)),
            _ => new DiceSwatch { Style = id }
        };
    }

    private void MarkChosen()
    {
        var current = Current;
        foreach (var tile in _options.GetChildren().OfType<Button>())
        {
            var chosen = tile.Name == "Option_" + (current.Length > 0 ? current : "default");
            if (chosen) tile.AddThemeStyleboxOverride("normal", RelickVisuals.Frame(MenuVisuals.Tone.Chosen, BoardArt.Gold, MenuVisuals.Cut.Control, 2));
            else tile.RemoveThemeStyleboxOverride("normal");
        }
    }

    internal void Choose(string id)
    {
        if (_saving) return;
        if (Locked(id) is { } reward)
        {
            _hint = reward.HowToEarn;
            Present();
            return;
        }
        _hint = "";
        var deck = StyleOfDeck;
        switch (_category)
        {
            case WardrobeCategory.Avatar: _draft = _draft with { Avatar = id }; break;
            case WardrobeCategory.Frame: _draft = _draft with { Frame = id }; break;
            case WardrobeCategory.Title: _draft = _draft with { Title = id }; break;
            case WardrobeCategory.Playmat: _draft = _draft with { Playmat = id }; break;
            case WardrobeCategory.Backdrop: _draft = _draft with { Backdrop = id }; break;
            case WardrobeCategory.Hero: _draft = _draft with { Hero = id }; break;
            case WardrobeCategory.Cover: _draftDecks[_deck] = deck with { Cover = id }; break;
            case WardrobeCategory.Sleeve: _draftDecks[_deck] = deck with { Sleeve = id }; break;
            default: _draftDecks[_deck] = deck with { Dice = id }; break;
        }
        _saveError = "";
        MarkChosen();
        Present();
    }

    private void Present()
    {
        var deck = CurrentDeck;
        var style = StyleOfDeck;
        var own = PreviewProfile ?? (Connection.Profile is { } info ? ProfileView.From(info) : ProfileView.Default(PlayerName));
        if (PlayerName.Trim().Length > 0) own = own with { Name = PlayerName.Trim() };
        var view = own with { Avatar = _draft.Avatar, Frame = _draft.Frame, Title = _draft.Title, Playmat = _draft.Playmat };
        var home = _category is WardrobeCategory.Backdrop or WardrobeCategory.Hero;
        var relikario = home && _draft.Hero.Length > 0 ? _draft.Hero : deck?.Relikario ?? RelikarioCatalog.Yuno.Id;
        var cover = _category == WardrobeCategory.Cover && style.Cover.Length > 0 && CardCatalog.All.TryGetValue(style.Cover, out var card)
            ? BoardArt.LoadCardArtwork(card, relikario) : RelikarioArt.Card(relikario, 1);
        _stage.Present(view, home, home ? _draft.Backdrop : _draft.Playmat, cover, style.Sleeve, style.Dice,
            home ? "ASÍ SE VERÁ TU INICIO" : _category == WardrobeCategory.Cover ? "ASÍ SE VERÁ TU MAZO" : "ASÍ TE VERÁ TU RIVAL");
        BackdropTried?.Invoke(_draft.Backdrop);
        var dirty = Dirty;
        _save.Disabled = _saving || !dirty || !Offline && !Connection.Ready;
        _discard.Disabled = _saving || !dirty;
        _save.Text = _saving ? "Guardando…" : Short ? "Guardar" : "Guardar cambios";
        _note.Text = _saveError.Length > 0 ? _saveError : _hint.Length > 0 ? _hint : dirty && !_saving ? "Cambios sin guardar" : "";
        _note.TooltipText = _note.Text;
        _note.AddThemeColorOverride("font_color", _saveError.Length > 0 ? ArenaPresentation.Coral : RelickVisuals.State.Target);
    }

    internal void Discard()
    {
        if (_saving) return;
        _draft = _saved; _draftDecks.Clear(); _saveError = "";
        BuildOptions();
        Present();
    }

    internal void Save()
    {
        if (_saving || !Dirty) return;
        if (SavedProfile != null)
        {
            SavedProfile(_draft.Avatar, _draft.Title, _draft.Frame, _draft.Playmat);
            _saved = _draft;
            foreach (var (id, style) in _draftDecks) _savedDecks[id] = style;
            _draftDecks.Clear();
            Present();
            return;
        }
        if (!Connection.Ready) { _saveError = "No hay conexión con el servidor."; Present(); return; }
        _saveError = ""; _savedADeck = false;
        Connection.TakeProfileError(); Connection.TakePersonalizationError();
        if ((_draft.Avatar, _draft.Frame, _draft.Title, _draft.Playmat) != (_saved.Avatar, _saved.Frame, _saved.Title, _saved.Playmat))
            Connection.SetProfile(_draft.Avatar, _draft.Title, _draft.Frame, _draft.Playmat);
        if ((_draft.Backdrop, _draft.Hero) != (_saved.Backdrop, _saved.Hero)) Connection.SetHomeStyle(_draft.Hero, _draft.Backdrop);
        _styleQueue.Clear();
        foreach (var (id, style) in _draftDecks)
        {
            if (Decks.FirstOrDefault(deck => deck.Id == id) is not { } deck) continue;
            var saved = _savedDecks[id];
            if (style.Cover != saved.Cover) Connection.SetDeckCover(id, style.Cover);
            if (style.Sleeve != saved.Sleeve || style.Dice != saved.Dice) _styleQueue.Enqueue(deck);
        }
        _saving = true;
        Present();
        AdvanceSave();
    }

    // A deck's sleeve and die travel with the deck, and the server takes one deck at a time.
    private void AdvanceSave()
    {
        if (!Connection.Ready) { EndSave("Se perdió la conexión antes de confirmar los cambios."); return; }
        if (Connection.DeckPending) return;
        if (_savedADeck && Connection.DeckError.Length > 0) { EndSave(Connection.DeckError); return; }
        if (_styleQueue.TryDequeue(out var deck))
        {
            var style = _draftDecks[deck.Id];
            _savedADeck = true;
            Connection.SaveDeck(deck.Id, deck.Name, deck.Relikario, deck.Cards, deck.Traps, deck.Side, style.Sleeve, style.Dice);
            return;
        }
        if (Connection.ProfilePending || Connection.PersonalizationPending) return;
        var error = Connection.TakeProfileError();
        if (error.Length == 0) error = Connection.TakePersonalizationError();
        EndSave(error);
    }

    private void EndSave(string error)
    {
        _saving = false;
        _styleQueue.Clear();
        _saveError = error;
        if (error.Length > 0) { Present(); return; }
        GetNodeOrNull<SoundDirector>("/root/SoundDirector")?.Play(SoundCue.DeckSaved);
        Load();
    }
}

/// <summary>A cloth's colours and circuits, small.</summary>
public partial class PlaymatSwatch : Control
{
    public string Playmat { get; init; } = ProfileCatalog.DefaultPlaymat;

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }

    public override void _Draw()
    {
        var mat = ProfilePlaymats.Of(Playmat);
        var rect = new Rect2(Vector2.Zero, Size);
        BoardArt.Plate(this, rect, mat.Raised, mat.Frame, MenuVisuals.Cut.Control, 2);
        DrawLine(new(8, Size.Y * 0.35f), new(Size.X - 8, Size.Y * 0.35f), mat.Trace, 2);
        DrawLine(new(Size.X * 0.5f, Size.Y * 0.35f), new(Size.X * 0.5f, Size.Y - 8), mat.Trace, 2);
    }
}

/// <summary>The fitting room: the cloth, with the player and their cards on it.</summary>
public partial class WardrobeStage : Control
{
    public bool Compact { get; init; }
    public ProfileView View { get; private set; } = ProfileView.Default("");

    private RelickPlaymat _mat = RelickPlaymat.Classic;
    private MarginContainer _margin = null!;
    private Label _caption = null!, _name = null!, _title = null!;
    private ProfileAvatar _avatar = null!;
    private VBoxContainer _identity = null!, _piece = null!;
    private TextureRect _card = null!, _sleeve = null!;
    private DiceSwatch? _die;
    private Control _dieSpace = null!;

    public override void _Ready()
    {
        Name = "WardrobeStage";
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        Resized += QueueRedraw;
        _margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_margin);
        _margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) _margin.AddThemeConstantOverride("margin_" + edge, Compact ? 8 : 24);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", Compact ? 4 : 16); _margin.AddChild(column);
        _caption = ArenaPresentation.Text("", MenuVisuals.Type.Caption, MenuVisuals.Tone.Reading); column.AddChild(_caption);
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", Compact ? 8 : 32); column.AddChild(row);
        _identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _identity.AddThemeConstantOverride("separation", Compact ? 2 : 8); row.AddChild(_identity);
        _avatar = new ProfileAvatar { CustomMinimumSize = Compact ? new(68, 68) : new(160, 160), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _identity.AddChild(_avatar);
        _name = ArenaPresentation.Text("", Compact ? MenuVisuals.Type.Body : MenuVisuals.Type.Title);
        _name.Name = "ProfileName";
        _title = ArenaPresentation.Text("", MenuVisuals.Type.Caption, BoardArt.Gold);
        _title.Name = "ProfileTitle";
        foreach (var label in new[] { _name, _title })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center; label.ClipText = true;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _identity.AddChild(label);
        }
        static TextureRect Art(string name) => new()
        {
            Name = name, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore
        };
        _card = Art("StageCard"); _card.SizeFlagsStretchRatio = 1.3f; row.AddChild(_card);
        _piece = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _piece.AddThemeConstantOverride("separation", Compact ? 4 : 14); row.AddChild(_piece);
        _sleeve = Art("StageSleeve"); _sleeve.SizeFlagsVertical = SizeFlags.ExpandFill; _piece.AddChild(_sleeve);
        _dieSpace = new Control { CustomMinimumSize = Compact ? new(40, 40) : new(84, 84), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _piece.AddChild(_dieSpace);
    }

    public void Present(ProfileView view, bool home, string cloth, Texture2D? card, string sleeve, string dice, string caption)
    {
        View = view;
        _mat = ProfilePlaymats.Of(cloth);
        _caption.Text = caption;
        // The home shows the Relikario alone: name, sleeve and die belong to a duel.
        _identity.Visible = _piece.Visible = !home;
        _avatar.Avatar = view.Avatar; _avatar.Frame = view.Frame;
        _name.Text = view.Name; _title.Text = ProfileCatalog.Title(view.Title).Name.ToUpperInvariant();
        _card.Texture = card;
        _sleeve.Texture = SleeveArt.Back(sleeve);
        if (_die?.Style != dice)
        {
            if (_die != null) { _dieSpace.RemoveChild(_die); _die.QueueFree(); }
            _die = new DiceSwatch { Style = dice };
            _dieSpace.AddChild(_die);
            _die.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X < 4 || Size.Y < 4) return;
        var rect = new Rect2(Vector2.Zero, Size).Grow(-1);
        var outline = BoardArt.Octagon(rect, MenuVisuals.Cut.Panel, MenuVisuals.Cut.Panel);
        DrawPolygon(outline, [_mat.Raised, _mat.Raised, _mat.Surface, _mat.Background, _mat.Background, _mat.Background, _mat.Surface, _mat.Raised]);
        var trace = BoardArt.Tint(_mat.Trace, 0.28f);
        for (var i = 0; i < 5; i++)
        {
            var y = Size.Y * (0.22f + i * 0.14f); var bend = Size.X * (0.10f + i * 0.04f);
            DrawPolyline([new(2, y), new(bend, y), new(bend + 22, y + 22), new(bend + 70, y + 22)], trace, 1.5f, true);
            DrawPolyline([new(Size.X - 2, y), new(Size.X - bend, y), new(Size.X - bend - 22, y - 22), new(Size.X - bend - 70, y - 22)], trace, 1.5f, true);
        }
        DrawPolyline([.. outline, outline[0]], _mat.Frame, 2, true);
    }
}

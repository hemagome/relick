using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Builds one deck from the catalogue. It only edits a local copy: saving is its owner's job.</summary>
public partial class DeckEditor : VBoxContainer
{
    public string DeckName { get; init; } = "";
    public string Relikario { get; init; } = RelikarioCatalog.Yuno.Id;
    public IReadOnlyList<string> InitialCards { get; init; } = [];
    public IReadOnlyList<string> InitialTraps { get; init; } = [];
    public IReadOnlyList<string> InitialSide { get; init; } = [];
    public bool ShortScreen { get; init; }
    /// <summary>Name, main deck, Trap deck and side deck as they should be stored.</summary>
    public event Action<string, List<string>, List<string>, List<string>>? SaveRequested;
    public event Action? Closed;

    private static readonly (string Title, Func<CardDefinition, bool> Match)[] Groups =
    [
        ("Ataques", DeckRules.IsAttack),
        ("Habilidades", card => card.Kind == CardKind.Skill),
        ("Códigos", card => card.Kind == CardKind.Code && !DeckRules.IsTrap(card)),
        ("Equipos", card => card.Kind == CardKind.Equipment),
        ("Códigos Trampa", DeckRules.IsTrap)
    ];

    private readonly Dictionary<string, int> _counts = [];
    private readonly Dictionary<string, int> _side = [];
    private readonly Dictionary<string, (HandCard Card, Label Badge, Control Bench, Label BenchCount)> _catalog = [];
    private LineEdit _name = null!;
    private LineEdit _search = null!;
    private Label _totals = null!;
    private Label _problems = null!;
    private Label _notice = null!;
    private Button _save = null!;
    private VBoxContainer _rows = null!;
    private Button _deckTab = null!;
    private Button _sideTab = null!;
    private GridContainer _grid = null!;
    private ScrollContainer _gallery = null!;
    private CardInspector? _inspector;
    private int _filter = -1;
    private string _saved = "";
    /// <summary>The list on show, and where catalogue picks go: the deck (with its Trap deck) or the side deck.</summary>
    private bool _editingSide;
    private float CardScale => ShortScreen ? 0.6f : 0.8f;

    internal IReadOnlyDictionary<string, int> Counts => _counts;
    internal IReadOnlyDictionary<string, int> SideCounts => _side;
    internal bool Dirty => Fingerprint() != _saved;
    internal List<string> Main => Listed(_counts, card => !DeckRules.IsTrap(card));
    internal List<string> Traps => Listed(_counts, DeckRules.IsTrap);
    internal List<string> Side => Listed(_side, _ => true);
    internal List<string> Problems => DeckRules.Validate(Relikario, Main, Traps, Side);
    internal bool EditingSide
    {
        get => _editingSide;
        set { _editingSide = value; _deckTab.SetPressedNoSignal(!value); _sideTab.SetPressedNoSignal(value); Notify(""); Refresh(); }
    }

    public override void _Ready()
    {
        Name = "DeckEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", ShortScreen ? 8 : 14);
        foreach (var id in InitialCards.Concat(InitialTraps))
            if (CardCatalog.All.ContainsKey(id)) _counts[id] = _counts.GetValueOrDefault(id) + 1;
        foreach (var id in InitialSide)
            if (CardCatalog.All.ContainsKey(id)) _side[id] = _side.GetValueOrDefault(id) + 1;

        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); AddChild(header);
        var back = ArenaPresentation.Button(header, "← Mazos", RequestClose);
        back.Name = "EditorBack"; back.CustomMinimumSize = new(120, 48);
        _name = new LineEdit { Name = "DeckName", Text = DeckName, PlaceholderText = "Nombre del mazo", MaxLength = 32,
            CustomMinimumSize = new(180, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _name.TextChanged += _ => Refresh();
        header.AddChild(_name);
        _save = ArenaPresentation.Button(header, "Guardar", Save);
        _save.Name = "SaveDeck"; _save.CustomMinimumSize = new(170, 48); RelickVisuals.Primary(_save);

        _totals = ArenaPresentation.Text("", ShortScreen ? 13 : 15, BoardArt.Gold);
        _totals.Name = "DeckTotals"; _totals.AutowrapMode = TextServer.AutowrapMode.WordSmart; AddChild(_totals);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 14); AddChild(body);
        var catalog = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        catalog.AddThemeConstantOverride("separation", 10); body.AddChild(catalog);
        var filters = new HFlowContainer { Name = "CatalogFilters" }; filters.AddThemeConstantOverride("h_separation", 8); catalog.AddChild(filters);
        var group = new ButtonGroup();
        for (var index = -1; index < Groups.Length; index++)
        {
            var choice = index;
            var button = new Button { Text = index < 0 ? "Todas" : Groups[index].Title, ToggleMode = true, ButtonGroup = group,
                ButtonPressed = index < 0, CustomMinimumSize = new(0, 44), Name = "Filter" + (index + 1) };
            button.Pressed += () => { _filter = choice; ApplyFilter(); };
            filters.AddChild(button);
        }
        _search = new LineEdit { Name = "CatalogSearch", PlaceholderText = "Buscar por nombre", ClearButtonEnabled = true,
            CustomMinimumSize = new(200, 44), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _search.TextChanged += _ => ApplyFilter();
        filters.AddChild(_search);
        _gallery = new ScrollContainer { Name = "CatalogGallery", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever, FollowFocus = true };
        catalog.AddChild(_gallery);
        _grid = new GridContainer { Name = "CatalogCards", Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 10); _grid.AddThemeConstantOverride("v_separation", 10);
        _gallery.AddChild(_grid);
        foreach (var card in Groups.SelectMany(g => CardCatalog.All.Values.Where(g.Match).OrderBy(c => c.Name, StringComparer.CurrentCulture))
                     .Where(card => DeckRules.Compatible(card, Relikario)))
            AddCatalogCard(card);
        _gallery.Resized += () => CallDeferred(nameof(UpdateColumns));

        var side = new VBoxContainer { Name = "DeckPanel", CustomMinimumSize = new(ShortScreen ? 300 : 420, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        side.AddThemeConstantOverride("separation", 10); body.AddChild(side);
        var tabs = new HBoxContainer { Name = "ListTabs" }; tabs.AddThemeConstantOverride("separation", 8); side.AddChild(tabs);
        var tabGroup = new ButtonGroup();
        _deckTab = new Button { Name = "DeckTab", ToggleMode = true, ButtonGroup = tabGroup, ButtonPressed = true, CustomMinimumSize = new(0, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Ver el mazo; las cartas del catálogo se añaden aquí." };
        _deckTab.Pressed += () => EditingSide = false; tabs.AddChild(_deckTab);
        _sideTab = new Button { Name = "SideTab", ToggleMode = true, ButtonGroup = tabGroup, CustomMinimumSize = new(0, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Ver el banquillo; las cartas del catálogo se añaden aquí." };
        _sideTab.Pressed += () => EditingSide = true; tabs.AddChild(_sideTab);
        var listPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var frame = RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 8);
        frame.ContentMarginLeft = frame.ContentMarginRight = 12; frame.ContentMarginTop = frame.ContentMarginBottom = 10;
        listPanel.AddThemeStyleboxOverride("panel", frame); side.AddChild(listPanel);
        var listScroll = new ScrollContainer { Name = "DeckList", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        listPanel.AddChild(listScroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _rows.AddThemeConstantOverride("separation", 4); listScroll.AddChild(_rows);
        _problems = ArenaPresentation.Text("", 14); _problems.Name = "DeckProblems";
        _problems.AutowrapMode = TextServer.AutowrapMode.WordSmart; side.AddChild(_problems);
        _notice = ArenaPresentation.Text("", 14, BoardArt.Gold); _notice.Name = "EditorNotice";
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart; _notice.Visible = false; side.AddChild(_notice);
        if (!ShortScreen)
        {
            _inspector = new CardInspector { Name = "EditorInspector", CustomMinimumSize = new(0, 230), ReadingHeight = 90 };
            side.AddChild(_inspector);
        }
        _saved = Fingerprint();
        Refresh();
        CallDeferred(nameof(UpdateColumns));
        if (_catalog.Count > 0) CallDeferred(nameof(InspectFirst));
    }

    private void InspectFirst() => Inspect(CardCatalog.Get(_catalog.Keys.First()));

    private void AddCatalogCard(CardDefinition card)
    {
        var face = new HandCard { Card = card, PresentationScale = CardScale, Name = "Catalog_" + card.Id, TooltipText = card.Name,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        var badge = ArenaPresentation.Text("", ShortScreen ? 13 : 16, new Color("06141a"));
        badge.Name = "Copies"; badge.MouseFilter = MouseFilterEnum.Ignore;
        badge.AddThemeFontOverride("font", RelickVisuals.Strong);
        var plate = RelickVisuals.Frame(BoardArt.Gold, BoardArt.Gold.Lightened(0.4f), 6);
        plate.ContentMarginLeft = plate.ContentMarginRight = 7; plate.ContentMarginTop = plate.ContentMarginBottom = 1;
        badge.AddThemeStyleboxOverride("normal", plate);
        face.AddChild(badge); badge.Position = new(4, 4);
        // Side-deck copies get their own badge with the bench symbol, instead of a cryptic "B1".
        var bench = new PanelContainer { Name = "Bench", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        var benchPlate = RelickVisuals.Frame(new Color("c0acf7"), new Color("c0acf7").Lightened(0.4f), 6);
        benchPlate.ContentMarginLeft = benchPlate.ContentMarginRight = 6; benchPlate.ContentMarginTop = benchPlate.ContentMarginBottom = 1;
        bench.AddThemeStyleboxOverride("panel", benchPlate);
        var benchRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; benchRow.AddThemeConstantOverride("separation", 3); bench.AddChild(benchRow);
        benchRow.AddChild(new IconView { Icon = GameIcon.Bench, Color = new Color("06141a"), CustomMinimumSize = new(14, 14), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var benchCount = ArenaPresentation.Text("", ShortScreen ? 13 : 16, new Color("06141a"));
        benchCount.Name = "BenchCount"; benchCount.MouseFilter = MouseFilterEnum.Ignore; benchCount.AddThemeFontOverride("font", RelickVisuals.Strong);
        benchRow.AddChild(benchCount);
        face.AddChild(bench);
        face.Pressed += () => { Inspect(card); Add(card, _editingSide); };
        face.MouseEntered += () => Inspect(card);
        face.FocusEntered += () => Inspect(card);
        _catalog[card.Id] = (face, badge, bench, benchCount);
        _grid.AddChild(face);
    }

    private void UpdateColumns()
    {
        var width = _gallery.Size.X - 18; // Reserve the vertical scrollbar, even before it appears.
        _grid.Columns = Math.Clamp((int)((width + 10) / (126 * CardScale + 10)), 1, 10);
    }

    private void ApplyFilter()
    {
        var text = _search.Text.Trim();
        foreach (var (id, entry) in _catalog)
        {
            var card = CardCatalog.Get(id);
            entry.Card.Visible = (_filter < 0 || Groups[_filter].Match(card)) &&
                (text.Length == 0 || card.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    private void Inspect(CardDefinition card) => _inspector?.Present(card, card.Name, ArenaActionText.CardCost(card));

    private int MainCount => _counts.Where(pair => !DeckRules.IsTrap(CardCatalog.Get(pair.Key))).Sum(pair => pair.Value);
    private int SideCount => _side.Values.Sum();
    private int Count(Func<CardDefinition, bool> match) => _counts.Where(pair => match(CardCatalog.Get(pair.Key))).Sum(pair => pair.Value);

    /// <summary>Copies sharing the card's printed name, so alternate art shares the limit. The side deck counts
    /// together with the deck it would be swapped into: Trap Codes with the Trap deck, the rest with the main deck.</summary>
    private int Named(CardDefinition card) => _counts.Concat(_side)
        .Where(pair => CardCatalog.Get(pair.Key) is var other && other.Name == card.Name && DeckRules.IsTrap(other) == DeckRules.IsTrap(card)).Sum(pair => pair.Value);

    /// <summary>Why another copy cannot be added there, or null when it can. The rules stay advisory for drafts already over a limit.</summary>
    internal string? AddBlocked(CardDefinition card, bool toSide = false)
    {
        var limit = DeckRules.IsTrap(card) ? DeckRules.MaxTrapCopies : DeckRules.MaxCopies;
        var copies = !DeckRules.IsAttack(card) && Named(card) >= limit
            ? $"Ya tienes {limit} copias de {card.Name}" + (_side.Keys.Any(id => CardCatalog.Get(id).Name == card.Name) ? " entre el mazo y el banquillo." : ".") : null;
        if (toSide)
            return !DeckRules.AllowedInSide(card) ? "El banquillo no admite cartas de Ataque."
                : copies ?? (SideCount >= DeckRules.MaxSide ? $"El banquillo admite hasta {DeckRules.MaxSide} cartas. Quita una para añadir otra." : null);
        if (DeckRules.IsTrap(card))
            return Count(DeckRules.IsTrap) >= DeckRules.MaxTraps ? $"El mazo de Códigos Trampa admite hasta {DeckRules.MaxTraps} cartas." : copies;
        if (DeckRules.IsAttack(card))
            return Count(DeckRules.IsAttack) >= DeckRules.AttackCount ? $"El mazo lleva exactamente {DeckRules.AttackCount} Ataques." : null;
        return copies ?? (MainCount >= DeckRules.MainDeckSize ? $"El mazo principal ya tiene {DeckRules.MainDeckSize} cartas. Quita una para añadir otra." : null);
    }

    /// <summary>Whether the card itself is spent, as opposed to the target list merely being full.</summary>
    private bool AtOwnLimit(CardDefinition card)
    {
        if (AddBlocked(card, _editingSide) == null) return false;
        if (DeckRules.IsAttack(card)) return true;
        return Named(card) >= (DeckRules.IsTrap(card) ? DeckRules.MaxTrapCopies : DeckRules.MaxCopies) ||
            !_editingSide && DeckRules.IsTrap(card);
    }

    internal void Add(CardDefinition card, bool toSide = false)
    {
        if (AddBlocked(card, toSide) is { } reason) { Notify(reason); return; }
        var target = toSide ? _side : _counts;
        target[card.Id] = target.GetValueOrDefault(card.Id) + 1;
        Notify(""); Refresh();
    }

    internal void Remove(CardDefinition card, bool fromSide = false)
    {
        if (Take(fromSide ? _side : _counts, card)) { Notify(""); Refresh(); }
    }

    /// <summary>Moves one copy between the deck and the side deck, as a swap between duels would.</summary>
    internal void Move(CardDefinition card, bool toSide)
    {
        var from = toSide ? _counts : _side;
        if (!from.ContainsKey(card.Id)) return;
        // The moving copy no longer counts where it was, so only the destination's own capacity can stop it.
        Take(from, card);
        if (AddBlocked(card, toSide) is { } reason) { from[card.Id] = from.GetValueOrDefault(card.Id) + 1; Notify(reason); Refresh(); return; }
        var to = toSide ? _side : _counts;
        to[card.Id] = to.GetValueOrDefault(card.Id) + 1;
        Notify(""); Refresh();
    }

    private static bool Take(Dictionary<string, int> counts, CardDefinition card)
    {
        if (!counts.TryGetValue(card.Id, out var copies)) return false;
        if (copies <= 1) counts.Remove(card.Id); else counts[card.Id] = copies - 1;
        return true;
    }

    /// <summary>Shows a message from the owner, such as a save the server refused.</summary>
    public void Notify(string message)
    {
        _notice.Text = message; _notice.Visible = message.Length > 0;
    }

    /// <summary>The owner calls this once the server has accepted the save.</summary>
    public void MarkSaved() => _saved = Fingerprint();

    public void SetBusy(bool busy)
    {
        _save.Disabled = busy;
        if (busy) _save.Text = "Guardando…"; else Refresh();
    }

    private void Save()
    {
        if (_name.Text.Trim().Length == 0) { Notify("Ponle un nombre al mazo antes de guardarlo."); _name.GrabFocus(); return; }
        SaveRequested?.Invoke(_name.Text.Trim(), Main, Traps, Side);
    }

    private void RequestClose()
    {
        if (!Dirty) { Closed?.Invoke(); return; }
        var dialog = new ConfirmationDialog { Title = "Cambios sin guardar", DialogText = "Si sales ahora perderás los cambios de este mazo.",
            OkButtonText = "Salir sin guardar", CancelButtonText = "Seguir editando" };
        AddChild(dialog);
        dialog.Confirmed += () => Closed?.Invoke();
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        dialog.PopupCentered();
    }

    private string Fingerprint() => _name.Text.Trim() + "\n" + Tally(_counts) + "\n" + Tally(_side);

    private static string Tally(Dictionary<string, int> counts) =>
        string.Join(",", counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "×" + pair.Value));

    private static List<string> Listed(Dictionary<string, int> counts, Func<CardDefinition, bool> match) =>
        Groups.SelectMany(g => counts.Keys.Select(CardCatalog.Get).Where(card => g.Match(card) && match(card)).OrderBy(card => card.Name, StringComparer.CurrentCulture))
            .SelectMany(card => Enumerable.Repeat(card.Id, counts[card.Id])).ToList();

    private void Refresh()
    {
        var problems = Problems;
        var skills = Count(card => card.Kind == CardKind.Skill);
        // The Mazo and Banquillo tabs carry their own totals; this line keeps the composition rules.
        _totals.Text = $"Ataques {Count(DeckRules.IsAttack)}/{DeckRules.AttackCount}  ·  " +
            $"Habilidades {skills} (mín. {DeckRules.MinSkills})  ·  Códigos Trampa {Count(DeckRules.IsTrap)}/{DeckRules.MaxTraps}";
        _deckTab.Text = $"Mazo · {MainCount}/{DeckRules.MainDeckSize}";
        _sideTab.Text = $"Banquillo · {SideCount}/{DeckRules.MaxSide}";
        _problems.Text = problems.Count == 0 ? "Mazo listo para jugar." : "Para poder jugarlo:\n• " + string.Join("\n• ", problems);
        _problems.AddThemeColorOverride("font_color", problems.Count == 0 ? ArenaPresentation.Mint : ArenaPresentation.Coral);
        _save.Text = problems.Count == 0 ? "Guardar" : "Guardar borrador";
        foreach (var (id, entry) in _catalog)
        {
            var copies = _counts.GetValueOrDefault(id); var benched = _side.GetValueOrDefault(id);
            entry.Badge.Text = $"×{copies}";
            entry.Badge.Visible = copies > 0;
            entry.BenchCount.Text = benched.ToString();
            entry.Bench.Visible = benched > 0;
            // Right after the main-deck badge, or in its corner when the card is only on the bench.
            entry.Bench.Position = new(copies > 0 ? 4 + entry.Badge.GetCombinedMinimumSize().X + 4 : 4, 4);
            var card = CardCatalog.Get(id);
            entry.Card.TooltipText = copies + benched == 0 ? card.Name
                : $"{card.Name}\n{copies} en el mazo · {benched} en el banquillo";
            // Dim only what has reached its own limit; a full deck would otherwise grey out the whole catalogue.
            entry.Card.SelfModulate = AtOwnLimit(CardCatalog.Get(id)) ? new Color(1, 1, 1, 0.45f) : Colors.White;
        }
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        var shown = _editingSide ? _side : _counts;
        var toSide = !_editingSide;
        if (_editingSide)
        {
            var help = ArenaPresentation.Text(ShortScreen ? $"Hasta {DeckRules.MaxSide} cartas, sin Ataques." :
                $"Hasta {DeckRules.MaxSide} cartas, sin Ataques. Antes del segundo y del tercer duelo de un encuentro " +
                "podrás cambiarlas por cartas del mazo sin alterar su total. Las copias cuentan junto con el mazo.", 13, BoardArt.Muted);
            help.Name = "SideHelp"; help.AutowrapMode = TextServer.AutowrapMode.WordSmart; _rows.AddChild(help);
        }
        foreach (var (title, match) in Groups)
        {
            var cards = shown.Keys.Select(CardCatalog.Get).Where(match).OrderBy(card => card.Name, StringComparer.CurrentCulture).ToList();
            if (cards.Count == 0) continue;
            var heading = ArenaPresentation.Text($"{title.ToUpperInvariant()} · {cards.Sum(card => shown[card.Id])}", 12, BoardArt.Muted);
            _rows.AddChild(heading);
            foreach (var card in cards)
            {
                var row = new HBoxContainer { Name = "Row_" + card.Id }; row.AddThemeConstantOverride("separation", 6); _rows.AddChild(row);
                var copies = ArenaPresentation.Text(shown[card.Id] + "×", 16, BoardArt.Gold);
                copies.CustomMinimumSize = new(34, 0); copies.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(copies);
                var name = new Button { Text = card.Name, Flat = true, Alignment = HorizontalAlignment.Left, ClipText = true,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(0, 40), TooltipText = card.Name };
                name.AddThemeFontSizeOverride("font_size", ShortScreen ? 14 : 16);
                name.Pressed += () => Inspect(card); name.MouseEntered += () => Inspect(card); name.FocusEntered += () => Inspect(card);
                row.AddChild(name);
                var fromSide = _editingSide;
                // A Trap Code moves to the Trap deck and the rest to the main deck; Attacks never reach the side deck.
                if (!toSide || DeckRules.AllowedInSide(card))
                {
                    var move = ArenaPresentation.Button(row, toSide ? "→" : "←", () => Move(card, toSide));
                    move.Name = "Move"; move.CustomMinimumSize = new(44, 40);
                    move.TooltipText = toSide ? "Pasar una copia al banquillo" : "Pasar una copia al mazo";
                }
                var less = ArenaPresentation.Button(row, "−", () => Remove(card, fromSide));
                less.Name = "Less"; less.CustomMinimumSize = new(44, 40); less.TooltipText = "Quitar una copia";
                var more = ArenaPresentation.Button(row, "+", () => Add(card, fromSide), AddBlocked(card, fromSide) != null);
                more.Name = "More"; more.CustomMinimumSize = new(44, 40); more.TooltipText = "Añadir una copia";
            }
        }
        if (shown.Count == 0)
            _rows.AddChild(ArenaPresentation.Text(_editingSide ? "El banquillo está vacío. Pulsa una carta del catálogo para añadirla aquí." :
                "Pulsa una carta del catálogo para añadirla.", 14, BoardArt.Muted));
    }
}

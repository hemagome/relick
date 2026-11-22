using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Edits a local deck. Its owner submits the save and confirms it.</summary>
public partial class DeckEditor : VBoxContainer
{
    public string DeckName { get; init; } = "";
    public string Relikario { get; init; } = RelikarioCatalog.Yuno.Id;
    public IReadOnlyList<string> InitialCards { get; init; } = [];
    public IReadOnlyList<string> InitialTraps { get; init; } = [];
    public IReadOnlyList<string> InitialSide { get; init; } = [];
    public string InitialSleeve { get; init; } = CosmeticCatalog.DefaultSleeve;
    public string InitialDice { get; init; } = CosmeticCatalog.DefaultDice;
    public bool ShortScreen { get; init; }
    public bool Touch { get; init; }

    /// <summary>Name, main deck, Trap deck, side deck, sleeve and die.</summary>
    public event Action<string, List<string>, List<string>, List<string>, string, string>? SaveRequested;
    public event Action? Closed;

    private enum Section { Main, Traps, Side, Style }

    private static readonly (string Title, Func<CardDefinition, bool> Match)[] Groups =
    [
        ("Ataques", DeckRules.IsAttack),
        ("Habilidades", card => card.Kind == CardKind.Skill),
        ("Códigos", card => card.Kind == CardKind.Code && !DeckRules.IsTrap(card)),
        ("Equipos", card => card.Kind == CardKind.Equipment),
        ("Áreas", card => card.Kind == CardKind.Area),
        ("Códigos Trampa", DeckRules.IsTrap)
    ];
    private static readonly CardDefinition[] OrderedCards = Groups
        .SelectMany(group => CardCatalog.All.Values.Where(group.Match).OrderBy(card => card.Name, StringComparer.CurrentCulture))
        .ToArray();

    private readonly record struct CopyEdit(CardDefinition Card, Section Section, int Delta, Section? From = null);

    private readonly Dictionary<string, int> _counts = [];
    private readonly Dictionary<string, int> _side = [];
    private readonly Stack<CopyEdit> _history = [];
    private Section _section;
    private bool _grouped = true;
    private CardDefinition? _selected;
    private string _saved = "";
    private string _sleeve = CosmeticCatalog.DefaultSleeve;
    private string _dice = CosmeticCatalog.DefaultDice;

    internal string Sleeve => _sleeve;
    internal string Dice => _dice;
    internal bool Dirty => Fingerprint() != _saved;
    internal bool Saving { get; private set; }
    internal List<string> Main => Listed(Section.Main);
    internal List<string> Traps => Listed(Section.Traps);
    internal List<string> Side => Listed(Section.Side);
    internal List<string> Problems => DeckRules.Validate(Relikario, Main, Traps, Side);
    internal bool EditingSide => _section == Section.Side;
    internal bool EditingStyle => _section == Section.Style;

    public override void _Ready()
    {
        Name = "DeckEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        foreach (var id in DeckRules.NormalizeAttackArt(Relikario, InitialCards).Concat(InitialTraps))
            if (CardCatalog.All.ContainsKey(id)) _counts[id] = _counts.GetValueOrDefault(id) + 1;
        foreach (var id in InitialSide)
            if (CardCatalog.All.ContainsKey(id)) _side[id] = _side.GetValueOrDefault(id) + 1;
        _sleeve = CosmeticCatalog.Sleeve(InitialSleeve).Id;
        _dice = CosmeticCatalog.DiceFor(InitialDice).Id;

        BuildView();
        _saved = Fingerprint();
        _selected = OrderedCards.FirstOrDefault(card => Copies(card, Section.Main) > 0)
            ?? _catalog.Values.FirstOrDefault().Face?.Card;
        Refresh();
        ApplyFilter();
        Notify("");
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Saving || _history.Count == 0 || GetViewport().GuiGetFocusOwner() is LineEdit ||
            @event is not InputEventKey { Pressed: true, Echo: false, ShiftPressed: false, Keycode: Key.Z } key ||
            !key.IsCommandOrControlPressed()) return;
        Undo();
        GetViewport().SetInputAsHandled();
    }

    private void SelectSection(Section section)
    {
        if (_section == section) return;
        _section = section;
        _deckScroll.ScrollVertical = 0;
        _gallery.ScrollVertical = 0;
        if (_selected == null || !InCatalogSection(_selected))
            _selected = OrderedCards.FirstOrDefault(card => Copies(card, section) > 0)
                ?? _catalog.Values.Select(tile => tile.Face.Card).FirstOrDefault(InCatalogSection);
        UpdateFilterOptions();
        Refresh();
        Notify("");
    }

    private void Inspect(CardDefinition card)
    {
        if (_selected == card) return;
        _selected = card;
        PresentInspector();
        PresentSelection();
    }

    private static Section Home(CardDefinition card) => DeckRules.IsTrap(card) ? Section.Traps : Section.Main;
    private static string SectionName(Section section) => section switch
    {
        Section.Main => "Principal", Section.Traps => "Códigos Trampa", Section.Side => "Banquillo", _ => "Estilo"
    };

    private Dictionary<string, int> CountsIn(Section section) => section == Section.Side ? _side : _counts;

    private int Copies(CardDefinition card, Section section) =>
        section == Section.Side || section == Home(card) ? CountsIn(section).GetValueOrDefault(card.Id) : 0;

    private int CountIn(Section section) => CountsIn(section)
        .Where(pair => section == Section.Side || Home(CardCatalog.Get(pair.Key)) == section).Sum(pair => pair.Value);

    private int Count(Func<CardDefinition, bool> match) => _counts.Where(pair => match(CardCatalog.Get(pair.Key))).Sum(pair => pair.Value);

    private List<string> Listed(Section section) => OrderedCards
        .SelectMany(card => Enumerable.Repeat(card.Id, Copies(card, section))).ToList();

    /// <summary>Copies sharing the printed name and the same destination, including any in the side deck.</summary>
    private int Named(CardDefinition card) => _counts.Concat(_side)
        .Where(pair => CardCatalog.Get(pair.Key) is var other && other.Name == card.Name && Home(other) == Home(card))
        .Sum(pair => pair.Value);

    internal string? AddBlocked(CardDefinition card, bool toSide = false) => AddBlocked(card, toSide ? Section.Side : Home(card));

    private string? AddBlocked(CardDefinition card, Section section, bool moving = false)
    {
        if (section == Section.Style) return "Elige una sección del mazo para añadir cartas.";
        if (!DeckRules.Compatible(card, Relikario)) return $"{card.Name} no es compatible con este Relikario.";
        if (section == Section.Side && !DeckRules.AllowedInSide(card)) return "El banquillo no admite cartas de Ataque.";
        if (section != Section.Side && section != Home(card))
            return $"{card.Name} va en {SectionName(Home(card))}.";

        var limit = DeckRules.IsTrap(card) ? DeckRules.MaxTrapCopies : DeckRules.MaxCopies;
        var copies = !DeckRules.IsAttack(card) && Named(card) - (moving ? 1 : 0) >= limit
            ? $"Ya tienes {limit} copias de {card.Name} entre el mazo y el banquillo." : null;
        if (section == Section.Side)
            return copies ?? (CountIn(section) >= DeckRules.MaxSide ? $"El banquillo admite hasta {DeckRules.MaxSide} cartas. Quita una para añadir otra." : null);
        if (section == Section.Traps)
            return CountIn(section) >= DeckRules.MaxTraps ? $"El mazo de Códigos Trampa admite hasta {DeckRules.MaxTraps} cartas." : copies;
        if (DeckRules.IsAttack(card))
            return Count(DeckRules.IsAttack) >= DeckRules.AttackCount ? $"El mazo lleva exactamente {DeckRules.AttackCount} Ataques." : null;
        return copies ?? (CountIn(section) >= DeckRules.MainDeckSize ? $"El mazo principal ya tiene {DeckRules.MainDeckSize} cartas. Quita una para añadir otra." : null);
    }

    internal void Add(CardDefinition card, bool toSide = false) => Add(card, toSide ? Section.Side : Home(card));

    private void Add(CardDefinition card, Section section)
    {
        if (Saving) return;
        Inspect(card);
        if (AddBlocked(card, section) is { } reason) { Notify(reason, error: true); return; }
        Record(new(card, section, 1), $"+1 {card.Name} · {SectionName(section)}");
    }

    internal void Remove(CardDefinition card, bool fromSide = false) => Remove(card, fromSide ? Section.Side : Home(card));

    private void Remove(CardDefinition card, Section section)
    {
        if (Saving || Copies(card, section) == 0) return;
        Inspect(card);
        Record(new(card, section, -1), $"−1 {card.Name} · {SectionName(section)}");
    }

    internal void Move(CardDefinition card, bool toSide)
    {
        var from = toSide ? Home(card) : Section.Side;
        var to = toSide ? Section.Side : Home(card);
        if (Saving || Copies(card, from) == 0) return;
        Inspect(card);
        if (AddBlocked(card, to, moving: true) is { } reason) { Notify(reason, error: true); return; }
        Record(new(card, to, 1, from), $"{card.Name} → {SectionName(to)}");
    }

    private void Record(CopyEdit edit, string message)
    {
        ApplyEdit(edit, 1);
        _history.Push(edit);
        Refresh();
        Notify(message, error: false);
    }

    private void ApplyEdit(CopyEdit edit, int direction)
    {
        void Change(Section section, int delta)
        {
            var counts = CountsIn(section);
            var quantity = counts.GetValueOrDefault(edit.Card.Id) + direction * delta;
            if (quantity == 0) counts.Remove(edit.Card.Id);
            else counts[edit.Card.Id] = quantity;
        }
        Change(edit.Section, edit.Delta);
        if (edit.From is { } from) Change(from, -1);
    }

    private void Undo()
    {
        if (Saving || !_history.TryPop(out var edit)) return;
        ApplyEdit(edit, -1);
        _selected = edit.Card;
        var section = edit.From ?? edit.Section;
        if (_section != section) SelectSection(section);
        else Refresh();
        Notify($"Cambio deshecho · {edit.Card.Name}", error: false);
    }

    internal void ChooseStyle(string? sleeve = null, string? dice = null)
    {
        if (Saving) return;
        if (CosmeticCatalog.IsSleeve(sleeve)) _sleeve = sleeve!;
        if (CosmeticCatalog.IsDice(dice)) _dice = dice!;
        PresentStyle();
    }

    /// <summary>Shows a message from the owner, such as a rejected save.</summary>
    public void Notify(string message) => Notify(message, error: message.Length > 0);

    private void Notify(string message, bool error)
    {
        _notice.Text = message.Length > 0 ? message : EditingStyle ? (Touch ? "Toca para inspeccionar" : "Clic para inspeccionar")
            : Touch ? "Toca para inspeccionar · Usa + y − para editar"
            : "Clic derecho: quita del mazo o añade desde la colección";
        _notice.TooltipText = _notice.Text;
        _notice.AddThemeColorOverride("font_color", error ? ArenaPresentation.Coral : message.Length > 0 ? RelickVisuals.State.Target : BoardArt.Muted);
    }

    public void MarkSaved() => _saved = Fingerprint();

    internal void SetSaving(bool saving)
    {
        Saving = saving;
        ProcessMode = saving ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        _back.Disabled = _save.Disabled = saving;
        if (saving) { _save.Text = "Guardando…"; Notify(""); }
        else PresentRules();
    }

    private void Save()
    {
        if (Saving) return;
        if (_name.Text.Trim().Length == 0)
        {
            Notify("Ponle un nombre al mazo antes de guardarlo.");
            _name.GrabFocus();
            return;
        }
        SaveRequested?.Invoke(_name.Text.Trim(), Main, Traps, Side, _sleeve, _dice);
    }

    private void RequestClose()
    {
        if (Saving) return;
        if (!Dirty) { Closed?.Invoke(); return; }
        if (GetChildren().OfType<Window>().Any(window => window.Visible)) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Cambios sin guardar", OkButtonText = "Salir sin guardar", CancelButtonText = "Seguir editando"
        };
        MenuDialog.Fill(dialog, MenuDialog.Message("Si sales ahora perderás los cambios de este mazo."), 420, destructive: true);
        AddChild(dialog);
        dialog.Confirmed += () => Closed?.Invoke();
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        MenuDialog.Show(dialog);
    }

    private string Fingerprint() => _name.Text.Trim() + "\n" + Tally(_counts) + "\n" + Tally(_side) + "\n" + _sleeve + "/" + _dice;

    private static string Tally(Dictionary<string, int> counts) =>
        string.Join(",", counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "×" + pair.Value));
}

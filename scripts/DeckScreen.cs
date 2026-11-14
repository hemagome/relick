using Godot;
using Relick.Core;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

/// <summary>The player's saved decks: choose the one to play, and open the editor to build or change one.</summary>
public partial class DeckScreen : Control
{
    public LobbyConnection Connection { get; init; } = null!;
    public event Action? Closed;
    /// <summary>A guest sees the training deck they play with, but cannot build or keep decks.</summary>
    public bool Guest { get; init; }
    internal const string GuestNotice = "Como invitado juegas con el mazo de entrenamiento. Cierra sesión e inicia con una cuenta (correo, Discord u otra) para armar y guardar tus mazos.";
    /// <summary>Decks to show instead of the server's, for offline previews and tests.</summary>
    internal IReadOnlyList<DeckInfo>? PreviewDecks { get; set; }
    internal DeckEditor? Editor { get; private set; }

    private MarginContainer _margin = null!;
    private VBoxContainer? _rows;
    private Label? _status;
    private Button? _create;
    private int _shownRevision = -1;
    private bool _shownPending;
    private string _shownError = "";
    private ulong _editingId;
    private int _savesBeforeEdit;
    private bool _saving;

    private IReadOnlyList<DeckInfo> Decks => PreviewDecks ?? Connection.Decks;
    private bool ShortScreen => ArenaViewport.Short(this);

    public override void _Ready()
    {
        Name = "DeckScreen";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = RelickVisuals.Controls();
        _margin = new MarginContainer(); AddChild(_margin);
        _margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += AdjustMargins; AdjustMargins();
        ShowList();
    }

    private void AdjustMargins()
    {
        var horizontal = (int)Math.Max(16, (Size.X - 1320) / 2);
        foreach (var side in new[] { "left", "right" }) _margin.AddThemeConstantOverride("margin_" + side, horizontal);
        foreach (var side in new[] { "top", "bottom" }) _margin.AddThemeConstantOverride("margin_" + side, ShortScreen ? 10 : 24);
    }

    private void Clear()
    {
        foreach (var child in _margin.GetChildren()) { _margin.RemoveChild(child); child.QueueFree(); }
        _rows = null; _status = null; _create = null; Editor = null;
    }

    private void ShowList()
    {
        Clear();
        var column = new VBoxContainer { Name = "DeckListScreen" }; column.AddThemeConstantOverride("separation", ShortScreen ? 8 : 16); _margin.AddChild(column);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); column.AddChild(header);
        var back = ArenaPresentation.Button(header, "← Volver", () => Closed?.Invoke());
        back.Name = "DecksBack"; back.CustomMinimumSize = new(120, 48);
        var title = ArenaPresentation.Text("Mis mazos", ShortScreen ? 26 : 36); title.AddThemeFontOverride("font", RelickVisuals.Display);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
        _create = ArenaPresentation.Button(header, "Nuevo mazo", () => Edit(0, "Mazo nuevo", Enumerable.Repeat(CardCatalog.Attack.Id, DeckRules.AttackCount).ToList(), []));
        _create.Name = "NewDeck"; _create.CustomMinimumSize = new(160, 48); RelickVisuals.Primary(_create);
        _create.Visible = !Guest;
        if (Guest)
        {
            var notice = ArenaPresentation.Text(GuestNotice, 15, BoardArt.Gold); notice.Name = "GuestNotice";
            notice.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(notice);
        }
        _status = ArenaPresentation.Text("", 15, ArenaPresentation.Coral); _status.Name = "DecksStatus";
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart; _status.Visible = false; column.AddChild(_status);
        var scroll = new ScrollContainer { Name = "DeckRows", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        column.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _rows.AddThemeConstantOverride("separation", 10); scroll.AddChild(_rows);
        _shownRevision = -1;
        PresentList();
    }

    public override void _Process(double delta)
    {
        if (Editor != null) PresentEditor(); else PresentList();
    }

    private void PresentList()
    {
        if (_rows == null) return;
        var pending = Connection.DeckPending; var error = Connection.DeckError;
        if (_shownRevision == Connection.DeckRevision && _shownPending == pending && _shownError == error) return;
        _shownRevision = Connection.DeckRevision; _shownPending = pending; _shownError = error;
        _status!.Text = error; _status.Visible = error.Length > 0;
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        var decks = Decks;
        var busy = pending || PreviewDecks == null && !Connection.Ready;
        _create!.Disabled = busy || decks.Count >= 20;
        _create.TooltipText = decks.Count >= 20 ? "Puedes guardar hasta 20 mazos." : "";

        var training = CardCatalog.TrainingDeck(); var trainingTraps = CardCatalog.TrainingTraps();
        AddRow("Training", RelikarioCatalog.Yuno.Id, "Mazo de entrenamiento", "42 cartas · 3 Códigos Trampa · Listo para jugar", decks.All(deck => !deck.Active), true, busy,
            use: () => Connection.SetActiveDeck(0),
            edit: null,
            copy: Guest ? null : () => Edit(0, "Copia de entrenamiento", training, trainingTraps),
            delete: null);
        foreach (var deck in decks)
        {
            var problems = DeckRules.Validate(deck.Relikario, deck.Cards, deck.Traps, deck.Side);
            var summary = $"{deck.Cards.Count} cartas · {deck.Traps.Count} Códigos Trampa · " + (deck.Side.Count > 0 ? $"{deck.Side.Count} en banquillo · " : "") +
                (problems.Count == 0 ? "Listo para jugar" : "Borrador: " + problems[0]);
            var saved = deck;
            AddRow("Deck" + deck.Id, deck.Relikario, deck.Name, summary, deck.Active, problems.Count == 0, busy,
                use: () => Connection.SetActiveDeck(saved.Id),
                edit: () => Edit(saved.Id, saved.Name, saved.Cards, saved.Traps, saved.Side),
                copy: decks.Count >= 20 ? null : () => Edit(0, Shorten("Copia de " + saved.Name), saved.Cards, saved.Traps, saved.Side),
                delete: () => ConfirmDelete(saved));
        }
    }

    private static string Shorten(string name) => name.Length <= 32 ? name : name[..32].Trim();

    /// <summary>The Relikario's portrait cut from its skill cards, or null when it has none yet.</summary>
    internal static Texture2D? Thumbnail(string relikario) =>
        relikario == RelikarioCatalog.Yuno.Id ? GD.Load<Texture2D>("res://assets/characters/yuno/thumbnail.png") : null;

    private void AddRow(string key, string relikario, string name, string summary, bool active, bool valid, bool busy, Action use, Action? edit, Action? copy, Action? delete)
    {
        var panel = new PanelContainer { Name = key, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = RelickVisuals.Frame(RelickVisuals.Playmat.Surface, active ? ArenaPresentation.Mint : RelickVisuals.Playmat.Frame, 8, active ? 2 : 1);
        style.ContentMarginLeft = style.ContentMarginRight = ShortScreen ? 12 : 18; style.ContentMarginTop = style.ContentMarginBottom = ShortScreen ? 8 : 14;
        panel.AddThemeStyleboxOverride("panel", style); _rows!.AddChild(panel);
        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", ShortScreen ? 10 : 16); panel.AddChild(body);
        if (Thumbnail(relikario) is { } portrait)
        {
            var height = ShortScreen ? 52 : 72;
            body.AddChild(new TextureRect { Name = "RelikarioThumbnail", Texture = portrait, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new(height * portrait.GetWidth() / (float)portrait.GetHeight(), height),
                SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        }
        var row = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("h_separation", 12); row.AddThemeConstantOverride("v_separation", 8); body.AddChild(row);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(260, 0) };
        titles.AddThemeConstantOverride("separation", 2); row.AddChild(titles);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 10); titles.AddChild(heading);
        var title = ArenaPresentation.Text(name, ShortScreen ? 18 : 22, BoardArt.Ink); title.Name = "DeckTitle"; heading.AddChild(title);
        if (active)
        {
            var badge = ArenaPresentation.Text("EN USO", 12, ArenaPresentation.Mint); badge.Name = "ActiveBadge";
            badge.SizeFlagsVertical = SizeFlags.ShrinkCenter; heading.AddChild(badge);
        }
        var detail = ArenaPresentation.Text(summary, 14, valid ? BoardArt.Muted : ArenaPresentation.Coral);
        detail.Name = "DeckSummary"; detail.AutowrapMode = TextServer.AutowrapMode.WordSmart; titles.AddChild(detail);
        var actions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter }; actions.AddThemeConstantOverride("separation", 8); row.AddChild(actions);
        var useButton = ArenaPresentation.Button(actions, active ? "En uso" : "Usar", use, busy || active || !valid);
        useButton.Name = "Use"; useButton.CustomMinimumSize = new(96, 44);
        useButton.TooltipText = valid ? "" : "Completa el mazo para poder jugarlo.";
        if (!active && valid) RelickVisuals.Primary(useButton);
        if (edit != null) { var b = ArenaPresentation.Button(actions, "Editar", edit, busy); b.Name = "Edit"; b.CustomMinimumSize = new(88, 44); }
        if (copy != null) { var b = ArenaPresentation.Button(actions, "Duplicar", copy, busy); b.Name = "Copy"; b.CustomMinimumSize = new(96, 44); }
        if (delete != null) { var b = ArenaPresentation.Button(actions, "Borrar", delete, busy); b.Name = "Delete"; b.CustomMinimumSize = new(88, 44); }
    }

    private void ConfirmDelete(DeckInfo deck)
    {
        var dialog = new ConfirmationDialog { Title = "Borrar mazo", DialogText = $"¿Borrar «{deck.Name}»? No se puede deshacer.",
            OkButtonText = "Borrar", CancelButtonText = "Cancelar" };
        AddChild(dialog);
        dialog.Confirmed += () => Connection.DeleteDeck(deck.Id);
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        dialog.PopupCentered();
    }

    internal void Edit(ulong id, string name, IReadOnlyList<string> cards, IReadOnlyList<string> traps, IReadOnlyList<string>? side = null)
    {
        Clear();
        _editingId = id; _saving = false;
        Editor = new DeckEditor { DeckName = name, InitialCards = cards.ToList(), InitialTraps = traps.ToList(), InitialSide = side?.ToList() ?? [], ShortScreen = ShortScreen };
        Editor.Closed += ShowList;
        Editor.SaveRequested += (deckName, main, trapDeck, bench) =>
        {
            if (Connection.DeckPending) return;
            _savesBeforeEdit = Connection.DeckSaves; _saving = true;
            Connection.SaveDeck(_editingId, deckName, RelikarioCatalog.Yuno.Id, main, trapDeck, bench);
            // Without a connection the request is not sent at all.
            if (!Connection.DeckPending) { _saving = false; Editor.Notify("No hay conexión con el servidor. Inténtalo de nuevo al reconectar."); }
        };
        _margin.AddChild(Editor);
    }

    private void PresentEditor()
    {
        if (!_saving) return;
        Editor!.SetBusy(Connection.DeckPending);
        if (Connection.DeckPending) return;
        _saving = false;
        if (Connection.DeckSaves > _savesBeforeEdit) { Editor.MarkSaved(); ShowList(); }
        else Editor.Notify(Connection.DeckError.Length > 0 ? Connection.DeckError : "No se pudo guardar el mazo. Inténtalo de nuevo.");
    }
}

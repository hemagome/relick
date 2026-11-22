using Godot;
using Relick.Core;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

/// <summary>The player's saved decks: choose the one to play, and open the editor to build or change one.</summary>
public partial class DeckScreen : Control
{
    public LobbyConnection Connection { get; init; } = null!;
    /// <summary>The editor and the picker take the whole screen; the list lives beside the navigation.</summary>
    public event Action<bool>? FullScreenChanged;
    /// <summary>A guest sees the training deck they play with, but cannot build or keep decks.</summary>
    public bool Guest { get; init; }
    /// <summary>Fingers instead of a mouse: the editor grows every control to a touch target.</summary>
    internal bool Touch { get; init; } = OS.HasFeature("mobile");
    internal const string GuestNotice = "Como invitado juegas con el mazo de entrenamiento. Cierra sesión e inicia con una cuenta (correo, Discord u otra) para armar y guardar tus mazos.";
    /// <summary>Decks to show instead of the server's, for offline previews and tests.</summary>
    internal IReadOnlyList<DeckInfo>? PreviewDecks { get; set; }
    internal DeckEditor? Editor { get; private set; }
    internal RelikarioPicker? Picker { get; private set; }

    private MarginContainer _margin = null!;
    private Label? _status;
    private int _shownRevision = -1;
    private bool _shownPending;
    private string _shownError = "";
    private int _savesBeforeEdit;

    private IReadOnlyList<DeckInfo> Decks => PreviewDecks ?? Connection.Decks;
    private bool ShortScreen => GetViewportRect().Size.Y < 600;

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
        _tiles = null; _detail = null; _status = null; _create = null; Editor = null; Picker = null;
    }

    /// <summary>Back to the list, unless an editor still holds changes.</summary>
    internal void ShowDecks()
    {
        if (Editor == null) ShowList();
    }

    public override void _Process(double delta)
    {
        if (Editor != null) PresentEditor(); else PresentList();
    }

    private static string Shorten(string name) => name.Length <= 32 ? name : name[..32].Trim();

    /// <summary>The Relikario's portrait cut from its skill cards, or null when it has none yet.</summary>
    internal static Texture2D? Thumbnail(string relikario) =>
        RelikarioArt.Thumbnail(relikario);

    private void ConfirmDelete(DeckInfo deck)
    {
        var dialog = new ConfirmationDialog { Title = "Borrar mazo", OkButtonText = "Borrar", CancelButtonText = "Cancelar" };
        MenuDialog.Fill(dialog, MenuDialog.Message($"¿Borrar «{deck.Name}»? No se puede deshacer."), 420, destructive: true);
        AddChild(dialog);
        dialog.Confirmed += () => Connection.DeleteDeck(deck.Id);
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        MenuDialog.Show(dialog);
    }

    /// <summary>A new deck starts by choosing its Relikario; the editor then opens with the twelve Attacks every deck carries.</summary>
    internal void ChooseRelikario()
    {
        Clear();
        FullScreenChanged?.Invoke(true);
        // Stacking the card over its details takes about 940 of height; below it they sit side by side.
        Picker = new RelikarioPicker { ShortScreen = GetViewportRect().Size.Y < 940 };
        Picker.Closed += ShowList;
        Picker.Chosen += relikario => Edit(0, "Mazo nuevo", Enumerable.Repeat(DeckRules.DefaultAttackArt(relikario), DeckRules.AttackCount).ToList(), [], relikario: relikario);
        _margin.AddChild(Picker);
    }

    internal void Edit(ulong id, string name, IReadOnlyList<string> cards, IReadOnlyList<string> traps, IReadOnlyList<string>? side = null, string? relikario = null,
        string? sleeve = null, string? dice = null)
    {
        Clear();
        FullScreenChanged?.Invoke(true);
        var editor = new DeckEditor { DeckName = name, Relikario = relikario ?? RelikarioCatalog.Yuno.Id, InitialCards = cards.ToList(), InitialTraps = traps.ToList(), InitialSide = side?.ToList() ?? [], ShortScreen = ShortScreen, Touch = Touch, Owns = Connection.Owns,
            InitialSleeve = sleeve ?? CosmeticCatalog.DefaultSleeve, InitialDice = dice ?? CosmeticCatalog.DefaultDice };
        Editor = editor;
        editor.Closed += ShowList;
        editor.SaveRequested += (deckName, main, trapDeck, bench, deckSleeve, deckDice) =>
        {
            if (!Connection.Ready) { editor.Notify("No hay conexión con el servidor. Inténtalo de nuevo al reconectar."); return; }
            if (Connection.DeckPending) return;
            _savesBeforeEdit = Connection.DeckSaves;
            Connection.SaveDeck(id, deckName, editor.Relikario, main, trapDeck, bench, deckSleeve, deckDice);
            if (Connection.DeckPending) editor.SetSaving(true);
            else editor.Notify(Connection.DeckError.Length > 0 ? Connection.DeckError : "No se pudo enviar la solicitud de guardado.");
        };
        _margin.AddChild(editor);
    }

    private void PresentEditor()
    {
        if (Editor is not { Saving: true } editor || Connection.DeckPending && Connection.Ready) return;
        if (Connection.DeckSaves > _savesBeforeEdit)
        {
            editor.MarkSaved();
            GetNodeOrNull<SoundDirector>("/root/SoundDirector")?.Play(SoundCue.DeckSaved);
            ShowList();
            return;
        }

        editor.SetSaving(false);
        editor.Notify(!Connection.Ready ? "Se perdió la conexión antes de confirmar el guardado."
            : Connection.DeckError.Length > 0 ? Connection.DeckError : "No se pudo guardar el mazo. Inténtalo de nuevo.");
    }
}

using Godot;
using Relick.Core;

namespace Relick;

// The navigation shell and which of its pages is showing. The play page and its rooms are in OnlineLobby.Presentation.
public partial class OnlineLobby
{
    private MenuShell _shell = null!;
    private MenuPage _page = MenuPage.Play;
    private Control _playPage = null!, _profilePage = null!, _settingsPage = null!;
    private DeckScreen? _deckScreen;
    private Wardrobe? _wardrobe;

    private void BuildShell()
    {
        _shell = new MenuShell { Name = "MenuShell" };
        _content.AddChild(_shell);
        _shell.Navigation.Chosen += ShowPage;
        _playPage = BuildPlayPage();
        _profilePage = BuildProfilePage();
        _settingsPage = BuildSettingsPage();
        foreach (var page in new[] { _playPage, _profilePage, _settingsPage })
        {
            _shell.Body.AddChild(page);
            page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            page.Visible = page == _playPage;
        }
    }

    private void ShowPage(MenuPage page)
    {
        if (_arena != null) return;
        if (page == _page)
        {
            // The tab of the current page is also the way back to its first screen.
            if (page == MenuPage.Decks) _deckScreen?.ShowDecks();
            if (page == MenuPage.Play) CloseCompetition();
            return;
        }
        if (_page == MenuPage.Customize && _wardrobe?.Dirty == true && !_leavingWardrobe)
        {
            ConfirmLeavingWardrobe(page);
            return;
        }
        CloseCompetition();
        if (_page == MenuPage.Decks) FreeDecks();
        if (_page == MenuPage.Customize) FreeWardrobe();
        _page = page;
        _shell.Navigation.Select(page);
        _shell.Navigation.Visible = true;
        _playPage.Visible = page == MenuPage.Play;
        _profilePage.Visible = page == MenuPage.Profile;
        _settingsPage.Visible = page == MenuPage.Settings;
        _localError = "";
        switch (page)
        {
            case MenuPage.Decks:
                _deckScreen = new DeckScreen { Connection = _lobby, Guest = _lobby.Account?.Guest == true };
                _deckScreen.FullScreenChanged += full => _shell.Navigation.Visible = !full;
                _deckScreen.StyleRequested += OpenDeckStyle;
                _shell.Body.AddChild(_deckScreen);
                break;
            case MenuPage.Customize:
                _wardrobe = new Wardrobe { Connection = _lobby, PlayerName = OwnProfile().Name, StartAt = _wardrobeStart, StartDeck = _wardrobeDeck };
                _wardrobe.BackdropTried += backdrop => _background.Backdrop = backdrop;
                _shell.Body.AddChild(_wardrobe);
                _wardrobeStart = WardrobeCategory.Avatar; _wardrobeDeck = 0;
                break;
            case MenuPage.Profile: _presentedProfileRevision = -1; PresentProfile(); break;
            case MenuPage.Settings: PresentSettings(); break;
        }
    }

    private void FreeDecks()
    {
        if (_deckScreen == null) return;
        _shell.Body.RemoveChild(_deckScreen); _deckScreen.QueueFree(); _deckScreen = null;
    }

    private WardrobeCategory _wardrobeStart = WardrobeCategory.Avatar;
    private ulong _wardrobeDeck;
    private bool _leavingWardrobe;

    private void OpenDeckStyle(ulong deck)
    {
        _wardrobeStart = WardrobeCategory.Sleeve; _wardrobeDeck = deck;
        // The button that asked belongs to the screen this replaces.
        Callable.From(() => ShowPage(MenuPage.Customize)).CallDeferred();
    }

    private void FreeWardrobe()
    {
        if (_wardrobe == null) return;
        _shell.Body.RemoveChild(_wardrobe); _wardrobe.QueueFree(); _wardrobe = null;
        _background.Backdrop = _lobby.HomeBackdrop;
    }

    private void ConfirmLeavingWardrobe(MenuPage next)
    {
        if (GetNodeOrNull("LeaveWardrobeDialog") != null) return;
        var dialog = new ConfirmationDialog { Name = "LeaveWardrobeDialog", Title = "Cambios sin guardar",
            OkButtonText = "Salir sin guardar", CancelButtonText = "Seguir aquí" };
        MenuDialog.Fill(dialog, MenuDialog.Message("Si sales ahora perderás lo que te probaste."), 420, destructive: true);
        AddChild(dialog);
        dialog.Confirmed += () => LeaveWardrobe(next);
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        MenuDialog.Show(dialog);
    }

    private void LeaveWardrobe(MenuPage next)
    {
        _leavingWardrobe = true;
        ShowPage(next);
        _leavingWardrobe = false;
    }

    private void OpenDecks() => ShowPage(MenuPage.Decks);

    private void ReturnToPlay()
    {
        // A room or a sign-out does not wait for the wardrobe.
        if (_page != MenuPage.Play) LeaveWardrobe(MenuPage.Play);
    }

    private void PresentNavigation(bool ready, bool busy, bool searching, bool inRoom)
    {
        var reason = inRoom ? "Cierra tu sala para cambiar esto." : searching ? "Cancela la búsqueda para cambiar esto."
            : busy ? "Espera a que termine la solicitud." : !ready ? "Conéctate para cambiar esto." : "";
        // An open editor survives an outage; only the way into it closes.
        _shell.Navigation.Lock(MenuPage.Decks, reason);
        _shell.Navigation.Lock(MenuPage.Customize, reason);
        _shell.Navigation.Lock(MenuPage.Profile, reason);
        var profile = _lobby.Profile;
        var name = _auth != null && _lobby.Account?.Name is { Length: > 0 } saved ? saved : _name.Text;
        var detail = _lobby.Account?.Guest == true ? "Invitado"
            : _lobby.Season is { Open: true } && _lobby.Standing is { Played: > 0 } standing ? CompetitiveRules.Standing(standing.Value, standing.Played)
            : ProfileCatalog.Title(profile?.Title).Name;
        _shell.Navigation.Present(name, detail, profile?.Avatar ?? ProfileCatalog.DefaultAvatar, profile?.Frame ?? ProfileCatalog.DefaultFrame);
    }

    /// <summary>A page's scrolling, centred column under its title.</summary>
    private static (Control Page, VBoxContainer Column) Page(string name, string title)
    {
        var page = new MenuColumn { Name = name };
        page.Title = title;
        return (page, page.Column);
    }
}

/// <summary>A titled, centred, scrolling column: the body of Perfil and Ajustes.</summary>
public partial class MenuColumn : ScrollContainer
{
    private const int MaxWidth = 900;
    public string Title { get; set; } = "";
    public VBoxContainer Column { get; } = new();
    private readonly MarginContainer _margin = new();
    private Label _heading = null!;

    public override void _Ready()
    {
        HorizontalScrollMode = ScrollMode.Disabled; FollowFocus = true;
        _margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_margin);
        var outer = new VBoxContainer(); outer.AddThemeConstantOverride("separation", 16); _margin.AddChild(outer);
        _heading = ArenaPresentation.Heading(Title, MenuVisuals.Type.Hero, BoardArt.Ink);
        outer.AddChild(_heading);
        Column.AddThemeConstantOverride("separation", 16);
        outer.AddChild(Column);
        Resized += Arrange; Arrange();
    }

    private void Arrange()
    {
        var shortScreen = GetViewportRect().Size.Y < 600;
        var side = (int)Math.Max(shortScreen ? 12 : 24, (Size.X - MaxWidth) / 2);
        foreach (var edge in new[] { "left", "right" }) _margin.AddThemeConstantOverride("margin_" + edge, side);
        foreach (var edge in new[] { "top", "bottom" }) _margin.AddThemeConstantOverride("margin_" + edge, shortScreen ? 10 : 28);
        _heading.AddThemeFontSizeOverride("font_size", shortScreen ? MenuVisuals.Type.Title : MenuVisuals.Type.Hero);
    }
}

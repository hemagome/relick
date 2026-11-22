using Godot;

namespace Relick;

public enum MenuPage { Play, Decks, Customize, Profile, Settings }

/// <summary>The frame of every screen outside the duel: a bar on top, or a rail at the left when the screen is short.</summary>
public partial class MenuShell : Container
{
    internal const int BarHeight = 76, RailWidth = 92;

    public MenuNavigation Navigation { get; } = new() { Name = "MenuNavigation" };
    public Control Body { get; } = new() { Name = "MenuBody", MouseFilter = MouseFilterEnum.Ignore };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(Navigation); AddChild(Body);
        Navigation.VisibilityChanged += QueueSort;
        Resized += Arrange;
        Arrange();
    }

    private void Arrange()
    {
        Navigation.Arrange(ArenaViewport.Short(this), Size.X);
        QueueSort();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren || !IsNodeReady()) return;
        var bar = Vector2.Zero;
        if (Navigation.Visible)
        {
            if (Navigation.Rail)
            {
                FitChildInRect(Navigation, new(0, 0, RailWidth, Size.Y));
                bar = new(RailWidth, 0);
            }
            else
            {
                FitChildInRect(Navigation, new(0, 0, Size.X, BarHeight));
                bar = new(0, BarHeight);
            }
        }
        FitChildInRect(Body, new(bar, Size - bar));
    }
}

/// <summary>The tabs, the player and the settings. One button per page, in both orientations.</summary>
public partial class MenuNavigation : PanelContainer
{
    public event Action<MenuPage>? Chosen;
    public bool Rail { get; private set; }

    private readonly Dictionary<MenuPage, NavigationButton> _buttons = [];
    private BoxContainer _box = null!, _pages = null!, _end = null!;
    private TextureRect _logo = null!;
    private Control _space = null!;
    private ProfileAvatar _avatar = null!;
    private Button _player = null!;
    private Label _name = null!, _detail = null!;
    private VBoxContainer _who = null!;
    private MenuPage _current = MenuPage.Play;

    internal IReadOnlyDictionary<MenuPage, NavigationButton> Buttons => _buttons;

    public override void _Ready()
    {
        _box = new BoxContainer(); AddChild(_box);
        _logo = RelickVisuals.Logo(compact: true); _logo.SizeFlagsVertical = SizeFlags.ShrinkCenter; _box.AddChild(_logo);
        _pages = new BoxContainer(); _box.AddChild(_pages);
        foreach (var (page, icon, title) in Pages())
        {
            var button = new NavigationButton { Name = "Nav" + page, Symbol = icon, Caption = title };
            button.Pressed += () => Chosen?.Invoke(page);
            _pages.AddChild(button);
            _buttons.Add(page, button);
        }
        _space = new Control { MouseFilter = MouseFilterEnum.Ignore }; _box.AddChild(_space);
        _end = new BoxContainer(); _box.AddChild(_end);

        _player = new Button { Name = "NavPlayer", TooltipText = "Mi perfil", AccessibilityName = "Mi perfil" };
        foreach (var state in new[] { "normal", "hover", "pressed" }) _player.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10); _player.AddChild(row);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
        _avatar = new ProfileAvatar { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new(48, 48), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(_avatar);
        _who = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _who.AddThemeConstantOverride("separation", 0); row.AddChild(_who);
        _name = ArenaPresentation.Text("", MenuVisuals.Type.Control); _name.Name = "NavPlayerName";
        _detail = ArenaPresentation.Text("", MenuVisuals.Type.Caption, BoardArt.Muted); _detail.Name = "NavPlayerDetail";
        foreach (var label in new[] { _name, _detail }) { label.ClipText = true; label.MouseFilter = MouseFilterEnum.Ignore; _who.AddChild(label); }
        _player.Pressed += () => Chosen?.Invoke(MenuPage.Profile);
        _end.AddChild(_player);

        var settings = new NavigationButton { Name = "NavSettings", Symbol = GameIcon.Gear, Caption = "Ajustes" };
        settings.Pressed += () => Chosen?.Invoke(MenuPage.Settings);
        _end.AddChild(settings);
        _buttons.Add(MenuPage.Settings, settings);
        Select(_current);
    }

    private static IEnumerable<(MenuPage, GameIcon, string)> Pages() =>
    [
        (MenuPage.Play, GameIcon.Sword, "Jugar"),
        (MenuPage.Decks, GameIcon.Cards, "Mazos"),
        (MenuPage.Customize, GameIcon.Brush, "Personalizar"),
        (MenuPage.Profile, GameIcon.Person, "Perfil")
    ];

    public void Arrange(bool rail, float width)
    {
        if (!IsNodeReady()) return;
        Rail = rail;
        var style = new StyleBoxFlat { BgColor = BoardArt.Tint(MenuVisuals.Tone.Background, 0.88f), BorderColor = MenuVisuals.Tone.Divider,
            BorderWidthBottom = rail ? 0 : 1, BorderWidthRight = rail ? 1 : 0,
            ContentMarginLeft = rail ? 4 : width < 1200 ? 12 : 28, ContentMarginRight = rail ? 4 : width < 1200 ? 12 : 28,
            ContentMarginTop = rail ? 6 : 0, ContentMarginBottom = rail ? 6 : 0 };
        AddThemeStyleboxOverride("panel", style);
        foreach (var box in new[] { _box, _pages, _end }) box.Vertical = rail;
        _box.AddThemeConstantOverride("separation", rail ? 2 : width < 1200 ? 10 : 28);
        _pages.AddThemeConstantOverride("separation", rail ? 2 : 4);
        _end.AddThemeConstantOverride("separation", rail ? 2 : 4);
        _logo.Visible = !rail && width >= 1000;
        _space.SizeFlagsHorizontal = rail ? SizeFlags.Fill : SizeFlags.ExpandFill;
        _space.SizeFlagsVertical = rail ? SizeFlags.ExpandFill : SizeFlags.Fill;
        _player.Visible = !rail;
        _who.Visible = width >= 1100;
        _player.CustomMinimumSize = new(width >= 1100 ? 230 : 56, 56);
        _player.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        // With four tabs, narrow bars keep the symbol only beside the settings.
        foreach (var (page, button) in _buttons) button.Arrange(rail, iconOnly: !rail && (page == MenuPage.Settings || width < 800));
    }

    public void Select(MenuPage page)
    {
        _current = page;
        foreach (var (key, button) in _buttons) button.Current = key == page;
    }

    public void Lock(MenuPage page, string reason)
    {
        if (!_buttons.TryGetValue(page, out var button)) return;
        button.Disabled = reason.Length > 0;
        button.TooltipText = reason.Length > 0 ? reason : button.Caption;
        if (page == MenuPage.Profile) _player.Disabled = reason.Length > 0;
        button.QueueRedraw();
    }

    public void Present(string name, string detail, string avatar, string frame)
    {
        _name.Text = name; _detail.Text = detail;
        _avatar.Avatar = avatar; _avatar.Frame = frame;
    }
}

/// <summary>A page of the menu: symbol and caption, side by side in the bar or stacked in the rail.</summary>
public partial class NavigationButton : Button
{
    public GameIcon Symbol { get; init; }
    public string Caption { get; init; } = "";
    private bool _current, _rail, _iconOnly;

    public bool Current
    {
        get => _current;
        set { if (_current == value) return; _current = value; QueueRedraw(); }
    }

    public override void _Ready()
    {
        TooltipText = AccessibilityName = Caption;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; Resized += QueueRedraw;
    }

    public void Arrange(bool rail, bool iconOnly)
    {
        _rail = rail; _iconOnly = iconOnly;
        var text = GetThemeFont("font").GetStringSize(Caption, fontSize: MenuVisuals.Type.Control).X;
        CustomMinimumSize = rail ? new(MenuShell.RailWidth - 8, 60) : iconOnly ? new(56, 56) : new(text + 64, MenuShell.BarHeight);
        SizeFlagsVertical = iconOnly ? SizeFlags.ShrinkCenter : SizeFlags.Fill;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var color = Disabled ? BoardArt.Tint(BoardArt.Muted, 0.45f) : _current ? BoardArt.Gold : IsHovered() ? Colors.White : MenuVisuals.Tone.Reading;
        var font = GetThemeFont("font");
        if (_rail)
        {
            if (_current)
            {
                BoardArt.Plate(this, new(Vector2.Zero, Size), MenuVisuals.Tone.Chosen, MenuVisuals.Tone.Divider, MenuVisuals.Cut.Control);
                DrawRect(new(0, 10, 3, Size.Y - 20), BoardArt.Gold);
            }
            BoardArt.Icon(this, Symbol, new(Size.X / 2, 22), 22, color);
            DrawString(font, new(0, 51), Caption, HorizontalAlignment.Center, Size.X, MenuVisuals.Type.Caption, color);
            return;
        }
        if (_current) DrawRect(new(8, Size.Y - 4, Size.X - 16, 4), BoardArt.Gold);
        if (_iconOnly) { BoardArt.Icon(this, Symbol, Size / 2, 26, color); return; }
        BoardArt.Icon(this, Symbol, new(26, Size.Y / 2), 20, color);
        DrawString(font, new(46, Size.Y / 2 + 6), Caption, HorizontalAlignment.Left, -1, MenuVisuals.Type.Control, color);
    }
}

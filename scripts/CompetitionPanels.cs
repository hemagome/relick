using Godot;

namespace Relick;

/// <summary>The frame the ranked and tournament screens share: title, Volver and two panels.</summary>
public abstract partial class CompetitionPanel : MarginContainer
{
    public event Action? Closed;
    protected abstract string Title { get; }
    protected VBoxContainer Left { get; private set; } = null!;
    protected VBoxContainer Right { get; private set; } = null!;
    protected bool Compact { get; private set; }
    protected virtual float LeftShare => 0.8f;
    private Label _error = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Compact = GetViewportRect().Size.Y < 720;
        Resized += Arrange; Arrange();
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", Compact ? 6 : 16); AddChild(column);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); column.AddChild(header);
        MenuVisuals.Back(header, "CompetitionBack", "Jugar", () => Closed?.Invoke());
        header.AddChild(ArenaPresentation.Heading(Title, Compact ? MenuVisuals.Type.Title : MenuVisuals.Type.Hero, BoardArt.Ink));
        _error = Line(column, "", MenuVisuals.Type.Body, ArenaPresentation.Coral); _error.Name = "CompetitionError"; _error.Visible = false;
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", Compact ? 8 : 20); column.AddChild(body);
        Left = Side(body, LeftShare);
        Right = Side(body, 1);
        Build();
    }

    private void Arrange()
    {
        var compact = GetViewportRect().Size.Y < 720;
        var side = (int)Math.Max(compact ? 8 : 24, (Size.X - 1240) / 2);
        foreach (var edge in new[] { "left", "right" }) AddThemeConstantOverride("margin_" + edge, side);
        foreach (var edge in new[] { "top", "bottom" }) AddThemeConstantOverride("margin_" + edge, compact ? 8 : 24);
    }

    private VBoxContainer Side(Node parent, float share)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = share };
        var style = RelickVisuals.Frame(MenuVisuals.Tone.Surface, MenuVisuals.Tone.Edge, MenuVisuals.Cut.Panel);
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = Compact ? 10 : 24;
        panel.AddThemeStyleboxOverride("panel", style); parent.AddChild(panel);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", Compact ? 4 : 12); panel.AddChild(content);
        return content;
    }

    protected abstract void Build();

    protected void ShowError(string error)
    {
        _error.Text = error; _error.Visible = error.Length > 0;
    }

    protected static Label Line(Node parent, string text, int size, Color? color = null)
    {
        var label = ArenaPresentation.Text(text, size, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        parent.AddChild(label);
        return label;
    }

    protected static VBoxContainer Scrolling(Node parent, string name, int separation)
    {
        var scroll = new ScrollContainer { Name = name, SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        parent.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", separation);
        scroll.AddChild(column);
        return column;
    }
}

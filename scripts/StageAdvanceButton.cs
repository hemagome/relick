using Godot;

namespace Relick;

/// <summary>A gentle gold pulse suggests advancing after all legal plays are exhausted.</summary>
public partial class StageAdvanceButton : Button
{
    public bool Suggested { get; set; }
    public bool IsGlowing => Suggested && !Disabled;
    private StyleBoxFlat _halo = null!;

    public override void _Ready()
    {
        MouseDefaultCursorShape = Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        if (IsGlowing)
        {
            foreach (var state in new[] { "normal", "hover", "pressed" })
                AddThemeStyleboxOverride(state, new StyleBoxFlat
                {
                    BgColor = new Color(state == "pressed" ? "51422c" : state == "hover" ? "3d352b" : "302c29"),
                    BorderColor = BoardArt.Gold,
                    BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                    CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                    ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12
                });
            AddThemeColorOverride("font_color", BoardArt.Gold);
            AddThemeColorOverride("font_hover_color", BoardArt.Gold.Lightened(0.2f));
            AddThemeColorOverride("font_pressed_color", BoardArt.Gold);
            _halo = new StyleBoxFlat
            {
                DrawCenter = false, ShadowSize = 9,
                BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
            };
        }
        Resized += QueueRedraw;
        SetProcess(IsGlowing);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (!IsGlowing) return;
        // Use a shared clock so selecting a card doesn't restart the pulse on UI rebuilds.
        var pulse = (1 + Mathf.Sin((float)(Time.GetTicksMsec() % 1600) / 1600 * Mathf.Tau)) / 2;
        _halo.BorderColor = BoardArt.Tint(BoardArt.Gold, 0.65f + pulse * 0.3f);
        _halo.ShadowColor = BoardArt.Tint(BoardArt.Gold, 0.18f + pulse * 0.2f);
        DrawStyleBox(_halo, new Rect2(Vector2.Zero, Size));
    }
}

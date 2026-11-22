using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Two Relikario cards, fanned: the game, before signing in.</summary>
public partial class LoginArt : Control
{
    private Texture2D? _front, _behind;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _front = RelikarioArt.Card(RelikarioCatalog.Yuno.Id, 1);
        _behind = RelikarioArt.Card(RelikarioCatalog.Osamu.Id, 1);
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        if (Size.X < 40 || Size.Y < 40) return;
        void Card(Texture2D? card, float x, float width, float height, float degrees)
        {
            if (card == null) return;
            var rect = BoardArt.FitArtwork(card, new Rect2(-Size.X * width / 2, -Size.Y * height / 2, Size.X * width, Size.Y * height));
            DrawSetTransform(new(Size.X * x, Size.Y / 2), Mathf.DegToRad(degrees));
            RelickVisuals.Shadow(this, rect, new Color(0, 0, 0, 0.5f), 18, MenuVisuals.Cut.Panel);
            DrawTextureRect(card, rect, false);
        }
        Card(_behind, 0.32f, 0.48f, 0.72f, -9);
        Card(_front, 0.64f, 0.56f, 0.84f, 4);
        DrawSetTransform(Vector2.Zero);
    }
}

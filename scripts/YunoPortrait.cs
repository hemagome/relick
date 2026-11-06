using Godot;

namespace Relick;

public enum PortraitMotion { Idle, Attack, Hit }

/// <summary>One fixed canvas shared by all clips. UI rebuilds reparent this node, preserving playback.</summary>
public partial class YunoPortrait : Control
{
    public static double DurationFor(PortraitMotion motion) => motion switch
    {
        PortraitMotion.Attack => 1.2,
        PortraitMotion.Hit => 0.9,
        _ => 2.0
    };
    private const int Columns = 8;
    private const int FrameWidth = 608;
    private const int FrameHeight = 504;
    private readonly Dictionary<PortraitMotion, Texture2D> _sheets = new();
    private double _elapsed;

    public float PortraitHeight { get; set; } = 236;
    public bool FlipHorizontal { get; set; }
    public Color AccentColor { get; set; } = new("75e6bd");

    public PortraitMotion Motion { get; private set; }
    public bool IsActionPlaying => Motion != PortraitMotion.Idle;
    public int FrameCount => Motion == PortraitMotion.Attack ? 56 : 64;
    public double DurationSeconds => DurationFor(Motion);
    // Stabilize exact frame boundaries reached through repeated 60/120 Hz delta additions.
    public int CurrentFrame => Math.Min(FrameCount - 1, (int)(_elapsed / DurationSeconds * FrameCount + 1e-9));
    public bool AssetsReady => _sheets.Count == 3;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        ClipContents = true;
        CustomMinimumSize = new Vector2(0, PortraitHeight);
        foreach (var (motion, file) in new[]
        {
            (PortraitMotion.Idle, "idle"), (PortraitMotion.Attack, "attack"), (PortraitMotion.Hit, "hit")
        })
        {
            var texture = GD.Load<Texture2D>($"res://assets/characters/yuno/{file}.png");
            var rows = motion == PortraitMotion.Attack ? 7 : 8;
            if (texture == null || texture.GetWidth() != Columns * FrameWidth || texture.GetHeight() != rows * FrameHeight)
                throw new InvalidOperationException($"Hoja de Yuno inválida: {file}. Se esperaba {Columns * FrameWidth} × {rows * FrameHeight}.");
            _sheets.Add(motion, texture);
        }
        QueueRedraw();
    }

    public void Play(PortraitMotion motion)
    {
        Motion = motion;
        _elapsed = 0;
        QueueRedraw();
    }

    public override void _Process(double delta) => AdvancePlayback(delta);

    internal void AdvancePlayback(double delta)
    {
        _elapsed += delta;
        if (_elapsed >= DurationSeconds)
        {
            if (Motion == PortraitMotion.Idle) _elapsed %= DurationSeconds;
            else { Motion = PortraitMotion.Idle; _elapsed = 0; }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_sheets.TryGetValue(Motion, out var texture)) return;
        var halo = new Vector2(Size.X / 2, Size.Y * 0.56f);
        var radius = Math.Min(Size.X * 0.46f, Size.Y * 0.48f);
        for (var i = 3; i >= 1; i--) DrawCircle(halo, radius * i / 3, BoardArt.Tint(AccentColor, 0.025f));
        DrawArc(halo, radius, -Mathf.Pi * 0.9f, Mathf.Pi * 0.7f, 64, BoardArt.Tint(AccentColor, 0.16f), 1, true);
        var font = ThemeDB.FallbackFont;
        if (IsActionPlaying)
        {
            var caption = Motion == PortraitMotion.Attack ? "ATAQUE" : "RECIBIENDO DAÑO";
            DrawCircle(new Vector2(5, 9), 3, Motion == PortraitMotion.Hit ? new Color("f08076") : AccentColor);
            DrawString(font, new Vector2(15, 13), caption, HorizontalAlignment.Left, -1, 11, new Color("a2afbc"));
        }
        var scale = Math.Min(Size.X / FrameWidth, Math.Max(0, Size.Y - 24) / FrameHeight);
        var extent = new Vector2(FrameWidth, FrameHeight) * scale;
        var target = new Rect2(new Vector2((Size.X - extent.X) / 2, Size.Y - extent.Y), extent);
        var source = new Rect2((CurrentFrame % Columns) * FrameWidth, (CurrentFrame / Columns) * FrameHeight, FrameWidth, FrameHeight);
        if (FlipHorizontal) DrawSetTransform(new Vector2(Size.X, 0), 0, new Vector2(-1, 1));
        DrawTextureRectRegion(texture, target, source);
        if (FlipHorizontal) DrawSetTransform(Vector2.Zero);
    }
}

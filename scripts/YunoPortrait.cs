using Godot;

namespace Relick;

public enum PortraitMotion { Idle, Attack, Hit, ExhaustedIdle, Heal, HitNormal, HitCritical }

/// <summary>One fixed canvas shared by all clips. UI rebuilds reparent this node, preserving playback.</summary>
public partial class YunoPortrait : Control
{
    private sealed record Clip(string File, int Frames, double Seconds, bool Loops);
    private static readonly IReadOnlyDictionary<PortraitMotion, Clip> Clips = new Dictionary<PortraitMotion, Clip>
    {
        [PortraitMotion.Idle] = new("idle", 64, 2.0, true),
        [PortraitMotion.Attack] = new("attack", 56, 1.2, false),
        [PortraitMotion.Hit] = new("hit", 64, 0.9, false),
        [PortraitMotion.ExhaustedIdle] = new("exhausted-idle", 16, 2.8, true),
        [PortraitMotion.Heal] = new("heal", 16, 1.2, false),
        [PortraitMotion.HitNormal] = new("hit-normal", 16, 0.9, false),
        [PortraitMotion.HitCritical] = new("hit-critical", 16, 1.2, false)
    };
    public static double DurationFor(PortraitMotion motion) => Clips[motion].Seconds;
    private const int Columns = 8;
    private const int FrameWidth = 608;
    private const int FrameHeight = 504;
    private readonly Dictionary<PortraitMotion, Texture2D> _sheets = new();
    private double _elapsed;
    private PortraitMotion _restMotion;

    public float PortraitHeight { get; set; } = 236;
    public bool FlipHorizontal { get; set; }
    public Color AccentColor { get; set; } = new("75e6bd");

    public PortraitMotion Motion { get; private set; }
    public bool IsActionPlaying => !Clips[Motion].Loops;
    public int FrameCount => Clips[Motion].Frames;
    public double DurationSeconds => DurationFor(Motion);
    // Stabilize exact frame boundaries reached through repeated 60/120 Hz delta additions.
    public int CurrentFrame => Math.Min(FrameCount - 1, (int)(_elapsed / DurationSeconds * FrameCount + 1e-9));
    public bool AssetsReady => _sheets.Count == Clips.Count;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        ClipContents = true;
        CustomMinimumSize = new Vector2(0, PortraitHeight);
        foreach (var (motion, clip) in Clips)
        {
            var file = clip.File;
            var texture = GD.Load<Texture2D>($"res://assets/characters/yuno/{file}.png");
            var rows = clip.Frames / Columns;
            if (texture == null || texture.GetWidth() % Columns != 0 || texture.GetHeight() % rows != 0 ||
                texture.GetWidth() / Columns * FrameHeight != texture.GetHeight() / rows * FrameWidth)
                throw new InvalidOperationException($"Hoja de Yuno inválida: {file}. Debe conservar la cuadrícula {Columns} × {rows} y la proporción de cada fotograma.");
            _sheets.Add(motion, texture);
        }
        QueueRedraw();
    }

    public void SetHealth(int health)
    {
        var rest = health <= 0 ? PortraitMotion.ExhaustedIdle : PortraitMotion.Idle;
        if (_restMotion == rest) return;
        _restMotion = rest;
        // Let the final hit finish before entering the zero-health breathing loop.
        if (!IsActionPlaying) ReturnToRest();
    }

    public void ReturnToRest() => Play(_restMotion);

    public void Play(PortraitMotion motion)
    {
        Motion = motion;
        _elapsed = 0;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) AdvancePlayback(delta);
    }

    internal void AdvancePlayback(double delta)
    {
        var previousFrame = CurrentFrame;
        var previousMotion = Motion;
        _elapsed += delta;
        if (_elapsed >= DurationSeconds)
        {
            if (!IsActionPlaying) _elapsed %= DurationSeconds;
            else { Motion = _restMotion; _elapsed = 0; }
        }
        if (CurrentFrame != previousFrame || Motion != previousMotion) QueueRedraw();
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
            var caption = Motion switch
            {
                PortraitMotion.Attack => "ATAQUE", PortraitMotion.Heal => "CURACIÓN",
                PortraitMotion.HitNormal => "DAÑO NORMAL", PortraitMotion.HitCritical => "¡CRÍTICO!", _ => "RECIBIENDO DAÑO"
            };
            var color = Motion switch
            {
                PortraitMotion.Hit or PortraitMotion.HitNormal => new Color("f08076"),
                PortraitMotion.HitCritical => new Color("f4bf75"), PortraitMotion.Heal => new Color("53d49e"), _ => AccentColor
            };
            DrawCircle(new Vector2(5, 9), 3, color);
            DrawString(font, new Vector2(15, 13), caption, HorizontalAlignment.Left, -1, 11, new Color("a2afbc"));
        }
        var scale = Math.Min(Size.X / FrameWidth, Math.Max(0, Size.Y - 24) / FrameHeight);
        var extent = new Vector2(FrameWidth, FrameHeight) * scale;
        var target = new Rect2(new Vector2((Size.X - extent.X) / 2, Size.Y - extent.Y), extent);
        var frameWidth = texture.GetWidth() / Columns;
        var frameHeight = texture.GetHeight() / (FrameCount / Columns);
        var source = new Rect2((CurrentFrame % Columns) * frameWidth, (CurrentFrame / Columns) * frameHeight, frameWidth, frameHeight);
        if (FlipHorizontal) DrawSetTransform(new Vector2(Size.X, 0), 0, new Vector2(-1, 1));
        DrawTextureRectRegion(texture, target, source);
        if (FlipHorizontal) DrawSetTransform(Vector2.Zero);
    }
}

using Godot;
using Relick.Core;

namespace Relick;

public enum PortraitMotion { Idle, Attack, Hit, ExhaustedIdle, Heal, HitNormal, HitCritical }

/// <summary>
/// A Relikario's animated figure: one fixed canvas shared by all clips, read from that Relikario's art folder
/// (<see cref="RelikarioArt"/>). UI rebuilds reparent this node, preserving playback. A Relikario without clips
/// draws an empty stand instead of failing.
/// </summary>
public partial class RelikarioPortrait : Control
{
    // A still clip is one drawing moved by a shader instead of a sheet of frames.
    private sealed record Clip(string File, int Frames, double Seconds, bool Loops, bool Still = false);
    private static readonly IReadOnlyDictionary<PortraitMotion, Clip> Clips = new Dictionary<PortraitMotion, Clip>
    {
        [PortraitMotion.Idle] = new("idle-rest", 1, 3.6, true, Still: true),
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
    private const int StillPadding = 16;
    private Control _sprite = null!;
    private ShaderMaterial? _restMaterial;
    internal bool ReducedMotion { get; private set; }
    /// <summary>The rest pose moves continuously; it has no frames to step through.</summary>
    public bool IsContinuous => Clips[Motion].Still;
    private readonly Dictionary<PortraitMotion, Texture2D> _sheets = new();
    private double _elapsed;
    private PortraitMotion _restMotion;

    public float PortraitHeight { get; set; } = 236;
    /// <summary>Receives hover so the arena can read this Relikario in the inspector; clicks still reach the board.</summary>
    public bool Inspectable { get; init; }
    public bool FlipHorizontal { get; set; }
    public Color AccentColor { get; set; } = new("75e6bd");

    public PortraitMotion Motion { get; private set; }
    public bool IsActionPlaying => !Clips[Motion].Loops;
    public int FrameCount => Clips[Motion].Frames;
    public double DurationSeconds => DurationFor(Motion);
    // Stabilize exact frame boundaries reached through repeated 60/120 Hz delta additions.
    public int CurrentFrame => Math.Min(FrameCount - 1, (int)(_elapsed / DurationSeconds * FrameCount + 1e-9));
    public bool AssetsReady => _sheets.Count == Clips.Count;
    /// <summary>Any form's Id; forms of one Relikario share their clips.</summary>
    public string RelikarioId { get; private set; } = RelikarioCatalog.Yuno.Id;

    public override void _Ready()
    {
        MouseFilter = Inspectable ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        ClipContents = true;
        if (CustomMinimumSize.Y <= 0) CustomMinimumSize = new Vector2(0, PortraitHeight);
        ReducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
            ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool();
        LoadClips();
        _restMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/yuno_idle.gdshader") };
        _restMaterial.SetShaderParameter("phase", FlipHorizontal ? 1.7f : 0f);
        _restMaterial.SetShaderParameter("strength", ReducedMotion ? 0.35f : 1f);
        // The figure has its own canvas item so the rest shader never touches the halo or caption.
        _sprite = new Control { Name = "Figure", MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Linear };
        AddChild(_sprite); _sprite.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _sprite.Draw += DrawFigure;
        Resized += Refresh;
        Refresh();
    }

    /// <summary>Shows another Relikario; a new form of the same Relikario keeps the clips and the playback.</summary>
    public void SetRelikario(string relikario)
    {
        if (RelikarioArt.Family(relikario) == RelikarioArt.Family(RelikarioId)) { RelikarioId = relikario; return; }
        RelikarioId = relikario;
        if (_sprite == null) return;
        LoadClips();
        Refresh();
    }

    private void LoadClips()
    {
        _sheets.Clear();
        var name = RelikarioCatalog.NameOf(RelikarioId);
        foreach (var (motion, clip) in Clips)
        {
            var file = clip.File;
            // A missing clip leaves the stand empty; a malformed one is an art error worth stopping for.
            if (RelikarioArt.Load(RelikarioId, file) is not { } texture) continue;
            if (clip.Still)
            {
                // Imported at half size like the sheets; only the padded canvas proportion matters.
                if (texture.GetWidth() * (FrameHeight + StillPadding * 2) != texture.GetHeight() * (FrameWidth + StillPadding * 2))
                    throw new InvalidOperationException($"Imagen de reposo de {name} inválida: {file}. Debe conservar la proporción {FrameWidth + StillPadding * 2} × {FrameHeight + StillPadding * 2}.");
                _sheets.Add(motion, texture);
                continue;
            }
            var rows = clip.Frames / Columns;
            if (texture.GetWidth() % Columns != 0 || texture.GetHeight() % rows != 0 ||
                texture.GetWidth() / Columns * FrameHeight != texture.GetHeight() / rows * FrameWidth)
                throw new InvalidOperationException($"Hoja de {name} inválida: {file}. Debe conservar la cuadrícula {Columns} × {rows} y la proporción de cada fotograma.");
            _sheets.Add(motion, texture);
        }
    }

    private void Refresh()
    {
        if (_sprite == null) return;
        _sprite.Material = IsContinuous ? _restMaterial : null;
        QueueRedraw(); _sprite.QueueRedraw();
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
        Refresh();
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
        if (CurrentFrame != previousFrame || Motion != previousMotion) Refresh();
    }

    private (Vector2 Halo, float Radius, Rect2 Target) Placement()
    {
        const float captionSpace = 24;
        var availableHeight = Math.Max(0, Size.Y - captionSpace);
        var scale = Math.Min(Size.X / FrameWidth, availableHeight / FrameHeight);
        var extent = new Vector2(FrameWidth, FrameHeight) * scale;
        var radius = Math.Min(Size.X * 0.46f, availableHeight / 2);
        // Keep the sprite and halo together just above the PR slots as the panel stretches.
        var halo = new Vector2(Size.X / 2, Size.Y - Math.Max(radius, extent.Y / 2));
        return (halo, radius, new Rect2(halo - extent / 2, extent));
    }

    public override void _Draw()
    {
        if (!_sheets.ContainsKey(Motion)) return;
        var (halo, radius, _) = Placement();
        for (var i = 3; i >= 1; i--) DrawCircle(halo, radius * i / 3, BoardArt.Tint(AccentColor, 0.025f));
        DrawArc(halo, radius, -Mathf.Pi * 0.9f, Mathf.Pi * 0.7f, 64, BoardArt.Tint(AccentColor, 0.16f), 1, true);
        if (!IsActionPlaying) return;
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
        DrawString(ThemeDB.FallbackFont, new Vector2(15, 13), caption, HorizontalAlignment.Left, -1, 11, new Color("a2afbc"));
    }

    private void DrawFigure()
    {
        if (!_sheets.TryGetValue(Motion, out var texture)) return;
        var target = Placement().Target;
        if (FlipHorizontal) _sprite.DrawSetTransform(new Vector2(Size.X, 0), 0, new Vector2(-1, 1));
        if (IsContinuous)
        {
            // The still carries a transparent margin so the breath can lift the hair past the canvas edge.
            var margin = StillPadding * target.Size.X / FrameWidth;
            _sprite.DrawTextureRect(texture, target.Grow(margin), false);
            return;
        }
        var frameWidth = texture.GetWidth() / Columns;
        var frameHeight = texture.GetHeight() / (FrameCount / Columns);
        var source = new Rect2((CurrentFrame % Columns) * frameWidth, (CurrentFrame / Columns) * frameHeight, frameWidth, frameHeight);
        _sprite.DrawTextureRectRegion(texture, target, source);
    }
}

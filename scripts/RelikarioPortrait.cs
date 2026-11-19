using Godot;
using Relick.Core;

namespace Relick;

public enum PortraitMotion { Idle, Attack, Hit, ExhaustedIdle, Heal, HitNormal, HitCritical }

/// <summary>
/// A Relikario's animated figure: one fixed canvas shared by all clips, read from that Relikario's art folder
/// (<see cref="RelikarioArt"/>). UI rebuilds reparent this node, preserving playback. A Relikario without clips
/// uses its static portrait when available.
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
    /// <summary>This pose is sampled continuously from time instead of stepping through a sheet.</summary>
    public bool IsContinuous => _osamu != null || _ironwing != null || Clips[Motion].Still;
    private readonly Dictionary<PortraitMotion, Texture2D> _sheets = new();
    private Texture2D? _staticPortrait;
    private OsamuPortraitRig? _osamu;
    private IronwingPortraitRig? _ironwing;
    internal OsamuPortraitRig.Pose? OsamuPose => _osamu?.Current;
    internal bool UsesIronwingRig => _ironwing != null;
    private double _elapsed;
    private PortraitMotion _restMotion;

    public float PortraitHeight { get; set; } = 236;
    /// <summary>Receives hover so the arena can read this Relikario in the inspector; clicks still reach the board.</summary>
    public bool Inspectable { get; init; }
    public bool FlipHorizontal { get; set; }
    public Color AccentColor { get; set; } = new("75e6bd");
    internal bool StandingOnTable
    {
        get => _standingOnTable;
        set { if (_standingOnTable != value) { _standingOnTable = value; Refresh(); } }
    }
    private bool _standingOnTable;

    public PortraitMotion Motion { get; private set; }
    public bool IsActionPlaying => !Clips[Motion].Loops;
    public int FrameCount => _osamu != null || UsesIronwingRig ? 1 : Clips[Motion].Frames;
    public double DurationSeconds => DurationFor(Motion);
    // Stabilize exact frame boundaries reached through repeated 60/120 Hz delta additions.
    public int CurrentFrame => Math.Min(FrameCount - 1, (int)(_elapsed / DurationSeconds * FrameCount + 1e-9));
    public bool AssetsReady => _osamu != null || UsesIronwingRig || _sheets.Count == Clips.Count;
    /// <summary>Any form's Id; forms of one Relikario share their clips.</summary>
    public string RelikarioId { get; private set; } = RelikarioCatalog.Yuno.Id;

    internal (Vector2 Point, float Width) AttackLaunch(bool lob)
    {
        // Match the glove in Yuno's extended frame and the hand in Osamu's release pose.
        var target = Placement().Target;
        var transform = GetGlobalTransform();
        return (FigurePoint(target, lob ? new(.67f, .54f) : new(.79f, .69f)), target.Size.X * transform.Scale.X);
    }

    internal (Vector2 Far, Vector2 Near) IronwingGuns()
    {
        var (far, near) = _ironwing!.Guns(Placement().Target, FlipHorizontal);
        var transform = GetGlobalTransform();
        return (transform * far, transform * near);
    }

    internal Vector2 AttackTarget
    {
        get
        {
            var target = Placement().Target;
            return _ironwing != null ? GetGlobalTransform() * _ironwing.Target(target, FlipHorizontal) :
                FigurePoint(target, _osamu != null ? new(.32f, .50f) : new(.44f, .62f));
        }
    }

    private Vector2 FigurePoint(Rect2 target, Vector2 uv)
    {
        if (FlipHorizontal) uv.X = 1 - uv.X;
        return GetGlobalTransform() * (target.Position + target.Size * uv);
    }

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
        _osamu = null;
        _ironwing = null;
        var family = RelikarioArt.Family(RelikarioId);
        if (family == RelikarioCatalog.Osamu.Id &&
            RelikarioArt.Load(RelikarioId, "sprite") is { } body && RelikarioArt.Load(RelikarioId, "turbo-sword") is { } sword)
        {
            _staticPortrait = null;
            _osamu = new OsamuPortraitRig(body, sword);
            _osamu.Advance(Motion, _elapsed, 0, ReducedMotion, FlipHorizontal);
            return;
        }
        if (family == RelikarioCatalog.Ironwing.Id && RelikarioArt.Load(RelikarioId, "sprite") is { } hull)
        {
            _staticPortrait = null;
            _ironwing = new IronwingPortraitRig(hull);
            _ironwing.Advance(Motion, _elapsed, 0, ReducedMotion, FlipHorizontal);
            return;
        }
        var name = RelikarioCatalog.NameOf(RelikarioId);
        foreach (var (motion, clip) in Clips)
        {
            var file = clip.File;
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
        _staticPortrait = _sheets.Count == 0 ? RelikarioArt.Portrait(RelikarioId) : null;
    }

    private void Refresh()
    {
        if (_sprite == null) return;
        _sprite.Material = IsContinuous && _sheets.ContainsKey(Motion) ? _restMaterial : null;
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
        _osamu?.BeginMotion();
        _ironwing?.BeginMotion();
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
            else
            {
                var remaining = _elapsed - DurationSeconds;
                var actionDelta = Math.Max(0, delta - remaining);
                _osamu?.Advance(Motion, DurationSeconds, actionDelta, ReducedMotion, FlipHorizontal);
                _osamu?.BeginMotion();
                _ironwing?.Advance(Motion, DurationSeconds, actionDelta, ReducedMotion, FlipHorizontal);
                _ironwing?.BeginMotion();
                delta = remaining;
                Motion = _restMotion;
                _elapsed = UsesIronwingRig ? remaining % DurationSeconds : 0;
            }
        }
        if (_osamu != null)
        {
            _osamu.Advance(Motion, _elapsed, delta, ReducedMotion, FlipHorizontal);
            _sprite.QueueRedraw();
        }
        if (_ironwing != null)
        {
            _ironwing.Advance(Motion, _elapsed, delta, ReducedMotion, FlipHorizontal);
            _sprite.QueueRedraw();
        }
        if (CurrentFrame != previousFrame || Motion != previousMotion) Refresh();
    }

    private (Vector2 Halo, float Radius, Rect2 Target) Placement()
    {
        if (StandingOnTable)
        {
            var extentOnTable = new Vector2(FrameWidth, FrameHeight) * Math.Min((Size.X - 12) / FrameWidth, (Size.Y - 26) / FrameHeight);
            var feet = new Vector2(Size.X / 2, Size.Y * .94f);
            return (feet, extentOnTable.X * .43f, new Rect2(feet - extentOnTable * new Vector2(.5f, 1), extentOnTable));
        }
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
        if (_osamu == null && _ironwing == null && !_sheets.ContainsKey(Motion) && _staticPortrait == null) return;
        var (halo, radius, _) = Placement();
        if (StandingOnTable)
        {
            DrawSetTransform(halo, 0, new Vector2(1, .14f));
            DrawCircle(new Vector2(0, 13), radius * 1.13f, new Color(0f, .02f, .04f, .48f), antialiased: true);
            DrawCircle(Vector2.Zero, radius, RelickVisuals.Playmat.Background, antialiased: true);
            DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 54, BoardArt.Tint(AccentColor, .7f), 1.3f, true);
            DrawSetTransform(Vector2.Zero);
        }
        else
        {
            for (var i = 3; i >= 1; i--) DrawCircle(halo, radius * i / 3, BoardArt.Tint(AccentColor, 0.025f));
            DrawArc(halo, radius, -Mathf.Pi * 0.9f, Mathf.Pi * 0.7f, 64, BoardArt.Tint(AccentColor, 0.16f), 1, true);
        }
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
        var target = Placement().Target;
        if (_osamu != null)
        {
            _osamu.Draw(_sprite, target, Motion, _elapsed, FlipHorizontal);
            return;
        }
        if (_ironwing != null)
        {
            _ironwing.Draw(_sprite, target, Motion, _elapsed, FlipHorizontal, ReducedMotion);
            return;
        }
        _sprite.DrawSetTransform(Vector2.Zero);
        if (!_sheets.TryGetValue(Motion, out var texture))
        {
            if (_staticPortrait is { } portrait)
                _sprite.DrawTextureRect(portrait, BoardArt.FitArtwork(portrait, target.Grow(-target.Size.Y * 0.12f)), false);
            return;
        }
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

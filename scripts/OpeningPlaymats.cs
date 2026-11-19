using Godot;

namespace Relick;

/// <summary>Unrolls two cloth surfaces in opposite directions before exposing the playable board.</summary>
internal sealed partial class OpeningPlaymats : Control
{
    internal const float Duration = 1.4f;
    private const float JoinAt = 1.02f;
    private const int RollSegments = 32;
    private ShaderMaterial _cloth = null!;
    private BoardCanvas? _board;
    private float _age, _unit;
    private Vector2 _origin;

    internal Vector2 Unrolled { get; private set; }

    public override void _Ready()
    {
        ZIndex = 28;
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        _cloth = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/opening_playmat.gdshader") };
        _cloth.SetShaderParameter("projection", BoardCanvas.ProjectionParameters);
        _cloth.SetShaderParameter("rise", BoardCanvas.PerspectiveRise);
        _cloth.SetShaderParameter("field", new Vector4(BoardCanvas.DesignWidth, BoardCanvas.FarEdge, BoardCanvas.Seam, BoardCanvas.NearEdge));
        var surface = new ColorRect { Material = _cloth, ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(surface);
        surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += PresentFrame;
    }

    internal void Play()
    {
        _age = 0;
        Modulate = Colors.White;
        var mat = RelickVisuals.Playmat;
        _cloth.SetShaderParameter("background", mat.Background);
        _cloth.SetShaderParameter("cloth", mat.Surface);
        _cloth.SetShaderParameter("rival_color", ArenaPresentation.Coral);
        _cloth.SetShaderParameter("own_color", ArenaPresentation.Mint);
        Show();
        PresentFrame();
    }

    internal void Follow(BoardCanvas? board)
    {
        _board = board;
        PresentFrame();
    }

    internal void Stop()
    {
        Hide();
        _board = null;
    }

    internal void AdvancePlayback(double delta)
    {
        _age = Mathf.Min(Duration, _age + (float)delta);
        Modulate = BoardArt.Tint(Colors.White, 1 - Mathf.SmoothStep(1.17f, Duration, _age));
        PresentFrame();
    }

    internal bool Finished => _age >= Duration;

    private void PresentFrame()
    {
        if (!Visible || Size.X <= 0 || Size.Y <= 0) return;
        var design = new Vector2(BoardCanvas.DesignWidth, BoardCanvas.DesignHeight);
        if (IsInstanceValid(_board) && _board!.Size.X > 0 && _board.Size.Y > 0)
        {
            _unit = _board.Unit;
            _origin = _board.GetGlobalRect().Position - GetGlobalRect().Position + (_board.Size - design * _unit) / 2;
        }
        else
        {
            _unit = Math.Min(Size.X * .89f / design.X, Size.Y / design.Y);
            _origin = (Size - design * _unit) / 2;
        }
        // The rival leads by a few frames. Both rolls flatten before the seam lights up.
        Unrolled = new(Mathf.SmoothStep(.08f, .98f, _age), Mathf.SmoothStep(.14f, JoinAt, _age));
        _cloth.SetShaderParameter("viewport_size", Size);
        _cloth.SetShaderParameter("origin", _origin);
        _cloth.SetShaderParameter("unit", _unit);
        _cloth.SetShaderParameter("unrolled", Unrolled);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || _unit <= 0) return;
        DrawSetTransform(_origin, 0, Vector2.One * _unit);
        DrawRoll(true, Unrolled.X);
        DrawRoll(false, Unrolled.Y);
        DrawJoin();
    }

    private static Vector2 At(float x, float y, float height = 0) => BoardCanvas.Project(new(x, y), height);

    private void DrawRoll(bool rival, float travel)
    {
        var remaining = 1 - travel;
        var radius = (12 + 27 * Mathf.Sqrt(remaining)) * Mathf.SmoothStep(0, .10f, remaining);
        if (radius < 1) return;
        var x = BoardCanvas.DesignWidth * (rival ? travel : remaining);
        var far = rival ? BoardCanvas.FarEdge : BoardCanvas.Seam;
        var near = rival ? BoardCanvas.Seam : BoardCanvas.NearEdge;
        var accent = rival ? ArenaPresentation.Coral : ArenaPresentation.Mint;
        var turn = (rival ? -1 : 1) * Mathf.Tau * 9 * (1 - Mathf.Sqrt(remaining));
        var cast = radius * 1.08f;
        DrawColoredPolygon([At(x - cast, far), At(x + cast, far), At(x + cast, near + 9), At(x - cast, near + 9)],
            new Color(0, 0, 0, .30f));

        Vector2 Arc(float y, float angle) => At(x + radius * Mathf.Cos(angle), y, radius * (1 + Mathf.Sin(angle)));
        var dark = RelickVisuals.Playmat.Background.Lerp(accent, .09f);
        var lit = RelickVisuals.Playmat.Surface.Lerp(accent, .44f);
        // Draw the visible half of the cylinder in strips aligned to the tabletop's projection.
        for (var strip = 0; strip < RollSegments; strip++)
        {
            var from = Mathf.Pi * (1 - (float)strip / RollSegments);
            var to = Mathf.Pi * (1 - (float)(strip + 1) / RollSegments);
            var light = Mathf.Exp(-Mathf.Pow(((strip + .5f) / RollSegments - .38f) * 4.3f, 2));
            DrawColoredPolygon([Arc(far, from), Arc(near, from), Arc(near, to), Arc(far, to)], dark.Lerp(lit, light));
        }
        foreach (var y in new[] { far + 5, far + 14, near - 9 })
        {
            var rim = new Vector2[RollSegments + 1];
            for (var i = 0; i < rim.Length; i++) rim[i] = Arc(y, Mathf.Pi * i / RollSegments);
            DrawPolyline(rim, BoardArt.Tint(accent, .48f), 1.6f, true);
        }
        var stripe = Mathf.PosMod(turn + 2.2f, Mathf.Tau);
        if (stripe < Mathf.Pi)
            DrawLine(Arc(far + 14, stripe), Arc(near - 9, stripe), BoardArt.Tint(accent, .32f * Mathf.Sin(stripe)), 1.4f, true);

        var end = At(x, near, radius);
        var endRadius = At(x + radius, near, radius).X - end.X;
        DrawCircle(end, endRadius, dark, antialiased: true);
        DrawArc(end, endRadius - .45f, 0, Mathf.Tau, 40, BoardArt.Tint(accent, .66f), 1.6f, true);
        var spiral = new Vector2[81];
        for (var i = 0; i < spiral.Length; i++)
        {
            var t = (float)i / (spiral.Length - 1);
            var angle = t * Mathf.Tau * 3 + turn;
            spiral[i] = end + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * (endRadius * (.11f + .73f * t));
        }
        DrawPolyline(spiral, BoardArt.Tint(accent, .73f), Math.Min(1.8f, endRadius * .16f), true);
    }

    private void DrawJoin()
    {
        if (_age < JoinAt) return;
        var pulse = Mathf.SmoothStep(JoinAt, 1.32f, _age);
        var left = At(0, BoardCanvas.Seam);
        var right = At(BoardCanvas.DesignWidth, BoardCanvas.Seam);
        var center = (left + right) / 2;
        var energy = Mathf.Sin(pulse * Mathf.Pi);
        DrawLine(left, right, BoardArt.Tint(RelickVisuals.Playmat.Trace, .52f * pulse), 1.5f, true);
        foreach (var (edge, accent) in new[] { (left, ArenaPresentation.Coral), (right, ArenaPresentation.Mint) })
        {
            var tip = center.Lerp(edge, pulse);
            DrawLine(center, tip, BoardArt.Tint(accent, .20f * energy), 14, true);
            DrawLine(center, tip, BoardArt.Tint(accent.Lerp(Colors.White, .56f), .83f * energy), 2.5f, true);
        }
    }
}

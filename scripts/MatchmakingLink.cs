using Godot;

namespace Relick;

public enum MatchmakingLinkState { Idle, Starting, Searching, Canceling, Matched }

// Decorative only: queue membership and matched rooms always come from the server.
public partial class MatchmakingLink : Control
{
    private string? _sleeve;
    /// <summary>The player's active-deck sleeve on their own card; the rival's silhouette stays classic until known.</summary>
    public string? Sleeve
    {
        get => _sleeve;
        set { if (_sleeve == value) return; _sleeve = value; if (IsInsideTree()) { _back = SleeveArt.Back(value); QueueRedraw(); } }
    }
    public const double CycleSeconds = 2;
    public const double ConfirmationSeconds = 0.35;
    private Texture2D _back = null!;
    private double _elapsed;
    private bool _running, _reducedMotion, _compact;
    public MatchmakingLinkState State { get; private set; }
    internal double PlaybackSeconds => _elapsed;
    public bool ReducedMotion
    {
        get => _reducedMotion;
        set { _reducedMotion = value; if (IsNodeReady()) { UpdateProcessing(); QueueRedraw(); } }
    }
    public bool Compact
    {
        get => _compact;
        set { _compact = value; CustomMinimumSize = new(value ? 108 : 150, value ? 48 : 64); QueueRedraw(); }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _back = SleeveArt.Back(Sleeve);
        Resized += QueueRedraw; VisibilityChanged += UpdateProcessing;
        Compact = _compact; UpdateProcessing();
    }

    public void Present(MatchmakingLinkState state, bool running)
    {
        if (State != state)
        {
            if (state is MatchmakingLinkState.Idle or MatchmakingLinkState.Starting or MatchmakingLinkState.Matched || State == MatchmakingLinkState.Idle)
                _elapsed = 0;
            State = state; QueueRedraw();
        }
        _running = running;
        UpdateProcessing();
    }

    private void UpdateProcessing() => SetProcess(IsVisibleInTree() && _running && !ReducedMotion &&
        (State == MatchmakingLinkState.Searching || State == MatchmakingLinkState.Matched && _elapsed < ConfirmationSeconds));

    public override void _Process(double delta) => AdvancePlayback(delta);

    internal void AdvancePlayback(double delta)
    {
        if (!_running || ReducedMotion || !IsVisibleInTree()) return;
        if (State == MatchmakingLinkState.Searching) _elapsed = (_elapsed + Math.Max(0, delta)) % CycleSeconds;
        else if (State == MatchmakingLinkState.Matched) _elapsed = Math.Min(ConfirmationSeconds, _elapsed + Math.Max(0, delta));
        else return;
        QueueRedraw(); UpdateProcessing();
    }

    public override void _Draw()
    {
        if (State == MatchmakingLinkState.Idle) return;
        var cardHeight = Compact ? 31f : 44f;
        var cardWidth = cardHeight * 0.71f;
        var center = new Vector2(Size.X / 2, (Size.Y - 14) / 2);
        var offset = Compact ? 38f : 53f;
        var own = new Rect2(center + new Vector2(-offset - cardWidth / 2, -cardHeight / 2), new(cardWidth, cardHeight));
        var rival = new Rect2(center + new Vector2(offset - cardWidth / 2, -cardHeight / 2), new(cardWidth, cardHeight));
        var matched = State == MatchmakingLinkState.Matched;
        var reveal = matched ? ReducedMotion ? 1f : (float)Math.Clamp(_elapsed / (ConfirmationSeconds * 0.7), 0, 1) : 0;
        BoardArt.Plate(this, own.Grow(2), RelickVisuals.Playmat.Raised, BoardArt.Tint(RelickVisuals.State.Own, 0.6f), 3);
        DrawTextureRect(_back, own, false);
        BoardArt.Plate(this, rival.Grow(2), RelickVisuals.Playmat.Raised,
            BoardArt.Tint(matched ? RelickVisuals.State.Rival : BoardArt.Muted, 0.5f), 3);
        if (reveal > 0) DrawTextureRect(_back, rival, false, BoardArt.Tint(Colors.White, reveal));
        if (reveal < 1) BoardArt.Text(this, "?", rival.Position + new Vector2(cardWidth / 2 - 4, cardHeight / 2 + 5), 15, BoardArt.Tint(BoardArt.Muted, 1 - reveal));
        BoardArt.Text(this, "TÚ", new(own.GetCenter().X - 7, own.End.Y + 14), 11, BoardArt.Muted);
        BoardArt.Text(this, "RIVAL", new(rival.GetCenter().X - 16, rival.End.Y + 14), 11, BoardArt.Muted);
        var phase = (float)(_elapsed / CycleSeconds);
        for (var i = 0; i < 3; i++)
        {
            var x = center.X + (i - 1) * (Compact ? 11 : 14);
            var brightness = State == MatchmakingLinkState.Searching && !ReducedMotion
                ? 0.25f + 0.65f * Mathf.Pow(Math.Max(0, Mathf.Cos((phase - i * 0.15f) * Mathf.Tau)), 2)
                : matched ? 0.85f : 0.3f;
            DrawPolyline([new(x - 3, center.Y - 5), new(x + 2, center.Y), new(x - 3, center.Y + 5)],
                BoardArt.Tint(RelickVisuals.State.Target, brightness), 2, true);
        }
    }
}

using Godot;
using Relick.Core;

namespace Relick;

// Small, purely cosmetic touches: none of them blocks a play, delays a decision or runs a rule.
public partial class OnlineArena
{
    /// <summary>Seconds the result stays up before a click or key press anywhere may dismiss it.</summary>
    internal const double ResultOverlayHold = 1.5;

    private string? _lastPlayAnchor;
    internal int RejectionShakes { get; private set; }
    private CenterContainer _resultOverlay = null!;
    private PanelContainer _resultCard = null!;
    private Label _resultOverlayTitle = null!, _resultOverlayReason = null!, _resultOverlayScore = null!;
    private string? _resultOverlayKey;
    private ulong _resultOverlayShownAt;
    internal bool ResultOverlayVisible => _resultOverlay.Visible;
    internal string ResultOverlayTitle => _resultOverlayTitle.Text;

    /// <summary>The card or zone whose action is about to be sent, so a refusal can point at it.</summary>
    private void RememberPlayAnchor() => _lastPlayAnchor = _cardActionAnchor?.Name;

    /// <summary>A short shake of the card or zone whose play was refused. Only its drawn place moves, and only briefly.</summary>
    private void ShakeRejected()
    {
        var name = _lastPlayAnchor; _lastPlayAnchor = null;
        if (_impacts.ReducedMotion || name == null || _root?.FindChild(name, true, false) is not Control target) return;
        var rest = target.Position;
        var tween = target.CreateTween();
        foreach (var dx in new[] { -7f, 6, -4, 2, 0 })
            tween.TweenProperty(target, "position", rest + new Vector2(dx, 0), 0.04);
        RejectionShakes++;
    }

    /// <summary>
    /// Shows the code a duelist just played, read from the public history. A follow-up decision inside the
    /// same resolution (choosing Disquete's card, say) is not a new play and gets no flash.
    /// </summary>
    internal void FlashPlayedCard(DuelSnapshot previous, DuelSnapshot current)
    {
        var seen = previous.History?.Select(e => e.Id).DefaultIfEmpty(0UL).Max() ?? 0;
        var entry = current.History?.LastOrDefault(e => e.Id > seen && e.CardId.Length > 0 &&
            e.Status is DuelResolutionStatus.Response or DuelResolutionStatus.Resolving);
        if (entry == null || previous.Resolution?.CardId == entry.CardId) return;
        if (!CardCatalog.All.TryGetValue(entry.CardId, out var card) || card.Kind != CardKind.Code) return;
        if (BoardArt.LoadCardArtwork(card) is not { } art) return;
        var lane = _combatCenter != null && IsInstanceValid(_combatCenter) ? _combatCenter.GetGlobalRect() : GetGlobalRect();
        var center = new Vector2(lane.GetCenter().X, Math.Max(lane.GetCenter().Y - 150, GlobalPosition.Y + 150));
        _impacts.PlayedCard(art, center, entry.SourceActor == current.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral);
    }

    private void BuildResultOverlay()
    {
        // Never takes the pointer: the rail keeps «Volver al lobby», «Revisar tablero» and the history.
        _resultOverlay = new CenterContainer { Name = "DuelResultOverlay", Visible = false, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 21 };
        AddChild(_resultOverlay); _resultOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _resultCard = new PanelContainer { Name = "DuelResultCard", MouseFilter = MouseFilterEnum.Ignore };
        _resultCard.Resized += () => _resultCard.PivotOffset = _resultCard.Size / 2;
        _resultOverlay.AddChild(_resultCard);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 8); _resultCard.AddChild(column);
        _resultOverlayTitle = ArenaPresentation.Text("", 52); _resultOverlayTitle.AddThemeFontOverride("font", RelickVisuals.Display);
        _resultOverlayTitle.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(_resultOverlayTitle);
        _resultOverlayReason = ArenaPresentation.Text("", 17); _resultOverlayReason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _resultOverlayReason.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(_resultOverlayReason);
        _resultOverlayScore = ArenaPresentation.Text("", 15, BoardArt.Muted); _resultOverlayScore.HorizontalAlignment = HorizontalAlignment.Center;
        _resultOverlayScore.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(_resultOverlayScore);
        foreach (var label in new[] { _resultOverlayTitle, _resultOverlayReason, _resultOverlayScore }) label.MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>The finished duel gets one central moment; the rail panel stays as its lasting version.</summary>
    private void UpdateResultOverlay(bool settled)
    {
        if (_snapshot is not { Winner: int winner } s || !settled) { HideResultOverlay(); return; }
        var key = $"{RoomCode}:{s.Revision}:{s.MySeat}";
        if (key == _resultOverlayKey) return;
        _resultOverlayKey = key;
        var accent = winner == -1 ? BoardArt.Gold : winner == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral;
        _resultOverlayTitle.Text = winner == -1 ? "EMPATE" : winner == s.MySeat ? "¡VICTORIA!" : "DERROTA";
        _resultOverlayTitle.AddThemeColorOverride("font_color", accent);
        _resultOverlayReason.Text = s.EndReason ?? "Resultado confirmado por el servidor.";
        DuelPlayerView Seat(int seat) => s.Players[seat];
        string Score(int seat, string who) => $"{who} · {Seat(seat).Health}/{Seat(seat).MaxHealth} vidas · {Seat(seat).Pr} PR";
        _resultOverlayScore.Text = $"{Score(s.MySeat, "TÚ")}     {Score(1 - s.MySeat, "RIVAL")}";
        _resultCard.AddThemeStyleboxOverride("panel", RelickVisuals.Glass(RelickVisuals.Playmat.Raised.Lerp(accent, 0.12f), RelickVisuals.Playmat.Surface,
            BoardArt.Tint(accent, 0.9f), 12, 14, BoardArt.Tint(accent, 0.35f)));
        var width = Math.Min(560, Size.X - 40);
        foreach (var label in new[] { _resultOverlayReason, _resultOverlayScore }) label.CustomMinimumSize = new(width - 48, 0);
        _resultOverlay.Show(); _resultOverlay.MoveToFront();
        PositionResultOverlay();
        _resultOverlayShownAt = Time.GetTicksMsec();
        if (_impacts.ReducedMotion) { _resultOverlay.Modulate = Colors.White; _resultCard.Scale = Vector2.One; return; }
        _resultOverlay.Modulate = new Color(1, 1, 1, 0); _resultCard.Scale = Vector2.One * 0.9f;
        var tween = _resultOverlay.CreateTween().SetParallel();
        tween.TweenProperty(_resultOverlay, "modulate:a", 1f, 0.25);
        tween.TweenProperty(_resultCard, "scale", Vector2.One, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// On the board the moment is centred over the playmat, clear of the reading rail. A rebuilt rail has
    /// no place until its first layout, so this runs again every frame while the result is up.
    /// </summary>
    internal void PositionResultOverlay()
    {
        if (_resultOverlay is not { Visible: true }) return;
        var rail = _readerRail != null && IsInstanceValid(_readerRail) && _readerRail.IsVisibleInTree() && _readerRail.Size.X > 0
            ? _readerRail.GetGlobalRect() : (Rect2?)null;
        var right = rail is { } r && r.Position.X > GlobalPosition.X ? r.Position.X - GlobalPosition.X - Size.X - 12 : 0;
        if (!Mathf.IsEqualApprox(_resultOverlay.OffsetRight, right)) _resultOverlay.OffsetRight = right;
    }

    private void HideResultOverlay()
    {
        if (_resultOverlay is not { Visible: true }) return;
        _resultOverlay.Hide(); _resultOverlay.Modulate = Colors.White;
    }

    public override void _Input(InputEvent input)
    {
        // Any click or key press after the hold dismisses the result; the event still reaches its target.
        if (_resultOverlay is not { Visible: true } || Time.GetTicksMsec() - _resultOverlayShownAt < ResultOverlayHold * 1000) return;
        if (input is InputEventMouseButton { Pressed: true } or InputEventKey { Pressed: true, Echo: false }) HideResultOverlay();
    }

    /// <summary>For tests: lets the hold elapse without waiting for it.</summary>
    internal void ExpireResultOverlayHold() => _resultOverlayShownAt = 0;
}

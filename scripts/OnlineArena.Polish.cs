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

    private (DuelSnapshot Previous, DuelSnapshot Current)? _queuedPlay;

    /// <summary>
    /// The flash of a played code and a created Trap's summon, one frame after the Render that showed them:
    /// read straight away, the rebuilt board has no layout yet and every place sits at the top-left corner.
    /// </summary>
    private void PresentQueuedPlay()
    {
        if (_queuedPlay is not { } play) return;
        _queuedPlay = null;
        if (_snapshot?.Revision != play.Current.Revision) return;
        FlashPlayedCard(play.Previous, play.Current); SummonCreatedTrap(play.Previous, play.Current);
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
        // A link of a visible response chain slots into the chain instead of flashing on its own.
        if (LiveChain(current) is { Count: >= 2 } chain && chain.Any(link => link.CardId == entry.CardId)) return;
        // A created Trap gets its own recipe animation instead (SummonCreatedTrap).
        if (!CardCatalog.All.TryGetValue(entry.CardId, out var card) || card.Kind != CardKind.Code || card.CodeType == CodeType.Trap) return;
        if (BoardArt.LoadCardArtwork(card) is not { } art) return;
        var lane = _combatCenter != null && IsInstanceValid(_combatCenter) ? _combatCenter.GetGlobalRect() : GetGlobalRect();
        var center = new Vector2(lane.GetCenter().X, Math.Max(lane.GetCenter().Y - 150, GlobalPosition.Y + 150));
        _impacts.PlayedCard(art, center, entry.SourceActor == current.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral);
    }

    /// <summary>
    /// After a «Crear» decision, the public history names the Trap. Its two exchanged cards are already in
    /// memory (rulebook § 4.6), so echoes of them rise from there, meet over the exchange and call the Trap,
    /// which then flies to its place: the hand for Súper Ataque, the equipment zone for Súper Imán. Zahira
    /// Flair's host is chosen next, so its card appears and fades over the exchange.
    /// </summary>
    internal void SummonCreatedTrap(DuelSnapshot previous, DuelSnapshot current)
    {
        if (previous.Phase != Phase.TrapCraftChoice) return;
        var seen = previous.History?.Select(e => e.Id).DefaultIfEmpty(0UL).Max() ?? 0;
        var entry = current.History?.LastOrDefault(e => e.Id > seen && e.CardId.Length > 0 &&
            CardCatalog.All.TryGetValue(e.CardId, out var trap) && trap.CodeType == CodeType.Trap);
        if (entry == null || _root == null || BoardArt.LoadCardArtwork(CardCatalog.Get(entry.CardId)) is not { } art) return;
        var relative = entry.Actor == current.MySeat ? 0 : 1;
        Rect2? Find(string name) => _root.FindChild(name, true, false) is Control { } node && node.IsVisibleInTree() ? node.GetGlobalRect() : null;
        var memory = Find("MemoryButton" + relative);
        var exchange = _exchangeTracks[relative] is { } track && IsInstanceValid(track)
            ? track.ExchangeButtons.Where(IsInstanceValid).Select(b => b.GetGlobalRect()).Aggregate((a, b) => a.Merge(b)) : (Rect2?)null;
        if (memory == null || exchange == null) return;
        var ingredients = (previous.TrapIngredients ?? []).Where(CardCatalog.All.ContainsKey)
            .Select(id => BoardArt.LoadCardArtwork(CardCatalog.Get(id), current.Players[entry.Actor].RelikarioId)).OfType<Texture2D>().ToArray();
        Rect2? destination = entry.CardId == CardCatalog.SuperMagnet.Id ? Find("EquipmentZone" + relative)
            : entry.CardId == CardCatalog.SuperAttack.Id ? relative == 0 ? HandLanding() : Find("OpponentHandFan") ?? Find("OpponentHandScroll") : null;
        _impacts.TrapSummon(ingredients, memory.Value.GetCenter(), exchange.Value.GetCenter(), art, destination,
            relative == 0 ? ArenaPresentation.Mint : ArenaPresentation.Coral);
        if (entry.CardId == CardCatalog.SuperAttack.Id && destination != null && _impacts.ShowingSummon)
            HideUntilSummonLands(current, entry.Actor, relative);
    }

    /// <summary>
    /// The board was rebuilt with Súper Ataque already in the hand; that card stays hidden while its echo is
    /// summoned and fades in as the flying Trap lands on it, so the card is never seen before it arrives.
    /// </summary>
    private void HideUntilSummonLands(DuelSnapshot current, int actor, int relative)
    {
        var hand = current.Players[actor];
        var slot = relative == 0 ? Array.LastIndexOf(hand.Hand, CardCatalog.SuperAttack.Id) : hand.HandCount - 1;
        if (slot < 0 || ZoneNode(relative, CardZone.Hand, slot) is not { } card) return;
        HiddenSummonedCard = card;
        card.Modulate = new Color(1, 1, 1, 0);
        var tween = card.CreateTween();
        tween.TweenInterval(ImpactLayer.SummonLanding);
        tween.TweenProperty(card, "modulate:a", 1f, ImpactLayer.SummonLandingFade);
    }

    /// <summary>For tests: the hand card kept hidden while the Trap summon flies to it.</summary>
    internal Control? HiddenSummonedCard { get; private set; }

    /// <summary>Where a card joining the own hand lands: the visible band of the fan, at board card size.</summary>
    private Rect2? HandLanding()
    {
        if (_hand == null || !IsInstanceValid(_hand) || !_hand.IsVisibleInTree()) return null;
        var hand = _hand.GetGlobalRect();
        var top = Math.Min(hand.Position.Y + 20, GlobalPosition.Y + Size.Y - 160);
        return new Rect2(hand.GetCenter().X - 60, top, 120, 160);
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

using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private readonly ResponseChain _responseChain = new() { Name = "ResponseChain" };

    /// <summary>
    /// The open chain as the server publishes it, in activation order: the pending attack it answers (if any), the
    /// activation that opened it and every response. Only an open Error de Carga window has a chain.
    /// </summary>
    internal static List<PublicChainLink> LiveChain(DuelSnapshot s)
    {
        if (s.Phase != Phase.ErrorResponse || s.Resolution is not { } live || live.CardId.Length == 0) return [];
        var chain = new List<PublicChainLink>();
        if (live.Attack is { } attack) chain.Add(new() { Actor = attack.Actor, CardId = attack.CardId, Attack = true });
        chain.Add(new() { Actor = live.Actor, CardId = live.CardId, PlaceLetter = live.PlaceLetter });
        chain.AddRange(live.Responses.Select(r => new PublicChainLink { Actor = r.Actor, CardId = r.CardId, PlaceLetter = r.PlaceLetter }));
        return chain;
    }

    /// <summary>
    /// Follows a new revision: a resolved chain plays back (then any chain the same move opened), otherwise the live chain
    /// grows. Without animation (recovery, a gap, a resize) the live chain simply appears in place.
    /// </summary>
    internal void SyncResponseChain(DuelSnapshot s, bool animate)
    {
        var resolved = animate ? s.Visuals.LastOrDefault(v => v.Kind == DuelVisualKind.Chain && v.Chain != null) : null;
        if (resolved != null) _responseChain.Resolve(resolved.Chain!, s.MySeat, LiveChain(s));
        else
        {
            // Recovery or a gap drops any playback: the board already shows its outcome.
            if (!animate) _responseChain.Clear();
            _responseChain.Present(LiveChain(s), s.MySeat, animate);
        }
        PositionResponseChain();
    }

    /// <summary>
    /// Places the chain over the middle of the board, as large as fits, clear of the response buttons, the action dock,
    /// the hand and a die on screen. If nothing else fits it only keeps clear of the buttons and shrinks.
    /// </summary>
    internal void PositionResponseChain()
    {
        var chain = _responseChain;
        if (!chain.HasContent || _root == null) { if (chain.Visible && !chain.HasContent) chain.Visible = false; return; }
        var count = chain.Links.Count;
        var bounds = new Rect2(Vector2.Zero, Size).Grow(-8);
        if (bounds.Size.X <= 0 || bounds.Size.Y <= 0) return;
        var origin = GlobalPosition;
        Rect2 Local(Control c) { var r = c.GetGlobalRect(); return new(r.Position - origin, r.Size); }
        var controls = new List<Control>(_moveButtons.Where(b => IsInstanceValid(b) && b.IsVisibleInTree()));
        if (_decisionPopup is { Visible: true }) controls.Add(_decisionPopup);
        if (_cardActions.IsVisibleInTree()) controls.Add(_cardActions);
        if (_advanceButton != null && IsInstanceValid(_advanceButton) && _advanceButton.IsVisibleInTree()) controls.Add(_advanceButton);
        // The top bar of the stacked layouts holds the COMBO toggle and the menus: it counts as buttons.
        if (_root.GetNodeOrNull<Control>("ArenaHeader") is { } header && header.IsVisibleInTree()) controls.Add(header);
        var buttons = controls.Select(Local).Where(r => r.Size.X > 0 && r.Size.Y > 0).ToList();
        var soft = new List<Rect2>();
        var desktop = Desktop;
        var lane = _combatCenter != null && IsInstanceValid(_combatCenter) && _combatCenter.IsVisibleInTree() ? Local(_combatCenter) : (Rect2?)null;
        // The desktop rail reads the card and its answers; the stacked layouts can cover the reading for a moment.
        if (desktop && _readerRail != null && IsInstanceValid(_readerRail) && _readerRail.IsVisibleInTree()) soft.Add(Local(_readerRail));
        if (!desktop && lane is { } bar) soft.Add(bar);
        // The plate carries the time and whose answer it is, not just its button.
        if (desktop && lane is { } plate) buttons.Add(plate);
        if (_hand != null && IsInstanceValid(_hand) && _hand.IsVisibleInTree()) soft.Add(Local(_hand).Intersection(bounds));
        if (_diceOverlay.Visible && _diceOverlay.GetChildCount() > 0 && _diceOverlay.GetChild(0) is Control dice) soft.Add(Local(dice));
        var board = desktop && soft.Count > 0 ? new Rect2(bounds.Position, new Vector2(Math.Max(0, soft[0].Position.X - bounds.Position.X - 8), bounds.Size.Y)) : bounds;
        if (board.Size.X < bounds.Size.X * 0.5f) board = bounds;
        if (_floatingReader is { Visible: true } floating)
        {
            // Beside the card being answered, never behind it or across it.
            var reader = Local(floating);
            var from = reader.GetCenter().X < board.GetCenter().X ? reader.End.X + 8 : board.Position.X;
            var to = reader.GetCenter().X < board.GetCenter().X ? board.End.X : reader.Position.X - 8;
            if (to - from >= board.Size.X * 0.5f) board = new(new Vector2(from, board.Position.Y), new Vector2(to - from, board.Size.Y));
        }
        var anchor = desktop && lane is { } center ? center.GetCenter() : board.GetCenter();
        foreach (var obstacles in new[] { buttons.Concat(soft).ToList(), buttons })
        {
            var limit = new Vector2(board.Size.X, Math.Min(bounds.Size.Y * 0.36f, ResponseChain.MaxModuleHeight + 21));
            for (var attempt = 0; attempt < 6; attempt++, limit *= new Vector2(1, 0.84f))
            {
                var size = ResponseChain.Measure(count, limit);
                if (size.Y - 7 < ResponseChain.MinModuleHeight && attempt < 5) continue;
                if (Place(size, obstacles) is { } spot) { Apply(spot); return; }
            }
        }
        // Nowhere clear: the smallest chain at the top edge, still never over a button's row if avoidable.
        var fallback = ResponseChain.Measure(count, new Vector2(bounds.Size.X, ResponseChain.MinModuleHeight + 7));
        Apply(new Rect2(new Vector2(bounds.GetCenter().X - fallback.X / 2, bounds.Position.Y), fallback));

        Rect2? Place(Vector2 size, List<Rect2> obstacles)
        {
            var x = Math.Clamp(anchor.X - size.X / 2, board.Position.X, Math.Max(board.Position.X, board.End.X - size.X));
            var candidates = new List<float> { anchor.Y - size.Y / 2 };
            foreach (var o in obstacles) { candidates.Add(o.Position.Y - size.Y - 6); candidates.Add(o.End.Y + 6); }
            candidates.Add(bounds.Position.Y); candidates.Add(bounds.End.Y - size.Y);
            Rect2? best = null; var distance = float.MaxValue;
            foreach (var y in candidates)
            {
                var spot = new Rect2(x, Math.Clamp(y, bounds.Position.Y, bounds.End.Y - size.Y), size);
                if (obstacles.Any(o => o.Intersects(spot))) continue;
                // Off the centre line a full-width chain may still slide sideways to clear a narrow obstacle.
                var d = Math.Abs(spot.GetCenter().Y - anchor.Y);
                if (d < distance) { best = spot; distance = d; }
            }
            return best;
        }

        void Apply(Rect2 spot)
        {
            chain.Position = spot.Position.Round(); chain.Size = spot.Size;
            chain.Visible = true;
        }
    }
}

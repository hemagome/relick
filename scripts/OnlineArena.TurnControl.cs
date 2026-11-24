using Godot;

namespace Relick;

// The contextual turn button (double-click guard and Space) and the informative clock that goes with it.
public partial class OnlineArena
{
    /// <summary>How long a turn button whose meaning just changed waits before taking a press.</summary>
    internal const ulong TurnGuardMilliseconds = 400;
    private const long ClockWarning = 15_000_000;

    private string? _turnButtonMeaning;
    private ulong _turnGuardUntil;
    private bool _lastTurnHeld;
    private TurnCore? _turnCore;
    private readonly List<Label> _clockLabels = [];

#if RELICK_DEV_TOOLS
    internal Func<long?>? TestServerNow { get; set; }
#endif

    internal bool TurnButtonHeld => _turnButtonMeaning != null && Time.GetTicksMsec() < _turnGuardUntil;

    private bool IsTurnButton(Button button) =>
        button == _advanceButton || _passResponseSlot != null && button.GetParent() == _passResponseSlot;

    // A new board puts the next button exactly where the last one was: the second click of a double click
    // must not end the turn or pass the next response.
    private void ArmTurnGuard()
    {
        var s = _snapshot!;
        string? meaning = null;
        if (_passResponseSlot != null) meaning = $"pass:{s.Revision}";
        else if (_advanceButton != null)
            meaning = $"advance:{s.Turn}:{s.Phase}:{s.ExtraDrawStages}:{s.ExtraCombatStages}:{s.ExtraActionStages}:{s.ExtraFinalStages}";
        if (meaning == _turnButtonMeaning) return;
        _turnButtonMeaning = meaning;
        if (meaning != null) _turnGuardUntil = Time.GetTicksMsec() + TurnGuardMilliseconds;
    }

    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouseMotion motion) { _pointer = motion.Position; return; }
        if (input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } && _historyOpen && IsVisibleInTree() &&
            _showcase != null && !GetViewport().GetEmbeddedSubwindows().Any(TakesKeys))
        {
            GetViewport().SetInputAsHandled(); SetHistoryOpen(false); return;
        }
        if (input is not InputEventKey { Keycode: Key.Space, CtrlPressed: false, AltPressed: false, MetaPressed: false, ShiftPressed: false } key) return;
        if (TurnShortcutTarget() is not { } button) return;
        // Consumed even while held, so Space never falls through to the card that was clicked last.
        GetViewport().SetInputAsHandled();
        if (key.Pressed && !key.Echo && !button.Disabled && !TurnButtonHeld) button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>The button Space would press, or null when the key belongs to something else.</summary>
    private Button? TurnShortcutTarget()
    {
        if (!IsVisibleInTree() || _root == null || _snapshot is not { Winner: null } || _preparing) return null;
        Control? choice = _root.FindChild("ChoiceShade", true, false) as Control ?? _deckChoiceShade;
        if (choice != null || GetNodeOrNull("ProfilePopup") != null) return null;
        var button = _advanceButton ?? _passResponseSlot?.GetChildren().OfType<Button>().FirstOrDefault();
        if (button == null || !IsInstanceValid(button) || !button.IsVisibleInTree()) return null;
        if (_decisionPopup is { Visible: true } popup && popup.GetGlobalRect().Intersects(button.GetGlobalRect())) return null;
        var viewport = GetViewport();
        if (viewport.GetEmbeddedSubwindows().Any(TakesKeys)) return null;
        // Focus placed with the keyboard keeps Space as the usual press; focus left behind by a click does not.
        if (viewport.GuiGetFocusOwner() is { } focus && focus != button && (focus is LineEdit or TextEdit || focus.HasFocus(true))) return null;
        return button;
    }

    // A tooltip is a window too, and hovering the turn button shows one that names this very shortcut.
    private static bool TakesKeys(Window window) => window.Visible && !window.Unfocusable;

    private Label AddClockLabel(Node parent, int size)
    {
        var label = ArenaPresentation.Text("", size);
        label.Name = "TurnClock"; label.Visible = false;
        label.MouseFilter = MouseFilterEnum.Ignore; label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        label.AddThemeFontOverride("font", RelickVisuals.Strong);
        parent.AddChild(label); _clockLabels.Add(label);
        return label;
    }

    private void ClearTurnClock() { _turnCore = null; _clockLabels.Clear(); }

    /// <summary>Remaining time as m:ss; past the reference, the extra time with a plus sign.</summary>
    internal static string ClockText(long remaining)
    {
        var seconds = remaining >= 0 ? (remaining + 999_999) / 1_000_000 : -remaining / 1_000_000;
        return $"{(remaining < 0 ? "+" : "")}{seconds / 60}:{seconds % 60:00}";
    }

    private long? ClockNow()
    {
#if RELICK_DEV_TOOLS
        if (TestServerNow != null) return TestServerNow();
#endif
        return Connection.Ready ? Connection.ServerNow : null;
    }

    /// <summary>Called every frame; it only touches nodes when what they show changes. It never sends a move.</summary>
    private void RefreshTurnClock()
    {
        var text = ""; var tip = ""; var color = BoardArt.Muted;
        float? fraction = null;
        if (!_preparing && _snapshot is { Winner: null, Clock: { Running: int seat } clock } s)
        {
            text = "–:––";
            if (ClockNow() is long now)
            {
                var remaining = clock.Remaining(seat, now);
                var mine = seat == s.MySeat;
                text = ClockText(remaining);
                color = remaining <= ClockWarning ? BoardArt.Gold : mine ? ArenaPresentation.Mint : ArenaPresentation.Coral;
                fraction = clock.Reference > 0 ? (float)remaining / clock.Reference : null;
                // Whole seconds of the total, without a sign: it is time spent, not time left.
                string Total(int player) => ClockText(-clock.DuelTime(player, now)).TrimStart('+');
                var limit = RatedLimit;
                if (limit > 0)
                {
                    // In a rated duel the decision itself runs out: its last minute takes the place of the reference.
                    var left = clock.UpdatedAt + limit - now;
                    if (left <= LimitShownFrom)
                    {
                        text = $"¡{ClockText(Math.Max(0, left))}!";
                        color = RelickVisuals.State.Danger;
                        fraction = Math.Max(0, (float)left / LimitShownFrom);
                    }
                }
                var rule = limit > 0 ? $"Este duelo cuenta: pasar {limit / 60_000_000} min sin decidir lo pierde."
                    : "Es informativo: al agotarse no ocurre nada.";
                tip = $"Tiempo de decisión {(mine ? "tuyo" : "del rival")} en este turno. {rule}\n" +
                    $"En el duelo: tú {Total(s.MySeat)} · rival {Total(1 - s.MySeat)}";
            }
        }
        foreach (var label in _clockLabels)
        {
            if (!IsInstanceValid(label)) continue;
            if (label.Visible != text.Length > 0) label.Visible = text.Length > 0;
            if (label.Text != text) label.Text = text;
            if (!label.HasThemeColorOverride("font_color") || label.GetThemeColor("font_color") != color)
                label.AddThemeColorOverride("font_color", color);
        }
        if (_turnCore == null || !IsInstanceValid(_turnCore)) return;
        _turnCore.SetGauge(fraction, color);
        if (_turnCore.TooltipText != tip) _turnCore.TooltipText = tip;
    }
}

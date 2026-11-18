using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineArena
{
    private sealed record OpeningInitiative(int RollOne, int RollTwo, int Starter, string[] Names, int MySeat, string?[] Dice);
    private readonly DiceFace[] _initiativeDice =
    [new() { Name = "InitiativeDice0" }, new() { Name = "InitiativeDice1" }];
    private readonly Label[] _initiativeNames = [new(), new()];
    private readonly Label[] _initiativeValues = [new(), new()];
    private CenterContainer _initiativeOverlay = null!;
    private PanelContainer _initiativePanel = null!;
    private Label _initiativeHint = null!;
    private Label _initiativeWinner = null!;
    private OpeningInitiative? _openingInitiative;
    private Flight? _openingTurnDraw;
    private bool _initiativeStarted, _initiativeLanded;
    private double _initiativeResultRemaining;
    private Tween? _initiativeTween;
    /// <summary>How long the winner stays on screen after both dice land.</summary>
    internal const double InitiativeResultSeconds = 2.6;
    internal int PlayedInitiativeRolls { get; private set; }

    private void BuildInitiativeOverlay()
    {
        _initiativeOverlay = new CenterContainer
        {
            Name = "InitiativeOverlay", Visible = false, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 30
        };
        AddChild(_initiativeOverlay); _initiativeOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _initiativePanel = ArenaPresentation.Panel(_initiativeOverlay, "111f2c", BoardArt.Gold);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); _initiativePanel.AddChild(column);
        var title = ArenaPresentation.Text("¿QUIÉN EMPIEZA?", 20, BoardArt.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(title);
        _initiativeHint = ArenaPresentation.Text("", 13, BoardArt.Muted);
        _initiativeHint.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(_initiativeHint);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); column.AddChild(row);
        for (var seat = 0; seat < 2; seat++)
        {
            var player = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(player);
            var name = _initiativeNames[seat]; name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.AddThemeFontSizeOverride("font_size", 16); player.AddChild(name);
            player.AddChild(_initiativeDice[seat]);
            var value = _initiativeValues[seat]; value.HorizontalAlignment = HorizontalAlignment.Center;
            value.AddThemeFontSizeOverride("font_size", 18); player.AddChild(value);
        }
        // Sized up front so the label keeps one height and its pivot stays centred when the result pops in.
        _initiativeWinner = ArenaPresentation.Text("", 40, BoardArt.Gold);
        _initiativeWinner.HorizontalAlignment = HorizontalAlignment.Center;
        _initiativeWinner.AddThemeConstantOverride("outline_size", 10);
        _initiativeWinner.AddThemeColorOverride("font_outline_color", new Color("0a141d"));
        _initiativeWinner.AddThemeConstantOverride("shadow_outline_size", 18);
        _initiativeWinner.AddThemeConstantOverride("shadow_offset_x", 0);
        _initiativeWinner.AddThemeConstantOverride("shadow_offset_y", 0);
        _initiativeWinner.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(_initiativeWinner);
    }

    private void QueueOpeningInitiative(DuelSnapshot combat, PreparationInfo? opening, Flight? draw)
    {
        if (opening is not { Started: true, RollOne: >= 1 and <= 6, RollTwo: >= 1 and <= 6 })
        { if (draw != null) _flights.Enqueue(draw); return; }
        _openingInitiative = new(opening.RollOne, opening.RollTwo, opening.StartingSeat,
            combat.Players.Select(p => p.Name).ToArray(), combat.MySeat, combat.Players.Select(p => p.Dice).ToArray());
        _openingTurnDraw = draw;
        _initiativeStarted = false; _initiativeLanded = false;
        _initiativeResultRemaining = InitiativeResultSeconds;
    }

    private void UpdateOpeningInitiative(double delta)
    {
        var opening = _openingInitiative!;
        if (!_initiativeStarted)
        {
            _initiativeStarted = true; PlayedInitiativeRolls++;
            _initiativeHint.Text = "La tirada más alta comienza el duelo";
            _initiativePanel.CustomMinimumSize = new(Short ? 440 : 540, 0);
            for (var seat = 0; seat < 2; seat++)
            {
                var own = seat == opening.MySeat;
                _initiativeNames[seat].Text = $"{opening.Names[seat]} · {(own ? "TÚ" : "RIVAL")}";
                _initiativeNames[seat].TooltipText = opening.Names[seat];
                _initiativeNames[seat].AddThemeColorOverride("font_color", own ? ArenaPresentation.Mint : ArenaPresentation.Coral);
                _initiativeDice[seat].CustomMinimumSize = Short ? new(192, 148) : new(242, 200);
                // The dice are seen rolling: the value stays blank (one line tall) until they land.
                _initiativeValues[seat].Text = "\u00A0";
                // Each player rolls their own die cosmetic, as in every later roll.
                _initiativeDice[seat].ApplyStyle(opening.Dice.ElementAtOrDefault(seat));
                _initiativeDice[seat].RollTo(seat == 0 ? opening.RollOne : opening.RollTwo);
            }
            _initiativeWinner.Text = "\u00A0";
            ResetInitiativeEmphasis();
            _initiativeOverlay.Show();
            return;
        }
        if (_initiativeDice.Any(die => die.IsRolling)) return;
        if (!_initiativeLanded)
        {
            _initiativeLanded = true;
            _initiativeValues[0].Text = opening.RollOne.ToString();
            _initiativeValues[1].Text = opening.RollTwo.ToString();
            ShowInitiativeWinner(opening);
            if (_openingTurnDraw != null) _initiativeHint.Text = "Quien empieza roba una carta al iniciar su turno";
            _combatMessage.Text = $"Iniciativa · {opening.Names[0]} {opening.RollOne} · {opening.Names[1]} {opening.RollTwo}\nEmpieza {opening.Names[opening.Starter]}";
        }
        _initiativeResultRemaining -= Math.Max(0, delta);
        if (_initiativeResultRemaining > 0) return;
        _initiativeOverlay.Hide();
        ResetInitiativeEmphasis();
        _openingInitiative = null;
        if (_openingTurnDraw is { } draw) _flights.Enqueue(draw);
        _openingTurnDraw = null;
        _lastDisabled = null;
    }

    private void ResetOpeningInitiative()
    {
        _openingInitiative = null; _openingTurnDraw = null;
        _initiativeStarted = false; _initiativeLanded = false;
        _initiativeOverlay?.Hide();
        ResetInitiativeEmphasis();
        foreach (var die in _initiativeDice) die.Value = 0;
    }

    /// <summary>
    /// Green when you start, red when your rival does: the winning die glows and grows, the other dims, and the
    /// announcement pops in large with a glow of the same colour. Reduced motion shows the same end state at once.
    /// </summary>
    private void ShowInitiativeWinner(OpeningInitiative opening)
    {
        var mine = opening.Starter == opening.MySeat;
        var color = mine ? ArenaPresentation.Mint : ArenaPresentation.Coral;
        _initiativeWinner.Text = mine ? "¡EMPIEZAS TÚ!" : $"¡EMPIEZA {opening.Names[opening.Starter].ToUpperInvariant()}!";
        _initiativeWinner.AddThemeColorOverride("font_color", color);
        _initiativeWinner.AddThemeColorOverride("font_shadow_color", new Color(color, 0.55f));
        _initiativeWinner.AddThemeFontSizeOverride("font_size", Short ? 32 : 40);
        _initiativeValues[opening.Starter].AddThemeColorOverride("font_color", color);
        _initiativeValues[opening.Starter].AddThemeFontSizeOverride("font_size", 26);
        var winner = _initiativeDice[opening.Starter]; var loser = _initiativeDice[1 - opening.Starter];
        winner.Highlight = color;
        winner.PivotOffset = winner.Size / 2; _initiativeWinner.PivotOffset = _initiativeWinner.Size / 2;
        if (_impacts.ReducedMotion)
        {
            winner.Scale = new(1.08f, 1.08f); loser.Modulate = new(1, 1, 1, 0.45f);
            return;
        }
        _initiativeWinner.Scale = new(0.35f, 0.35f); _initiativeWinner.Modulate = new(1, 1, 1, 0);
        _initiativeTween = CreateTween().SetParallel();
        _initiativeTween.TweenProperty(_initiativeWinner, "scale", Vector2.One, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _initiativeTween.TweenProperty(_initiativeWinner, "modulate:a", 1f, 0.2);
        _initiativeTween.TweenProperty(winner, "scale", new Vector2(1.12f, 1.12f), 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _initiativeTween.TweenProperty(loser, "modulate", new Color(1, 1, 1, 0.45f), 0.35);
        // After the pop, the announcement breathes gently until the overlay closes.
        var breathe = _initiativeTween.Chain().SetParallel(false);
        breathe.TweenProperty(_initiativeWinner, "scale", new Vector2(1.06f, 1.06f), 0.5).SetTrans(Tween.TransitionType.Sine);
        breathe.TweenProperty(_initiativeWinner, "scale", Vector2.One, 0.5).SetTrans(Tween.TransitionType.Sine);
        breathe.TweenProperty(_initiativeWinner, "scale", new Vector2(1.06f, 1.06f), 0.5).SetTrans(Tween.TransitionType.Sine);
        breathe.TweenProperty(_initiativeWinner, "scale", Vector2.One, 0.5).SetTrans(Tween.TransitionType.Sine);
    }

    private void ResetInitiativeEmphasis()
    {
        _initiativeTween?.Kill(); _initiativeTween = null;
        if (_initiativeWinner == null) return;
        _initiativeWinner.Scale = Vector2.One; _initiativeWinner.Modulate = Colors.White;
        for (var seat = 0; seat < 2; seat++)
        {
            _initiativeDice[seat].Highlight = null; _initiativeDice[seat].Scale = Vector2.One; _initiativeDice[seat].Modulate = Colors.White;
            _initiativeValues[seat].RemoveThemeColorOverride("font_color");
            _initiativeValues[seat].AddThemeFontSizeOverride("font_size", 18);
        }
    }
}

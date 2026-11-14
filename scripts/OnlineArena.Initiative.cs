using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineArena
{
    private sealed record OpeningInitiative(int RollOne, int RollTwo, int Starter, string[] Names, int MySeat);
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
        _initiativeWinner = ArenaPresentation.Text("", 18, BoardArt.Gold);
        _initiativeWinner.HorizontalAlignment = HorizontalAlignment.Center;
        _initiativeWinner.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(_initiativeWinner);
    }

    private void QueueOpeningInitiative(DuelSnapshot combat, PreparationInfo? opening, Flight? draw)
    {
        if (opening is not { Started: true, RollOne: >= 1 and <= 6, RollTwo: >= 1 and <= 6 })
        { if (draw != null) _flights.Enqueue(draw); return; }
        _openingInitiative = new(opening.RollOne, opening.RollTwo, opening.StartingSeat,
            combat.Players.Select(p => p.Name).ToArray(), combat.MySeat);
        _openingTurnDraw = draw;
        _initiativeStarted = false; _initiativeLanded = false;
        _initiativeResultRemaining = 1.5;
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
                _initiativeDice[seat].RollTo(seat == 0 ? opening.RollOne : opening.RollTwo);
            }
            _initiativeWinner.Text = "\u00A0";
            _initiativeOverlay.Show();
            return;
        }
        if (_initiativeDice.Any(die => die.IsRolling)) return;
        if (!_initiativeLanded)
        {
            _initiativeLanded = true;
            _initiativeValues[0].Text = opening.RollOne.ToString();
            _initiativeValues[1].Text = opening.RollTwo.ToString();
            _initiativeWinner.Text = $"Empieza {opening.Names[opening.Starter]}";
            if (_openingTurnDraw != null) _initiativeHint.Text = "Quien empieza roba una carta al iniciar su turno";
            _combatMessage.Text = $"Iniciativa · {opening.Names[0]} {opening.RollOne} · {opening.Names[1]} {opening.RollTwo}\nEmpieza {opening.Names[opening.Starter]}";
        }
        _initiativeResultRemaining -= Math.Max(0, delta);
        if (_initiativeResultRemaining > 0) return;
        _initiativeOverlay.Hide();
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
        foreach (var die in _initiativeDice) die.Value = 0;
    }
}

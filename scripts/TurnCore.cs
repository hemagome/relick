using Godot;
using Relick.Core;

namespace Relick;

/// <summary>
/// The plate in the pocket between both skill blocks: who decides, the stage, the time and the one contextual
/// button. Its size never changes; the time also drains around its outline.
/// </summary>
public partial class TurnCore : PanelContainer
{
    internal const float PlateWidth = 236, PlateHeight = 112;
    private const float Cut = 10, GaugeWidth = 3, ButtonHeight = 48;
    private static readonly Phase[] Stages = [Phase.Draw, Phase.Combat, Phase.Actions, Phase.Final];

    private float? _remaining;
    private Color _gaugeColor;
    private string _idle = "";

    internal HBoxContainer Dock { get; private set; } = null!;
    internal Label Time { get; private set; } = null!;
    internal float? Remaining => _remaining;

    /// <param name="turn">Null outside a live turn (preparation and result).</param>
    /// <param name="idle">What the button's room says when there is no button.</param>
    internal static TurnCore Create(Phase stage, int? turn, string idle, Color accent)
    {
        var core = new TurnCore { Name = "StageIndicator", MouseFilter = MouseFilterEnum.Pass, _idle = idle };
        var frame = RelickVisuals.Glass(RelickVisuals.Playmat.Raised.Lerp(accent, 0.14f), RelickVisuals.Playmat.Surface,
            BoardArt.Tint(accent, 0.28f), (int)Cut, 8, BoardArt.Tint(accent, 0.30f));
        frame.ContentMarginLeft = frame.ContentMarginRight = 9; frame.ContentMarginTop = frame.ContentMarginBottom = 4;
        core.AddThemeStyleboxOverride("panel", frame);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 2); core.AddChild(column);

        static Label Line(Label label, string name)
        {
            label.Name = name; label.MouseFilter = MouseFilterEnum.Ignore;
            label.SizeFlagsVertical = SizeFlags.ShrinkCenter; label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        // Words say whose decision it is; colour only repeats them.
        var status = new HBoxContainer { Name = "StageStatusCell", MouseFilter = MouseFilterEnum.Pass };
        status.AddThemeConstantOverride("separation", 5); column.AddChild(status);
        status.AddChild(new IconView { Name = "StageStatusIcon", Icon = GameIcon.Bolt, Color = accent,
            CustomMinimumSize = new(16, 16), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var headline = Line(ArenaPresentation.Heading("", 13, accent), "StageStatus");
        headline.ClipText = true; headline.SizeFlagsHorizontal = SizeFlags.ExpandFill; status.AddChild(headline);
        core.Time = Line(ArenaPresentation.Text("", 14), "TurnClock");
        core.Time.AddThemeFontOverride("font", RelickVisuals.Strong);
        core.Time.Visible = false; status.AddChild(core.Time);

        var now = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(now);
        var title = Line(ArenaPresentation.Heading(StageAppearance.For(stage).Title, 17, accent), "CurrentStageTitle");
        title.ClipText = true; title.SizeFlagsHorizontal = SizeFlags.ExpandFill; now.AddChild(title);
        if (turn is int number) now.AddChild(Line(ArenaPresentation.Text($"TURNO {number:00}", 11, BoardArt.Muted), "StageTurn"));

        var track = new HBoxContainer { Name = "StageTrack", CustomMinimumSize = new(0, 5), MouseFilter = MouseFilterEnum.Ignore };
        track.AddThemeConstantOverride("separation", 3); column.AddChild(track);
        var current = Array.IndexOf(Stages, stage);
        for (var index = 0; index < Stages.Length; index++)
            track.AddChild(new ColorRect
            {
                MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
                CustomMinimumSize = new(0, index == current ? 5 : 3),
                Color = index == current ? accent : index < current ? BoardArt.Tint(accent, 0.45f) : BoardArt.Tint(BoardArt.Muted, 0.22f)
            });

        core.Dock = new HBoxContainer { Name = "TurnDock", CustomMinimumSize = new(0, ButtonHeight), MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(core.Dock);
        return core;
    }

    /// <summary>Called once the button is built: without one, its room keeps its size and names the pending step.</summary>
    internal void FillDock()
    {
        if (Dock.GetChildCount() > 0)
        {
            foreach (var control in Dock.GetChildren().OfType<Control>()) control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            return;
        }
        var label = ArenaPresentation.Text(_idle, 12, BoardArt.Muted);
        label.Name = "StageDetail"; label.MouseFilter = MouseFilterEnum.Ignore;
        label.SizeFlagsHorizontal = label.SizeFlagsVertical = SizeFlags.ExpandFill;
        label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Dock.AddChild(label);
    }

    /// <summary>Fraction of the reference still left; null hides the gauge and zero leaves a full warning outline.</summary>
    internal void SetGauge(float? remaining, Color color)
    {
        // Half a pixel of outline is the finest step worth a redraw.
        if (remaining is float value) remaining = MathF.Round(Math.Clamp(value, 0, 1) * 1400) / 1400;
        if (_remaining == remaining && _gaugeColor == color) return;
        _remaining = remaining; _gaugeColor = color; QueueRedraw();
    }

    public override void _Draw()
    {
        if (_remaining is not float remaining) return;
        float left = 1, top = 1, right = Size.X - 1, bottom = Size.Y - 1;
        // Clockwise from the top centre, following the cut corners.
        Vector2[] path =
        [
            new(Size.X / 2, top), new(right - Cut, top), new(right, top + Cut), new(right, bottom - Cut), new(right - Cut, bottom),
            new(left + Cut, bottom), new(left, bottom - Cut), new(left, top + Cut), new(left + Cut, top), new(Size.X / 2, top)
        ];
        if (remaining <= 0) { DrawPolyline(path, _gaugeColor, GaugeWidth, true); return; }
        var length = 0f;
        for (var index = 1; index < path.Length; index++) length += path[index - 1].DistanceTo(path[index]);
        var spent = (1 - remaining) * length;
        var line = new List<Vector2>();
        for (var index = 1; index < path.Length; index++)
        {
            var step = path[index - 1].DistanceTo(path[index]);
            if (line.Count > 0) line.Add(path[index]);
            else if (spent < step) line.AddRange([path[index - 1].Lerp(path[index], spent / step), path[index]]);
            spent -= step;
        }
        if (line.Count > 1) DrawPolyline(line.ToArray(), _gaugeColor, GaugeWidth, true);
    }
}

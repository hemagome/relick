using Godot;
using Relick.Core;

namespace Relick;

internal readonly record struct StageAppearance(string Title, GameIcon Icon, Color Accent)
{
    internal static StageAppearance For(Phase stage) => stage switch
    {
        Phase.Preparation => new("PREPARACIÓN", GameIcon.Cards, RelickVisuals.State.Target),
        Phase.Draw => new("ROBO", GameIcon.Cards, ArenaPresentation.Mint),
        Phase.Combat => new("COMBATE", GameIcon.Sword, BoardArt.Gold),
        Phase.Actions => new("ACCIONES", GameIcon.Bolt, RelickVisuals.State.Target),
        Phase.Final => new("FINAL", GameIcon.Flag, new Color("c0acf7")),
        Phase.Finished => new("DUELO TERMINADO", GameIcon.Flag, BoardArt.Gold),
        _ => new("RESOLVIENDO", GameIcon.Reload, BoardArt.Muted)
    };

    internal static Phase Current(DuelSnapshot snapshot) => snapshot.Winner != null ? Phase.Finished
        : snapshot.Phase is Phase.Preparation or Phase.Finished ? snapshot.Phase : snapshot.Stage ?? snapshot.Phase;
}

internal readonly record struct StageAnnouncement(Phase Stage, int Turn, bool OwnTurn, bool Repeated)
{
    internal Color Accent => OwnTurn ? RelickVisuals.State.Own : RelickVisuals.State.Rival;
    internal string Detail => Repeated ? $"ETAPA REPETIDA  /  {(OwnTurn ? "TÚ" : "RIVAL")}" : $"TURNO {Turn:00}  /  {(OwnTurn ? "TÚ" : "RIVAL")}";
}

public partial class StageBanner : Control
{
    internal const double Duration = 0.95;
    private const double ReducedDuration = 0.7;
    private double _elapsed;
    internal bool ReducedMotion { get; set; }
    internal StageAnnouncement? Current { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 12;
        Resized += QueueRedraw;
        Stop();
    }

    internal void Play(StageAnnouncement announcement)
    {
        Current = announcement;
        _elapsed = 0;
        Show();
        SetProcess(true);
        QueueRedraw();
    }

    internal void Stop()
    {
        Current = null;
        Hide();
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed >= (ReducedMotion ? ReducedDuration : Duration)) Stop();
        else if (!ReducedMotion) QueueRedraw();
    }

    public override void _Draw()
    {
        if (Current is not { } announcement) return;
        var style = StageAppearance.For(announcement.Stage);
        var accent = announcement.Accent;
        var enter = ReducedMotion ? 1 : Mathf.SmoothStep(0, 1, Math.Min(1, (float)_elapsed / 0.18f));
        var exit = ReducedMotion ? 0 : Mathf.SmoothStep(0, 1, Math.Clamp(((float)_elapsed - 0.69f) / 0.26f, 0, 1));
        var opacity = enter * (1 - exit);
        var center = new Vector2(Size.X / 2 + 48 * (enter + exit - 1), Size.Y * 0.46f);
        var width = Math.Min(Size.X * 0.77f, 764) * Mathf.Lerp(0.9f, 1, enter);
        var height = Math.Clamp(Size.Y * 0.20f, 75, 124);
        var left = center.X - width / 2;
        var right = center.X + width / 2;
        var top = center.Y - height / 2;
        var bottom = center.Y + height / 2;
        var bevel = height * 0.24f;
        Vector2[] frame = [new(left, center.Y), new(left + bevel, top), new(right - bevel, top),
            new(right, center.Y), new(right - bevel, bottom), new(left + bevel, bottom)];
        var fill = RelickVisuals.Playmat.Background.Lerp(accent, 0.16f);
        DrawColoredPolygon(frame, BoardArt.Tint(fill, 0.96f * opacity));
        DrawPolyline([.. frame, frame[0]], BoardArt.Tint(accent, 0.85f * opacity), 2, true);
        var rail = Math.Min(112, Size.X * 0.09f) * enter;
        DrawLine(new(left - rail, center.Y), frame[0], BoardArt.Tint(accent, opacity * 0.6f), 2, true);
        DrawLine(frame[3], new(right + rail, center.Y), BoardArt.Tint(accent, opacity * 0.6f), 2, true);

        var icon = height * 0.31f;
        var fontSize = (int)(height * 0.40f);
        var titleWidth = ThemeDB.FallbackFont.GetStringSize(style.Title, fontSize: fontSize).X;
        var titleStart = center.X - (titleWidth + icon + 14) / 2;
        BoardArt.CenterText(this, announcement.Detail, new(center.X, top + height * 0.28f), (int)(height * 0.13f), BoardArt.Tint(accent, opacity));
        BoardArt.Icon(this, style.Icon, new(titleStart + icon / 2, center.Y + height * 0.10f), icon, BoardArt.Tint(accent, opacity));
        BoardArt.Text(this, style.Title, new(titleStart + icon + 14, center.Y + height * 0.26f), fontSize, BoardArt.Tint(BoardArt.Ink, opacity));
    }
}

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
        // A dimmed sweep across the board isolates the announcement, then streaks carry it forward.
        DrawRect(new Rect2(0, top - height * 0.55f, Size.X, height * 2.1f), new Color(0, 0, 0, 0.34f * opacity));
        if (!ReducedMotion)
        {
            for (var streak = 0; streak < 9; streak++)
            {
                var lane = top - height * 0.35f + streak * height * 1.7f / 8;
                var speed = 0.75f + 0.5f * ((streak * 37) % 10) / 10f;
                var x = Mathf.PosMod(Size.X * (0.15f + streak * 0.21f) + (float)_elapsed * Size.X * speed, Size.X * 1.3f) - Size.X * 0.15f;
                var length = 70 + (streak * 53) % 120;
                DrawLine(new(x, lane), new(x + length, lane), BoardArt.Tint(accent, 0.38f * opacity), streak % 3 == 0 ? 3 : 1.5f, true);
            }
        }
        var fill = RelickVisuals.Playmat.Background.Lerp(accent, 0.20f);
        RelickVisuals.Shadow(this, new Rect2(left + bevel, top + 4, width - bevel * 2, height - 8), BoardArt.Tint(accent, 0.40f * opacity), 26, 4);
        DrawColoredPolygon(frame, BoardArt.Tint(fill, 0.96f * opacity));
        var sheen = BoardArt.Tint(accent, 0.18f * opacity);
        DrawPolygon([frame[1], frame[2], new(right - bevel * 0.5f, center.Y), new(left + bevel * 0.5f, center.Y)], [sheen, sheen, Colors.Transparent, Colors.Transparent]);
        DrawPolyline([.. frame, frame[0]], BoardArt.Tint(accent, 0.95f * opacity), 2.5f, true);
        var rail = Math.Min(112, Size.X * 0.09f) * enter;
        DrawLine(new(left - rail, center.Y), frame[0], BoardArt.Tint(accent, opacity * 0.6f), 2, true);
        DrawLine(frame[3], new(right + rail, center.Y), BoardArt.Tint(accent, opacity * 0.6f), 2, true);

        var icon = height * 0.31f;
        var fontSize = (int)(height * 0.40f);
        var titleWidth = RelickVisuals.Display.GetStringSize(style.Title, fontSize: fontSize).X;
        var titleStart = center.X - (titleWidth + icon + 14) / 2;
        BoardArt.CenterText(this, announcement.Detail, new(center.X, top + height * 0.28f), (int)(height * 0.13f), BoardArt.Tint(accent, opacity));
        BoardArt.Icon(this, style.Icon, new(titleStart + icon / 2, center.Y + height * 0.10f), icon, BoardArt.Tint(accent, opacity));
        BoardArt.Title(this, style.Title, new(titleStart + icon + 14, center.Y + height * 0.26f), fontSize, BoardArt.Tint(BoardArt.Ink, opacity));
    }
}

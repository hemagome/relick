using Godot;
using Relick.Core;

namespace Relick;

/// <summary>
/// The public response chain drawn as memory modules on a circuit bus: each link is a numbered module with the card's
/// art, its owner's colour and its RAM speed (8, 16 or 32). New links slot in and a trace joins them to the previous
/// one; at resolution the bus is read back from the last module to the first, marking each as resolved or canceled.
/// It only draws: it never takes the mouse, so the response buttons under or around it keep working.
/// </summary>
public partial class ResponseChain : Control
{
    internal enum Outcome { Pending, Resolved, Canceled, Continues }

    internal sealed class Link
    {
        public int Actor { get; init; }
        public string CardId { get; init; } = "";
        public bool Attack { get; init; }
        public Texture2D? Art { get; init; }
        public Outcome Target { get; set; }
        public Outcome Outcome { get; set; }
        // Seconds since this module started to slot in; negative while it waits for the previous one.
        public double Age { get; set; }
        public double OutcomeAge { get; set; }
        public bool Matches(PublicChainLink other) => other.Actor == Actor && other.CardId == CardId && other.Attack == Attack;
    }

    internal const double EnterSeconds = 0.34, TraceSeconds = 0.2, Stagger = 0.26, StepSeconds = 0.5, HoldSeconds = 1.1,
        FadeSeconds = 0.32, ReducedHoldSeconds = 1.8;
    internal const float MaxModuleHeight = 150, MinModuleHeight = 46, ModuleRatio = 0.72f;
    private readonly List<Link> _links = [];
    private List<PublicChainLink>? _queued;
    private bool _resolving;
    private double _resolveClock, _fade;
    private int _mySeat;

    internal bool ReducedMotion { get; set; }
    /// <summary>Tests drive time with <see cref="Advance"/> instead of the frame clock.</summary>
    internal bool ManualClock { get; set; }
    internal IReadOnlyList<Link> Links => _links;
    /// <summary>True while there is a chain to show: a live one of two links or more, or a resolution still playing.</summary>
    internal bool HasContent => _links.Count >= 2;
    internal bool Resolving => _resolving;
    internal int ResolvedSteps => _links.Count(l => l.Outcome != Outcome.Pending);
    internal bool Entering => _links.Any(l => l.Age < EnterSeconds + TraceSeconds);
    internal float Opacity => _fade <= 0 ? 1 : (float)Math.Clamp(1 - _fade / FadeSeconds, 0, 1);
    /// <summary>The module rectangles in local coordinates, in chain order, as last laid out.</summary>
    internal IReadOnlyList<Rect2> ModuleRects => Layout(Size, _links.Count).Modules;

    /// <summary>Speed of a card in the RAM rules (6.1–6.7): Error de Carga answers anything, COMBO cards answer plays.</summary>
    internal static int Ram(string cardId) => CardCatalog.All.TryGetValue(cardId, out var card)
        ? card.CancelActivation ? 32 : card.IsCombo ? 16 : 8 : 8;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 18; Visible = false;
        ReducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
            ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool();
        Resized += QueueRedraw;
        SetProcess(false);
    }

    /// <summary>Shows the chain still open. Links already on screen stay; only new ones slot in.</summary>
    internal void Present(IReadOnlyList<PublicChainLink> live, int mySeat, bool animate)
    {
        if (_resolving)
        {
            // A new chain opened by the same move waits for the resolution on screen to finish.
            _queued = live.Count >= 2 ? live.ToList() : null;
            return;
        }
        _mySeat = mySeat;
        if (live.Count < 2) { Clear(); return; }
        Merge(live, animate);
        _fade = 0;
        Wake();
    }

    /// <summary>Plays a resolved chain from its last link to its first. Missing links slot in first.</summary>
    internal void Resolve(IReadOnlyList<PublicChainLink> resolved, int mySeat, IReadOnlyList<PublicChainLink>? next = null)
    {
        _queued = next is { Count: >= 2 } ? next.ToList() : null;
        _mySeat = mySeat;
        if (resolved.Count < 2)
        {
            Clear();
            if (_queued is { } queued) { _queued = null; Present(queued, mySeat, true); }
            return;
        }
        _resolving = false;
        Merge(resolved, true);
        for (var i = 0; i < _links.Count; i++)
            _links[i].Target = resolved[i].Attack ? Outcome.Continues : resolved[i].Canceled ? Outcome.Canceled : Outcome.Resolved;
        _resolving = true; _resolveClock = 0; _fade = 0;
        if (ReducedMotion)
            foreach (var link in _links) { link.Outcome = link.Target; link.OutcomeAge = StepSeconds; }
        Wake();
    }

    internal void Clear()
    {
        _links.Clear(); _resolving = false; _resolveClock = 0; _fade = 0; _queued = null;
        Visible = false; SetProcess(false); QueueRedraw();
    }

    /// <summary>Moves every animation forward; the arena calls it from its frame, tests call it directly.</summary>
    internal void Advance(double delta)
    {
        if (_links.Count == 0) return;
        delta = Math.Max(0, delta);
        foreach (var link in _links)
        {
            link.Age += delta;
            if (link.Outcome != Outcome.Pending) link.OutcomeAge += delta;
        }
        if (_resolving)
        {
            // The read-back starts once every module has slotted in, counting only the time since then.
            var settled = _links.Min(l => l.Age) - (EnterSeconds + TraceSeconds);
            _resolveClock += ReducedMotion ? delta : Math.Clamp(settled, 0, delta);
            var steps = ReducedMotion ? _links.Count : Math.Min(_links.Count, (int)(_resolveClock / StepSeconds) + (Entering ? 0 : 1));
            for (var step = 0; step < steps; step++)
            {
                var link = _links[_links.Count - 1 - step];
                if (link.Outcome == Outcome.Pending) { link.Outcome = link.Target; link.OutcomeAge = 0; }
            }
            var done = ReducedMotion ? ReducedHoldSeconds : _links.Count * StepSeconds + HoldSeconds;
            if (_resolveClock >= done)
            {
                if (ReducedMotion) _fade = FadeSeconds; else _fade += Math.Min(delta, _resolveClock - done);
                if (_fade >= FadeSeconds)
                {
                    var queued = _queued; var seat = _mySeat;
                    Clear();
                    if (queued != null) Present(queued, seat, true);
                    return;
                }
            }
        }
        Modulate = new Color(1, 1, 1, Opacity);
        if (!_resolving && !Entering && ReducedMotion) SetProcess(false);
        QueueRedraw();
    }

    public override void _Process(double delta) { if (!ManualClock) Advance(delta); }

    private void Wake()
    {
        Visible = HasContent; Modulate = new Color(1, 1, 1, Opacity);
        SetProcess(HasContent); QueueRedraw();
    }

    private void Merge(IReadOnlyList<PublicChainLink> chain, bool animate)
    {
        var kept = _links.Count <= chain.Count && _links.Select((l, i) => l.Matches(chain[i])).All(m => m) ? _links.Count : 0;
        if (kept == 0) _links.Clear();
        var start = _links.Count == 0 ? 0.0 : Math.Min(0, _links[^1].Age - EnterSeconds);
        for (var i = kept; i < chain.Count; i++)
        {
            var link = chain[i];
            var card = CardCatalog.All.GetValueOrDefault(link.CardId);
            var wait = (i - kept) * Stagger;
            _links.Add(new Link
            {
                Actor = link.Actor, CardId = link.CardId, Attack = link.Attack,
                Art = card == null ? null : CardShowcase.Artwork(card),
                Age = !animate || ReducedMotion ? EnterSeconds + TraceSeconds : Math.Min(start, 0) - wait
            });
        }
    }

    /// <summary>The size the chain needs for <paramref name="count"/> links within the given limits.</summary>
    internal static Vector2 Measure(int count, Vector2 limit)
    {
        if (count <= 0) return Vector2.Zero;
        var height = Math.Clamp(limit.Y - 7 - HeaderHeight(limit.Y), 0, MaxModuleHeight);
        var width = height * ModuleRatio;
        var gap = Math.Clamp(width * 0.3f, 10, 34);
        var total = count * width + (count - 1) * gap;
        if (total > limit.X - 8 && total > 0)
        {
            var scale = (limit.X - 8) / total;
            height *= scale; width *= scale; gap *= scale; total = limit.X - 8;
        }
        height = Mathf.Floor(height);
        var header = HeaderHeight(height + 7 + 18);
        return new Vector2(Mathf.Floor(total) + 8, height + 7 + header);
    }

    private static float HeaderHeight(float height) => height >= 120 ? 18 : 0;

    private static (float Header, Rect2[] Modules) Layout(Vector2 size, int count)
    {
        if (count == 0) return (0, []);
        var header = HeaderHeight(size.Y);
        // Room for the drop shadow keeps every stroke inside the control's rectangle.
        var height = Math.Max(1, size.Y - header - 7);
        var width = height * ModuleRatio;
        var room = size.X - 8;
        var gap = count > 1 ? Math.Max(0, (room - count * width) / (count - 1)) : 0;
        if (count * width > room) { width = room / count; gap = 0; }
        var origin = 4 + (room - (count * width + (count - 1) * gap)) / 2;
        return (header, Enumerable.Range(0, count).Select(i => new Rect2(origin + i * (width + gap), header + 3, width, height)).ToArray());
    }

    private static Color RamColor(int ram) => ram switch
    {
        32 => new Color("ff5ad1"), 16 => BoardArt.Gold, _ => RelickVisuals.State.Target
    };

    private static float Ease(double t) { var x = (float)Math.Clamp(t, 0, 1); return 1 - (1 - x) * (1 - x) * (1 - x); }

    public override void _Draw()
    {
        if (_links.Count < 2) return;
        var (header, modules) = Layout(Size, _links.Count);
        var font = RelickVisuals.Strong; var display = RelickVisuals.Display;
        // The slot board behind the modules: a dark panel with a faint grid, so the chain reads over a busy field.
        var panel = new Rect2(Vector2.Zero, Size - new Vector2(0, 1));
        DrawRect(panel, new Color(0.01f, 0.03f, 0.05f, 0.78f));
        var grid = new Color(BoardArt.Gold.R, BoardArt.Gold.G, BoardArt.Gold.B, 0.06f);
        for (var gx = 12f; gx < panel.Size.X; gx += 12) DrawLine(new Vector2(gx, 0), new Vector2(gx, panel.Size.Y), grid, 1);
        for (var gy = 12f; gy < panel.Size.Y; gy += 12) DrawLine(new Vector2(0, gy), new Vector2(panel.Size.X, gy), grid, 1);
        DrawRect(panel, BoardArt.Tint(BoardArt.Gold, 0.35f), false, 1);
        if (header > 0)
        {
            var title = _resolving ? $"RESOLVIENDO · {_links.Count} → 1" : $"CADENA DE RESPUESTA · {_links.Count}";
            DrawString(display, new Vector2(0, 13), title, HorizontalAlignment.Center, Size.X, 12, BoardArt.Gold);
        }
        // The bus: traces run between modules, drawn as each new module slots in.
        for (var i = 1; i < _links.Count; i++)
        {
            var progress = Ease((_links[i].Age - EnterSeconds * 0.4) / TraceSeconds);
            if (progress <= 0) continue;
            DrawTrace(modules[i - 1], modules[i], progress, i);
        }
        for (var i = 0; i < _links.Count; i++)
        {
            var link = _links[i];
            if (link.Age < 0) continue;
            var enter = Ease(link.Age / EnterSeconds);
            var mine = link.Actor == _mySeat;
            var rect = modules[i];
            rect.Position += new Vector2(0, (1 - enter) * rect.Size.Y * 0.45f * (mine ? 1 : -1));
            DrawModule(link, i, rect, enter, font, display);
        }
        // During resolution a pulse runs back along the bus from the module that resolves to the one it cancels.
        if (_resolving && !ReducedMotion)
            for (var i = 1; i < _links.Count; i++)
            {
                var link = _links[i];
                if (link.Outcome != Outcome.Resolved || _links[i - 1].Target != Outcome.Canceled || link.OutcomeAge > StepSeconds) continue;
                var t = Ease(link.OutcomeAge / StepSeconds);
                var from = new Vector2(modules[i].Position.X, modules[i].GetCenter().Y);
                var to = new Vector2(modules[i - 1].End.X, modules[i - 1].GetCenter().Y);
                DrawCircle(from.Lerp(to, t), Math.Max(3, modules[i].Size.X * 0.04f), RelickVisuals.State.Danger);
            }
    }

    private void DrawTrace(Rect2 left, Rect2 right, float progress, int index)
    {
        var color = BoardArt.Tint(BoardArt.Gold, 0.75f);
        var spacing = Math.Max(3, left.Size.Y * 0.05f);
        var start = left.End.X; var end = right.Position.X;
        if (end - start < 2) return;
        var reach = Mathf.Lerp(start, end, progress);
        for (var lane = -1; lane <= 1; lane++)
        {
            // Each line of the bus jogs once, as a routed circuit trace does.
            var y0 = left.GetCenter().Y + lane * spacing; var y1 = y0 + (index % 2 == 0 ? -spacing : spacing) * 0.8f;
            var mid = (start + end) / 2 + lane * 2;
            var points = new List<Vector2> { new(start, y0) };
            if (reach > mid - 3) { points.Add(new(mid - 3, y0)); points.Add(new(Math.Min(reach, mid + 3), y1)); }
            if (reach > mid + 3) points.Add(new(reach, y1));
            if (points.Count == 1) points.Add(new(reach, y0));
            DrawPolyline(points.ToArray(), lane == 0 ? BoardArt.Gold : color, lane == 0 ? 2 : 1, true);
        }
        DrawCircle(new Vector2(start, left.GetCenter().Y), spacing * 0.7f, BoardArt.Gold);
        if (progress >= 1) DrawCircle(new Vector2(end, left.GetCenter().Y + (index % 2 == 0 ? -spacing : spacing) * 0.8f), spacing * 0.7f, BoardArt.Gold);
        else if (!ReducedMotion) DrawCircle(new Vector2(reach, left.GetCenter().Y), spacing, Colors.White);
    }

    private void DrawModule(Link link, int index, Rect2 rect, float alpha, Font font, Font display)
    {
        var owner = link.Actor == _mySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral;
        var canceled = link.Outcome == Outcome.Canceled;
        var edge = canceled ? RelickVisuals.State.Danger : owner;
        var small = rect.Size.Y < 90;
        // The board: a dark circuit plate with gold contacts along its lower edge and a key notch.
        DrawRect(new Rect2(rect.Position + new Vector2(2, 3), rect.Size), new Color(0, 0, 0, 0.45f * alpha));
        var plate = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.07f, 0.08f, 0.94f * alpha).Lerp(BoardArt.Tint(owner, 0.94f * alpha), 0.12f),
            BorderColor = BoardArt.Tint(edge, alpha), CornerRadiusTopLeft = 2, CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2
        };
        plate.SetBorderWidthAll(Math.Max(2, (int)(rect.Size.X / 40)));
        DrawStyleBox(plate, rect);
        var pins = Math.Max(4, rect.Size.Y * 0.07f);
        var pinRow = new Rect2(rect.Position.X + 4, rect.End.Y - pins - 3, rect.Size.X - 8, pins);
        var count = Math.Max(4, (int)(pinRow.Size.X / 5));
        var notch = (int)(count * 0.38f);
        for (var p = 0; p < count; p++)
        {
            if (p == notch) continue;
            var x = pinRow.Position.X + p * pinRow.Size.X / count;
            DrawRect(new Rect2(x, pinRow.Position.Y, Math.Max(1, pinRow.Size.X / count - 1.5f), pinRow.Size.Y), BoardArt.Tint(BoardArt.Gold, 0.85f * alpha));
        }
        // The window shows the card's art, cropped to fill.
        var inset = Math.Max(3, rect.Size.X * 0.05f);
        var window = new Rect2(rect.Position.X + inset, rect.Position.Y + inset, rect.Size.X - inset * 2, pinRow.Position.Y - rect.Position.Y - inset * 1.6f);
        DrawRect(window, new Color(0.01f, 0.02f, 0.03f, alpha));
        if (link.Art is { } art && window.Size.X > 0 && window.Size.Y > 0)
        {
            var source = art.GetSize(); var aspect = window.Size.X / window.Size.Y;
            var crop = source.X / source.Y > aspect ? new Vector2(source.Y * aspect, source.Y) : new Vector2(source.X, source.X / aspect);
            var region = new Rect2((source - crop) / 2 * new Vector2(1, 0.6f), crop);
            DrawTextureRectRegion(art, window, region, new Color(1, 1, 1, alpha * (canceled ? 0.42f : 1)));
        }
        // The owner's name strip across the bottom of the window, so colour is never the only cue.
        var strip = Math.Max(11, window.Size.Y * 0.16f);
        var stripRect = new Rect2(window.Position.X, window.End.Y - strip, window.Size.X, strip);
        DrawRect(stripRect, new Color(0.02f, 0.03f, 0.04f, 0.78f * alpha));
        DrawRect(new Rect2(stripRect.Position, new Vector2(stripRect.Size.X, 2)), BoardArt.Tint(owner, alpha));
        var ownerSize = (int)Math.Clamp(strip * 0.62f, 8, 12);
        DrawString(font, new Vector2(stripRect.Position.X, stripRect.Position.Y + strip * 0.5f + ownerSize * 0.38f),
            link.Attack ? (link.Actor == _mySeat ? "TU ATAQUE" : "ATAQUE RIVAL") : link.Actor == _mySeat ? "TÚ" : "RIVAL",
            HorizontalAlignment.Center, stripRect.Size.X, ownerSize, BoardArt.Tint(owner, alpha));
        // Address tile: the link's position in the chain.
        var tile = Math.Clamp(rect.Size.X * 0.3f, 16, 30);
        var tileRect = new Rect2(rect.Position + new Vector2(inset * 0.6f, inset * 0.6f), new Vector2(tile, tile));
        DrawRect(tileRect, BoardArt.Tint(edge, alpha));
        DrawRect(tileRect.Grow(-2), new Color(0.02f, 0.04f, 0.05f, alpha));
        var numberSize = (int)(tile * 0.62f);
        DrawString(display, new Vector2(tileRect.Position.X, tileRect.GetCenter().Y + numberSize * 0.36f), (index + 1).ToString(),
            HorizontalAlignment.Center, tile, numberSize, BoardArt.Tint(edge, alpha));
        // RAM badge: speed as a pill and as stacked bars (one, two or four), readable without colour.
        var ram = Ram(link.CardId); var ramColor = RamColor(ram);
        var badgeSize = (int)Math.Clamp(rect.Size.X * 0.12f, 8, 12);
        var label = small ? ram.ToString() : ram + " RAM";
        var badgeWidth = font.GetStringSize(label, HorizontalAlignment.Left, -1, badgeSize).X + 8;
        var badge = new Rect2(rect.End.X - badgeWidth - inset * 0.6f, rect.Position.Y + inset * 0.6f, badgeWidth, badgeSize + 5);
        DrawRect(badge, new Color(0.02f, 0.03f, 0.04f, 0.9f * alpha));
        DrawRect(badge, BoardArt.Tint(ramColor, alpha), false, 1);
        DrawString(font, new Vector2(badge.Position.X, badge.End.Y - 3.5f), label, HorizontalAlignment.Center, badge.Size.X, badgeSize, BoardArt.Tint(ramColor, alpha));
        var bars = ram == 32 ? 4 : ram == 16 ? 2 : 1;
        for (var b = 0; b < bars; b++)
            DrawRect(new Rect2(badge.End.X - 3 - b * 4, badge.End.Y + 2, 3, 3), BoardArt.Tint(ramColor, alpha));
        // Waiting for an answer: the newest module of an open chain blinks its cursor.
        if (!_resolving && index == _links.Count - 1 && link.Age >= EnterSeconds)
        {
            var blink = ReducedMotion || (int)(link.Age * 2.2) % 2 == 0;
            if (blink) DrawRect(new Rect2(rect.End.X - inset - 5, pinRow.Position.Y - 9, 5, 7), BoardArt.Tint(BoardArt.Gold, alpha));
        }
        if (link.Outcome == Outcome.Pending) return;
        // Outcome: a read sweep, then a stamp.
        var age = link.OutcomeAge;
        if (!ReducedMotion && age < 0.3)
        {
            var y = Mathf.Lerp(window.Position.Y, window.End.Y, (float)(age / 0.3));
            DrawRect(new Rect2(window.Position.X, y - 2, window.Size.X, 4), BoardArt.Tint(canceled ? RelickVisuals.State.Danger : Colors.White, 0.8f * alpha));
        }
        if (canceled)
        {
            DrawRect(window, new Color(0.25f, 0.02f, 0.06f, 0.35f * alpha));
            for (var h = -window.Size.Y; h < window.Size.X; h += 9)
            {
                var a = new Vector2(window.Position.X + Math.Max(0, h), window.Position.Y + Math.Max(0, -h));
                var length = Math.Min(window.End.X - a.X, window.End.Y - a.Y);
                if (length > 0) DrawLine(a, a + new Vector2(length, length), BoardArt.Tint(RelickVisuals.State.Danger, 0.35f * alpha), 1);
            }
        }
        var stamp = link.Outcome switch { Outcome.Canceled => "ANULADO", Outcome.Continues => "CONTINÚA", _ => "RESUELTO" };
        var stampColor = link.Outcome switch
        {
            Outcome.Canceled => RelickVisuals.State.Danger, Outcome.Continues => BoardArt.Gold, _ => ArenaPresentation.Mint
        };
        var pop = ReducedMotion ? 1 : Ease(age / 0.18);
        var stampSize = (int)Math.Clamp(rect.Size.X * 0.15f, 8, 15);
        var stampWidth = Math.Min(window.Size.X, font.GetStringSize(stamp, HorizontalAlignment.Left, -1, stampSize).X + 10);
        var stampRect = new Rect2(window.GetCenter() - new Vector2(stampWidth, stampSize + 8) / 2, new Vector2(stampWidth, stampSize + 8));
        DrawRect(stampRect, new Color(0.02f, 0.03f, 0.04f, 0.88f * alpha * pop));
        DrawRect(stampRect, BoardArt.Tint(stampColor, alpha * pop), false, 2);
        DrawString(display, new Vector2(stampRect.Position.X, stampRect.End.Y - 5), stamp, HorizontalAlignment.Center, stampRect.Size.X, stampSize,
            BoardArt.Tint(stampColor, alpha * pop));
    }
}

using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Reveals both starting cards and fills their life meters on one clock.</summary>
public sealed partial class OpeningRelikarioReveal : Control
{
    internal const float TransformAt = .39f;
    internal const float FillAt = .71f;
    internal const float FilledAt = 1.51f;
    internal const float Duration = 1.97f;
    private static readonly Color Gold = new("f2cc81");
    private static readonly Color[] Accents = [ArenaPresentation.Mint, ArenaPresentation.Coral];
    private readonly RelikarioPortrait[] _portraits = new RelikarioPortrait[2];
    private readonly LifeMeter[] _meters = new LifeMeter[2];
    private readonly Texture2D?[] _cards = new Texture2D?[2];
    private readonly string[] _names = new string[2];
    private GradientTexture2D _glow = null!;
    private float _age;
    private int _lastTick, _tickCount;

    internal event Action<float>? LifeTick;
    internal IReadOnlyList<RelikarioPortrait> Portraits => _portraits;
    internal IReadOnlyList<LifeMeter> Meters => _meters;
    internal IReadOnlyList<Texture2D?> Cards => _cards;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        TextureFilter = TextureFilterEnum.Linear;
        ZIndex = 29;
        _glow = new GradientTexture2D
        {
            Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new(.5f, .5f), FillTo = new(1, .5f),
            Gradient = new Gradient { Offsets = [0, .46f, 1], Colors = [Colors.White, new Color(1, 1, 1, .22f), new Color(1, 1, 1, 0)] }
        };
        Resized += LayoutStages;
    }

    internal void Play(DuelPlayerView own, DuelPlayerView rival)
    {
        _age = 0;
        _lastTick = 0;
        _tickCount = Math.Max(own.Health, rival.Health);
        Modulate = Colors.White;
        DuelPlayerView[] players = [own, rival];
        for (var side = 0; side < 2; side++)
        {
            var player = players[side];
            _cards[side] = RelikarioArt.Card(player.RelikarioId, 1) ?? RelikarioArt.Portrait(player.RelikarioId);
            _names[side] = RelikarioCatalog.Find(player.RelikarioId)?.Name ?? player.Name;
            if (_portraits[side] == null) BuildStage(side, player.RelikarioId);
            _portraits[side].SetRelikario(player.RelikarioId);
            _portraits[side].SetHealth(player.Health);
            _portraits[side].ReturnToRest();
            _portraits[side].Hide();
            _meters[side].SetHealth(player.Health, player.MaxHealth, animate: false);
            _meters[side].SetOpeningHealth(0);
        }
        Show();
        LayoutStages();
    }

    private void BuildStage(int side, string relikarioId)
    {
        var portrait = new RelikarioPortrait
        {
            PortraitHeight = 1, AccentColor = Accents[side], FlipHorizontal = side == 1, StandingOnTable = true
        };
        portrait.SetRelikario(relikarioId);
        _portraits[side] = portrait;
        var meter = new LifeMeter { Stacked = true };
        _meters[side] = meter;
        AddChild(portrait); AddChild(meter);
        meter.MouseFilter = MouseFilterEnum.Ignore;
    }

    internal void Stop()
    {
        if (!Visible) return;
        Hide();
        foreach (var meter in _meters) meter.SetOpeningHealth(null);
        Array.Clear(_cards);
    }

    internal void AdvancePlayback(double delta)
    {
        _age = Math.Min(Duration, _age + (float)delta);
        var reveal = Mathf.SmoothStep(TransformAt, TransformAt + .24f, _age);
        foreach (var portrait in _portraits)
        {
            portrait.Visible = reveal > 0;
            portrait.Modulate = BoardArt.Tint(Colors.White, reveal);
            portrait.Scale = Vector2.One * Mathf.Lerp(.84f, 1, reveal);
        }
        var fill = Mathf.Clamp((_age - FillAt) / (FilledAt - FillAt), 0, 1);
        var tick = (int)Math.Floor(fill * _tickCount);
        if (tick > _lastTick)
        {
            foreach (var meter in _meters)
                meter.SetOpeningHealth(Math.Clamp(tick * meter.Health / _tickCount, Math.Min(1, meter.Health), meter.Health));
            _lastTick = tick;
            LifeTick?.Invoke(tick == _tickCount ? 1.38f : Mathf.Lerp(.91f, 1.17f, fill));
        }
        Modulate = BoardArt.Tint(Colors.White, 1 - Mathf.SmoothStep(FilledAt + .22f, Duration, _age));
        if (_age >= Duration) Stop();
        else QueueRedraw();
    }

    private float StageWidth => Math.Min(Size.X * .88f, Size.Y * 1.7f);
    private Vector2 Center(int side) => new(Size.X / 2 + StageWidth * (side == 0 ? -.255f : .255f), Size.Y * .45f);

    private void LayoutStages()
    {
        if (!Visible) return;
        var meterWidth = Math.Min(246, StageWidth * .34f);
        var stageSize = new Vector2(StageWidth * .44f, Size.Y * .66f);
        for (var side = 0; side < 2; side++)
        {
            var center = Center(side);
            var portrait = _portraits[side];
            portrait.Position = center - stageSize * .5f - new Vector2(0, Size.Y * .015f);
            portrait.Size = stageSize;
            portrait.PivotOffset = stageSize * new Vector2(.5f, .86f);
            var meter = _meters[side];
            meter.Position = new Vector2(center.X - meterWidth / 2, Size.Y * .83f);
            meter.Size = new Vector2(meterWidth, 56);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || Size.X <= 0 || Size.Y <= 0) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("090f1b"));
        var cardHeight = Size.Y * .54f;
        var turn = Mathf.SmoothStep(TransformAt - .16f, TransformAt + .05f, _age);
        var burst = Mathf.SmoothStep(TransformAt - .10f, TransformAt, _age) * (1 - Mathf.SmoothStep(TransformAt, TransformAt + .34f, _age));
        var divider = BoardArt.Tint(Gold, .28f);
        DrawLine(new Vector2(Size.X / 2, Size.Y * .19f), new Vector2(Size.X / 2, Size.Y * .92f), divider, 1, true);
        var font = RelickVisuals.Strong;
        for (var side = 0; side < 2; side++)
        {
            var center = Center(side);
            var accent = Accents[side];
            Caption(side == 0 ? "TU RELIKARIO" : "RELIKARIO RIVAL", center.X, Size.Y * .115f,
                (int)Math.Clamp(Size.Y * .027f, 13, 19), BoardArt.Muted);
            var radius = cardHeight * .65f;
            DrawTextureRect(_glow, new Rect2(center - Vector2.One * radius * 1.45f, Vector2.One * radius * 2.9f), false,
                BoardArt.Tint(accent, .44f));
            DrawArc(center, radius * (1 + burst * .25f), 0, Mathf.Tau, 96, BoardArt.Tint(accent, .32f + burst * .40f), 1, true);
            DrawArc(center, radius * .92f, 0, Mathf.Tau, 96, BoardArt.Tint(accent, .13f), 1, true);
            if (_cards[side] is { } art && turn < 1)
            {
                var cardSize = new Vector2(cardHeight * EvolutionReveal.CardAspect * Mathf.Lerp(1, .035f, turn), cardHeight);
                var card = new Rect2(center - cardSize / 2, cardSize);
                DrawTextureRect(art, card, false, BoardArt.Tint(Colors.White, 1 - turn * turn));
                DrawRect(card, BoardArt.Tint(accent.Lerp(Gold, burst), 1 - turn * .55f), false, 1.5f);
            }
            if (burst > 0)
            {
                var beam = new Vector2(12 + 54 * burst, cardHeight * .65f);
                DrawTextureRect(_glow, new Rect2(center - beam, beam * 2), false, BoardArt.Tint(Gold, burst));
                DrawLine(center - new Vector2(0, cardHeight * .56f), center + new Vector2(0, cardHeight * .56f),
                    BoardArt.Tint(Colors.White, burst), 1 + 2 * burst, true);
            }
            Caption(_names[side], center.X, Size.Y * .81f, (int)Math.Clamp(Size.Y * .046f, 16, 30), accent);
        }
        void Caption(string text, float x, float y, int size, Color color)
        {
            var width = font.GetStringSize(text, fontSize: size).X;
            DrawString(font, new Vector2(x - width / 2, y), text, fontSize: size, modulate: color);
        }
    }
}

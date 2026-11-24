using Godot;

namespace Relick;

/// <summary>
/// Damage stopped or lowered by a card. A reduction raises a shield arc toward the attacker that takes the blow and cracks,
/// with the original damage struck through; the reduced damage then lands as a normal hit. A block stops the blow before it
/// reaches the portrait: an attack projectile bounces off a full shield, the card responsible pops over the portrait and a
/// BLOQUEADO stamp slams down. Reduced motion keeps the shield, card and stamp without movement.
/// </summary>
public partial class ImpactLayer
{
    private sealed record GuardEffect(RelikarioPortrait Defender, RelikarioPortrait? Attacker, Texture2D? Art,
        Texture2D? Projectile, Texture2D? ProjectileLeft, Color Accent, int Original, int Final, bool Blocked)
    {
        public double Age { get; set; }
        /// <summary>Seconds the bounced projectile travels before it meets the shield.</summary>
        public double Impact => Projectile != null ? 0.36 : 0;
        public double Duration => Impact + (Blocked ? 1.35 : 1.1);
    }

    private readonly List<GuardEffect> _guards = [];
    internal const string BlockedStamp = "BLOQUEADO";
    internal int GuardCount => _guards.Count;
    internal bool GuardBlocked(int index) => _guards[index].Blocked;

    /// <param name="rival">The other duelist: the shield faces them.</param>
    /// <param name="bounce">A block whose attack was not already shown in flight brings its own blow, which bounces off.</param>
    internal void Guard(RelikarioPortrait defender, RelikarioPortrait? rival, bool bounce, Texture2D? art, Color accent, int original, int final, bool blocked)
    {
        if (!defender.IsVisibleInTree()) return;
        var attacker = rival;
        Texture2D? projectile = null, left = null;
        if (bounce && blocked && !ReducedMotion && attacker is { } from && from.IsVisibleInTree())
        {
            projectile = RelikarioArt.Load(from.RelikarioId, "attack-projectile");
            left = RelikarioArt.Load(from.RelikarioId, "attack-projectile-left");
        }
        _guards.Add(new(defender, attacker, art, projectile, left, accent, original, final, blocked));
        SetProcess(true); QueueRedraw();
    }

    private void AgeGuards(double step)
    {
        for (var i = _guards.Count - 1; i >= 0; i--)
        {
            _guards[i].Age += step;
            if (_guards[i].Age >= _guards[i].Duration) _guards.RemoveAt(i);
        }
    }

    private void DrawGuards(Vector2 origin)
    {
        foreach (var guard in _guards)
            if (guard.Defender.IsVisibleInTree()) DrawGuard(guard, origin);
    }

    private void DrawGuard(GuardEffect g, Vector2 origin)
    {
        var portrait = g.Defender.GetGlobalRect();
        var center = g.Defender.AttackTarget - origin;
        var radius = Math.Clamp(Math.Min(portrait.Size.X, portrait.Size.Y) * 0.42f, 34, 120);
        var toward = g.Attacker is { } a && a.IsVisibleInTree() ? a.AttackTarget - origin - center : Size / 2 - center;
        var facing = toward.LengthSquared() < 1 ? -Mathf.Pi / 2 : toward.Angle();
        var local = (float)(g.Age - g.Impact);
        var life = (float)(g.Duration - g.Impact);
        var fade = local < 0 ? 0 : local > life - 0.3f ? Math.Clamp((life - local) / 0.3f, 0, 1) : 1;

        if (g.Projectile != null) DrawBouncingBlow(g, origin, center, radius, facing);
        if (local < 0) return;
        var flare = ReducedMotion ? 0 : Math.Max(0, 1 - local / 0.25f);
        if (g.Blocked) DrawBlock(g, center, radius, local, fade, flare);
        else DrawReduction(g, portrait, origin, center, radius, facing, local, fade, flare);
    }

    private void DrawBouncingBlow(GuardEffect g, Vector2 origin, Vector2 center, float radius, float facing)
    {
        if (g.Attacker is not { } attacker || !attacker.IsVisibleInTree()) return;
        var (launch, width) = attacker.AttackLaunch(false);
        var from = launch - origin;
        var shield = center + Vector2.FromAngle(facing) * radius * 1.15f;
        var direction = shield.X < from.X ? -1 : 1;
        var art = direction < 0 ? g.ProjectileLeft ?? g.Projectile! : g.Projectile!;
        var flip = direction < 0 && g.ProjectileLeft == null;
        var size = art.GetSize() * (width * .39f / Math.Max(art.GetWidth(), art.GetHeight()));
        Vector2 at; float rotation, alpha = 1;
        if (g.Age < g.Impact)
        {
            var t = (float)(g.Age / g.Impact);
            at = from.Lerp(shield, t * (0.65f + 0.35f * t));
            var path = shield - from;
            rotation = Mathf.Atan2(path.Y, Math.Abs(path.X)) * direction;
        }
        else
        {
            // Knocked back and up, spinning away.
            var t = Math.Min(1, (float)(g.Age - g.Impact) / 0.4f);
            var back = (from - shield).Normalized();
            at = shield + back * radius * 1.1f * t + new Vector2(0, -radius * 1.4f * t + radius * 1.6f * t * t);
            rotation = direction * (Mathf.Tau * 0.9f * t);
            alpha = 1 - t;
            if (t >= 1) return;
        }
        DrawSetTransform(at, rotation, new(flip ? -1 : 1, 1));
        DrawTextureRect(art, new Rect2(-size / 2, size), false, new Color(1, 1, 1, alpha));
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawShieldArc(Vector2 center, float radius, float from, float to, Color color, float alpha, float width)
    {
        // Layered arcs read as a glowing energy barrier.
        DrawArc(center, radius, from, to, 48, BoardArt.Tint(color, 0.18f * alpha), width * 4, true);
        DrawArc(center, radius, from, to, 48, BoardArt.Tint(color, 0.35f * alpha), width * 2.2f, true);
        DrawArc(center, radius, from, to, 48, BoardArt.Tint(Colors.White, 0.85f * alpha).Lerp(BoardArt.Tint(color, alpha), 0.45f), width, true);
    }

    private void DrawReduction(GuardEffect g, Rect2 portrait, Vector2 origin, Vector2 center, float radius, float facing, float local, float fade, float flare)
    {
        var grow = ReducedMotion ? 1 : Mathf.Min(1, local / 0.12f);
        var span = 1.05f * grow;
        var arcRadius = radius * 1.2f;
        DrawShieldArc(center, arcRadius, facing - span, facing + span, g.Accent, fade, 5 + 6 * flare);
        // Cracks spread across the barrier where the blow landed.
        var crack = ReducedMotion ? 1 : Math.Clamp((local - 0.12f) / 0.25f, 0, 1);
        if (crack > 0)
        {
            var hit = center + Vector2.FromAngle(facing) * arcRadius;
            for (var i = -2; i <= 2; i++)
            {
                var angle = facing + Mathf.Pi + i * 0.55f;
                var mid = hit + Vector2.FromAngle(angle + 0.25f) * radius * 0.18f * crack;
                var end = mid + Vector2.FromAngle(angle - 0.2f) * radius * 0.2f * crack;
                DrawPolyline([hit, mid, end], BoardArt.Tint(Colors.White, 0.9f * fade), 2, true);
            }
        }
        // Shards fly off as the shield gives way.
        if (!ReducedMotion && local > 0.35f)
        {
            var t = Math.Min(1, (local - 0.35f) / 0.5f);
            for (var i = 0; i < 7; i++)
            {
                var angle = facing + (i - 3) * 0.32f;
                var start = center + Vector2.FromAngle(angle) * (arcRadius + radius * 0.6f * t);
                DrawLine(start, start + Vector2.FromAngle(angle + 0.6f) * 9, BoardArt.Tint(g.Accent, 1 - t), 3, true);
            }
        }
        // The damage before the card: struck through, it reads as "this much was stopped".
        var text = $"-{g.Original}";
        var size = 38;
        var font = RelickVisuals.Display;
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        // Just left of where the reduced hit lands, rising with it: it reads "-3 -1".
        var drift = Math.Clamp(local / (float)NumberSeconds, 0, 1);
        var point = new Vector2(portrait.GetCenter().X - 34 - width / 2, portrait.Position.Y + portrait.Size.Y * 0.62f) - origin +
            new Vector2(0, ReducedMotion ? 0 : -46 * drift * drift);
        var start2 = point - new Vector2(width / 2, 0);
        DrawStringOutline(font, start2, text, HorizontalAlignment.Left, -1, size, 8, new Color(0.02f, 0.03f, 0.07f, fade));
        DrawString(font, start2, text, HorizontalAlignment.Left, -1, size, new Color(0.85f, 0.88f, 0.95f, 0.85f * fade));
        var strike = ReducedMotion ? 1 : Math.Clamp((local - 0.15f) / 0.2f, 0, 1);
        if (strike > 0)
            DrawLine(start2 + new Vector2(-4, -size * 0.32f), start2 + new Vector2(-4 + (width + 8) * strike, -size * 0.32f - 6 * strike),
                BoardArt.Tint(g.Accent, fade), 4, true);
        if (g.Final == 0)
            BoardArt.CenterText(this, "0", point + new Vector2(0, size * 0.95f), 30, BoardArt.Tint(g.Accent, fade));
    }

    private void DrawBlock(GuardEffect g, Vector2 center, float radius, float local, float fade, float flare)
    {
        // A full barrier around the defender, brightest at the moment of the block.
        var pulse = ReducedMotion ? 0 : 0.5f + 0.5f * Mathf.Sin(local * Mathf.Tau * 1.6f);
        DrawCircle(center, radius * (1.15f + 0.1f * flare), BoardArt.Tint(g.Accent, (0.1f + 0.15f * flare) * fade));
        DrawShieldArc(center, radius * (1.15f + 0.1f * flare), 0, Mathf.Tau, g.Accent, fade, 4 + 5 * flare + 1.5f * pulse);
        if (!ReducedMotion && flare > 0)
            for (var ray = 0; ray < 12; ray++)
            {
                var direction = Vector2.FromAngle(ray * Mathf.Tau / 12);
                DrawLine(center + direction * radius * (1.25f + 0.2f * (1 - flare)), center + direction * radius * (1.45f + 0.45f * (1 - flare)),
                    BoardArt.Tint(g.Accent, flare), 3, true);
            }
        // The card that stopped the blow rises over the portrait.
        if (g.Art is { } art)
        {
            var rise = ReducedMotion ? 1 : Math.Min(1, local / 0.22f);
            var pop = ReducedMotion ? 1 : rise < 1 ? Mathf.Lerp(0.6f, 1.08f, rise) : Mathf.Lerp(1.08f, 1, Math.Min(1, (local - 0.22f) / 0.12f));
            var height = Math.Clamp(radius * 1.55f, 70, 180) * pop;
            var size = new Vector2(height * art.GetWidth() / art.GetHeight(), height);
            var at = center + new Vector2(0, -radius * 1.15f - height * 0.35f - 10 * rise);
            // Kept on screen: compact layouts put the portrait near an edge.
            at = at.Clamp(size / 2 + new Vector2(8, 8), Size - size / 2 - new Vector2(8, 8));
            var rect = new Rect2(at - size / 2, size);
            RelickVisuals.Shadow(this, rect.Grow(-4), BoardArt.Tint(g.Accent, 0.55f * fade), 18, 8);
            DrawTextureRect(art, rect, false, new Color(1, 1, 1, fade));
            BoardArt.Outline(this, rect, BoardArt.Tint(g.Accent, fade), 6, 2);
        }
        // The stamp slams down across the portrait.
        var slam = ReducedMotion ? 1 : Math.Min(1, Math.Max(0, local - 0.12f) / 0.16f);
        if (slam <= 0) return;
        var scale = ReducedMotion ? 1 : Mathf.Lerp(1.9f, 1, slam * slam);
        var fontSize = (int)(Math.Clamp(radius * 0.48f, 24, 54) * scale);
        var font = RelickVisuals.Display;
        var width = font.GetStringSize(BlockedStamp, HorizontalAlignment.Left, -1, fontSize).X;
        var half = new Vector2(width / 2 + 24, fontSize);
        var stampCenter = (center + new Vector2(0, radius * 0.35f)).Clamp(half, Size - half);
        DrawSetTransform(stampCenter, -0.14f, Vector2.One);
        var box = new Rect2(new Vector2(-width / 2 - 14, -fontSize * 0.95f), new Vector2(width + 28, fontSize * 1.3f));
        var alpha = fade * Math.Min(1, slam * 1.5f);
        DrawRect(box, new Color(0.02f, 0.03f, 0.07f, 0.55f * alpha));
        DrawRect(box, BoardArt.Tint(g.Accent, alpha), false, 4);
        DrawStringOutline(font, new Vector2(-width / 2, 0), BlockedStamp, HorizontalAlignment.Left, -1, fontSize, 10, new Color(0.02f, 0.03f, 0.07f, alpha));
        DrawString(font, new Vector2(-width / 2, 0), BlockedStamp, HorizontalAlignment.Left, -1, fontSize, BoardArt.Tint(g.Accent, alpha));
        DrawSetTransform(Vector2.Zero);
    }
}

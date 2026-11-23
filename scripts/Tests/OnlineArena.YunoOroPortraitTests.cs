using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckYunoOroPortrait()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        var gold = RelikarioCatalog.YunoOro.Id;
        var sprite = RelikarioArt.Load(gold, "sprite") ?? throw new Exception("Falta el PNG de Yuno Oro.");
        using var pixels = sprite.GetImage();
        Check(pixels.GetPixel(0, 0).A < .01f && pixels.GetPixel(pixels.GetWidth() / 2, pixels.GetHeight() / 3).A > .9f &&
            RelikarioArt.Load(RelikarioCatalog.YunoOroLv2.Id, "sprite")?.GetRid() == sprite.GetRid(),
            "El PNG debe conservar el canal alfa y compartirse entre niveles.");

        RelikarioPortrait Probe(float x, bool mirrored = false)
        {
            var portrait = new RelikarioPortrait { Position = new(x, 60), Size = new(430, 400), FlipHorizontal = mirrored };
            portrait.SetRelikario(gold);
            AddChild(portrait);
            portrait.SetProcess(false);
            return portrait;
        }

        var slow = Probe(20);
        var fast = Probe(20);
        var rival = Probe(470, mirrored: true);
        var effects = new ImpactLayer();
        AddChild(effects);
        try
        {
            const double tick = 1.0 / 120;
            foreach (var motion in Enum.GetValues<PortraitMotion>())
            {
                slow.Play(motion);
                fast.Play(motion);
                var poses = new HashSet<YunoOroPortraitRig.Pose>();
                for (var i = 0; i < 48; i++)
                {
                    fast.AdvancePlayback(tick);
                    poses.Add(fast.YunoOroPose ?? throw new Exception("No se cargó el rig de Yuno Oro."));
                    if (i % 2 == 1) slow.AdvancePlayback(2 * tick);
                }
                var a = slow.YunoOroPose!.Value;
                var b = fast.YunoOroPose!.Value;
                Check(fast.AssetsReady && fast.IsContinuous && fast.Motion == motion && poses.Count >= 46 &&
                    a.Offset.DistanceTo(b.Offset) < .001f && Math.Abs(a.Punch - b.Punch) < .001f,
                    $"La animación {motion} debe ser continua y coincidir a 60 y 120 Hz.");
            }

            fast.Play(PortraitMotion.Attack);
            rival.Play(PortraitMotion.Attack);
            fast.AdvancePlayback(YunoOroPortraitRig.FireAt);
            rival.AdvancePlayback(YunoOroPortraitRig.FireAt);
            var leftOrigin = fast.AttackLaunch(false).Point.X - fast.GlobalPosition.X;
            var rightOrigin = rival.AttackLaunch(false).Point.X - rival.GlobalPosition.X;
            Check(leftOrigin > fast.Size.X / 2 && rightOrigin < rival.Size.X / 2,
                "El proyectil debe salir del guante y espejarse entre asientos.");

            var reduced = new YunoOroPortraitRig(sprite);
            var full = new YunoOroPortraitRig(sprite);
            reduced.Advance(PortraitMotion.Attack, YunoOroPortraitRig.FireAt, .2, true, false);
            full.Advance(PortraitMotion.Attack, YunoOroPortraitRig.FireAt, .2, false, false);
            Check(reduced.Current.Glow == 0 && reduced.Current.Punch < full.Current.Punch,
                "El movimiento reducido debe atenuar el golpe y quitar los destellos.");

            effects.ReducedMotion = false;
            effects.LaunchAttack(fast, rival);
            effects.SetProcess(false);
            effects._Process(YunoOroPortraitRig.FireAt + .21);
            Check(effects.AttackInFlightTo(rival), "El daño debe esperar al proyectil dorado.");
            effects._Process(.14);
            Check(!effects.AttackInFlightTo(rival), "El proyectil dorado debe liberar el impacto al llegar.");
            effects.ReducedMotion = true;
            effects.LaunchAttack(rival, fast);
            Check(!effects.AttackInFlightTo(fast), "El movimiento reducido omite el proyectil.");

            fast.Play(PortraitMotion.HitCritical);
            fast.SetHealth(0);
            Check(fast.Motion == PortraitMotion.HitCritical, "El golpe crítico debe terminar antes de bajar la guardia.");
            fast.AdvancePlayback(fast.DurationSeconds + .18);
            Check(fast.Motion == PortraitMotion.ExhaustedIdle && fast.YunoOroPose!.Value.Height < .96f,
                "Yuno Oro debe bajar la guardia y seguir respirando a cero vidas.");
            var exhausted = fast.YunoOroPose;
            fast.SetRelikario(RelikarioCatalog.YunoOroLv2.Id);
            Check(fast.YunoOroPose == exhausted, "La evolución debe conservar la pose y la reproducción.");
            fast.Play(PortraitMotion.Heal);
            fast.SetHealth(2);
            fast.AdvancePlayback(fast.DurationSeconds + .18);
            Check(fast.Motion == PortraitMotion.Idle, "Al curarse debe volver a la guardia.");
        }
        finally
        {
            slow.QueueFree();
            fast.QueueFree();
            rival.QueueFree();
            effects.QueueFree();
        }

        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Yuno Oro", "Yuno"], Relikarios = [gold, RelikarioCatalog.Yuno.Id],
            Hands = [[CardCatalog.AttackYunoOro.Id], [CardCatalog.Attack.Id]], Decks = [[], []], Bonuses = [null, null]
        }, new SeededRandom(91));
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        {
            GetWindow().Size = size;
            GetWindow().ContentScaleSize = size;
            for (var seat = 0; seat < 2; seat++)
            {
                ResetAnimations();
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 720UL + (ulong)seat));
                for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var figure = _portraits[seat];
                Check(figure.UsesYunoOroRig && figure.IsVisibleInTree() && figure.FlipHorizontal == (seat == 1) &&
                    figure.GetGlobalRect().Grow(1).HasPoint(figure.AttackLaunch(lob: false).Point),
                    $"La figura y su guante deben quedar visibles en {size}, asiento {seat + 1}.");
            }
        }
        GD.Print("PVP_YUNO_ORO_ART_PASS · PNG transparente, siete animaciones a 120 Hz, evolución, proyectil y ambos asientos en cuatro tamaños.");
    }
}

using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckOsamuPresentation()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var blue = RelikarioArt.Load("osamu", "attack-card");
        var red = BoardArt.LoadCardArtwork(CardCatalog.Attack, "yuno");
        Check(blue != null && red != null && blue != red && BoardArt.LoadCardArtwork(CardCatalog.Attack, "osamu-lv2") == blue,
            "Falta la carta azul de Ataque de Osamu o su evolución muestra otro arte.");
        var super = BoardArt.LoadCardArtwork(CardCatalog.SuperAttack, "osamu");
        Check(super != null && super != blue && super == BoardArt.LoadCardArtwork(CardCatalog.SuperAttack, "yuno"),
            "Súper Ataque debe conservar su propia ilustración.");

        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Osamu", "Yuno"], Relikarios = ["osamu", "yuno"],
            Hands = [[CardCatalog.Attack.Id, CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
            Decks = [[], []], Bonuses = [null, null]
        }, new SeededRandom(73));
        game.State.Players[0].MarkForeignHandCard(1);
        game.State.Players[0].Memory.Add(CardCatalog.Attack.Id);
        game.State.Players[1].RevealHandCard(0);
        GetWindow().ContentScaleSize = new(1820, 1024);
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, 0, 103));
        Check(_handCards.Single(card => card.Name == "HandCard0").FaceTexture == blue &&
            _handCards.Single(card => card.Name == "HandCard1").FaceTexture == red,
            "Ataques de dos dueños en una mano deben conservar su arte de origen.");
        Check(_opponentCards.Single().GetNode<HandCard>("RevealedFront").FaceTexture == red &&
            _root!.FindChild("MemoryTopCard", true, false) is HandCard memory && memory.FaceTexture == blue,
            "El rival revelado y la memoria deben usar el arte de sus dueños.");
        SelectCard(0);
        Check(_showcase?.FaceTexture == blue, "El inspector no amplía el Ataque azul que se seleccionó.");

        RelikarioPortrait Probe()
        {
            var portrait = new RelikarioPortrait { Visible = false };
            portrait.SetRelikario("osamu");
            AddChild(portrait); portrait.SetProcess(false);
            return portrait;
        }
        var low = Probe();
        var high = Probe();
        const double tick = 1.0 / 120;
        foreach (var motion in Enum.GetValues<PortraitMotion>())
        {
            low.Play(motion); high.Play(motion);
            var poses = new HashSet<OsamuPortraitRig.Pose>();
            for (var step = 0; step < 48; step++)
            {
                high.AdvancePlayback(tick);
                poses.Add(high.OsamuPose ?? throw new Exception("No se cargó la figura articulada de Osamu."));
                if (step % 2 == 1) low.AdvancePlayback(2 * tick);
            }
            var a = low.OsamuPose!.Value; var b = high.OsamuPose!.Value;
            Check(high.IsContinuous && poses.Count >= 46 && high.Motion == motion &&
                a.Offset.DistanceTo(b.Offset) < .01f && Math.Abs(a.SwordAngle - b.SwordAngle) < .01f,
                $"El clip {motion} debe cambiar de pose a 120 Hz y conservar su tiempo a 60 Hz.");
        }
        high.Play(PortraitMotion.HitCritical); high.SetHealth(0);
        Check(high.Motion == PortraitMotion.HitCritical, "El último golpe debe terminar antes del reposo a cero.");
        high.AdvancePlayback(RelikarioPortrait.DurationFor(PortraitMotion.HitCritical) + .2);
        Check(high.Motion == PortraitMotion.ExhaustedIdle && high.OsamuPose!.Value.Height < .95f,
            "Osamu debe bajar la guardia y continuar respirando a cero vidas.");
        var exhausted = high.OsamuPose;
        high.SetRelikario("osamu-lv2");
        Check(high.OsamuPose == exhausted, "Evolucionar reinicia el movimiento de Osamu.");
        high.Play(PortraitMotion.Heal); high.SetHealth(3);
        high.AdvancePlayback(RelikarioPortrait.DurationFor(PortraitMotion.Heal) + .2);
        Check(high.Motion == PortraitMotion.Idle, "Después de curarse, Osamu debe recuperar su guardia.");
        low.QueueFree(); high.QueueFree();
        GD.Print("PVP_OSAMU_ART_PASS · Ataques por Relikario, siete movimientos continuos a 120 Hz y regreso a la guardia.");
    }
}

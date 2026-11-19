using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckGuardAnimation()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (capture == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(capture);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, name + ".png"));
        }
        var reduced = _impacts.ReducedMotion;
        try
        {
            foreach (var motion in new[] { false, true })
            foreach (var size in new[] { new Vector2I(1440, 1024), new(844, 390) })
            foreach (var seat in new[] { 0, 1 })
            foreach (var block in new[] { true, false })
            {
                _impacts.ReducedMotion = motion;
                GetWindow().ContentScaleSize = size;
                await Layout();
                const int defender = 1, attacker = 0;
                var game = GameEngine.FromOpening(new DuelOpening
                {
                    Names = ["Ana", "Beto"], StartingSeat = attacker, Hands = [[], []],
                    Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 8).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 8).ToList()], Bonuses = [null, null]
                }, new SeededRandom(91));
                game.State.Players[defender].Equipment = block ? CardCatalog.CardboardBox.Id : CardCatalog.DiamondHelmet.Id;
                game.State.Phase = Phase.DefenseChoice;
                game.State.PendingAttack = new(attacker, CardCatalog.Destiny.Id, 3, false);
                var before = DuelSnapshot.For(game, seat, 2850);
                ResetAnimations(); PresentTestSnapshot(before);
                await Layout();
                var cues = DuelVisualEvents.Capture(game);
                var kind = block ? DuelMoveKind.BoxDefense : DuelMoveKind.PassDefense;
                var option = DuelOptions.For(game, defender).First(o => o.Move.Kind == kind);
                Check(option.Move.Apply(game, defender).Success, "No se resuelve la defensa de prueba.");
                var after = DuelSnapshot.For(game, seat, 2851);
                after.Visuals = cues;
                var guardCue = cues.Single(c => c.Kind == DuelVisualKind.Guard);
                Check(guardCue.Blocked == block && guardCue.Original == 3 && guardCue.Value == (block ? 0 : 1), "El aviso de protección no describe el daño.");
                var origins = CaptureOrigins(after, before);
                PresentTestSnapshot(after); Animate(cues, origins);
                await Layout();
                for (var i = 0; i < 12 && (_flight != null || _flights.Count > 0 || _pendingGuards.Count > 0); i++) UpdateFlights(0.5);
                Check(_pendingGuards.Count == 0 && _impacts.GuardCount == 1 && _impacts.GuardBlocked(0) == block,
                    "La protección no se reproduce tras los vuelos.");
                if (!block) Check(_pendingImpacts.Count == 0 && _impacts.LastNumberAt != null, "La reducción pierde el golpe reducido.");
                _impacts._Process(block ? 0.6 : 0.4);
                if (!motion) await Capture($"guard-{(block ? "block" : "reduce")}-{size.X}x{size.Y}-seat{seat}");
                _impacts._Process(3);
                Check(_impacts.GuardCount == 0, "La protección queda en pantalla.");
                Animate(cues, origins); ResetAnimations();
                Check(_pendingGuards.Count == 0 && _impacts.GuardCount == 0, "Interrumpir deja protecciones pendientes.");
            }
        }
        finally { _impacts.ReducedMotion = reduced; }
        GD.Print("PVP_GUARD_ANIMATION_PASS · bloqueo con Caja y reducción con Casco, movimiento normal y reducido, dos tamaños y ambos asientos.");
    }
}

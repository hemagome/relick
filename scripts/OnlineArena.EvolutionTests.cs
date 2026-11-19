using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckEvolutionReveal()
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
        _impacts.ReducedMotion = false;
        try
        {
            foreach (var (actor, form) in new[] { (0, RelikarioCatalog.Yuno), (1, RelikarioCatalog.Osamu) })
            {
                var evolved = RelikarioCatalog.EvolutionFor(form)!;
                var oldArt = RelikarioArt.Card(form.Id, form.Level);
                var newArt = RelikarioArt.Card(evolved.Id, evolved.Level);
                Check(oldArt != null && newArt != null && oldArt != newArt, "Falta el arte de una forma del Relikario.");
                var game = GameEngine.FromOpening(new DuelOpening
                {
                    Names = ["Ana", "Beto"], Relikarios = [RelikarioCatalog.Yuno.Id, RelikarioCatalog.Osamu.Id], StartingSeat = actor,
                    Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
                    Decks = [[CardCatalog.Attack.Id], [CardCatalog.Agility.Id]], Bonuses = [null, null]
                }, new SeededRandom(84));
                game.State.Phase = Phase.Combat;
                game.State.Active.Health = 4;
                game.State.Active.Relikia = evolved.EvolutionCost;
                game.State.Active.HasNaturalSix = true;
                if (form == RelikarioCatalog.Osamu) game.State.Active.Memory.Add(CardCatalog.Agility.Id);
                var before = new[] { DuelSnapshot.For(game, 0, 400), DuelSnapshot.For(game, 1, 400) };
                Check(game.Evolve(actor).Success, "La evolución de prueba no se pudo resolver.");

                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    await Layout();
                    _needsBaseline = true;
                    PresentDuelSnapshot(before[seat]);
                    await Layout();
                    var after = DuelSnapshot.For(game, seat, 401);
                    var relative = actor == seat ? 0 : 1;
                    Check(!IsAnimating, "La forma base reproduce una evolución.");
                    PresentDuelSnapshot(after);
                    Check(IsAnimating && _evolution.RelativePlayer == relative && _evolution.FaceTexture == oldArt,
                        "La evolución no bloquea las jugadas desde la carta de origen del asiento correcto.");
                    await Layout();
                    var landing = EvolutionAnchor(relative);
                    var ownOsamu = form == RelikarioCatalog.Osamu && actor == seat;
                    if (size.X == 1440)
                        Check(ownOsamu == (landing == null), $"Aterrizaje incorrecto para {form.Id} en el asiento {seat}: {landing}.");

                    _evolution.AdvancePlayback(.36, landing);
                    if (size.X == 1440 && seat == actor) await Capture($"evolucion-{form.Id}-lv1");
                    _evolution.AdvancePlayback(.52, landing);
                    Check(_evolution.FaceTexture == newArt, "La carta no cambia al arte del nivel evolucionado.");
                    if (seat == actor && size.X is 1440 or 700) await Capture($"evolucion-{form.Id}-lv2-{size.X}");
                    _evolution.AdvancePlayback(EvolutionReveal.Duration, landing);
                    Check(!IsAnimating && _portraits[relative].RelikarioId == evolved.Id && (!ownOsamu || ChoosingDeckGallery),
                        "Al terminar no queda la forma evolucionada o se pierde la búsqueda de Osamu.");
                }

                var missed = DuelSnapshot.For(game, actor, 402);
                PresentDuelSnapshot(before[actor]); PresentDuelSnapshot(missed);
                Check(!IsAnimating, "Un salto de revisión reproduce una evolución pasada.");
                PresentDuelSnapshot(before[actor]); _needsBaseline = true;
                PresentDuelSnapshot(DuelSnapshot.For(game, actor, 401));
                Check(!IsAnimating, "Reconectar a una forma evolucionada reproduce la secuencia.");

                var current = DuelSnapshot.For(game, actor, 401);
                PresentDuelSnapshot(before[actor]); PresentDuelSnapshot(current);
                ResetAnimations();
                Check(!IsAnimating && _portraits[0].RelikarioId == evolved.Id, "Interrumpir la secuencia deja bloqueada o desactualizada la arena.");
                _impacts.ReducedMotion = true;
                PresentDuelSnapshot(before[actor]); PresentDuelSnapshot(current);
                Check(!IsAnimating && _portraits[0].RelikarioId == evolved.Id, "Movimiento reducido no muestra directamente la forma final.");
                _impacts.ReducedMotion = false;
            }
        }
        finally { _impacts.ReducedMotion = reduced; ResetAnimations(); }
        GD.Print("PVP_EVOLUTION_REVEAL_PASS · cartas Lv 1 y Lv 2, Yuno y Osamu, cuatro tamaños, ambos asientos, reconexión y movimiento reducido.");
    }
}

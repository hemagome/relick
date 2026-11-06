using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void PileAnimationSmokeTest()
    {
        try
        {
            SetProcess(false); _helpOpen = true; _die.SetProcess(false);
            _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
            async Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _drawLayoutFrames = 0;
            }
            var rng = new SkillsTestRandom();
            PlayerState Fixture(int actor = 0)
            {
                BindGame(new GameEngine(rng));
                _game.State.CurrentPlayer = actor; _game.State.Phase = Phase.Actions;
                foreach (var player in _game.State.Players)
                {
                    player.Hand.Clear(); player.Memory.Clear(); player.Skills.Clear(); player.CorruptedMemory.Clear();
                    player.Exchange.Clear(); player.Equipment = null;
                }
                return _game.State.Active;
            }
            foreach (var actor in new[] { 0, 1 })
            {
                var p = Fixture(actor); p.Hand.Add("senshuni"); Render(); await Layout();
                var origin = DrawTarget(new(actor, 0))!.GetGlobalRect();
                Execute(_game.PlayCode(actor, 0)); await Layout();
                var sequence = _drawQueue.ToArray();
                Check(sequence.Length == 3 && sequence[0].PileDestination == CardZone.Memory && sequence[0].CardId == "senshuni" &&
                    sequence[0].Origin!.Value.Position == origin.Position && sequence.Skip(1).All(d => d.PileDestination == null),
                    "Senshuni no viaja de la mano a Memoria antes de sus dos robos.");
                UpdateDrawAnimations(0.1);
                Check(_flyingFront?.Card.Id == "senshuni" && _flyingFront.Visible && !_flyingBack!.Visible,
                    "Una carta usada no muestra su frente al viajar a Memoria.");
                var time = _drawTime; Render(); await Layout();
                Check(_drawTime == time && PileTarget(actor, CardZone.Memory)!.Modulate.A == 0,
                    "Renderizar reinicia la animación o muestra Memoria antes de la llegada.");
                DrainTestCards();
                Check(!CardsInMotion && _returnHandStage == null && PileTarget(actor, CardZone.Memory)!.Modulate.A == 1 && p.Hand.Count == 2,
                    "Llegar a Memoria bloquea los robos o deja zonas invisibles.");
            }

            var own = Fixture(); own.Memory.Add("senshuni"); own.Relikia = 400; Render(); await Layout();
            Execute(_game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0)); await Layout();
            Check(_drawQueue.Count == 3 && _drawQueue.Peek().PileSource == CardZone.Memory && _drawQueue.Peek().PileDestination == CardZone.Corrupted,
                "Activar desde Memoria no anima la corrupción antes del robo.");
            UpdateDrawAnimations(0.1);
            Check(_flyingFront!.Visible && !_flyingBack!.Visible, "La corrupción no parte boca arriba desde Memoria.");
            UpdateDrawAnimations(0.25);
            Check(!_flyingFront.Visible && _flyingBack!.Visible, "La carta corrupta no se voltea al llegar.");
            DrainTestCards();
            var corrupted = PileTarget(0, CardZone.Corrupted)!;
            Check(corrupted.Amount == 1 && !corrupted.TooltipText.Contains("Senshuni") && corrupted.GetChildren().OfType<HandCard>().Count() == 0,
                "La zona de Corruptas revela la identidad de una carta.");

            // Equipment moves only when destroyed or canceled, not while activation uses memory internally.
            own = Fixture(); own.Hand.Add("lucky-pendant"); Render(); await Layout();
            Execute(_game.Equip(0, 0));
            Check(!CardsInMotion, "Equipar anima un paso temporal por Memoria.");
            own.Hand.Add("cardboard-box"); Render(); await Layout(); Execute(_game.Equip(0, 0));
            Check(_drawQueue.Single().PileSource == CardZone.Equipment && _drawQueue.Peek().CardId == "lucky-pendant",
                "Reemplazar un equipo no anima el equipo destruido.");
            DrainTestCards();

            // Canceling a code that had already been corrupted restores it with a reverse flight.
            own = Fixture(); own.Memory.Add("extra-life"); own.Relikia = 1000; own.Relikario = RelikarioCatalog.YunoLv2;
            Render(); await Layout(); rng.Enqueue(3);
            Execute(_game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0)); DrainTestDice();
            _game.State.Players[1].Hand.Add("load-error");
            Execute(_game.PlayCode(1, 0)); await Layout();
            Check(own.Memory.Contains("extra-life") && own.CorruptedMemory.Count == 0 &&
                _drawQueue.Any(d => d.PileSource == CardZone.Corrupted && d.PileDestination == CardZone.Memory),
                "Anular una activación no anima el regreso de Corruptas a Memoria.");
            DrainTestCards();

            own = Fixture(); own.Hand.Add("senshuni"); Render(); await Layout();
            Execute(_game.PlayCode(0, 99)); Check(!CardsInMotion, "Una jugada rechazada anima cartas.");
            Execute(_game.PlayCode(0, 0)); await Layout(); UpdateDrawAnimations(0.1);
            NewGame();
            Check(_activeDraw == null && _flyingCard == null && _returnHandStage == null && _drawQueue.All(d => d.InitialDeal),
                "Reiniciar conserva una carta viajando a Memoria o Corruptas.");
            GD.Print("PILE_ANIMATION_PASS · ambos jugadores, Memoria antes del efecto, corrupción boca abajo, equipo destruido, anulación, privacidad, continuidad y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

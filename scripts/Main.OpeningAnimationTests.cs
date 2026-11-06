using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void OpeningAnimationSmokeTest()
    {
        try
        {
            SetProcess(false); _helpOpen = true; _die.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            async Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _drawLayoutFrames = 0;
            }
            async Task Drain()
            {
                for (var i = 0; CardsInMotion && i < 40; i++)
                {
                    await Layout(); UpdateDrawAnimations(1);
                }
                Check(!CardsInMotion, "La secuencia no termina.");
            }
            // The same entry point as opening and restarting a real game.
            NewGame(); await Layout();
            Check(_drawQueue.Count == 10 && _drawQueue.Select(d => d.Player).SequenceEqual(Enumerable.Range(0, 10).Select(i => i % 2)),
                "Faltan las cinco parejas del reparto inicial.");
            Check(_drawQueue.All(d => DrawTarget(d)!.Modulate.A == 0), "Las manos aparecen antes del reparto.");
            UpdateDrawAnimations(0.05);
            Check(_flyingFront != null && !_flyingFront.Visible && _flyingBack!.Visible, "La primera carta no parte boca abajo.");
            Check(_activeDraw?.Player == 0 && _pairedDraw?.Player == 1 && _pairedDraw.Slot == _activeDraw.Slot &&
                _pairedFlyingCard!.Visible && _drawQueue.Count == 8 && _pairedFlyingCard.GetChildren().All(c => c is TextureRect),
                "El reparto no inicia ambos vuelos juntos o revela la carta rival.");
            var pairedPosition = _pairedFlyingCard!.Position;
            var elapsedDeal = _drawTime;
            Render(); await Layout();
            Check(_drawTime == elapsedDeal && DrawTarget(_pairedDraw!)!.Modulate.A == 0,
                "Renderizar reinicia el reparto simultáneo o revela su destino.");
            UpdateDrawAnimations(0.18);
            Check(_flyingFront!.Visible, "La carta inicial propia no se revela.");
            Check(_pairedFlyingCard!.Position.DistanceTo(pairedPosition) > 1, "La carta rival no avanza junto con la propia.");
            UpdateDrawAnimations(0.05);
            Check(_activeDraw == null && _pairedDraw == null && _pairedFlyingCard == null &&
                DrawTarget(new(0, 0))!.Modulate.A == 1 && DrawTarget(new(1, 0))!.Modulate.A == 1,
                "La pareja no llega simultáneamente o deja cartas invisibles.");
            for (var pair = 1; pair < 5; pair++) UpdateDrawAnimations(0.28);
            Check(!CardsInMotion, "El reparto requiere más de cinco tandas de 0,28 segundos.");
            await Layout();
            // Restarting during a simultaneous flight clears both cards.
            NewGame(); await Layout(); UpdateDrawAnimations(0.05);
            Check(_pairedFlyingCard != null, "No comienza el vuelo simultáneo al reiniciar.");
            NewGame(); await Layout();
            Check(_pairedFlyingCard == null && _pairedDraw == null && _drawQueue.Count == 10,
                "Reiniciar conserva una carta de la pareja anterior.");
            await Drain();

            var oldHand = _game.State.Players[0].Hand.ToArray();
            var firstRect = DrawTarget(new(0, 1))!.GetGlobalRect();
            Execute(_game.Start(new[] { 1, 3 })); await Layout();
            var sequence = _drawQueue.ToArray();
            Check(sequence[0].ReturnToDeck && sequence[0].Slot == 3 && sequence[0].CardId == oldHand[3] &&
                sequence[1].ReturnToDeck && sequence[1].Slot == 1 && sequence[1].CardId == oldHand[1] && sequence[2].Shuffle &&
                sequence[3].Slot == 3 && sequence[4].Slot == 4,
                "El mulligan no devuelve, baraja y roba en ese orden.");
            Check(sequence[1].Origin!.Value.Position.DistanceTo(firstRect.Position) < 1 && _returnHandCards.Count == 5,
                "No se conserva la mano original para las devoluciones.");
            UpdateDrawAnimations(0.1);
            Check(_flyingFront?.Card.Id == oldHand[3] && _flyingFront.Visible && _returnHandCards.Count == 4,
                "El mulligan no mueve la carta seleccionada desde la mano original.");
            var elapsed = _drawTime;
            Render(); await Layout();
            Check(_drawTime == elapsed && _returnHandStage != null, "Renderizar reinicia el regreso de cartas.");
            UpdateDrawAnimations(0.24);
            Check(!_flyingFront!.Visible && _flyingBack!.Visible, "La carta devuelta no se voltea al llegar al mazo.");
            UpdateDrawAnimations(1); UpdateDrawAnimations(1);
            Check(_returnHandStage == null && _returnHandCards.Count == 0, "La mano original persiste después de devolver las cartas.");
            UpdateDrawAnimations(0.15);
            Check(_activeDraw?.Shuffle == true && _shuffleCards.Count == 4 && _flyingCard == null,
                "No se reproduce el barajado antes de robar reemplazos.");
            var shufflePosition = _shuffleCards[0].Position;
            Render(); await Layout(); UpdateDrawAnimations(0.12);
            Check(_shuffleCards[0].Position.DistanceTo(shufflePosition) > 1, "El barajado no continúa después de renderizar.");
            await Drain();
            Check(_game.State.Players[0].Deck.TakeLast(2).SequenceEqual(new[] { oldHand[3], oldHand[1] }),
                "La animación cambia el orden real del mazo.");

            // Keeping the initial hand never returns or shuffles cards.
            BindGame(new GameEngine(42)); Render();
            Execute(_game.Start(Array.Empty<int>()));
            Check(!_drawQueue.Any(d => d.ReturnToDeck || d.Shuffle), "Conservar la mano anima un mulligan inexistente.");
            await Drain();

            BindGame(new GameEngine(42)); Render(); await Layout();
            Execute(_game.Start(new[] { 99 }));
            Check(!CardsInMotion && _returnHandStage == null, "Un mulligan rechazado anima cartas.");
            Execute(_game.Start(Enumerable.Range(0, 5))); await Layout();
            Check(_drawQueue.Take(5).All(d => d.ReturnToDeck) && _drawQueue.Count(d => d.Shuffle) == 1,
                "Cambiar toda la mano no devuelve cinco cartas antes de un único barajado.");
            await Drain();

            // Cards returned by Relikia use the same flight and deck animation.
            _game.State.CurrentPlayer = 0; _game.State.Phase = Phase.Actions; _game.State.ActionsLeft = 2;
            var p = _game.State.Active; p.Relikia = 1000;
            Execute(_game.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace)); await Drain();
            var returned = p.Hand[0];
            Execute(_game.ResolveRelikiaPlacement(0, 0, CardDestination.DeckBottom)); await Layout();
            Check(_drawQueue.Count == 2 && _drawQueue.Peek().ReturnToDeck && _drawQueue.Last().Shuffle && p.Deck[^1] == returned,
                "Devolver una carta por Relikia no anima el regreso y barajado.");
            await Drain();

            // Recycling returns memory cards to the deck, so it also mixes the stack.
            var rng = new SkillsTestRandom(); BindGame(new GameEngine(rng));
            _game.State.CurrentPlayer = 0; _game.State.Phase = Phase.Actions; _game.State.ActionsLeft = 2;
            p = _game.State.Active; p.Relikia = 1000; p.Deck.Clear(); p.Deck.Add("attack");
            p.Memory.Clear(); p.Memory.AddRange(Enumerable.Repeat("attack", 6));
            rng.Enqueue(4);
            Execute(_game.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace));
            if (_game.State.Phase == Phase.DieRerollChoice) Execute(_game.ResolveDieReroll(0, false));
            Check(_drawQueue.Any(d => d.Shuffle), "Reciclar cartas de memoria no anima el mazo.");
            await Drain();

            // Restarting during a shuffle replaces every old visual with a fresh deal.
            _drawQueue.Enqueue(new(0, -1, Shuffle: true)); Render(); await Layout(); UpdateDrawAnimations(0.1);
            NewGame(); await Layout();
            Check(_shuffleCards.Count == 0 && _returnHandStage == null && _activeDraw == null && _drawQueue.Count == 10,
                "Reiniciar conserva cartas o barajados de la partida anterior.");
            await Drain();
            GD.Print("OPENING_ANIMATION_PASS · reparto inicial, privacidad, mulligan, regreso, barajado, conservación del orden, Relikia y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

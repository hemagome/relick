using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void DrawSmokeTest()
    {
        try
        {
            _helpOpen = true;
            SetProcess(false);
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _drawLayoutFrames = 0;
            }
            BindGame(new GameEngine(42));
            _game.State.CurrentPlayer = 1; _game.State.Phase = Phase.Actions; _game.State.ActionsLeft = 2;
            var own = _game.State.Players[0];
            own.Hand.Clear();
            var before = own.Deck.Count;
            Execute(_game.Advance(1));
            await Layout();
            var deck = (DeckStack)_root.FindChild("DeckStack0", true, false);
            Check(deck.HasBackArtwork && deck.Amount == before - 3, "El mazo gráfico no refleja el robo real.");
            Check(_drawQueue.Count == 3 && DrawTarget(_drawQueue.Peek())!.Modulate.A == 0, "El robo múltiple no oculta las cartas antes de llegar.");
            UpdateDrawAnimations(0.2);
            Check(_flyingFront != null && !_flyingFront.Visible && _flyingBack!.Visible, "El robo propio no empieza boca abajo.");
            var progress = _drawTime;
            Render(); await Layout();
            Check(_drawTime == progress && _flyingCard != null, "Actualizar el tablero reinicia el robo.");
            UpdateDrawAnimations(0.3);
            Check(_flyingFront!.Visible && !_flyingBack!.Visible, "El robo propio no revela la carta al llegar.");
            var imageArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--draw-frame="));
            if (imageArg != null)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(imageArg.Split('=', 2)[1]);
            }
            UpdateDrawAnimations(0.2);
            Check(_drawQueue.Count == 2 && _activeDraw == null, "Las cartas no se roban en secuencia.");
            UpdateDrawAnimations(1);
            UpdateDrawAnimations(1);
            Check(!CardsInMotion && !_drawOverlay!.Visible && own.Hand.Count == 3, "El robo no termina o cambia las reglas.");
            Check(DrawTarget(new(0, 0))!.Modulate.A == 1 && DrawTarget(new(0, 1))!.Modulate.A == 1, "Quedan cartas invisibles al finalizar.");

            // A turn with an empty hand draws three hidden cards for the opponent.
            _game.State.Players[1].Hand.Clear();
            _game.State.CurrentPlayer = 0; _game.State.Phase = Phase.Actions;
            Execute(_game.Advance(0)); await Layout();
            Check(_drawQueue.Count == 3, "La mano vacía no encola tres robos.");
            UpdateDrawAnimations(0.5);
            Check(_flyingFront == null && _flyingBack!.Visible, "El robo del rival filtra su carta.");
            for (var i = 0; i < 3; i++) UpdateDrawAnimations(1);
            Check(!CardsInMotion && _game.State.Players[1].Hand.Count == 3, "El robo del rival no termina.");

            // Relikia also emits a real draw; restarting cancels its flight.
            _game.State.CurrentPlayer = 0; _game.State.Phase = Phase.Actions;
            _game.State.ActionsLeft = 2; own.Relikia = 1000;
            Execute(_game.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace)); await Layout();
            Check(CardsInMotion && _drawQueue.Count == 1, "Relikia no anima su robo.");
            UpdateDrawAnimations(0.2);
            NewGame(); await Layout();
            Check(!CardsInMotion && _drawOverlay == null && _flyingCard == null, "Reiniciar deja una animación antigua.");
            // Drawing the last card still arrives even when exhaustion ends the duel.
            _game.State.CurrentPlayer = 0; _game.State.Phase = Phase.Actions; _game.State.ActionsLeft = 2;
            own = _game.State.Players[0];
            own.Memory.Clear(); own.Deck.Clear(); own.Deck.Add("attack"); own.Relikia = 1000;
            Execute(_game.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace)); await Layout();
            deck = (DeckStack)_root.FindChild("DeckStack0", true, false);
            Check(deck.Amount == 0 && _game.State.IsOver && _drawQueue.Count == 1, "El mazo vacío no conserva el último robo.");
            UpdateDrawAnimations(1);
            Check(!CardsInMotion && DrawTarget(new(0, own.Hand.Count - 1))!.Modulate.A == 1, "El fin del duelo deja la última carta invisible.");
            NewGame(); await Layout();
            var minimum = _root.GetCombinedMinimumSize();
            Check(minimum.X <= Size.X - 48 && minimum.Y <= Size.Y - 30, $"El mazo desborda el tablero: {minimum}.");
            GD.Print("DRAW_SMOKE_PASS · mazos, robo múltiple, giro propio, rival oculto, Relikia, persistencia y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

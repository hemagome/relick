using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void SenshuniSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false); _helpOpen = true;
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            BindGame(new GameEngine(42));
            var s = _game.State; s.CurrentPlayer = 0; s.Phase = Phase.Actions; s.ActionsLeft = 2;
            s.Players[1].Hand.Clear(); // Test the resolved effect separately from Error de Carga.
            var p = s.Active; p.Hand.Clear(); p.Hand.Add("senshuni"); p.Hand.AddRange(Enumerable.Repeat("attack", 10)); Render();
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _drawLayoutFrames = 0;
            }
            async System.Threading.Tasks.Task FinishDraws()
            {
                await Layout();
                for (var i = 0; i < 20 && CardsInMotion; i++) UpdateDrawAnimations(1);
            }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            void Press(Node parent, string name)
            {
                var button = (Button)parent.FindChild(name, true, false);
                Check(!button.Disabled, $"Botón bloqueado: {name}"); button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            await Layout();
            var card = (HandCard)_root.FindChild("HandCard0", true, false);
            Check(card.HasCodeArtwork && card.Card.Text == "Roba 2 cartas.", "Senshuni no muestra su imagen limpia o su efecto real.");
            var before = p.Deck.Count;
            card.EmitSignal(BaseButton.SignalName.Pressed);
            Check(CanUseSelected() && SelectedVerb().Contains("1 acción"), "No se puede jugar Senshuni por una acción.");
            UseSelected();
            Check(p.Hand.Count == 12 && p.Deck.Count == before - 2 && p.Memory.Single() == "senshuni" && s.ActionsLeft == 1 && _drawQueue.Count == 2,
                $"Senshuni: fase={s.Phase}, mano={p.Hand.Count}, mazo={p.Deck.Count}/{before}, memoria={string.Join(",",p.Memory)}, acciones={s.ActionsLeft}, vuelos={_drawQueue.Count}.");
            // Drive actual process frames, rather than just draining queued events.
            SetProcess(true);
            for (var frame = 0; frame < 30 && _activeDraw == null; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(_activeDraw != null && _flyingCard!.IsVisibleInTree() && _drawTime > 0,
                $"Senshuni anim: activo={_activeDraw}, cola={_drawQueue.Count}, visible={_flyingCard?.IsVisibleInTree()}, tiempo={_drawTime}, layout={_drawLayoutFrames}, procesa={IsProcessing()}, overlay={_drawOverlay?.Visible}.");
            var firstPosition = _flyingCard!.Position;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(_flyingCard!.Position.DistanceTo(firstPosition) > 0.01f, "La animación de Senshuni no avanza entre fotogramas.");
            SetProcess(false);
            await FinishDraws();
            Check(!CardsInMotion && _root.FindChildren("HandCard*", "Button", true, false).OfType<HandCard>().Count() == 12, "La animación deja cartas invisibles o limita la mano.");
            _helpOpen = false; ShowMemory(0);
            Check(((HandCard)Dialog().FindChild("MemoryCard0", true, false)).HasCodeArtwork, "Senshuni pierde su imagen en memoria.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            p.Relikia = 400; s.ActionsLeft = 2; Render(); _helpOpen = false;
            ShowRelikiaEffects(); Press(Dialog(), "RelikiaReplayCode");
            Check(((HandCard)Dialog().FindChild("RelikiaMemory0_0", true, false)).Card.Id == "senshuni", "Relikia no reconoce Senshuni como código válido.");
            before = p.Hand.Count; Press(Dialog(), "RelikiaMemory0_0");
            Check(p.Hand.Count == before + 2 && p.CorruptedMemory.Single() == "senshuni" && p.Memory.Count == 0 && p.Relikia == 0 && s.ActionsLeft == 0,
                "Relikia no activa el robo ni corrompe Senshuni.");
            Check(_drawQueue.Count == 2, "Relikia no encola los dos robos de Senshuni.");
            await FinishDraws();

            // Pending responses must not draw early; passing resumes both flights.
            s.ActionsLeft = 2; s.Phase = Phase.Actions;
            p.Hand.Clear(); p.Hand.Add("senshuni");
            s.Players[1].Hand.Add("load-error");
            before = p.Deck.Count;
            Execute(_game.PlayCode(0, 0));
            Check(s.Phase == Phase.ErrorResponse && !CardsInMotion && p.Deck.Count == before,
                "Senshuni roba antes de resolver Error de Carga.");
            Execute(_game.PassErrorResponse(1));
            Check(_drawQueue.Count == 2 && p.Deck.Count == before - 2, "Pasar la respuesta no reanuda el robo animado.");
            await FinishDraws();
            s.ActionsLeft = 2; p.Hand.Clear(); p.Hand.Add("senshuni");
            Execute(_game.PlayCode(0, 0));
            Execute(_game.PlayCode(1, 0));
            Check(!CardsInMotion && p.Hand.Count == 0 && s.ActionsLeft == 2,
                "Un Senshuni anulado roba cartas o no devuelve la acción.");
            GD.Print("SENSHUNI_SMOKE_PASS · arte limpio, código por 1 acción, dos robos animados, mano sin límite, memoria y activación/corrupción por Relikia.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

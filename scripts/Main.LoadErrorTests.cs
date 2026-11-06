using Godot;
using Relick.Core;
namespace Relick;
public partial class Main
{
    private async void LoadErrorSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            void Fixture(bool counter)
            {
                BindGame(new GameEngine(rng)); var s = _game.State; s.CurrentPlayer = 1; s.Phase = Phase.Actions;
                foreach (var p in s.Players) { p.Hand.Clear(); p.Memory.Clear(); }
                s.Players[0].Hand.Add("load-error"); s.Players[0].Equipment = "lucky-pendant";
                s.Players[1].Hand.Add("blue-egg"); if (counter) s.Players[1].Hand.Add("load-error"); s.Players[1].Relikia = 200;
                _helpOpen = false; Render();
            }
            async System.Threading.Tasks.Task Layout() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            foreach (var counter in new[] { false, true })
            {
                Fixture(counter); await Layout();
                Check(((HandCard)_root.FindChild("HandCard0", true, false)).HasCodeArtwork, "Error de Carga no muestra arte limpio.");
                Execute(_game.PlayCode(1, 0)); ShowPendingSkillChoice();
                Check(_game.State.Phase == Phase.ErrorResponse && Dialog().Title.Contains("Error de Carga"), "No se abre la ventana de respuesta.");
                ((Button)Dialog().FindChild("LoadErrorResponse0", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
                if (counter)
                {
                    Check(PracticeBot.Step(_game, 1).Success && _game.State.Phase == Phase.DestroyChoice, "El rival no puede contraresponder.");
                    Check(PracticeBot.Step(_game, 1).Success && _game.State.Players[0].Equipment == null && _game.State.Players[0].ActionsLeft == 2 && _game.State.Players[1].ActionsLeft == 0,
                        "La doble respuesta no aplica el ejemplo 0/2 acciones.");
                }
                else Check(_game.State.Players[0].Equipment == "lucky-pendant" && _game.State.Players[1].ActionsLeft == 2 && _game.State.Players[1].Relikia == 200 && _game.State.Players[0].ActionsLeft == 1,
                    "Anular el Huevo no conserva objetivo y devuelve todos los costes.");
            }
            Fixture(false); var enemy = _game.State.Players[1]; enemy.Hand.Clear(); enemy.Hand.Add("extra-life"); enemy.Relikario = RelikarioCatalog.YunoLv2; enemy.Health = 1;
            Execute(_game.PlayCode(1, 0)); ShowPendingSkillChoice(); rng.Enqueue(2);
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            Check(_game.State.Phase == Phase.DieRerollChoice, "No se conserva el código en resolución tras pasar.");
            var interrupt = (Button?)_root.FindChild("InterruptWithLoadError", true, false);
            Check(interrupt != null && !interrupt.Disabled, "No se permite responder mientras se resuelve un dado de código.");
            interrupt!.EmitSignal(BaseButton.SignalName.Pressed); await Layout();
            Check(enemy.Health == 1 && enemy.ActionsLeft == 2 && _game.State.PendingDieRoll == null, "Anular durante resolución no evita curación o no devuelve acción.");
            DrainTestDice(); NewGame(); await Layout();
            Check(_game.State.ActivationChain.Count == 0 && _game.State.Players.All(p => p.ActionsLeft == 2), "Reiniciar conserva la cadena.");
            GD.Print("LOAD_ERROR_SMOKE_PASS · arte limpio, respuesta al Huevo, devolución PR/acciones, contraerror 0/2, interrupción al resolver y reinicio."); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

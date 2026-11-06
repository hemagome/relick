using Godot;
using Relick.Core;
namespace Relick;
public partial class Main
{
    private async void PointsSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom(); BindGame(new GameEngine(rng));
            var s = _game.State; s.Phase = Phase.Actions; s.CurrentPlayer = 0; s.ActionsLeft = 2;
            s.Active.Hand.Clear(); s.Active.Hand.Add(CardCatalog.PointsIncrease.Id); s.Players[1].Relikia = 200;
            s.Players[1].Equipment = CardCatalog.CardboardBox.Id; _helpOpen = false; Render();
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            await Layout(); var card = (HandCard)_root.FindChild("HandCard0", true, false);
            Check(card.HasCodeArtwork && BoardArt.CardType(card.Card).Contains("JUGADA"), "No se muestra el arte o subtipo jugada.");
            card.EmitSignal(BaseButton.SignalName.Pressed); rng.Enqueue(6); UseSelected();
            Check(_die.IsRolling && s.Phase == Phase.PointsChoice && s.PendingPoints!.Amount == 1500 && s.ActionsLeft == 1 && s.Players[1].Equipment != null, "La tirada o el coste son incorrectos, o la Caja cancela una jugada.");
            DrainTestDice(); DrainTestCards(); ShowPendingSkillChoice();
            Check(Dialog().FindChildren("Points*", "Button", true, false).Count == 4 && ((Button)_root.FindChild("AdvanceStageButton", true, false)).Disabled, "Faltan opciones o se permite saltar el efecto.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            Check(s.PendingPoints != null && s.Players[1].Relikia == 200, "Cerrar la elección aplica un efecto implícito.");
            DrainTestCards(); ShowPendingSkillChoice(); ((Button)Dialog().FindChild("Points1Decrease", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
            Check(s.Players[1].Relikia == 0 && s.Active.Relikia == 0 && s.Phase == Phase.Actions && s.PendingPoints == null && s.Active.Memory.Single() == CardCatalog.PointsIncrease.Id, "La elección no aplica la reducción al jugador correcto.");
            NewGame(); await Layout(); Check(_game.State.PendingPoints == null, "Reiniciar conserva una elección pendiente.");
            GD.Print("POINTS_SMOKE_PASS · arte limpio, jugada, dado, cuatro opciones, elección obligatoria, mínimo cero, Caja excluida y reinicio."); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

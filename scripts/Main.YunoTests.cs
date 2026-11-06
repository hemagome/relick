using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void YunoSmokeTest()
    {
        try
        {
            SetProcess(false); _helpOpen = true;
            _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            void Fixture(int actor = 0, string? skill = null)
            {
                BindGame(new GameEngine(rng));
                var s = _game.State; s.Phase = Phase.Combat; s.CurrentPlayer = actor;
                foreach (var p in s.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Memory.Clear(); }
                s.Active.Hand.Add("attack"); s.Active.Skills.Add(skill ?? CardCatalog.Destiny.Id);
                _helpOpen = false; Render();
            }
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            Fixture(); await Layout();
            Check(_game.State.Players.All(p => p.Health == 8 && p.MaxHealth == 8) && _lifeMeters.All(m => m.Health == 8), "La partida o los medidores no empiezan en 8 vidas.");
            var name = (Label)_root.FindChild("RelikarioName0", true, false);
            Check(name.TooltipText.Contains("Vida máxima: 8") && name.TooltipText.Contains("Una vez por turno"), "No se puede consultar la habilidad de Yuno.");
            rng.Enqueue(3); Execute(_game.Attack(0, 0, 0));
            DrainTestCards(); ShowPendingSkillChoice();
            Check(!_helpOpen, "La elección aparece antes de terminar la tirada.");
            DrainTestDice(); DrainTestCards(); ShowPendingSkillChoice();
            Check(Dialog().Title.Contains("Yuno") && _game.State.Players[1].Health == 8, "Yuno no permite decidir antes del daño.");
            Check(((Button)_root.FindChild("AdvanceStageButton", true, false)).Disabled, "Se permite avanzar con la tirada pendiente.");
            rng.Enqueue(6);
            ((Button)Dialog().FindChild("YunoReroll", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            Check(_die.IsRolling && _die.Value == 6 && _die.RollCaption.Contains("6 se convierte en 5"), "El relanzamiento no muestra el dado real y la conversión.");
            Check(_game.State.LastAttackDie == 5 && _game.State.Players[1].Health == 5 && _game.State.Active.AttackRerollUsedThisTurn, "El relanzamiento de 6 no se resuelve como 5.");
            DrainTestDice(); await Layout();

            Fixture(skill: CardCatalog.Magnet.Id);
            _game.State.Players[1].Hand.Add("lucky-pendant");
            rng.Enqueue(6); Execute(_game.Attack(0, 0, 0)); DrainTestDice(); DrainTestCards(); ShowPendingSkillChoice();
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            Check(_game.State.Phase == Phase.MagnetChoice && _game.State.LastAttackDie == 6 && !_game.State.Active.AttackRerollUsedThisTurn, "Conservar un 6 pierde el crítico original.");
            DrainTestCards(); ShowPendingSkillChoice(); Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();

            Fixture(); rng.Enqueue(1); Execute(_game.Attack(0, 0, 0)); DrainTestDice(); DrainTestCards(); ShowPendingSkillChoice();
            Check(_game.State.Phase == Phase.Combat && !_helpOpen && _game.State.Players[1].Health == 8, "Un 1 ofrece relanzamiento.");
            Fixture(1); rng.Enqueue(3); Execute(_game.Attack(1, 0, 0)); DrainTestDice();
            rng.Enqueue(6); Check(PracticeBot.Step(_game, 1).Success, "El rival no sabe decidir el relanzamiento.");
            Check(_game.State.LastAttackDie == 5 && _game.State.Players[0].Health == 5, "Yuno no funciona para el rival.");
            NewGame(); await Layout();
            Check(_game.State.Players.All(p => p.Health == 8 && p.MaxHealth == 8 && !p.AttackRerollUsedThisTurn) && _game.State.PendingAttackRoll == null && !_die.IsBusy && _die.RollCaption.Length == 0, "Reiniciar conserva el relanzamiento o no restaura las 8 vidas.");
            Check(_root.GetCombinedMinimumSize().Y <= Size.Y - 30, "La información del Relikario desborda la ventana.");
            GD.Print("YUNO_SMOKE_PASS · 8 vidas, habilidad consultable, elección tras dado, 1 bloqueado, relanzamiento 6→5, conservar crítico, rival y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

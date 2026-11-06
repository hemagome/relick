using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void EvolutionSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            BindGame(new GameEngine(rng));
            var s = _game.State; s.Phase = Phase.Combat; s.CurrentPlayer = 0;
            foreach (var player in s.Players) { player.Hand.Clear(); player.Skills.Clear(); player.Memory.Clear(); }
            var p = s.Active; p.Hand.Add("attack"); p.Skills.Add(CardCatalog.Destiny.Id); p.Relikia = 1800;
            _helpOpen = false; Render();
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Button EvolveButton() => (Button)_root.FindChild("EvolveRelikario", true, false);
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            void Reroll(int value)
            {
                rng.Enqueue(value);
                ((Button)Dialog().FindChild("YunoReroll", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
                DrainTestDice();
            }
            await Layout();
            Check(EvolveButton().Disabled && EvolveButton().TooltipText.Contains("pendiente"), "Se ofrece evolucionar sin 6 natural.");
            rng.Enqueue(6); Execute(_game.Attack(0, 0, 0)); KeepTestAttackRoll(); DrainTestDice(); await Layout();
            Check(p.HasNaturalSix && !EvolveButton().Disabled, "El 6 natural no habilita la evolución.");
            p.Health = 8; p.Relikia = 2000; Render(); EvolveButton().EmitSignal(BaseButton.SignalName.Pressed); await Layout();
            Check(p.Level == 2 && p.Health == 10 && p.MaxHealth == 10 && p.Relikia == 200, "La evolución no paga PR o no añade las dos vidas.");
            var name = (Label)_root.FindChild("RelikarioName0", true, false);
            Check(name.Text.Contains("Lv 2") && name.TooltipText.Contains("2 veces") && EvolveButton().Disabled && _lifeMeters[0].Health == 10,
                "El tablero no actualiza el nivel, las vidas o la habilidad.");
            p.AttackRerollUsedThisTurn = false; s.AttackedThisTurn = false; p.Hand.Add("attack");
            // The prior critical left the opponent at 3; restore health to inspect all outcomes.
            s.Players[1].Health = 8;
            rng.Enqueue(2); Execute(_game.Attack(0, 0, 0)); DrainTestDice(); DrainTestCards(); ShowPendingSkillChoice();
            Check(Dialog().Title.Contains("Lv 2"), "No se muestra la nueva habilidad.");
            Reroll(3); await Layout(); DrainTestCards(); ShowPendingSkillChoice();
            Check(s.Phase == Phase.AttackRerollChoice && s.Players[1].Health == 8, "El primer relanzamiento resuelve el ataque antes de elegir el segundo.");
            Reroll(6); await Layout();
            Check(s.LastAttackDie == 6 && s.Players[1].Health == 3 && !_die.RollCaption.Contains("convierte"), "Lv 2 sigue convirtiendo 6 en 5.");
            Check(s.PendingAttackRoll == null && !GetChildren().OfType<AcceptDialog>().Any(d => !d.IsQueuedForDeletion()), "Queda una tercera elección abierta.");

            // A different kind of die uses the same Lv 2 choice and stops on 1.
            s.Phase = Phase.Actions; s.ActionsLeft = 2; p.Relikia = 1000; p.AttackRerollUsedThisTurn = false; p.Bonus = "lucky-pendant";
            rng.Enqueue(3); Execute(_game.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus)); DrainTestDice(); await Layout(); DrainTestCards(); ShowPendingSkillChoice();
            Check(s.Phase == Phase.DieRerollChoice && Dialog().Title.Contains("Lv 2"), "No se ofrece relanzar el dado de Relikia.");
            Reroll(1); await Layout();
            Check(s.Phase == Phase.Actions && s.PendingDieRoll == null && p.AttackRerollUsedThisTurn, "Un 1 no detiene la cadena de relanzamientos.");
            NewGame(); await Layout();
            Check(_game.State.Players.All(player => player.Level == 1 && player.MaxHealth == 8 && player.Health == 8 && !player.HasNaturalSix), "Reiniciar conserva evolución o condición.");
            var minimum = _root.GetCombinedMinimumSize();
            Check(minimum.X <= Size.X - 48 && minimum.Y <= Size.Y - 30, $"La evolución desborda el tablero: {minimum}.");
            GD.Print("EVOLUTION_SMOKE_PASS · condición persistente, 1800 PR, Lv 2 visible, máximo 10, dos relanzamientos, crítico real, otros dados, parada en 1 y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

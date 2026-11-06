using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void AttackTimingSmokeTest()
    {
        try
        {
            SetProcess(false); _helpOpen = true;
            _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            var attacks = 0;
            void Fixture(int actor = 0, bool level2 = false)
            {
                BindGame(new GameEngine(rng)); attacks = 0;
                _game.AttackPerformed += _ => attacks++;
                _game.State.CurrentPlayer = actor; _game.State.Phase = Phase.Combat;
                foreach (var p in _game.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Memory.Clear(); p.Equipment = null; }
                _game.State.Active.Hand.Add("attack"); _game.State.Active.Skills.Add("destiny-shuffle");
                if (level2) _game.State.Active.Relikario = RelikarioCatalog.YunoLv2;
                _yuno.Play(PortraitMotion.Idle); _enemyYuno.Play(PortraitMotion.Idle);
                Render();
            }
            void Start(int actor, int die)
            {
                rng.Enqueue(die); Execute(_game.Attack(actor, 0, 0)); PlayPendingCombatAnimations();
                Check(attacks == 0 && PortraitFor(actor).Motion == PortraitMotion.Idle,
                    "Declarar el ataque inicia la animación antes de conocer el resultado final.");
            }
            foreach (var actor in new[] { 0, 1 })
            foreach (var die in Enumerable.Range(1, 6))
            {
                Fixture(actor); Start(actor, die); DrainTestDice();
                Check(attacks == 0 && PortraitFor(actor).Motion == PortraitMotion.Idle,
                    "La tirada inicial anima antes de decidir si relanza.");
                if (_game.State.Phase == Phase.AttackRerollChoice) Execute(_game.ResolveAttackReroll(actor, false));
                PlayPendingCombatAnimations();
                Check(attacks == (die >= 4 ? 1 : 0) && PortraitFor(actor).Motion == (die >= 4 ? PortraitMotion.Attack : PortraitMotion.Idle),
                    $"Resultado final {die}: número de animaciones incorrecto para jugador {actor}.");
                if (die >= 4)
                {
                    PortraitFor(actor).AdvancePlayback(0.2);
                    var frame = PortraitFor(actor).CurrentFrame;
                    Render(); PlayPendingCombatAnimations();
                    Check(PortraitFor(actor).CurrentFrame == frame && attacks == 1, "La animación se repite al actualizar.");
                }
            }

            // A successful original roll rerolled into a miss must never animate.
            Fixture(); Start(0, 5); DrainTestDice();
            rng.Enqueue(2); Execute(_game.ResolveAttackReroll(0, true)); DrainTestDice();
            Check(attacks == 0 && _yuno.Motion == PortraitMotion.Idle, "Un 5 convertido en fallo conserva la animación del ataque.");

            // A miss rerolled into a hit waits for the visible die to land.
            Fixture(); Start(0, 2); DrainTestDice();
            rng.Enqueue(6); Execute(_game.ResolveAttackReroll(0, true));
            Check(attacks == 1 && _game.State.LastAttackDie == 5, "Yuno Lv 1 no confirma un único ataque exitoso.");
            _die.AdvancePlayback(DiceFace.RollSeconds - 0.01); PlayPendingCombatAnimations();
            Check(_yuno.Motion == PortraitMotion.Idle && _enemyYuno.Motion == PortraitMotion.Idle, "Ataque o daño se reproduce con el dado en movimiento.");
            _die.AdvancePlayback(0.02); PlayPendingCombatAnimations();
            Check(_yuno.Motion == PortraitMotion.Attack && _enemyYuno.Motion == PortraitMotion.Hit,
                "Ataque y reacción no comienzan al terminar el dado definitivo.");

            // Two level-2 rerolls still produce one animation after the final decision.
            Fixture(level2: true); Start(0, 5); DrainTestDice();
            rng.Enqueue(4); Execute(_game.ResolveAttackReroll(0, true)); DrainTestDice();
            Check(attacks == 0 && _yuno.Motion == PortraitMotion.Idle, "El primer relanzamiento Lv 2 anima sin confirmar.");
            rng.Enqueue(6); Execute(_game.ResolveAttackReroll(0, true)); PlayPendingCombatAnimations();
            Check(_yuno.Motion == PortraitMotion.Idle, "El segundo relanzamiento anima antes de terminar el dado.");
            DrainTestDice();
            Check(attacks == 1 && _yuno.Motion == PortraitMotion.Attack, "Dos relanzamientos no producen exactamente una animación.");

            Fixture(level2: true); Start(0, 6); DrainTestDice();
            rng.Enqueue(1); Execute(_game.ResolveAttackReroll(0, true)); DrainTestDice();
            Check(attacks == 0 && _yuno.Motion == PortraitMotion.Idle, "Un relanzamiento final de 1 anima el ataque.");

            // Combo rerolls also wait for the final choice; canceling damage does not cancel a successful attack.
            Fixture(); _game.State.Active.Hand.Add("reroll"); Start(0, 2); DrainTestDice();
            rng.Enqueue(4); Execute(_game.PlayCode(0, 0)); DrainTestDice();
            Check(attacks == 0, "Re-lanza anima antes de conservar el dado.");
            Execute(_game.ResolveAttackReroll(0, false)); PlayPendingCombatAnimations();
            Check(attacks == 1 && _yuno.Motion == PortraitMotion.Attack, "Re-lanza no anima el resultado final exitoso.");

            Fixture(); _game.State.Players[1].Equipment = "cardboard-box";
            Start(0, 5); DrainTestDice(); Execute(_game.ResolveAttackReroll(0, false));
            Execute(_game.CancelAttackWithBox(1)); PlayPendingCombatAnimations();
            Check(attacks == 1 && _yuno.Motion == PortraitMotion.Attack && _enemyYuno.Motion == PortraitMotion.Idle,
                "Cancelar daño cancela la animación exitosa o reproduce daño inexistente.");

            Fixture(); Start(0, 2); DrainTestDice(); rng.Enqueue(5); Execute(_game.ResolveAttackReroll(0, true));
            NewGame();
            Check(_pendingCombatAnimations.Count == 0 && _yuno.Motion == PortraitMotion.Idle, "Reiniciar conserva un ataque pendiente.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("ATTACK_TIMING_PASS · resultados 1–6, ambos jugadores, dado completo, Yuno Lv 1/Lv 2, combos, daño cancelado, una sola reproducción y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

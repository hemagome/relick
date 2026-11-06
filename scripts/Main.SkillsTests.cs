using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void SkillsSmokeTest()
    {
        try
        {
            SetProcess(false); _helpOpen = true;
            _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            void Fixture(int actor, string skill)
            {
                BindGame(new GameEngine(rng));
                var s = _game.State; s.Phase = Phase.Combat; s.CurrentPlayer = actor;
                foreach (var p in s.Players) { p.Hand.Clear(); p.Skills.Clear(); p.SkillCounters.Clear(); p.Memory.Clear(); }
                s.Active.Hand.Add("attack"); s.Active.Skills.Add(skill);
                Render();
            }
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            void Press(string name)
            {
                var button = (Button)Dialog().FindChild(name, true, false);
                Check(!button.Disabled, $"Control bloqueado: {name}"); button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            Fixture(1, CardCatalog.Destiny.Id);
            var defender = _game.State.Players[0]; defender.Skills.AddRange(new[] { CardCatalog.Destiny.Id, CardCatalog.Destiny.Id });
            defender.Hand.AddRange(Enumerable.Repeat("attack", 5));
            rng.Enqueue(5); Execute(_game.Attack(1, 0, 0)); KeepTestAttackRoll(); _die.AdvancePlayback(3); await Layout();
            var tile = (SkillTile)_root.FindChild("Skill0_0", true, false);
            Check(tile.HasSkillArtwork, "La habilidad enseñada no usa el arte limpio.");
            _helpOpen = false; DrainTestCards(); ShowPendingSkillChoice();
            Check(Dialog().Title.Contains("Destiny"), "No se abre la respuesta del defensor.");
            for (var i = 0; i < 3; i++) Press($"DefenseDiscard{i}");
            Press("ConfirmDestiny"); await Layout();
            Check(defender.Health == 8 && defender.CountersOnSkill(0) == 1 && defender.CountersOnSkill(1) == 0,
                "Cancelar daño no conserva contadores independientes.");
            tile = (SkillTile)_root.FindChild("Skill0_0", true, false);
            Check(tile.Counters == 1 && tile.TooltipText.Contains("1/2"), "El contador no aparece en el tablero.");
            defender.Hand.Clear(); defender.Hand.AddRange(Enumerable.Repeat("attack", 5));
            _game.State.AttackedThisTurn = false; _game.State.Active.Hand.Add("attack");
            rng.Enqueue(6); Execute(_game.Attack(1, 0, 0)); KeepTestAttackRoll(); _die.AdvancePlayback(3);
            DrainTestCards(); ShowPendingSkillChoice(); Press("DefendWithSlot1");
            for (var i = 0; i < 4; i++) Press($"DefenseDiscard{i}");
            Press("ConfirmDestiny"); await Layout();
            Check(defender.CountersOnSkill(0) == 1 && defender.CountersOnSkill(1) == 1 && defender.Hand.Count == 1,
                "La respuesta crítica no descarta cuatro cartas con la copia elegida.");

            Fixture(0, CardCatalog.Magnet.Id);
            var rival = _game.State.Players[1]; rival.Hand.AddRange(new[] { "lucky-pendant", CardCatalog.Destiny.Id });
            rng.Enqueue(6); Execute(_game.Attack(0, 0, 0)); KeepTestAttackRoll(); _die.AdvancePlayback(3); await Layout();
            DrainTestCards(); ShowPendingSkillChoice();
            var shown = (HandCard)Dialog().FindChild("MagnetTakeHand1", true, false);
            Check(shown.HasSkillArtwork && shown.Card.Id == CardCatalog.Destiny.Id, "Magnet no permite mirar la mano rival con el arte correcto.");
            Press("MagnetTakeHand0"); await Layout();
            Check(_game.State.Active.Hand.Single() == "lucky-pendant" && rival.Hand.Single() == CardCatalog.Destiny.Id, "La elección de Magnet no transfiere la carta correcta.");
            var remaining = (OpponentHandCard)_root.FindChild("OpponentHandCard0", true, false);
            Check(remaining.IsFaceDown && remaining.PublicCard == null, "La mano rival queda revelada tras la elección.");

            Fixture(0, CardCatalog.Magnet.Id);
            rng.Enqueue(5); rng.Enqueue(4); Execute(_game.Attack(0, 0, 0)); KeepTestAttackRoll();
            Check(_die.Value == 5 && _die.HasPendingRolls, "Magnet no conserva la tirada de acierto antes del dado de daño.");
            _die.AdvancePlayback(3); _die.AdvancePlayback(0.1);
            Check(_die.Value == 4 && _die.IsRolling, "No se anima la segunda tirada de Magnet.");
            _die.AdvancePlayback(3);
            NewGame(); await Layout();
            Check(!_die.HasPendingRolls && _game.State.Players.All(p => p.SkillCounters.Count == 0), "Reiniciar conserva dados o contadores.");
            Check(_root.GetCombinedMinimumSize().X <= Size.X - 48 && _root.GetCombinedMinimumSize().Y <= Size.Y - 30, "Las nuevas habilidades desbordan el tablero.");
            GD.Print("SKILLS_SMOKE_PASS · arte limpio, copias repetidas, defensa normal/crítica, contadores, elección Magnet, privacidad, dos dados y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void KeepTestAttackRoll()
    {
        for (var i = 0; i < 30; i++)
        {
            if (_game.State.Phase == Phase.AttackRerollChoice) Execute(_game.ResolveAttackReroll(_game.State.DecisionPlayer, false));
            else if (_game.State.Phase == Phase.DieRerollChoice) Execute(_game.ResolveDieReroll(_game.State.DecisionPlayer, false));
            else break;
        }
    }

    private void DrainTestDice()
    {
        for (var i = 0; i < 100 && (_die.IsBusy || _die.IsPresented); i++) _die.AdvancePlayback(3);
        DrainTestCards();
        PlayPendingCombatAnimations();
    }

    private void DrainTestCards()
    {
        if (CardsInMotion && _drawOverlay == null) Render();
        for (var i = 0; CardsInMotion && i < 100; i++)
        {
            _drawLayoutFrames = 0;
            UpdateDrawAnimations(1);
        }
    }

    private sealed class SkillsTestRandom : IGameRandom
    {
        private readonly Queue<int> _values = new();
        public void Enqueue(int value) => _values.Enqueue(value - 1);
        public int Next(int max) => _values.Count > 0 ? _values.Dequeue() % max : max - 1;
    }
}

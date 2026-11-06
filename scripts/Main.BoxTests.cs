using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void BoxSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            void Fixture(int actor)
            {
                BindGame(new GameEngine(rng));
                var s = _game.State; s.CurrentPlayer = 0; s.Phase = Phase.Actions; s.ActionsLeft = 2;
                foreach (var p in s.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
                s.Players[0].Hand.Add(CardCatalog.CardboardBox.Id);
                Check(_game.Equip(0, 0).Success, "No se puede equipar Caja.");
                s.CurrentPlayer = actor; s.Phase = Phase.Combat;
                s.Players[actor].Hand.Add("attack"); s.Players[actor].Skills.Add(CardCatalog.Destiny.Id);
                _helpOpen = false; Render();
            }
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            void Attack(int actor, int die)
            {
                rng.Enqueue(die); Execute(_game.Attack(actor, 0, 0)); KeepTestAttackRoll(); DrainTestDice();
            }
            Fixture(1); await Layout();
            ShowEquipment(0);
            Check(((HandCard)Dialog().FindChild("ZoneCardPreview", true, false)).HasEquipmentArtwork, "La Caja no muestra su arte limpio.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            Attack(1, 5); await Layout(); DrainTestCards(); ShowPendingSkillChoice();
            Check(_game.State.Players[0].Health == 8 && Dialog().FindChild("BoxCancelAttack", true, false) != null && Dialog().FindChild("ConfirmDestiny", true, false) == null,
                "La Caja sola no ofrece defensa antes de recibir daño.");
            ((Button)Dialog().FindChild("BoxCancelAttack", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
            Check(_game.State.Players[0].Health == 8 && _game.State.Players[0].Equipment == null && _game.State.Players[0].Memory.Single() == CardCatalog.CardboardBox.Id,
                "Cancelar no destruye la Caja o se aplica el daño.");
            Check(((ZoneCardButton)_root.FindChild("EquipmentZone0", true, false)).Card == null, "El equipo destruido permanece visible.");

            Fixture(1); var own = _game.State.Players[0]; own.Skills.Add(CardCatalog.Destiny.Id); own.Hand.AddRange(Enumerable.Repeat("attack", 3));
            Attack(1, 5); await Layout(); DrainTestCards(); ShowPendingSkillChoice();
            Check(Dialog().FindChild("BoxCancelAttack", true, false) != null && Dialog().FindChild("ConfirmDestiny", true, false) != null, "No se ofrecen Caja y Destiny como alternativas.");
            for (var i = 0; i < 3; i++) ((Button)Dialog().FindChild($"DefenseDiscard{i}", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            ((Button)Dialog().FindChild("ConfirmDestiny", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
            Check(own.Health == 8 && own.Equipment == CardCatalog.CardboardBox.Id && own.CountersOnSkill(0) == 1, "Usar Destiny destruye la Caja por error.");

            // Real object codes can be canceled; character codes cannot.
            Fixture(1); var enemy = _game.State.Players[1];
            _game.State.Phase = Phase.Actions; _game.State.ActionsLeft = 2;
            enemy.Hand.Clear(); enemy.Hand.Add("senshuni"); var before = enemy.Deck.Count;
            Execute(_game.PlayCode(1, 0));
            Check(_game.State.Phase == Phase.Actions && enemy.Hand.Count == 2 && _game.State.Players[0].Equipment != null,
                "La Caja cancela Senshuni pese a ser código de personaje.");
            Fixture(1); enemy = _game.State.Players[1];
            _game.State.Phase = Phase.Actions; _game.State.ActionsLeft = 2; enemy.Hand.Clear(); enemy.Hand.Add(CardCatalog.ExtraLife.Id);
            before = enemy.Deck.Count; Execute(_game.PlayCode(1, 0)); await Layout(); DrainTestCards(); ShowPendingSkillChoice();
            Check(_game.State.Phase == Phase.CodeResponse && enemy.Hand.Count == 0 && enemy.Deck.Count == before && ((Button)_root.FindChild("AdvanceStageButton", true, false)).Disabled,
                "El código se resuelve antes de decidir su cancelación.");
            ((Button)Dialog().FindChild("BoxCancelCode", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
            Check(enemy.Hand.Count == 0 && enemy.Deck.Count == before && enemy.Memory.Single() == CardCatalog.ExtraLife.Id && _game.State.Players[0].Equipment == null && _game.State.PendingCode == null,
                "La cancelación no conserva el código en memoria o llega a ejecutar su efecto.");
            NewGame(); await Layout();
            Check(CardCatalog.Senshuni.CodeType == CodeType.Character && CardCatalog.Get("senshuni").CodeType == CodeType.Character && _game.State.Players.All(p => p.Equipment == null), "La prueba deja cambios de subtipo o equipo inicial.");
            GD.Print("BOX_SMOKE_PASS · imagen limpia, defensa sin Destiny, alternativas, cancelar objeto antes de efecto, Senshuni excluida y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void ExtraLifeSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom();
            void Fixture()
            {
                BindGame(new GameEngine(rng));
                var s = _game.State; s.Phase = Phase.Actions; s.CurrentPlayer = 0; s.ActionsLeft = 2;
                s.Active.Hand.Clear(); s.Active.Hand.Add(CardCatalog.ExtraLife.Id); s.Active.Health = 3;
                _lifeMeters[0].SetHealth(3, 8, false); _helpOpen = false; Render();
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
                Check(!button.Disabled, $"Botón bloqueado: {name}"); button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            Fixture(); await Layout();
            var hand = (HandCard)_root.FindChild("HandCard0", true, false);
            Check(hand.HasCodeArtwork && hand.Card.CodeType == CodeType.Object && BoardArt.CardType(hand.Card).Contains("OBJETO"), "Vida Extra no muestra su imagen o subtipo correctos.");
            hand.EmitSignal(BaseButton.SignalName.Pressed); Check(CanUseSelected(), "No se puede jugar Vida Extra.");
            rng.Enqueue(6); UseSelected();
            Check(_die.IsRolling && _die.Value == 6 && _die.RollCaption.Contains("Vida Extra") && _game.State.Players[0].Health == 7 && _game.State.ActionsLeft == 1,
                "La carta no anima el dado ni aplica la curación correcta.");
            Check(_lifeMeters[0].Health == 7 && _lifeMeters[0].IsAnimating, "La curación no anima la barra de vida.");
            DrainTestDice(); ShowMemory(0);
            Check(((HandCard)Dialog().FindChild("MemoryCard0", true, false)).HasCodeArtwork, "Vida Extra pierde su imagen en memoria.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            var p = _game.State.Active; p.Health = 1; p.Relikia = 400; _game.State.ActionsLeft = 2; Render();
            ShowRelikiaEffects(); Press("RelikiaReplayCode"); rng.Enqueue(2); Press("RelikiaMemory0_0");
            Check(p.Health == 3 && p.CorruptedMemory.Single() == CardCatalog.ExtraLife.Id && p.Relikia == 0 && _game.State.ActionsLeft == 0,
                "Activar Vida Extra por Relikia no cura o no la corrompe.");
            DrainTestDice(); await Layout();

            Fixture(); p = _game.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Health = 1; Render();
            rng.Enqueue(2); Execute(_game.PlayCode(0, 0)); DrainTestDice(); DrainTestCards(); ShowPendingSkillChoice();
            Check(_game.State.PendingDieRoll!.Purpose == DiePurpose.ExtraLife && Dialog().FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text.Contains("VIDA EXTRA")),
                "El relanzamiento no identifica el dado de curación.");
            rng.Enqueue(6); Press("YunoReroll"); DrainTestDice(); await Layout(); DrainTestCards(); ShowPendingSkillChoice();
            Check(p.Health == 1, "La curación se aplica antes de conservar el resultado final.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); await Layout();
            Check(p.Health == 5 && !p.HasNaturalSix && _game.State.PendingDieRoll == null, "El 6 relanzado no cura cuatro vidas o se considera natural.");

            Fixture(); p = _game.State.Active; _game.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Execute(_game.PlayCode(0, 0));
            Check(_game.State.Phase == Phase.CodeResponse && !_die.IsBusy && p.Health == 3, "Vida Extra lanza o cura antes de la respuesta de Caja.");
            Check(PracticeBot.Step(_game, 1).Success && p.Health == 3 && !_die.IsBusy && _game.State.Players[1].Equipment == null,
                "La Caja del rival no cancela Vida Extra.");
            NewGame(); await Layout();
            Check(_game.State.Players.All(player => player.Health == 8 && player.MaxHealth == 8), "La prueba conserva la curación al reiniciar.");
            GD.Print("EXTRA_LIFE_SMOKE_PASS · imagen limpia, objeto, dado, curación y barra animada, Relikia, Yuno Lv 2, Caja antes de lanzar y reinicio.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

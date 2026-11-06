using Godot;
using Relick.Core;
namespace Relick;
public partial class Main
{
    private async void ComboSmokeTest()
    {
        try
        {
            SetProcess(false); _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            var rng = new SkillsTestRandom(); BindGame(new GameEngine(rng)); var s = _game.State;
            s.CurrentPlayer = 0; s.Phase = Phase.Actions; s.ActionsLeft = 2;
            var p = s.Active; p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 199; s.Players[1].Equipment = "lucky-pendant";
            _helpOpen = false; Render();
            async System.Threading.Tasks.Task Layout() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            await Layout(); var egg = (HandCard)_root.FindChild("HandCard0", true, false); Check(egg.HasCodeArtwork, "Falta imagen limpia del Huevo.");
            egg.EmitSignal(BaseButton.SignalName.Pressed); Check(!CanUseSelected(), "El Huevo se permite sin 200 PR.");
            p.Relikia = 200; Check(CanUseSelected(), "No se habilita el Huevo con coste y objetivo."); UseSelected(); ShowPendingSkillChoice();
            Check(s.Phase == Phase.DestroyChoice && p.Relikia == 0 && s.ActionsLeft == 1, "El Huevo no paga el coste o no abre objetivos.");
            ((Button)Dialog().FindChild("Destroy1Equipment", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Layout();
            Check(s.Players[1].Equipment == null && s.Players[1].Memory.Single() == "lucky-pendant", "Destruir no actualiza equipo y memoria.");
            // Bonus and exchange appear as distinct field targets in the real picker.
            p.Bonus = "senshuni"; s.Players[1].Exchange.Add("attack");
            p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200; s.ActionsLeft = 2;
            Execute(_game.PlayCode(0, 0)); ShowPendingSkillChoice();
            Check(Dialog().FindChild("Destroy0Bonus", true, false) != null && Dialog().FindChild("Destroy1Exchange0", true, false) != null,
                "El selector no muestra Bonus y canje.");
            ((Button)Dialog().FindChild("Destroy0Bonus", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await Layout(); _drawLayoutFrames = 0;
            Check(p.Memory.Contains("senshuni") && p.Bonus != null && _drawQueue.Count == 1 && _drawQueue.Peek().BonusDestination,
                "Destruir Bonus no lo manda a memoria o no anima su reposición.");
            UpdateDrawAnimations(1);
            p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200; s.ActionsLeft = 2;
            Execute(_game.PlayCode(0, 0)); ShowPendingSkillChoice();
            ((Button)Dialog().FindChild("Destroy1Exchange0", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await Layout();
            Check(s.Players[1].Exchange.Count == 0 && s.Players[1].Memory.Contains("attack"), "Destruir canje no actualiza zona y memoria.");
            p.Hand.Clear(); p.Hand.AddRange(new[] { "attack", "reroll" }); p.Skills.Add("destiny-shuffle"); s.Phase = Phase.Combat; p.ActionsLeft = 0;
            rng.Enqueue(1); Execute(_game.Attack(0, 0, 0)); DrainTestDice(); await Layout(); ShowPendingSkillChoice();
            Check(((HandCard)_root.FindChild("HandCard0", true, false)).HasCodeArtwork && Dialog().FindChild("PlayRerollCombo", true, false) != null, "No se ofrece el combo gratis sobre el 1.");
            rng.Enqueue(2); ((Button)Dialog().FindChild("PlayRerollCombo", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            Check(p.ActionsLeft == 0 && p.Hand.Count == 1 && p.Memory.Contains("reroll") && CardsInMotion, "El combo gasta acción o no roba con 1–3.");
            DrainTestDice(); await Layout(); _drawLayoutFrames = 0;
            for (var i = 0; i < 30 && CardsInMotion; i++) UpdateDrawAnimations(1);
            if (s.Phase == Phase.AttackRerollChoice) _game.ResolveAttackReroll(0, false);
            NewGame(); await Layout(); Check(_game.State.Players.All(player => player.ActionsLeft == 2), "Reiniciar no restaura los cupos.");
            GD.Print("COMBO_SMOKE_PASS · imágenes limpias, Huevo 200 PR/objetivo, destrucción a memoria, combo sobre 1 sin acciones y robo animado."); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

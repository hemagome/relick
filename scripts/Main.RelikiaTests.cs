using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private sealed class RelikiaTestRandom : IGameRandom { public int Next(int exclusiveMax) => exclusiveMax - 1; }

    private async void RelikiaSmokeTest()
    {
        try
        {
            _helpOpen = true; _die.SetProcess(false);
            _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            void Press(Node root, string name)
            {
                var b = root.FindChild(name, true, false) as Button ?? throw new InvalidOperationException($"Falta {name}.");
                Check(!b.Disabled, $"{name} está desactivado.");
                b.EmitSignal(BaseButton.SignalName.Pressed);
            }
            PlayerState Fixture()
            {
                _helpOpen = true;
                BindGame(new GameEngine(new RelikiaTestRandom()));
                var s = _game.State; s.CurrentPlayer = 0; s.Phase = Phase.Actions; s.ActionsLeft = 2;
                var p = s.Active; p.Relikia = 1000; p.Bonus = "destiny-shuffle";
                p.Hand.Clear(); p.Hand.Add("lucky-pendant");
                p.Memory.Clear(); p.Memory.Add("lucky-pendant");
                Render(); return p;
            }
            StageAdvanceButton Advance() => (StageAdvanceButton)_root.FindChild("AdvanceStageButton", true, false);
            var player = Fixture(); player.Hand.Clear(); player.Relikia = 99; Render();
            Check(Advance().IsGlowing, "No se sugiere terminar sin jugadas.");
            player.Relikia = 100; Render(); Check(!Advance().IsGlowing, "El brillo ignora Relikia disponible.");
            _game.State.Phase = Phase.Combat; player.Skills.Clear(); player.Relikia = 0; Render();
            Check(Advance().IsGlowing, "No se sugiere avanzar desde Combate vacío.");
            player.Hand.Add("magnet-shuffle"); Render(); Check(!Advance().IsGlowing, "Se sugiere avanzar aunque se pueda enseñar.");
            _die.RollTo(6); Render(); Check(!Advance().IsGlowing && Advance().Disabled, "El brillo aparece durante el dado.");
            _die.AdvancePlayback(3);

            player = Fixture(); _helpOpen = false;
            Press(_root, "RelikiaEffectsButton");
            Check(Dialog().FindChildren("Relikia*", "Button", true, false).OfType<Button>().Count(b => b.Name != "") >= 4, "Faltan los cuatro efectos.");
            Press(Dialog(), "RelikiaDrawAndPlace");
            Check(_game.State.Phase == Phase.RelikiaChoice && Advance().Disabled && !Advance().IsGlowing, "La elección permite saltar etapa.");
            ShowPendingRelikiaChoice();
            Press(Dialog(), "RelikiaHand0"); Press(Dialog(), "RelikiaPlaceDeck");
            _helpOpen = true;
            Check(player.Deck.Last() == "lucky-pendant" && player.Relikia == 700 && _game.State.ActionsLeft == 1, "Colocar al fondo no resolvió el efecto de 300.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            player = Fixture(); player.Relikia = 400; Render();
            _helpOpen = false; Press(_root, "RelikiaEffectsButton");
            Check(((Button)Dialog().FindChild("RelikiaReplayCode", true, false)).Disabled, "Se permite reciclar equipos como códigos.");
            Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); _helpOpen = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            player = Fixture(); _game.State.Players[1].Memory.Add("lucky-pendant"); _helpOpen = false;
            Press(_root, "RelikiaEffectsButton"); Press(Dialog(), "RelikiaClaimBonus");
            Check(_die.IsRolling && _game.State.PendingRelikia == RelikiaChoice.BonusReward, "Relikia no anima el D6 real.");
            ShowPendingRelikiaChoice(); Check(!GetChildren().OfType<AcceptDialog>().Any(d => !d.IsQueuedForDeletion()), "La elección tapa el lanzamiento.");
            _die.AdvancePlayback(3); ShowPendingRelikiaChoice(); Press(Dialog(), "RelikiaRewardCorruptCodes");
            Check(_game.State.Phase == Phase.Actions && player.CorruptedMemory.Count == 0 && _game.State.Players[1].CorruptedMemory.Count == 0,
                "Sin códigos, corromper debe resolver sin crear cartas ni elecciones.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            player = Fixture(); _game.State.CurrentPlayer = 1;
            _game.State.Active.Relikia = 500; _game.State.Active.Bonus = "destiny-shuffle";
            player.Hand.Clear(); player.Hand.AddRange(new[] { "lucky-pendant", "attack", "lucky-pendant" });
            _game.UseRelikiaEffect(1, RelikiaEffect.ClaimBonus); _game.ChooseRelikiaReward(1, RelikiaReward.OpponentDiscards);
            _die.AdvancePlayback(3); Render();
            Check(Advance().Disabled && !Advance().IsGlowing, "Se sugiere terminar durante el descarte del rival.");
            Press(_root, "HandCard2"); UseSelected();
            Press(_root, "HandCard0"); UseSelected();
            Check(player.Hand.Single() == "attack" && _game.State.CurrentPlayer == 1 && _game.State.Phase == Phase.Actions,
                "El humano no elige sus descartes durante el turno del rival.");

            Fixture(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var minimum = _root.GetCombinedMinimumSize();
            Check(minimum.X <= _root.Size.X + 1 && minimum.Y <= _root.Size.Y + 1, $"Relikia desborda el tablero: {minimum} / {_root.Size}");
            GetWindow().Size = new(976, 837);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            minimum = _root.GetCombinedMinimumSize();
            Check(minimum.X <= _root.Size.X + 1 && minimum.Y <= _root.Size.Y + 1, $"Relikia desborda la ventana mínima: {minimum} / {_root.Size}");
            GD.Print("RELIKIA_SMOKE_PASS · cuatro efectos, elecciones, D6, corrupción, descarte del adversario, brillo y ventana mínima.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

using Godot;
using Relick.Core;
using Relick.Network;

namespace Relick;

public partial class OnlineLobby
{
    private async void CombatSmokeTest()
    {
        using var rival = new LobbyConnection();
        async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Tiempo agotado: " + _lobby.Message + " / " + rival.Message);
                rival.Tick();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        try
        {
            await Until(() => _lobby.Ready);
            if (_lobby.Room != null) throw new Exception("La prueba necesita un perfil sin sala activa.");
            _name.Text = "Ana"; _create.EmitSignal(Button.SignalName.Pressed);
            await Until(() => _lobby.Room != null);
            rival.Connect(_server, _database, null); await Until(() => rival.Ready);
            rival.Join(_lobby.Room!.Code, "Beto");
            await Until(() => _lobby.InitialSkills?.Selected == false && rival.InitialSkills?.Selected == false);
            _lobby.ChooseInitialSkill("magnet-shuffle"); rival.ChooseInitialSkill("destiny-shuffle");
            await Until(() => _lobby.Hand.Length == 5 && rival.Hand.Length == 5);
            _lobby.Confirm([]); rival.Confirm([]);
            await Until(() => _arena != null && _lobby.Duel != null && rival.Duel != null);
            var path = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--pvp-screenshot="))?.Split('=', 2)[1];
            var captured = false;
            var recoveryChecked = false;
            var steps = 0;
            while (_lobby.Duel!.Winner == null && steps++ < 600)
            {
                await Until(() => !_arena!.IsAnimating);
                var current = _lobby.Duel;
                var ownTurn = current.DecisionPlayer == current.MySeat;
                var snapshot = ownTurn ? current : rival.Duel!;
                var choices = snapshot.Options.Select((option, index) => (option, index)).Where(p => p.option.Move.Kind != DuelMoveKind.Concede).ToList();
                if (choices.Count == 0) throw new Exception("No hay jugadas en " + snapshot.Phase);
                var choice = choices.FirstOrDefault(p => p.option.RequiredDiscards > 0);
                if (choice.option == null) choice = choices[0];
                if (ownTurn)
                {
                    var cards = _arena!.FindChildren("HandCard*", "Button", true, false).OfType<HandCard>().ToArray();
                    if (choice.option.RequiredDiscards > 0)
                        foreach (var card in cards.Take(choice.option.RequiredDiscards)) card.EmitSignal(Button.SignalName.Pressed);
                    else if (choice.option.Move.Kind is DuelMoveKind.Learn or DuelMoveKind.Attack or DuelMoveKind.PlayCode or DuelMoveKind.Equip or DuelMoveKind.Exchange or DuelMoveKind.Discard or DuelMoveKind.Placement)
                        cards[choice.option.Move.Index].EmitSignal(Button.SignalName.Pressed);
                    var button = _arena.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == choice.option.Label);
                    if (button == null || button.Disabled) throw new Exception("No hay botón activo para " + choice.option.Label);
                    button.EmitSignal(Button.SignalName.Pressed);
                }
                else rival.Play(choice.index, Enumerable.Range(0, choice.option.RequiredDiscards));
                await Until(() => _lobby.Duel?.Revision == current.Revision + 1 && rival.Duel?.Revision == current.Revision + 1 && !_lobby.PlayPending && !rival.PlayPending);
                if (!recoveryChecked && _lobby.Duel!.Visuals.Any(v => v.Kind == DuelVisualKind.Die))
                {
                    var revision = _lobby.Duel.Revision;
                    var visuals = _arena!.PlayedVisualRevisions;
                    Connect();
                    await Until(() => _lobby.Ready && _lobby.Duel?.Revision == revision);
                    if (_arena.PlayedVisualRevisions != visuals || _arena.IsAnimating) throw new Exception("Reconectar repite animaciones antiguas.");
                    recoveryChecked = true;
                }
                if (path != null && !captured && _lobby.Duel!.Turn >= 3)
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(path); captured = true;
                }
                if (steps % 20 == 0) GD.Print($"PVP_COMBAT_PROGRESS · {steps} jugadas · turno {_lobby.Duel!.Turn}");
            }
            if (_lobby.Duel!.Winner == null || rival.Duel!.Winner != _lobby.Duel.Winner) throw new Exception("El duelo no terminó con el mismo resultado.");
            if (!recoveryChecked) throw new Exception("No se comprobó la recuperación de una tirada.");
            _lobby.Leave(); await Until(() => _lobby.Room == null && rival.Room == null);
            GD.Print($"PVP_COMBAT_PASS · duelo completo de {steps} jugadas mediante la arena y un rival remoto.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("PVP_COMBAT_FAIL · " + error);
            GetTree().Quit(1);
        }
    }
}

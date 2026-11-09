using Godot;
using Relick.Network;

namespace Relick;

public partial class OnlineLobby
{
    private async void PreparationSmokeTest()
    {
        using var rival = new LobbyConnection();
        async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception($"Tiempo agotado: {_lobby.Message} / rival {rival.Message}; combate={_lobby.Duel != null}, preparación={_arena?.IsPreparing}, animando={_arena?.IsAnimating}");
                rival.Tick();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        try
        {
            await Until(() => _lobby.Ready);
            if (_lobby.Room != null) throw new Exception("La prueba necesita un perfil sin sala activa.");
            _name.Text = "Ana";
            _create.EmitSignal(Button.SignalName.Pressed);
            await Until(() => _lobby.Room != null);
            rival.Connect(_server, _database, null);
            await Until(() => rival.Ready);
            rival.Join(_lobby.Room!.Code, "Beto");
            await Until(() => _arena?.IsChoosingInitialSkill == true && rival.InitialSkills?.Selected == false);
            var selectedSkill = _arena!.FindChildren("InitialSkillCard*", "Button", true, false).OfType<HandCard>().Single(c => c.Card.Id == "destiny-shuffle");
            GuiInputTestDriver.Click(selectedSkill);
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GuiInputTestDriver.Click(_arena.InitialSkillConfirmButton!);
            rival.ChooseInitialSkill("magnet-shuffle");
            await Until(() => _arena?.OpeningCardCount == 5 && _arena.PreparationConfirmButton?.Disabled == false);
            var preparationArena = _arena;
            if (_content.Visible || !_arena!.IsPreparing) throw new Exception("El mulligan no se abrió dentro del tablero.");
            foreach (var card in _arena!.FindChildren("HandCard*", "Button", true, false).OfType<HandCard>().Take(2)) GuiInputTestDriver.Click(card);
            await Until(() => _arena!.PreparationConfirmButton!.Text.Contains("Cambiar 2"));
            GuiInputTestDriver.Click(_arena!.PreparationConfirmButton!);
            await Until(() => _lobby.Preparation?.MyConfirmed == true);
            if (!_arena!.PreparationConfirmButton!.Disabled || _arena.FindChildren("HandCard*", "Button", true, false).OfType<HandCard>().Any(c => !c.Disabled)) throw new Exception("La elección confirmada sigue editable.");
            Connect();
            await Until(() => _lobby.Ready && _lobby.Preparation?.MyConfirmed == true && _arena?.PreparationConfirmButton?.Disabled == true);
            if (_arena != preparationArena || _arena!.OpeningSelectionCount != 2 || _arena.IsAnimating)
                throw new Exception("Reconectar pierde o repite la preparación confirmada.");
            rival.Confirm([]);
            await Until(() => _arena!.PlayedInitiativeRolls == 1);
            var dice = _arena!.FindChildren("InitiativeDice*", "Control", true, false).OfType<DiceFace>().OrderBy(d => d.Name.ToString()).ToArray();
            var opening = _lobby.OpeningResult!;
            if (dice.Length != 2 || dice.Any(d => !d.IsRolling) || dice[0].LatestResult != opening.RollOne || dice[1].LatestResult != opening.RollTwo)
                throw new Exception("La iniciativa no anima ambos resultados autorizados por el servidor.");
            await Until(() => _lobby.Duel != null && _arena != null && !_arena.IsPreparing && !_arena.IsAnimating);
            if (_arena != preparationArena) throw new Exception("El combate reemplazó el tablero de preparación.");
            if (_content.Visible) throw new Exception("La preparación sigue disponible después del inicio.");
            var expected = _lobby.Duel!.CurrentPlayer == _lobby.Room!.MySeat ? 6 : 5;
            if (_lobby.Duel!.Players[_lobby.Room.MySeat].Hand.Length != expected) throw new Exception("La mano visible no incluye el robo del primer turno.");
            Connect();
            await Until(() => _lobby.Ready && _arena!.OpeningCardCount == 5 && !_arena.IsPreparing && !_arena.IsAnimating);
            if (_arena!.PlayedInitiativeRolls != 1) throw new Exception("Reconectar repite los dados de iniciativa.");
            var path = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--pvp-screenshot="))?.Split('=', 2)[1];
            if (path != null)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(path);
            }
            _lobby.Leave();
            await Until(() => _lobby.Room == null && rival.Room == null);
            GD.Print("PVP_PREPARATION_PASS · selección, confirmación, espera, Bonus, iniciativa y mano del primer turno.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            if (_lobby.Ready && _lobby.Room != null) _lobby.Leave();
            GD.PushError("PVP_PREPARATION_FAIL · " + error);
            GetTree().Quit(1);
        }
    }
}

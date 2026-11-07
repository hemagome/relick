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
                if (DateTime.UtcNow > deadline) throw new Exception("Tiempo agotado esperando la preparación: " + _lobby.Message);
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
            await Until(() => _choices.Count == 5 && !_confirm.Disabled);
            foreach (var choice in _choices.Values.Take(2)) choice.ButtonPressed = true;
            await Until(() => _confirm.Text.Contains("Cambiar 2"));
            _confirm.EmitSignal(Button.SignalName.Pressed);
            await Until(() => _lobby.Preparation?.MyConfirmed == true);
            if (!_confirm.Disabled || _choices.Values.Any(c => !c.Disabled)) throw new Exception("La elección confirmada sigue editable.");
            rival.Confirm([]);
            await Until(() => _lobby.Duel != null && _arena != null);
            if (_content.Visible) throw new Exception("La preparación sigue disponible después del inicio.");
            var expected = _lobby.Duel!.CurrentPlayer == _lobby.Room!.MySeat ? 6 : 5;
            if (_lobby.Duel!.Players[_lobby.Room.MySeat].Hand.Length != expected) throw new Exception("La mano visible no incluye el robo del primer turno.");
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

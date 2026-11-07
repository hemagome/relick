using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineLobby
{
    private async void InputPeerSmokeTest()
    {
        var args = OS.GetCmdlineUserArgs();
        var role = args.Single(a => a.StartsWith("--pvp-input-peer=")).Split('=', 2)[1];
        var session = args.Single(a => a.StartsWith("--pvp-input-session=")).Split('=', 2)[1];
        var host = role == "host";
        async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Tiempo agotado: " + _lobby.Message);
                await Frame();
            }
            await Frame(); await Frame();
        }
        async Task Click(Control control)
        {
            for (var parent = control.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(control);
            await Frame(); await Frame();
            GuiInputTestDriver.Click(control);
        }
        async Task ClickCurrent(Func<Control> resolve)
        {
            await Frame(); await Frame();
            var control = resolve();
            for (var parent = control.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(control);
            await Frame(); await Frame();
            // The other player's preparation may rebuild the controls while layout settles.
            GuiInputTestDriver.Click(resolve());
        }
        Button ArenaButton(string text) => _arena!.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == text);
        try
        {
            // Headless runs need embedded popups to exercise their GUI routing.
            GetViewport().GuiEmbedSubwindows = true;
            if (role is not ("host" or "guest")) throw new Exception("Usa host o guest.");
            await Until(() => _lobby.Ready);
            if (_lobby.Room != null) throw new Exception("La prueba necesita un perfil sin sala activa.");
            GuiInputTestDriver.ReplaceText(_name, host ? "Ana teclado" : "Beto teclado");
            if (host)
            {
                await Click(_create);
                await Until(() => _lobby.Room != null);
                System.IO.File.WriteAllText(session + ".room", _lobby.Room!.Code);
            }
            else
            {
                await Until(() => System.IO.File.Exists(session + ".room"));
                GuiInputTestDriver.ReplaceText(_code, System.IO.File.ReadAllText(session + ".room"));
                await Click(_join);
            }
            await Until(() => _arena?.IsChoosingInitialSkill == true);
            await ClickCurrent(() => _arena!.FindChildren("InitialSkillCard*", "Button", true, false).OfType<HandCard>().Single(c => c.Card.Id == (host ? "destiny-shuffle" : "magnet-shuffle")));
            await ClickCurrent(() => _arena!.InitialSkillConfirmButton!);
            await Until(() => _arena?.OpeningCardCount == 5 && _arena.PreparationConfirmButton?.Disabled == false);
            var preparationArena = _arena;
            if (_content.Visible || !_arena!.IsPreparing) throw new Exception("El mulligan sigue en la sala.");
            GD.Print($"PVP_INPUT_PEER_PROGRESS · {role} · sala compartida y cinco cartas.");
            if (_lobby.Room!.MySeat != (host ? 0 : 1) || _lobby.Room.PlayerOne != "Ana teclado" || _lobby.Room.PlayerTwo != "Beto teclado")
                throw new Exception("Las dos instancias no tienen identidades y nombres independientes.");
            await ClickCurrent(() => (Control)_arena!.FindChild("HandCard0", true, false));
            await Until(() => _arena!.OpeningSelectionCount == 1 && _arena.PreparationConfirmButton!.Text.Contains("Cambiar 1"));
            GD.Print($"PVP_INPUT_PEER_PROGRESS · {role} · carta seleccionada.");
            await ClickCurrent(() => _arena!.PreparationConfirmButton!);
            await Until(() => _arena != null && !_arena.IsPreparing && _lobby.Duel != null && !_arena.IsAnimating);
            if (_arena != preparationArena) throw new Exception("No se conservó la arena al empezar el combate.");
            GD.Print($"PVP_INPUT_PEER_PROGRESS · {role} · combate iniciado.");
            // Verify combat hit testing too, including an embedded dialog.
            await Click((Control)_arena!.FindChild("MemoryButton0", true, false));
            var dialog = _arena!.GetChildren().OfType<AcceptDialog>().Single();
            await Until(() => dialog.Visible);
            await Click(dialog.GetOkButton());
            await Until(() => !GodotObject.IsInstanceValid(dialog) || !dialog.Visible);
            System.IO.File.WriteAllText(session + "." + role + ".ready", "ready");
            if (host)
            {
                await Until(() => System.IO.File.Exists(session + ".guest.ready"));
                var concede = _lobby.Duel!.Options.Single(o => o.Move.Kind == DuelMoveKind.Concede);
                await Click(ArenaButton(concede.Label));
                var confirmation = _arena.GetChildren().OfType<ConfirmationDialog>().Single();
                await Until(() => confirmation.Visible);
                await Click(confirmation.GetOkButton());
            }
            await Until(() => _lobby.Duel?.Winner != null);
            if (_lobby.Duel!.Winner != 1) throw new Exception("Los clientes no ven la victoria del segundo jugador.");
            if (host)
            {
                await Until(() => System.IO.File.Exists(session + ".guest.result"));
                await Click(ArenaButton("Cerrar sala"));
            }
            else System.IO.File.WriteAllText(session + ".guest.result", "seen");
            await Until(() => _lobby.Room == null);
            GD.Print($"PVP_INPUT_PEER_PASS · {role} · nombre, código, sala, cambio de carta, confirmación, arena, diálogos y cierre con eventos de entrada.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"PVP_INPUT_PEER_FAIL · {role} · " + error);
            GetTree().Quit(1);
        }
    }
}

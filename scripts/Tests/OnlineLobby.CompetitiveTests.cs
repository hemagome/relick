using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineLobby
{
    // Two processes play a ranked duel and a two-player tournament through the menus, against a local module with a
    // season and a tournament open. The session's ".enrolled" files tell the driver when to start the tournament.
    private async void CompetitivePeerSmokeTest()
    {
        var args = OS.GetCmdlineUserArgs();
        var role = args.Single(a => a.StartsWith("--pvp-competitive-peer=")).Split('=', 2)[1];
        var session = args.Single(a => a.StartsWith("--pvp-input-session=")).Split('=', 2)[1];
        var shots = args.FirstOrDefault(a => a.StartsWith("--pvp-competitive-shots="))?.Split('=', 2)[1];
        var host = role == "host";
        async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Until(Func<bool> condition, string waiting)
        {
            var deadline = DateTime.UtcNow.AddSeconds(70);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception($"Tiempo agotado esperando {waiting}: {_lobby.Message} {_lobby.CompetitionError}");
                await Frame();
            }
            for (var i = 0; i < 4; i++) await Frame();
        }
        async Task Click(Func<Control?> find)
        {
            await Until(() => find() is { } control && control.IsVisibleInTree() && control is not BaseButton { Disabled: true }, "un control");
            GuiInputTestDriver.Click(find()!);
            await Frame();
        }
        async Task Shot(string name)
        {
            if (shots == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(shots);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(shots, $"{name}-{role}.png"));
        }
        void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        async Task Play(bool tournament)
        {
            await Until(() => _arena?.IsChoosingInitialSkill == true && !_arena.IsAnimating, "la arena");
            Check(_lobby.RoomMode?.Mode == (tournament ? "tournament" : "ranked"), "La sala no conoce su modo.");
            if (host && !tournament)
            {
                await Click(() => _arena!.FindChild("ArenaMenu", true, false) as Control);
                var menu = ((MenuButton)_arena!.FindChild("ArenaMenu", true, false)).GetPopup();
                menu.EmitSignal(PopupMenu.SignalName.IdPressed, 2L);
                menu.Hide();
                await Until(() => _arena.FindChild("RatedLeaveDialog", true, false) is ConfirmationDialog { Visible: true }, "el aviso de salida");
                var warning = (ConfirmationDialog)_arena.FindChild("RatedLeaveDialog", true, false);
                Check(warning.DialogText.Contains("derrota") && _lobby.Room != null, "Salir de un duelo con puntuación no avisa.");
                warning.Hide(); warning.QueueFree();
            }
            _lobby.ChooseInitialSkill(CardCatalog.Magnet.Id);
            await Until(() => _lobby.Hand.Length == 5 && _arena!.PreparationConfirmButton?.Disabled == false && !_arena.IsAnimating, "la mano");
            await Click(() => _arena!.PreparationConfirmButton);
            await Until(() => _lobby.Duel != null && !_arena!.IsPreparing && !_arena.IsAnimating, "el combate");
            if (host)
            {
                await Click(() => _arena!.FindChild("ConcedeButton", true, false) as Control);
                await Until(() => _arena!.GetChildren().OfType<ConfirmationDialog>().Any(d => d.Name == "ConcedeDialog" && d.Visible), "la confirmación");
                GuiInputTestDriver.Click(_arena!.GetChildren().OfType<ConfirmationDialog>().Single(d => d.Name == "ConcedeDialog").GetOkButton());
            }
            await Until(() => _arena?.FindChild("DuelResultPanel", true, false) is Control { Visible: true }, "el resultado");
            await Until(() => _arena!.FindChild("DuelResultRated", true, false) is Label { Visible: true } label &&
                label.Text.Contains(tournament ? "torneo" : "→"), "el resultado con puntuación");
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            await Shot(tournament ? "resultado-torneo" : "resultado-elo");
            System.IO.File.WriteAllText($"{session}.{role}.seen-{tournament}", "seen");
            await Until(() => System.IO.File.Exists($"{session}.{(host ? "guest" : "host")}.seen-{tournament}"), "al rival");
            if (host) await Click(() => _arena!.FindChild("ReturnToLobby", true, false) as Control);
            await Until(() => _lobby.Room == null, "el cierre");
            if (!host) await Click(() => _arena!.FindChild("ReturnToLobby", true, false) as Control);
            await Until(() => _arena == null && _content.Visible, "la sala");
        }
        try
        {
            GetViewport().GuiEmbedSubwindows = true;
            await Until(() => _lobby.Ready && _lobby.CompetitiveAvailable && _lobby.Season != null && _lobby.Tournament != null, "los modos abiertos");
            _name.Text = host ? "Ana" : "Beto";

            await Click(() => _rankedCard);
            await Click(() => _competition?.FindChild("RankedSearch", true, false) as Control);
            await Play(tournament: false);
            await Until(() => _lobby.Standing is { Played: 1 }, "la puntuación");
            Check(_lobby.Standing!.Value == (host ? 976 : 1024) && _status.Text.StartsWith("ELO"), "Volver a la sala no informa el cambio de ELO.");
            await Until(() => _rankedCard.IsVisibleInTree(), "la tarjeta");
            await Shot("sala-tras-elo");

            await Click(() => _tournamentCard);
            await Click(() => _competition?.FindChild("TournamentAction", true, false) as Control);
            await Until(() => _lobby.MyTournament != null, "la inscripción");
            System.IO.File.WriteAllText($"{session}.{role}.enrolled", "ready");
            await Until(() => _lobby.MyTournament is { Match: > 0 }, "el inicio del torneo");
            await Shot("torneo");
            await Click(() => _competition?.FindChild("TournamentAction", true, false) as Control);
            await Play(tournament: true);
            await Until(() => _lobby.Tournament is { State: CompetitionView.Finished }, "el final del torneo");
            Check(_lobby.Tournament!.Champion == "Beto", "El campeón no es quien ganó.");

            if (!host)
            {
                await Until(() => _lobby.Owns(RewardKind.Frame, RewardCatalog.ChampionFrame.Id) && _lobby.Emblems.Count == 1, "los premios");
                await Click(() => _shell.Navigation.Buttons[MenuPage.Customize]);
                await Until(() => _wardrobe != null, "el vestidor");
                await Click(() => _wardrobe!.FindChild("CategoryFrame", true, false) as Control);
                _wardrobe!.Choose(RewardCatalog.ChampionFrame.Id);
                await Click(() => _wardrobe!.FindChild("CategoryHero", true, false) as Control);
                _wardrobe!.Choose(RelikarioCatalog.Osamu.Id);
                await Click(() => _wardrobe!.FindChild("CategoryBackdrop", true, false) as Control);
                _wardrobe!.Choose("abismo");
                await Click(() => _wardrobe!.FindChild("WardrobeSave", true, false) as Control);
                await Until(() => _lobby.Profile?.Frame == RewardCatalog.ChampionFrame.Id && _lobby.HomeHero == RelikarioCatalog.Osamu.Id &&
                    _lobby.HomeBackdrop == "abismo" && _wardrobe is { Dirty: false }, "el guardado del vestidor");
                await Click(() => _shell.Navigation.Buttons[MenuPage.Profile]);
                await Until(() => _page == MenuPage.Profile, "el perfil");
                await Shot("perfil");
                await Click(() => _shell.Navigation.Buttons[MenuPage.Play]);
                await Until(() => _hero.IsVisibleInTree() && _hero.Relikario == RelikarioCatalog.Osamu.Id && _background.Backdrop == "abismo", "el inicio personalizado");
                await Shot("inicio-personalizado");
            }
            GD.Print($"PVP_COMPETITIVE_PEER_PASS · {role} · ELO, torneo, premios e inicio personalizado con eventos de entrada.");
            QuitWithAudio();
        }
        catch (Exception error)
        {
            GD.PushError($"PVP_COMPETITIVE_PEER_FAIL · {role} · " + error);
            GetTree().Quit(1);
        }
    }
}

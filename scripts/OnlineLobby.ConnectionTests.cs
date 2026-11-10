using Godot;
using Relick.Core;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    private async Task CheckArenaConnectionNotice(OnlineArena online)
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Key(Key key)
        {
            GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = true }, true);
            GetViewport().PushInput(new InputEventKey { Keycode = key }, true);
        }
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        var previousArena = _arena;
        var embedded = GetViewport().GuiEmbedSubwindows;
        GetViewport().GuiEmbedSubwindows = true;
        _arena = online;
        var retries = 0;
        void Retry() => retries++;
        _reconnect.Pressed -= Connect; _reconnect.Pressed += Retry;
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (byte seat in new byte[] { 0, 1 })
            foreach (var phase in new[] { Phase.Preparation, Phase.Combat, Phase.Finished })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
                var game = new GameEngine(42);
                game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
                if (phase == Phase.Finished) game.Concede(seat);
                if (phase == Phase.Preparation)
                    online.PresentInitialSkillChoice(new RoomInfo("UITEST00", seat, "Ana", "Beto", true),
                        new InitialSkillInfo(false, "", [CardCatalog.Magnet.Id, CardCatalog.Destiny.Id]), true, false, new PreparationInfo());
                else online.PresentTestSnapshot(DuelSnapshot.For(game, seat, 420));
                await Layout();
                var menu = (MenuButton)online.FindChild("ArenaMenu", true, false);
                menu.FocusMode = FocusModeEnum.All;
                Check(Enumerable.Range(0, menu.GetPopup().ItemCount).All(i => menu.GetPopup().GetItemText(i) != "Reconectar"), "El menú conserva un reintento redundante.");
                Check(online.FindChild("ResultReconnect", true, false) == null, "El resultado duplica el reintento del aviso central.");
                PresentConnectionNotice(true, false); await Layout();
                Check(!_connectionNotice.Visible, "Un duelo conectado muestra el aviso.");
                menu.GrabFocus(); GuiInputTestDriver.Click(menu); await Layout();
                Check(menu.GetPopup().Visible, "No se abre el menú antes del corte.");
                PresentConnectionNotice(false, false); await Layout();
                Check(_connectionNotice.Visible && _reconnect.HasFocus() && !menu.GetPopup().Visible,
                    "El aviso no cierra el menú ni toma el foco durante el corte.");
                Check(new Rect2(Vector2.Zero, Size).Encloses(_connectionNoticePanel.GetGlobalRect()), "El aviso del duelo sale de pantalla.");
                var point = menu.GetGlobalRect().GetCenter();
                GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left }, true);
                Check(!menu.GetPopup().Visible, "El aviso deja abrir el menú detrás.");
                Key(Godot.Key.Tab); await Layout();
                Check(_reconnect.HasFocus(), "Tab escapa del aviso al tablero.");
                var previousRetries = retries;
                GuiInputTestDriver.Click(_reconnect);
                Check(retries == previousRetries + 1, "El aviso no permite reconectar desde el duelo.");
                PresentConnectionNotice(false, true); await Layout();
                Check(_connectionNotice.Visible && _reconnect.Disabled && _connectionNoticeTitle.Text == "Reconectando…", "El reintento oculta el aviso o permite envíos repetidos.");
                Key(Godot.Key.Tab); Key(Godot.Key.Enter); await Layout();
                Check(_connectionNotice.HasFocus() && retries == previousRetries + 1, "El teclado escapa al tablero o repite un reintento pendiente.");
                PresentConnectionNotice(false, false); await Layout();
                Check(!_reconnect.Disabled && _reconnect.HasFocus(), "El reintento fallido no vuelve a ofrecer Reconectar.");
                if (phase == Phase.Combat && seat == 0 && size.X is 1440 or 700) await Capture($"duel-disconnected-{size.X}x{size.Y}");
                PresentConnectionNotice(true, false); await Layout();
                Check(!_connectionNotice.Visible && menu.HasFocus(), "La recuperación no retira el aviso o devuelve el foco al duelo.");

                // A card consultation is a Window and sits above CanvasLayers.
                var dialog = new AcceptDialog { Title = "Memoria", DialogText = "Consulta de cartas" };
                online.AddChild(dialog); ArenaViewport.FitDialog(dialog, size); await Layout();
                // Also cover reconnecting before an offline frame was presented.
                PresentConnectionNotice(false, true); await Layout();
                Check(_connectionNotice.Visible && !GodotObject.IsInstanceValid(dialog), "Un diálogo tapa el aviso o una reconexión directa no lo muestra.");
                PresentConnectionNotice(true, false); await Layout();
            }
        }
        finally
        {
            PresentConnectionNotice(true, false);
            _reconnect.Pressed -= Retry; _reconnect.Pressed += Connect;
            _arena = previousArena; GetViewport().GuiEmbedSubwindows = embedded;
        }
        GD.Print("PVP_ARENA_CONNECTION_PASS · aviso único, menú y consultas abiertas, reintento, fallo, recuperación, bloqueo de clics y teclado; preparación, duelo y resultado en cuatro tamaños y ambos asientos.");
    }
}

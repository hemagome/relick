using Godot;

namespace Relick;

public partial class OnlineLobby
{
    private async Task CheckAudioControls()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Frames() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var settingsPath = $"/tmp/relick-audio-{Guid.NewGuid():N}.cfg";
        var preferences = new AudioPreferences { SettingsPath = settingsPath };
        var embedded = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        var originalSize = GetWindow().ContentScaleSize;
        try
        {
            AddChild(preferences);
            var musicBus = AudioServer.GetBusIndex("Music"); var effectsBus = AudioServer.GetBusIndex("Sfx");
            Check(musicBus > 0 && effectsBus > 0 && musicBus != effectsBus, "Faltan buses independientes de música y efectos.");
            preferences.SetMusicVolume(0); preferences.SetEffectsVolume(0.7f); preferences.Flush();
            Check(AudioServer.IsBusMute(musicBus) && !AudioServer.IsBusMute(effectsBus), "Silenciar música también silencia los efectos.");
            var config = new ConfigFile(); Check(config.Load(settingsPath) == Error.Ok, "No se guarda el volumen.");
            Check(config.GetValue("audio", "music").AsDouble() == 0 && Math.Abs(config.GetValue("audio", "effects").AsDouble() - 0.7) < 0.001,
                "Se mezclan las preferencias de volumen.");

            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            {
                GetWindow().ContentScaleSize = size; await Frames();
                Check(GetWindow().ContentScaleSize == size, "La prueba no conserva el tamaño solicitado.");
                preferences.OpenControls(); await Frames();
                var dialog = preferences.GetNode<AudioSettingsDialog>("AudioSettings");
                var music = dialog.FindChild("MusicVolume", true, false) as HSlider;
                var effects = dialog.FindChild("EffectsVolume", true, false) as HSlider;
                Check(music != null && effects != null && music.IsVisibleInTree() && effects.IsVisibleInTree(), "No aparecen los dos controles de volumen.");
                GuiInputTestDriver.Click(music!); await Frames();
                Check(music!.Value is > 45 and < 55 && Math.Abs(preferences.MusicVolume - music.Value / 100) < 0.001,
                    "El deslizador no recibe la interacción real.");
                music!.Value = 35; effects!.Value = 20;
                Check(Math.Abs(preferences.MusicVolume - 0.35) < 0.001 && Math.Abs(preferences.EffectsVolume - 0.20) < 0.001,
                    "Los controles no ajustan sus canales independientemente.");
                Check(!AudioServer.IsBusMute(musicBus) && Math.Abs(AudioServer.GetBusVolumeLinear(musicBus) - 0.35) < 0.001,
                    "El volumen de música no se aplica al audio.");
                Check(new Rect2(Vector2.Zero, dialog.Size).Encloses(music.GetGlobalRect()) &&
                    new Rect2(Vector2.Zero, dialog.Size).Encloses(effects.GetGlobalRect()), "El sonido queda fuera del diálogo.");
                var capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--audio-screenshot-dir="));
                if (capture != null && DisplayServer.GetName() != "headless")
                {
                    var directory = capture["--audio-screenshot-dir=".Length..]; System.IO.Directory.CreateDirectory(directory);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"{directory}/sound-{size.X}x{size.Y}.png");
                }
                GuiInputTestDriver.Click(dialog.GetOkButton()); await Frames();
                Check(!dialog.Visible, "Listo no cierra el control de sonido.");
            }
            preferences.Flush(); RemoveChild(preferences); preferences.Free();
            preferences = new AudioPreferences { SettingsPath = settingsPath }; AddChild(preferences);
            Check(Math.Abs(preferences.MusicVolume - 0.35) < 0.001 && Math.Abs(preferences.EffectsVolume - 0.20) < 0.001,
                "Reabrir la aplicación pierde los niveles guardados.");
            preferences.SetEffectsVolume(0); Check(AudioServer.IsBusMute(effectsBus) && !AudioServer.IsBusMute(musicBus), "Silenciar efectos altera la música.");
            GD.Print("AUDIO_SETTINGS_PASS · dos buses, volumen independiente, silencio real, controles y cierre, persistencia y cuatro tamaños.");
        }
        finally
        {
            RemoveChild(preferences); preferences.Free();
            GetNode<AudioPreferences>("/root/AudioPreferences").Apply();
            GetViewport().GuiEmbedSubwindows = embedded; GetWindow().ContentScaleSize = originalSize;
            if (System.IO.File.Exists(settingsPath)) System.IO.File.Delete(settingsPath);
        }
    }
}

using Godot;

namespace Relick;

public partial class OnlineLobby
{
    private async Task CheckAudioControls()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void CheckAppearance(Button mute, HSlider slider, bool silent)
        {
            var speaker = mute.GetNode<IconView>("SpeakerIcon");
            var fill = ((StyleBoxFlat)slider.GetThemeStylebox("grabber_area")).BgColor;
            var hoverFill = ((StyleBoxFlat)slider.GetThemeStylebox("grabber_area_highlight")).BgColor;
            Check(mute.Text.Length == 0 && speaker.Icon == (silent ? GameIcon.SpeakerMuted : GameIcon.Speaker),
                "El icono de parlante no refleja el estado del canal.");
            Check(speaker.Size.X > 0 && speaker.GetGlobalRect().GetCenter().DistanceTo(mute.GetGlobalRect().GetCenter()) < 1,
                "El icono de parlante queda descentrado.");
            Check(slider.Editable && fill == hoverFill && (silent ? Math.Abs(fill.R - fill.B) < 0.05f : fill == BoardArt.Gold),
                "La barra de volumen no conserva su color de estado o deja de ser ajustable.");
        }
        async Task Frames() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string suffix = "")
        {
            var capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--audio-screenshot-dir="));
            if (capture == null || DisplayServer.GetName() == "headless") return;
            var directory = capture["--audio-screenshot-dir=".Length..]; System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var size = GetWindow().ContentScaleSize;
            GetViewport().GetTexture().GetImage().SavePng($"{directory}/sound-{size.X}x{size.Y}{suffix}.png");
        }
        var settingsPath = $"/tmp/relick-audio-{Guid.NewGuid():N}.cfg";
        var preferences = new AudioPreferences { SettingsPath = settingsPath };
        var embedded = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        var originalSize = GetWindow().ContentScaleSize;
        try
        {
            var legacy = new ConfigFile();
            legacy.SetValue("audio", "music", 0.42); legacy.SetValue("audio", "effects", 0.7);
            Check(legacy.Save(settingsPath) == Error.Ok, "No se prepara la configuración anterior.");
            AddChild(preferences);
            Check(!preferences.MusicMuted && !preferences.EffectsMuted && Math.Abs(preferences.MusicVolume - 0.42) < 0.001,
                "Las preferencias anteriores pierden su volumen o activan silencio inesperadamente.");
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
                var musicMute = dialog.FindChild("MusicMute", true, false) as Button;
                var effectsMute = dialog.FindChild("EffectsMute", true, false) as Button;
                Check(music != null && effects != null && music.IsVisibleInTree() && effects.IsVisibleInTree(), "No aparecen los dos controles de volumen.");
                Check(musicMute != null && effectsMute != null && musicMute.IsVisibleInTree() && effectsMute.IsVisibleInTree(),
                    "Faltan los interruptores de silencio.");
                CheckAppearance(musicMute!, music!, preferences.MusicVolume == 0);
                CheckAppearance(effectsMute!, effects!, false);
                GuiInputTestDriver.Click(music!); await Frames();
                Check(music!.Value is > 45 and < 55 && Math.Abs(preferences.MusicVolume - music.Value / 100) < 0.001,
                    "El deslizador no recibe la interacción real.");
                music!.Value = 35; effects!.Value = 20;
                Check(Math.Abs(preferences.MusicVolume - 0.35) < 0.001 && Math.Abs(preferences.EffectsVolume - 0.20) < 0.001,
                    "Los controles no ajustan sus canales independientemente.");
                Check(!AudioServer.IsBusMute(musicBus) && Math.Abs(AudioServer.GetBusVolumeLinear(musicBus) - 0.35) < 0.001,
                    "El volumen de música no se aplica al audio.");
                GuiInputTestDriver.Click(musicMute!); await Frames();
                Check(preferences.MusicMuted && AudioServer.IsBusMute(musicBus) && !AudioServer.IsBusMute(effectsBus) && music.Value == 35,
                    "Silenciar música altera el volumen o los efectos.");
                CheckAppearance(musicMute!, music, true);
                CheckAppearance(effectsMute!, effects, false);
                await Capture("-muted");
                GuiInputTestDriver.Click(effectsMute!); await Frames();
                Check(preferences.EffectsMuted && AudioServer.IsBusMute(effectsBus) && effects.Value == 20,
                    "Silenciar efectos pierde su volumen.");
                CheckAppearance(effectsMute!, effects, true);
                GuiInputTestDriver.Click(musicMute!); await Frames();
                Check(!preferences.MusicMuted && !AudioServer.IsBusMute(musicBus) && AudioServer.IsBusMute(effectsBus) &&
                    Math.Abs(AudioServer.GetBusVolumeLinear(musicBus) - 0.35) < 0.001,
                    "Reactivar música no restaura su nivel independientemente de los efectos.");
                CheckAppearance(musicMute!, music, false);
                GuiInputTestDriver.Click(effectsMute!); await Frames();
                Check(!preferences.EffectsMuted && !AudioServer.IsBusMute(effectsBus) &&
                    Math.Abs(AudioServer.GetBusVolumeLinear(effectsBus) - 0.20) < 0.001,
                    "Reactivar efectos no restaura su volumen.");
                Check(new Rect2(Vector2.Zero, dialog.Size).Encloses(music.GetGlobalRect()) &&
                    new Rect2(Vector2.Zero, dialog.Size).Encloses(effects.GetGlobalRect()) &&
                    new Rect2(Vector2.Zero, dialog.Size).Encloses(musicMute!.GetGlobalRect()) &&
                    new Rect2(Vector2.Zero, dialog.Size).Encloses(effectsMute!.GetGlobalRect()), "El sonido queda fuera del diálogo.");
                await Capture();
                GuiInputTestDriver.Click(dialog.GetOkButton()); await Frames();
                Check(!dialog.Visible, "Listo no cierra el control de sonido.");
            }
            preferences.SetMusicMuted(true); preferences.SetEffectsMuted(true);
            preferences.SetMusicVolume(0.45f);
            Check(AudioServer.IsBusMute(musicBus), "Ajustar el volumen mientras está silenciado vuelve a activar el sonido.");
            preferences.SetMusicVolume(0.35f);
            preferences.Flush(); RemoveChild(preferences); preferences.Free();
            preferences = new AudioPreferences { SettingsPath = settingsPath }; AddChild(preferences);
            Check(Math.Abs(preferences.MusicVolume - 0.35) < 0.001 && Math.Abs(preferences.EffectsVolume - 0.20) < 0.001,
                "Reabrir la aplicación pierde los niveles guardados.");
            Check(preferences.MusicMuted && preferences.EffectsMuted && AudioServer.IsBusMute(musicBus) && AudioServer.IsBusMute(effectsBus),
                "Reabrir la aplicación pierde los silencios guardados.");
            preferences.OpenControls(); await Frames();
            var restored = preferences.GetNode<AudioSettingsDialog>("AudioSettings");
            var restoredMusicMute = (Button)restored.FindChild("MusicMute", true, false);
            var restoredEffectsMute = (Button)restored.FindChild("EffectsMute", true, false);
            Check(restoredMusicMute.ButtonPressed && restoredEffectsMute.ButtonPressed,
                "Reabrir el diálogo pierde el estado de sus interruptores.");
            CheckAppearance(restoredMusicMute, (HSlider)restored.FindChild("MusicVolume", true, false), true);
            CheckAppearance(restoredEffectsMute, (HSlider)restored.FindChild("EffectsVolume", true, false), true);
            GuiInputTestDriver.Click(restoredMusicMute); GuiInputTestDriver.Click(restoredEffectsMute); await Frames();
            Check(!AudioServer.IsBusMute(musicBus) && !AudioServer.IsBusMute(effectsBus) &&
                Math.Abs(preferences.MusicVolume - 0.35) < 0.001 && Math.Abs(preferences.EffectsVolume - 0.20) < 0.001,
                "Reactivar tras reiniciar no recupera los volúmenes anteriores.");
            GuiInputTestDriver.Click(restored.GetOkButton()); await Frames();
            preferences.SetEffectsVolume(0); Check(AudioServer.IsBusMute(effectsBus) && !AudioServer.IsBusMute(musicBus), "Silenciar efectos altera la música.");
            GD.Print("AUDIO_SETTINGS_PASS · silencios independientes con restauración de volumen, clics reales, persistencia y compatibilidad anterior; cuatro tamaños.");
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

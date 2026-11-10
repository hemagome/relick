using Godot;

namespace Relick;

public partial class AudioPreferences : Node
{
    [Export] public string SettingsPath { get; set; } = "user://audio.cfg";
    public float MusicVolume { get; private set; } = 0.65f;
    public float EffectsVolume { get; private set; } = 0.80f;
    public bool MusicMuted { get; private set; }
    public bool EffectsMuted { get; private set; }
    private Godot.Timer _saveTimer = null!;
    private bool _dirty;
    private AudioSettingsDialog? _dialog;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var path = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--audio-settings-path="));
        if (path != null && SettingsPath == "user://audio.cfg") SettingsPath = path["--audio-settings-path=".Length..];
        var config = new ConfigFile();
        if (config.Load(SettingsPath) == Error.Ok)
        {
            MusicVolume = Normalize((float)config.GetValue("audio", "music", MusicVolume).AsDouble());
            EffectsVolume = Normalize((float)config.GetValue("audio", "effects", EffectsVolume).AsDouble());
            MusicMuted = config.GetValue("audio", "music_muted", false).AsBool();
            EffectsMuted = config.GetValue("audio", "effects_muted", false).AsBool();
        }
        _saveTimer = new Godot.Timer { OneShot = true, WaitTime = 0.35, ProcessCallback = Godot.Timer.TimerProcessCallback.Idle };
        AddChild(_saveTimer); _saveTimer.Timeout += Flush;
        Apply();
    }

    public void SetMusicVolume(float volume) { MusicVolume = Normalize(volume); Changed(); }
    public void SetEffectsVolume(float volume) { EffectsVolume = Normalize(volume); Changed(); }
    public void SetMusicMuted(bool muted) { MusicMuted = muted; Changed(); }
    public void SetEffectsMuted(bool muted) { EffectsMuted = muted; Changed(); }

    private static float Normalize(float volume) => float.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0;
    private void Changed() { Apply(); _dirty = true; _saveTimer.Start(); }

    internal void Apply()
    {
        SetBus("Music", MusicVolume, MusicMuted); SetBus("Sfx", EffectsVolume, EffectsMuted);
    }

    private static void SetBus(string name, float volume, bool muted)
    {
        var bus = AudioServer.GetBusIndex(name);
        if (bus < 0) return;
        AudioServer.SetBusMute(bus, muted || volume == 0);
        AudioServer.SetBusVolumeLinear(bus, volume == 0 ? 1 : volume);
    }

    public void Flush()
    {
        if (!_dirty) return;
        var config = new ConfigFile();
        config.SetValue("audio", "music", MusicVolume);
        config.SetValue("audio", "effects", EffectsVolume);
        config.SetValue("audio", "music_muted", MusicMuted);
        config.SetValue("audio", "effects_muted", EffectsMuted);
        var result = config.Save(SettingsPath);
        if (result == Error.Ok) _dirty = false;
        else GD.PushWarning("No se pudo guardar el volumen en este dispositivo.");
    }

    public void OpenControls()
    {
        if (_dialog == null)
        {
            _dialog = new AudioSettingsDialog { Name = "AudioSettings", Preferences = this };
            AddChild(_dialog);
        }
        _dialog.Refresh();
        _dialog.PopupCenteredClamped(new Vector2I(420, 300), 0.9f);
    }

    public override void _ExitTree() => Flush();
}

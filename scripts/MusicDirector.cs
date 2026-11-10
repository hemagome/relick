using Godot;
using Relick.Core;

namespace Relick;

public enum MusicPhase { Menu, Combat, Climax }

// An autoload: the same audio clock survives menus, board rebuilds and reconnects.
public partial class MusicDirector : AudioStreamPlayer
{
    [Export] public string DefaultEnemyTheme { get; set; } = "yuno";
    [Export] public Godot.Collections.Dictionary<string, string> ThemeAliases { get; set; } = new()
    {
        ["yuno-lv2"] = "yuno"
    };
    public MusicPhase DesiredPhase { get; private set; } = MusicPhase.Menu;
    public string EnemyTheme { get; private set; } = "yuno";
    public int DesiredClipIndex { get; private set; } = -1;
    private AudioStreamInteractive? _score;
    private AudioStreamPlaybackInteractive? _playback;
    private string? _duel;
    private bool _climax;
    private int _requested = -1;
    private bool _released;
    private readonly HashSet<string> _unavailable = [];

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        PlaybackType = AudioServer.PlaybackType.Stream;
        // Keep runtime initial-clip changes local to this player.
        _score = Stream?.Duplicate() as AudioStreamInteractive;
        Stream = _score;
        ShowMenu();
    }

    public void ShowMenu()
    {
        _duel = null;
        _climax = false;
        EnemyTheme = DefaultEnemyTheme;
        Request(MusicPhase.Menu);
    }

    public void PresentDuel(string duel, DuelSnapshot snapshot)
    {
        if (_duel != duel)
        {
            _duel = duel;
            _climax = false;
        }
        if (snapshot.Phase == Phase.Preparation)
        {
            Request(MusicPhase.Menu);
            return;
        }
        if (snapshot.Players.Length != 2 || snapshot.MySeat is < 0 or > 1) return;
        // A player's soundtrack is determined exclusively by the opposing Relikario.
        var enemy = snapshot.Players[1 - snapshot.MySeat].RelikarioId;
        EnemyTheme = ThemeAliases.TryGetValue(enemy, out var theme) ? theme : enemy;
        // Latch the first zero, including a recovered duel whose players healed later.
        _climax |= snapshot.Players.Any(p => p.Health <= 0 || p.HasReachedZero);
        Request(_climax ? MusicPhase.Climax : MusicPhase.Combat);
    }

    private void Request(MusicPhase phase)
    {
        if (_released) return;
        DesiredPhase = phase;
        var clip = FindClip(phase, EnemyTheme);
        if (clip < 0 && phase != MusicPhase.Menu) clip = FindClip(phase, DefaultEnemyTheme);
        DesiredClipIndex = clip;
        if (_score == null || clip < 0 || !IsConfigured(_score.GetClipStream(clip)))
        {
            if (_unavailable.Add($"{phase}:{EnemyTheme}"))
                GD.Print($"Música: falta configurar {phase}:{EnemyTheme} en assets/audio/music.tres. Se conserva el audio disponible.");
            return;
        }
        if (_requested == clip) return;
        _requested = clip;
        if (!Playing)
        {
            _score.InitialClip = clip;
            Play();
            _playback = GetStreamPlayback() as AudioStreamPlaybackInteractive;
        }
        else if (_playback != null)
        {
            // Godot schedules the transition on its audio thread, using the score's table.
            _playback.SwitchToClip(clip);
        }
    }

    private int FindClip(MusicPhase phase, string theme)
    {
        if (_score == null) return -1;
        var name = phase == MusicPhase.Menu ? "Menu" : $"{phase}:{theme}";
        for (var i = 0; i < _score.ClipCount; i++)
            if (_score.GetClipName(i) == name) return i;
        return -1;
    }

    private static bool IsConfigured(AudioStream? stream) => stream switch
    {
        AudioStreamOggVorbis ogg => ogg.Loop && ogg.Bpm > 0 && ogg.BeatCount > 0 && ogg.BarBeats > 0,
        AudioStreamMP3 mp3 => mp3.Loop && mp3.Bpm > 0 && mp3.BeatCount > 0 && mp3.BarBeats > 0,
        _ => false
    };

    public void ReleaseAudio()
    {
        if (_released) return;
        _released = true;
        Stop(); Stream = null;
        _playback?.Dispose(); _playback = null;
        _score?.Dispose(); _score = null;
    }

    public override void _ExitTree() => ReleaseAudio();
}

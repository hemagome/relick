using Godot;

namespace Relick;

public enum SoundCue { CardDraw, DiceRoll, Heal, Attack, CardShuffle }

public partial class SoundDirector : Node
{
    private readonly Dictionary<SoundCue, AudioStreamPlayer> _players = [];
    internal event Action<SoundCue>? SoundPlayed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        foreach (var (cue, file) in new[]
        {
            (SoundCue.CardDraw, "card-draw"), (SoundCue.DiceRoll, "dice-roll"),
            (SoundCue.Heal, "heal"), (SoundCue.Attack, "attack"), (SoundCue.CardShuffle, "card-shuffle")
        })
        {
            var player = new AudioStreamPlayer
            {
                Name = cue.ToString(), Stream = GD.Load<AudioStreamWav>($"res://assets/audio/sfx/{file}.wav"),
                Bus = "Sfx", VolumeDb = -6, MaxPolyphony = 4, PlaybackType = AudioServer.PlaybackType.Stream
            };
            AddChild(player); _players[cue] = player;
        }
    }

    public void Play(SoundCue cue)
    {
        if (!_players.TryGetValue(cue, out var player) || player.Stream == null) return;
        player.Play();
        SoundPlayed?.Invoke(cue);
    }

    public void ReleaseAudio()
    {
        foreach (var player in _players.Values)
            if (GodotObject.IsInstanceValid(player)) { player.Stop(); player.Stream = null; }
        _players.Clear();
    }

    public override void _ExitTree() => ReleaseAudio();
}

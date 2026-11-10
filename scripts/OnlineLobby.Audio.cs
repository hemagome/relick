using Godot;

namespace Relick;

public partial class OnlineLobby
{
    private bool _quitting;

    private async void QuitWithAudio()
    {
        if (_quitting) return;
        _quitting = true;
        GetNode<AudioPreferences>("/root/AudioPreferences").Flush();
        GetNode<MusicDirector>("/root/MusicDirector").ReleaseAudio();
        GetNode<SoundDirector>("/root/SoundDirector").ReleaseAudio();
        // Stop native voices and flush managed resource disposal while the audio thread is alive.
        await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        for (var i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit();
    }
}

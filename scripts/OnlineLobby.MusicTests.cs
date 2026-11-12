using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineLobby
{
    private async void MusicSmokeTest()
    {
        MusicDirector? music = null;
        OnlineArena? arena = null;
        try
        {
            void Check(bool value, string message) { if (!value) throw new Exception(message); }
            async Task Frames(int count = 3)
            {
                for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            async Task Clip(int expected)
            {
                var deadline = Time.GetTicksMsec() + 6000;
                while (music!.GetStreamPlayback() is not AudioStreamPlaybackInteractive p || p.GetCurrentClipIndex() != expected)
                {
                    if (Time.GetTicksMsec() > deadline) throw new Exception($"El reproductor no alcanza el clip {expected}.");
                    await Frames(1);
                }
                // Allow the quantized fade to finish before the next assertion.
                await ToSignal(GetTree().CreateTimer(0.85), SceneTreeTimer.SignalName.Timeout);
                Check(((AudioStreamPlaybackInteractive)music.GetStreamPlayback()).GetCurrentClipIndex() == expected,
                    "Una transición anterior sobrescribe la petición actual.");
            }
            async Task Reach(int expected)
            {
                var deadline = Time.GetTicksMsec() + 3000;
                while (music!.GetStreamPlayback() is not AudioStreamPlaybackInteractive p || p.GetCurrentClipIndex() != expected)
                {
                    if (Time.GetTicksMsec() > deadline) throw new Exception($"La música no pasa por el clip {expected}.");
                    await Frames(1);
                }
            }
            DuelSnapshot Snapshot(Phase phase, int first = 8, int second = 8, bool reachedZero = false) => new()
            {
                Phase = phase, Players = [new() { Health = first, HasReachedZero = reachedZero }, new() { Health = second }]
            };

            var template = GD.Load<AudioStreamInteractive>("res://assets/audio/music.tres");
            Check(template.ClipCount >= 5, "Faltan las tres fases de música o sus puentes.");
            const int any = (int)AudioStreamInteractive.ClipAny;
            Check(template.HasTransition(any, any) &&
                template.GetTransitionFromTime(any, any) == AudioStreamInteractive.TransitionFromTime.NextBeat &&
                template.GetTransitionToTime(any, any) == AudioStreamInteractive.TransitionToTime.Start &&
                template.GetTransitionFadeMode(any, any) == AudioStreamInteractive.FadeMode.Cross,
                "La regla general debe fundir hacia el comienzo de la pieza de destino.");
            (double Bpm, int Beats, int Bar)[] meters = [(126, 144, 3), (160, 192, 4), (168, 160, 4)];
            for (var i = 0; i < 3; i++)
            {
                var song = template.GetClipStream(i) as AudioStreamOggVorbis;
                Check(song != null && song.Loop && song.Bpm == meters[i].Bpm && song.BeatCount == meters[i].Beats && song.BarBeats == meters[i].Bar &&
                    Math.Abs(song.GetLength() - meters[i].Beats * 60 / meters[i].Bpm) < 0.05,
                    "Una composición no conserva su tempo, duración o bucle.");
            }
            // Each lead-in ends on its last beat, keeps a tail and hands over to its piece without a fade.
            for (var i = 3; i < 5; i++)
            {
                var bridge = template.GetClipStream(i) as AudioStreamOggVorbis; var next = i - 2;
                Check(template.GetClipName(i) == "To" + template.GetClipName(next) &&
                    bridge is { Loop: false, BeatCount: 8, BarBeats: 4 } && bridge.Bpm == meters[next].Bpm &&
                    bridge.GetLength() > 8 * 60 / bridge.Bpm + 1 &&
                    template.GetClipAutoAdvance(i) == AudioStreamInteractive.AutoAdvanceMode.Enabled &&
                    template.GetClipAutoAdvanceNextClip(i) == next && template.HasTransition(i, next) &&
                    template.GetTransitionFromTime(i, next) == AudioStreamInteractive.TransitionFromTime.End &&
                    template.GetTransitionToTime(i, next) == AudioStreamInteractive.TransitionToTime.Start &&
                    template.GetTransitionFadeMode(i, next) == AudioStreamInteractive.FadeMode.Disabled,
                    "Un puente no conduce a su pieza en el último pulso.");
            }
            Check(template.GetTransitionFromTime(1, 4) == AudioStreamInteractive.TransitionFromTime.NextBar,
                "El clímax debe partir en un compás del combate.");

            // A genuinely empty score must be harmless before music assets are assigned.
            music = new MusicDirector { Stream = new AudioStreamInteractive { ClipCount = 5 } };
            AddChild(music);
            music.PresentDuel("empty", Snapshot(Phase.Combat, second: 0));
            Check(!music.Playing && music.DesiredPhase == MusicPhase.Climax, "Una pista ausente inicia audio inválido.");
            RemoveChild(music); music.Free();

            var score = (AudioStreamInteractive)template.Duplicate();
            // A second rival without lead-ins exercises the direct transitions.
            score.ClipCount = 7;
            score.SetClipName(5, "Combat:another"); score.SetClipName(6, "Climax:another");
            for (var i = 0; i < 7; i++)
            {
                var clock = AudioStreamOggVorbis.LoadFromFile("res://tests/fixtures/music-clock.ogg");
                var bridge = i is 3 or 4;
                clock.Bpm = 600; clock.BarBeats = 4; clock.BeatCount = bridge ? 4 : 20; clock.Loop = !bridge;
                score.SetClipStream(i, clock);
            }
            music = new MusicDirector { Stream = score, VolumeDb = -80, Bus = "Music" };
            AddChild(music); await Frames(); await Clip(0);
            Check(music.Playing, "No empieza la música de menú.");
            var playbackId = music.GetStreamPlayback().GetInstanceId();
            music.PresentDuel("A", Snapshot(Phase.Preparation));
            Check(music.DesiredPhase == MusicPhase.Combat, "La selección de habilidad no inicia el combate.");
            await Reach(3); await Clip(1);
            music.PresentDuel("A", Snapshot(Phase.Draw)); await Frames();
            Check(music.DesiredClipIndex == 1 && ((AudioStreamPlaybackInteractive)music.GetStreamPlayback()).GetCurrentClipIndex() == 1,
                "Empezar el duelo repite la entrada del combate.");
            for (var i = 0; i < 10; i++) music.PresentDuel("A", Snapshot(Phase.Actions));
            Check(music.GetStreamPlayback().GetInstanceId() == playbackId, "Reconstruir el tablero reinicia el audio.");

            var opponents = Snapshot(Phase.Combat);
            opponents.Players[0].RelikarioId = "another";
            music.PresentDuel("opponents", opponents); await Clip(1);
            Check(music.EnemyTheme == "yuno", "Se elige la música del Relikario propio.");
            opponents.MySeat = 1;
            music.PresentDuel("opponents", opponents); await Clip(5);
            Check(music.EnemyTheme == "another", "Invertir asientos no cambia al rival musical.");
            opponents.Players[1].RelikarioId = "third";
            music.PresentDuel("opponents", opponents);
            Check(music.DesiredClipIndex == 5, "Cambiar de Relikario propio altera la música del enemigo.");
            opponents.Players[1].Health = 0;
            music.PresentDuel("opponents", opponents); await Clip(6);
            Check(music.EnemyTheme == "another", "El cero propio usa otro tema de clímax.");
            var evolved = Snapshot(Phase.Combat); evolved.Players[1].RelikarioId = "yuno-lv2";
            music.PresentDuel("evolved", evolved); await Clip(1);
            Check(music.EnemyTheme == "yuno", "La evolución pierde la identidad musical de Yuno.");

            foreach (var zeroSeat in new[] { 0, 1 })
            {
                music.PresentDuel("zero-" + zeroSeat, Snapshot(Phase.Combat)); await Clip(1);
                music.PresentDuel("zero-" + zeroSeat, Snapshot(Phase.Combat, zeroSeat == 0 ? 0 : 8, zeroSeat == 1 ? 0 : 8));
                await Reach(4); await Clip(2);
                music.PresentDuel("zero-" + zeroSeat, Snapshot(Phase.Actions));
                Check(music.DesiredPhase == MusicPhase.Climax, "Curarse o reconectar abandona el clímax.");
            }
            music.PresentDuel("recovered", Snapshot(Phase.Actions, reachedZero: true));
            Check(music.DesiredPhase == MusicPhase.Climax, "Recuperar un duelo curado pierde su primer cero.");
            var result = Snapshot(Phase.Finished); result.Winner = 0;
            music.PresentDuel("recovered", result);
            Check(music.DesiredPhase == MusicPhase.Climax, "El resultado interrumpe el clímax.");
            music.ShowMenu(); await Clip(0);
            music.PresentDuel("new", Snapshot(Phase.Combat)); await Reach(3); await Clip(1);
            Check(music.DesiredPhase == MusicPhase.Combat, "Un nuevo duelo hereda el clímax anterior.");

            // Requests can change before the next audio mix or during a quantized transition.
            music.ShowMenu(); music.PresentDuel("fast", Snapshot(Phase.Combat));
            music.PresentDuel("fast", Snapshot(Phase.Combat, second: 0)); music.ShowMenu();
            await Clip(0);
            Check(music.GetStreamPlayback().GetInstanceId() == playbackId, "Cambiar de fase recrea el reproductor.");

            // Exercise the real arena hook, including a board rebuild and a healed checkpoint.
            var global = GetNode<MusicDirector>("/root/MusicDirector");
            arena = new OnlineArena { Connection = new(), RoomCode = "MUSIC-ARENA" };
            AddChild(arena); arena.SetProcess(false);
            var game = new GameEngine(42); game.ChooseInitialSkill(0, CardCatalog.Magnet.Id);
            game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
            arena.PresentTestSnapshot(DuelSnapshot.For(game, 0, 1));
            Check(global.DesiredPhase == MusicPhase.Combat, "La arena no solicita música de combate.");
            game.State.Players[1].Health = 0;
            arena.PresentTestSnapshot(DuelSnapshot.For(game, 0, 2));
            Check(global.DesiredPhase == MusicPhase.Climax, "La arena no solicita el clímax con el rival a cero.");
            game.State.Players[1].Health = 3;
            arena.PresentTestSnapshot(DuelSnapshot.For(game, 0, 3));
            Check(global.DesiredPhase == MusicPhase.Climax, "Reconstruir la arena elimina el clímax.");
            await arena.CheckAudioPresentation();
            await CheckAudioControls();
            global.ShowMenu();
            GD.Print("MUSIC_PASS · tres composiciones, selección por rival y evolución, ambos asientos, curación, recuperación, resultado, nuevo duelo, peticiones rápidas y reproducción sin reinicios.");
            // Let native audio mixing release active voices before engine teardown.
            global.Stop();
            foreach (var player in GetNode<SoundDirector>("/root/SoundDirector").GetChildren().OfType<AudioStreamPlayer>()) player.Stop();
            arena.QueueFree(); arena = null;
            music.Stop(); music.QueueFree(); music = null;
            score = null!; template = null!;
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            CallDeferred(nameof(FinishAudioSmokeTest));
        }
        catch (Exception error)
        {
            GD.PushError("MUSIC_FAIL · " + error); GetTree().Quit(1);
        }
        finally
        {
            if (arena != null && GodotObject.IsInstanceValid(arena)) { RemoveChild(arena); arena.Free(); }
            if (music != null && GodotObject.IsInstanceValid(music)) { music.Stop(); RemoveChild(music); music.Free(); }
        }
    }

    private async void FinishAudioSmokeTest()
    {
        // The previous async state machine must return before its resource locals can be collected.
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        GC.Collect(); GC.WaitForPendingFinalizers();
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        QuitWithAudio();
    }
}

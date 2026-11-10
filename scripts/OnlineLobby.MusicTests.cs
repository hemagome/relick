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
                var deadline = Time.GetTicksMsec() + 3000;
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
            DuelSnapshot Snapshot(Phase phase, int first = 8, int second = 8, bool reachedZero = false) => new()
            {
                Phase = phase, Players = [new() { Health = first, HasReachedZero = reachedZero }, new() { Health = second }]
            };

            var template = GD.Load<AudioStreamInteractive>("res://assets/audio/music.tres");
            Check(template.ClipCount >= 3, "Faltan las tres fases de música.");
            const int any = (int)AudioStreamInteractive.ClipAny;
            Check(template.HasTransition(any, any) &&
                template.GetTransitionFromTime(any, any) == AudioStreamInteractive.TransitionFromTime.NextBar &&
                template.GetTransitionToTime(any, any) == AudioStreamInteractive.TransitionToTime.SamePosition &&
                template.GetTransitionFadeMode(any, any) == AudioStreamInteractive.FadeMode.Cross &&
                template.GetTransitionFadeBeats(any, any) == 4,
                "Las transiciones deben conservar la posición armónica y cambiar al siguiente compás.");
            for (var i = 0; i < 3; i++)
            {
                var song = template.GetClipStream(i) as AudioStreamOggVorbis;
                Check(song is { Loop: true, Bpm: 160, BeatCount: 128, BarBeats: 4 } && Math.Abs(song.GetLength() - 48) < 0.05,
                    "Una composición no conserva su tempo, duración o bucle.");
            }

            // A genuinely empty score must be harmless before music assets are assigned.
            music = new MusicDirector { Stream = new AudioStreamInteractive { ClipCount = 3 } };
            AddChild(music);
            music.PresentDuel("empty", Snapshot(Phase.Combat, second: 0));
            Check(!music.Playing && music.DesiredPhase == MusicPhase.Climax, "Una pista ausente inicia audio inválido.");
            RemoveChild(music); music.Free();

            var score = (AudioStreamInteractive)template.Duplicate();
            score.ClipCount = 5;
            score.SetClipName(3, "Combat:another"); score.SetClipName(4, "Climax:another");
            for (var i = 0; i < 5; i++)
            {
                var clock = AudioStreamOggVorbis.LoadFromFile("res://tests/fixtures/music-clock.ogg");
                clock.Bpm = 600; clock.BarBeats = 4; clock.BeatCount = 20; clock.Loop = true;
                score.SetClipStream(i, clock);
            }
            music = new MusicDirector { Stream = score, VolumeDb = -80, Bus = "Music" };
            AddChild(music); await Frames(); await Clip(0);
            Check(music.Playing, "No empieza la música de menú.");
            var playbackId = music.GetStreamPlayback().GetInstanceId();
            music.PresentDuel("A", Snapshot(Phase.Preparation)); await Frames();
            Check(music.DesiredPhase == MusicPhase.Menu, "La preparación adelanta el combate.");
            music.PresentDuel("A", Snapshot(Phase.Draw)); await Clip(1);
            for (var i = 0; i < 10; i++) music.PresentDuel("A", Snapshot(Phase.Actions));
            Check(music.GetStreamPlayback().GetInstanceId() == playbackId, "Reconstruir el tablero reinicia el audio.");

            var opponents = Snapshot(Phase.Combat);
            opponents.Players[0].RelikarioId = "another";
            music.PresentDuel("opponents", opponents); await Clip(1);
            Check(music.EnemyTheme == "yuno", "Se elige la música del Relikario propio.");
            opponents.MySeat = 1;
            music.PresentDuel("opponents", opponents); await Clip(3);
            Check(music.EnemyTheme == "another", "Invertir asientos no cambia al rival musical.");
            opponents.Players[1].RelikarioId = "third";
            music.PresentDuel("opponents", opponents);
            Check(music.DesiredClipIndex == 3, "Cambiar de Relikario propio altera la música del enemigo.");
            opponents.Players[1].Health = 0;
            music.PresentDuel("opponents", opponents); await Clip(4);
            Check(music.EnemyTheme == "another", "El cero propio usa otro tema de clímax.");
            var evolved = Snapshot(Phase.Combat); evolved.Players[1].RelikarioId = "yuno-lv2";
            music.PresentDuel("evolved", evolved); await Clip(1);
            Check(music.EnemyTheme == "yuno", "La evolución pierde la identidad musical de Yuno.");

            foreach (var zeroSeat in new[] { 0, 1 })
            {
                music.PresentDuel("zero-" + zeroSeat, Snapshot(Phase.Combat, zeroSeat == 0 ? 0 : 8, zeroSeat == 1 ? 0 : 8));
                await Clip(2);
                music.PresentDuel("zero-" + zeroSeat, Snapshot(Phase.Actions));
                Check(music.DesiredPhase == MusicPhase.Climax, "Curarse o reconectar abandona el clímax.");
            }
            music.PresentDuel("recovered", Snapshot(Phase.Actions, reachedZero: true));
            Check(music.DesiredPhase == MusicPhase.Climax, "Recuperar un duelo curado pierde su primer cero.");
            var result = Snapshot(Phase.Finished); result.Winner = 0;
            music.PresentDuel("recovered", result);
            Check(music.DesiredPhase == MusicPhase.Climax, "El resultado interrumpe el clímax.");
            music.ShowMenu(); await Clip(0);
            music.PresentDuel("new", Snapshot(Phase.Combat)); await Clip(1);
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

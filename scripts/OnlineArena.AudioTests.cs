using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckAudioPresentation()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var sound = GetNode<SoundDirector>("/root/SoundDirector");
        var played = new List<SoundCue>();
        void Record(SoundCue cue) => played.Add(cue);
        sound.SoundPlayed += Record;
        try
        {
            ResetAnimations();
            _die.QueueRoll(5); _die.QueueRoll(6);
            Check(played.SequenceEqual(new[] { SoundCue.DiceRoll }), "El relanzamiento suena antes de empezar.");
            _die.AdvancePlayback(DiceFace.RollSeconds + DiceFace.ResultSeconds + 0.1);
            _die.AdvancePlayback(0);
            Check(played.Count(c => c == SoundCue.DiceRoll) == 2, "El relanzamiento no suena al empezar.");
            _die.AdvancePlayback(DiceFace.RollSeconds + DiceFace.ResultSeconds + 0.1);
            _die.ShowAdjustedResult(6, "+1");
            Check(played.Count == 2, "Modificar un dado vuelve a reproducir el lanzamiento.");
            ResetAnimations();
            Animate([new() { Kind = DuelVisualKind.Attack, Player = 0 }, new() { Kind = DuelVisualKind.Heal, Player = 1, Value = 2 }], new());
            Check(played.Count(c => c == SoundCue.Attack) == 1 && played.Count(c => c == SoundCue.Heal) == 1,
                "Ataque y curación no suenan junto a sus animaciones.");
            PlayPendingPortraits();
            Check(played.Count == 4, "Volver a presentar una animación repite el sonido.");
            ResetAnimations();
            Animate([new() { Kind = DuelVisualKind.Draw, Player = _snapshot!.MySeat, Value = 0 }], new());
            UpdateFlights(0); UpdateFlights(0); UpdateFlights(0);
            Check(played.Count(c => c == SoundCue.CardDraw) == 1, "El robo no suena al comenzar su vuelo.");
            ResetAnimations();
            Animate([new() { Kind = DuelVisualKind.Shuffle, Player = _snapshot.MySeat }], new());
            Check(!played.Contains(SoundCue.CardShuffle), "El barajado suena antes de comenzar su animación.");
            UpdateFlights(0); UpdateFlights(0); UpdateFlights(0); UpdateFlights(0.1);
            Check(played.Count(c => c == SoundCue.CardShuffle) == 1, "El barajado debe sonar una sola vez al comenzar.");
            ResetAnimations();
            var before = played.Count;
            PresentTestSnapshot(_snapshot!);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(played.Count == before, "Recuperar el tablero repite efectos históricos.");
            foreach (var die in _initiativeDice) die.RollTo(4);
            Check(played.Count(c => c == SoundCue.DiceRoll) == 4, "Los dados de iniciativa no tienen sonido.");
            ResetAnimations();
            GD.Print("SFX_PASS · cinco efectos, robo y barajado sin repeticiones, ataque/curación, dado y relanzamiento, iniciativa, ajustes silenciosos y recuperación.");
        }
        finally { sound.SoundPlayed -= Record; ResetAnimations(); }
    }
}

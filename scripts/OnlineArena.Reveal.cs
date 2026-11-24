using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// A card turned face up in hand (Perro Fantasma). The rival sees it leave the hand, turn from the sleeve to its face
    /// and settle back; its owner sees it rise and turn in place. Both then keep the eye badge while it stays public.
    /// </summary>
    private void PlayReveal(DuelVisualEvent cue)
    {
        var s = _snapshot!;
        var reduced = _impacts.ReducedMotion;
        if (cue.Player == s.MySeat)
        {
            if (_handDeparture == null && _handCards.FirstOrDefault(card => card.Name == $"HandCard{cue.Value}") is { } own) own.PlayReveal(reduced);
        }
        else if (_opponentCards.FirstOrDefault(card => card.Name == $"OpponentHandCard{cue.Value}") is { } rival) rival.PlayReveal(reduced);
        else return;
        if (!reduced) PlaySound(SoundCue.CardDraw);
    }
}

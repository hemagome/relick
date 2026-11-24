using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// Shields and blocks play once any blow already flying at the defender lands. A block shows its own blow bouncing off
    /// only when this revision did not already launch the attack (Caja, Destiny, Esquivar answer after the attack was shown).
    /// Green when the protected duelist is you, red when it is your rival.
    /// </summary>
    private void PlayPendingGuards()
    {
        for (var i = 0; i < _pendingGuards.Count;)
        {
            var cue = _pendingGuards[i];
            var relative = cue.Player == _snapshot!.MySeat ? 0 : 1;
            var defender = _portraits[relative];
            if (_impacts.AttackInFlightTo(defender)) { i++; continue; }
            _pendingGuards.RemoveAt(i);
            var bounce = cue.Attacker != null && !_snapshot.Visuals.Any(v => v.Kind == DuelVisualKind.Attack && v.Player == cue.Attacker);
            var art = cue.GuardCard is { } id && CardCatalog.All.TryGetValue(id, out var card) ? CardShowcase.Artwork(card) : null;
            var accent = relative == 0 ? ArenaPresentation.Mint : ArenaPresentation.Coral;
            _impacts.Guard(defender, _portraits[1 - relative], bounce, cue.Blocked ? art : null, accent, cue.Original, cue.Value, cue.Blocked);
            if (cue.Blocked) _impacts.Flash(accent);
        }
    }
}

namespace Relick;

public partial class Main
{
    private readonly List<(int Player, PortraitMotion Motion)> _pendingCombatAnimations = new();

    private void PlayPendingCombatAnimations()
    {
        if (_die.IsBusy || CardsInMotion || _game.State.PendingAttackRoll != null) return;
        foreach (var (player, motion) in _pendingCombatAnimations) PortraitFor(player).Play(motion);
        _pendingCombatAnimations.Clear();
    }
}

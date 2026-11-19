namespace Relick.Core;

/// <summary>
/// Damage that a public card lowered (<see cref="Blocked"/> false: Casco de Diamantes, Anillo Morado) or stopped entirely
/// (Blocked true: Estrella del Fontanero, Caja de Cartón, Destiny, Esquivar, Hoguera, Turbo Sword). <see cref="Attacker"/>
/// is set only when the damage came from an attack, so the client can show the blow being stopped.
/// </summary>
public sealed record DamageGuard(int Defender, int? Attacker, string CardId, int Original, int Final, bool Blocked);

public sealed partial class GameEngine
{
    /// <summary>Raised when a card lowers or stops incoming damage, before any reduced damage is dealt.</summary>
    public event Action<DamageGuard>? DamageGuarded;

    internal void NotifyGuard(int defender, int? attacker, string cardId, int original, int final, bool blocked)
    {
        if (original <= 0) return;
        DamageGuarded?.Invoke(new(defender, attacker, cardId, original, Math.Clamp(final, 0, original), blocked));
    }
}

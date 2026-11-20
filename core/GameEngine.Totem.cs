namespace Relick.Core;

public sealed partial class GameEngine
{
    private bool HasStackedTotem(int actor) => SalvationTotemEffect.ActiveCopies(State, actor, EffectsSuppressed).Count() > 1;

}

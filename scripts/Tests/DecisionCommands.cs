namespace Relick.Core;

/// <summary>
/// Test shorthands that answer the pending decision by what the choice means instead of by its index, as the engine's
/// former per-card commands did. Players always answer with <see cref="DuelMoveKind.Choose"/>; these exist only so the
/// tests read naturally. Compiled with the UI tests and linked into the engine test project.
/// </summary>
internal static class DecisionCommands
{
    private static CommandResult Answer(GameEngine game, int actor, string source, Func<DecisionChoice, bool> matches, string error, string? step = null)
    {
        if (game.State.Phase != Phase.Decision || game.State.PendingDecision is not { } decision || decision.Source != source ||
            decision.Decider != actor || step != null && decision.Step != step) return CommandResult.Fail(error);
        var choices = game.DecisionChoices(actor);
        for (var i = 0; i < choices.Count; i++)
            if (matches(choices[i])) return game.ResolveDecision(actor, i);
        return CommandResult.Fail(error);
    }

    public static CommandResult ResolvePointsChange(this GameEngine game, int actor, int target, bool increase) =>
        Answer(game, actor, CardCatalog.PointsIncrease.Id, c => c.Value == target * 2 + (increase ? 1 : 0), "No te corresponde resolver este cambio de PR.");

    public static CommandResult ResolveDestruction(this GameEngine game, int actor, int owner, bool equipment, int index = -1) =>
        game.ResolveDestruction(actor, owner, equipment ? FieldZone.Equipment : FieldZone.Attached, index);

    public static CommandResult ResolveDestruction(this GameEngine game, int actor, int owner, FieldZone zone, int index = -1)
    {
        var source = game.State.PendingDecision?.Source ?? "";
        // Bomba chooses zones: a hand card is drawn at random, so naming its position is refused.
        return Answer(game, actor, source, c => c.Field is { } t && t.Owner == owner && t.Zone == zone &&
            (t.Index == index || source != CardCatalog.TimeBomb.Id && zone is FieldZone.Equipment or FieldZone.Bonus),
            "No te corresponde elegir objetivo.", FieldDestruction.Target);
    }

    public static CommandResult ResolveDeckSearch(this GameEngine game, int actor, int deckIndex) =>
        Answer(game, actor, CardCatalog.KonamyCode.Id, c => c.Value == deckIndex, "No te corresponde buscar una carta en el mazo.");

    public static CommandResult ChooseMindResetTarget(this GameEngine game, int actor, int target) =>
        Answer(game, actor, CardCatalog.MindReset.Id, c => target >= 0 && c.Value == target, "Elige un jugador con cartas en la mano.", MindResetEffect.Target);

    public static CommandResult ChooseMindResetMemory(this GameEngine game, int actor, int owner, int index) =>
        Answer(game, actor, CardCatalog.MindReset.Id, c => c.Owner == owner && c.Value == index, "Elige dos cartas distintas de cualquier memoria.", MindResetEffect.Memory);

    /// <summary>The hand card, or finishing without one (-1) when no target hand or no non-skill is left.</summary>
    public static CommandResult ResolveMindReset(this GameEngine game, int actor, int handIndex)
    {
        var step = game.State.PendingDecision?.Step;
        return Answer(game, actor, CardCatalog.MindReset.Id, c => c.Owner == null && c.Value == handIndex && (step == MindResetEffect.Hand || handIndex == -1),
            "No te corresponde resolver Resetear la Mente.");
    }

    public static CommandResult ResolveMemoryRecovery(this GameEngine game, int actor, int owner, int memoryIndex) =>
        Answer(game, actor, CardCatalog.FloppyDisk.Id, c => c.Owner == owner && c.Value == memoryIndex, "No te corresponde recuperar una carta de memoria.");

    public static CommandResult ResolveTotem(this GameEngine game, int actor, bool cancelDamage) =>
        Answer(game, actor, CardCatalog.Totem.Id, c => (c.Value == 1) == cancelDamage, "No te corresponde decidir la protección del Tótem.");

    /// <summary>Shows these Tótems: the decision chooses how many, so the copies must be distinct playable Tótems.</summary>
    public static CommandResult ResolveGoldenTotem(this GameEngine game, int actor, IEnumerable<int> handIndices)
    {
        var selected = handIndices.ToArray();
        var playable = game.PlayableTotems(actor).ToHashSet();
        if (selected.Length > 2 || selected.Distinct().Count() != selected.Length || !selected.All(playable.Contains) ||
            game.State.Players[actor].Equipment == null && selected.Length == 0)
            return CommandResult.Fail("Muestra hasta dos Tótems de Salvación distintos de tu mano; Tótem de Oro necesita un equipo debajo.");
        return Answer(game, actor, CardCatalog.GoldenTotem.Id, c => c.Value == selected.Length, "No te corresponde colocar Tótem de Oro.");
    }

    public static CommandResult ResolveZahiraTarget(this GameEngine game, int actor, int owner, FieldZone zone, int index) =>
        Answer(game, actor, CardCatalog.ZahiraFlair.Id, c => c.Field is { } t && t.Owner == owner && t.Zone == zone && t.Index == index,
            "No te corresponde elegir dónde colocar Zahira Flair.");

    public static CommandResult ResolveBlueEggDraw(this GameEngine game, int actor, bool pay) =>
        Answer(game, actor, CardCatalog.BlueEgg.Id, c => c.Field == null && c.Value == (pay ? 1 : 0), "No te corresponde decidir el robo de Huevo Azul Explosivo.");
}

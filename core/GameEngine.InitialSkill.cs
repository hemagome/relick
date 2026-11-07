namespace Relick.Core;

public sealed partial class GameEngine
{
    public string[] InitialSkillsFor(int actor) => actor is < 0 or > 1 || State.Phase != Phase.Preparation ||
        State.Players[actor].Deck.Count < 5 || State.Players[actor].Hand.Count != 0 || State.Players[actor].InitialSkillId != null ? [] :
        State.Players[actor].Deck.Where(id => CardCatalog.Get(id).Kind == CardKind.Skill).Distinct().Order(StringComparer.Ordinal).ToArray();

    public CommandResult ChooseInitialSkill(int actor, string skillId)
    {
        if (!InitialSkillsFor(actor).Contains(skillId)) return CommandResult.Fail("Elige una habilidad disponible en tu mazo antes del mulligan.");
        var player = State.Players[actor];
        player.Deck.Remove(skillId); player.InitialSkillId = skillId;
        player.Hand.Add(skillId); CardDrawn?.Invoke(actor, 0);
        Shuffle(player.Deck);
        for (var i = 0; i < 4; i++) DrawOne(player);
        Log($"{player.Name} elige su habilidad inicial y recibe otras cuatro cartas.");
        return CommandResult.Ok();
    }
}

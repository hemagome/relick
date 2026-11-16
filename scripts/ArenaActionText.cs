using Relick.Core;

namespace Relick;

// Client copy is independent of the server's diagnostic labels and option indices.
internal static class ArenaActionText
{
    public static string Label(DuelOption option, DuelSnapshot s) => option.Move.Kind switch
    {
        DuelMoveKind.Learn => "Enseñar",
        DuelMoveKind.Attack => "Atacar",
        DuelMoveKind.PlayCode => HandCard(option, s)?.CancelActivation == true ? "Anular" : HandCard(option, s)?.IsCombo == true ? "Activar" : "Jugar",
        DuelMoveKind.Equip => "Equipar",
        DuelMoveKind.Exchange => "Canjear",
        DuelMoveKind.Discard or DuelMoveKind.TurnBuffDiscard => "Descartar",
        DuelMoveKind.Advance => s.Phase switch
        {
            Phase.Draw => s.ExtraDrawStages > 0 ? "Repetir robo" : "Ir a combate",
            Phase.Combat => s.ExtraCombatStages > 0 ? "Repetir combate" : "Ir a acciones",
            Phase.Actions => s.ExtraActionStages > 0 ? "Repetir acciones" : "Finalizar turno",
            _ => s.ExtraFinalStages > 0 ? "Repetir final" : "Finalizar turno"
        },
        DuelMoveKind.AttackDie or DuelMoveKind.EffectDie => option.Move.Flag ? $"Relanzar con {RelikarioCatalog.DisplayNameOf(s.Players.ElementAtOrDefault(s.MySeat)?.RelikarioId)}" : "Conservar",
        DuelMoveKind.LearningDie => "Relanzar con Reverse",
        DuelMoveKind.Dexterity => "Destreza · +1",
        DuelMoveKind.Pc => "Usar PC",
        DuelMoveKind.Pendant => option.Move.Value switch { 0 => "Relanzar", 1 => "Sumar +1", _ => "Sumar +2" },
        DuelMoveKind.PassDefense or DuelMoveKind.EffectDamage => "Recibir daño",
        DuelMoveKind.BoxDefense => "Cancelar daño",
        DuelMoveKind.DestinyDefense => "Defender",
        DuelMoveKind.ProtectDestruction => option.Move.Index < 0 ? "Permitir" : "Sacrificar",
        DuelMoveKind.Magnet => option.Move.Flag ? "Tomar Bonus" : "Tomar carta",
        DuelMoveKind.SkipMagnet => "No tomar carta",
        DuelMoveKind.CodeResponse => option.Move.Flag ? "Anular con Caja" : "Dejar resolver",
        DuelMoveKind.PassError => "Dejar resolver",
        DuelMoveKind.Points => option.Move.Flag ? "Aumentar PR" : "Restar PR",
        DuelMoveKind.Destroy => "Destruir",
        DuelMoveKind.BlueEggDraw => option.Move.Flag ? $"Robar 1 carta · {GameEngine.BlueEggDrawCost} PR" : "Continuar sin robar",
        DuelMoveKind.Placement => option.Move.Value == (int)CardDestination.Exchange ? "Canjear" : "Al mazo",
        DuelMoveKind.Reward => option.Move.Value == (int)RelikiaReward.OpponentDiscards ? "Provocar descartes" : "Corromper códigos",
        DuelMoveKind.Corrupt => "Corromper",
        DuelMoveKind.Recycle => "Reciclar",
        DuelMoveKind.SearchDeck => "Añadir a mano",
        DuelMoveKind.MindResetTarget => option.Move.Target == s.MySeat ? "Mi mano" : "Mano rival",
        DuelMoveKind.MindResetMemory => "Elegir",
        DuelMoveKind.MindResetHand => "Devolver al mazo",
        DuelMoveKind.RecoverMemory => "Recuperar",
        DuelMoveKind.FieldCode => "Activar",
        DuelMoveKind.Sniper => option.Move.Flag ? "Equipar" : "Canjear",
        DuelMoveKind.Totem => option.Move.Flag ? "Cancelar daño" : "Recibir daño",
        DuelMoveKind.GoldenTotem => "Colocar",
        DuelMoveKind.TrapCraft => option.Label,
        DuelMoveKind.Relikia => option.Move.Value == (int)RelikiaEffect.ReplayCode ? "Activar" : RelikiaEffects.Get((RelikiaEffect)option.Move.Value).Name,
        DuelMoveKind.Evolve => "Evolucionar",
        DuelMoveKind.Concede => "Abandonar duelo",
        _ => option.Label
    };

    public static CardDefinition? HandCard(DuelOption option, DuelSnapshot s) => option.Move.Index >= 0 &&
        option.Move.Index < s.Players[s.MySeat].Hand.Length && s.Players[s.MySeat].Hand[option.Move.Index] is { } id ? CardCatalog.Get(id) : null;

    public static string CardCost(CardDefinition card) => card.Kind switch
    {
        CardKind.Attack when card.Id == CardCatalog.SuperAttack.Id => "Canje de dos Ataques",
        CardKind.Code when card.Id == CardCatalog.SuperMagnet.Id => "Canje de un Equipo y otra carta",
        CardKind.Code when card.Id == CardCatalog.ZahiraFlair.Id => "Canje de dos Personajes",
        CardKind.Code => Resources(card.ActionCost, card.PrCost, card.DiscardCost),
        CardKind.Equipment => "1 acción", CardKind.Attack => "Consume 1 Ataque",
        CardKind.Skill => "Según el espacio de habilidad elegido", _ => ""
    };

    public static string Cost(DuelOption option, DuelSnapshot s)
    {
        var m = option.Move;
        switch (m.Kind)
        {
            case DuelMoveKind.PlayCode:
                var card = HandCard(option, s)!;
                return Resources(card.ActionCost, GameEngine.CodePrCost(card, s.LegacyBlueEgg), card.DiscardCost);
            case DuelMoveKind.Equip: case DuelMoveKind.Exchange: return "1 acción";
            case DuelMoveKind.Learn:
                var p = s.Players[s.MySeat];
                var position = Enumerable.Range(0, GameEngine.MaxSkills).FirstOrDefault(i => !Enumerable.Range(0, p.Skills.Length).Any(j => p.SkillPosition(j) == i));
                return Resources(0, GameEngine.SkillSlotCosts[position]);
            case DuelMoveKind.Relikia:
                var effect = RelikiaEffects.Get((RelikiaEffect)m.Value);
                return Resources(effect.Actions, effect.Cost + (effect.Id == RelikiaEffect.ReplayCode ? GameEngine.CodePrCost(CardCatalog.Get(s.Players[s.MySeat].Memory[m.Index]), s.LegacyBlueEgg) : 0),
                    effect.Id == RelikiaEffect.ReplayCode ? CardCatalog.Get(s.Players[s.MySeat].Memory[m.Index]).DiscardCost : 0);
            case DuelMoveKind.FieldCode: case DuelMoveKind.Sniper: return "Sin coste adicional";
            case DuelMoveKind.Attack: return HandCard(option, s)?.Id == CardCatalog.SuperAttack.Id
                ? "Consume Súper Ataque" : "Consume 1 Ataque";
            case DuelMoveKind.Dexterity: return "Consume 1 ficha";
            case DuelMoveKind.Pc: case DuelMoveKind.Pendant: case DuelMoveKind.BoxDefense: return "Destruye el equipo";
            case DuelMoveKind.CodeResponse when m.Flag: return "Destruye Caja de Cartón";
            case DuelMoveKind.ProtectDestruction when m.Index >= 0: return "Destruye la habilidad elegida";
            case DuelMoveKind.Totem when m.Flag: return s.Players[s.MySeat].EquipmentCovers?.Contains(CardCatalog.GoldenTotem.Id) == true ? "Destruye Tótem de Oro" : "Destruye 1 Tótem";
            case DuelMoveKind.DestinyDefense: return $"{option.RequiredDiscards} descartes · +1 contador";
            default: return "";
        }
    }

    private static string Resources(int actions, int pr, int discards = 0)
    {
        var parts = new List<string>();
        if (actions > 0) parts.Add(actions == 1 ? "1 acción" : $"{actions} acciones");
        if (pr > 0) parts.Add($"{pr} PR");
        if (discards > 0) parts.Add(discards == 1 ? "1 descarte" : $"{discards} descartes");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Sin coste";
    }
}

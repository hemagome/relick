using Relick.Core;

namespace Relick;

// Client copy is independent of the server's diagnostic labels and option indices.
internal static class ArenaActionText
{
    public static string Label(DuelOption option, DuelSnapshot s) => option.Move.Kind switch
    {
        DuelMoveKind.Learn => "Enseñar",
        DuelMoveKind.RevealGhostDog => "Revelar para la próxima etapa de acciones",
        DuelMoveKind.Attack when option.Field?.Zone == FieldZone.TomeCard =>
            $"Atacar con {CardCatalog.Get(s.Players[s.MySeat].Skills[option.Move.Value]).Name}",
        DuelMoveKind.Attack => "Atacar",
        DuelMoveKind.PlayCode when s.Phase == Phase.ErrorResponse && HandCard(option, s)?.Id == CardCatalog.CopyHelmet.Id => "Casco de Copias",
        DuelMoveKind.PlayCode when s.Phase == Phase.ErrorResponse && HandCard(option, s)?.Id == CardCatalog.ComboParry.Id => "Combo Parry",
        DuelMoveKind.PlayCode => HandCard(option, s)?.CancelActivation == true ? "Anular" : HandCard(option, s)?.IsCombo == true ? "Activar" : "Jugar",
        DuelMoveKind.Equip when option.Move.Flag => "Equipar al rival",
        DuelMoveKind.Equip when HandCard(option, s)?.Id == CardCatalog.MetalHood.Id => "Equiparme",
        DuelMoveKind.Equip => "Equipar",
        DuelMoveKind.UsePurpleRing => "Ganar 500 PR",
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
        DuelMoveKind.Choose => option.Button ?? option.Label,
        DuelMoveKind.BlueEggDraw => option.Move.Flag ? $"Robar 1 carta · {GameEngine.BlueEggDrawCost} PR" : "Continuar sin robar",
        DuelMoveKind.Placement => option.Move.Value == (int)CardDestination.Exchange ? "Canjear" : "Al mazo",
        DuelMoveKind.Reward => option.Move.Value == (int)RelikiaReward.OpponentDiscards ? "Provocar descartes" : "Corromper códigos",
        DuelMoveKind.Corrupt => "Corromper",
        DuelMoveKind.DestinyPickMemory => "Activar Púa del Destino",
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
        DuelMoveKind.RelikarioSearch => "Buscar con Osamu",
        DuelMoveKind.Concede => "Abandonar duelo",
        _ => option.Label
    };

    public static CardDefinition? HandCard(DuelOption option, DuelSnapshot s)
    {
        var id = option.Field?.Zone == FieldZone.TomeCard ? option.CardId :
            s.Players[s.MySeat].Hand.ElementAtOrDefault(option.Move.Index);
        return id == null ? null : CardCatalog.Get(id);
    }

    public static string CardCost(CardDefinition card) => card.Kind switch
    {
        CardKind.Attack when card.Id == CardCatalog.SuperAttack.Id => "Canje de dos Ataques",
        CardKind.Code when card.Id == CardCatalog.SuperMagnet.Id => "Canje de un Equipo y otra carta",
        CardKind.Code when card.Id == CardCatalog.ZahiraFlair.Id => "Canje de dos Personajes",
        CardKind.Code when card.Id == CardCatalog.VictoriousManualControl.Id => "1 acción · 1 vida",
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
                if (card.Id == CardCatalog.VictoriousManualControl.Id) return CardCost(card);
                return Resources(card.ActionCost, GameEngine.CodePrCost(card, s.LegacyBlueEgg), card.DiscardCost);
            case DuelMoveKind.Equip: case DuelMoveKind.Exchange: case DuelMoveKind.RelikarioSearch:
            case DuelMoveKind.DestinyPickMemory: case DuelMoveKind.RevealGhostDog: return "1 acción";
            case DuelMoveKind.UsePurpleRing:
            case DuelMoveKind.Choose when s.Decision?.Source == CardCatalog.PurpleRing.Id && option.Field != null:
                return "Destruye Anillo Morado";
            case DuelMoveKind.Learn:
                var p = s.Players[s.MySeat];
                var position = Enumerable.Range(0, GameEngine.MaxSkills).FirstOrDefault(i => !Enumerable.Range(0, p.Skills.Length).Any(j => p.SkillPosition(j) == i));
                return Resources(0, GameEngine.SkillSlotCosts[position]);
            case DuelMoveKind.Relikia:
                var effect = RelikiaEffects.Get((RelikiaEffect)m.Value);
                var replayed = effect.Id == RelikiaEffect.ReplayCode ? CardCatalog.Get(s.Players[s.MySeat].Memory[m.Index]) : null;
                var resources = Resources(effect.Actions, effect.Cost + (replayed == null ? 0 : GameEngine.CodePrCost(replayed, s.LegacyBlueEgg)),
                    replayed?.DiscardCost ?? 0);
                return replayed?.Id == CardCatalog.VictoriousManualControl.Id ? resources + " · 1 vida" : resources;
            case DuelMoveKind.FieldCode: case DuelMoveKind.Sniper: return "Sin coste adicional";
            case DuelMoveKind.Attack: return HandCard(option, s)?.Id == CardCatalog.SuperAttack.Id
                ? "Consume Súper Ataque" : "Consume 1 Ataque";
            case DuelMoveKind.Dexterity: return "Consume 1 ficha";
            case DuelMoveKind.Pc: case DuelMoveKind.Pendant: case DuelMoveKind.BoxDefense: return "Destruye el equipo";
            case DuelMoveKind.CodeResponse when m.Flag: return "Destruye Caja de Cartón";
            case DuelMoveKind.ProtectDestruction when m.Index >= 0: return "Destruye la habilidad elegida";
            case DuelMoveKind.Totem when m.Flag:
            case DuelMoveKind.Choose when s.Decision?.Source == CardCatalog.Totem.Id && option.Field != null:
                return s.Players[s.MySeat].EquipmentCovers?.Contains(CardCatalog.GoldenTotem.Id) == true ? "Destruye Tótem de Oro" : "Destruye 1 Tótem";
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

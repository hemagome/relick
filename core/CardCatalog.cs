namespace Relick.Core;

// Legacy MVP subtypes. Target taxonomy and unresolved card mappings: docs/CARD_TAXONOMY.md.
public enum CodeType { None, Character, Object, Play }

public enum CardKind { Attack, Skill, Code, Attachment, Equipment }

/// <summary>Only cards supplied by the user; no sample cards in the playable catalogue.</summary>
public sealed record CardDefinition(
    string Id, string Name, CardKind Kind, string Text,
    int NormalDamage = 0, int CriticalDamage = 0, int Reward = 0,
    int Heal = 0, int Draw = 0, CodeType CodeType = CodeType.None, bool HealFromDie = false, bool ChangePointsFromDie = false, int PrCost = 0, bool DestroyNonSkill = false, bool IsCombo = false, int ActionCost = 1, bool RerollOwnDie = false, bool CancelActivation = false,
    int DamageReduction = 0, bool DestroyOnAttackOne = false, bool DestroyChosenZone = false, bool SearchNonSkill = false,
    bool SwapHands = false, int EarliestTurn = 0, int SetOwnDie = 0, bool ResetMind = false, bool DodgeAttack = false, bool StoreDie = false, int DamageBonus = 0, int DamageCounterLimit = 0, bool EquipOnDiscard = false, int SearchAttacks = 0);

public static class CardCatalog
{
    public static readonly CardDefinition TripleAttack = new("triple-cross-attack", "Triple Ataque Cruzado", CardKind.Code,
        "Busca 3 ataques de tu mazo a tu mano.", CodeType: CodeType.Play, SearchAttacks: 3);
    public static readonly CardDefinition Sniper = new("sniper-scope", "Mira de Sniper", CardKind.Equipment,
        "Si esta carta es descartada: equipa esta carta gratis o coloca en canje. Equipada: +1 a los daños. Si recibes daño coloca 1 contador en esta carta; con 3 contadores se destruye.", DamageBonus: 1, DamageCounterLimit: 3, EquipOnDiscard: true);
    public static readonly CardDefinition Pc = new("pc-8bits", "PC 8BITS", CardKind.Equipment,
        "Al equipar: lanza 1D6 y colócalo sobre esta carta. Al lanzar un dado puedes destruir este equipo: si guardó 1–3, suma ese valor al dado (máx. 5); si guardó 4–6, cambia el resultado del dado por el valor guardado.", StoreDie: true);
    public static readonly CardDefinition Dodge = new("dodge", "Esquivar", CardKind.Code,
        "COMBO. Si tu oponente te está atacando, lanza 1D6: 1–2–3: roba 1 carta; 4–5–6: niega el ataque.",
        CodeType: CodeType.Play, IsCombo: true, ActionCost: 1, DodgeAttack: true);
    public static readonly CardDefinition MindReset = new("mind-reset", "Resetear la Mente", CardKind.Code,
        "COMBO. Selecciona 2 cartas de cualquier memoria y 1 no-habilidad de la mano (mira la mano) de un jugador objetivo. Envía esas cartas al mazo de su respectivo dueño, baraja y luego el jugador objetivo roba una carta.",
        CodeType: CodeType.Play, IsCombo: true, ActionCost: 1, ResetMind: true);
    public static readonly CardDefinition Agility = new("agility-100", "Agilidad al 100%", CardKind.Code,
        "COMBO. Cambia el resultado de uno de tus dados a 5.",
        CodeType: CodeType.Play, IsCombo: true, ActionCost: 1, SetOwnDie: 5);
    public static readonly CardDefinition MindSwap = new("mind-swap", "Cambio de Mentes", CardKind.Code,
        "No puedes activar esta carta en el primer turno del juego. Cambia todas las cartas de tu mano (mín. 0) por las de tu oponente.",
        CodeType: CodeType.Play, SwapHands: true, EarliestTurn: 2);
    public static readonly CardDefinition KonamyCode = new("konamy-code", "Código Konamy", CardKind.Code,
        "Busca en tu mazo una no-habilidad y añádela a tu mano.", CodeType: CodeType.Play, SearchNonSkill: true);
    public static readonly CardDefinition TimeBomb = new("time-bomb", "Bomba de Tiempo", CardKind.Code,
        "Escoge una de estas opciones: zona de equipos, mano al azar o zona Bonus. Destruye una carta de ese lugar.",
        CodeType: CodeType.Object, DestroyChosenZone: true);
    public static readonly CardDefinition DiamondHelmet = new("diamond-helmet", "Casco de Diamantes", CardKind.Equipment,
        "Resta 2 puntos a cualquier daño que recibas. Si en el dado de ataque alguien saca 1: destruye esta carta.",
        DamageReduction: 2, DestroyOnAttackOne: true);
    public static readonly CardDefinition Attack = new("attack", "Ataque", CardKind.Attack,
        "Consume esta carta para atacar con una habilidad enseñada.");
    public static readonly CardDefinition Magnet = new("magnet-shuffle", "Relick Magnet Shuffle", CardKind.Skill,
        "Daño normal: 1D6 · Crítico: 5 · Premio: 500 PR. Al realizar un crítico puedes mirar la mano del adversario y tomar una carta de ella o de su Bonus.", 0, 5, 500);
    public static readonly CardDefinition Destiny = new("destiny-shuffle", "Relick Destiny Shuffle", CardKind.Skill,
        "Daño normal: 3 · Crítico: 5 · Premio: 600 PR. Coloca 1 contador sobre esta copia y descarta 3 cartas para cancelar el daño de un ataque (4 cartas si es crítico). Con 2 contadores esta copia no puede volver a activar el efecto.", 3, 5, 600);
    public static readonly CardDefinition Senshuni = new("senshuni", "Senshuni", CardKind.Code,
        "Roba 2 cartas.", Draw: 2, CodeType: CodeType.Character);
    public static readonly CardDefinition Pendant = new("lucky-pendant", "Colgante de la Suerte", CardKind.Equipment,
        "Cuando está equipada, puedes destruirla y elegir: relanzar un dado; o, si el dado fue relanzado, sumar +1 o +2. Al atacar, la suma no puede alcanzar 6. Destruir envía el equipo a la memoria.");

    public static readonly CardDefinition CardboardBox = new("cardboard-box", "Caja de Cartón", CardKind.Equipment,
        "Si está equipada, puedes destruirla para cancelar el daño de un ataque o cancelar la activación de un código de tipo objeto y enviarlo a la memoria de su dueño.");

    public static readonly CardDefinition ExtraLife = new("extra-life", "Vida Extra", CardKind.Code,
        "Lanza 1D6 y gana: 1 → 1 vida; 2 → 2 vidas; 3, 4 o 5 → 3 vidas; 6 → 4 vidas. Sin superar el máximo de tu Relikario.",
        CodeType: CodeType.Object, HealFromDie: true);

    public static readonly CardDefinition PointsIncrease = new("points-increase", "Aumento de Relick Points", CardKind.Code,
        "Lanza 1D6: aumenta o disminuye los PR de cualquier jugador: 1 → 300; 2 → 400; 3–4 → 500; 5 → 1000; 6 → 1500. Los PR no bajan de cero.",
        CodeType: CodeType.Play, ChangePointsFromDie: true);

    public static readonly CardDefinition BlueEgg = new("blue-egg", "Huevo Azul Explosivo", CardKind.Code,
        "Paga 200 PR. Destruye una no-habilidad en el campo.", CodeType: CodeType.Object, PrCost: 200, DestroyNonSkill: true);
    public static readonly CardDefinition Reroll = new("reroll", "Re-lanza", CardKind.Code,
        "COMBO. Activar esta carta no cuenta como acción. Relanza uno de tus dados. Si sale 1, 2 o 3, roba 1 carta.", CodeType: CodeType.Play, IsCombo: true, ActionCost: 0, RerollOwnDie: true);

    public static readonly CardDefinition LoadError = new("load-error", "Error de Carga", CardKind.Code,
        "COMBO. Cuando un código se esté activando o resolviendo, o un equipo esté siendo equipado: cancélalo y envíalo a memoria. La activación anulada recupera sus costes.", CodeType: CodeType.Play, IsCombo: true, CancelActivation: true);

    public static readonly IReadOnlyDictionary<string, CardDefinition> All =
        new[] { Attack, Magnet, Destiny, Pendant, Senshuni, CardboardBox, ExtraLife, PointsIncrease, BlueEgg, Reroll, LoadError, DiamondHelmet, TimeBomb, KonamyCode, MindSwap, Agility, MindReset, Dodge, Pc, Sniper, TripleAttack }.ToDictionary(c => c.Id);

    public static CardDefinition Get(string id) => All[id];

    public static List<string> TrainingDeck() =>
        Enumerable.Repeat(Attack.Id, 12)
            .Concat(new[] { TripleAttack.Id, MindReset.Id, Agility.Id, Pc.Id, Magnet.Id, Destiny.Id, Sniper.Id,
                Pendant.Id, DiamondHelmet.Id, Senshuni.Id, Dodge.Id, CardboardBox.Id, KonamyCode.Id, ExtraLife.Id, MindSwap.Id, PointsIncrease.Id, TimeBomb.Id, BlueEgg.Id, Reroll.Id, LoadError.Id }).ToList();
}


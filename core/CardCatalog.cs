namespace Relick.Core;

// Subtypes referenced by the official rules and by effects such as Caja de Cartón.
public enum CodeType { None, Character, Object, Play, Trap }

public enum CardKind { Attack, Skill, Code, Attachment, Equipment, Area }

/// <summary>
/// Only cards supplied by the user; no sample cards in the playable catalogue. A card whose rules live in an
/// <see cref="ICardEffect"/> (core/Effects) needs no rule flag here. HealFromDie stays only as a hint for the card icon
/// and the test bot; the engine no longer reads it.
/// </summary>
public sealed record CardDefinition(
    string Id, string Name, CardKind Kind, string Text,
    int NormalDamage = 0, int CriticalDamage = 0, int Reward = 0,
    int Heal = 0, int Draw = 0, CodeType CodeType = CodeType.None, bool HealFromDie = false, int PrCost = 0, bool IsCombo = false, int ActionCost = 1, bool RerollOwnDie = false, bool CancelActivation = false,
    int DamageReduction = 0, bool DestroyOnAttackOne = false,
    int EarliestTurn = 0, int SetOwnDie = 0, bool StoreDie = false, int DamageBonus = 0, int DamageCounterLimit = 0, bool EquipOnDiscard = false,
    int DamagePerReroll = 0, int MaxNormalDamage = 0, bool ProtectOwnCard = false, bool DrawOnExpensiveLearn = false,
    bool PreventDamage = false, bool DestroyAtNextTurnStart = false, bool DiscardForTurnBuff = false, bool FieldCombatDie = false,
    int DiscardCost = 0, bool RepeatStage = false, bool Stackable = false,
    // Deckbuilding data: null affinity means the card fits any Relikario. Special marks the printed "S" skills.
    string? Relikario = null, bool Special = false, bool LightEquipment = false)
{
    public string? ArtworkLabel { get; init; }

    public int NormalDamageAt(int counters) => MaxNormalDamage > 0 ? Math.Min(MaxNormalDamage, NormalDamage + counters * DamagePerReroll) : NormalDamage + counters * DamagePerReroll;
    public int CriticalDamageAt(int counters) => CriticalDamage + counters * DamagePerReroll;
    public string NormalDamageText(int counters = 0) => Id switch
    {
        "magnet-shuffle" => "1D6",
        "turbo-sword" => "1D6−1 · mín. 2",
        _ => NormalDamageAt(counters).ToString()
    };
    public string CriticalDamageText(int counters = 0) => Id switch
    {
        "turbo-sword" => "1D6+1 · mín. 4",
        "turbo-overlap" => "2D6 · elige uno",
        _ => CriticalDamageAt(counters).ToString()
    };
}

public static class CardCatalog
{
    public static readonly CardDefinition SuperAttack = new("super-attack", "Súper Ataque", CardKind.Attack,
        "Canjea dos Ataques para crearlo en tu mano. Se considera un Ataque y no cuesta acción. Al atacar con esta carta, suma +2 al D6 de ataque (máximo 5) y +2 al daño del ataque.",
        CodeType: CodeType.Trap, ActionCost: 0, DamageBonus: 2);
    public static readonly CardDefinition ZahiraFlair = new("zahira-flair", "Zahira Flair", CardKind.Code,
        "Canjea dos Personajes. Colócala encima de una carta en el campo, que pierde sus efectos. En la etapa final del adversario, su dueño lanza 1D6. 1–2: destruye Zahira Flair. 3–4: muévela encima de otra carta del campo. 5–6: el Relikario debajo pierde 2 vidas, o se destruye la otra carta que esté debajo.",
        CodeType: CodeType.Trap, ActionCost: 0);
    public static readonly CardDefinition SuperMagnet = new("super-magnet", "Súper Imán", CardKind.Code,
        "Canjea un Equipo y cualquier otra carta para crearlo en la zona de equipos. Se considera un equipo. Cuando un efecto rival vaya a afectar una o más de tus cartas en el campo, sustituye ese efecto por colocar 1 contador sobre Súper Imán. Con 3 o más contadores, destruye esta carta. Si sale del campo, queda fuera del juego.",
        CodeType: CodeType.Trap, ActionCost: 0);
    public const int SuperMagnetCounterLimit = 3;
    public static readonly CardDefinition GalacticShield = new("escudo-galactico", "Escudo Galáctico", CardKind.Code,
        "Coloca esta carta encima de tu Relikario y pon debajo las 3 cartas del tope de tu mazo boca abajo. Antes de recibir daño, añade a tu mano una de esas cartas y resta 2 al daño. Si otra carta encima de tu Relikario fuera a salir del campo, puedes enviar una de esas cartas a tu memoria para evitarlo.",
        CodeType: CodeType.Object);
    public static readonly CardDefinition VictoriousManualControl = new("control-manual-victorioso", "Control Manual Victorioso", CardKind.Code,
        "Paga 1 punto de vida: mueve una carta en tu campo a una de tus zonas que esté vacía y que sea compatible.",
        CodeType: CodeType.Object);
    public static readonly CardDefinition GoldenTotem = new("golden-totem", "Tótem de Oro", CardKind.Code,
        "Muestra hasta 2 Tótems de Salvación de tu mano y colócalos en la zona de equipos. Coloca esta carta encima de tu equipo. Necesita al menos un equipo debajo. Si un Tótem de Salvación equipado fuera a ser destruido, destruye esta carta en su lugar. No puede activarse con Relikia.",
        CodeType: CodeType.Character);
    public static readonly CardDefinition Totem = new("salvation-totem", "Tótem de Salvación", CardKind.Equipment,
        "Si el daño te llevaría a 0 vidas, puedes cancelarlo y destruir un Tótem de Salvación equipado. Al equipar otra copia, apílala sobre este equipo. Con dos o más copias, destruye una si recibes daño y todos tus fallos de ataque infligen daño normal.", Stackable: true);
    public static readonly CardDefinition TimeBender = new("time-bender", "Doblador de Tiempo", CardKind.Code,
        "COMBO. Descarta 2 cartas: al terminar la etapa actual de tu turno, juégala una vez más. La repetición de Combate permite volver a aprender, atacar y usar efectos de etapa; la de Acciones concede dos acciones nuevas.",
        CodeType: CodeType.Object, IsCombo: true, DiscardCost: 2, RepeatStage: true);
    public static readonly CardDefinition ReloadCartridge = new("reload-cartridge", "Cartucho de Re-carga", CardKind.Code,
        "No puedes usar esta carta en el primer turno del juego. Ambos jugadores descartan sus manos (mín. 1) y roban 4 cartas.",
        Draw: 4, CodeType: CodeType.Play, EarliestTurn: 2);
    public static readonly CardDefinition QuickLoad = new("quick-load", "Carga Rápida", CardKind.Code,
        "Roba 2 cartas. Luego lanza 1D6: 1–2: descarta 2 cartas; 3–4: descarta 1 carta; 5: roba 1 carta; 6: roba 2 cartas.",
        Draw: 2, CodeType: CodeType.Play);
    public static readonly CardDefinition Jessica = new("jessica-lee", "Jessica Lee", CardKind.Code,
        "Una vez en tu etapa de combate, si esta carta está en tu zona de canje/Bonus, puedes lanzar 1D6: 5: 2 daños al adversario; 6: 3 daños al adversario y tú robas 1 carta. Error de Carga solo puede cancelarla en el turno en que entra en juego.",
        CodeType: CodeType.Character, ActionCost: 0, FieldCombatDie: true);
    public static readonly CardDefinition PlumberStar = new("plumber-star", "Estrella del Fontanero", CardKind.Code,
        "COMBO. Descarta 1 carta para activar hasta el final del turno: no recibes daño y duplicas todo el daño que inflijan tus cartas o habilidades. Error de Carga cancela antes del descarte; Caja de Cartón lo conserva.",
        CodeType: CodeType.Object, IsCombo: true, DiscardForTurnBuff: true, DiscardCost: 1);
    public static readonly CardDefinition FloppyDisk = new("floppy-disk", "Disquete", CardKind.Code,
        "Selecciona una carta de cualquier memoria y añádela a tu mano. No puede recuperar la copia que se está activando; sí otra copia de Disquete.", CodeType: CodeType.Object);
    public static readonly CardDefinition Bonfire = new("bonfire", "Hoguera", CardKind.Equipment,
        "Al equipar: gana +1 vida. Mientras esta carta esté equipada, todos los daños que recibas se transforman en 0. Destruye esta carta al inicio de tu siguiente turno luego de haber sido equipada.",
        Heal: 1, PreventDamage: true, DestroyAtNextTurnStart: true);
    public static readonly CardDefinition Reverse = new("reverse-shuffle", "Relick Reverse Shuffle", CardKind.Skill,
        "Al relanzar un dado propio: +1 al daño normal (máx. 5) y crítico de esta copia. Si una carta propia va a ser destruida, puedes destruir esta habilidad enseñada en su lugar, incluidos costes de equipos. Al enseñarla en el espacio de 500 o 1000 PR: lanza D6, puedes relanzarlo hasta dos veces y roba la mitad del resultado final, redondeada hacia arriba.",
        NormalDamage: 2, CriticalDamage: 4, Reward: 600, DamagePerReroll: 1, MaxNormalDamage: 5, ProtectOwnCard: true, DrawOnExpensiveLearn: true, Relikario: RelikarioCatalog.Yuno.Id);
    public static readonly CardDefinition TripleAttack = new("triple-cross-attack", "Triple Ataque Cruzado", CardKind.Code,
        "Busca 3 ataques de tu mazo a tu mano.", CodeType: CodeType.Play);
    public static readonly CardDefinition Sniper = new("sniper-scope", "Mira de Sniper", CardKind.Equipment,
        "Si esta carta es descartada: equipa esta carta gratis o coloca en canje. Equipada: +1 a los daños. Si recibes daño coloca 1 contador en esta carta; con 3 contadores se destruye.", DamageBonus: 1, DamageCounterLimit: 3, EquipOnDiscard: true);
    public static readonly CardDefinition Pc = new("pc-8bits", "PC 8BITS", CardKind.Equipment,
        "Al equipar: lanza 1D6 y colócalo sobre esta carta. Al lanzar un dado puedes destruir este equipo: si guardó 1–3, suma ese valor al dado (máx. 5); si guardó 4–6, cambia el resultado del dado por el valor guardado.", StoreDie: true);
    public static readonly CardDefinition Dodge = new("dodge", "Esquivar", CardKind.Code,
        "COMBO. Si tu oponente te está atacando, lanza 1D6: 1–2–3: roba 1 carta; 4–5–6: niega el ataque.",
        CodeType: CodeType.Play, IsCombo: true, ActionCost: 1);
    public static readonly CardDefinition MindReset = new("mind-reset", "Resetear la Mente", CardKind.Code,
        "COMBO. Selecciona 2 cartas de cualquier memoria y 1 no-habilidad de la mano (mira la mano) de un jugador objetivo. Envía esas cartas al mazo de su respectivo dueño, baraja y luego el jugador objetivo roba una carta.",
        CodeType: CodeType.Play, IsCombo: true, ActionCost: 1);
    public static readonly CardDefinition Agility = new("agility-100", "Agilidad al 100%", CardKind.Code,
        "COMBO. Cambia el resultado de uno de tus dados a 5.",
        CodeType: CodeType.Play, IsCombo: true, ActionCost: 1, SetOwnDie: 5);
    public static readonly CardDefinition MindSwap = new("mind-swap", "Cambio de Mentes", CardKind.Code,
        "No puedes activar esta carta en el primer turno del juego. Cambia todas las cartas de tu mano (mín. 0) por las de tu oponente.",
        CodeType: CodeType.Play, EarliestTurn: 2);
    public static readonly CardDefinition KonamyCode = new("konamy-code", "Código Konamy", CardKind.Code,
        "Busca en tu mazo una no-habilidad y añádela a tu mano.", CodeType: CodeType.Play);
    public static readonly CardDefinition Ellie = new("ellie", "Ellie", CardKind.Code,
        "Busca en tu mazo 2 cartas con la palabra «vida» o «vidas» excepto «Ellie» y añádelas a tu mano.",
        CodeType: CodeType.Character);
    public static readonly CardDefinition Robotize = new("robotizar", "Robotizar", CardKind.Code,
        "Coloca esta carta encima de tu Relikario. Mientras permanezca ahí, resta 1 al daño que recibas. Al inicio de tu etapa de acción, envía la carta del tope de tu mazo al canje o, si es un Código, puedes añadirla a tu mano. Si sacas 1 en 1D6, destruye esta carta.",
        CodeType: CodeType.Play);
    public static readonly CardDefinition ComboParry = new("combo-parry", "Combo Parry", CardKind.Code,
        "COMBO. Tu adversario revela su mano y envía a su mazo todas las cartas COMBO de su mano. Luego baraja su mazo. No se pueden activar COMBO en respuesta a esta carta.",
        CodeType: CodeType.Play, IsCombo: true);
    public static readonly CardDefinition DestinyPick = new("pua-del-destino", "Púa del Destino", CardKind.Code,
        "COMBO. Escoge uno: roba 1 carta, haz 1 punto de daño no letal a tu adversario o corrompe 1 carta de cualquier memoria. Como una acción en tu etapa de acciones, puedes corromper esta carta de tu memoria para usar su efecto.",
        CodeType: CodeType.Object, IsCombo: true);
    public static readonly CardDefinition TimeBomb = new("time-bomb", "Bomba de Tiempo", CardKind.Code,
        "Escoge una de estas opciones: zona de equipos, mano al azar o zona Bonus. Destruye una carta de ese lugar.",
        CodeType: CodeType.Object);
    public static readonly CardDefinition TakeEquipment = new("dame-ese-equipo", "¡Dame ese equipo!", CardKind.Code,
        "Selecciona un equipo en la zona de equipos del adversario y equípatelo tú. Luego, coloca 1 carta de Ataque de tu mazo debajo de tu Relikario y revuelve el mazo.",
        CodeType: CodeType.Play);
    public static readonly CardDefinition EquipmentRaid = new("robo-de-equipos", "Misión: Robo de Equipos", CardKind.Area,
        "Lado A: cuando llega a ser el Área, puedes colocar 1 Ataque de la memoria del adversario debajo de tu Relikario. Añade ¡Dame ese equipo! de tu mazo o memoria a tu mano.\nLado B: cuando llega a ser el Área, equípate un Equipo de tu mazo. Si tu adversario añade a su campo o mano un Equipo del que eres dueño original, él gana 1500 PR, mira tu mano y añade una de esas cartas a su mano. Luego, destruye esta Área.");
    public static readonly CardDefinition DiamondHelmet = new("diamond-helmet", "Casco de Diamantes", CardKind.Equipment,
        "Resta 2 puntos a cualquier daño que recibas. Si en el dado de ataque alguien saca 1: destruye esta carta.",
        DamageReduction: 2, DestroyOnAttackOne: true);
    public static readonly CardDefinition MetalHood = new("capirote-metalico", "Capirote Metálico", CardKind.Equipment,
        "Puedes equipar esta carta a tu adversario. Mientras esté equipada, quien la lleva recibe 2 menos de daño y tiene 1 acción menos por turno. Si quien la lleva saca 1 en un dado, destruye esta carta.",
        DamageReduction: 2);
    public static readonly CardDefinition PurpleRing = new("anillo-morado", "Anillo Morado", CardKind.Equipment,
        "Equipo ligero. Puedes destruir esta carta para ganar 500 PR o, cuando recibas daño, recibir solo la mitad de ese daño.",
        LightEquipment: true);
    public static readonly CardDefinition Attack = new("attack", "Ataque", CardKind.Attack,
        "Consume esta carta para atacar con una habilidad enseñada.");
    public static readonly CardDefinition AttackYuno = Attack with { Id = "attack-yuno-ns41", ArtworkLabel = "Yuno (NS-41/150)" };
    public static readonly CardDefinition AttackOsamu = Attack with { Id = "attack-osamu-ns42", ArtworkLabel = "Osamu (NS-42/150)" };
    public static readonly CardDefinition AttackYunoDt = Attack with { Id = "attack-yuno-dt72", ArtworkLabel = "Yuno (DT-72/100)" };
    public static readonly CardDefinition AttackOsamuDt = Attack with { Id = "attack-osamu-dt73", ArtworkLabel = "Osamu (DT-73/100)" };
    public static readonly CardDefinition Magnet = new("magnet-shuffle", "Relick Magnet Shuffle", CardKind.Skill,
        "Daño normal: 1D6 · Crítico: 5 · Premio: 500 PR. Al realizar un crítico puedes mirar la mano del adversario y tomar una carta de ella o de su Bonus.", 0, 5, 500, Relikario: RelikarioCatalog.Yuno.Id);
    public static readonly CardDefinition Destiny = new("destiny-shuffle", "Relick Destiny Shuffle", CardKind.Skill,
        "Daño normal: 3 · Crítico: 5 · Premio: 600 PR. Coloca 1 contador sobre esta copia y descarta 3 cartas para cancelar el daño de un ataque (4 cartas si es crítico). Con 2 contadores esta copia no puede volver a activar el efecto.", 3, 5, 600, Relikario: RelikarioCatalog.Yuno.Id);
    public static readonly CardDefinition TurboSword = new("turbo-sword", "Relick Turbo Sword", CardKind.Skill,
        "Antes de recibir daño, lanza 1D6. Con 5, cancela el daño y pon 1 contador en esta copia. Con 6, cancela el daño y el oponente recibe la mitad. Con 2 contadores o más, esta copia no activa su efecto.",
        Reward: 500, Relikario: RelikarioCatalog.Osamu.Id);
    public static readonly CardDefinition TurboOverlap = new("turbo-overlap", "Relick Turbo Overlap", CardKind.Skill,
        "Al enseñar esta carta o realizar un crítico con ella, añade una Agilidad al 100% de tu mazo o memoria a tu mano.",
        NormalDamage: 3, Reward: 300, Relikario: RelikarioCatalog.Osamu.Id);
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
        CodeType: CodeType.Play);

    public static readonly CardDefinition BlueEgg = new("blue-egg", "Huevo Azul Explosivo", CardKind.Code,
        "Selecciona y destruye una no-habilidad en el campo. Además puedes pagar 200 PR para robar 1 carta.",
        CodeType: CodeType.Object);
    public static readonly CardDefinition Reroll = new("reroll", "Re-lanza", CardKind.Code,
        "COMBO. Activar esta carta no cuenta como acción. Relanza uno de tus dados. Si sale 1, 2 o 3, roba 1 carta.", CodeType: CodeType.Play, IsCombo: true, ActionCost: 0, RerollOwnDie: true);

    public static readonly CardDefinition LoadError = new("load-error", "Error de Carga", CardKind.Code,
        "COMBO. Cuando un código se esté activando o cuando un equipo esté siendo equipado: cancélalo y envíalo a la memoria.", CodeType: CodeType.Play, IsCombo: true, CancelActivation: true);

    public static readonly IReadOnlyDictionary<string, CardDefinition> All =
        new[] { Attack, AttackYuno, AttackOsamu, AttackYunoDt, AttackOsamuDt, Magnet, Destiny, Reverse, TurboSword, TurboOverlap, Pendant, Senshuni, CardboardBox, ExtraLife, PointsIncrease, BlueEgg, Reroll, LoadError, DiamondHelmet, TimeBomb, KonamyCode, MindSwap, Agility, MindReset, Dodge, Pc, Sniper, TripleAttack, Bonfire, FloppyDisk, PlumberStar, Jessica, QuickLoad, ReloadCartridge, TimeBender, Totem, GoldenTotem, SuperMagnet, ZahiraFlair, SuperAttack, TakeEquipment, EquipmentRaid, PurpleRing, GalacticShield, VictoriousManualControl, Ellie, Robotize, ComboParry, MetalHood, DestinyPick }.ToDictionary(c => c.Id);

    public static CardDefinition Get(string id) => All[id];

    public static List<string> TrainingDeck() =>
        Enumerable.Repeat(Attack.Id, 12)
            .Concat(new[] { TripleAttack.Id, MindReset.Id, Agility.Id, Pc.Id, Magnet.Id, Destiny.Id, Reverse.Id, Sniper.Id,
                Pendant.Id, DiamondHelmet.Id, Senshuni.Id, Dodge.Id, CardboardBox.Id, KonamyCode.Id, ExtraLife.Id, MindSwap.Id, PointsIncrease.Id, TimeBomb.Id, BlueEgg.Id, Reroll.Id, LoadError.Id, Bonfire.Id, FloppyDisk.Id, PlumberStar.Id, Jessica.Id, QuickLoad.Id, ReloadCartridge.Id, TimeBender.Id, Totem.Id, GoldenTotem.Id }).ToList();

    /// <summary>Trap deck that accompanies <see cref="TrainingDeck"/>; it is separate from the 42 main cards.</summary>
    public static List<string> TrainingTraps() => [SuperMagnet.Id, ZahiraFlair.Id, SuperAttack.Id];
}


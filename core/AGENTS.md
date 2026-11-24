# core/AGENTS.md · Motor de reglas

`core/` es C# puro: sin Godot, sin red, sin reflexión, sin E/S. Lo compilan el cliente (`Relick.csproj`), el servidor (`server/spacetimedb/StdbModule.csproj`) y las pruebas (`tests/Relick.Core.Tests.csproj`). Si algo de Godot se cuela aquí, falla la compilación del servidor.

Las reglas generales están en el [AGENTS.md raíz](../AGENTS.md). Este archivo dice dónde está cada cosa y cómo reutilizarla.

## Fuentes de verdad

| Qué | Dónde |
|---|---|
| Definiciones de cartas, `CardKind`, `CodeType`, mazo de desarrollo | [CardCatalog.cs](CardCatalog.cs): `CardDefinition`, `CardCatalog.All`, `TrainingDeck()`, `TrainingTraps()` |
| Definiciones de Relikarios, niveles, evolución, lista del constructor | [RelikarioCatalog.cs](RelikarioCatalog.cs): `RelikarioDefinition`, `All`, `Playable`, `Roster`, `EvolutionFor` |
| Efectos de Relikia (cristales) | [RelikiaEffects.cs](RelikiaEffects.cs) y [GameEngine.Relikia.cs](GameEngine.Relikia.cs) |
| Clasificación de categorías | [docs/CARD_TAXONOMY.md](../docs/CARD_TAXONOMY.md) |
| Reglas de mazo, afinidad, Ataque por defecto | [DeckRules.cs](DeckRules.cs) |
| Estado de partida, fases, dados | [GameState.cs](GameState.cs): `GameState`, `PlayerState`, `Phase`, `DiePurpose`, `IGameRandom` |
| Zonas y objetivos en el campo | `FieldZone` y `FieldTarget` en [GameEngine.Codes.cs](GameEngine.Codes.cs) |
| Combate, daño base, turno | [GameEngine.cs](GameEngine.cs) (`Attack`, `PlayCode`, `DamageFor`…) y los parciales `GameEngine.*.cs` |
| Registro de módulos de carta y operaciones permitidas | [Decisions.cs](Decisions.cs): `CardRules`, `CardEffect`, interfaces de disparador, `CardEffects.Registered`, `GameEngine.EffectContext` |
| Decisiones del jugador | `PendingDecision`, `DecisionKind`, `DecisionChoice` en [Decisions.cs](Decisions.cs) |
| Disparadores y su orden | `TurnMoment`, `DamageStep`, `LeaveStep` en [Decisions.cs](Decisions.cs); `IExchangeTrigger` en [GameEngine.ExchangeTriggers.cs](GameEngine.ExchangeTriggers.cs); diseño en [docs/CARD_TRIGGERS.md](../docs/CARD_TRIGGERS.md) |
| Respuestas en cadena (COMBO, Error de Carga) | [GameEngine.ComboResponses.cs](GameEngine.ComboResponses.cs), [GameEngine.LoadError.cs](GameEngine.LoadError.cs), [GameEngine.TargetResponses.cs](GameEngine.TargetResponses.cs); [docs/RAM.md](../docs/RAM.md) |
| Pausas y reanudación | [EngineContinuations.cs](EngineContinuations.cs): `EngineContinuation`, `ContinuationKind` |
| Títulos de los dados | [DiePurposes.cs](DiePurposes.cs) |
| Jugadas y protocolo | [DuelProtocol.cs](DuelProtocol.cs): `DuelMoveKind`, `DuelMove`, `DuelOpening`, `DuelRandom`, `DuelReplay`, `DuelJson` |
| Opciones que el servidor ofrece | [DuelOptions.cs](DuelOptions.cs): `DuelOptions.For(game, seat)` |
| Serialización del estado | [EngineSnapshot.cs](EngineSnapshot.cs): `SaveSnapshot`, `GameEngine.FromSnapshot` |
| Vista filtrada por jugador | [DuelProjection.cs](DuelProjection.cs): `DuelSnapshot.For(game, seat, revision)` |
| Señales visuales públicas e historial | [DuelVisualEvents.cs](DuelVisualEvents.cs), [DuelHistory.cs](DuelHistory.cs) |
| Reloj de turno, competiciones, cosméticos | [TurnClock.cs](TurnClock.cs), [CompetitiveRules.cs](CompetitiveRules.cs), [CosmeticCatalog.cs](CosmeticCatalog.cs), [ProfileCatalog.cs](ProfileCatalog.cs), [RewardCatalog.cs](RewardCatalog.cs) |

## Cómo se ejecuta una carta

1. **Datos comunes** en `CardDefinition`: costes (`ActionCost`, `PrCost`, `DiscardCost`, `EarliestTurn`), `IsCombo`, daños, premio, afinidad (`Relikario`), `Special`, `LightEquipment`, `Stackable`. El motor los lee para cualquier carta.
2. **Reglas propias** en un módulo de `core/Effects/` registrado en `CardEffects.Registered` ([Decisions.cs](Decisions.cs)):
   - `CardEffect` si el jugador la activa (`Resolve`, y opcionalmente `Unavailable`, `Choices`, `Choose`, `Continue`, `HoldsActivationCard`).
   - `CardRules` si solo reacciona (equipos, Áreas, habilidades con acción propia), más las interfaces que use.
3. **Interfaces opcionales** que el motor consulta: `ITurnTrigger`, `IDamageTrigger`, `IFieldLeaveReplacement`, `IExchangeTrigger`, `IDeckSearchTrigger`, `ITargetResponse`, `ISkillAction`, `IAttackDamageEffect`, `IAttackDamageRoll`, `IAttackRewardRoll`.
4. **Operaciones**: un módulo solo cambia la partida a través de `GameEngine.EffectContext` (robar, dañar, destruir con sus sustituciones, preguntar, tirar un dado, mover cartas…). Así consume el azar y aplica las sustituciones igual que el resto del motor.

## Ejemplos existentes por patrón

| Patrón | Carta | Archivo |
|---|---|---|
| Sin módulo: solo datos | Ataque y sus variantes de ilustración; Game | `Attack`, `AttackYuno…`, `Game` en [CardCatalog.cs](CardCatalog.cs) |
| Activación simple | Senshuni (roba 2) | [Effects/SenshuniEffect.cs](Effects/SenshuniEffect.cs) |
| Activación con dado | Esquivar | [Effects/DodgeEffect.cs](Effects/DodgeEffect.cs) |
| Activación con objetivo en el campo (`ChooseTarget`) y pasos | ¡Dame ese equipo! | [Effects/TakeEquipmentEffect.cs](Effects/TakeEquipmentEffect.cs) |
| Elección privada en el mazo (`ChooseCard`) | Código Konamy | [Effects/KonamyCodeEffect.cs](Effects/KonamyCodeEffect.cs) |
| Decisión de varios pasos (`Picks`, `ShowsHandOf`) | Resetear la Mente | [Effects/MindResetEffect.cs](Effects/MindResetEffect.cs) |
| Continuo: modifica el daño mientras está equipada | Hoguera, Casco de Diamantes, Capirote Metálico | [Effects/GuardEquipmentEffect.cs](Effects/GuardEquipmentEffect.cs) (`IDamageTrigger`, paso `Guard`) |
| Disparado por un momento del turno | Vehículo Moto-Ratón | [Effects/MouseBikeEffect.cs](Effects/MouseBikeEffect.cs) (`ITurnTrigger`, `TurnStart`) |
| Sustitución al salir del campo | Escudo Galáctico, Tótem de Oro | [Effects/GalacticShieldEffect.cs](Effects/GalacticShieldEffect.cs), [Effects/GoldenTotemEffect.cs](Effects/GoldenTotemEffect.cs) |
| Disparado al llegar al canje | Habitación Secreta | [Effects/SecretRoomEffect.cs](Effects/SecretRoomEffect.cs) (`IExchangeTrigger`) |
| Respuesta a un efecto que elige objetivo | Sin Opciones | [Effects/NoOptionsEffect.cs](Effects/NoOptionsEffect.cs) (`ITargetResponse`) |
| Habilidad con acción propia y tiradas de daño | Relick Rolling Shuffle | [Effects/RollingShuffleEffect.cs](Effects/RollingShuffleEffect.cs) |
| Un módulo parametrizado para varias cartas | Letras R, E, L, I, C, K; equipos de guarda | [Effects/LetterEffect.cs](Effects/LetterEffect.cs), [Effects/GuardEquipmentEffect.cs](Effects/GuardEquipmentEffect.cs) |
| Lógica compartida entre módulos | Destrucción con sustituciones | [Effects/FieldDestruction.cs](Effects/FieldDestruction.cs) |

## Reutilizar antes de crear

**Principio: reutiliza un comportamiento existente antes de escribir uno nuevo.**

1. **Busca efectos parecidos** por verbo y por concepto, en inglés y en español:
   ```bash
   grep -rn "Draw(\|Heal(\|Destroy\|TakeFromMemory\|SearchDeckCardToHand" core/Effects
   grep -n "public .*(" core/Decisions.cs        # operaciones de EffectContext
   grep -rln "IDamageTrigger\|ITurnTrigger\|IFieldLeaveReplacement" core/Effects
   ```
   Busca también el texto de reglas de cartas parecidas en `CardCatalog.cs` y su sección en `assets/cards/README.md`.
2. **¿Se puede parametrizar?** Si dos cartas solo difieren en números o en la carta que nombran, usa un módulo con constructor, como `GuardEquipmentEffect` (instancias estáticas) o `LetterEffect` (recibe la definición). Si difieren en un dato que el motor ya lee (`Draw`, `ActionCost`…), usa ese dato.
3. **¿Falta una operación?** Añádela a `EffectContext` con un nombre genérico que no mencione la carta, y que delegue en el motor existente. Si dos módulos repiten la misma secuencia, extráela a un archivo compartido de `core/Effects/` como `FieldDestruction`.
4. **¿Cuándo un módulo específico está justificado?** Cuando el texto impreso combina pasos que ninguna otra carta comparte. Un módulo por carta es la norma del proyecto; lo que no debe repetirse es la lógica de las operaciones.
5. **No anticipes cartas futuras.** No crees interfaces, enums ni parámetros que solo use una carta hipotética. Generaliza cuando exista la segunda carta real.
6. **Preserva la compatibilidad.** Si cambias una operación compartida, ejecuta todas las pruebas de las cartas que la usan (`grep -rn "NombreOperacion" core/Effects tests`) y no cambies el orden del azar ni los textos del historial sin necesidad.

## Relikario frente a carta

| | Carta | Relikario |
|---|---|---|
| Definición | `CardDefinition` en `CardCatalog` | Un `RelikarioDefinition` por nivel en `RelikarioCatalog` (`EvolvesTo`, `BaseForm`, `EvolutionCost`) |
| Registro | `CardCatalog.All` (+ `CardEffects.Registered` si tiene módulo) | `All`, `Playable` (forma base) y la entrada de `Roster` |
| Reglas propias | Módulo en `core/Effects/` | Datos comunes (`CanRerollAttack`, `AttackDieBonus…`, `EvolutionHealing`…) y, si no basta, un parcial `GameEngine.<Nombre>.cs` |
| Acciones del jugador | La activación genérica (`PlayCode`, `Equip`…) | Un valor nuevo en `DuelMoveKind` (como `IronwingRoll`, `WdBoost`), su opción en `DuelOptions` y su texto en `DuelHistory` |
| Decisiones | `Choices`/`Choose` del módulo | Despacho por `decision.Source` en `DecisionChoices`/`ApplyDecision` de [Decisions.cs](Decisions.cs) (aún no hay registro de módulos de Relikario) |
| Ciclo de vida | Mano → campo/memoria/canje | Siempre en juego; evoluciona durante el duelo ([GameEngine.Evolution.cs](GameEngine.Evolution.cs)) |
| Presentación | Imagen `assets/cards/<id>-clean.png` | Carpeta `assets/characters/<familia>/`, retrato animado, proyectil y música propia |

Guía completa: [docs/ADDING_RELIKARIO.md](../docs/ADDING_RELIKARIO.md).

## Cuidado con

- `DuelMoveKind`, `Phase`, `DecisionKind` y `DiePurpose` viajan por red y se guardan: añade valores al final o donde fijen las reglas, documenta el motivo y no reordenes los existentes sin evaluar la compatibilidad ([docs/CARD_EFFECTS.md](../docs/CARD_EFFECTS.md#compatibilidad)).
- El orden dentro de un `TurnMoment` lo fija `Order`; el de la cadena de daño, el enum `DamageStep`. La prueba que lo fija está en `tests/TurnTriggerTests.cs`.
- Una decisión no guarda sus opciones: se recalculan en cada vista. `Choices` debe ser una función pura del estado.
- La información privada (mano, mazo, búsquedas) solo puede aparecer en las opciones de quien decide; el texto `Public` de cada opción es lo único que entra al historial.

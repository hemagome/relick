# Añadir una carta

Guía para que un desarrollador o un agente de IA añada una carta siguiendo los patrones actuales del proyecto. Antes de empezar, lee la [clasificación de cartas](CARD_TAXONOMY.md), que es la especificación de categorías, y [Efectos de carta](CARD_EFFECTS.md) y [Disparadores](CARD_TRIGGERS.md), que explican la arquitectura de reglas.

## Principios

- **Las reglas viven en `core/`**, en C# puro, sin Godot ni red. El servidor compila los mismos archivos, así que una carta nueva no necesita cambios en `server/`.
- **Una carta con reglas propias es un módulo** en `core/Effects/`. No añadas ramas `if (card.Id == …)` al motor ni indicadores de una sola carta a `CardDefinition`.
- **`EffectContext` solo ofrece operaciones genéricas** (robar, dañar, destruir, preguntar, tirar un dado…). Si te falta una, añádela con un nombre que no mencione la carta.
- **Las elecciones del jugador son decisiones** (`PendingDecision`) que la interfaz presenta sola. No crees una fase nueva en `Phase` para una carta.
- **No hay compatibilidad con partidas guardadas.** Si cambias la forma del estado, se vacían las salas y partidas de la base al publicar ([Compatibilidad](CARD_EFFECTS.md#compatibilidad)).

## 1. Reunir los datos de la carta

Pide o consulta, por cada carta:

| Dato | Ejemplo |
|---|---|
| Nombre impreso y código de colección | Esquivar · LR-27/82 |
| Categoría (`CardKind`) | `Attack`, `Skill`, `Code`, `Attachment`, `Equipment`, `Area` |
| Subtipo de Código (`CodeType`) | `Play`, `Object`, `Character`, `Trap` |
| ¿COMBO? | Se puede jugar como respuesta |
| Costes | Acciones (`ActionCost`, 1 por defecto), PR (`PrCost`), descartes (`DiscardCost`), turno mínimo (`EarliestTurn`) |
| Afinidad | Genérica, o de un Relikario (`Relikario`) |
| Habilidades | Daño normal y crítico, premio en PR, habilidad «S» (`Special`) |
| Equipos | Normal o ligero (`LightEquipment`), apilable (`Stackable`) |
| Texto de reglas exacto | Como en la carta impresa; se muestra en el inspector |
| Imagen | Escaneo o foto de la carta, para restaurarla |

Si el texto es ambiguo, pregunta antes de programar. Indica qué interpretación tomas y anótala en la sección de la carta en [assets/cards/README.md](../assets/cards/README.md).

## 2. Definirla en el catálogo

En [core/CardCatalog.cs](../core/CardCatalog.cs):

1. Añade un `public static readonly CardDefinition` con un `Id` estable en kebab-case (por ejemplo `"mind-swap"`). El `Id` también nombra la imagen y no debe cambiar después.
2. Rellena solo los datos que el motor lee de forma común: categoría, subtipo, costes, COMBO, afinidad, daños y premio. El texto va en `Text`.
3. Añádela al diccionario `All`.
4. Si debe estar en el mazo de desarrollo, añádela a `TrainingDeck()`. Ese mazo tiene 42 cartas, así que habrá que quitar otra. Los Códigos Trampa van en `TrainingTraps()`.

Un **Ataque con otra ilustración** no es una carta nueva mecánicamente. Se declara como variante:

```csharp
public static readonly CardDefinition AttackOsamuDt = Attack with
    { Id = "attack-osamu-dt73", ArtworkLabel = "Osamu (DT-73/100)", Relikario = RelikarioCatalog.Osamu.Id };
```

## 3. Ilustración

- Guarda la imagen limpia en `assets/cards/<id>-clean.png`. La cargan `BoardArt.LoadCardArtwork` y `SkillArtwork` por convención de nombre, sin registro.
- Respeta la proporción de la carta impresa: 697 × 987 en vertical, o 987 × 697 en horizontal, como las habilidades. Conserva márgenes, textos impresos y créditos, sin bordes nuevos.
- Si restauras la imagen (quitar marcas de agua, reconstruir zonas), documenta la procedencia y el prompt en [assets/cards/README.md](../assets/cards/README.md), como las cartas existentes.
- Godot genera el archivo `.import` al abrir el proyecto en el editor, o con `godot --headless --path . --import`. Añade la imagen y su `.import` al mismo commit.

## 4. Reglas

### ¿Necesita módulo?

| Caso | Qué hacer |
|---|---|
| Habilidad o Ataque sin efecto extra | Nada más: los datos del catálogo bastan |
| Código o efecto que hace algo al resolverse | Módulo `CardEffect` |
| Carta que reacciona a algo de la partida (equipo, Área, trampa en juego…) | Módulo `CardRules` que implementa un disparador |

### Módulo de activación (`CardEffect`)

Crea `core/Effects/<Nombre>Effect.cs` y regístralo en la lista `Registered` de `CardEffects`, en [core/Decisions.cs](../core/Decisions.cs). Solo `CardId` y `Resolve` son obligatorios:

```csharp
internal sealed class MindSwapEffect : CardEffect
{
    public override string CardId => CardCatalog.MindSwap.Id;
    public override void Resolve(GameEngine.EffectContext ctx) => ctx.SwapHands();
}
```

Los demás miembros son opcionales:

- `Unavailable(ctx, excludedHand)`: por qué no se puede activar ahora, además de los costes comunes; `null` si se puede.
- `Choices` y `Choose`: las opciones y la respuesta de una decisión que la carta abrió con `ctx.Ask(...)`.
- `Continue(ctx, step, next)`: cómo sigue tras una pausa, ya sea un dado (`ctx.Roll(purpose, step)`) o una operación que se puede sustituir (`ctx.Then(step)`). Los pasos son cadenas constantes, porque se guardan en la partida.

Buenos ejemplos para copiar:

- [DodgeEffect](../core/Effects/DodgeEffect.cs): tirada de dado.
- [KonamyCodeEffect](../core/Effects/KonamyCodeEffect.cs): búsqueda privada en el mazo.
- [MindResetEffect](../core/Effects/MindResetEffect.cs): decisión de varios pasos.
- [TimeBombEffect](../core/Effects/TimeBombEffect.cs) y [FieldDestruction](../core/Effects/FieldDestruction.cs): destrucción con sustituciones.

### Decisiones y su presentación

`ctx.Ask(step, kind, instruction, …)` abre una decisión. Lo que verá el jugador depende del tipo:

| `DecisionKind` | Presentación |
|---|---|
| `Confirm` | Un botón por opción. Si una opción tiene `Field`, esa carta se ilumina en el tablero |
| `ChooseTarget` | El jugador elige primero la carta en el tablero (cada opción lleva `Field`) |
| `ChooseCard` | Galería de cartas (opciones con `CardId`; `Owner` para memorias) |

Cada `DecisionChoice` lleva:

- `Label`: el texto completo de la opción.
- `Button`: el texto corto del botón.
- `Public`: lo que verá el rival en el historial, sin revelar cartas privadas.

Una decisión con una sola opción se resuelve sola, salvo que esa opción tenga `RequiresAcknowledgement`. `allowsCombos: true` permite que el rival responda con COMBO mientras se espera la decisión.

### Dados

Si la carta lanza un dado con un propósito nuevo, añade el valor a `DiePurpose` (en `core/GameState.cs`) y su título y carta a la tabla de [core/DiePurposes.cs](../core/DiePurposes.cs).

### Cartas que reaccionan (disparadores)

Derivan de `CardRules`, que no tiene activación, e implementan la familia que corresponda. El motor las recorre en el orden que fijan las reglas:

| Interfaz | Momento | Ejemplo |
|---|---|---|
| `ITurnTrigger` | Inicio de turno, inicio de acciones, etapa final | [MouseBikeEffect](../core/Effects/MouseBikeEffect.cs), [RobotizeEffect](../core/Effects/RobotizeEffect.cs), [ZahiraFlairEffect](../core/Effects/ZahiraFlairEffect.cs) |
| `IDamageTrigger` | Un paso de la cadena de daño (`DamageStep`) | [GuardEquipmentEffect](../core/Effects/GuardEquipmentEffect.cs), [PurpleRingEffect](../core/Effects/PurpleRingEffect.cs), [SalvationTotemEffect](../core/Effects/SalvationTotemEffect.cs) |
| `IFieldLeaveReplacement` | Una carta va a salir del campo | [GalacticShieldEffect](../core/Effects/GalacticShieldEffect.cs), [GoldenTotemEffect](../core/Effects/GoldenTotemEffect.cs) |

Si la carta encaja en un paso que aún no existe, añade el valor al enum del paso (`TurnMoment`, `DamageStep` o `LeaveStep`) en la posición que dicten las reglas. Después actualiza la prueba que fija el orden, en `tests/TurnTriggerTests.cs`. Las reglas que el motor todavía no expone como disparador (Mira de Sniper al descartarse, Reverse Shuffle, Estrella del Fontanero) están en [Disparadores](CARD_TRIGGERS.md).

## 5. Interfaz

Normalmente no hay que tocarla: el inspector muestra `Text`, y las decisiones y opciones se presentan solas. Solo hace falta en casos concretos:

- **Una animación propia**, en `scripts/OnlineArena.Animations.cs` y `scripts/ArenaEffects.cs`.
- **Un coste visible especial en el botón** de una opción, en `scripts/ArenaActionText.cs`.
- **Que una opción que gasta una carta propia se ilumine como activación**, en `IsFieldActivation` de `scripts/OnlineArena.Targets.cs`.

## 6. Pruebas

1. Crea `tests/<Nombre>Tests.cs` con una propiedad `Cases` y añádela a la lista de `tests/Program.cs`. Prepara la partida con `GameEngine.FromOpening(new DuelOpening { … }, new ScriptedRandom())`. `ScriptedRandom.EnqueueDie(n)` fija los dados.
2. Comprueba al menos:
   - el efecto principal;
   - cada rama del texto;
   - lo que impide activarla;
   - las respuestas (Error de Carga, COMBO);
   - guardar y recargar a mitad de una decisión (`GameEngine.FromSnapshot(game.SaveSnapshot(), random)`);
   - que el rival no ve información privada (`DuelSnapshot.For(game, rival, …)`).
3. Para responder decisiones en las pruebas, usa `game.ResolveDecision(actor, índice)` o los atajos de [scripts/Tests/DecisionCommands.cs](../scripts/Tests/DecisionCommands.cs). Añade uno ahí si tu carta lo necesita.
4. Si tiene presentación propia, añade una comprobación en `scripts/Tests/` y regístrala en la tabla `checks` de `OnlineLobby.UiTests.cs`; debe pasar también ejecutada sola (`./pruebas.sh --checks=Nombre`).

## 7. Verificar

```bash
./pruebas.sh
dotnet build server/spacetimedb/StdbModule.csproj
```

Ambas deben pasar. El módulo del servidor compila el mismo `core/`, así que un fallo ahí suele ser código de Godot que se coló en el motor.

## Lista de comprobación

- [ ] Datos de la carta confirmados, y las dudas anotadas.
- [ ] `CardDefinition` en `CardCatalog`, en `All` y, si procede, en `TrainingDeck()`.
- [ ] `assets/cards/<id>-clean.png` con su `.import`, y la procedencia documentada.
- [ ] Módulo en `core/Effects/` registrado en `CardEffects`, si tiene reglas propias.
- [ ] Ningún `if` por identificador nuevo en el motor y ninguna fase nueva en `Phase`.
- [ ] Pruebas del motor nuevas en `tests/Program.cs`, y de interfaz si hay presentación propia.
- [ ] Motor, servidor e interfaz en verde.

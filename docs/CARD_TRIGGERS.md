# Disparadores de carta

Cómo reacciona una carta a algo que ocurre en la partida sin que el jugador la active. Complementa [Efectos de carta](CARD_EFFECTS.md), que cubre la activación. Las interfaces y los enums están en `core/Decisions.cs`; el registro, en `CardEffects`.

## Principios

- **Interfaces pequeñas.** Una carta registrada implementa solo las familias que usa, además de `CardRules` (o de `CardEffect` si también se activa). Un equipo o un Área que solo reacciona deriva de `CardRules`, así que el motor nunca lo resuelve como un código.
- **Orden explícito.** El orden entre familias y dentro de cada una lo fijan las reglas mediante enums (`TurnMoment`, `DamageStep`, `LeaveStep`) y `Order`, nunca el orden de registro.
- **Pausa y reanudación serializables.** Un disparador puede pedir una decisión o lanzar un dado. La cadena se guarda en ese punto mediante `EngineContinuation` y `PendingDecision`, y continúa después de reconectar.
- **Motor puro.** Sin reflexión ni descubrimiento dinámico: el registro es la lista explícita de `CardEffects.Registered`.

## Familias

| Interfaz | Momento | Cartas |
|---|---|---|
| `ITurnTrigger` | Un momento del turno (`TurnMoment`: `TurnStart`, `ActionStart`, `FinalStage`), en su `Order` | Vehículo Moto-Ratón (`TurnStart`), Perro Fantasma (`ActionStart`, 0), Robotizar (`ActionStart`, 1), Zahira Flair (`FinalStage`) |
| `IDamageTrigger` | Un paso de la cadena de daño (`DamageStep`) | Ver la tabla de daño |
| `IFieldLeaveReplacement` | Una carta va a salir del campo (`FieldLeave`: destruir, devolver o corromper), por pasos (`LeaveStep`) | Escudo Galáctico (`Protect`), Tótem de Oro (`Cover`) |
| `IExchangeTrigger` | Una carta llega al canje | Habitación Secreta |
| `IDeckSearchTrigger` | Termina una búsqueda que trajo cartas del mazo a la mano | Relick Turbo Attack |
| `ITargetResponse` | Un efecto rival elige al jugador o una carta suya | Sin Opciones |
| `ISkillAction` | Acción propia de una habilidad enseñada | Relick Gold Digger, Relick Rolling Shuffle |
| `IAttackDamageEffect`, `IAttackDamageRoll`, `IAttackRewardRoll` | Cálculo del daño o del premio de un ataque | Relick Wild Aim, Relick Rolling Shuffle, Relick Turbo Attack |

### Momentos del turno

`GameEngine.TurnTriggers.cs` resuelve un momento así:

1. `RunTurnTriggers(moment, seat, from)` dispara los disparadores del momento, en su `Order`, hasta que uno devuelve `true`: ha empezado algo que volverá más tarde.
2. **Vuelta por continuación** (Robotizar): el disparador encadena sus pasos con `ctx.Then(...)` y, al terminar, llama a `ctx.FinishTurnTrigger(moment)`, que dispara los siguientes o vuelve a `CompletePendingPlay`.
3. **Vuelta por activación** (Perro Fantasma, Zahira Flair): cada copia se resuelve como una activación completa, que vuelve por `CompletePendingPlay`. Mientras `State.ActionStartActor` está puesto, `CompletePendingPlay` vuelve a ejecutar el momento desde el primer disparador.
4. **Límite:** la reentrada empieza siempre por el primer disparador, así que un disparador que ya terminó debe devolver `false` si lo vuelven a disparar. Cada disparador guarda su propio avance (por ejemplo, las copias reveladas de Perro Fantasma en `PlayerState.GhostDogReveals`).

**Inicio de acciones:** Perro Fantasma (cartas reveladas en mano, de menor a mayor índice) y después Robotizar (copias sobre el Relikario). `tests/TurnTriggerTests.cs` fija este orden.

### Cadena de daño

`ResumeDamage` (`core/GameEngine.cs`) recorre `DamageStep` en orden. Cada paso consulta sus fuentes en el momento, porque un paso anterior puede haber cambiado el campo. `DamageEvent` lleva el destinatario, la cantidad y el dado de ataque. `Modify` cambia el daño sin pausar; `TryPause` puede detener la cadena y reanudarla con `EffectContext.ContinueDamage`. Desde `Respond`, si no queda daño, la cadena termina.

| Paso | Quién actúa | `DamageStep` |
|---|---|---|
| 1 | Límite de daño no letal; Estrella del Fontanero del atacante duplica | `Amplify` (motor) |
| 2 | Estrella del Fontanero del defensor: daño 0 | `Prevent` (motor) |
| 3 | Hoguera, Casco de Diamantes, Capirote Metálico | `Guard` (`GuardEquipmentEffect`) |
| 4 | Turbo Sword: dado de defensa | `Respond` (`TurboSwordEffect`) |
| 5 | Robotizar: −1 por copia | `Reduce` (`RobotizeEffect`) |
| 6 | Anillo Morado: decisión | `Absorb` (`PurpleRingEffect`) |
| 7 | Escudo Galáctico: carta a mano, −2 | `Shield` (`GalacticShieldEffect`) |
| 8 | Límite no letal; Mira de Sniper: contadores | `Counters` (motor y `SniperScopeEffect`) |
| 9 | Tótem de Salvación: cancelar daño letal | `Lethal` (`SalvationTotemEffect`, decisión) |
| 10 | Vidas del Relikario | motor, al terminar |

### Salida del campo

`RequestDestruction` y `ReturnFieldCard` consultan la misma cadena (`TryReplaceFieldLeave`). Escudo Galáctico protege lo que está sobre el Relikario tanto si se destruye como si vuelve a la mano; si declina, la salida sigue desde el paso siguiente. Tótem de Oro se destruye en lugar de un Tótem de Salvación.

## Lo que todavía resuelve el motor

Estas reglas no son módulos y su lógica vive en `GameEngine.*.cs`:

- **Estrella del Fontanero**, porque es un estado del jugador y no una carta en juego.
- **Reverse Shuffle**, una habilidad con fase propia (`DestructionResponse`) y su comando de protección.
- **Mira de Sniper al descartarse**, con su fase `SniperChoice`, lanzada desde varios caminos de descarte.
- **Los resultados de algunos dados** (Turbo Sword, Zahira Flair), que se resuelven por su propósito (`DiePurpose`, títulos en `core/DiePurposes.cs`).
- **Decisiones despachadas por identificador** en `DecisionChoices` y `ApplyDecision` (`core/Decisions.cs`): Casco de Copias, Misión: Robo de Equipos, Agujero Negro y las habilidades de los Relikarios (Osamu, Ironwing, WD y Yuno Oro). Los Relikarios aún no tienen un registro de módulos como las cartas.
- **La ventana de dado** (Colgante, PC, Destreza, Re-lanza, Agilidad) y las **respuestas COMBO**, que tienen su propia secuencia ([RAM](RAM.md)).

Si una carta nueva necesita uno de estos puntos, valora convertirlo en una familia de disparadores en lugar de añadir otra rama por identificador.

## Añadir un disparador

1. Implementa la interfaz en el módulo de la carta y regístralo en `CardEffects.Registered`.
2. Si el momento o el paso no existe, añade el valor al enum en la posición que dicten las reglas y actualiza la prueba que fija el orden (`tests/TurnTriggerTests.cs`).
3. Prueba el orden con varias cartas a la vez y guarda y recarga la partida en cada pausa.

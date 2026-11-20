# Efectos de carta y decisiones genéricas

Piloto del 16 de noviembre de 2026, ampliado el mismo día con cuatro cartas más después con otras cinco (Senshuni, Cambio de Mentes, Cartucho de Re-carga, Triple Ataque Cruzado y Resetear la Mente) y luego con Esquivar y Tótem de Oro. Responde a los puntos 2 y 3 del [informe del motor](RULES_ENGINE_REVIEW.md): que una carta nueva no necesite su propia fase ni condiciones repartidas por el motor, y que el cliente muestre sus decisiones sin conocerla.

## Cómo funciona

**Un módulo por carta.** Las reglas de una carta viven en un archivo de `core/Effects/` que hereda de `CardEffect` (`core/Decisions.cs`) y se registra en `CardEffects`. El módulo responde a cinco preguntas:

| Método | Qué decide |
|---|---|
| `Unavailable` | Por qué no se puede activar ahora, además de los costes comunes. |
| `Resolve` | Qué hace al resolverse. |
| `Choices` | Las opciones de una decisión que abrió, calculadas en vivo. |
| `Choose` | Qué hace con la opción elegida. |
| `Continue` | Cómo sigue después de una operación que puede pausar, como una destrucción protegible. |

`CardId` y `Resolve` son obligatorios. Los demás tienen un valor por defecto (sin restricción propia, sin opciones, sin continuación), así que cada módulo solo sobrescribe lo que usa.

**Una sola fase de decisión.** `Phase.Decision` con `PendingDecision` sustituye a una fase por carta. Guarda qué carta pregunta, el paso que continúa, quién decide, el tipo (`Confirm`, `ChooseTarget`, `ChooseCard`), la instrucción y si se admiten COMBO mientras espera. Las opciones no se guardan: se recalculan, así que siempre reflejan el tablero y nunca contienen cartas privadas en el estado.

**Decisiones de varios pasos y anidadas.** Una decisión puede recordar lo que ya se eligió (`Picks`, como las memorias de Resetear la Mente) y la mano que solo quien decide puede mirar mientras espera (`ShowsHandOf`); la vista pública la oculta a los demás. Si un COMBO pregunta mientras otra decisión espera, la anterior queda debajo (`Suspended`) y vuelve intacta al contestar la nueva, igual que antes volvía la fase anterior.

**Una sola jugada.** `DuelMoveKind.Choose` lleva el número de la opción. El servidor ya validaba las jugadas por índice de opción, así que no cambió.

**Operaciones reutilizables.** El módulo usa `EffectContext`: preguntar, destruir (con Súper Imán, Reverse Shuffle y Tótem de Oro incluidos), robar, curar, tirar un dado, exigir descartes, tomar una carta de una memoria, reponer el Bonus, mover una carta del mazo, azar y las consultas de objetivos. Son las mismas funciones del motor, así que una carta migrada consume el azar en el mismo orden que antes.

**Tiradas.** `Roll(purpose, step)` pasa por la misma ventana de dado de siempre (relanzamientos del Relikario, Colgante, PC, destreza, Re-lanza y Agilidad) y entrega el resultado final al paso indicado. La tirada conserva su propósito anterior (`ExtraLife`, `PointsChange`, `QuickLoad`), así que el estado y el registro durante la tirada no cambian. Cuando el dado de una activación de Código es definitivo, el rival puede responder con Error de Carga antes de que el efecto continúe ([RAM](RAM.md)).

**Decisiones que se quedan sin opciones.** Si una respuesta (Resetear la Mente, un COMBO) deja una decisión sin opciones, el efecto termina sin efecto, como pide A.2, en lugar de quedarse esperando.

**Lo que ve cada jugador.** `DuelSnapshot.Decision` publica a ambos la carta, quién decide, el tipo y la instrucción. Las opciones solo llegan a quien decide, como siempre. Cada opción lleva su texto, el botón corto (`Button`), la carta (`CardId`), el objetivo en el tablero (`Field`), la memoria de origen (`Owner`) y si es la acción principal (`Primary`). Su texto público (`DecisionChoice.Public`) es lo único que entra al historial; la búsqueda de Konamy no nombra la carta.

**El cliente.** Dibuja cualquier decisión según su tipo: botones para `Confirm`, el tablero iluminado para `ChooseTarget` y la galería para `ChooseCard`. Una galería con cartas de memorias se agrupa por dueño. El título y la instrucción vienen del servidor.

## Cartas migradas

| Carta | Decisión o tirada | Reemplaza |
|---|---|---|
| Huevo Azul Explosivo | Objetivo en el campo, después pagar o no por robar. | `DestroyChoice`, `BlueEggDrawChoice` |
| Bomba de Tiempo | Zona del campo o mano al azar. | `DestroyChoice` |
| Código Konamy | Carta de tu mazo, privada. | `DeckSearchChoice` |
| Vida Extra | Tirada. | `HealFromDie` en el motor |
| Aumento de Relick Points | Tirada, después destino y signo. | `PointsChoice`, `PendingPoints` |
| Carga Rápida | Robo, tirada y descarte. | `QuickLoadDie` |
| Disquete | Carta de cualquier memoria, agrupada por dueño. | `MemoryRecoveryChoice` |
| Senshuni | Ninguna: roba 2. | El robo genérico del motor |
| Cambio de Mentes | Ninguna: intercambia las manos. | `SwapHands` en el motor |
| Cartucho de Re-carga | Ninguna: la secuencia de recarga del motor, con sus Miras de Sniper intermedias. | `ReloadBothHands` en el motor |
| Triple Ataque Cruzado | Ninguna: los tres primeros Ataques del mazo, que son idénticos. | `SearchAttacks` en el motor |
| Resetear la Mente | Mano a mirar, dos memorias (agrupadas por dueño) y una no-habilidad de esa mano, como pasos de la misma decisión. | `MindResetChoice`, `PendingMindReset` |
| Esquivar | Tirada: 1–3 roba, 4–6 niega el ataque. | `DodgeAttack` en el motor |
| Tótem de Oro | Cuántos Tótems de Salvación mostrar (cero, uno o dos), después la colocación del motor. | `GoldenTotemChoice` |
| ¡Dame ese equipo! | Equipo rival en el campo, después la transferencia del motor y un Ataque del mazo debajo del Relikario. | Nada: es una carta nueva, escrita directamente como módulo. |

Cada una es un archivo de 12 a 100 líneas en `core/Effects/`, más `FieldDestruction.cs`, que comparten las dos que destruyen. Se quitaron doce indicadores de `CardDefinition`: `DestroyNonSkill`, `DestroyChosenZone`, `SearchNonSkill`, `ChangePointsFromDie`, `DrawThenDie`, `RecoverMemory`, `SwapHands`, `ReloadBothHands`, `SearchAttacks`, `ResetMind`, `DodgeAttack` y `DeployTotems`. Ni el cliente ni el bot de pruebas leían `SwapHands`, `ReloadBothHands`, `SearchAttacks`, `ResetMind` ni `DeployTotems`. `DodgeAttack` lo leían dos sitios que ahora reconocen la carta por su identificador: la pista del cliente («Requiere un ataque rival pendiente de defensa») y el motor al decidir si el defensor tiene una respuesta que esperar antes del daño, que es un disparador y no una activación. `HealFromDie` sigue solo como pista para el icono de la carta y el bot de pruebas; el motor ya no lo lee. `Draw` se queda como dato que leen los módulos (cuántas cartas roban Senshuni, Carga Rápida y Cartucho), y `EarliestTurn`, `IsCombo` y `ActionCost` siguen siendo costes comunes de activación. El robo y la curación genéricos del motor (`Draw`, `Heal`) solo los usan ya las cartas de prueba.

Resetear la Mente conserva su propia activación (sin coste en PR y sin la línea «juega»), que el motor reconoce por el identificador de la carta en lugar de un indicador. Cartucho de Re-carga y Cambio de Mentes llaman a las secuencias del motor de siempre mediante `EffectContext.ReloadBothHands` y `EffectContext.SwapHands`, porque intercalan disparadores (Mira de Sniper) y revelaciones que ya estaban probados. Tótem de Oro hace lo mismo con su colocación (`EffectContext.DeployGoldenTotem`): destruir el equipo anterior puede pausarla con una sustitución, así que conserva `GoldenTotemResolving` y `PendingGoldenTotem` como estado de resolución, no de decisión. Esquivar pasa su dado por `EffectContext.Roll` y niega el ataque con `EffectContext.NegatePendingAttack`; la ventana de Error de Carga tras el dado sigue igual.

Antes, el robo opcional de Huevo Azul tocaba unos 11 archivos: fase, continuación, protocolo, opciones, historial, guía, título, botones, filtros y etiqueta. Con este modelo, una carta con decisiones parecidas se añade con su entrada en el catálogo, su módulo, sus pruebas y su arte. No toca el protocolo ni la interfaz.

Cambios de comportamiento, todos menores:

- Huevo Azul sin objetivos al resolverse (por ejemplo, tras una respuesta) termina sin efecto en lugar de quedarse esperando una elección vacía.
- La etiqueta de fase muestra el nombre de la carta («BOMBA DE TIEMPO») en lugar de «RESOLVER EFECTO» o «BUSCAR EN EL MAZO».
- Aumento de Relick Points muestra cuatro botones a la vez («+500 a mis PR», «−500 a mis PR», «+500 al rival», «−500 al rival») en lugar de elegir primero el destino y después el signo. Es un toque en lugar de dos, pero ya no hay paso intermedio.
- Disquete muestra en la galería el título «DISQUETE · ELIGE UNA CARTA DE CUALQUIER MEMORIA…» que manda el servidor.
- Resetear la Mente muestra la elección de mano como botones («Mi mano», «Mano rival», esta como acción principal) y las memorias y la carta de la mano en la galería, con el título del servidor. La etiqueta de fase dice «RESETEAR LA MENTE» como antes. Si una interrupción deja la decisión sin opciones, termina sin efecto en lugar de quedarse esperando; las opciones de «finalizar» de siempre se conservan.
- El bot de pruebas elige ahora la mano rival en Resetear la Mente, porque es la opción principal.
- Tótem de Oro pregunta cuántas copias mostrar con botones («Colocar sin Tótems», «Mostrar 1 Tótem», «Mostrar 2 Tótems») en lugar de seleccionar las cartas en la mano. Muestra las primeras copias de la mano, como hacía el bot de pruebas; las copias son idénticas, así que solo importa en el caso raro de tener a la vez una copia propia y una prestada por Cambio de Mentes. La jugada anterior, que nombra las copias exactas, se sigue aceptando. Durante la elección, intentar otra carta dice «Decide primero: Tótem de Oro.».

## Primera carta nueva como módulo

¡Dame ese equipo! es la primera carta que nunca tuvo código en el motor: se escribió directamente como módulo. El costo real fue:

- **El módulo** (`TakeEquipmentEffect.cs`, 55 líneas): disponibilidad (el rival tiene un equipo y tu mazo tiene un Ataque), la decisión `ChooseTarget` con las opciones del rival y dos pasos de continuación, equipar y anexar.
- **Cuatro operaciones nuevas en `EffectContext`**: `RivalEquipment`, `TakeEquipment` (que pasa antes por la sustitución de Súper Imán), `FinishEquipmentTransfer` y `AttachAttackFromDeck`. Las tres últimas envuelven la transferencia de equipo de `GameEngine.Equipment.cs` y el anexado de `GameEngine.Piles.cs`, que reutilizan el reemplazo del equipo propio (con Reverse Shuffle y Tótem de Oro) y las cartas anexadas.
- **Su entrada en el catálogo, el arte y siete pruebas** (`tests/TakeEquipmentTests.cs`): solo el rival como objetivo, anulación y requisitos, recuperar la partida durante Reverse, Súper Imán, el último Tótem con cartas anexadas, PC con el último Ataque del mazo y la activación con Relikia.

No tocó el protocolo, las fases ni la interfaz para su decisión: el cliente la dibuja con el tablero iluminado genérico. Los cambios del cliente en el mismo commit son de las zonas nuevas (Área y equipo ligero) que trajeron las otras cartas.

Misión: Robo de Equipos y Anillo Morado, del mismo commit, usan decisiones genéricas (`PendingDecision`) pero no son módulos: `Decisions.cs` las despacha por identificador, igual que las decisiones de Osamu. Son disparadores (al llegar a ser el Área, al equiparse), no activaciones, así que esperan a que los módulos cubran disparadores.

## Compatibilidad

- **Duelos guardados con una fase anterior.** Al cargarse, `GameEngine.LegacyPhases` convierte cada fase propia de carta ya sustituida en la decisión equivalente. Lo hace tanto en el nivel principal como en el estado que aparta un Estrella del Fontanero o un Doblador de Tiempo (`PendingTurnBuff`), así que esas fases ya no tienen código propio:
  - `TotemChoice` → decisión de Tótem de Salvación;
  - `DeckSearchChoice` → búsqueda de Código Konamy;
  - `GoldenTotemChoice` → cuántos Tótems mostrar;
  - `DestroyChoice` → objetivo de Huevo Azul o Bomba de Tiempo;
  - `MindResetChoice` → el paso pendiente de Resetear la Mente, con la mano mirada y las memorias ya elegidas;
  - `ZahiraTarget` → dónde colocar o mover Zahira Flair.
  
  Pruebas: cada una guardada en su fase anterior, más Konamy apartado por Estrella y Resetear la Mente a mitad de sus elecciones.
- **Fases a las que solo llega una partida antigua.** `BlueEggDrawChoice`, `PointsChoice` y `MemoryRecoveryChoice` ya no se abren en partidas nuevas. Se llega a ellas desde una continuación (`OfferBlueEggDraw`, `FinishCodeDestruction`) o un dado (`PointsChange` sin paso) guardados a medias, así que conservan su código hasta que esas continuaciones se adapten igual que `RobotizeActionStart`.
- **Tiradas guardadas a medias.** Una tirada de Vida Extra, Aumento de PR, Carga Rápida o Esquivar guardada antes de esta versión no conoce su módulo y se termina con el código anterior. Pruebas: las tiradas guardadas antes de mover Vida Extra y Esquivar.
- **Registros de jugadas antiguos.** `ResolveDestruction`, `ResolveDeckSearch`, `ResolveBlueEggDraw`, `ResolvePointsChange`, `ResolveMemoryRecovery`, `ChooseMindResetTarget`, `ChooseMindResetMemory`, `ResolveMindReset` y `ResolveGoldenTotem` responden a la decisión nueva (esta última con las copias exactas que nombra), así que esas jugadas siguen reproduciéndose. La prueba de duelos grabados compara el estado completo antes y después de cada decisión.
- **Clientes anteriores con el servidor nuevo.** Las fases y jugadas viajan como números. Un cliente compilado desde `main` lee la vista sin errores; recibe una fase desconocida y opciones como «Equipo rival» o «Añadir Hoguera · mazo 3», que su interfaz muestra como botones con ese texto. Queda jugable, pero sin el tablero iluminado ni la galería: conviene publicar el cliente antes que el servidor.
- **Cliente nuevo con el servidor anterior.** El cliente conserva el código de las fases anteriores.

## Validación

- 494 comprobaciones del motor, incluidas las de decisiones: Resetear la Mente pausando la búsqueda de Konamy con su mano privada y su guardado a medias, Resetear la Mente y Tótem de Oro guardados en su fase anterior, la decisión de Tótem de Oro (opciones solo para quien decide) y la tirada de Esquivar guardada antes del módulo; además: protocolo y privacidad, COMBO permitido o no, interrupción de Estrella del Fontanero, duelos y tiradas guardados con el formato anterior y definiciones sin indicadores. También pasan los 2000 duelos simulados, la reconstrucción y los duelos grabados.
- Matriz completa de interfaz de Godot sin ventana. Se reescribió la prueba de Aumento de PR para los cuatro botones, la de Resetear la Mente busca la galería genérica y la jugada `Choose`, y la de Tótem de Oro, los tres botones de la confirmación.
- En una base local aislada con `tests/server/dev`: la batería PvP general, con búsquedas privadas y reciclajes verificados por la red, y las 31 comprobaciones de mazos. La batería reconocía como búsqueda en el mazo cualquier galería; ahora solo la de Konamy, porque Resetear la Mente también elige en galería cartas de memoria y de mano.
- Un núcleo compilado desde `main` lee una vista con decisión sin fallar.

## Siguientes pasos

1. ~~Escribir la primera carta nueva directamente como módulo, para medir el costo real.~~ Hecho con ¡Dame ese equipo! (ver «Primera carta nueva como módulo»).
2. Decidir la secuencia de RAM ([RAM](RAM.md)) antes de migrar respuestas: Error de Carga, Caja de Cartón y los COMBO de dado.
3. Ya no quedan Códigos sin respuestas por migrar. Lo siguiente depende de decisiones de diseño: Error de Carga, Caja de Cartón, Estrella del Fontanero, Doblador de Tiempo, Re-lanza y Agilidad necesitan la secuencia de RAM, y Jessica Lee, Mira de Sniper, Misión: Robo de Equipos, Anillo Morado y los demás equipos necesitan que los módulos cubran disparadores, no solo activaciones. El diseño está en [Disparadores de carta](CARD_TRIGGERS.md).
4. Retirar el código de las fases anteriores cuando ya no queden duelos guardados en ellas.

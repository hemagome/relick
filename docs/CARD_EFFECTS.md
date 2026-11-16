# Efectos de carta y decisiones genéricas

Piloto del 16 de noviembre de 2026, ampliado el mismo día con cuatro cartas más. Responde a los puntos 2 y 3 del [informe del motor](RULES_ENGINE_REVIEW.md): que una carta nueva no necesite su propia fase ni condiciones repartidas por el motor, y que el cliente muestre sus decisiones sin conocerla.

## Cómo funciona

**Un módulo por carta.** Las reglas de una carta viven en un archivo de `core/Effects/` que implementa `ICardEffect` (`core/Decisions.cs`) y se registra en `CardEffects`. El módulo responde a cinco preguntas:

| Método | Qué decide |
|---|---|
| `Unavailable` | Por qué no se puede activar ahora, además de los costes comunes. |
| `Resolve` | Qué hace al resolverse. |
| `Choices` | Las opciones de una decisión que abrió, calculadas en vivo. |
| `Choose` | Qué hace con la opción elegida. |
| `Continue` | Cómo sigue después de una operación que puede pausar, como una destrucción protegible. |

**Una sola fase de decisión.** `Phase.Decision` con `PendingDecision` sustituye a una fase por carta. Guarda qué carta pregunta, el paso que continúa, quién decide, el tipo (`Confirm`, `ChooseTarget`, `ChooseCard`), la instrucción y si se admiten COMBO mientras espera. Las opciones no se guardan: se recalculan, así que siempre reflejan el tablero y nunca contienen cartas privadas en el estado.

**Una sola jugada.** `DuelMoveKind.Choose` lleva el número de la opción. El servidor ya validaba las jugadas por índice de opción, así que no cambió.

**Operaciones reutilizables.** El módulo usa `EffectContext`: preguntar, destruir (con Súper Imán, Reverse Shuffle y Tótem de Oro incluidos), robar, curar, tirar un dado, exigir descartes, tomar una carta de una memoria, reponer el Bonus, mover una carta del mazo, azar y las consultas de objetivos. Son las mismas funciones del motor, así que una carta migrada consume el azar en el mismo orden que antes.

**Tiradas.** `Roll(purpose, step)` pasa por la misma ventana de dado de siempre (relanzamientos del Relikario, Colgante, PC, destreza, Re-lanza y Agilidad) y entrega el resultado final al paso indicado. La tirada conserva su propósito anterior (`ExtraLife`, `PointsChange`, `QuickLoad`), así que el estado y el registro durante la tirada no cambian. Es también el único punto donde habría que añadir la ventana de Error de Carga tras el dado que pide el reglamento ([RAM](RAM.md), discrepancia 2).

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

Cada una es un archivo de 22 a 48 líneas en `core/Effects/`, más `FieldDestruction.cs`, que comparten las dos que destruyen. Se quitaron seis indicadores de `CardDefinition`: `DestroyNonSkill`, `DestroyChosenZone`, `SearchNonSkill`, `ChangePointsFromDie`, `DrawThenDie` y `RecoverMemory`. `HealFromDie` sigue solo como pista para el icono de la carta y el bot de pruebas; el motor ya no lo lee.

Antes, el robo opcional de Huevo Azul tocaba unos 11 archivos: fase, continuación, protocolo, opciones, historial, guía, título, botones, filtros y etiqueta. Con este modelo, una carta con decisiones parecidas se añade con su entrada en el catálogo, su módulo, sus pruebas y su arte. No toca el protocolo ni la interfaz.

Cambios de comportamiento, todos menores:

- Huevo Azul sin objetivos al resolverse (por ejemplo, tras una respuesta) termina sin efecto en lugar de quedarse esperando una elección vacía.
- La etiqueta de fase muestra el nombre de la carta («BOMBA DE TIEMPO») en lugar de «RESOLVER EFECTO» o «BUSCAR EN EL MAZO».
- Aumento de Relick Points muestra cuatro botones a la vez («+500 a mis PR», «−500 a mis PR», «+500 al rival», «−500 al rival») en lugar de elegir primero el destino y después el signo. Es un toque en lugar de dos, pero ya no hay paso intermedio.
- Disquete muestra en la galería el título «DISQUETE · ELIGE UNA CARTA DE CUALQUIER MEMORIA…» que manda el servidor.

## Compatibilidad

- **Duelos guardados con una fase anterior** (`DestroyChoice`, `DeckSearchChoice`, `BlueEggDrawChoice`, `PointsChoice`, `MemoryRecoveryChoice`). Se cargan y se terminan con sus jugadas de entonces: el código anterior se conserva solo para ellos. Pruebas: Huevo y Aumento de PR guardados en su fase anterior.
- **Tiradas guardadas a medias.** Una tirada de Vida Extra, Aumento de PR o Carga Rápida guardada antes de esta versión no conoce su módulo y se termina con el código anterior. Prueba: «una tirada guardada antes de mover Vida Extra».
- **Registros de jugadas antiguos.** `ResolveDestruction`, `ResolveDeckSearch`, `ResolveBlueEggDraw`, `ResolvePointsChange` y `ResolveMemoryRecovery` responden a la decisión nueva, así que esas jugadas siguen reproduciéndose. La prueba de duelos grabados compara el estado completo antes y después de cada decisión.
- **Clientes anteriores con el servidor nuevo.** Las fases y jugadas viajan como números. Un cliente compilado desde `main` lee la vista sin errores; recibe una fase desconocida y opciones como «Equipo rival» o «Añadir Hoguera · mazo 3», que su interfaz muestra como botones con ese texto. Queda jugable, pero sin el tablero iluminado ni la galería: conviene publicar el cliente antes que el servidor.
- **Cliente nuevo con el servidor anterior.** El cliente conserva el código de las fases anteriores.

## Validación

- 482 comprobaciones del motor, incluidas 7 de decisiones: protocolo y privacidad, COMBO permitido o no, interrupción de Estrella del Fontanero, duelos y tiradas guardados con el formato anterior y definiciones sin indicadores. También pasan los 2000 duelos simulados, la reconstrucción y los duelos grabados.
- Matriz completa de interfaz de Godot sin ventana. Se reescribió la prueba de Aumento de PR para los cuatro botones.
- En una base local aislada con `tests/server/dev`: la batería PvP general, con búsquedas privadas y reciclajes verificados por la red, y las 31 comprobaciones de mazos.
- Un núcleo compilado desde `main` lee una vista con decisión sin fallar.

## Siguientes pasos

1. Escribir la primera carta nueva directamente como módulo, para medir el costo real.
2. Decidir la secuencia de RAM ([RAM](RAM.md)) antes de migrar respuestas: Error de Carga, Caja de Cartón y los COMBO de dado.
3. Seguir con códigos sin respuestas: Senshuni, Cambio de Mentes, Cartucho de Re-carga, Triple Ataque Cruzado y Resetear la Mente. Mira de Sniper es un equipo y necesita que los módulos cubran disparadores, no solo activaciones.
4. Retirar el código de las fases anteriores cuando ya no queden duelos guardados en ellas.

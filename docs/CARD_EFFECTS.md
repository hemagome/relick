# Efectos de carta y decisiones genéricas

Cómo se programan las reglas propias de una carta sin tocar el motor, el protocolo ni la interfaz. Los disparadores (cartas que reaccionan sin activarse) están en [Disparadores](CARD_TRIGGERS.md); el procedimiento completo, en [Añadir una carta](ADDING_CARD.md).

## Un módulo por carta

Las reglas de una carta viven en un archivo de `core/Effects/` que hereda de `CardEffect` (`core/Decisions.cs`) y se registra en `CardEffects.Registered`. El módulo responde a cinco preguntas:

| Método | Qué decide |
|---|---|
| `Unavailable` | Por qué no se puede activar ahora, además de los costes comunes. |
| `Resolve` | Qué hace al resolverse. |
| `Choices` | Las opciones de una decisión que abrió, calculadas en vivo. |
| `Choose` | Qué hace con la opción elegida. |
| `Continue` | Cómo sigue después de una operación que puede pausar, como un dado o una destrucción protegible. |

`CardId` y `Resolve` son obligatorios. Los demás tienen un valor por defecto (sin restricción propia, sin opciones, sin continuación), así que cada módulo solo sobrescribe lo que usa. Un mismo módulo puede servir a varias cartas con parámetros, como `LetterEffect` o `GuardEquipmentEffect`.

Los datos comunes de activación siguen en `CardDefinition`: `ActionCost`, `PrCost`, `DiscardCost`, `EarliestTurn` e `IsCombo`. `Draw` es un dato que leen los módulos (cuántas cartas roban Senshuni, Carga Rápida o Cartucho de Re-carga).

## Decisiones

**Una sola fase.** `Phase.Decision` con `PendingDecision` sirve a todas las cartas. Guarda qué carta pregunta (`Source`), el paso que continúa, quién decide, el tipo (`Confirm`, `ChooseTarget`, `ChooseCard`), la instrucción y si se admiten COMBO mientras espera. Las opciones no se guardan: se recalculan, así que siempre reflejan el tablero y nunca guardan cartas privadas en el estado.

**Varios pasos y anidadas.** Una decisión puede recordar lo ya elegido (`Picks`, como las memorias de Resetear la Mente) y la mano que solo quien decide puede mirar (`ShowsHandOf`). Si un COMBO pregunta mientras otra decisión espera, la anterior queda debajo (`Suspended`) y vuelve intacta al contestar la nueva.

**Una sola jugada.** `DuelMoveKind.Choose` lleva el número de la opción. El servidor valida las jugadas por índice de opción, así que una carta nueva no cambia el protocolo.

**Sin opciones.** Si una respuesta deja una decisión sin opciones, el efecto termina sin efecto en lugar de quedarse esperando (reglamento, A.2). Una decisión con una sola opción se resuelve sola, salvo que esa opción tenga `RequiresAcknowledgement`.

**Lo que ve cada jugador.** `DuelSnapshot.Decision` publica a ambos la carta, quién decide, el tipo y la instrucción. Las opciones solo llegan a quien decide. Cada `DecisionChoice` lleva su texto (`Label`), el botón corto (`Button`), la carta (`CardId`), el objetivo en el tablero (`Field`), la memoria de origen (`Owner`) y si es la acción principal (`Primary`). Su texto público (`Public`) es lo único que entra al historial.

**El cliente.** Dibuja cualquier decisión según su tipo: botones para `Confirm`, el tablero iluminado para `ChooseTarget` y la galería para `ChooseCard`, agrupada por dueño cuando hay cartas de memorias. El título y la instrucción vienen del servidor.

## Operaciones reutilizables

El módulo solo cambia la partida a través de `GameEngine.EffectContext`: preguntar, destruir (con Súper Imán, Reverse Shuffle, Tótem de Oro y Escudo Galáctico incluidos), robar, curar, dañar, tirar un dado, exigir descartes, tomar una carta de una memoria, reponer el Bonus, mover cartas del mazo, buscar objetivos y obtener azar. Son las funciones del propio motor, así que un módulo consume el azar y aplica las sustituciones igual que el resto de reglas.

**Tiradas.** `Roll(purpose, step)` pasa por la ventana de dado común (relanzamientos del Relikario, Colgante, PC, Destreza, Re-lanza y Agilidad) y entrega el resultado final al paso indicado. Cuando el dado de una activación de Código es definitivo, el rival puede responder con Error de Carga antes de que el efecto continúe ([RAM](RAM.md)).

**Pausas.** `ctx.Then(step)` devuelve una continuación que reanuda la carta en `Continue` cuando termina una operación que puede pausar. Los pasos son cadenas constantes porque se guardan en la partida.

Si falta una operación, añádela a `EffectContext` con un nombre genérico que no mencione la carta ([core/AGENTS.md](../core/AGENTS.md#reutilizar-antes-de-crear)).

## Compatibilidad

No se mantiene código para partidas guardadas con versiones anteriores. Cuando un cambio altera el estado de una partida (fases, continuaciones, jugadas), se vacían las salas y partidas de la base y se publican juntos el cliente y el servidor. Las cuentas y los mazos no guardan estado de reglas y se conservan.

- **Una sola forma de responder.** Las elecciones de carta llegan siempre como `DuelMoveKind.Choose`. Las pruebas usan atajos con nombre (`ResolveTotem`, `ResolveDestruction`, `ChooseMindResetTarget`…) definidos en `scripts/Tests/DecisionCommands.cs`, que responden la decisión por su significado; el motor no los tiene.
- **Cliente y servidor juntos.** Las fases y jugadas viajan por nombre en el JSON, y un valor desconocido falla al leer. Un cliente de otra versión no es compatible.

## Lo que todavía no es un módulo

Algunas cartas conservan parte de su lógica en `GameEngine.*.cs` o en el despacho por identificador de `core/Decisions.cs`; la lista está en [Disparadores · Lo que todavía resuelve el motor](CARD_TRIGGERS.md#lo-que-todavía-resuelve-el-motor). Los textos especiales de daño y premio de algunas habilidades (`NormalDamageText`, `CriticalDamageText` y `RewardText` en `core/CardCatalog.cs`) se eligen con un `switch` por identificador. Son excepciones conocidas: no las amplíes si existe otra salida.

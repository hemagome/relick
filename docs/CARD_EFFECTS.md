# Efectos de carta y decisiones genéricas

Piloto del 16 de noviembre de 2026. Responde a los puntos 2 y 3 del [informe del motor](RULES_ENGINE_REVIEW.md): que una carta nueva no necesite su propia fase ni condiciones repartidas por el motor, y que el cliente muestre sus decisiones sin conocerla.

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

**Operaciones reutilizables.** El módulo usa `EffectContext`: preguntar, destruir (con Súper Imán, Reverse Shuffle y Tótem de Oro incluidos), robar, reponer el Bonus, mover una carta del mazo, azar y las consultas de objetivos. Son las mismas funciones del motor, así que una carta migrada consume el azar en el mismo orden que antes.

**Lo que ve cada jugador.** `DuelSnapshot.Decision` publica a ambos la carta, quién decide, el tipo y la instrucción. Las opciones solo llegan a quien decide, como siempre. Cada opción lleva su texto, el botón corto (`Button`), la carta (`CardId`), el objetivo en el tablero (`Field`) y si es la acción principal (`Primary`). Su texto público (`DecisionChoice.Public`) es lo único que entra al historial; la búsqueda de Konamy no nombra la carta.

**El cliente.** Dibuja cualquier decisión según su tipo: botones para `Confirm`, el tablero iluminado para `ChooseTarget` y la galería para `ChooseCard`. El título y la instrucción vienen del servidor.

## Cartas migradas

Huevo Azul Explosivo, Bomba de Tiempo y Código Konamy. Cada una es un archivo de 23 a 48 líneas, más `FieldDestruction.cs`, que comparten las dos que destruyen. Sus definiciones en `CardCatalog` ya no llevan indicadores: se quitaron `DestroyNonSkill`, `DestroyChosenZone` y `SearchNonSkill`.

Antes, el robo opcional de Huevo Azul tocaba unos 11 archivos: fase, continuación, protocolo, opciones, historial, guía, título, botones, filtros y etiqueta. Con este modelo, una carta con decisiones parecidas se añade con su entrada en el catálogo, su módulo, sus pruebas y su arte. No toca el protocolo ni la interfaz.

Cambios de comportamiento, todos menores:

- Huevo Azul sin objetivos al resolverse (por ejemplo, tras una respuesta) termina sin efecto en lugar de quedarse esperando una elección vacía.
- La etiqueta de fase muestra el nombre de la carta («BOMBA DE TIEMPO») en lugar de «RESOLVER EFECTO» o «BUSCAR EN EL MAZO».

## Compatibilidad

- **Duelos guardados con una fase anterior** (`DestroyChoice`, `DeckSearchChoice`, `BlueEggDrawChoice`). Se cargan y se terminan con sus jugadas de entonces: el código anterior se conserva solo para ellos. Prueba: «un duelo guardado con la fase anterior de Huevo».
- **Registros de jugadas antiguos.** `ResolveDestruction`, `ResolveDeckSearch` y `ResolveBlueEggDraw` responden a la decisión nueva, así que las jugadas `Destroy`, `SearchDeck` y `BlueEggDraw` siguen reproduciéndose. La prueba de duelos grabados compara el estado completo antes y después de cada decisión.
- **Clientes anteriores con el servidor nuevo.** Las fases y jugadas viajan como números. Un cliente compilado desde `main` lee la vista sin errores; recibe una fase desconocida y opciones como «Equipo rival» o «Añadir Hoguera · mazo 3», que su interfaz muestra como botones con ese texto. Queda jugable, pero sin el tablero iluminado ni la galería: conviene publicar el cliente antes que el servidor.
- **Cliente nuevo con el servidor anterior.** El cliente conserva el código de las fases anteriores.

## Validación

- 480 comprobaciones del motor, incluidas 5 nuevas de decisiones: protocolo y privacidad, COMBO permitido o no, interrupción de Estrella del Fontanero, duelo guardado con fase anterior y definiciones sin indicadores. También pasan los 2000 duelos simulados y la reconstrucción.
- Matriz completa de interfaz de Godot sin ventana.
- En una base local aislada con `tests/server/dev`: la batería PvP general, con 4 búsquedas privadas verificadas por la red a través de la decisión, y las 31 comprobaciones de mazos.
- Un núcleo compilado desde `main` lee una vista con decisión sin fallar.

## Siguientes pasos

1. Escribir la primera carta nueva directamente como módulo, para medir el costo real.
2. Migrar otras elecciones simples: Disquete (`MemoryRecoveryChoice`), Aumento de Relick Points (`PointsChoice`) y Mira de Sniper (`SniperChoice`).
3. Retirar el código de las fases anteriores cuando ya no queden duelos guardados en ellas.
4. Más adelante, con cuidado: la cadena de Error de Carga, las respuestas de Caja de Cartón y los relanzamientos de dado. Son los de tiempos más delicados.

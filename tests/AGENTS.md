# tests/AGENTS.md · Pruebas

Usa los mecanismos existentes; no introduzcas otro framework de pruebas.

| Capa | Dónde | Cómo se registra | Cómo se ejecuta |
|---|---|---|---|
| Motor | `tests/*.cs` (ejecutable de consola [Relick.Core.Tests.csproj](Relick.Core.Tests.csproj)) | Clase estática con `Cases` concatenada en la lista de [Program.cs](Program.cs) | `./pruebas.sh --motor` o `dotnet run --project tests/Relick.Core.Tests.csproj` |
| Interfaz | `scripts/Tests/*.cs` | Entrada en la tabla `checks` de `scripts/Tests/OnlineLobby.UiTests.cs` | `./pruebas.sh --ui` o `--checks=Nombre` |
| Red | `tests/pvp/` contra un servidor local con `tests/server/dev` | `tests/pvp/Program.cs` | Ver [server/AGENTS.md](../server/AGENTS.md#validar) |

## Herramientas del motor

- `GameEngine.FromOpening(new DuelOpening { … }, new ScriptedRandom())` o `TestGame.Create(...)` ([TestCards.cs](TestCards.cs)) preparan una partida. Cada clase de pruebas define su propio `Check`; `Ok`, `Eq`, `Fingerprint` y `Combat()` son funciones locales de los casos de [Program.cs](Program.cs) y solo se ven allí (para comparar el estado antes y después en otra clase, serializa `g.State` como hace `Fingerprint`).
- `ScriptedRandom.EnqueueDie(n)` fija los dados. `SeededRandom` reproduce simulaciones.
- `game.ResolveDecision(actor, índice)` responde una decisión; los atajos con nombre están en `scripts/Tests/DecisionCommands.cs`.
- `GameEngine.FromSnapshot(game.SaveSnapshot(), random)` comprueba guardar y recargar a mitad de una decisión.
- `DuelSnapshot.For(game, rival, revision)` comprueba qué ve el rival.
- `DuelOptions.For(game, seat)` comprueba qué opciones ofrece el servidor.
- `tests/SimulationDriver.cs` ejecuta duelos simulados completos.

Ejemplos completos: [TakeEquipmentTests.cs](TakeEquipmentTests.cs) (carta con objetivo), [DodgeTests.cs](DodgeTests.cs) (dado), [TurnTriggerTests.cs](TurnTriggerTests.cs) (orden de disparadores), [IronwingTests.cs](IronwingTests.cs) (Relikario), [DeckRulesTests.cs](DeckRulesTests.cs) (afinidad y mazos).

## Escenarios que debe cubrir una carta o un Relikario

Incluye los que apliquen al texto de la carta:

- **Activación válida** y su efecto principal, una prueba por rama del texto.
- **Activación inválida**: fuera de turno o de etapa, sin acciones o PR suficientes, sin objetivos (`Unavailable`), y que el estado no cambia (estado serializado igual antes y después).
- **Condiciones** satisfechas y no satisfechas.
- **Objetivos**: válidos, inválidos (índices fuera de rango, cartas del jugador equivocado) y ausencia de objetivos al resolver (la carta termina sin efecto, no se queda esperando).
- **Costes** y **restricciones de frecuencia** («una vez por turno»), incluido el turno siguiente.
- **Interacciones** con respuestas (COMBO, Error de Carga, Sin Opciones), sustituciones (Súper Imán, Reverse Shuffle, Tótem de Oro, Escudo Galáctico) y cartas que comparten disparador.
- **Orden de resolución** cuando hay varios disparadores en el mismo momento o paso de daño.
- **Cancelaciones**: la carta anulada o el ataque negado.
- **Cambios de estado**: zonas, contadores, vidas, PR y fases tras resolver; guardar y recargar durante cada pausa.
- **Privacidad / sincronización**: el rival no ve información privada en `DuelSnapshot.For`, y la jugada está disponible en `DuelOptions.For` para el asiento correcto.
- **Regresiones**: ejecuta toda la suite, no solo tus pruebas.

## Reglas

- Una prueba de interfaz debe pasar también ejecutada sola (`./pruebas.sh --checks=Nombre`).
- No uses el reloj real ni la red en las pruebas del motor ni de interfaz; `--fixed-fps 145` está pensado para pruebas sin servidor.
- No borres, saltes ni relajes una prueba existente para que pase un cambio; si una regla cambió de verdad, explica por qué en la PR.
- `./pruebas.sh` termina con `PRUEBAS_PASS` y código 0 cuando todo está bien.

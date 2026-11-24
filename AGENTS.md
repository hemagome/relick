# AGENTS.md

Instrucciones para agentes de programación (Codex, OpenCode, Claude Code y similares). Son operativas: dicen qué leer, qué no romper y cómo validar. Las personas que colaboran deben leer [CONTRIBUTING.md](CONTRIBUTING.md).

La documentación del repositorio está en español; los identificadores y los comentarios del código, en inglés. Mantén esa convención.

## Qué es este proyecto

Simulador **no oficial**, hecho por la comunidad, del juego de cartas Relick: duelos PvP en línea para dos jugadores. No está afiliado a Relick ni a sus titulares de derechos ([NOTICE.md](NOTICE.md)).

| Pieza | Tecnología | Dónde |
|---|---|---|
| Motor de reglas | C# puro (`net8.0`), sin Godot ni red | `core/` |
| Cliente | Godot 4.7.2 **.NET** (C#), renderizador GL Compatibility | `scripts/`, `scenes/`, `assets/`, `project.godot`, `Relick.csproj` |
| Servidor autoritativo | Módulo C# de SpacetimeDB 2.16.1 (`wasi-wasm`) que compila el mismo `core/` | `server/spacetimedb/` |
| Cliente de red | `SpacetimeDB.ClientSDK.Godot` 2.16.1 y bindings generados | `network/` |
| Pruebas del motor | Ejecutable de consola propio (sin xUnit/NUnit) | `tests/` |
| Pruebas de interfaz | Godot sin ventana, tabla de comprobaciones | `scripts/Tests/` |
| Pruebas de red | Ejecutable contra un servidor local | `tests/pvp/`, `tests/server/` |
| Audio original | Generadores C++/Python | `tools/audio/` |

Flujo de una jugada: el cliente envía el índice de una opción calculada por el servidor (`PlayDuelV2` en [server/spacetimedb/LiveDuels.cs](server/spacetimedb/LiveDuels.cs)), con la revisión esperada y un identificador de comando. El servidor recarga el estado guardado, recalcula `DuelOptions.For`, aplica la jugada con el motor y publica a cada jugador solo su vista (`DuelSnapshot.For`). El cliente nunca calcula dados, daño ni resultados.

## Comandos

```bash
./pruebas.sh                                   # motor + interfaz sin ventana; debe terminar con PRUEBAS_PASS (≈1 min)
./pruebas.sh --motor                           # solo el motor
./pruebas.sh --checks=Nombre,Nombre            # solo esas comprobaciones de interfaz (--lista las enumera)
dotnet build server/spacetimedb/StdbModule.csproj   # el servidor compila el mismo core/
./jugar.sh                                     # compila y abre el juego (necesita Godot .NET en el PATH o RELICK_GODOT)
```

Sin el script: `dotnet run --project tests/Relick.Core.Tests.csproj` (motor) y `dotnet build Relick.csproj && godot --headless --fixed-fps 145 --path . -- --pvp-ui-test` (interfaz, termina con `PVP_UI_PASS`). Las pruebas de red necesitan un servidor local con `tests/server/dev`: ver [README.md](README.md#pruebas) y [docs/PVP.md](docs/PVP.md#desarrollo-local). **Nunca publiques módulos en Maincloud** ni ejecutes `spacetime publish` contra un servidor remoto.

## Qué leer según la tarea

Lee solo lo que necesites. Cada documento enlaza al código que es su fuente de verdad.

| Tarea | Lee |
|---|---|
| Añadir una carta | [docs/ADDING_CARD.md](docs/ADDING_CARD.md) y [core/AGENTS.md](core/AGENTS.md) |
| Añadir un Relikario | [docs/ADDING_RELIKARIO.md](docs/ADDING_RELIKARIO.md) y [core/AGENTS.md](core/AGENTS.md) |
| Implementar o reutilizar un efecto | [core/AGENTS.md](core/AGENTS.md#reutilizar-antes-de-crear), [docs/CARD_EFFECTS.md](docs/CARD_EFFECTS.md), [docs/CARD_TRIGGERS.md](docs/CARD_TRIGGERS.md) |
| Modificar reglas globales | [core/AGENTS.md](core/AGENTS.md), [docs/RAM.md](docs/RAM.md), [docs/RULES_ENGINE_REVIEW.md](docs/RULES_ENGINE_REVIEW.md) |
| Clasificar una carta | [docs/CARD_TAXONOMY.md](docs/CARD_TAXONOMY.md) |
| Networking y servidor | [server/AGENTS.md](server/AGENTS.md), [docs/PVP.md](docs/PVP.md), [docs/STACK.md](docs/STACK.md) |
| Interfaz y animaciones | [scripts/AGENTS.md](scripts/AGENTS.md), [docs/UI.md](docs/UI.md) |
| Pruebas | [tests/AGENTS.md](tests/AGENTS.md) |
| Música y efectos de sonido | [docs/MUSIC.md](docs/MUSIC.md), [assets/audio/README.md](assets/audio/README.md) |
| Licencias, recursos de terceros, publicación | [NOTICE.md](NOTICE.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [docs/PUBLICATION_AUDIT.md](docs/PUBLICATION_AUDIT.md) |

## Reglas obligatorias de integridad del juego

1. **No inventes reglas del TCG.** La fuente es el texto impreso que aporta quien pide la carta y el reglamento que citan los documentos. Si no está escrito, no existe.
2. **No interpretes en silencio un texto ambiguo.** Detente, describe la ambigüedad y las interpretaciones posibles, y pide aclaración. Si te autorizan a elegir, anota la interpretación en la sección de la carta en `assets/cards/README.md` (o en el `README.md` del Relikario).
3. **No cambies reglas globales para resolver una sola carta** sin una justificación escrita en el commit o la PR. Prefiere una operación genérica nueva en `EffectContext` o un disparador.
4. **No añadas `if (card.Id == …)` al motor ni indicadores de una sola carta a `CardDefinition`.** Las reglas propias van en un módulo de `core/Effects/` ([core/AGENTS.md](core/AGENTS.md)). Los `switch` por identificador que ya existen (por ejemplo `NormalDamageText`) son deuda conocida: no los amplíes si hay otra salida.
5. **Preserva el determinismo.** El motor solo obtiene azar de `IGameRandom` (el servidor usa `DuelRandom`, de [core/DuelProtocol.cs](core/DuelProtocol.cs), que registra cada valor para la auditoría; las pruebas, `ScriptedRandom` o `SeededRandom`). No uses `System.Random`, la hora, `Guid`, el orden de un `HashSet`/`Dictionary` ni otra fuente no determinista dentro de `core/`.
6. **Respeta el modelo de autoridad.** El servidor valida identidad, sala, revisión, opción y selección. No añadas reducers que acepten resultados calculados por el cliente, ni confíes en la interfaz para ocultar información: la privacidad la garantiza `DuelSnapshot.For`.
7. **Estado consistente y serializable.** Todo lo que deba sobrevivir entre jugadas vive en `GameState` y se guarda con `SaveSnapshot`. Los pasos de continuación son cadenas constantes. Si cambias la forma del estado, avisa: al publicar se vacían salas y partidas ([docs/CARD_EFFECTS.md](docs/CARD_EFFECTS.md#compatibilidad)).
8. **No alteres identificadores existentes** (`CardDefinition.Id`, `RelikarioDefinition.Id`, valores de enums que viajan por red, nombres de pasos) sin evaluar mazos guardados, imágenes por convención de nombre, bindings y pruebas. Añade valores nuevos; no reordenes ni renombres.
9. **Sin regresiones.** `./pruebas.sh` y la compilación del servidor deben quedar en verde. No borres ni debilites una prueba para que pase.
10. **No incorpores recursos de terceros** (imágenes, escaneos, logotipos, textos, audio, fuentes, código) sin verificar que se pueden redistribuir. Ver la sección siguiente.

Ante cualquier duda de reglas o de interacción entre cartas, **pregunta antes de programar**. Documenta la duda en la PR si no hay a quién preguntar y deja la carta sin implementar.

## Licencia y propiedad intelectual

- El código original está bajo **PolyForm Noncommercial 1.0.0** (`SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0`). Es *source available*, no *open source*. No lo describas como código abierto.
- La licencia **no cubre** marcas, nombres de cartas, textos de reglas, ilustraciones, escaneos, logotipos ni personajes de Relick o de terceros. Ver [NOTICE.md](NOTICE.md).
- Las dependencias conservan sus licencias ([THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)). No copies código de terceros dentro del repositorio sin documentar su licencia allí.
- No añadas escaneos ni ilustraciones oficiales nuevas, ni imágenes generadas a partir de ellas, sin una autorización documentada del titular. Las imágenes de cartas nuevas se añaden **con su marca de agua**: no la quites ni edites esas imágenes con herramientas generativas (`image_gen` u otras). Hay recursos ya incluidos con este problema: están listados como bloqueantes en [docs/PUBLICATION_AUDIT.md](docs/PUBLICATION_AUDIT.md); no los copies como patrón.
- No escribas rutas personales, correos, tokens ni identificadores privados en archivos versionados.

## Definition of Done (carta o Relikario)

- [ ] Definición registrada (`CardCatalog.All`; o `RelikarioCatalog.All`, `Playable` y `Roster`).
- [ ] Identificador nuevo, único, estable y en kebab-case; ningún identificador existente cambiado.
- [ ] Comportamiento fiel al texto aportado; dudas resueltas o documentadas, sin reglas inventadas.
- [ ] Efectos y operaciones existentes reutilizados cuando era posible; ningún `if` por identificador nuevo en el motor ni fase nueva en `Phase`.
- [ ] Costes, condiciones (`Unavailable`), restricciones de frecuencia y objetivos validados por el motor, no por la interfaz.
- [ ] Compatible con el servidor: `dotnet build server/spacetimedb/StdbModule.csproj` compila y la jugada pasa por `DuelOptions`/`DuelMoveKind` existentes o por una nueva justificada.
- [ ] Privacidad comprobada con `DuelSnapshot.For` desde el asiento rival.
- [ ] Recursos gráficos, textos y audio revisados: procedencia documentada, permiso de distribución confirmado (o marcados como bloqueantes) y marca de agua intacta en las imágenes de cartas.
- [ ] Pruebas nuevas registradas en `tests/Program.cs` (y en `scripts/Tests/` si hay presentación propia).
- [ ] `./pruebas.sh` termina con `PRUEBAS_PASS`.
- [ ] Sin regresiones conocidas; documentación actualizada si cambió una convención.

## Estilo de código

- C# con `Nullable` e `ImplicitUsings` activados; `namespace Relick.Core` en `core/`, `Relick` en `scripts/`, `Relick.Network` en `network/`.
- Clases `sealed` e `internal` en el motor salvo que el cliente o el servidor las necesiten; registros (`record`) para datos de catálogo.
- Comentarios de documentación breves en inglés que explican el porqué. Textos visibles al jugador en español.
- Godot genera un `.uid` por script y un `.import` por recurso: añádelos al mismo commit.
- No edites a mano `network/bindings/` (generado con `spacetime generate`, ver [docs/PVP.md](docs/PVP.md)).
- No añadas dependencias NuGet ni herramientas nuevas sin una necesidad justificada.

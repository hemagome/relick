# server/AGENTS.md · Servidor y sincronización

El servidor es un módulo C# de SpacetimeDB 2.16.1 ([spacetimedb/StdbModule.csproj](spacetimedb/StdbModule.csproj), `wasi-wasm`). Compila los archivos de `core/` enlazados (`<Compile Include="../../core/**/*.cs" …>`), así que **una carta nueva no necesita cambios aquí**: el motor ya la ejecuta en el servidor.

Guía de funcionamiento, desarrollo local y límites: [docs/PVP.md](../docs/PVP.md). Decisiones de diseño: [docs/STACK.md](../docs/STACK.md). Escalabilidad: [docs/SCALABILITY.md](../docs/SCALABILITY.md).

## Modelo de autoridad

- El cliente envía **intenciones**, nunca resultados: `PlayDuelV2(roomCode, expectedRevision, commandId, option, selectedCards)` en [spacetimedb/LiveDuels.cs](spacetimedb/LiveDuels.cs).
- El servidor comprueba pertenencia a la sala, formato del comando, presupuesto de solicitudes (`ChargePlay`), revisión esperada y que la opción exista en `DuelOptions.For(game, seat)` recalculado en el servidor. Después aplica la jugada con el motor y guarda el estado (`SaveSnapshot`).
- El azar sale de `ctx.Rng` a través de `DuelRandom.Record` y se guarda en la auditoría (`AppendAudit`) junto con la jugada.
- Los comandos repetidos se reconocen por `DuelReceipt` (idempotencia); una revisión distinta se rechaza sin tocar el estado.
- Cada jugador recibe solo su vista mediante vistas (`[View]`): `MyBoard`, `MyDuelHand`, `MyChoices`, `MyDuel`, `MyHand`… Las tablas con estado privado no son públicas.

## Reglas para agentes

1. No añadas reducers que acepten dados, daño, cartas robadas u otros resultados calculados por el cliente.
2. Una jugada nueva de Relikario o de carta se expresa como un `DuelMoveKind` y una opción de `DuelOptions` en `core/`; `PlayDuelV2` la valida sin cambios. Solo toca este módulo si la jugada necesita una selección de cartas nueva (`validSlots`).
3. Si cambias tablas o reducers, regenera los bindings del cliente (comando en [docs/PVP.md](../docs/PVP.md#desarrollo-local)) y no los edites a mano.
4. Si cambias la forma del estado guardado (`GameState`, fases, continuaciones), el cliente y el servidor deben publicarse juntos y las salas y partidas en curso se vacían ([docs/CARD_EFFECTS.md](../docs/CARD_EFFECTS.md#compatibilidad)). Dilo en la PR.
5. **No publiques nada en Maincloud ni en un servidor remoto.** El desarrollo y las pruebas usan un servidor local (`127.0.0.1:3100`) con los módulos de `tests/server/` (`dev` admite identidad anónima con `RELICK_DEV_AUTH`). Publicar es decisión del propietario.
6. No versiones tokens, identidades ni copias de bases de datos. `network/server.cfg` solo contiene la dirección de la base de desarrollo y el `client_id` público de SpacetimeAuth (un cliente PKCE sin secreto).

## Validar

```bash
dotnet build server/spacetimedb/StdbModule.csproj      # siempre, tras cualquier cambio en core/
spacetime start --listen-addr 127.0.0.1:3100            # en otra terminal, para las pruebas de red
spacetime publish relick-dev --server http://127.0.0.1:3100 --module-path tests/server/dev --no-config
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj
```

Las variantes (`--decks`, `--login`, escalabilidad, competiciones) están en [docs/PVP.md](../docs/PVP.md) y [docs/SCALABILITY.md](../docs/SCALABILITY.md).

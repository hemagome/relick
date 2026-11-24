# Duelo PvP

El único modo de juego es el duelo en línea entre dos jugadores. El servidor (módulo de SpacetimeDB en `server/spacetimedb/`) ejecuta el motor de `core/` y es la autoridad: el cliente solo presenta el estado y envía intenciones.

## Cómo se juega

Al abrir el juego con `./jugar.sh` se entra directamente al menú. Hay dos formas de empezar un duelo:

- **Partida rápida.** Pulsa **Buscar rival**. Es una cola casual única, por orden de llegada, sin rangos ni puntuaciones. Puedes cancelar mientras esperas. Cuando llega otro jugador, el servidor crea una sala completa y ambos pasan a la preparación.
- **Sala privada.** Crea una sala y comparte su código de ocho caracteres; el rival lo introduce con **Tengo un código**. Un tercer jugador recibe un rechazo.

La preparación ocurre en el mismo tablero del duelo:

1. Aparecen los Relikarios y cada jugador elige una habilidad de su propio mazo.
2. El servidor extrae esa copia, baraja el resto y reparte otras cuatro cartas. La mano rival permanece boca abajo.
3. Cada jugador conserva la mano o cambia de una a cinco cartas, una sola vez, y confirma. La selección queda bloqueada mientras espera al rival.
4. Cuando ambos confirman, las cartas devueltas van al fondo de su mazo, se reparten los reemplazos y los Bonus y se lanzan dos D6 de iniciativa (los empates se repiten). Quien empieza roba su carta de turno.

En combate, pulsa una carta para ver sus jugadas o elige una opción general. La bandera blanca junto a la rueda dentada abandona el duelo tras confirmar y concede la victoria al rival. **Cerrar sala** elimina la sala y sus datos para ambos jugadores cuando no hay un combate en curso; un duelo con jugadas no se puede borrar antes de terminarlo.

Clasificatoria ELO y Torneo aparecen en el menú como «Próximamente» ([Competencias](COMPETITIVE.md)).

## Conexión y cuentas

`network/server.cfg` indica el servidor, la base de datos y el cliente de SpacetimeAuth que usa el juego por defecto. Las variables de entorno `RELICK_SERVER`, `RELICK_DATABASE` y `RELICK_AUTH_CLIENT` lo sustituyen.

- El módulo real (`server/spacetimedb`) solo admite jugadores que iniciaron sesión con SpacetimeAuth. La primera vez, el juego abre el navegador para iniciar sesión.
- Al indicar `RELICK_SERVER`, el juego entra sin cuenta con una identidad anónima del dispositivo, salvo que también indiques `RELICK_AUTH_CLIENT`. Solo los módulos de prueba (`tests/server/dev`) admiten esa identidad.
- `--pvp-profile=<nombre>` usa una identidad distinta en el mismo equipo, para jugar contra ti mismo. Cada perfil guarda su credencial en los datos de usuario de Godot, separada por servidor y base. No compartas esos archivos `.token`.
- Cerrar la aplicación desconecta el cliente y conserva la sala hasta su vencimiento. **Reconectar**, o volver a abrir el juego con el mismo perfil, recupera la sala, la preparación y el duelo; los envíos pendientes reutilizan su identificador.

## Autoridad e integridad

- Cada jugada (`PlayDuelV2`) lleva la sala, la revisión esperada y un identificador de comando. El servidor decide el actor, recalcula sus opciones (`DuelOptions.For`) y valida la opción y las cartas seleccionadas. Una solicitud basada en un tablero anterior se rechaza sin tocar la partida.
- Los reintentos con el mismo identificador son idempotentes (`DuelReceipt`). Los rechazos consumen cuota de solicitudes y se comunican por `my_move_result`.
- El azar lo genera el servidor y se guarda en una auditoría privada junto a cada jugada.
- Las salas privadas y la cola rápida son excluyentes, también en el servidor.
- Cliente y servidor deben compilarse desde la misma versión. Las jugadas (`DuelMoveKind`) y las fases (`Phase`) viajan por nombre en el JSON de `DuelJson`; un valor desconocido falla al leer en lugar de interpretarse como otra jugada.

## Privacidad

- Las vistas (`my_room`, `my_hand`, `my_initial_skill`, `my_duel`, `MyBoard`, `MyDuelHand`, `MyChoices`, `my_quick_search`…) se filtran por la identidad autenticada. Las tablas con estado, auditoría y comprobantes son privadas.
- Mazos, manos, la elección inicial y la selección del mulligan solo los ve su dueño.
- Las opciones de una decisión solo llegan a quien decide. El rival ve la carta, quién decide y la instrucción. El historial público (`DuelHistory`, últimas veinte jugadas aceptadas) describe intenciones y resultados sin revelar robos, descartes privados ni búsquedas de mazo.
- Las señales visuales (`DuelVisualEvents`) no llevan identidades de cartas ocultas. Por ejemplo, Cambio de Mentes solo transmite origen, destinatario y cantidad, y Código Konamy no nombra la carta añadida.

## Vencimientos y limpieza

| Estado | Vence tras |
|---|---|
| Sala sin preparar | 10 minutos sin actividad |
| Duelo en preparación o en curso | 2 horas sin jugadas ni reconexión |
| Resultado de un duelo terminado | 24 horas |
| Búsqueda de partida rápida | 15 segundos sin renovar (el cliente la renueva cada cinco) |

Un reductor programado (`expire_room`) aplica los vencimientos. La búsqueda rápida también se elimina al desconectarse su conexión. Reconectar recupera un duelo ya asignado, pero no inicia otra búsqueda. Más detalle en [Escalabilidad](SCALABILITY.md).

## Reglas de partida que resuelve el servidor

**Agotamiento del mazo.** Al retirar la última carta, o al intentar robar de un mazo vacío, el primer agotamiento lanza un D6. Tras los relanzamientos y modificadores permitidos, el dueño elige esa cantidad de cartas de su memoria normal (todas si hay menos; las corruptas no participan), las devuelve al mazo y lo baraja. La elección pausa los robos y la reposición de Bonus pendientes, que se reanudan una sola vez después. Si se agota el mazo rival durante tu turno, decide su dueño. Error de Carga puede responder al COMBO, pero no cancelar el reciclaje obligatorio. En el segundo agotamiento, el dueño pierde dos vidas inevitables (sin reducción de equipo) y se comparan las vidas: gana quien tenga más, y una igualdad es empate.

**Ventana de Defensa.** Si una respuesta se resuelve sin terminar el ataque (Esquivar con 1–3, Púa del Destino, una respuesta anulada por Error de Carga) y el defensor ya no tiene ninguna defensa, el daño se resuelve en la misma jugada.

**Causa pública del resultado.** `EndReason` en el tablero público explica cómo terminó el duelo. Cada jugador confirma por separado su regreso al menú.

**Reloj de decisión.** `TurnClock` (`core/TurnClock.cs`) mide, con la hora del servidor, quién decide, desde cuándo y cuánto ha usado cada asiento en el turno y en el duelo. Viaja como `Clock` dentro del tablero y se reinicia en cada revisión. Ninguna regla depende de él todavía. Si se le dan consecuencias, el vencimiento debe decidirlo un reductor programado que compruebe la revisión, y habrá que acordar una gracia para animaciones y desconexiones.

**Respuestas COMBO.** `set_combo_responses` cambia la preferencia privada de cada jugador. Si apagarla deja pasar una respuesta pendiente sin otras decisiones, el servidor continúa la resolución una sola vez.

**Estilo del mazo.** Cada mazo guarda una mica y un dado (`core/CosmeticCatalog.cs`) con `save_deck_v_2`. El estilo se congela al repartir (`loadout_style`) y cada jugador ve el del rival en el duelo.

**Perfil público.** Cada cuenta tiene retrato, título, marco y tapete (`core/ProfileCatalog.cs`) y sus resultados por Relikario (`player_record`), que se escriben una vez en la jugada que termina el duelo. Abandonar cuenta como derrota. Un rival solo ve tu perfil mientras compartís sala (`my_room_profiles`).

## Desarrollo local

Instala .NET 8 y el componente WASI si aún no está disponible:

```bash
dotnet workload install wasi-experimental --skip-manifest-update
```

En una terminal, inicia un servidor local:

```bash
spacetime start --listen-addr 127.0.0.1:3100
```

En otra, desde la raíz del repositorio:

```bash
spacetime publish relick-dev --server http://127.0.0.1:3100 --module-path tests/server/dev --no-config
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --pvp-profile=uno
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --pvp-profile=dos
```

`tests/server/dev` compila el mismo código con `RELICK_DEV_AUTH`, que admite la identidad anónima. **No publiques `tests/server/dev` en un servidor remoto.**

Para probar el inicio de sesión real en local, publica el módulo real en otra base y pasa el cliente de SpacetimeAuth:

```bash
spacetime publish relick-auth-check --server http://127.0.0.1:3100 --module-path server/spacetimedb --no-config
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-auth-check RELICK_AUTH_CLIENT=<client_id de network/server.cfg> ./jugar.sh
```

### Bindings

Los bindings del cliente se generan con la CLI 2.16.1 y se incluyen en el repositorio. Regenéralos al cambiar tablas, vistas o reductores, sin editarlos a mano ni usar `--include-private`:

```bash
spacetime generate --module-path server/spacetimedb --lang csharp --out-dir network/bindings --namespace Relick.Network.Types --yes
```

`tests/pvp/test_bindings/` contiene los bindings del módulo de pruebas `tests/server/lifecycle` (espacio de nombres `Relick.TestNetwork.Types`), que añade reductores solo para pruebas.

## Pruebas de red

Todas se ejecutan contra un servidor local con `tests/server/dev` publicado. Servidor y base se pasan como argumentos; por defecto, `http://127.0.0.1:3100` y `relick-dev`.

```bash
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj                                   # batería general
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --decks http://127.0.0.1:3100 relick-dev
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --quick-match http://127.0.0.1:3100 relick-dev
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --profiles http://127.0.0.1:3100 relick-dev
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --connection-health http://127.0.0.1:3100 relick-dev
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --login <client-id>
```

- **General:** aislamiento de salas, rechazo del tercer jugador, consultas privadas denegadas, preparación y mulligan, solicitudes inválidas, reintentos, reconexión antes y después del inicio, un duelo completo, abandono, jugadas obsoletas, historial público y búsquedas privadas.
- **`--decks`:** mazos guardados, estilos, rechazos, cuentas de invitado y eliminación de cuentas.
- **`--quick-match`:** privacidad de la cola, cancelaciones, exclusión con salas privadas, renovación y caducidad, desconexión y varias parejas a la vez.
- **`--profiles`:** valores predeterminados, rechazos, visibilidad dentro de la sala y resultados de duelos reales.
- **`--connection-health`:** cortes silenciosos simulados con un proxy TCP local, sin tocar la red del equipo.
- **`--login`:** el inicio de sesión real de SpacetimeAuth, sin abrir el juego.
- **Competencias y escalabilidad:** [Competencias](COMPETITIVE.md) y [Escalabilidad](SCALABILITY.md).

### Interfaz contra un servidor local

```bash
# Preparación con clics reales, reconexión y paso al combate
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --pvp-profile=preparation-ui-test --pvp-preparation-test
# Un duelo mediante los botones de la arena contra un segundo cliente real
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --pvp-profile=combat-ui-test --pvp-combat-test --pvp-screenshot=/tmp/relick-combat.png
```

`--pvp-screenshot=<ruta>` guarda una captura (necesita ventana). El perfil de prueba no debe tener otra sala activa.

Dos procesos con entrada real, cada uno en una terminal y con la misma ruta de sesión, nueva en cada ejecución:

```bash
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --headless --pvp-profile=input-host --pvp-input-peer=host --pvp-input-session=/tmp/relick-input-session-1
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --headless --pvp-profile=input-guest --pvp-input-peer=guest --pvp-input-session=/tmp/relick-input-session-1
```

Comprueban identidades independientes, creación y entrada a sala, el mulligan, la continuidad en la misma arena, la memoria y el abandono con resultado compartido. Añade `--pvp-input-quick` a ambos para recorrer la partida rápida en lugar de la sala privada.

Las pruebas de interfaz sin servidor (`./pruebas.sh`, `--pvp-ui-test`, `--lobby-only`…) están en [Interfaz · Validación](UI.md#validación).

## Límites conocidos

- La partida rápida empareja por orden de llegada, sin estimar el nivel.
- No hay controles de tráfico ni de conexiones a nivel de infraestructura, más allá de la cuota de solicitudes del módulo.
- La auditoría guarda las jugadas y el azar recientes, pero no garantiza una repetición completa de cada partida.
- La exportación a Android no se ha validado en dispositivos físicos.

# Competencias: ELO, temporadas, torneos y premios

Los dos modos nacen cerrados. Sin una temporada o un torneo en la base, el juego los
muestra como «Próximamente» y el servidor rechaza buscar o inscribirse. Abrirlos es una
acción del dueño de la base, sin actualizar el cliente.

## Publicar

Publicar el módulo es una decisión del propietario de la base. Usa siempre el módulo real, nunca los de `tests/server`, y conserva los datos:

```bash
spacetime publish <base> --server maincloud --module-path server/spacetimedb --delete-data=never --no-config
```

`<base>` es la base configurada en `network/server.cfg`. El cliente oculta lo que el servidor no ofrece. Las tablas nuevas no se pueden retirar después con `--delete-data=never`: haz una copia antes de cada publicación.

## Administrar

Con la identidad dueña de la base en la CLI:

```bash
spacetime call --server maincloud <base> open_season '"Temporada 1"' 30
spacetime call --server maincloud <base> close_season
spacetime call --server maincloud <base> open_tournament '"Copa Relick"' 8 1440
spacetime call --server maincloud <base> start_tournament
spacetime call --server maincloud <base> cancel_tournament
```

- `open_season`: nombre y días (1 a 365). Se cierra sola en su fecha.
- `open_tournament`: nombre, cupo (2 a 32) y minutos hasta empezar. Empieza solo. Con
  menos de dos inscritos se cancela.
- Solo puede haber una temporada abierta y un torneo en marcha.

## Reglas

Los números están en `core/CompetitiveRules.cs`.

| Regla | Valor |
|---|---|
| ELO inicial y piso | 1000 y 100 |
| Colocación | 5 duelos, con K = 48. Después, K = 24 |
| Ligas | Bronce, Plata desde 950, Oro desde 1100, Platino desde 1250, Leyenda desde 1400 |
| Rango de búsqueda | ±100, crece 5 por segundo. A los 2 minutos acepta a cualquiera |
| Misma pareja | Hasta 5 duelos clasificados en 24 horas |
| Preparar la mano | 5 minutos |
| Cada decisión | 5 minutos. El último minuto se muestra en el reloj |
| Cerrar la sala o abandonar | Derrota |
| Temporada nueva | Conserva la mitad de la distancia a 1000 |
| Presentarse a una ronda | 10 minutos. Quien falta cae; si faltan ambos, caen los dos |
| Empate en torneo | Se repite. Al tercero avanza la mejor semilla |

Una sala privada y la partida rápida cuentan en el perfil y nunca mueven el ELO. El
torneo tampoco lo mueve. Los invitados no compiten. Los límites de tiempo existen solo
en los duelos con puntuación: los demás conservan su reloj informativo.

El torneo es de eliminación directa con semillas al azar. Los pases van a las mejores
semillas. Los nombres de los inscritos son visibles para todos los jugadores; sus
identidades no salen del servidor.

## Premios

| Premio | Cómo se gana |
|---|---|
| Emblema de temporada y título Clasificado | Terminar una temporada con 5 duelos |
| Marco Liga | Terminarla en Oro o más |
| Mica Temporada | Terminarla en Platino o más |
| Emblema, título, marco y dado Campeón | Ganar un torneo |

`RewardCatalog` los define. El servidor los concede y los comprueba al guardar el perfil
o un mazo, así que un cliente modificado no puede ponérselos.

Eliminar la cuenta borra su ELO, emblemas y premios. En un torneo empezado su lugar
queda como «Cuenta eliminada» y su rival avanza.

## Servidor

| Archivo | Contenido |
|---|---|
| `Modes.cs` | Modo de cada sala, liquidación única y derrota por tiempo |
| `Seasons.cs` | Temporadas, ELO, último resultado y límite por pareja |
| `RankedMatch.cs` | Cola clasificatoria |
| `Tournaments.cs` | Inscripción, cuadro, presencia y avance |
| `Rewards.cs` | Premios, emblemas y lo que ve el rival |
| `Personalization.cs` | Inicio, portadas y resultados por Relikario |

Una sala sin fila en `room_mode` es privada.

## Pruebas

Solo en local. `tests/server/dev` añade reductores de administración para pruebas, y
`tests/server/competitive` además reduce los límites a seis segundos.

```bash
spacetime start --listen-addr 127.0.0.1:3100
spacetime publish relick-dev --server http://127.0.0.1:3100 --module-path tests/server/dev --no-config
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --competitive http://127.0.0.1:3100 relick-dev
spacetime publish relick-short --server http://127.0.0.1:3100 --module-path tests/server/competitive --no-config
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --competitive-time http://127.0.0.1:3100 relick-short
```

Cada batería necesita una base recién creada. Para recorrerlo con la interfaz, abre una
temporada y un torneo con `open_season_for_test` y `open_tournament_for_test`, lanza dos
procesos con `--pvp-competitive-peer=host` y `=guest` y el mismo `--pvp-input-session`,
y llama a `start_tournament_for_test` cuando existan los dos archivos `.enrolled`.

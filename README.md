# Relick · PvP (simulador comunitario no oficial)

[![Godot](https://img.shields.io/badge/Godot-4.7.2%20.NET-478CBF?logo=godotengine&logoColor=white)](https://godotengine.org/download)
[![.NET](https://img.shields.io/badge/.NET%20SDK-8.0.400-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/C%23-net8.0-239120?logo=csharp&logoColor=white)](Relick.csproj)
[![SpacetimeDB](https://img.shields.io/badge/SpacetimeDB-2.16.1-1A1A1A)](https://spacetimedb.com)
[![Renderer](https://img.shields.io/badge/render-GL%20Compatibility-478CBF)](project.godot)
[![Android](https://img.shields.io/badge/Android-net9.0%20·%20ARM64-3DDC84?logo=android&logoColor=white)](export-android.sh)
[![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-lightgrey)](LICENSE)

Simulador **no oficial**, desarrollado por la comunidad, del juego de cartas coleccionables Relick: duelos en línea para dos jugadores. El cliente es Godot .NET; las reglas son un motor C# puro (`core/`) que el servidor SpacetimeDB ejecuta de forma autoritativa. Interfaz en español; solo hay modo PvP.

> **Aviso.** This is an unofficial, community-developed fan project inspired by the Relick Trading Card Game. It is not affiliated with, endorsed by, sponsored by, or officially supported by Relick or its respective rights holders. All trademarks, card names, artwork, characters, and other third-party intellectual property remain the property of their respective owners. The software license applies exclusively to the original software covered by that license and does not grant any rights to third-party intellectual property. This project is intended for non-commercial purposes. No ownership or official association with the Relick franchise is claimed.
>
> Este es un proyecto no oficial de aficionados, inspirado en el juego de cartas coleccionables Relick y desarrollado por la comunidad. No está afiliado, respaldado ni patrocinado por Relick ni por sus titulares de derechos, y no cuenta con su soporte oficial. Las marcas, nombres de cartas, ilustraciones, personajes y demás propiedad intelectual de terceros pertenecen a sus dueños. La licencia del software solo cubre el software original y no concede derechos sobre la propiedad intelectual de terceros. Proyecto sin fines comerciales; no se reclama titularidad ni asociación oficial con la franquicia Relick. Ver [NOTICE.md](NOTICE.md).

## Estado

En desarrollo activo; todavía no hay una versión estable ni el repositorio es público. Antes de publicarlo hay bloqueantes legales pendientes, sobre todo los recursos gráficos de Relick incluidos ([docs/PUBLISHING.md](docs/PUBLISHING.md)).

Funciona hoy, cubierto por las pruebas automáticas:

- Duelos PvP completos entre dos clientes, con el motor ejecutándose en el servidor: partida rápida y salas privadas con código, reconexión y reloj de decisión.
- Cinco Relikarios jugables con dos niveles y evolución: Yuno, Osamu, Ironwing, WD y Yuno Oro.
- Cartas de los tipos Ataque, Habilidad, Código (Jugada, Objeto, Personaje y Trampa), Equipo (normal y ligero), Área y cartas sobre el Relikario, con respuestas COMBO, Error de Carga y recetas de Códigos Trampa.
- Constructor de mazos guardados en el servidor, perfil público y personalización (micas, dados, fondos).
- Cuentas con SpacetimeAuth, o identidad anónima en un servidor local de desarrollo.
- Música y efectos de sonido originales.

La Clasificatoria ELO y los torneos aparecen en el menú como «Próximamente»: el servidor tiene parte de la lógica ([docs/COMPETITIVE.md](docs/COMPETITIVE.md)), pero no están disponibles para jugar.

Las versiones de las insignias son las que fija el repositorio: `Godot.NET.Sdk/4.7.2` en [Relick.csproj](Relick.csproj), el SDK en [global.json](global.json) y `SpacetimeDB.ClientSDK.Godot`, `SpacetimeDB.ClientSDK` y `SpacetimeDB.Runtime` 2.16.1 en los proyectos del cliente, las pruebas y el [módulo del servidor](server/spacetimedb/StdbModule.csproj). La CLI `spacetime` debe ser también la 2.16.1.

## Requisitos

| Herramienta | Para qué |
|---|---|
| [Godot 4.7.2 **.NET**](https://godotengine.org/download) (no la edición estándar) | Abrir y ejecutar el juego |
| [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) | Compilar el juego, el motor y las pruebas |
| [CLI de SpacetimeDB 2.16.1](https://spacetimedb.com/install) | Solo para un servidor local, las pruebas de red o publicar el módulo |

Sin servidor propio, el juego se conecta a la base de desarrollo en Maincloud configurada en [network/server.cfg](network/server.cfg) y pide iniciar sesión con SpacetimeAuth la primera vez.

## Ejecutar

### Linux y macOS

```bash
./jugar.sh
```

`jugar.sh` compila `Relick.csproj` y abre Godot. Busca `godot` o `godot4` en el `PATH`; si no, indica el ejecutable con `RELICK_GODOT`. En macOS suele ser:

```bash
RELICK_GODOT=/Applications/Godot_mono.app/Contents/MacOS/Godot ./jugar.sh
```

### Windows

Desde PowerShell, en la raíz del repositorio:

```powershell
dotnet build Relick.csproj
& "C:\Ruta\Godot_v4.7.2-stable_mono_win64.exe" --path .
```

`jugar.sh` también funciona en Git Bash o WSL. Otra opción en cualquier sistema: abrir `project.godot` en el editor de Godot .NET y pulsar F5.

### Opciones útiles

```bash
./jugar.sh --pvp-profile=dos       # segunda identidad en el mismo equipo, para jugar contra ti mismo
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh   # servidor local, sin cuenta
```

Cómo levantar el servidor local y publicar el módulo: [PvP · Desarrollo local](docs/PVP.md#desarrollo-local).

## Pruebas

```bash
./pruebas.sh                                  # motor e interfaz completa, sin ventana ni servidor (menos de un minuto)
./pruebas.sh --motor                          # solo el motor de reglas
./pruebas.sh --checks=TotemLayout,TurnCore    # solo esas comprobaciones de interfaz; --lista muestra los nombres
```

`pruebas.sh` compila una vez, ejecuta el motor y reparte la interfaz entre varios procesos de Godot (`-j N`; por defecto, la mitad de los núcleos, hasta 8). Cada proceso usa su propio `user://`, así que no toca tus ajustes. Termina con `PRUEBAS_PASS` y código 0; si algo falla, dice qué comprobación y dónde quedan los registros. Guarda la duración de cada comprobación en `.godot/pruebas-tiempos.tsv` para repartirlas mejor en la siguiente ejecución.

Sin el script (por ejemplo, en Windows): `dotnet run --project tests/Relick.Core.Tests.csproj` para el motor; para la interfaz, `dotnet build Relick.csproj` y luego `godot --headless --fixed-fps 145 --path . -- --pvp-ui-test` (`Godot_…_mono_win64.exe` en Windows). Termina con `PVP_UI_PASS` y código 0. `--fixed-fps 145` evita que Godot duerma entre frames sin pantalla y fija el paso de tiempo; sin él la prueba es correcta, pero tarda más del doble. Úsalo solo en pruebas sin servidor: las de red y audio dependen del reloj real. Los filtros (`--lobby-only`, `--decks-only`…) y las capturas renderizadas están en [Interfaz · Validación](docs/UI.md#validación).

`./jugar.sh --headless --shutdown-test` cierra el juego con el finalizador de .NET cargado y debe terminar con código 0. Protege el arreglo de `ManagedShutdown` para una carrera de Godot .NET 4.7.2 que tumbaba el proceso al cerrar. Depende de un detalle interno del motor: repítela al actualizar Godot.

Las pruebas de red necesitan un servidor local con el módulo de desarrollo, que **nunca** se publica en un servidor remoto:

```bash
spacetime start --listen-addr 127.0.0.1:3100
spacetime publish relick-dev --server http://127.0.0.1:3100 --module-path tests/server/dev --no-config
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj
```

Variantes de la batería PvP y pruebas de escalabilidad: [PvP](docs/PVP.md#pruebas-de-red) y [Escalabilidad](docs/SCALABILITY.md).

## Contribuir

- Personas: [CONTRIBUTING.md](CONTRIBUTING.md) explica el entorno, las pruebas, cómo reportar errores de juego, cómo presentar PRs y qué recursos no deben incorporarse.
- Agentes de programación (Codex, OpenCode, Claude Code…): [AGENTS.md](AGENTS.md), con un `AGENTS.md` por módulo en [core/](core/AGENTS.md), [server/](server/AGENTS.md), [scripts/](scripts/AGENTS.md) y [tests/](tests/AGENTS.md).

## Ampliar el juego

Guías pensadas para que un desarrollador o un agente de IA añada contenido siguiendo los patrones del proyecto:

- **[Añadir un Relikario](docs/ADDING_RELIKARIO.md)**: definición y evolución, poderes, cartas y Ataques propios, artes, animaciones, música, pruebas y qué pedir al diseñador.
- **[Añadir una carta](docs/ADDING_CARD.md)**: clasificación, definición, ilustración, módulo de reglas, disparadores, decisiones y pruebas.

## Exportar

```bash
./export-windows.sh    # artifacts/Relick-windows-x86_64.zip (necesita las plantillas .NET de Godot 4.7.2)
./export-android.sh    # artifacts/Relick-android-arm64.apk (SDK de .NET 9, OpenJDK 17 y Android SDK)
```

Cada script comprueba sus requisitos y explica qué falta. El cliente exportado solo es compatible con el servidor publicado desde la misma versión.

## Estructura

```text
core/                 Motor de reglas en C# puro: catálogos, estado, decisiones y cadenas de disparadores
core/Effects/         Un módulo por carta con reglas propias
scripts/              Sala, arena PvP, presentación y animaciones (Godot)
scripts/Tests/        Pruebas de interfaz; solo se compilan en desarrollo
network/              Conexión con SpacetimeDB y bindings generados
server/spacetimedb/   Módulo autoritativo; compila el mismo core/
assets/               Cartas, personajes, audio y marca
tests/                Pruebas del motor
tests/pvp/            Pruebas de red contra un servidor local
tests/server/         Módulos de prueba (autenticación de desarrollo, vencimientos cortos), solo locales
tools/audio/          Generadores de la música y los efectos originales
```

## Documentación

- [PvP: configuración, servidor y pruebas en línea](docs/PVP.md)
- [Competencias: ELO, temporadas, torneos y premios](docs/COMPETITIVE.md)
- [Interfaz, animaciones y validación](docs/UI.md)
- [Stack y diseño del duelo en línea](docs/STACK.md)
- [Audio](docs/MUSIC.md)
- [Clasificación de cartas](docs/CARD_TAXONOMY.md)
- [Efectos de carta como módulos](docs/CARD_EFFECTS.md) y [disparadores](docs/CARD_TRIGGERS.md)
- [Respuestas en cadena (RAM)](docs/RAM.md)
- [Escalabilidad: estado del duelo, límites y mediciones](docs/SCALABILITY.md)
- [Antes de publicar](docs/PUBLISHING.md), [aviso legal](NOTICE.md) y [avisos de terceros](THIRD_PARTY_NOTICES.md)

## Licencia y propiedad intelectual

El código original está disponible bajo la **[PolyForm Noncommercial License 1.0.0](LICENSE)** (`SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0`). Es un proyecto *source available*, **no open source**: se permite usarlo, modificarlo y distribuirlo con fines no comerciales en los términos de la licencia. Cualquier uso comercial que la licencia no permita expresamente requiere una autorización adicional del titular del código.

La licencia se aplica únicamente al código y los recursos originales cuyos derechos pertenecen al titular del proyecto. **No concede ningún derecho** sobre las marcas, el logotipo, los nombres de cartas y personajes, los textos de reglas ni las ilustraciones de Relick, que pertenecen a sus titulares, ni sobre los componentes de terceros, que conservan sus propias licencias:

- [NOTICE.md](NOTICE.md): alcance de la licencia y aviso de proyecto no oficial.
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md): Godot (MIT), SpacetimeDB (Business Source License 1.1), grabaciones CC0 y elementos de Relick.
- [docs/PUBLISHING.md](docs/PUBLISHING.md): lo que falta resolver antes de publicar.

El juego muestra el mismo aviso en la pantalla de inicio de sesión, al pie de la página Jugar y en Ajustes → Acerca de, junto con las licencias de terceros.

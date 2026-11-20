# Relick · PvP

[![Godot](https://img.shields.io/badge/Godot-4.7.2%20.NET-478CBF?logo=godotengine&logoColor=white)](https://godotengine.org/download)
[![.NET](https://img.shields.io/badge/.NET%20SDK-8.0.400-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/C%23-net8.0-239120?logo=csharp&logoColor=white)](Relick.csproj)
[![SpacetimeDB](https://img.shields.io/badge/SpacetimeDB-2.16.1-1A1A1A)](https://spacetimedb.com)
[![Renderer](https://img.shields.io/badge/render-GL%20Compatibility-478CBF)](project.godot)
[![Android](https://img.shields.io/badge/Android-net9.0%20·%20ARM64-3DDC84?logo=android&logoColor=white)](export-android.sh)

Juego de cartas para dos jugadores con duelos en línea. El cliente es Godot .NET; las reglas son un motor C# puro (`core/`) que el servidor SpacetimeDB ejecuta de forma autoritativa. Interfaz en español; solo hay modo PvP.

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
dotnet run --project tests/Relick.Core.Tests.csproj    # motor de reglas (sin Godot ni servidor)
./jugar.sh --headless --pvp-ui-test                    # interfaz completa sin ventana ni servidor
```

En Windows, la prueba de interfaz es `dotnet build Relick.csproj` seguido de `Godot_…_mono_win64.exe --headless --path . -- --pvp-ui-test`. Termina con `PVP_UI_PASS` y código de salida 0. Los filtros (`--lobby-only`, `--decks-only`…) y las capturas renderizadas están en [Interfaz · Validación](docs/UI.md#validación).

Las pruebas de red necesitan un servidor local con el módulo de desarrollo, que **nunca** se publica en Maincloud:

```bash
spacetime start --listen-addr 127.0.0.1:3100
spacetime publish relick-dev --server http://127.0.0.1:3100 --module-path tests/server/dev --no-config
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj
```

Variantes de la batería PvP y pruebas de escalabilidad: [PvP](docs/PVP.md#desarrollo-local) y [Escalabilidad](docs/SCALABILITY.md).

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
tests/pvp/            Pruebas de red contra un servidor publicado
tests/server/         Módulos de prueba (autenticación de desarrollo, vencimientos cortos), solo locales
tools/audio/          Generadores de la música y los efectos originales
```

## Documentación

- [PvP: configuración, servidor y pruebas en línea](docs/PVP.md)
- [Interfaz, animaciones y validación](docs/UI.md)
- [Stack y diseño del duelo en línea](docs/STACK.md)
- [Audio](docs/MUSIC.md)
- [Clasificación de cartas](docs/CARD_TAXONOMY.md)
- [Efectos de carta como módulos](docs/CARD_EFFECTS.md) y [disparadores](docs/CARD_TRIGGERS.md)
- [Respuestas en cadena (RAM)](docs/RAM.md)
- [Evaluación del motor](docs/RULES_ENGINE_REVIEW.md), [revisión DRY](docs/DRY_REVIEW.md) y [escalabilidad](docs/SCALABILITY.md)

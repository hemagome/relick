# Avisos de terceros

Componentes, dependencias y recursos de este repositorio que **no** son código original del proyecto y, por tanto, no están cubiertos por PolyForm Noncommercial 1.0.0 ([NOTICE.md](NOTICE.md)). Cada uno conserva su licencia y sus avisos de copyright.

Revisado el 24 de noviembre de 2026 sobre las versiones fijadas en el repositorio. Las licencias de los paquetes NuGet se comprobaron en los propios paquetes (`~/.nuget/packages/<paquete>/2.16.1/`). Lo marcado como **pendiente** no se ha podido verificar y debe confirmarse antes de publicar ([docs/PUBLICATION_AUDIT.md](docs/PUBLICATION_AUDIT.md)).

## Motor y SDK

| Componente | Versión | Uso | Licencia | Notas |
|---|---|---|---|---|
| [Godot Engine](https://godotengine.org) | 4.7.2 (.NET) | Motor del cliente; se distribuye dentro de cada exportación | MIT | El juego muestra el texto MIT de Godot y los avisos de sus componentes internos en Ajustes → Acerca de → Licencias de terceros (`Engine.GetLicenseText`, `GetCopyrightInfo`, `GetLicenseInfo`). |
| `Godot.NET.Sdk`, `GodotSharp` | 4.7.2 | Compilación del cliente | MIT (proyecto Godot) | Paquetes del proyecto Godot. |
| .NET (SDK y runtime) | 8.0 (Android: 9.0) | Compilación; el runtime viaja en las exportaciones | MIT (.NET Foundation y colaboradores) | <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT> |

## SpacetimeDB

| Paquete | Versión | Uso | Licencia declarada en el paquete |
|---|---|---|---|
| `SpacetimeDB.Runtime` | 2.16.1 | Módulo del servidor (`server/spacetimedb`, `tests/server/*`) | **Business Source License 1.1**, Clockwork Laboratories, Inc. |
| `SpacetimeDB.BSATN.Runtime` | 2.16.1 | Dependencia del servidor y del SDK de cliente; viaja en las exportaciones del juego | **Business Source License 1.1**, Clockwork Laboratories, Inc. |
| `SpacetimeDB.ClientSDK.Godot` | 2.16.1 | Cliente de red del juego | **Pendiente**: el paquete no incluye archivo de licencia ni la declara en su `.nuspec`. Repositorio: <https://github.com/clockworklabs/com.clockworklabs.spacetimedbsdk> |
| `SpacetimeDB.ClientSDK` | 2.16.1 | Pruebas de red (`tests/pvp`) | **Pendiente**, igual que el anterior |

Condiciones relevantes de la BSL 1.1 de SpacetimeDB 2.16.1 (resumen; el texto vinculante es el `LICENSE` de cada paquete):

- No es una licencia *open source*. Permite copiar, modificar y redistribuir, y el uso en producción solo según la *Additional Use Grant*: una aplicación o servicio con **no más de una instancia de SpacetimeDB en producción**, y sin ofrecer un «Database Service» comercial a terceros.
- *Change Date*: 2031-11-19; después pasa a AGPL v3.0 con excepción de enlace.
- PolyForm Noncommercial no se aplica a estos paquetes ni los relicencia. El proyecto los consume como dependencias de NuGet y no copia su código fuente.

Los archivos de [network/bindings/](network/bindings/) los genera la CLI `spacetime generate` a partir del módulo del proyecto. **Pendiente:** confirmar que la licencia de la CLI no impone condiciones sobre el código generado.

## Audio

| Archivo | Origen | Autor | Licencia |
|---|---|---|---|
| `assets/audio/sources/wooden-dice-roll-3.flac` y su derivado `assets/audio/sfx/dice-roll.wav` | [Wooden dice on wodden table roll](https://opengameart.org/content/wooden-dice-on-wodden-table-roll) | Wuzzy | CC0 1.0 |
| `assets/audio/sources/card-shuffle-bmaczero.wav` y su derivado `assets/audio/sfx/card-shuffle.wav` | [Playing Card Sounds](https://opengameart.org/content/playing-card-sounds) | Brian MacIntosh (BMacZero) | CC0 1.0 |

Procedencia y procesado: [assets/audio/sources/DICE-LICENSE.txt](assets/audio/sources/DICE-LICENSE.txt), [assets/audio/sources/CARD-SHUFFLE-LICENSE.txt](assets/audio/sources/CARD-SHUFFLE-LICENSE.txt) y [assets/audio/README.md](assets/audio/README.md). CC0 no exige atribución; se conserva igualmente. La música y los demás efectos son originales, generados por `tools/audio/`.

## Propiedad intelectual de Relick

Los siguientes elementos pertenecen a Relick o a sus respectivos titulares (los créditos impresos en las cartas incluyen «© 2018 Fuyuhiro Gushiken» / «©2018 FG» e ilustradores individuales). **No se distribuyen bajo PolyForm Noncommercial ni bajo ninguna otra licencia de este proyecto**, y el repositorio no documenta una autorización para redistribuirlos:

| Elemento | Ubicación |
|---|---|
| Logotipo de Relick (cabecera de la web oficial) | `assets/brand/relick-logo.png` |
| Escaneos de cartas (con su marca de agua) y reverso | `assets/cards/*-clean.png`, `assets/cards/card-back.png` |
| Cartas de Relikario (con su marca de agua), retratos, figuras y animaciones derivadas | `assets/characters/*/` |
| Imágenes de los efectos de Relikia (procedencia no documentada) | `assets/relikia/*.webp` |
| Nombres de cartas y personajes, y textos de reglas transcritos | `core/CardCatalog.cs`, `core/RelikarioCatalog.cs`, `core/RelikiaEffects.cs`, `assets/cards/README.md`, `assets/characters/*/README.md`, documentación |

Estado y alternativas: [docs/PUBLICATION_AUDIT.md](docs/PUBLICATION_AUDIT.md).

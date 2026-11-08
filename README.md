# Relick · PvP

Juego de cartas para **Godot 4.7.2 o posterior, edición .NET**, con interfaz en español y duelos de dos jugadores mediante SpacetimeDB. El juego abre directamente la pantalla de salas. Solo se ofrece PvP.

## Abrir y jugar

Necesitas Godot .NET y el SDK de .NET 8. Abre `project.godot` y pulsa F5, o ejecuta:

```bash
./jugar.sh
```

1. Escribe tu nombre y crea una sala o introduce el código de tu rival.
2. Cuando entra el segundo jugador, ambos pasan al tablero.
3. Elige una habilidad entre las presentes en tu mazo. Esa carta y otras cuatro forman tu mano inicial.
4. Selecciona las cartas que quieres cambiar —también puedes cambiar la habilidad elegida— o conserva las cinco. Confirma y espera al rival.
5. Se resuelven los cambios, los Bonus y la iniciativa. Quien empieza roba una carta adicional.
6. Enseña habilidades, ataca, juega códigos, equipa o canjea cartas. Las opciones visibles están autorizadas por el servidor.
7. Al terminar, cierra la sala para volver a la pantalla de creación y acceso. Durante preparación puedes cerrarla; durante un duelo puedes abandonar mediante su confirmación.

La conexión predeterminada está en `network/server.cfg`. `RELICK_SERVER` y `RELICK_DATABASE` permiten usar otra base de desarrollo. La identidad del dispositivo se guarda para recuperar una sala con Reconectar. El directorio de identidad anterior se conserva aunque el título del juego ahora sea Relick.

## Alcance actual

- Yuno Gyambura y su evolución, con animaciones de reposo, ataque y daño. Las hojas actuales conservan la marca de agua de Ludo del recurso aportado.
- Veintiuna definiciones aportadas: Ataque, Magnet Shuffle, Destiny Shuffle, Colgante de la Suerte, Senshuni, Caja de Cartón, Vida Extra, Aumento de Relick Points, Huevo Azul Explosivo, Re-lanza, Error de Carga, Casco de Diamantes, Bomba de Tiempo, Código Konamy, Cambio de Mentes, Agilidad al 100%, Resetear la Mente, Esquivar, PC 8BITS, Mira de Sniper y Triple Ataque Cruzado.
- Mazos de desarrollo simétricos de 23 cartas (tres Ataques y una copia de cada otra carta), cuatro espacios de habilidad con costes 0/300/500/1000 PR, Memoria, Corruptas, Bonus, canje, respuestas, COMBO y efectos de Relikia.
- Selección de habilidad inicial, mulligan y combate en la misma arena; manos privadas, reintentos idempotentes y recuperación de decisiones pendientes.
- Primer agotamiento: D6, selección de cartas de la memoria y barajado. Segundo: dos vidas inevitables y comparación de vidas; igualdad es empate.
- Interfaz horizontal adaptable a PC, tablet y teléfono. La exportación y validación en hardware Android/iOS siguen pendientes.
- Servidor autoritativo: el cliente presenta información autorizada y envía intenciones; no decide dados, daño ni legalidad del duelo.

La [clasificación indicada por el usuario](docs/CARD_TAXONOMY.md) es la referencia para ampliar efectos y deckbuilding. Documenta habilidades genéricas/específicas, equipos ligeros, áreas orientables y códigos Jugada/Acción/Trampa con extradeck, distinguiendo la especificación de lo ya implementado. Aún no hay constructor de mazos ni catálogo completo.

## Documentación

- [PvP, configuración, servidor y pruebas en línea](docs/PVP.md).
- [Interfaz, animaciones y optimizaciones](docs/UI.md).
- [Clasificación de cartas](docs/CARD_TAXONOMY.md).
- [Evaluación del motor para 500 cartas](docs/RULES_ENGINE_REVIEW.md).
- [Revisión de DRY y optimizaciones](docs/DRY_REVIEW.md).
- [Mediciones de escalabilidad](docs/SCALABILITY.md).

## Comprobaciones

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
dotnet build Relick.csproj
godot --headless --path . -- --pvp-ui-test
```

La prueba de interfaz trabaja sin servidor: comprueba cuatro tamaños horizontales, ambos asientos, privacidad, selección de habilidad sin reconstruir el tablero, mulligan, recuperación y animaciones. Para capturas renderizadas:

```bash
./jugar.sh -- --pvp-ui-test --ui-screenshot-dir=/tmp/relick-ui
./jugar.sh -- --lobby-preview-dir=/tmp/relick-lobby
```

Las simulaciones automáticas de reglas existen solo en `tests/SimulationDriver.cs` y no se compilan en el juego ni en el servidor. Los fixtures pueden preparar manos explícitas; el flujo interactivo siempre solicita elegir una habilidad.

## Estructura

```text
core/                 Motor C# puro, catálogo, estado y proyecciones
scripts/              Sala, arena PvP, presentación y animaciones
scenes/Main.tscn       Entrada directa a OnlineLobby
network/              Conexión y bindings de SpacetimeDB
server/spacetimedb/    Módulo autoritativo
tests/                Reglas, simulaciones y pruebas de red
```

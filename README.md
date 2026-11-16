# Relick · PvP

Juego de cartas para **Godot 4.7.2 o posterior, edición .NET**, con interfaz en español y duelos de dos jugadores mediante SpacetimeDB. El juego abre directamente la pantalla de salas. Solo se ofrece PvP.

## Abrir y jugar

Necesitas Godot .NET 4.7.2 o posterior y el SDK de .NET 8. Abre `project.godot` y pulsa F5, o ejecuta:

```bash
./jugar.sh
```

El juego entra directo a la sala PvP. La primera vez pide iniciar sesión con SpacetimeAuth (correo, Discord u otra cuenta) y guarda la sesión para entrar directo la próxima vez. La cuenta conserva tu nombre y tus mazos, con su mica y su dado, en cualquier dispositivo.

1. Pulsa **Buscar rival** para una partida rápida casual. También puedes crear una sala privada o introducir el código de tu rival. Puedes cancelar mientras esperas en partida rápida.
2. Al encontrar rival o completarse la sala privada, ambos pasan al tablero.
3. Elige una habilidad entre las presentes en tu mazo. Esa carta y otras cuatro forman tu mano inicial.
4. Selecciona las cartas que quieres cambiar —también puedes cambiar la habilidad elegida— o conserva las cinco. Confirma y espera al rival.
5. Se resuelven los cambios, los Bonus y la iniciativa. Quien empieza roba una carta adicional.
6. Enseña habilidades, ataca, juega códigos, equipa o canjea cartas. Quien inicia no puede atacar durante el primer turno de la partida; el rival sí puede atacar en el siguiente, y después ambos pueden hacerlo. Las opciones visibles están autorizadas por el servidor.
7. Al terminar, cierra la sala para volver a la pantalla de creación y acceso. Durante preparación puedes cerrarla; durante un duelo puedes abandonar mediante su confirmación.

En el editor de mazos, la pestaña «Estilo» elige la mica y el dado de cada mazo ([detalles](docs/UI.md#personalización-de-mazos-micas-y-dados)).

### Servidor

El juego se conecta al servidor de desarrollo publicado en Maincloud, configurado en `network/server.cfg` (base `relick-pvp-dev-c2006ee7`). Para usar otra base, por ejemplo un servidor local, indica `RELICK_SERVER` y `RELICK_DATABASE`; en ese caso se entra sin cuenta, con la identidad del dispositivo, salvo que también indiques `RELICK_AUTH_CLIENT`. Cómo levantar el servidor local y publicar el módulo está en [PvP · Desarrollo local](docs/PVP.md#desarrollo-local).

### Opciones de `jugar.sh`

`jugar.sh` compila `Relick.csproj` y abre Godot. Busca `godot` o `godot4` en el `PATH`; `RELICK_GODOT=/ruta/a/godot` indica otro ejecutable. `--headless` abre Godot sin ventana; cualquier otra opción pasa al juego:

```bash
./jugar.sh --pvp-profile=dos                      # segunda identidad en el mismo equipo
./jugar.sh --headless --pvp-ui-test               # prueba de interfaz sin ventana
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --pvp-profile=uno
```

La forma con `--` de la documentación (`./jugar.sh --headless -- --pvp-ui-test`) sigue funcionando: lo anterior a `--` va a Godot y lo posterior, al juego.

## Exportar para Windows

Con Godot 4.7.2 .NET, el SDK de .NET 8 y las plantillas de exportación **.NET de la misma versión** instaladas, ejecuta desde Linux:

```bash
./export-windows.sh
```

El script importa los recursos, compila en modo Release para Windows x86_64 y crea `artifacts/Relick-windows-x86_64.zip`. Comprueba que se incluyan el ejecutable, los recursos, el ensamblado del juego y el runtime de .NET, y verifica la integridad del ZIP. Requiere Python 3 para empaquetar y acceso a NuGet durante la primera compilación para descargar el runtime de Windows.

Comparte el ZIP completo. El jugador debe extraerlo y abrir `Relick/Relick.exe`, conservando todos los archivos juntos. No necesita instalar Godot ni el SDK de .NET. Necesita internet porque el único modo de juego es PvP; esta versión incluye la conexión de desarrollo de `network/server.cfg`.

El preset `Windows Desktop` también está disponible en **Proyecto → Exportar**. Incluye explícitamente `network/server.cfg` y todos los recursos para conservar las imágenes cargadas dinámicamente. Las plantillas oficiales conservan su icono predeterminado; no se requiere Wine ni rcedit para generar este paquete. Antes de distribuir una versión nueva, comprueba el inicio y un duelo entre dos equipos en Windows.

## Exportar un APK para Android

```bash
./export-android.sh
```

Genera `artifacts/Relick-android-arm64.apk`, firmado con una clave de depuración local, para probar en dispositivos **Android 7.0 o posterior de 64 bits (ARM64)**. Copia el APK al teléfono, ábrelo y permite instalar aplicaciones desde la aplicación que lo abra. Requiere internet para jugar PvP y usa el servidor de desarrollo de `network/server.cfg`.

La exportación necesita Godot 4.7.2 .NET con sus plantillas Android .NET, **SDK de .NET 9**, OpenJDK 17 y Android SDK con `platform-tools` y `build-tools;35.0.1`. Configura `JAVA_HOME` y `ANDROID_HOME`; `RELICK_GODOT` permite indicar el ejecutable de Godot. Si se prepararon herramientas locales en `artifacts/android-toolchain/environment.sh`, el script carga ese entorno automáticamente. La configuración temporal del editor no modifica tus ajustes habituales.

Android compila para `net9.0`, requerido por la plantilla APK de Godot; escritorio conserva `net8.0`. El script verifica la firma, la alineación de las bibliotecas y la presencia del juego, runtime y configuración PvP antes de entregar el APK. Los registros están en `artifacts/android/`. Conserva `artifacts/android/debug.keystore` para poder actualizar la aplicación instalada con APK posteriores. Es una compilación de prueba; falta validar el inicio y un duelo en un dispositivo Android físico.

## Alcance actual

- Audio original: menú compartido, combate y clímax según el Relikario rival —incluidos los dos temas de Yuno—, efectos de robo, barajado, dado, curación y ataque, y volúmenes independientes guardados en el dispositivo.
- Micas y dados cosméticos por mazo, guardados en la cuenta y visibles para ambos jugadores durante el duelo.
- Yuno Gyambura y su evolución, con animaciones de reposo, ataque y daño. Las hojas actuales conservan la marca de agua de Ludo del recurso aportado.
- Treinta y una definiciones aportadas: Ataque, Magnet Shuffle, Destiny Shuffle, Colgante de la Suerte, Senshuni, Caja de Cartón, Vida Extra, Aumento de Relick Points, Huevo Azul Explosivo, Re-lanza, Error de Carga, Casco de Diamantes, Bomba de Tiempo, Código Konamy, Cambio de Mentes, Agilidad al 100%, Resetear la Mente, Esquivar, PC 8BITS, Mira de Sniper, Triple Ataque Cruzado, Relick Reverse Shuffle, Hoguera, Disquete, Estrella del Fontanero, Jessica Lee, Carga Rápida, Cartucho de Re-carga, Doblador de Tiempo, Tótem de Salvación y Tótem de Oro.
- Mazos de desarrollo simétricos de 42 cartas (doce Ataques y una copia de cada otra carta), cuatro espacios de habilidad con costes 0/300/500/1000 PR, Memoria, Corruptas, Bonus, canje, respuestas, COMBO y efectos de Relikia.
- Error de Carga tiene una única ventana antes de ejecutar cada activación. Admite contraerrores; cancelar deja la carta en memoria y devuelve acciones y PR sin ejecutar su efecto ni pagar sus descartes. Pasar cierra esa ventana, también durante dados, búsquedas y protecciones posteriores.
- Control privado «COMBO: preguntar / pasar» durante ambos turnos. Pasar conserva las cartas y omite sus respuestas opcionales, incluidos contraerrores, sin consumir acciones ni PR; mantiene las decisiones de habilidades, equipos y efectos ya iniciados. Persiste al reconectar.
- Selección de habilidad inicial, mulligan y combate en la misma arena; manos privadas, reintentos idempotentes y recuperación de decisiones pendientes.
- Primer agotamiento: D6, selección de cartas de la memoria y barajado. Segundo: dos vidas inevitables y comparación de vidas; igualdad es empate.
- Al terminar el turno, el jugador elige qué cartas descartar hasta tener un máximo de siete. Los efectos del descarte se resuelven antes de pasar el turno.
- Doblador de Tiempo descarta dos cartas para repetir la etapa actual del turno propio al terminarla. Combate renueva ataque, aprendizaje y usos de etapa; Acciones concede dos acciones nuevas. Se conservan los efectos y límites por turno.
- Tótem de Salvación permite destruir una copia para cancelar daño que llevaría a cero. Con dos o más copias, los fallos de ataque infligen daño normal y recibir daño consume una copia. Otro Tótem se apila; otro equipo sustituye toda la pila.
- Tótem de Oro coloca hasta dos Tótems de Salvación de la mano sin acciones de equipo adicionales y se coloca encima de un equipo. Sustituye automáticamente la destrucción de una copia de Salvación. No puede activarse con Relikia.
- Destreza: Yuno empieza con una ficha por partida. Puedes gastarla sin acciones ni PR para sumar +1 a un dado propio pendiente de 2, 3 o 4; no se recupera al cambiar de turno o evolucionar.
- Evolucionar a Yuno Lv 2 recupera hasta dos vidas sin superar las ocho iniciales. En cada tirada o relanzamiento del D6 de ataque, Yuno Lv 2 suma +1 salvo si sale 1; el resultado máximo es 6. Es un efecto de su evolución: los dados de daño, otros efectos y otros Relikarios no reciben ese aumento automáticamente.
- El premio del ataque se entrega después de resolver el daño y las decisiones pendientes del combate. Cancelar solo el daño conserva el premio; negar el ataque completo lo impide.
- Interfaz horizontal adaptable a PC, tablet y teléfono. Exportación APK de prueba para Android ARM64; la validación en hardware Android y la exportación para iOS siguen pendientes.
- Servidor autoritativo: el cliente presenta información autorizada y envía intenciones; no decide dados, daño ni legalidad del duelo.

La [clasificación indicada por el usuario](docs/CARD_TAXONOMY.md) es la referencia para ampliar efectos y deckbuilding. Documenta habilidades genéricas/específicas, equipos ligeros, áreas orientables y códigos Jugada/Acción/Trampa con extradeck, distinguiendo la especificación de lo ya implementado. Desde la sala de duelos, **Mazo** abre los mazos guardados y el constructor. **Nuevo mazo** pide primero el Relikario: se listan los 18, pero solo los programados (por ahora, Yuno) se pueden elegir, y su carta gira entre el nivel 1 y el 2. Las reglas del mazo: 42 cartas con exactamente 12 Ataques, al menos 3 habilidades compatibles, hasta 2 copias por nombre, un mazo aparte de hasta 3 Códigos Trampa y un banquillo de hasta 10 cartas sin Ataques (los límites de copias cuentan mazo y banquillo juntos). El banquillo se guarda con el mazo; el intercambio entre duelos de un encuentro aún no está en el gameplay. Las reglas están en `core/DeckRules.cs` y el servidor las aplica al repartir. El catálogo aún no está completo.

## Documentación

- [PvP, configuración, servidor y pruebas en línea](docs/PVP.md).
- [Interfaz, animaciones y optimizaciones](docs/UI.md).
- [Stack y diseño del duelo en línea](docs/STACK.md).
- [Audio: música original por rival, clímax, efectos y controles de volumen](docs/MUSIC.md).
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

La primera orden ejecuta las pruebas del motor. La prueba de interfaz trabaja sin servidor: comprueba cuatro tamaños horizontales, ambos asientos, privacidad, selección de habilidad sin reconstruir el tablero, mulligan, recuperación y animaciones. `./jugar.sh --headless --pvp-ui-test` hace lo mismo compilando antes. Los filtros de la matriz (`--lobby-only`, `--decks-only`, `--cosmetics-only`…) están en [Interfaz · Validación](docs/UI.md#validación). Para capturas renderizadas:

```bash
./jugar.sh --pvp-ui-test --ui-screenshot-dir=/tmp/relick-ui
./jugar.sh --lobby-preview-dir=/tmp/relick-lobby
```

Las pruebas de red (`tests/pvp`) necesitan un servidor con el módulo publicado; por defecto apuntan a `http://127.0.0.1:3100` y la base `relick-dev`, publicada con `tests/server/dev`, que nunca se publica en Maincloud:

```bash
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj
```

Sus variantes (`--quick-match`, `--decks`, `--connection-health`, `--login`) y las pruebas de interfaz con dos procesos están en [PvP · Desarrollo local](docs/PVP.md#desarrollo-local).

Las simulaciones automáticas de reglas existen solo en `tests/SimulationDriver.cs` y no se compilan en el juego ni en el servidor. Los fixtures pueden preparar manos explícitas; el flujo interactivo siempre solicita elegir una habilidad.

## Estructura

```text
core/                 Motor C# puro, catálogo, estado y proyecciones
scripts/              Sala, arena PvP, presentación y animaciones
scenes/Main.tscn       Entrada directa a OnlineLobby
network/              Conexión y bindings de SpacetimeDB
server/spacetimedb/    Módulo autoritativo
tests/                Reglas, simulaciones y fixtures
tests/pvp/            Pruebas de red contra un servidor publicado
tests/server/dev/     Módulo con autenticación de desarrollo, solo local
```

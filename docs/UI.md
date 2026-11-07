# Tablero compartido y pantallas móviles

Práctica y PvP usan `ArenaPresentation` para construir los mismos paneles de jugadores, medidores, zonas, habilidades, fases, canje, Bonus y mano. `ArenaMotion` comparte la trayectoria de cartas y el movimiento de barajado. Los retratos, cartas y dado 3D también son los mismos componentes.

Los controladores permanecen separados: práctica consulta el motor local; `OnlineArena` consume únicamente la proyección autorizada y envía una opción del servidor con sala y revisión. No se construye un motor con cartas ocultas en el cliente PvP ni se predicen daño, dados o costes.

## Interacción

- Toca una carta para seleccionarla. La práctica actualiza su acción principal; PvP muestra sus jugadas autorizadas.
- Toca una habilidad propia para elegir con cuál atacar. En PvP, **Ver carta** y **Ver habilidad** abren sus detalles. En práctica compacta están en **Menú**.
- La defensa con Destiny permite seleccionar varias cartas y exige la cantidad indicada por el servidor.
- Memoria, equipo, cartas sobre Yuno, Bonus y canje conservan sus consultas. La mano rival solo enseña las revelaciones permitidas. La memoria corrupta muestra cantidades y reversos.
- La preparación PvP usa las ilustraciones y selección de cartas, conservando la confirmación independiente de cada jugador.

## Distribución adaptable

La disposición amplia conserva el tablero enfrentado. Con menos de 1100 unidades lógicas de ancho, el tablero puede desplazarse verticalmente y los controles secundarios pasan a un menú. La mano tiene desplazamiento horizontal independiente.

En pantallas cortas y horizontales (altura menor de 600 y ancho de al menos 700), el tablero queda a la izquierda y la mano/acciones a la derecha. Esto evita que una mano inferior ocupe casi toda la altura. Los dispositivos pequeños en vertical conservan la mano debajo. Los resúmenes de vidas permanecen fuera del desplazamiento del tablero.

La configuración móvil tiene en cuenta densidad de pantalla y área segura. Los botones principales usan al menos 48 unidades lógicas, los menús aumentan la separación entre elementos y los detalles se pueden abrir con un toque. Los diálogos largos de cartas, elecciones y Relikia se adaptan mediante desplazamiento. Se habilita la entrada de ratón emulada por el toque para los controles de Godot.

Esto prepara la interfaz; **no constituye una exportación Android/iOS ni una validación en hardware móvil**. Aún hay que comprobar teclado virtual, zonas seguras, consumo, rendimiento y funcionamiento táctil en los dispositivos elegidos.

## Animaciones y recuperación

El servidor adjunta señales visuales de la última revisión aceptada: dados, ataque, daño, robos, devoluciones, barajado, Bonus y movimientos a memoria/corruptas. No incluyen identificadores de cartas, mazos, registros ni cintas de azar. El cliente utiliza únicamente caras ya autorizadas por sus vistas para representar una carta.

Las señales permiten representar dos tiradas consecutivas con el mismo resultado. El dado anima el resultado recibido; no genera azar para las reglas. Los retratos reaccionan a las señales reales de ataque y daño.

La primera sincronización, una reconexión o un salto de revisiones restauran el estado sin reproducir efectos antiguos. Si llega una revisión nueva antes de terminar una animación, se descarta la cola anterior para no acumular retraso. Los vuelos se omiten cuando sus zonas están fuera del área visible o recortadas por un panel desplazable; el tablero sigue mostrando el estado actual.

El formato de las tablas y las reglas no cambian. La información adicional está en el JSON del tablero preparado; no se envía el estado interno completo ni se amplía el historial de auditoría.

## Optimizaciones

- Seleccionar una carta/habilidad actualiza selección y acciones sin reconstruir el tablero completo. Se conservan retratos y medidores al actualizar una revisión.
- PvP reconstruye la presentación al cambiar la revisión o distribución; no lo hace cada fotograma sin cambios.
- Las cartas suspenden su procesamiento de hover cuando ya no necesitan animarlo.
- Los retratos ocultos no avanzan su reproducción; los visibles solo solicitan redibujado cuando cambia su fotograma.
- El viewport 3D del dado deja de actualizar continuamente una pose inmóvil.
- Las hojas originales de Yuno permanecen intactas. Godot importa versiones de la mitad de ancho y alto, manteniendo su cuadrícula y todos sus fotogramas. El área de píxeles resultante es una cuarta parte.
- Las carpetas de pruebas, servidor, documentación, resultados de generación y artefactos tienen `.gdignore` para no importarlas como recursos del juego.

Medición de los tres recursos `.ctex` de Yuno en esta máquina:

| Recurso importado | Antes | Después |
|---|---:|---:|
| Reposo | 17,043,638 bytes | 6,081,800 bytes |
| Daño | 16,980,420 bytes | 6,105,614 bytes |
| Ataque | 12,783,188 bytes | 4,968,092 bytes |
| **Total** | **44.6 MiB** | **16.4 MiB** |

La reducción medida es aproximadamente 63 %. Es tamaño de recursos importados, no una medición de RAM/VRAM, tamaño total del instalador o mejora de FPS. Los datos están en `artifacts/shared-arena-textures.json`.

## Validación reproducible

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
./jugar.sh --headless -- --shared-arena-test
```

La prueba de interfaz recorre 1440×1024, 390×844, 844×390 y 320×568 en ambos modos. Comprueba límites de pantalla, mano accesible, selección estable y privacidad desde el asiento 1. Además ejecuta las señales visuales, incluidos dados repetidos y devolución de cartas ocultas.

Para capturas renderizadas, ejecuta sin `--headless`:

```bash
./jugar.sh -- --shared-arena-test --ui-screenshot-dir=/tmp/relick-ui
```

Son capturas de estados de prueba; su texto de conexión no representa una sesión conectada. Se pueden consultar las capturas locales en `artifacts/shared-arena/`.

La prueba de combate de [PvP](PVP.md) también reconecta durante una tirada y verifica que no se repitan las animaciones. Las comprobaciones de dos procesos siguen usando clics y teclas a través del `Viewport`.

Validación de esta entrega: 117 comprobaciones del motor, 57 de red tanto locales como en Maincloud, dos procesos con perfiles independientes, un duelo completo de 69 jugadas con recuperación, regresión de entrada al abrir PvP, animaciones de práctica y la matriz de tamaños/señales descrita arriba. El módulo está publicado en la misma base de desarrollo `relick-pvp-dev-c2006ee7`.

Referencias de Godot: [resoluciones y escala](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html), [importación de imágenes](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_images.html).

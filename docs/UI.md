# Interfaz PvP y pantallas horizontales

El juego inicia directamente en `OnlineLobby`, desde `scenes/Main.tscn`. No se construye un motor local, un rival automático ni un tablero oculto al abrir las salas. El controlador de práctica y sus pruebas de interfaz fueron retirados; las simulaciones de reglas restantes solo se compilan en las pruebas .NET.

## Salas y preparación

La entrada agrupa el nombre del jugador y dos tarjetas: crear una sala o unirse con código. Usa el fondo de arena, superficies oscuras, acentos verde/azul y foco visible. En pantallas cortas se reducen márgenes y descripciones. Enter en el código permite unirse cuando hay conexión.

Cuando entra el segundo jugador, la misma arena representa elección de habilidad, reparto, mulligan y combate. La selección inicial muestra habilidades presentes en el propio mazo y recibe después otras cuatro cartas. La elegida puede devolverse en el mulligan.

El mulligan usa identificadores de copias: dos cartas iguales pueden seleccionarse por separado. Se bloquea durante el envío y después de confirmar. Una reconexión recupera la elección confirmada sin repetir el reparto. Se representan devoluciones propias, barajado visual, reemplazos, Bonus y robo de iniciativa. No se inventan identidades ni cantidades del mulligan rival que el servidor no haya autorizado.

## Presentación

`ArenaPresentation`, `ArenaMotion` y los controles de cartas, personajes, pilas y dado componen la arena PvP. `OnlineArena` solo consume las vistas autorizadas y envía una opción ligada a sala y revisión; no ejecuta ni predice las reglas.

- Vida, PR, fase, dueño del turno y cantidades de zonas permanecen consultables.
- Mano rival con reversos y únicamente las revelaciones autorizadas.
- Cartas, habilidades, Memoria, Corruptas, equipo, cartas sobre el Relikario, Bonus y canje tienen sus consultas.
- Las respuestas con descartes permiten selección múltiple y exigen la cantidad recibida del servidor.
- El dado muestra el resultado confirmado; su animación no consume azar del duelo.

## Distribución horizontal

PC, tablet y teléfono comparten alcance horizontal. El proyecto permite girar entre ambos lados del teléfono. Una ventana de escritorio alta conserva un lienzo horizontal con bandas.

Con menos de 1100 unidades lógicas de ancho, el campo admite desplazamiento vertical y los controles secundarios pasan al menú. En pantallas cortas —altura menor de 600— el tablero queda a la izquierda y mano/acciones a la derecha. La mano tiene desplazamiento horizontal independiente. Los objetivos táctiles principales miden al menos 48 unidades lógicas; los diálogos se ajustan al área disponible y admiten desplazamiento.

La configuración contempla densidad y área segura. No constituye una exportación Android/iOS ni una validación en hardware móvil; todavía deben comprobarse teclado virtual, rendimiento y consumo en dispositivos físicos.

## Actualizaciones y animaciones

- La selección de habilidad inicial actualiza las cartas y el botón existentes: no reconstruye la arena.
- Las referencias a cartas, habilidades propias y botón de evolución se reúnen al construir el tablero, evitando búsquedas del árbol en cada selección o cambio de habilitación.
- La mano de preparación se ordena y materializa solo al cambiar las filas de la suscripción. Una revisión local evita construir claves de preparación cada fotograma cuando los datos, la conexión y los envíos pendientes no cambian.
- Reconectar invalida la caché por época y vuelve a comprobar las versiones coherentes de tablero, mano y opciones. Retirar la suscripción de preparación invalida su mano mediante las eliminaciones de filas.
- Las señales visuales representan una revisión aceptada; tiradas consecutivas iguales siguen siendo eventos distintos. Una recuperación o salto de revisión restaura el estado sin reproducir efectos antiguos.
- Las cartas no mantienen procesamiento de hover cuando ya no necesitan animarlo. Los personajes ocultos no avanzan; los visibles solicitan redibujado cuando cambia su fotograma. El dado deja de actualizar continuamente su viewport 3D cuando está inmóvil.
- A cero vidas, Yuno usa un reposo agotado con respiración lenta, gesto preocupado y raspones. Se termina la reacción del último golpe antes de entrar al bucle. La vida recibida selecciona el reposo para ambos jugadores, también al recuperar la partida; volver a construir la arena no reinicia ese bucle.
- Cuando el motor aplica una curación efectiva, emite `Heal` con el destinatario y las vidas recuperadas. La arena reproduce una acción de 1,2 segundos con gesto de alivio y corazones verdes de píxel después del dado y de los movimientos de cartas pendientes; luego vuelve al reposo. La señal no contiene identidades de cartas y se descarta al recuperar el estado, igual que los demás efectos visuales.
- La geometría constante del icono de vida se calcula una sola vez. No se introdujo una caché global ilimitada de estilos o colores animados.

Los cinco atlas de Yuno conservan un lienzo lógico de 608×504 por cuadro. La importación limita el ancho a 2432 píxeles y mantiene la cuadrícula: reposo, ataque, daño, reposo agotado y curación usan el mismo tamaño visual. Los clips, su procedencia y sus prompts están documentados en [las animaciones de Yuno](../assets/characters/yuno/README.md).

## Validación

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
dotnet build Relick.csproj
./jugar.sh --headless -- --pvp-ui-test
```

La matriz recorre 1440×1024, 1024×768, 844×390 y 700×390 desde ambos asientos. Comprueba límites, privacidad, estabilidad de selección inicial, bloqueo tras confirmar, recuperación y terminación de animaciones. También comprueba el reposo a cero vidas para ambos jugadores/asientos, la transición tras el último golpe, la repetición, la reconstrucción del tablero y la recuperación visual. `--shared-arena-test` se mantiene como alias de esta prueba PvP; ya no ejecuta práctica.

Para capturas:

```bash
./jugar.sh -- --pvp-ui-test --ui-screenshot-dir=/tmp/relick-ui
./jugar.sh -- --lobby-preview-dir=/tmp/relick-lobby
```

La vista previa de salas no se conecta a ningún servidor. Las pruebas de preparación, combate y dos procesos con entrada real están descritas en [PvP](PVP.md).

# Etapa 2 · duelo PvP

Implementado y probado el 6 de noviembre de 2026 con CLI, Runtime y SDK 2.14.1:

- Módulo C# compilado para WASI con .NET 8.
- Crear sala con código aleatorio de ocho caracteres y unirse con nombre.
- Dos identidades por sala. Un tercer jugador recibe un rechazo.
- Reparto en servidor al entrar el segundo jugador: Magnet y cuatro cartas de los mismos mazos de práctica.
- Identificadores de instancia únicos para las 40 cartas. Mazo y mano persisten en tablas privadas.
- Vistas `my_room`, `my_hand`, `my_preparation` y `my_duel` filtradas por la identidad autenticada del solicitante.
- Confirmación independiente de ambos jugadores: conservar la mano o cambiar de una a cinco cartas una sola vez. Se guardan ambas selecciones antes de aplicar cambios. Solo su dueño recibe los identificadores seleccionados.
- Inicio transaccional al confirmar ambos: cartas devueltas al fondo de su propio mazo, reemplazos, Bonus, dos D6 de iniciativa (repitiendo empates) y robo de inicio de turno para quien empieza.
- Confirmaciones ligadas a sala e identificador de comando; los reintentos idénticos son seguros incluso después del inicio. Otra confirmación o una selección inválida no modifica la partida.
- Confirmación, elección e inicio almacenados en tablas privadas para recuperarlos al reconectar.
- Recuperación de la sala y las mismas cartas al pulsar Reconectar o volver a abrir PvP con el mismo perfil.
- Motor de reglas compartido en el servidor: enseñar, atacar, acciones, códigos, equipos, canje, evolución, Relikia, defensa, elecciones y COMBO con las cartas actuales.
- Tablero visual compartido con la práctica, vista propia de cada lugar y opciones autorizadas por el servidor. Incluye animaciones confirmadas y distribución adaptable a pantallas pequeñas.
- Cada jugada incluye sala, revisión esperada e identificador de comando. El servidor decide el actor, vuelve a calcular sus opciones y valida las selecciones. Una solicitud de un tablero anterior no puede apuntar a otra carta.
- Abandono que concede la victoria al rival. Cerrar sala después del resultado elimina sus miembros, cartas y registro. Un duelo que ya tiene jugadas no se puede borrar con Cerrar sala antes de terminarlo.

## Probar entre dos equipos

Abre el juego con `./jugar.sh` y pulsa **PvP**. Escribe tu nombre y crea una sala. El rival abre la misma versión del juego, pulsa PvP e introduce el código. Ambos verán los nombres de los jugadores y solo su propia mano. Marca las cartas que quieres cambiar o deja todas sin marcar para conservarlas, y pulsa confirmar. Cuando ambos confirmen, verán sus manos actualizadas, los Bonus públicos y los dados que deciden quién empieza. El jugador inicial tiene seis cartas por el robo de su primer turno. Después se abre automáticamente la arena PvP: pulsa una carta para filtrar sus jugadas o elige una opción general. Para defender con Destiny, selecciona las cartas a descartar y después pulsa la defensa del espacio elegido. El rival ve las decisiones y los resultados actualizados en su equipo. Abandonar duelo pide confirmación y concede la victoria al rival.

La conexión de desarrollo está en `network/server.cfg`:

- Servidor: `https://maincloud.spacetimedb.com`
- Base: `relick-pvp-dev-c2006ee7`
- Panel: <https://spacetimedb.com/relick-pvp-dev-c2006ee7>

Para probar dos identidades en la misma computadora, abre dos terminales:

```bash
./jugar.sh -- --pvp --pvp-profile=uno
./jugar.sh -- --pvp --pvp-profile=dos
```

Cada perfil guarda una credencial propia bajo los datos de usuario de Godot, separada por servidor y base. No utiliza las credenciales administrativas del CLI. No compartas esos archivos `.token`. Volver a práctica desconecta el cliente y conserva la sala para retomarla; **Cerrar sala** elimina la sala y sus datos para los dos jugadores cuando no hay un combate en curso.

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
spacetime publish relick-dev --server http://127.0.0.1:3100 --module-path server/spacetimedb --no-config
spacetime generate --module-path server/spacetimedb --lang csharp --out-dir network/bindings --namespace Relick.Network.Types --yes
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh -- --pvp --pvp-profile=uno
```

Repite la última orden con `--pvp-profile=dos` para el rival local. Las variables de entorno prevalecen sobre `network/server.cfg`. Los bindings están generados con 2.14.1 y se incluyen en el repositorio. No edites esos archivos manualmente ni generes con `--include-private`.

Pruebas contra un servidor real ya iniciado y con el módulo publicado:

```bash
dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj
```

Acepta servidor y base como argumentos para verificar otra publicación. La prueba crea identidades temporales, comprueba aislamiento, rechazo del tercer jugador, denegación de consultas privadas, selección de cero/dos/cinco cartas, solicitudes inválidas, espera mutua, Bonus, iniciativa, conservación de cartas, reintentos y reconexión antes/después del inicio. También completa un duelo, reconecta durante el combate, comprueba abandono, descarta jugadas obsoletas y verifica que el estado interno no sea consultable; cierra sus salas al finalizar. Las reglas locales se comprueban por separado:

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
```

Prueba de la interfaz de Godot contra el servidor local (confirma mediante sus botones, comprueba el bloqueo de selección y cierra su sala):

```bash
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh -- --pvp --pvp-profile=preparation-ui-test --pvp-preparation-test
```

Puedes añadir `--pvp-screenshot=/tmp/relick-preparation.png` para capturar el resultado usando la ventana de Godot. El perfil de prueba no debe tener otra sala activa.

Prueba de un duelo mediante los botones de la arena y un segundo cliente real:

```bash
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh -- --pvp --pvp-profile=combat-ui-test --pvp-combat-test --pvp-screenshot=/tmp/relick-combat.png
```

Regresión de entrada al arrancar directamente en PvP (no necesita un servidor disponible):

```bash
RELICK_SERVER=http://127.0.0.1:3199 RELICK_DATABASE=input-check ./jugar.sh --headless -- --pvp --pvp-profile=input-test --pvp-input-test
```

Inyecta clics y teclas mediante el `Viewport`, sin emitir las señales de los botones directamente. Comprueba nombre, código, regreso a práctica, continuación del reparto y reapertura. La práctica pausada no debe consumir eventos ni mantener sus capas de animación visibles sobre PvP, tampoco si se vuelve a renderizar.

Para probar dos procesos con entrada real de la interfaz contra el servidor local publicado, ejecuta estas órdenes en dos terminales. Usa una ruta de sesión nueva en cada ejecución, compartida por ambas órdenes:

```bash
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --headless -- --pvp --pvp-profile=input-host --pvp-input-peer=host --pvp-input-session=/tmp/relick-input-session-1
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --headless -- --pvp --pvp-profile=input-guest --pvp-input-peer=guest --pvp-input-session=/tmp/relick-input-session-1
```

Comprueba identidades independientes, escritura de nombres/código, creación y entrada a sala, cambio de una carta por jugador, confirmación, apertura/cierre de memoria y abandono confirmado con resultado compartido. Al terminar, cierra la sala. Los diálogos se integran en la ventana durante esta prueba para permitir su manejo sin pantalla.

## Persistencia, privacidad y escalabilidad

El servidor guarda el estado actual del duelo y sus decisiones pendientes. Las vistas leen paquetes ya preparados de tablero, mano privada y opciones; no reconstruyen la partida a partir del historial. Los registros anteriores compatibles se convierten una sola vez al reconectar. Las tablas de estado, auditoría y comprobantes permanecen privadas.

El protocolo actualizado distingue entre jugadas aceptadas y rechazos que consumen cuota. Después de actualizar, vuelve a abrir el juego con `./jugar.sh` para usar el cliente actual.

Se añadieron limpieza automática, límites operativos y comprobaciones de migración, compactación, reinicio y concurrencia. Las salas sin preparar caducan tras 10 minutos sin actividad; los duelos, tras 2 horas sin jugadas ni reconexión; los resultados, tras 24 horas. Cerrar una sala elimina sus datos para ambos participantes.

Consulta [el informe de escalabilidad](SCALABILITY.md) para el diseño, políticas, pruebas reproducibles y mediciones locales. Las mediciones locales no equivalen a capacidad de Maincloud.

## Límites de esta versión

El recorrido funcional es sala → preparación → duelo completo → resultado. Práctica y PvP comparten la presentación del tablero; véase [interfaz adaptable, animaciones y optimizaciones](UI.md). Se probaron tamaños móviles simulados y capturas; la exportación y validación en Android/iOS físicos siguen pendientes.

Las identidades son de dispositivo con token persistente. La reconexión es manual con Reconectar o reabriendo PvP con el mismo perfil; los envíos pendientes en la misma sesión reutilizan su identificador. Aún faltan cuentas entre dispositivos, controles de tráfico/conexiones en infraestructura, emparejamiento y clasificación. La auditoría reciente no garantiza conservar una repetición completa de cada partida.

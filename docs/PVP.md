# Etapa 2 · duelo PvP

Implementado y probado el 6 de noviembre de 2026 con CLI, Runtime y SDK 2.14.1:

- Módulo C# compilado para WASI con .NET 8.
- Crear sala con código aleatorio de ocho caracteres y unirse con nombre.
- Dos identidades por sala. Un tercer jugador recibe un rechazo.
- Al entrar el segundo jugador se preparan ambos mazos. Cada participante elige una habilidad de su propio mazo antes del mulligan; el servidor extrae una copia, baraja el resto y reparte otras cuatro cartas.
- Identificadores de instancia únicos para las 40 cartas. Mazo y mano persisten en tablas privadas.
- Vistas `my_room`, `my_hand`, `my_preparation` y `my_duel` filtradas por la identidad autenticada del solicitante.
- Confirmación independiente de ambos jugadores: conservar la mano o cambiar de una a cinco cartas una sola vez. Se guardan ambas selecciones antes de aplicar cambios. Solo su dueño recibe los identificadores seleccionados.
- Inicio transaccional al confirmar ambos: cartas devueltas al fondo de su propio mazo, reemplazos, Bonus, dos D6 de iniciativa (repitiendo empates) y robo de inicio de turno para quien empieza.
- Confirmaciones ligadas a sala e identificador de comando; los reintentos idénticos son seguros incluso después del inicio. Otra confirmación o una selección inválida no modifica la partida.
- Confirmación, elección e inicio almacenados en tablas privadas para recuperarlos al reconectar.
- Recuperación de la sala y las mismas cartas al pulsar Reconectar o volver a abrir PvP con el mismo perfil.
- Motor de reglas compartido en el servidor: enseñar, atacar, acciones, códigos, equipos, canje, evolución, Relikia, defensa, elecciones y COMBO con las cartas actuales.
- Tablero visual PvP, vista propia de cada lugar y opciones autorizadas por el servidor. Incluye animaciones confirmadas y distribución adaptable a pantallas pequeñas.
- Cada jugada incluye sala, revisión esperada e identificador de comando. El servidor decide el actor, vuelve a calcular sus opciones y valida las selecciones. Una solicitud de un tablero anterior no puede apuntar a otra carta.
- Abandono que concede la victoria al rival. Cerrar sala después del resultado elimina sus miembros, cartas y registro. Un duelo que ya tiene jugadas no se puede borrar con Cerrar sala antes de terminarlo.

## Probar entre dos equipos

Abre el juego con `./jugar.sh`: entrarás directamente a las salas PvP. Escribe tu nombre y crea una sala. El rival abre la misma versión e introduce el código. Al entrar el segundo jugador se abre el tablero para ambos, en **Preparación**: aparecen los Relikarios y primero eliges una habilidad entre las de tu mazo. Después se reparte esa habilidad junto a otras cuatro cartas; la del rival permanece boca abajo. Toca las cartas que quieres cambiar —incluida la habilidad elegida, si quieres— o conserva las cinco, y confirma. Tu selección queda bloqueada mientras esperas al rival. Cuando ambos confirman, se muestran tus devoluciones, el barajado visual, los reemplazos y los Bonus; el resumen de iniciativa identifica quién empieza y se anima su robo de turno. El combate continúa en el mismo tablero. Reconectar conserva una preparación confirmada. En combate, pulsa una carta para filtrar sus jugadas o elige una opción general. Para defender con Destiny, selecciona los descartes y después la defensa del espacio elegido. Abandonar duelo pide confirmación y concede la victoria al rival.

La conexión de desarrollo está en `network/server.cfg`:

- Servidor: `https://maincloud.spacetimedb.com`
- Base: `relick-pvp-dev-c2006ee7`
- Panel: <https://spacetimedb.com/relick-pvp-dev-c2006ee7>

Para probar dos identidades en la misma computadora, abre dos terminales:

```bash
./jugar.sh -- --pvp --pvp-profile=uno
./jugar.sh -- --pvp --pvp-profile=dos
```

Cada perfil guarda una credencial propia bajo los datos de usuario de Godot, separada por servidor y base. No utiliza las credenciales administrativas del CLI. No compartas esos archivos `.token`. Cerrar la aplicación desconecta el cliente y conserva la sala para retomarla hasta su vencimiento; **Cerrar sala** elimina la sala y sus datos para los dos jugadores cuando no hay un combate en curso.

La selección inicial se guarda en una tabla privada y se consulta mediante `my_initial_skill`, que solo expone las opciones y la elección del propio jugador. Las cartas se reparten después de la elección; no se puede volver a elegir ni confirmar mulligan antes de tener la mano. Los reintentos con el mismo identificador son idempotentes, y reconectar conserva la habilidad y las cartas. Al cerrar o vencer la sala se elimina también esa selección. Las salas anteriores que ya tenían manos repartidas continúan con esas manos, sin exigir elegir ni volver a repartir.

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

Prueba de la interfaz de Godot contra el servidor local (abre el tablero antes del mulligan, selecciona mediante clics, confirma, reconecta con la mano bloqueada, comprueba que el combate conserva la misma arena y cierra su sala):

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
RELICK_SERVER=http://127.0.0.1:3199 RELICK_DATABASE=input-check ./jugar.sh --headless -- --pvp --pvp-profile=input-test --pvp-ui-test
```

Inyecta clics y teclas mediante el `Viewport`, sin emitir directamente las señales de los botones. El juego inicia en salas PvP; la matriz sin conexión comprueba que no existan accesos a práctica.

Para probar dos procesos con entrada real de la interfaz contra el servidor local publicado, ejecuta estas órdenes en dos terminales. Usa una ruta de sesión nueva en cada ejecución, compartida por ambas órdenes:

```bash
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --headless -- --pvp --pvp-profile=input-host --pvp-input-peer=host --pvp-input-session=/tmp/relick-input-session-1
RELICK_SERVER=http://127.0.0.1:3100 RELICK_DATABASE=relick-dev ./jugar.sh --headless -- --pvp --pvp-profile=input-guest --pvp-input-peer=guest --pvp-input-session=/tmp/relick-input-session-1
```

Comprueba identidades independientes, escritura de nombres/código, creación y entrada a sala, cambio de una carta por jugador dentro del tablero, confirmación y continuidad en la misma arena, apertura/cierre de memoria y abandono confirmado con resultado compartido. Al terminar, cierra la sala. Los diálogos se integran en la ventana durante esta prueba para permitir su manejo sin pantalla.

## Persistencia, privacidad y escalabilidad

El servidor guarda el estado actual del duelo y sus decisiones pendientes. Las vistas leen paquetes ya preparados de tablero, mano privada y opciones; no reconstruyen la partida a partir del historial. Los registros anteriores compatibles se convierten una sola vez al reconectar. Las tablas de estado, auditoría y comprobantes permanecen privadas.

El protocolo actualizado distingue entre jugadas aceptadas y rechazos que consumen cuota. Después de actualizar, vuelve a abrir el juego con `./jugar.sh` para usar el cliente actual.

Se añadieron limpieza automática, límites operativos y comprobaciones de migración, compactación, reinicio y concurrencia. Las salas sin preparar caducan tras 10 minutos sin actividad; los duelos, tras 2 horas sin jugadas ni reconexión; los resultados, tras 24 horas. Cerrar una sala elimina sus datos para ambos participantes.

Consulta [el informe de escalabilidad](SCALABILITY.md) para el diseño, políticas, pruebas reproducibles y mediciones locales. Las mediciones locales no equivalen a capacidad de Maincloud.

## Límites de esta versión

El recorrido funcional es sala → preparación → duelo completo → resultado. La arena PvP usa componentes de presentación separados del motor; véase [interfaz adaptable, animaciones y optimizaciones](UI.md). Se probaron tamaños móviles simulados y capturas; la exportación y validación en Android/iOS físicos siguen pendientes.

Las identidades son de dispositivo con token persistente. La reconexión es manual con Reconectar o reabriendo PvP con el mismo perfil; los envíos pendientes en la misma sesión reutilizan su identificador. Aún faltan cuentas entre dispositivos, controles de tráfico/conexiones en infraestructura, emparejamiento y clasificación. La auditoría reciente no garantiza conservar una repetición completa de cada partida.

## Estado de la elección inicial de habilidad

La elección previa al mulligan está publicada en `relick-pvp-dev-c2006ee7` en Maincloud. La migración agregó `initial_skill` (tabla privada), `my_initial_skill` (vista propia) y `choose_initial_skill` (comando). No fue necesario limpiar tablas ni reiniciar datos: se aplicó la actualización con `--delete-data=never`.

Validación: 125 comprobaciones del motor; 65 comprobaciones de red tanto en una base local aislada como en Maincloud, incluido un duelo completo; selección y mulligan mediante Godot con reconexión en Maincloud; dos procesos independientes con entrada real en 844×390; y revisión gráfica de cuatro tamaños desde ambos asientos. Las salas creadas por las pruebas se cierran al finalizar.

Vuelve a abrir el juego con el cliente actualizado para utilizar la elección inicial en el PvP remoto. Las salas que ya tenían manos repartidas siguen con esas manos; las salas nuevas solicitan la habilidad antes de repartir las otras cuatro cartas.

## PvP como único modo y optimizaciones aplicadas

El arranque de `scenes/Main.tscn` carga directamente `OnlineLobby`. Se retiraron el controlador de práctica, sus accesos y el bot del ejecutable. El perfil de usuario anterior conserva su ubicación para recuperar identidades. Las simulaciones automáticas solo están en el proyecto de pruebas.

La consulta de candidatos de Memoria comprueba recursos y etapa una vez y realiza un recorrido. La mano de preparación se ordena por cambios de filas y la arena reacciona a una revisión local; reconectar invalida esas cachés. La selección de habilidad conserva los controles y actualiza sus referencias tipadas.

El motor actualizado está publicado en la misma base de desarrollo de Maincloud sin cambios de esquema ni borrado. Pasaron 125 comprobaciones del motor y 65 de red tanto locales como remotas, un duelo completo, la preparación con reconexión en Godot, dos clientes independientes en 844×390 y la revisión gráfica de cuatro tamaños y ambos asientos. Las pruebas verifican que el ensamblado del juego no incluya ni el antiguo controlador ni el simulador de pruebas.

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

## Curación e intensidad de los impactos

El módulo publicado emite `Heal` únicamente al recuperar vidas de forma efectiva, con el jugador y la cantidad recuperada. El cliente representa una curación arcade con corazones verdes y gesto de alivio. Las curaciones anuladas, pendientes de resolver o que no recuperan vida no emiten esa señal.

La señal `Hit` incluye `AttackDie`, separado de la cantidad de daño y de los dados secundarios: 4 conserva la reacción reducida anterior, 5 selecciona el impacto normal y 6 el crítico. El valor procede del resultado final del ataque después de relanzamientos/modificadores y persiste durante la decisión de defensa. El daño sin ataque utiliza 0 y conserva la reacción genérica. Estas señales no contienen identidades de cartas; una recuperación de estado no vuelve a reproducirlas.

Ambas actualizaciones están publicadas en `relick-pvp-dev-c2006ee7` con `--delete-data=never`, sin cambios de tablas ni borrado. La última validación pasó 131 comprobaciones del motor, 65 de red en Maincloud y la prueba de interfaz de Godot en modo gráfico y sin ventana. Se verificaron los tres impactos, su transición al reposo agotado a cero vidas y las curaciones para ambos jugadores/asientos, junto a la matriz de cuatro tamaños. La partida remota verificó además el transporte del dado final en tres impactos reales. Las salas de prueba se cerraron al finalizar.

## Casco de Diamantes

Equipo normal que resta dos a cada daño recibido, después de calcular el daño del ataque y con mínimo cero. Si absorbe todo el daño, no pierde vida, no emite impacto ni activa la derrota por daño a cero vidas. No reduce las dos vidas inevitables del segundo agotamiento del mazo.

Un 1 en cualquier tirada de ataque destruye inmediatamente los cascos equipados de ambos jugadores y los envía a sus respectivas memorias. Incluye relanzamientos de Yuno, Colgante y Re-lanza: cambiar después el resultado no recupera el equipo. Los unos de iniciativa, curación, reciclaje o daño secundario de Magnet no lo destruyen. Utiliza las reglas comunes de equipamiento, sustitución, cancelación y persistencia.

Las salas nuevas conservan mazos de 20 cartas: una copia del Casco sustituye una de Colgante. Las partidas existentes mantienen sus cartas. Publicado sin borrado en la misma base de desarrollo; validado con 139 comprobaciones del motor, la matriz de interfaz de Godot y 65 comprobaciones PvP remotas. La imagen restaurada y el prompt están en `assets/cards/`.

## Agotamiento del mazo

Al retirar la última carta, o intentar robar de un mazo vacío, el primer agotamiento lanza un D6. Tras resolver los relanzamientos y modificadores permitidos, el dueño elige exactamente esa cantidad de cartas de su memoria normal, las devuelve al mazo y lo baraja. Si hay menos cartas disponibles, elige todas; las corruptas no participan. Una memoria vacía también lanza el dado y continúa sin elección. Un nuevo intento de robo sin cartas resuelve el segundo agotamiento.

La elección pausa los robos pendientes y la reposición de Bonus; se reanudan una sola vez después de barajar. Si se agota el mazo rival durante tu turno, decide su dueño. Re-lanza puede modificar el dado de reciclaje; su robo adicional espera a que se reponga el mazo para no contar la misma falta de cartas como un segundo agotamiento. Error de Carga puede responder al COMBO, pero no cancelar el reciclaje obligatorio.

En el segundo agotamiento, el dueño pierde dos vidas inevitables, con mínimo cero. No se aplica reducción de equipo ni la derrota ordinaria por recibir daño a cero vidas: inmediatamente se comparan ambas vidas. Gana quien tenga más; una igualdad es empate, también con ambos a cero.

La arena muestra la memoria seleccionable durante esta decisión y conserva la mano habitual para el rival. El servidor valida propietario, revisión, cantidad, duplicados y límites; admite hasta seis selecciones. El estado persistido conserva el dado final y la continuación, de modo que reconectar no repite el lanzamiento ni los robos.

Publicado junto con Bomba de Tiempo en `relick-pvp-dev-c2006ee7`, sin cambios de tablas ni borrado (`--delete-data=never`). Validación: 157 comprobaciones del motor y 69 PvP remotas; la partida dedicada verificó elección de cinco cartas, recuperación de la decisión al reconectar, rechazo de cantidades inválidas y comparación final de vidas. Las salas de prueba se cerraron al terminar. La matriz de interfaz cubre hasta seis cartas de memoria para ambos jugadores/asientos.

## Código Konamy

Código de tipo Jugada, sin COMBO: una acción y cero PR. Elige una carta que no sea habilidad del mazo propio y la añade a la mano. No se puede activar sin objetivos legales, tampoco mediante Relikia. El mazo restante conserva su orden porque el texto no indica barajar. Si se retira su última carta, se aplican las reglas de agotamiento.

La decisión enumera las ilustraciones de los objetivos únicamente en las opciones privadas del dueño. El rival no recibe el contenido ni el orden del mazo y la carta añadida permanece oculta en la mano. El servidor valida el índice del mazo asociado a la revisión y rechaza habilidades o decisiones ajenas. La elección pendiente se restaura al reconectar.

Error de Carga puede anularla únicamente en la ventana inicial, antes de empezar la búsqueda, devolviendo los costes. Una vez que se deja resolver, la selección ya no puede cancelarse con Error. Caja de Cartón no responde a códigos Jugada. Activarla desde memoria cuesta dos acciones y 400 PR; la carta solo se corrompe después de superar las respuestas. Una copia sustituye una de Caja en los mazos nuevos; las partidas existentes conservan sus cartas.

También se corrigió un bloqueo previo de Relikia: si un COMBO consume la última carta de la mano durante el reciclaje, la colocación posterior termina sin pedir una carta inexistente.

Publicado en `relick-pvp-dev-c2006ee7` con `--delete-data=never`, sin cambios de tablas. Validación: 167 comprobaciones del motor, 69 PvP remotas y cinco búsquedas con identidades de cartas privadas verificadas entre ambos clientes. La interfaz pasó la matriz completa sin ventana y la matriz gráfica específica de búsqueda en cuatro tamaños, para ambos jugadores/asientos, con la confirmación visible antes de la inspección de carta.

## Cambio de Mentes

Código Jugada, sin COMBO: cuesta una acción y cero PR. No puede activarse en el turno 1 global de la partida, tampoco mediante Relikia. Está disponible desde el turno 2, aunque sea el primer turno personal de ese jugador.

Intercambia ambas manos completas al resolver, incluyendo manos vacías o de tamaños distintos. La carta jugada ya está en memoria y los códigos usados para responder también han salido de las manos: únicamente se intercambian las cartas restantes. No roba, descarta ni baraja y no activa agotamiento. Las demás zonas, vidas, PR y acciones de cada jugador permanecen en su sitio, con los costes habituales de la activación.

Error de Carga puede cancelarla antes de resolver; un contraerror permite resolver con las manos resultantes. Caja de Cartón no responde a códigos Jugada. Desde memoria exige dos acciones y 400 PR y corrompe el código; si se anula, conserva la restitución común de costes.

Las marcas de revelación siguen las copias transferidas. El resto de cartas continúa oculto en la vista del rival y los robos posteriores no heredan las marcas de los antiguos espacios. El estado y las versiones de ambas manos se recuperan al reconectar. Las señales de transferencia solo transportan origen, destinatario y cantidad, sin identidades; la interfaz anima un paquete boca abajo por mano para mantener una duración acotada con manos grandes.

Una copia sustituye una de Vida Extra en los mazos nuevos, conservando veinte cartas. Las partidas existentes conservan sus cartas.

Publicado en `relick-pvp-dev-c2006ee7` con `--delete-data=never`, sin cambios de tablas. Validación: 177 comprobaciones del motor, 69 PvP remotas y tres intercambios reales con manos, destinatarios y revelaciones verificados. La interfaz completa pasó sin ventana y la matriz gráfica específica cubrió cuatro tamaños, ambos jugadores/asientos, manos vacías o desiguales y animaciones sin filtrar identidades.

## Retirada del barrido de caducidad antiguo

Se eliminaron `legacy_sweep_control`, `legacy_expiry_sweep` y el reducer `sweep_legacy_expiry`, además de la llamada desde `ClientConnected` y sus tipos generados de cliente y pruebas. Las salas siguen recibiendo y renovando sus vencimientos en `room_expiry`; `expire_room` conserva la renovación segura y la restricción de ejecución al planificador.

La retirada se publica en dos pasos con `--delete-data=never`: primero un módulo temporal desactiva el barrido manteniendo el esquema anterior; tras vaciar únicamente la fila de control y comprobar que la cola está vacía, el módulo definitivo retira las dos tablas. No se reinicia la base ni se vacían tablas de salas o partidas. SpacetimeDB desconecta los clientes al retirar tablas; pueden volver a conectarse con su identidad habitual.

Verificado en el esquema publicado de `relick-pvp-dev-c2006ee7`: las dos tablas y el reducer retirados están ausentes; `room_expiry`, `expire_room`, `room` y `current_duel` siguen presentes. El ensayo local conservó una sala y su temporizador a través de la retirada. Pasaron tres comprobaciones de caducidad, cinco de persistencia y 69 PvP remotas; el cliente y los módulos principal y de pruebas compilan correctamente.

## Única función para enviar jugadas

Se retiró `play_duel`, que únicamente rechazaba llamadas de clientes antiguos con una indicación de actualizar. La función activa conserva su nombre `play_duel_v_2`, sus parámetros y la confirmación privada mediante `my_move_result`. Los bindings del cliente y de las pruebas se regeneraron sin la función obsoleta; se retiró también la comprobación que esperaba su mensaje de actualización. No cambian las tablas ni las reglas de las partidas.

Publicado en `relick-pvp-dev-c2006ee7` con `--delete-data=never`. El esquema remoto confirma que `play_duel` está ausente y conserva `play_duel_v_2` y `my_move_result`. Pasaron 68 comprobaciones PvP remotas; el cliente y los módulos principal y de pruebas compilan correctamente.

## Preferencia privada de respuestas COMBO

El reducer `set_combo_responses` permite a cada miembro cambiar únicamente su
propia preferencia durante un duelo. No recibe un actor del cliente. Usa un
identificador idempotente y los comprobantes privados existentes: reintentar un
comando anterior no vuelve a aplicar su valor ni revierte una preferencia posterior.
No añade tablas ni cambia los parámetros de las jugadas ordinarias.

El estado se conserva en el snapshot privado y se proyecta en `DuelPrivateData`.
Si cambiarlo no resuelve una respuesta pendiente, solo se actualizan el paquete
y las opciones del solicitante, conservando sus versiones. La revisión pública,
el paquete del rival y las animaciones permanecen intactos. El cliente actualiza
el control y vuelve a enlazar las opciones sin reconstruir el tablero, conservando
la selección actual. Rechaza botones de un snapshot anterior incluso cuando la
revisión pública coincide.

Si apagar el control pasa una respuesta pendiente de Error, defensa solo por
COMBO o dado sin otras decisiones, el servidor ejecuta esa continuación una sola
vez. Esta sí es una transición del juego: incrementa la revisión y transporta
los resultados y animaciones mediante los paquetes habituales. Las selecciones
obligatorias, habilidades, equipos y activaciones que ya empezaron se conservan.

Validado en una base temporal local con dos identidades, recuperación de conexión,
reintentos, privacidad, solicitudes inválidas y un duelo completo. Los bindings
se regeneraron con el CLI 2.14.1.

Publicado el 9 de noviembre de 2026 en `relick-pvp-dev-c2006ee7` de Maincloud con
`--delete-data=never`. El esquema remoto añadió únicamente `set_combo_responses`:
las 18 tablas y las vistas permanecen sin cambios. Se verificaron en el servidor
remoto el cambio de preferencia, su privacidad, la reconexión y los reintentos.
Pasaron las 78 comprobaciones PvP, incluido un duelo completo y el agotamiento
del mazo; las salas de prueba se cerraron al finalizar.
Si un cliente ya mostraba «No such reducer», usa Menú → Reconectar para reenviar
la solicitud pendiente al módulo actualizado.

## Resolución e historial público

El módulo prepara una vista pública de la activación original y sus respuestas,
además de las últimas veinte jugadas aceptadas con identificadores persistentes.
Las entradas describen intención y resultados públicos, sin reutilizar el log
interno ni revelar identidades de robos, descartes privados o búsquedas de mazo.
El registro vive en el checkpoint y se entrega igual a ambos participantes;
no necesita tablas, reducers ni bindings nuevos. Las preferencias COMBO sin
continuación y los comandos rechazados no generan entradas.

Las partidas anteriores conservan su estado y empiezan el historial con su
siguiente jugada aceptada después de actualizar el módulo. No se reconstruyen
jugadas pasadas a partir de los registros privados. El cliente conserva la
presentación anterior al conectar contra un módulo todavía sin estos campos.

Validación local del 9 de noviembre de 2026: 412 comprobaciones del motor,
85 comprobaciones PvP con dos clientes y duelos completos, reconexión,
reintentos y límite de veinte entradas; matriz completa de interfaz sin ventana
y revisión específica del historial en cinco tamaños desde ambos asientos.
Compilan el cliente y el módulo WASM.

Publicado el 9 de noviembre de 2026 en `relick-pvp-dev-c2006ee7` de Maincloud,
con autorización explícita y `--delete-data=never`. La actualización no cambió
tablas ni borró los datos existentes.
Pasaron también las 85 comprobaciones PvP en Maincloud, incluido el historial
compartido, los reintentos, la reconexión, el límite de veinte entradas, un duelo
completo y el agotamiento del mazo. Las salas de prueba se cerraron al finalizar.

## Causa pública y cierre del duelo

`EndReason` es un campo opcional del tablero público y del checkpoint del motor.
Se escribe en el punto de finalización con una causa pública controlada, sin
leer ni publicar el registro interno. Persiste al reconectar y no modifica las
reglas ni las tablas del servidor. Los resultados anteriores sin ese dato
muestran una confirmación genérica del servidor.

El cliente conserva el resultado autorizado antes de procesar el borrado de la
sala, incluso si ambos eventos llegan en el mismo procesamiento. Cada jugador
confirma por separado su regreso al lobby. La conservación es local a esa
sesión y se limpia al regresar, cambiar de identidad o iniciar otra sala.

La validación local pasó 415 comprobaciones del motor, 91 de red y dos procesos
Godot independientes con entrada real en 844×390, incluidos resultado, cierre
desde un jugador y retorno de ambos al lobby. El módulo WASM compila y está
publicado únicamente en una base local aislada de pruebas. La actualización de
Maincloud para enviar la causa del resultado sigue pendiente de publicación.

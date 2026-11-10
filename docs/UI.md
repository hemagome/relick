# Interfaz PvP y pantallas horizontales

El juego inicia directamente en `OnlineLobby`, desde `scenes/Main.tscn`. No se construye un motor local, un rival automático ni un tablero oculto al abrir las salas. El controlador de práctica y sus pruebas de interfaz fueron retirados; las simulaciones de reglas restantes solo se compilan en las pruebas .NET.

## Identidad y futuros playmats

La cabecera muestra el logotipo original de Relick. Marcos con esquinas recortadas, símbolos sólidos y circuitos de bajo contraste conectan las cartas y los controles con la estética del manga y sus referencias a videojuegos. Los paneles de ambos jugadores comparten una superficie carbón/plata; la identidad del propietario permanece en su cabecera.

`RelickVisuals` centraliza marcos, controles, logotipo y señales de interacción. `RelickPlaymat` define fondo, superficies, borde y circuitos, con una textura opcional para el tapete. Es una base para temas futuros, todavía sin selector, persistencia ni cambio en caliente. El arte de un tapete se dibuja bajo las superficies legibles de la interfaz.

Los significados de `RelickVisuals.State` son independientes del tapete: selección dorada con confirmación, objetivo cian con escuadras y foco de teclado blanco. La prioridad es selección > objetivo > hover; el foco añade un borde interior. Vida baja conserva su señal de peligro. Las zonas vacías mantienen su tamaño y posición con contornos tenues y costes discretos. Las fases futuras quedan atenuadas, las completadas llevan confirmación y la actual se indica con `»`.

## Salas y preparación

La entrada agrupa el nombre del jugador y dos tarjetas: crear una sala o unirse con código. Usa el fondo de arena, superficies oscuras, acentos verde/azul y foco visible. En pantallas cortas se reducen márgenes y descripciones. Enter en el código permite unirse cuando hay conexión.

El estado normal no muestra «Conectado». Al perder la conexión aparece un cuadro
central grande «Sin conexión», sobre la sala o el tablero, con el botón Reconectar.
Permanece visible durante el reintento y se retira cuando la suscripción de la sala
vuelve a estar lista. Una lectura autenticada por el mismo socket cada tres segundos
detecta cortes silenciosos: si no responde en siete segundos, se bloquean las
operaciones y se muestra el aviso. El intento inicial vence tras quince segundos.
Los datos de sala y los comandos pendientes se conservan para la recuperación.

Cuando entra el segundo jugador, la misma arena representa elección de habilidad, reparto, mulligan y combate. La selección inicial muestra habilidades presentes en el propio mazo y recibe después otras cuatro cartas. La elegida puede devolverse en el mulligan.

El mulligan usa identificadores de copias: dos cartas iguales pueden seleccionarse por separado. Se bloquea durante el envío y después de confirmar. Una reconexión recupera la elección confirmada sin repetir el reparto. Se representan devoluciones propias, barajado visual, reemplazos, Bonus y robo de iniciativa. No se inventan identidades ni cantidades del mulligan rival que el servidor no haya autorizado.

Después de colocar los Bonus, la iniciativa muestra dos dados 3D que ruedan a la vez, identificados con los nombres de los jugadores y «TÚ»/«RIVAL». Aterrizan con los resultados autorizados por el servidor; entonces se muestran los valores y quién empieza, antes del robo de su primer turno. Las jugadas esperan a que termine la presentación. Reconectar recupera el combate sin repetir los dados.

## Presentación

El lobby confirma «Copiado ✓» durante dos segundos solo si el portapapeles
contiene el código mostrado. Crear, entrar y cerrar bloquean solicitudes
repetidas mientras esperan respuesta; la reconexión recupera la pertenencia
real a la sala antes de volver a habilitar esas acciones. Los nombres y el
formato del código se validan antes del envío, y el servidor conserva la
validación definitiva. Las salas inexistentes o completas incluyen un siguiente
paso concreto. Los errores nuevos se desplazan a la vista una sola vez en
pantallas pequeñas; los campos conservan el texto para poder corregirlo.

El duelo terminado presenta victoria, derrota o empate, causa pública y vidas/PR
finales de ambos jugadores. Espera al dado, los movimientos de cartas y el último
impacto antes de reemplazar el inspector por el resultado. «Revisar tablero»
recupera la lectura de cartas; Menú → Ver resultado permite volver. El historial
sigue disponible. «Volver al lobby» cierra la sala, muestra la espera de
confirmación y permite a cada jugador terminar de leer su resultado aunque el
rival ya la haya cerrado. El resultado autorizado se conserva en memoria hasta
ese retorno; si no hay conexión se ofrece Reconectar. No se presenta una causa
inventada para resultados antiguos sin ese dato.

`ArenaPresentation`, `ArenaMotion` y los controles de cartas, personajes, pilas y dado componen la arena PvP. `OnlineArena` solo consume las vistas autorizadas y envía una opción ligada a sala y revisión; no ejecuta ni predice las reglas.

- Vida, PR, fase, dueño del turno y cantidades de zonas permanecen consultables.
- Mano rival con reversos y únicamente las revelaciones autorizadas.
- Cartas, habilidades, Memoria, Corruptas, equipo, cartas sobre el Relikario, Bonus y canje tienen sus consultas.
- Las respuestas con descartes permiten selección múltiple y exigen la cantidad recibida del servidor.
- El dado muestra el resultado confirmado; su animación no consume azar del duelo.

## Distribución horizontal

La barra contextual muestra el nombre de la carta elegida, el coste de su acción
principal y verbos breves como Jugar, Equipar, Canjear o Atacar. Los índices de
carta y espacio del protocolo no se usan como etiquetas de interfaz. El inspector
muestra la carta seleccionada sin un botón adicional de lectura; Memoria y las
pilas conservan sus consultas. La selección se puede limpiar sin ejecutar una
jugada. El avance de etapa ocupa un control separado, Relikia tiene su propio
menú y Abandonar está en el menú general con su confirmación habitual.

La lectura de cartas usa un inspector compartido en mano, elección inicial y
consultas de zonas. Conserva las cartas completas y sus tamaños de presentación.
La clasificación incluye el subtipo actual —por ejemplo, Código Personaje— y
COMBO cuando corresponde. Las habilidades muestran daño normal, daño crítico y
premio en un orden fijo; su descripción omite el prefijo que ya repite esos datos.
Los códigos y equipos muestran su texto sin convertir el mismo efecto en otro
indicador. No se añaden las etiquetas «Efecto», «Seleccionada» ni «Efecto visible».

El coste ocupa una sola fila, separado de los botones, y prioriza la opción
autorizada de la acción actual. Al consultar una carta sin una acción disponible
se puede mostrar su coste de catálogo; eso no habilita jugadas. Los contadores y
el daño de una habilidad enseñada corresponden a su copia autorizada. Limpiar la
selección retira esos datos. Sin carta seleccionada el inspector se contrae a la
instrucción actual; no reserva una zona de lectura vacía. Al consultar una carta,
clasificación, nombre, atributos y contadores permanecen en la cabecera. Solo la
descripción se desplaza: instrucción, coste y botones quedan fuera de esa lectura.
En escritorio, la lectura recibe parte del espacio libre al seleccionar una carta.
Elegir la habilidad inicial actualiza el inspector existente sin reconstruir la arena.

Al elegir un Ataque se iluminan las habilidades autorizadas; tocar una escoge la
copia real para la confirmación. Una sola habilidad disponible se selecciona sin
anunciar el ataque. Jessica se selecciona en su Bonus o canje, y las destrucciones
y el crítico de Magnet eligen objetivos sobre sus zonas del tablero. La mano al
azar se identifica como zona, aunque se haya tocado una de sus cartas. Las pilas
con varias cartas conservan su selector e inspección.

Los modificadores del dado se agrupan en Modificar. Los descartes y selecciones
obligatorias mantienen sus cantidades y confirmaciones; Oro muestra una sola
confirmación para la cantidad elegida. La recuperación, corrupción y activación
desde memoria presentan una galería con el origen de cada copia. Solo se muestran
identidades que ya estén autorizadas para el asiento.

La presentación conserva el índice de opción original y la revisión del tablero
para cada botón o entrada de menú. Los rótulos breves son locales al cliente y no
modifican costes, efectos, mensajes del servidor ni el protocolo de red. Se bloquean
los envíos durante animaciones, desconexión o una jugada pendiente, igual que antes.

PC, tablet y teléfono comparten alcance horizontal. El proyecto permite girar entre ambos lados del teléfono. Una ventana de escritorio alta conserva un lienzo horizontal con bandas.

Con menos de 1100 unidades lógicas de ancho, el campo admite desplazamiento vertical y los controles secundarios pasan al menú. En pantallas cortas —altura menor de 600— se muestran tablero, inspector y galería de cartas juntos. La mano usa desplazamiento vertical propio, con una columna en los anchos menores y dos desde 820 unidades; conserva el arte completo y la escala anterior de cada carta. El tablero admite desplazamiento también en ventanas anchas y cortas. En pantallas altas la mano mantiene su fila horizontal. Los objetivos táctiles principales miden al menos 48 unidades lógicas; los diálogos se ajustan al área disponible y admiten desplazamiento.

La configuración contempla densidad y área segura. No constituye una exportación Android/iOS ni una validación en hardware móvil; todavía deben comprobarse teclado virtual, rendimiento y consumo en dispositivos físicos.

## Guía de decisiones y avance de fase

La cabecera mantiene un contador «ACCIONES» con valores separados para TÚ y
RIVAL. Permanece fuera del desplazamiento del tablero en todos los tamaños,
también durante respuestas y resoluciones. Lee las acciones restantes de cada
jugador del snapshot autorizado; gastar, recuperar costes por una anulación o
comenzar un turno actualiza esos valores. No descuenta acciones al seleccionar
cartas ni durante el envío. El cero sigue visible con tratamiento atenuado y el
contador se omite durante la preparación. Los costes de cartas conservan su
campo único; el título contextual no repite el presupuesto disponible.

El inspector prioriza una instrucción concreta en descartes, objetivos, respuestas y dados. Al tocar una carta sin jugadas disponibles explica el bloqueo usando la fase, los recursos visibles y las opciones autorizadas del propio jugador. La espera solo anuncia un COMBO si el servidor ofrece uno. Enviar una decisión, perder conexión y resolver una animación tienen mensajes específicos; una instrucción nueva vuelve al inicio de la lectura. Los costes permanecen en su campo único.

`ArenaGuidance.SuggestAdvance` consulta todas las opciones del snapshot, independientemente de la carta seleccionada. Solo sugiere avanzar al jugador de turno que debe decidir, en robo, combate, acciones o final, si existe `Advance` y las únicas otras opciones son abandonar. Un COMBO legal, evolución, aprendizaje, Bonus/canje o Relikia impiden la sugerencia. Un COMBO sin sus condiciones cumplidas no la impide.

`AdvancePhaseButton` invita con un pulso dorado de 1,8 segundos, sin mover ni escalar el objetivo táctil. Se detiene al enviar, desconectar, ocultar el control, entrar en una resolución o recuperar una jugada disponible; fuera de la invitación no procesa fotogramas. El botón conserva el destino y el índice del servidor, incluyendo fases repetidas. Avanzar siempre requiere pulsarlo.

Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --guidance-only`. Con `--ui-screenshot-dir=/tmp/relick-guidance` y renderizado habilitado genera capturas de la invitación en cuatro resoluciones.

## Actualizaciones y animaciones

- La selección de habilidad inicial actualiza las cartas y el botón existentes: no reconstruye la arena.
- Las referencias a cartas, habilidades propias y botón de evolución se reúnen al construir el tablero, evitando búsquedas del árbol en cada selección o cambio de habilitación.
- La mano de preparación se ordena y materializa solo al cambiar las filas de la suscripción. Una revisión local evita construir claves de preparación cada fotograma cuando los datos, la conexión y los envíos pendientes no cambian.
- Reconectar invalida la caché por época y vuelve a comprobar las versiones coherentes de tablero, mano y opciones. Retirar la suscripción de preparación invalida su mano mediante las eliminaciones de filas.
- Durante el reciclaje, la fila de cartas muestra la memoria propia y permite elegir hasta seis cartas con un contador de selección. El rival conserva su propia mano; la elección pendiente se recupera al reconectar.
- Código Konamy muestra en esa fila solo las no-habilidades del mazo propio. Seleccionar una ilustración ofrece la opción autorizada correspondiente; la mano del adversario y su vista del mazo permanecen ocultas.
- Cambio de Mentes reconstruye ambas manos desde el estado autorizado. Anima un paquete boca abajo por cada mano no vacía, sin identidades en las señales y con duración independiente de la cantidad de cartas. Conserva las revelaciones de cada copia y comprueba los destinos de ambos asientos, también con manos vacías o desiguales.
- Las señales visuales representan una revisión aceptada; tiradas consecutivas iguales siguen siendo eventos distintos. Una recuperación o salto de revisión restaura el estado sin reproducir efectos antiguos.
- Los robos pendientes mantienen ocultas sus cartas de destino desde que se recibe la revisión. Cada carta aparece en la mano al terminar su propio vuelo; las siguientes siguen esperando, también con Triple Ataque Cruzado. Si una zona queda fuera de pantalla se omite su vuelo y se muestra su destino. Interrumpir las animaciones restaura las cartas, y reconectar presenta el estado recuperado sin repetir robos.
- Las cartas no mantienen procesamiento de hover cuando ya no necesitan animarlo. Los personajes ocultos no avanzan; los visibles solicitan redibujado cuando cambia su fotograma. El dado deja de actualizar continuamente su viewport 3D cuando está inmóvil.
- A cero vidas, Yuno usa un reposo agotado con respiración lenta, gesto preocupado y raspones. Se termina la reacción del último golpe antes de entrar al bucle. La vida recibida selecciona el reposo para ambos jugadores, también al recuperar la partida; volver a construir la arena no reinicia ese bucle.
- Cuando el motor aplica una curación efectiva, emite `Heal` con el destinatario y las vidas recuperadas. La arena reproduce una acción de 1,2 segundos con gesto de alivio y corazones verdes de píxel después del dado y de los movimientos de cartas pendientes; luego vuelve al reposo. La señal no contiene identidades de cartas y se descarta al recuperar el estado, igual que los demás efectos visuales.
- Los impactos distinguen el resultado final del ataque mediante `Hit.AttackDie`: 4 conserva la reacción reducida anterior, 5 usa un retroceso normal y 6 un tambaleo crítico con recuperación más lenta. La cantidad de daño y los dados secundarios no seleccionan el clip. Los tres impactos terminan en el reposo agotado si la vida actual es cero; no se interrumpen al recibir ese cambio de estado.
- Los iconos usan siluetas angulares y los marcos dibujados no crean recursos de estilo en cada redibujado. No se introdujo una caché global ilimitada de estilos o colores animados.

Los siete atlas de Yuno conservan un lienzo lógico de 608×504 por cuadro. La importación limita el ancho a 2432 píxeles y mantiene la cuadrícula: reposo, ataque, daño reducido, normal y crítico, reposo agotado y curación usan el mismo tamaño visual. Los clips, su procedencia y sus prompts están documentados en [las animaciones de Yuno](../assets/characters/yuno/README.md).

## Resolución e historial público

La resolución central distingue respuesta, resolución, resultado y activación
anulada. Identifica la carta original y a su jugador; las respuestas encadenadas
aparecen debajo, sin atribuir la activación original a quien contraataca. Presenta
el resultado cuando terminan el dado y los movimientos de cartas.

«Historial» abre las últimas veinte jugadas públicas, de la más reciente a la más
antigua. También está disponible en Menú para pantallas pequeñas. Cada entrada
identifica turno, jugada y resultados públicos: daño/curación y destinatario,
vidas, PR, robos por cantidad, cambios de zona públicos y anulaciones. No enumera
cartas robadas, descartes privados ni elecciones de mano o mazo. Consultarlo y
cerrarlo conserva la selección y no envía jugadas. Permanece bloqueado durante
la animación para no adelantar sus resultados.

El servidor asigna identificadores y conserva el registro acotado en el estado
recuperable; el cliente lo presenta sin acumular entradas propias. Reconectar,
reintentar una jugada o reconstruir la interfaz no duplica el historial. Los
cambios privados de preferencia COMBO solo generan una entrada si efectivamente
reanudan una resolución pendiente. Los duelos anteriores a esta actualización
empiezan su registro público con la siguiente jugada aceptada; no se reconstruye
desde el registro interno del motor, que contiene información privada.

## Validación

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
dotnet build Relick.csproj
./jugar.sh --headless -- --pvp-ui-test
```

Para repetir solo la matriz de búsqueda de Código Konamy, añade `--deck-search-only` a los argumentos de la prueba; para Cambio de Mentes, usa `--mind-swap-only`. Se puede combinar con `--ui-screenshot-dir=/tmp/relick-ui` para guardar las capturas de PC y móvil horizontal.

`--history-only` verifica activación, Error y contraerror, resultados, privacidad,
apertura/cierre mediante entrada real, recuperación sin duplicados y bloqueo
durante el dado en cinco tamaños, desde ambos asientos.

`--triple-attack-only` reproduce los tres robos de Triple Ataque Cruzado y
comprueba su aparición individual, la carta conservada, la privacidad rival y
la recuperación/interrupción en cuatro tamaños y ambos jugadores/asientos.

`--actions-only` comprueba gasto, respuesta rival, devolución por Error, cero,
reinicio de ambos presupuestos, selección sin gasto, recuperación y cabecera
visible al desplazar el campo en cinco tamaños y ambos jugadores/asientos.

`--closure-only` comprueba victoria/derrota/empate, causa persistida y heredada,
espera del dado e impacto final, revisión del tablero y reapertura del resultado
en cinco tamaños y ambos asientos. También verifica conexión, bloqueo de
solicitudes, errores visibles y copia del código sin falsos positivos en el
lobby; una ejecución gráfica restaura el portapapeles al terminar. La prueba de
dos procesos PvP exige que ambos vuelvan al lobby y conserva el resultado del
jugador cuyo rival cierra primero.

`--preparation-only` comprueba elección inicial, mulligan y ambos dados de iniciativa en los cuatro tamaños y desde ambos asientos, incluido el resultado, la espera del robo inicial y la recuperación sin repetir la animación.

La matriz recorre 1440×1024, 1024×768, 844×390 y 700×390 desde ambos asientos. La lectura del inspector añade 1280×480: verifica el modo vacío compacto y desplaza efectos largos mediante eventos de rueda, comprobando que cabecera, atributos, coste, botones y mano permanecen fijos. `--inspector-only` ejecuta esa revisión enfocada. Comprueba límites, privacidad, estabilidad de selección inicial, bloqueo tras confirmar, recuperación y terminación de animaciones. Incluye las seis zonas de Bomba de Tiempo y la selección de seis cartas de memoria para reciclar, con una memoria mayor que la mano. También comprueba los tres impactos, su transición al reposo agotado a cero vidas y las curaciones para ambos jugadores/asientos: orden tras el dado, repetición, reconstrucción y recuperación visual sin reproducir efectos pasados. `--shared-arena-test` se mantiene como alias de esta prueba PvP; ya no ejecuta práctica.

Para capturas:

```bash
./jugar.sh -- --pvp-ui-test --ui-screenshot-dir=/tmp/relick-ui
./jugar.sh -- --lobby-preview-dir=/tmp/relick-lobby
```

La vista previa de salas no se conecta a ningún servidor. Las pruebas de preparación, combate y dos procesos con entrada real están descritas en [PvP](PVP.md).

## Respuestas COMBO

El encabezado del duelo incluye un control táctil de dos estados, disponible
para ambos jugadores durante su propio turno o el del rival:

- **COMBO: preguntar** permite las respuestas opcionales de cartas COMBO.
- **COMBO: pasar** omite esas respuestas y conserva las cartas, incluidos los
  contraerrores. No consume acciones ni PR. Los COMBO pueden seguir jugándose
  como jugadas voluntarias durante las etapas normales del turno propio.

Pasar evita las pausas causadas exclusivamente por Error de Carga, Esquivar,
Estrella del Fontanero y modificadores de dado COMBO. Si ya existe una ventana
pendiente, apagar el control la pasa cuando no quedan decisiones de habilidades
o equipos. No omite objetivos, descartes obligatorios, efectos ya iniciados,
Destreza, Yuno, Destiny, Caja de Cartón ni protecciones de equipos. Volver a
preguntar habilita oportunidades futuras; no reabre una activación ya resuelta.

La preferencia pertenece al jugador en esa partida y persiste al reconectar.
Viaja únicamente en el paquete privado: cambiarla sin avanzar el juego conserva
la revisión, las opciones y las animaciones del rival. Las confirmaciones usan
comandos idempotentes; reintentar uno anterior no revierte una elección posterior.
No se añade una espera artificial uniforme: la preferencia es privada, pero las
pausas de una respuesta manual todavía pueden revelar información por su duración.

La matriz `--pvp-ui-test --combo-responses-only` comprueba ambos estados, cuatro
tamaños y ambos jugadores/asientos. La prueba PvP local también verifica
privacidad, reconexión, reintentos y rechazo de solicitudes ajenas o inválidas.

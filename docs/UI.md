# Interfaz PvP y pantallas horizontales

El juego inicia directamente en `OnlineLobby`, desde `scenes/Main.tscn`. No se construye un motor local, un rival automático ni un tablero oculto al abrir las salas. El controlador de práctica y sus pruebas de interfaz fueron retirados; las simulaciones de reglas restantes solo se compilan en las pruebas .NET.

## Identidad y futuros playmats

La cabecera muestra el logotipo original de Relick. Marcos con esquinas recortadas, símbolos sólidos y circuitos de bajo contraste conectan las cartas y los controles con la estética del manga y sus referencias a videojuegos. Cada campo se tiñe con el acento de su jugador y se ilumina durante su turno; la identidad del propietario permanece en su cabecera.

`RelickVisuals` centraliza marcos, controles, logotipo y señales de interacción. `RelickPlaymat` define fondo, superficies, borde y circuitos, con una textura opcional para el tapete. Es una base para temas futuros, todavía sin selector, persistencia ni cambio en caliente. El arte de un tapete se dibuja bajo las superficies legibles de la interfaz.

Los significados de `RelickVisuals.State` son independientes del tapete: selección dorada con confirmación, objetivo cian con escuadras y foco de teclado blanco. La prioridad es selección > objetivo > hover; el foco añade un borde interior. Vida baja conserva su señal de peligro. Las zonas vacías mantienen su tamaño y posición con contornos tenues y costes discretos. Las fases futuras quedan atenuadas, las completadas llevan confirmación y la actual se destaca con tamaño y color; la celda de estado dice con texto de quién es el turno.

## Sistema visual de los menús

Las pantallas fuera del duelo (sala, inicio de sesión, mazos, editor, selector de
Relikario, vestidor, perfil, ajustes, competencias y sus diálogos) toman sus colores, letras y esquinas de
`MenuVisuals`. No escribas colores ni tamaños propios en ellas.

| Elemento | Regla |
|---|---|
| Superficies | `MenuVisuals.Tone`, derivadas del tapete Clásico. El tapete del jugador solo viste la arena. |
| Menta | La acción principal: una por pantalla, con `RelickVisuals.Primary`. El resto son botones normales. |
| Oro | Selección: pestaña activa, mazo en uso, opción elegida. Nunca una acción. |
| Cian | Información y avisos que no son errores. |
| Coral | Error, rival y confirmaciones que destruyen algo. |
| Violeta | Solo significados del duelo y cosméticos elegidos por el jugador; nunca superficies. |
| Letra | `MenuVisuals.Type`: 12, 14, 16, 20, 28 y 40. |
| Esquinas | `MenuVisuals.Cut`: 4 en insignias, 8 en controles y 12 en paneles. |
| Volver | `MenuVisuals.Back`: «‹ Volver», con el destino en el tooltip. |
| Diálogos | `MenuDialog.Fill` y `MenuDialog.Show`: miden lo que su contenido y solo desplazan si no caben. |
| Táctil | Los controles del editor miden al menos 48 unidades cuando `Touch` está activo (móvil). |

`MenuShell` aloja las pantallas: Jugar, Mazos, Personalizar, Perfil y Ajustes. La
navegación es una barra superior o, con menos de 600 de alto, un riel a la izquierda.
El editor y el selector de Relikario la ocultan y vuelven con «‹ Volver». Durante una
búsqueda o una sala solo quedan Jugar y Ajustes.

`ArenaViewport.FitDialog` queda para los lectores del duelo (Memoria, Historial), que
aprovechan toda la pantalla. `RelickVisuals.Primary` fija el color del texto en todos
los estados, incluido el foco, también en el duelo.

`--pvp-ui-test --style-only` comprueba estas reglas en cinco tamaños, con ratón y en
táctil. Forma parte de `--lobby-only` y de la matriz completa.

## Tablero de escritorio

Con un lienzo de al menos 1400×980 unidades lógicas, `OnlineArena` construye el tablero completo; los tamaños menores conservan las distribuciones apiladas descritas más abajo, con el mismo acabado visual.

- **Lienzo único.** `BoardCanvas` coloca todas las zonas en un espacio de diseño de 1440×992 que se escala de forma uniforme al espacio que deja el riel: en 16:9 crece un 3 % y en 16:10 lo limita el ancho. Todas las cartas ocupan 138×193; las zonas apaisadas (habilidades y canje) usan esa misma carta girada. Con una ventana más estrecha que 16:9 el lienzo se reduce entero y el margen sobrante se reparte por encima y por debajo de los campos.
- **Campos enfrentados.** `ArenaPresentation.Field` construye el campo propio en la mitad inferior y gira el rival 180°. Desde el borde exterior se ven los dos espacios de canje apaisados, el Relikario (retrato, nombre, vidas, cristales de acción, Destreza y PR) sobre su equipo y las cartas sobre el Relikario, y las cuatro habilidades en un bloque 2×2 (500 y 1000 PR arriba, 0 y 300 PR abajo). La Memoria ocupa una pila por jugador, con el mazo en la esquina. El espacio junto al mazo queda libre para un Relikario que juegue con dos mazos. Un espacio de habilidad vacío solo muestra su coste, grande y centrado.
- **Pilas en la vista de mesa.** `RaisedStack` dibuja el mazo, la Memoria, los equipos, las cartas sobre el Relikario y las cargas del Escudo como bloques: la base queda fija, la tapa sube 0,3 mm por carta (con los 63 mm del ancho de una carta como regla) y solo se pintan los cantos que ve la cámara equivalente de `BoardCanvas`, con el color de la mica en el mazo y la Memoria. La tapa elevada es la base escalada desde el punto de la mesa bajo el ojo, así que sigue a la proyección si cambia la inclinación. Los vuelos usan la altura final de la pila. La tapa se desplaza hacia fuera al subir: por eso el mazo deja 18 unidades hasta el borde, que es el límite de la textura de la mesa. Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --table-view-only`.
- **Bonus y Área.** Cada jugador tiene un marco con dos espacios, Bonus y Área, montado sobre la línea central y junto a sus canjes. El marco y los espacios se delinean en verde para el jugador y en rojo para el rival.
- **Núcleo de turno.** `TurnCore` (236×112) ocupa el hueco entre las habilidades de ambos jugadores, sobre la línea central. Tiene de quién es la decisión y su reloj, la fase con el número de turno, una pista de cuatro tramos y un único botón contextual (avanzar o «Dejar resolver»). Toma el color de quien tiene el turno y nunca cambia de tamaño. Conserva los nombres `CombatCenter`, `StageIndicator` y `TurnDock`. Los equipos ligeros van bajo la Memoria de su dueño.
- **Manos.** La mano propia cuelga del borde inferior con las cartas al tamaño del tablero: en reposo solo el cuadro de efecto queda bajo la pantalla. La carta bajo el cursor, con foco o seleccionada sube hasta verse completa y sus acciones la acompañan; el área de pulsación no se mueve. Un borde rojo identifica las cartas del rival que tienes en mano. La mano rival cuelga del borde superior y solo asoma su franja inferior.
- **Elecciones.** Elegir entre cartas que no están en la mano —Disquete, reciclaje, búsqueda en el mazo, Resetear la Mente, corrupción, código de Relikia y habilidad inicial— abre un panel sobre el tablero atenuado. Las memorias se apilan en bloques con el color de su dueño y la decisión que confirma la elección va al pie del panel.
- **Riel adaptable.** A la derecha, una franja de 72 unidades con el menú, COMBO, Historial y las jugadas públicas en miniatura. El lector de cartas se fija, flota o entra en la galería según el espacio (ver [Riel adaptable](#riel-adaptable)).
- **Relikia.** El contador de PR del jugador abre un menú que solo nombra los cuatro efectos. El efecto bajo el cursor se explica en el lector con su lámina (`assets/relikia/`) y su coste.
- **Lienzo lógico.** En escritorio el alto lógico es 1024 y el ancho sigue la proporción de la ventana (entre 1440 y 2280), de modo que un monitor panorámico no muestra bandas laterales.

Entre el tablero de escritorio y las pantallas compactas (ancho de 1100 o más y menos de 980 de alto, como 1366×768 o un tablet), los dos campos van dentro de `ArenaScroll`. Si caben, ocupan su alto completo. En caso contrario, el desplazamiento ocupa el espacio que dejan la mano y el inspector. Las galerías centradas, como habilidad inicial, Disquete y reciclaje, conservan su confirmación visible al desplazarse. `--choice-preview-dir=DIRECTORIO` (con `--choice-sizes=1366x768,…` opcional) informa y captura esos botones en cada tamaño.

`./jugar.sh -- --pvp-ui-test --board-preview-dir=DIRECTORIO` guarda capturas del tablero en reposo, con una carta seleccionada, con el menú de Relikia y con Disquete, a 1820×1024 y 1440×1024.

### Riel adaptable

El riel solo ocupa lo que no encoge las cartas. `ChooseRailMode` mide el ancho que sobra junto al tablero a escala completa:

- **Con 320 unidades o más (16:9 y más ancho)** el lector queda fijo junto a la franja, hasta 420 en total. Sin carta que leer muestra «Última jugada».
- **Con menos (16:10, 3:2, 4:3)** solo queda la franja y el tablero crece (un 10–14 % en 16:10). El lector flota sobre la columna lateral de Bonus, Área y canje, sin tapar habilidades, Relikarios, Memorias, el núcleo ni la mano.
- **En una galería con riel estrecho** el lector es una columna del propio panel.

Reglas del lector:

- **Hover.** Aparece tras 180 ms en pantallas estrechas, no recibe clics y se aparta si el cursor entra en él. Al salir de la carta se cierra (250 ms flotando, 700 ms fijo, para poder ir a leerlo), así que el último hover ya no deja oculto lo que había debajo.
- **Fijado.** Una selección, una jugada pulsada en la franja o la carta que debes responder quedan abiertas. La carta a responder se abre sola.
- **Lado.** Prefiere la derecha. Pasa a la izquierda si ahí está la carta que se lee, el historial o algo que pulsar. Con objetivos en ambos lados se oculta.
- **Texto.** El arte cede alto antes que el texto. Con las 67 cartas actuales solo Reverse Shuffle necesita desplazamiento, en 4:3 y en el lector fijo más estrecho; al seleccionarla se puede desplazar.
- **Motivo.** Por qué una carta no se puede jugar se lee en el lector; el aviso central no lo repite porque taparía el botón de turno.

Franja e historial:

- **Jugada.** Cada ficha (`PlayTile`) muestra la carta, el color y lado de quien jugó y una cifra: vidas, PR, robo, acciones o dado, en ese orden. Su tooltip la cuenta completa. Los avances de fase sin efecto solo aparecen en el historial.
- **Historial.** El botón abre las últimas veinte jugadas en el lector fijo o en un cajón junto a la franja. Se cierra con el mismo botón, Esc o un clic en el campo vacío. El menú conserva el diálogo completo.
- **Sin adelantos.** Mientras se anima la última jugada, la franja, el historial y «Última jugada» no muestran su resultado. El cuadro de resolución solo aparece mientras hay algo resolviéndose.

Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --rail-only`, también en la matriz completa. Con `--ui-screenshot-dir` y renderizado guarda los tres modos.

### Acabado y efectos

`RelickVisuals` genera en memoria los marcos de esquinas recortadas con degradado, borde iluminado y halo (`Glass`, `Surface`, `Field`); no dependen de archivos importados. Los títulos usan una variante inclinada y pesada de la fuente del proyecto (`RelickVisuals.Display`). El tapete es un sombreador (`scripts/arena_playmat.gdshader`): dos mitades teñidas con el color de cada jugador, trama de puntos, líneas de velocidad, chevrones y un divisor luminoso cuyos pulsos toman el color de quien tiene el turno. Las zonas vacías son huecos con escuadras; las ocupadas proyectan sombra.

`ImpactLayer` añade señales cosméticas a partir de los mismos eventos autorizados que mueven los retratos: número de daño o curación sobre el personaje, destello de pantalla al recibir daño, un trazo del atacante al defensor y un aro donde aterriza cada carta. No bloquea jugadas, no forma parte de `IsAnimating` y se limpia al recuperar el estado. Los objetivos válidos laten en cian; la selección mantiene un halo dorado fijo. Con movimiento reducido se omiten trazos, aros y destellos, y los números aparecen sin desplazarse.

## Salas y preparación

Jugar muestra el Relikario del mazo en uso (o el que el jugador eligió para su inicio),
el mazo con «Cambiar», y los cuatro modos: Partida rápida, Sala privada, Clasificatoria
ELO y Torneo. El nombre se edita en Perfil. Partida rápida ofrece Buscar rival y, durante
la espera, un contador y Cancelar búsqueda, con tu tarjeta frente a una silueta en lugar
del Relikario. Sala privada ofrece Crear sala y «Tengo un código», que pide el código en
un diálogo. Buscar bloquea la navegación y las demás partidas hasta cancelar o encontrar
rival. La sala en espera muestra el código y los dos asientos. El fondo propio
del lobby combina un degradado gris azulado, tramas de manga, diagonales y los
chevrones del logotipo en cian/rojo de bajo contraste. La decoración se concentra
en los bordes; las superficies de lectura conservan su contraste. Es estático,
se adapta al tamaño y deja pasar los clics a los controles. Los acentos de las
acciones y el foco visible conservan sus significados. En pantallas cortas se
reducen márgenes y descripciones. Enter en el código permite unirse cuando hay conexión.

Mientras el servidor no tenga una temporada o un torneo, Clasificatoria ELO y Torneo son
espacios inactivos: contorno discontinuo, color atenuado y «Próximamente». No son
botones, no reciben clics ni foco y no consultan al servidor. Cuando existen, los
sustituye una `ModeCard` que abre su pantalla (`RankedPanel`, `TournamentPanel`). En
ventanas de menos de 720 de alto los cuatro modos se ven sin desplazar la sala.

Durante la partida rápida, el centro del panel muestra la miniatura del Relikario
del mazo equipado, la silueta rival y tres flechas con un ciclo suave de dos
segundos. El tiempo de espera y Cancelar búsqueda permanecen visibles; el botón
conserva su posición. La animación solo avanza durante una búsqueda confirmada
y se pausa al cancelar, perder conexión u ocultarse. La preferencia de reducir
animaciones del sistema, cuando está disponible, y el ajuste de proyecto
`relick/accessibility/reduced_motion` habilitan una variante estática.

Una sala confirmada después de solicitar partida rápida muestra «Rival
encontrado», revela un reverso clásico en el lado rival y entra a preparación tras 350 ms. También
se presenta cuando el segundo jugador es emparejado inmediatamente sin pasar por
la cola. Una sala privada o la recuperación de una partida ya abierta no repite
esa confirmación; no se simulan jugadores, porcentajes ni estimaciones de espera.

El estado normal no muestra «Conectado». Al perder la conexión aparece un cuadro
central grande «Sin conexión», sobre la sala o el tablero, con el botón Reconectar.
Es el único punto para reconectar también en preparación y resultado: el menú
del duelo no ofrece esta acción mientras estás conectado. Al aparecer cierra
menús y consultas abiertos para que no tapen el aviso; los clics y la navegación
por teclado permanecen en él, incluso durante el reintento.
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
impacto antes de abrir el resultado en el centro. «Revisar tablero» cierra el
panel y Menú → Ver resultado permite volver. El historial
sigue disponible. «Volver al lobby» cierra la sala, muestra la espera de
confirmación y permite a cada jugador terminar de leer su resultado aunque el
rival ya la haya cerrado. El resultado autorizado se conserva en memoria hasta
ese retorno; si no hay conexión se muestra el mismo aviso central de Reconectar. No se presenta una causa
inventada para resultados antiguos sin ese dato.

`ArenaPresentation`, `ArenaMotion` y los controles de cartas, personajes, pilas y dado componen la arena PvP. `OnlineArena` solo consume las vistas autorizadas y envía una opción ligada a sala y revisión; no ejecuta ni predice las reglas.

- Vida, PR, fase, dueño del turno y cantidades de zonas permanecen consultables.
- Mano rival con reversos y únicamente las revelaciones autorizadas.
- Cartas, habilidades, Memoria, equipo, cartas sobre el Relikario, Bonus y canje tienen sus consultas.
- Las respuestas con descartes permiten selección múltiple y exigen la cantidad recibida del servidor.
- El dado muestra el resultado confirmado; su animación no consume azar del duelo.

## Distribución horizontal

Al pulsar una carta aparecen junto a ella las acciones disponibles, con iconos y
etiquetas breves como Activar, Equipar, Canjear o Atacar. Si hay varias habilidades
para atacar, Atacar permite elegir una de las que se resaltan en el tablero.
Las decisiones de efectos, como elegir un objetivo o conservar un dado, se abren
en el centro. «Ver tablero» recoge el aviso temporalmente y «Volver a la decisión»
recupera sus opciones y la selección. Las galerías para elegir cartas llevan
su confirmación en el pie del panel.
El inspector acrílico muestra el nombre, el efecto y el coste de la carta.
Pasar el cursor sobre una carta visible abre su vista previa.
Un clic en el campo vacío limpia la selección del duelo y cierra la vista previa.
El avance de etapa ocupa un control separado, el contador de PR propio abre
los efectos de Relikia, y junto a la rueda dentada del menú están la bandera
blanca para abandonar y un parlante que abre los controles de sonido. Su confirmación es un cuadro centrado, del tamaño de su
pregunta, con «Cancelar» y «Abandonar». El contador rival es solo informativo.

La lectura de cartas usa un inspector compartido en mano, elección inicial y
consultas de zonas. Conserva las cartas completas y sus tamaños de presentación.
La consulta de Memoria propia o rival muestra una cuadrícula de todas sus copias
y un único inspector fijo a la derecha. Pasar el cursor sobre una carta o
pulsarla cambia el inspector y marca esa copia. La galería y la descripción se desplazan
por separado. Las columnas se ajustan al espacio disponible, con la misma escala
compacta de cartas en pantallas cortas. La cabecera identifica al propietario de
la memoria. La pila muestra el total y cuántas cartas están boca abajo. Ambos
jugadores pueden consultar las cartas corrompidas en «Fondo · Boca abajo» de
cualquiera de las dos memorias. La consulta conserva la posición y orientación
de cada carta en la partida.
La clasificación incluye el subtipo actual —por ejemplo, Código Personaje— y
COMBO cuando corresponde. Las habilidades muestran daño normal, daño crítico y
premio en un orden fijo; su descripción omite el prefijo que ya repite esos datos.
Los códigos y equipos muestran su texto sin convertir el mismo efecto en otro
indicador. No se añaden las etiquetas «Efecto», «Seleccionada» ni «Efecto visible».

El coste ocupa una sola fila, separado de los botones, y prioriza la opción
autorizada de la acción actual. Al consultar una carta sin una acción disponible
se puede mostrar su coste de catálogo; eso no habilita jugadas. Los contadores y
el daño de una habilidad enseñada corresponden a su copia autorizada. Limpiar la
selección retira esos datos. Sin una carta para mostrar, el inspector se contrae y
en escritorio el lector se retira o muestra la última jugada. Al consultar una carta, clasificación,
nombre, atributos y contadores permanecen en la cabecera. Solo se desplaza la
descripción. El coste queda visible debajo.
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

PC, tablet y teléfono comparten alcance horizontal. El proyecto permite girar entre ambos lados del teléfono. Una ventana de escritorio alta conserva un lienzo horizontal con bandas; una ancha amplía el lienzo.

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

El aviso central da la instrucción de descartes, objetivos y dados. Un halo amarillo señala los COMBO disponibles y al seleccionar la carta aparece «Activar» junto a ella, también durante el turno rival y en las respuestas de RAM. En la RAM, «Dejar resolver» está en el núcleo de turno. Si el efecto pide elegir después de activarse, el aviso o la galería muestra esa decisión.

`ArenaGuidance.SuggestAdvance` consulta todas las opciones del snapshot, independientemente de la carta seleccionada. Solo sugiere avanzar al jugador de turno que debe decidir, en robo, combate, acciones o final, si existe `Advance` y las únicas otras opciones son abandonar. Un COMBO legal, evolución, aprendizaje, Bonus/canje o Relikia impiden la sugerencia. Un COMBO sin sus condiciones cumplidas no la impide.

`AdvancePhaseButton` invita con el mismo brillo amarillo fijo de las cartas activables (`RelickVisuals.Playable`) cuando avanzar es la única jugada autorizada en tu turno, sin mover ni escalar el objetivo táctil. Se apaga al enviar, desconectar, ocultar el control, entrar en una resolución o recuperar una jugada disponible. El botón conserva el destino y el índice del servidor, incluyendo fases repetidas. Avanzar siempre requiere pulsarlo.

Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --guidance-only`. Con `--ui-screenshot-dir=/tmp/relick-guidance` y renderizado habilitado genera capturas de la invitación en cuatro resoluciones.

### Doble clic y atajo

Cuando el botón de turno cambia de significado (otra fase, una fase repetida o cada nueva ventana de respuesta) espera 400 ms (`TurnGuardMilliseconds`) antes de aceptar una pulsación: aparece en el mismo lugar que el anterior y un doble clic terminaría el turno. Una jugada dentro de la misma fase no reinicia la espera.

Espacio pulsa ese botón en todas las distribuciones. No repite al mantenerlo, respeta la espera y no actúa detrás de galerías, avisos que cubren el botón, diálogos o perfiles. Un tooltip no cuenta como diálogo. Si el foco se puso con el teclado, Espacio sigue pulsando el control enfocado; Enter siempre lo hace.

### Temporizador

El reloj muestra el tiempo de quien decide, aunque sea una respuesta en el turno ajeno: cada asiento dispone de una referencia de 90 segundos por turno. El contorno del núcleo se vacía con él, se vuelve dorado en los últimos 15 segundos y después cuenta el tiempo extra como «+0:12». **Es solo informativo: no avanza, pasa ni termina nada.** Su tooltip añade el total de cada jugador en el duelo.

Se repite donde el núcleo no se ve: en el aviso de decisión, en el lector durante una galería y en el indicador de las composiciones compactas. Se oculta en la preparación, al terminar y con un servidor que no lo publica; sin hora del servidor muestra «–:––». Los datos se describen en [PvP · Reloj de decisión](PVP.md#reloj-de-decisión).

Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --turn-core-only`; también forma parte de la matriz completa. Con `--ui-screenshot-dir` y renderizado guarda el núcleo en ambas vistas, la decisión y la galería.

## Estado de la decisión, rechazos, cartas jugables y tabla de ataque

Escritorio y pantallas compactas comparten estas señales. Ninguna aplica reglas: todas leen el snapshot autorizado y la conexión.

- **Estado en el núcleo de turno.** En el tablero de escritorio, la primera línea del núcleo (`StageStatusCell`) dice con texto e icono qué ocurre. Los estados posibles son «TU TURNO», «RESPONDE» (decides durante el turno rival), «RIVAL RESPONDE», «TURNO RIVAL», «ENVIANDO…», «RESOLVIENDO…», «JUGADA RECHAZADA» y «SIN CONEXIÓN». El color solo repite el mensaje. La línea se actualiza en cada fotograma sin reconstruir la arena. Sin botón, su hueco conserva el alto y nombra la fase especial (por ejemplo, «DEFENSA»). Las composiciones compactas conservan su indicador propio, que ya incluía «TÚ/RIVAL».
- **Jugada rechazada.** `LobbyConnection` publica `PlayRejection` y `PlayRejectionSerial` cuando el servidor rechaza una jugada o una preferencia COMBO, o cuando la partida cambió antes de enviarla. Una entrega incierta conserva su orden para la reconexión. El aviso central muestra el motivo durante seis segundos o hasta la siguiente jugada y sobrevive a la reconstrucción del tablero. Los rechazos anteriores a abrir la arena se ignoran.
- **Cartas jugables.** Un halo amarillo fijo rodea las cartas que tienen una jugada autorizada. Incluye los COMBO disponibles durante el turno rival y las cartas activables en el campo, como Jessica Lee en Bonus o canje. El cian señala los objetivos de una jugada. El halo en mano se atenúa durante el bloqueo y se oculta durante la preparación, las galerías de elección y el resultado.
- **Tabla de ataque.** Al leer una habilidad, el inspector muestra el resultado por cara final del D6, en el orden del manual: 1–3 fallo, 4 reducido (mitad redondeada hacia arriba, con `GameEngine.DamageFor`), 5 normal y 6 crítico.
  - Usa los aumentos de esa copia y avisa de Lv 2 (+1 al dado salvo con 1) y de Estrella del Fontanero (daño multiplicado), que son públicos.
  - Magnet muestra «½·1D6» y «1D6».
  - Una habilidad en la mano muestra los valores base.
  - Un Ataque seleccionado muestra la tabla de la habilidad que usaría. Al elegir entre varias, pasar el cursor por una habilidad iluminada muestra la suya, con el coste «Consume 1 Ataque».
  - Súper Ataque no tiene tabla, porque modifica el dado.
  - La nota final recuerda que no incluye respuestas, defensas ni el bonus de equipo.

- **Contadores de mano.** En el tablero de escritorio, cada abanico lleva su propio contador junto a la última carta. El propio queda en la franja visible de la mano. El rival queda justo bajo el borde superior, porque las cartas colgantes ya se solapan con la primera fila de su campo. Así cada contador queda fuera de las zonas, incluso con el lienzo de 16:10 o el panorámico. Una mano rival vacía centra su contador. Las composiciones compactas conservan el contador al principio de su franja.
- **Golpe final y segunda caída.** Con 0 vidas, el medidor apilado del escritorio rotula «GOLPE FINAL» sobre la barra vacía, igual que ya hacía la versión compacta. Si el jugador ya llegó a 0 y se curó, la barra conserva un borde rojo, porque volver a 0 termina el duelo. El tooltip explica ambas reglas, que usan `HasReachedZero` del snapshot.
- **Habilidades por enseñar.** Las ranuras vacías muestran su coste pequeño y atenuado. Solo cuando el servidor ofrece enseñar, la primera ranura libre (donde se enseña la siguiente habilidad) se ilumina con el color del jugador y el rótulo «ENSEÑAR AQUÍ».

Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --feedback-only`. También forma parte de la matriz completa. Con `--ui-screenshot-dir` y renderizado guarda el rechazo, la mano jugable y la tabla de ataque. `--board-reading-only` comprueba los contadores (sin solapes con ninguna zona, en cinco tamaños y ambos asientos), el golpe final, la segunda caída y la ranura por enseñar.

## Editor de mazos

Mis mazos es una vitrina: una ficha con portada por mazo y, al lado, el elegido con su
Relikario, mica, dado y acciones. «Estilo» abre el vestidor en ese mazo.

El editor muestra el mazo en el centro, el inspector a la izquierda y la colección a la derecha. En pantallas cortas el nombre y los totales ocupan la barra superior y Deshacer pasa junto a «Agrupar copias», para dejar el alto a las cartas. Las pestañas Principal, Trampas y Banquillo tienen sus propias cuadrículas. «Agrupar copias» alterna entre una ficha con contador por carta y una ficha por copia. Los requisitos del mazo se actualizan al editar y se pueden consultar antes de guardar un borrador.

- Pulsa una carta para ver su arte y su efecto. Pulsa el arte del inspector para ampliarlo.
- La colección muestra los tres artes de Ataque del Relikario del mazo. Yuno y Osamu tienen arte clásico, NS y DT. Ironwing tiene UT (su arte inicial), DT y NS. Cada copia cuenta para el total de 12 y conserva su ilustración durante el duelo. Los artes ajenos de mazos antiguos pasan al arte inicial del Relikario.
- Con clic derecho quitas una copia del mazo o añades una desde la colección. También puedes arrastrar desde la colección a la cuadrícula o a la pestaña de destino.
- Los botones `+` y `−` del inspector editan la carta seleccionada en la pestaña activa. El botón de intercambio mueve una copia entre el banquillo y el mazo que le corresponde.
- «Deshacer» o `Ctrl+Z` revierte el último cambio de cartas.

### Personalización de mazos: micas y dados

Cada mazo guardado tiene una mica y un dado. Son solo cosméticos y no cambian ninguna regla. El catálogo está en `core/CosmeticCatalog.cs`:

- **Micas:** Transparente, Menta, Coral, Oro, Violeta y Medianoche.
- **Dados:** Clásico, Menta, Coral, Oro, Violeta y Obsidiana.

Funcionamiento:

- **Editor.** La pestaña «Estilo» muestra una ficha por opción, con la elegida enmarcada en dorado. Mientras está abierta puedes consultar las cartas de la colección. Cambiar la mica o el dado marca el mazo como modificado. Duplicar un mazo copia su estilo. El mazo de entrenamiento usa la mica Transparente y el dado Clásico.
- **Micas.** `SleeveArt` recolorea en bitono el reverso impreso: los negros toman el tono oscuro de la mica y los blancos el claro, y se añade un borde con su color de canto. Cada reverso se genera una vez, a 512 px de alto, y se conserva durante la sesión. La mica Transparente deja ver el reverso impreso original.
  - Se ve en el mazo y en las cartas corruptas de cada jugador, en la mano oculta del rival y en las cartas que vuelan.
- **Dados.** `DiceFace.ApplyStyle` recolorea el cuerpo y los puntos del dado 3D. Cada tirada muestra el dado de quien tira, también cuando lanza el defensor (por ejemplo, con Esquivar), porque el evento visual del dado ahora indica quién tiró. Los dados de iniciativa conservan el estilo clásico, porque la preparación aún no conoce los estilos.
- **Visibilidad.** Ambos jugadores ven la mica y el dado del otro: viajan en la apertura del duelo y en la vista pública de cada jugador (`DuelPlayerView.Sleeve` y `Dice`). Las partidas anteriores a la personalización usan la mica Transparente y el dado Clásico.

Pruebas:

- `--cosmetics-only`: la mica del dueño en el mazo, las corruptas y la mano rival, y el dado de quien tira.
- `--decks-only`: el editor en cuatro tamaños, gestos de cartas, pestaña Estilo y guardado.
- Ambas forman parte de la matriz completa.

## Perfil público

- **Tarjeta (`ProfileCard`).** Retrato del Relikario dentro del marco elegido (`ProfileAvatar`), nombre con el color de su lado, título, resultados («7 V · 3 D · 1 E · 64 % de victorias»), Relikario favorito y tapete.
- **Rival.** En la preparación y en el duelo, el nombre de cada jugador se puede tocar y abre su tarjeta en una ventana emergente que se cierra al tocar fuera. Sin perfiles en el servidor, el nombre no responde.
- **Vestidor (`Wardrobe`).** La pestaña Personalizar reúne retrato, marco, título y tapete del perfil; Relikario y fondo del inicio; portada, mica y dado de cada mazo. La vista previa sigue cada elección y nada se guarda hasta pulsar Guardar. Salir con cambios pregunta antes. Lo que se gana aparece atenuado con «Se gana»; al pulsarlo dice cómo.
- **Perfil.** Tarjeta, nombre, liga, emblemas y resultados por Relikario.
- **Tapete.** Se aplica al abrir la arena y vuelve al Clásico al salir.

Prueba enfocada: `--profile-only`. Abre la tarjeta del rival en dos tamaños y ambos asientos, comprueba que cabe en pantalla y que no se duplica, y recorre el vestidor. Forma parte de la matriz completa; con `--ui-screenshot-dir` guarda la tarjeta y el editor.

## Toques de animación

Cuatro efectos cosméticos. Ninguno forma parte de `IsAnimating`: no bloquean jugadas ni decisiones. Todos se apagan con la preferencia de movimiento reducido del sistema o con `relick/accessibility/reduced_motion`.

- **Jugada rechazada.** Además del aviso del inspector, la carta o zona cuya acción se envió se sacude 0,2 s. Lo que se desplaza es su posición dibujada, que vuelve exactamente a su sitio.
- **Golpe final.** Con 0 vidas, el brillo rojo del corazón late en un ciclo de 1,6 s. Con movimiento reducido el brillo queda fijo.
- **Resultado central.** Cuando el duelo termina y ya acabaron el dado, los vuelos y el último impacto, aparece un momento central sobre el tapete. Muestra el resultado, la causa pública y las vidas y PR finales, y entra con una escala breve. Nunca toma el puntero y no tapa el riel, que conserva «Volver al lobby», «Revisar tablero» y el historial. Pasados 1,5 s, cualquier clic o tecla lo retira sin consumir el evento. «Revisar tablero» también lo cierra, y no reaparece para el mismo duelo.
- **Código jugado.** Cuando el historial público registra la activación de un código nuevo, su ilustración aparece 0,8 s sobre el centro del tablero con un destello dorado. Una decisión dentro de la misma resolución, como elegir la carta de Disquete, no repite el destello.

- **Creación de un Código Trampa.** Respeta el orden del reglamento (§ 4.6 y § 5.3): primero el canje se completa, con las dos cartas a Memoria, el Bonus a la mano y la reposición; después se decide si crear la trampa.
  - Al decidir, el inspector muestra la trampa y las dos cartas canjeadas que cumplieron su receta, en miniatura junto a «Canjeaste X y Y». Ese dato viene de `DuelSnapshot.TrapIngredients`, que los servidores anteriores dejan vacío.
  - Si se elige «Crear», dos ecos translúcidos de esas cartas salen de la Memoria, convergen sobre el canje e invocan la trampa. Las cartas reales no se mueven.
  - Luego la trampa vuela a su lugar: la mano para Súper Ataque y la zona de equipo para Súper Imán. Zahira Flair aparece y se desvanece sobre el canje, porque su anfitrión se elige a continuación.
  - «Pasar sin crear» no muestra nada. Funciona igual cuando la creación espera a que termine una cadena, porque en ambos casos las cartas ya están en Memoria.
  - Un código trampa creado no repite además el destello genérico de código jugado.

Pruebas enfocadas: `--polish-only` y `--trap-recipe-only`. Ambas forman parte de la matriz completa, y con `--ui-screenshot-dir` guardan el resultado, el código jugado, la decisión con su receta y los momentos de la invocación.

## Textos de interfaz

Un texto visible solo se mantiene si aporta algo que no dicen ya la forma, la posición, otro texto o el propio recuento de cartas. Cuando se sustituye por un icono, su texto sigue en el tooltip y en el nombre accesible. Las reglas, los costes, los nombres de cartas y los verbos de acción no se recortan.

- **Duelo.**
  - Sin «»» ante el jugador activo ni ante la etapa actual.
  - «EQUIPO» y «CANJE» sin «0/1» ni numeración.
  - En escritorio no hay mensaje de conexión junto al logotipo; el código de sala está en su tooltip.
  - El dado no lleva título y la iniciativa no narra la tirada.
  - La cabecera de resolución solo aparece cuando hay algo que resolver, y su mensaje no repite «jugador · carta».
  - Con la tabla del D6 visible, el inspector deja de repetir el daño normal y el crítico.
  - Las galerías por memoria no suman un total.
- **Sala.**
  - Las tarjetas no repiten con una frase lo que dicen sus botones.
  - El mazo en uso se lee junto al Relikario; su botón solo dice «Cambiar».
  - La sala muestra solo su código, sin «Sala ·» ni el número de asiento.
- **Mazos.**
  - Un mazo legal muestra ✓ en lugar de «42 cartas · Listo para jugar».
  - El mazo activo no repite «En uso» en un botón desactivado.
  - En pantallas cortas Estilo, Duplicar y Borrar son iconos con tooltip (`FramedIconButton`).
  - Los totales del editor no repiten los de las pestañas.
  - Las copias del banquillo llevan su propio distintivo con el icono del banquillo, en lugar de «B1».
- **Selector de Relikario.** Sin «Disponible», «Aún no jugable», «Toca la carta para girarla» ni «Cambio automático»; el subtítulo es más corto.

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

Los seis atlas de Yuno conservan un lienzo lógico de 608×504 por cuadro; el reposo con vida es una imagen fija animada por sombreador, continua a cualquier frecuencia de pantalla. La importación limita el ancho a 2432 píxeles y mantiene la cuadrícula: reposo, ataque, daño reducido, normal y crítico, reposo agotado y curación usan el mismo tamaño visual. Los clips, su procedencia y sus prompts están documentados en [las animaciones de Yuno](../assets/characters/yuno/README.md).

## Resolución e historial público

La resolución central distingue respuesta, resolución, resultado y activación
anulada. Identifica la carta original y a su jugador; las respuestas encadenadas
aparecen debajo, sin atribuir la activación original a quien contraataca. Presenta
el resultado cuando terminan el dado y los movimientos de cartas.

«Historial» (en escritorio, en la franja) abre las últimas veinte jugadas públicas, de la más reciente a la más
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

`--pvp-ui-test --matchmaking-only` verifica el ciclo, la cancelación sin mover el
botón, la pausa/desconexión, el movimiento reducido, el emparejamiento inmediato,
la confirmación autorizada y su duración acotada, en cinco tamaños. Se incluye
también en `--lobby-only`. Con `--ui-screenshot-dir` guarda los estados de búsqueda
y rival encontrado en una ejecución gráfica. La prueba de dos procesos con
`--pvp-input-quick` exige una sola confirmación visual en cada cliente.

```bash
./pruebas.sh
```

`pruebas.sh` compila, ejecuta el motor y reparte la matriz de interfaz entre varios procesos de Godot; sus opciones están en el [README](../README.md#pruebas). La matriz es la tabla `checks` de `OnlineLobby.UiTests.cs`: cada comprobación tiene nombre, se ejecuta sola con `--pvp-ui-test --checks=Nombre` y debe pasar tanto sola como detrás de las demás, porque el script las agrupa según su duración. Una comprobación nueva se añade a esa tabla; si además quieres un filtro `--algo-only`, añádelo a `UiTestFlags`. Un filtro mal escrito hace fallar la prueba en lugar de recorrer toda la matriz. Cada comprobación imprime `PVP_UI_CHECK · Nombre · ms`, y el script muestra las diez más lentas.

Para repetir solo la matriz de búsqueda de Código Konamy, añade `--deck-search-only` a los argumentos de la prueba; para Cambio de Mentes, usa `--mind-swap-only`. Se puede combinar con `--ui-screenshot-dir=/tmp/relick-ui` para guardar las capturas de PC y móvil horizontal.

`--portraits-only` reproduce las pruebas de los retratos de Yuno —impactos reducido, normal y
crítico, reposo agotado a cero vidas y curación— sin recorrer el resto de la matriz.

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

`--connection-only` comprueba el aviso compartido durante preparación, duelo y
resultado, con menú y consultas abiertos, bloqueo de clics y teclado, reintento
fallido y recuperación, en cuatro tamaños y ambos asientos. Incluye los controles
de conexión del lobby; `--ui-screenshot-dir` permite capturar el aviso en el duelo.

`--preparation-only` comprueba elección inicial, mulligan y ambos dados de iniciativa en los cuatro tamaños y desde ambos asientos, incluido el resultado, la espera del robo inicial y la recuperación sin repetir la animación.

La matriz recorre 1440×1024, 1024×768, 844×390 y 700×390 desde ambos asientos. La lectura del inspector añade 1280×480: verifica el modo vacío compacto y desplaza efectos largos mediante eventos de rueda, comprobando que cabecera, atributos, coste, botones y mano permanecen fijos. `--inspector-only` ejecuta esa revisión enfocada. Comprueba límites, privacidad, estabilidad de selección inicial, bloqueo tras confirmar, recuperación y terminación de animaciones. Incluye las seis zonas de Bomba de Tiempo y la selección de seis cartas de memoria para reciclar, con una memoria mayor que la mano. También comprueba los tres impactos, su transición al reposo agotado a cero vidas y las curaciones para ambos jugadores/asientos: orden tras el dado, repetición, reconstrucción y recuperación visual sin reproducir efectos pasados.

Para capturas:

```bash
./jugar.sh -- --pvp-ui-test --ui-screenshot-dir=/tmp/relick-ui
./jugar.sh -- --lobby-preview-dir=/tmp/relick-lobby
```

La vista previa de salas no se conecta a ningún servidor. Las pruebas de preparación, combate y dos procesos con entrada real están descritas en [PvP](PVP.md).

`--memory-inspection-only` comprueba la galería y el único inspector actualizable
de ambas memorias, 36 cartas con copias repetidas, desplazamiento independiente,
consulta de cartas corrompidas, privacidad de cartas ocultas, índices de selección de objetivos y
cierre sin modificar el duelo, en cuatro tamaños y ambos asientos. Con
`--ui-screenshot-dir` guarda capturas de la galería en una ejecución gráfica.

## Respuestas COMBO

El encabezado del duelo incluye un control táctil de dos estados, disponible
para ambos jugadores durante su propio turno o el del rival. Se dibuja como el
rótulo COMBO impreso en las cartas, una placa clara e inclinada con bisel, y
cada pulsación cambia la insignia de su extremo:

- **COMBO: preguntar** (placa clara, insignia menta con mano alzada) permite las respuestas opcionales de cartas COMBO.
- **COMBO: pasar** (placa apagada, insignia coral con salto) omite esas respuestas y conserva las cartas, incluidos los
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

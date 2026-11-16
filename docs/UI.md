# Interfaz PvP y pantallas horizontales

El juego inicia directamente en `OnlineLobby`, desde `scenes/Main.tscn`. No se construye un motor local, un rival automático ni un tablero oculto al abrir las salas. El controlador de práctica y sus pruebas de interfaz fueron retirados; las simulaciones de reglas restantes solo se compilan en las pruebas .NET.

## Identidad y futuros playmats

La cabecera muestra el logotipo original de Relick. Marcos con esquinas recortadas, símbolos sólidos y circuitos de bajo contraste conectan las cartas y los controles con la estética del manga y sus referencias a videojuegos. Cada campo se tiñe con el acento de su jugador y se ilumina durante su turno; la identidad del propietario permanece en su cabecera.

`RelickVisuals` centraliza marcos, controles, logotipo y señales de interacción. `RelickPlaymat` define fondo, superficies, borde y circuitos, con una textura opcional para el tapete. Es una base para temas futuros, todavía sin selector, persistencia ni cambio en caliente. El arte de un tapete se dibuja bajo las superficies legibles de la interfaz.

Los significados de `RelickVisuals.State` son independientes del tapete: selección dorada con confirmación, objetivo cian con escuadras y foco de teclado blanco. La prioridad es selección > objetivo > hover; el foco añade un borde interior. Vida baja conserva su señal de peligro. Las zonas vacías mantienen su tamaño y posición con contornos tenues y costes discretos. Las fases futuras quedan atenuadas, las completadas llevan confirmación y la actual se destaca con tamaño y color; la celda de estado dice con texto de quién es el turno.

## Tablero de escritorio

Con un lienzo de al menos 1400×980 unidades lógicas, `OnlineArena` construye el tablero completo; los tamaños menores conservan las distribuciones apiladas descritas más abajo, con el mismo acabado visual.

- **Lienzo único.** `BoardCanvas` coloca todas las zonas en un espacio de diseño de 1440×1024 que se escala de forma uniforme a la ventana. Todas las cartas ocupan 138×193; las zonas apaisadas (habilidades y canje) usan esa misma carta girada. Con una ventana más estrecha que 16:9 el lienzo se reduce entero y el margen sobrante se reparte por encima y por debajo de los campos.
- **Campos enfrentados.** `ArenaPresentation.Field` construye el campo propio en la mitad inferior; el rival es el mismo campo girado 180°. Desde el borde exterior: los dos espacios de canje apaisados, el Relikario (retrato, nombre, vidas, cristales de acción, Destreza y PR) sobre su equipo y las cartas sobre el Relikario, las cuatro habilidades en un bloque 2×2 (500 y 1000 PR arriba, 0 y 300 PR abajo), Memoria sobre Corruptas y el mazo en la esquina. El espacio junto al mazo queda libre para un Relikario que juegue con dos mazos. Un espacio de habilidad vacío solo muestra su coste, grande y centrado.
- **Bonus y Área.** Cada jugador tiene un marco con dos espacios, Bonus y Área, montado sobre la línea central —mitad en cada campo— y junto a sus canjes. El espacio de Área es todavía un marcador: el estado del juego aún no incluye áreas.
- **Barra de fase.** Sobre la línea central, `ArenaPresentation.PhaseBar` muestra el turno, las cuatro etapas (las completadas con confirmación, la actual resaltada) y el botón de avance. Toma el color de quien tiene el turno; sus celdas tienen ancho fijo para que el botón no se mueva entre etapas. Conserva los nombres `CombatCenter`, `StageIndicator` y `TurnDock`.
- **Manos.** La mano propia cuelga del borde inferior con las cartas al tamaño del tablero: en reposo solo el cuadro de efecto queda bajo la pantalla. La carta bajo el cursor, con foco o seleccionada sube hasta verse completa y sus acciones la acompañan; el área de pulsación no se mueve. La mano rival cuelga del borde superior y solo asoma su franja inferior.
- **Elecciones.** Elegir entre cartas que no están en la mano —Disquete, reciclaje, búsqueda en el mazo, Resetear la Mente, corrupción, código de Relikia y habilidad inicial— abre un panel sobre el tablero atenuado. Las memorias se apilan en bloques con el color de su dueño y la decisión que confirma la elección va al pie del panel.
- **Riel de lectura.** A la derecha: COMBO y Menú, la resolución en curso y, debajo, la carta señalada o seleccionada con su inspector. Cuando no hay carta que leer, ese espacio muestra el historial de jugadas públicas, la más reciente primero y con el color de quien jugó; el botón Historial ya no existe en escritorio y el menú conserva el diálogo completo.
- **Relikia.** El contador de PR del jugador abre un menú que solo nombra los cuatro efectos. El efecto bajo el cursor se explica en el riel con su lámina (`assets/relikia/`) y su coste.
- **Lienzo lógico.** En escritorio el alto lógico es 1024 y el ancho sigue la proporción de la ventana (entre 1440 y 2280), de modo que un monitor panorámico no muestra bandas laterales.

Entre el tablero de escritorio y las pantallas compactas (ancho de 1100 o más y menos de 980 de alto, como 1366×768 o un tablet), los dos campos van dentro de `ArenaScroll`. Si caben, ocupan su alto completo; si no, el desplazamiento recibe solo el alto que dejan la mano, el inspector y sus decisiones, de modo que el botón que confirma una elección —habilidad inicial, Disquete, reciclaje— nunca queda fuera de la ventana. `--choice-preview-dir=DIRECTORIO` (con `--choice-sizes=1366x768,…` opcional) informa y captura esos botones en cada tamaño.

`./jugar.sh -- --pvp-ui-test --board-preview-dir=DIRECTORIO` guarda capturas del tablero en reposo, con una carta seleccionada, con el menú de Relikia y con Disquete, a 1820×1024 y 1440×1024.

### Acabado y efectos

`RelickVisuals` genera en memoria los marcos de esquinas recortadas con degradado, borde iluminado y halo (`Glass`, `Surface`, `Field`); no dependen de archivos importados. Los títulos usan una variante inclinada y pesada de la fuente del proyecto (`RelickVisuals.Display`). El tapete es un sombreador (`scripts/arena_playmat.gdshader`): dos mitades teñidas con el color de cada jugador, trama de puntos, líneas de velocidad, chevrones y un divisor luminoso cuyos pulsos toman el color de quien tiene el turno. Las zonas vacías son huecos con escuadras; las ocupadas proyectan sombra.

`ImpactLayer` añade señales cosméticas a partir de los mismos eventos autorizados que mueven los retratos: número de daño o curación sobre el personaje, destello de pantalla al recibir daño, un trazo del atacante al defensor y un aro donde aterriza cada carta. No bloquea jugadas, no forma parte de `IsAnimating` y se limpia al recuperar el estado. Los objetivos válidos laten en cian; la selección mantiene un halo dorado fijo. Con movimiento reducido se omiten trazos, aros y destellos, y los números aparecen sin desplazarse.

## Salas y preparación

La entrada agrupa el nombre del jugador y dos tarjetas: Partida rápida y Sala privada.
La primera ofrece Buscar rival y, durante la espera, un contador y Cancelar búsqueda.
La segunda conserva Crear sala y Unirse mediante código. Buscar bloquea el nombre,
el código y las acciones privadas hasta cancelar o encontrar rival. El fondo propio
del lobby combina un degradado gris azulado, tramas de manga, diagonales y los
chevrones del logotipo en cian/rojo de bajo contraste. La decoración se concentra
en los bordes; las superficies de lectura conservan su contraste. Es estático,
se adapta al tamaño y deja pasar los clics a los controles. Los acentos de las
acciones y el foco visible conservan sus significados. En pantallas cortas se
reducen márgenes y descripciones. Enter en el código permite unirse cuando hay conexión.

Durante la partida rápida, un enlace de dos cartas ocupa el espacio central de
su panel: reverso propio, silueta rival y tres flechas con un ciclo suave de dos
segundos. El tiempo de espera y Cancelar búsqueda permanecen visibles; el botón
conserva su posición. La animación solo avanza durante una búsqueda confirmada
y se pausa al cancelar, perder conexión u ocultarse. La preferencia de reducir
animaciones del sistema, cuando está disponible, y el ajuste de proyecto
`relick/accessibility/reduced_motion` habilitan una variante estática.

Una sala confirmada después de solicitar partida rápida muestra «Rival
encontrado», completa el reverso rival y entra a preparación tras 350 ms. También
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
impacto antes de reemplazar el inspector por el resultado. «Revisar tablero»
recupera la lectura de cartas; Menú → Ver resultado permite volver. El historial
sigue disponible. «Volver al lobby» cierra la sala, muestra la espera de
confirmación y permite a cada jugador terminar de leer su resultado aunque el
rival ya la haya cerrado. El resultado autorizado se conserva en memoria hasta
ese retorno; si no hay conexión se muestra el mismo aviso central de Reconectar. No se presenta una causa
inventada para resultados antiguos sin ese dato.

`ArenaPresentation`, `ArenaMotion` y los controles de cartas, personajes, pilas y dado componen la arena PvP. `OnlineArena` solo consume las vistas autorizadas y envía una opción ligada a sala y revisión; no ejecuta ni predice las reglas.

- Vida, PR, fase, dueño del turno y cantidades de zonas permanecen consultables.
- Mano rival con reversos y únicamente las revelaciones autorizadas.
- Cartas, habilidades, Memoria, Corruptas, equipo, cartas sobre el Relikario, Bonus y canje tienen sus consultas.
- Las respuestas con descartes permiten selección múltiple y exigen la cantidad recibida del servidor.
- El dado muestra el resultado confirmado; su animación no consume azar del duelo.

## Distribución horizontal

Al pulsar una carta aparecen junto a ella las acciones disponibles, con iconos y
etiquetas breves como Jugar, Equipar, Canjear o Atacar. Si hay varias habilidades
para atacar, Atacar permite elegir una de las que se resaltan en el tablero.
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
la memoria. Las corruptas conservan su consulta por cantidad sin identidades;
elegir una carta oculta limpia el texto y los atributos del inspector anterior.
La clasificación incluye el subtipo actual —por ejemplo, Código Personaje— y
COMBO cuando corresponde. Las habilidades muestran daño normal, daño crítico y
premio en un orden fijo; su descripción omite el prefijo que ya repite esos datos.
Los códigos y equipos muestran su texto sin convertir el mismo efecto en otro
indicador. No se añaden las etiquetas «Efecto», «Seleccionada» ni «Efecto visible».

El coste ocupa una sola fila, separado de los botones, y prioriza la opción
autorizada de la acción actual. Al consultar una carta sin una acción disponible
se puede mostrar su coste de catálogo; eso no habilita jugadas. Los contadores y
el daño de una habilidad enseñada corresponden a su copia autorizada. Limpiar la
selección retira esos datos. Sin una carta para mostrar, el inspector se contrae a la
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

El inspector prioriza una instrucción concreta en descartes, objetivos, respuestas y dados. Al tocar una carta sin jugadas disponibles explica el bloqueo usando la fase, los recursos visibles y las opciones autorizadas del propio jugador. La espera solo anuncia un COMBO si el servidor ofrece uno. Enviar una decisión, perder conexión y resolver una animación tienen mensajes específicos; una instrucción nueva vuelve al inicio de la lectura. Los costes permanecen en su campo único.

`ArenaGuidance.SuggestAdvance` consulta todas las opciones del snapshot, independientemente de la carta seleccionada. Solo sugiere avanzar al jugador de turno que debe decidir, en robo, combate, acciones o final, si existe `Advance` y las únicas otras opciones son abandonar. Un COMBO legal, evolución, aprendizaje, Bonus/canje o Relikia impiden la sugerencia. Un COMBO sin sus condiciones cumplidas no la impide.

`AdvancePhaseButton` invita con un pulso dorado de 1,8 segundos, sin mover ni escalar el objetivo táctil. Se detiene al enviar, desconectar, ocultar el control, entrar en una resolución o recuperar una jugada disponible; fuera de la invitación no procesa fotogramas. El botón conserva el destino y el índice del servidor, incluyendo fases repetidas. Avanzar siempre requiere pulsarlo.

Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --guidance-only`. Con `--ui-screenshot-dir=/tmp/relick-guidance` y renderizado habilitado genera capturas de la invitación en cuatro resoluciones.

## Estado de la decisión, rechazos, cartas jugables y tabla de ataque

Escritorio y pantallas compactas comparten estas señales. Ninguna aplica reglas: todas leen el snapshot autorizado y la conexión.

- **Estado en la barra de fase.** En el tablero de escritorio, la primera celda de la barra (`StageStatusCell`) dice con texto e icono qué ocurre. Los estados posibles son «TU TURNO», «RESPONDE» (decides durante el turno rival), «RIVAL RESPONDE», «TURNO RIVAL», «ENVIANDO…», «RESOLVIENDO…», «JUGADA RECHAZADA» y «SIN CONEXIÓN». Debajo aparecen el turno y la fase especial (por ejemplo, «TURNO 02 · DEFENSA»). El color solo repite el mensaje. La celda se actualiza en cada fotograma sin reconstruir la arena. Cuando no se puede avanzar, un hueco del tamaño del botón de avance mantiene el ancho de la barra en el turno rival. Las composiciones compactas conservan su indicador propio, que ya incluía «TÚ/RIVAL».
- **Jugada rechazada.** `LobbyConnection` publica `PlayRejection` y `PlayRejectionSerial` cuando el servidor rechaza una jugada o una preferencia COMBO, o cuando la partida cambió antes de enviarla. Una entrega incierta no cuenta como rechazo: conserva su orden para la reconexión. El inspector muestra el motivo en un recuadro de peligro durante seis segundos o hasta la siguiente jugada. El recuadro sobrevive a la reconstrucción del tablero. Los rechazos anteriores a abrir la arena se ignoran.
- **Cartas jugables.** Una carta de la mano con al menos una opción autorizada de enseñar, atacar, jugar o activar, equipar o canjear lleva un borde menta fijo y un rayo en la esquina superior izquierda, que el abanico nunca tapa. La señal no late, para no competir con los objetivos, y queda atenuada mientras la mano está bloqueada. Los COMBO legales durante el turno rival también se marcan. No se marca nada en la preparación, las galerías de elección ni el resultado.
- **Tabla de ataque.** Al leer una habilidad, el inspector muestra el resultado por cara final del D6, en el orden del manual: 1–3 fallo, 4 reducido (mitad redondeada hacia arriba, con `GameEngine.DamageFor`), 5 normal y 6 crítico.
  - Usa los aumentos de esa copia y avisa de Lv 2 (+1 al dado salvo con 1) y de Estrella del Fontanero (daño multiplicado), que son públicos.
  - Magnet muestra «½·1D6» y «1D6».
  - Una habilidad en la mano muestra los valores base.
  - Un Ataque seleccionado muestra la tabla de la habilidad que usaría. Al elegir entre varias, pasar el cursor por una habilidad iluminada muestra la suya, con el coste «Consume 1 Ataque».
  - Súper Ataque no tiene tabla, porque modifica el dado.
  - La nota final recuerda que no incluye respuestas, defensas ni el bonus de equipo.

- **Contadores de mano.** En el tablero de escritorio, cada abanico lleva su propio contador junto a la última carta. El propio queda en la franja visible de la mano. El rival queda justo bajo el borde superior, porque las cartas colgantes ya se solapan con la primera fila de su campo. Así ningún contador cae sobre Corruptas, Memoria, Equipo u otra zona, ni siquiera con el lienzo de 16:10 o el panorámico. Una mano rival vacía centra su contador. Las composiciones compactas conservan el contador al principio de su franja.
- **Golpe final y segunda caída.** Con 0 vidas, el medidor apilado del escritorio rotula «GOLPE FINAL» sobre la barra vacía, igual que ya hacía la versión compacta. Si el jugador ya llegó a 0 y se curó, la barra conserva un borde rojo, porque volver a 0 termina el duelo. El tooltip explica ambas reglas, que usan `HasReachedZero` del snapshot.
- **Habilidades por enseñar.** Las ranuras vacías muestran su coste pequeño y atenuado. Solo cuando el servidor ofrece enseñar, la primera ranura libre (donde se enseña la siguiente habilidad) se ilumina con el color del jugador y el rótulo «ENSEÑAR AQUÍ».

Prueba enfocada: `godot --headless --path . -- --pvp-ui-test --feedback-only`. También forma parte de la matriz completa. Con `--ui-screenshot-dir` y renderizado guarda el rechazo, la mano jugable y la tabla de ataque. `--board-reading-only` comprueba los contadores (sin solapes con ninguna zona, en cinco tamaños y ambos asientos), el golpe final, la segunda caída y la ranura por enseñar.

## Personalización de mazos: micas y dados

Cada mazo guardado tiene una mica y un dado. Son solo cosméticos y no cambian ninguna regla. El catálogo está en `core/CosmeticCatalog.cs`:

- **Micas:** Clásica, Menta, Coral, Oro, Violeta y Medianoche.
- **Dados:** Clásico, Menta, Coral, Oro, Violeta y Obsidiana.

Funcionamiento:

- **Editor.** La pestaña «Estilo», junto a Mazo y Banquillo, muestra una ficha por opción, con la elegida enmarcada en dorado. Mientras está abierta, pulsar el catálogo solo consulta la carta. Cambiar la mica o el dado marca el mazo como modificado, y guardar los envía con él. Duplicar un mazo copia su estilo. El mazo de entrenamiento usa la mica y el dado clásicos.
- **Micas.** `SleeveArt` recolorea en bitono el reverso impreso: los negros toman el tono oscuro de la mica y los blancos el claro, y se añade un borde con su color de canto. Cada reverso se genera una vez, a 512 px de alto, y se conserva durante la sesión. La Clásica es el reverso impreso sin cambios.
  - Se ve en el mazo y en las cartas corruptas de cada jugador, en la mano oculta del rival y en las cartas que vuelan.
  - En la sala, la tarjeta de búsqueda muestra la mica del mazo activo.
- **Dados.** `DiceFace.ApplyStyle` recolorea el cuerpo y los puntos del dado 3D. Cada tirada muestra el dado de quien tira, también cuando lanza el defensor (por ejemplo, con Esquivar), porque el evento visual del dado ahora indica quién tiró. Los dados de iniciativa conservan el estilo clásico, porque la preparación aún no conoce los estilos.
- **Visibilidad.** Ambos jugadores ven la mica y el dado del otro: viajan en la apertura del duelo y en la vista pública de cada jugador (`DuelPlayerView.Sleeve` y `Dice`). Una partida anterior a esta versión usa los clásicos.

Pruebas:

- `--cosmetics-only`: la mica del dueño en el mazo, las corruptas y la mano rival, y el dado de quien tira.
- `--decks-only`: la pestaña Estilo y el guardado.
- Ambas forman parte de la matriz completa.

## Toques de animación

Cuatro efectos cosméticos. Ninguno forma parte de `IsAnimating`: no bloquean jugadas ni decisiones. Todos se apagan con la preferencia de movimiento reducido del sistema o con `relick/accessibility/reduced_motion`.

- **Jugada rechazada.** Además del aviso del inspector, la carta o zona cuya acción se envió se sacude 0,2 s. Lo que se desplaza es su posición dibujada, que vuelve exactamente a su sitio.
- **Golpe final.** Con 0 vidas, el brillo rojo del corazón late en un ciclo de 1,6 s. Con movimiento reducido el brillo queda fijo.
- **Resultado central.** Cuando el duelo termina y ya acabaron el dado, los vuelos y el último impacto, aparece un momento central sobre el tapete. Muestra el resultado, la causa pública y las vidas y PR finales, y entra con una escala breve. Nunca toma el puntero y no tapa el riel, que conserva «Volver al lobby», «Revisar tablero» y el historial. Pasados 1,5 s, cualquier clic o tecla lo retira sin consumir el evento. «Revisar tablero» también lo cierra, y no reaparece para el mismo duelo.
- **Código jugado.** Cuando el historial público registra la activación de un código nuevo, de cualquiera de los dos jugadores, su ilustración aparece 0,8 s sobre el centro del tablero con el color del jugador. Una decisión dentro de la misma resolución, como elegir la carta de Disquete, no repite el destello.

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
  - El botón del mazo dice «Mazo · nombre».
  - La sala muestra solo su código, sin «Sala ·» ni el número de asiento.
- **Mazos.**
  - Un mazo legal muestra ✓ en lugar de «42 cartas · Listo para jugar».
  - El mazo activo no repite «En uso» en un botón desactivado.
  - Editar, Duplicar y Borrar son iconos con tooltip (`FramedIconButton`).
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

`--pvp-ui-test --matchmaking-only` verifica el ciclo, la cancelación sin mover el
botón, la pausa/desconexión, el movimiento reducido, el emparejamiento inmediato,
la confirmación autorizada y su duración acotada, en cuatro tamaños. Se incluye
también en `--lobby-only`. Con `--ui-screenshot-dir` guarda los estados de búsqueda
y rival encontrado en una ejecución gráfica. La prueba de dos procesos con
`--pvp-input-quick` exige una sola confirmación visual en cada cliente.

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
dotnet build Relick.csproj
./jugar.sh --headless -- --pvp-ui-test
```

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
visibilidad de corruptas/cartas ocultas, índices de selección de objetivos y
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

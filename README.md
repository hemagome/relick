# Relick · Sala de práctica

Demo local para **Godot 4.7.2 o posterior, edición .NET**, contra un rival sencillo. Interfaz en español, sin cuenta ni servidor. Prototipo basado en un subconjunto de las reglas de Relick TCG; el catálogo jugable solo contiene cartas aportadas por el usuario: Ataque, Magnet Shuffle, Destiny Shuffle, Colgante de la Suerte, Senshuni, Caja de Cartón, Vida Extra, Aumento de Relick Points, Huevo Azul Explosivo, Re-lanza y Error de Carga. Yuno dispone de su habilidad y evolución.

## Abrir y jugar

1. Instala [Godot .NET](https://godotengine.org/download/) y el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) — el runtime por sí solo no basta.
2. Importa `project.godot` en Godot y pulsa **F5**. Si Godot pide compilar, pulsa **Build**.
3. Conserva tu mano inicial o pulsa cartas para cambiarlas. El dado decide quién empieza.
4. Selecciona una habilidad de tu mano y pulsa **Enseñar**. Luego selecciona un ataque y pulsa **Atacar y lanzar dado**.
5. Pulsa **Ir a acciones** para jugar códigos o poner equipos (1 acción cada uno), enviar cartas a canje o abrir **Efectos de Relikia**. Termina tu turno cuando quieras; el rival juega solo.

También puedes ejecutar `./jugar.sh` si las herramientas están disponibles. El archivo opcional `.runtime.env` permite señalar herramientas instaladas en otra ubicación. `global.json` selecciona un SDK estable de .NET 8.

## Probar a Yuno animado

El tablero muestra a **ambos relikarios enfrentados**, con las animaciones de Yuno de Ludo. El indicador «EN REPOSO» está oculto; los estados de ataque y daño siguen visibles.
Tu relikario está a la izquierda, con acento verde; el adversario está a la derecha,
con acento rojo. Cada panel muestra un medidor de vidas (8 como máximo para Yuno), PR y contadores de mazo y memoria.

- **Reposo:** cada personaje tiene su propio bucle al abrir y reiniciar la partida.
- **Ataque:** solo se reproduce una vez cuando el resultado definitivo es 4, 5 o 6, después de terminar las tiradas y decidir los relanzamientos. Los resultados finales 1, 2 y 3 no lo animan.
- **Daño:** solo lo reproduce quien recibe daño, sincronizado con el ataque tras terminar el dado; un fallo o daño cancelado no provoca esa reacción.
- Ataque dura **1.2 s**, daño **0.9 s** y el bucle de reposo **2 s**. Las acciones vuelven automáticamente a reposo. El rival espera
  a que ambos terminen sus acciones visibles antes de continuar su siguiente decisión.
- El botón superior **Pruebas** abre las pruebas **Reposo / Ataque / Daño** de ambos personajes. La prueba de ataque hace que
  ese personaje ataque y el otro reaccione, sin cambiar vidas, cartas ni turnos.
- Los relojes de animación son independientes. Seleccionar cartas o actualizar el tablero
  no reinicia ninguno de los dos personajes. Ambos reutilizan las mismas texturas.

La velocidad del juego (60 o 120 fps) no cambia la duración de los clips. Las hojas originales de 3 s se reproducen aceleradas en el MVP. La exportación
tiene 64 cuadros en reposo/daño y 56 en ataque, sobre un lienzo común de 608 × 504 por cuadro.
Se conserva la marca de agua de Ludo del plan gratuito. La prueba aún no incluye un reposo
herido ni una animación de derrota específicos.

## Tablero visual

- Barra de fases con la etapa activa y el dueño del turno. El botón para avanzar pulsa con un brillo dorado cuando no quedan jugadas legales (incluidos los efectos de Relikia). No avanza automáticamente ni brilla durante tiradas o elecciones pendientes.
- **Mano del rival** sobre la resolución de combate: un reverso por carta, usando la imagen original aportada por el usuario. Sustituye los contadores laterales de mano. Se actualiza al robar, jugar, canjear o descartar. La fila se desplaza si no cabe y **Ampliar** permite recorrer todas las cartas en una cuadrícula, sin límite de mano.
  El estado admite revelaciones individuales por efectos: solo esas cartas muestran el frente, con borde dorado, y pueden ampliarse para consultar su información. Una carta oculta no expone su nombre ni efecto en controles o ayudas. La mano solo se muestra completa durante la elección autorizada por un crítico de Magnet.
- **Mazo gráfico** para cada jugador: pila con el reverso original, cantidad restante y espacio vacío al agotarse. Memoria y Corruptas quedan debajo de la pila.
- **Robo animado** desde el mazo hasta la mano: cada carta sigue un arco y las tuyas giran para mostrar su frente al llegar; las del rival conservan el reverso. Los robos de turno, Senshuni, cambios iniciales y Relikia se reproducen carta por carta. La mano se desplaza para mostrar las nuevas cartas. El rival y los controles esperan al final de la animación; actualizar el tablero no la reinicia y una nueva partida la cancela.
- **Reparto y mulligan:** las cinco cartas iniciales se reparten simultáneamente a ambos jugadores, en cinco tandas de 0,28 segundos. El mulligan devuelve primero las cartas elegidas, anima un barajado y roba los reemplazos. Devolver cartas por Relikia y reciclar memoria también animan el mazo. El barajado visual conserva el orden establecido por las reglas: devolver al fondo no aleatoriza el mazo.
- **Memoria y Corruptas animadas:** ataques, códigos usados y descartes viajan desde la mano a Memoria. Los equipos destruidos y las cartas retiradas del campo también viajan a la memoria de su dueño. Al corromper una carta, se mueve desde Memoria y se voltea para llegar boca abajo a Corruptas. Si Error de Carga revierte una corrupción, la carta regresa a Memoria. Los movimientos se completan antes de ofrecer la siguiente elección.
- Vidas con contador y barra continua que se adapta a la vida máxima de cada personaje.
  El relleno se anima al recibir daño o curarse: verde por encima del 50 %, amarillo
  entre el 20 % y el 50 %, y rojo al 20 % o menos. PR identificados por una gema.
- Cuatro espacios de habilidad en una rejilla 2 × 2. Cada espacio mide 184 × 126, el tamaño de una carta de la mano girada a horizontal; mantiene su proporción al ajustar la ventana. Costes 500/1000 PR arriba y 0/300 PR abajo, con daño, crítico, premio y selección destacada al enseñar una habilidad. El lienzo del tablero se adapta desde 1440 × 1024 para mostrar las dos filas completas.
- Junto a cada personaje hay una pila de cartas **sobre el Relikario**, sin límite,
  y un espacio de **equipo**, con un máximo de una carta. Pulsa la pila para recorrer
  todas sus cartas y efectos en una lista desplazable; el equipo se puede ampliar.
  Ambos jugadores empiezan sin equipo ni cartas sobre el Relikario. Poner un equipo cuesta
  una acción: si ya había uno, se destruye y va a memoria antes de colocar el nuevo.
  Reiniciar vacía ambas zonas. No hay cartas de ejemplo ni controles que las creen en Pruebas.
- Dado 3D temporal: aparece al atacar o resolver una tirada por un efecto, cae sobre
  la mesa, rebota con altura decreciente y se asienta con el resultado en la cara superior.
  Permanece brevemente para leerlo y desaparece. El sorteo inicial no ocupa el tablero.
- Resumen de combate y progreso de canje hacia el Bonus en un panel central compacto.
- Cada jugador tiene accesos independientes a **Memoria** (cartas boca arriba) y **Corruptas** (cartas boca abajo), con cantidades separadas y miniatura de la pila. Al abrir Memoria se muestran todas sus cartas en una cuadrícula desplazable. Pulsa una carta para ampliarla y consultar su efecto; al volver se conserva el desplazamiento. En Corruptas se muestran reversos seleccionables sin revelar nombres ni efectos. Puedes cambiar de vista desde las pestañas de la galería.
- Cada jugador tiene dos espacios visibles de canje y uno de Bonus, con miniatura y nombre
  de cada carta. Pulsa una carta de cualquiera de esas zonas para ampliarla y consultar
  su tipo y efecto. Los espacios vacíos se distinguen de los ocupados.
  Antes de empezar el Bonus indica «Por colocar»; al recibirlo,
  la carta queda en la mano y la siguiente del mazo se convierte en el nuevo Bonus.
- Mano centrada con la carta de ataque aportada por el usuario, restaurada con IA para quitar la marca diagonal; habilidades, códigos y equipos muestran las imágenes de las cartas aportadas.
  Las cartas se elevan al pasar el cursor; el borde y la marca indican la selección.
- El efecto de la carta seleccionada aparece sobre los botones de acción; pasar el cursor
  por la carta permite consultar su explicación completa.
- **Registro** identifica al jugador 1 (tú) en verde y al jugador 2 (rival) en coral, con etiquetas J1/J2. Los ataques y efectos sobre otro jugador indican quién actúa y quién recibe el efecto. **Pruebas** permite probar animaciones y las nuevas zonas.

La interfaz está en `scripts/Main.Visuals.cs` y los símbolos, medidores y cartas se dibujan
en `scripts/BoardWidgets.cs`. No se necesitan imágenes ni servicios nuevos para estos elementos.

## Habilidad del Relikario: Yuno

Ambos jugadores usan Yuno Gyambura: en Lv 1 empiezan con **8 vidas** y sus curaciones no pueden superar ese máximo. Pasa el cursor sobre su nombre para consultar su habilidad.

Una vez por turno, después de lanzar el **dado de ataque**, puedes conservarlo o relanzarlo si no salió **1**. La elección se muestra antes de resolver el daño, los premios y los efectos del ataque. El resultado nuevo sustituye al anterior aunque sea peor. Si el relanzamiento da **6**, cuenta como **5**: no es crítico, no activa el efecto de Magnet y Destiny requiere tres descartes para cancelar ese daño.

Un 6 original que decidas conservar sigue siendo crítico. La habilidad no se aplica al dado de daño de Magnet, al sorteo inicial, al reciclaje ni a los efectos de Relikia. El dado animado muestra el resultado físico del relanzamiento y explica la conversión de 6 a 5. El rival automático también decide cuándo usarla. La disponibilidad se restablece al cambiar de turno y al iniciar una nueva partida.

El catálogo de Relikarios define su nombre, nivel, vida máxima, coste de evolución y habilidad de forma independiente de las habilidades enseñadas.

### Evolucionar a Yuno Lv 2

Un **6 natural** obtenido durante la partida deja cumplida la condición de evolución de ese jugador hasta que termine la partida. Se registra el resultado conservado de la tirada original: no cuentan el sorteo inicial, los relanzamientos ni los resultados modificados por habilidades. No se lanza un dado adicional para evolucionar.

Tras cumplir la condición, puedes pulsar **Lv 2 ↑** junto a tus PR durante tu etapa de Combate o Acciones, sin elecciones pendientes, y pagar **1800 PR**. La evolución no consume una acción adicional. Solo se cobra al evolucionar; la condición y el saldo pendiente se explican al pasar el cursor por el botón. El rival también puede evolucionar.

Yuno gana **2 vidas actuales**, incluso si ya tenía sus 8 vidas completas, y su máximo sube a **10**. Por ejemplo, 8/8 pasa a 10/10 y 3/8 pasa a 5/10. Las curaciones posteriores respetan el máximo de 10. Evolucionar no reinicia una habilidad ya utilizada en el turno ni borra el historial de haber llegado a cero vidas.

La habilidad de **Lv 2** sustituye a la anterior: una vez por turno puedes elegir un dado propio cuyo resultado no sea 1 y relanzarlo **hasta dos veces**. Puedes conservar cualquiera de los resultados; si un relanzamiento da **1**, la cadena termina inmediatamente. Solo se puede usar una cadena de relanzamientos por turno, compartida entre ataque, daño de Magnet, reciclaje y efectos de Relikia. En Lv 2, un **6 relanzado permanece como 6**, incluido su crítico y los efectos correspondientes. El resultado se decide antes de aplicar su efecto, incluso durante un reciclaje fuera de tu turno. Al reiniciar la partida, ambos jugadores vuelven a Lv 1 sin la condición cumplida.

## Magnet Shuffle y Destiny Shuffle

Las habilidades anteriores se sustituyeron por las dos cartas aportadas. El arte limpio conserva los créditos impresos; se retiró la marca diagonal. En la mano aparecen giradas para respetar su formato horizontal; al enseñarlas se muestran en horizontal. Las habilidades del rival se pueden ampliar. En las tuyas, clic derecho abre el detalle sin cambiar la selección.

- **Magnet Shuffle:** daño normal 1D6 (una tirada independiente de la tirada de acierto), crítico 5 y premio 500 PR. Un resultado reducido aplica la mitad del D6, redondeada hacia arriba. Ambas tiradas se animan en secuencia. Tras un crítico, puedes mirar la mano rival y tomar una carta elegida de ella o de su Bonus, o pasar. Mirar no revela permanentemente la mano. Tomar el Bonus lo repone desde el mazo rival y puede provocar agotamiento.
- **Destiny Shuffle:** daño normal 3, crítico 5 y premio 600 PR. Cuando recibes un ataque con daño, puedes seleccionar una copia enseñada y descartar exactamente 3 cartas, o 4 si es crítico, para cancelar el daño. Recibe un contador y no puede activarse con dos contadores. Puedes recibir el daño sin usar el efecto. El coste debe poder pagarse completo y cada copia conserva sus propios contadores visibles.
- La cancelación de Destiny cancela solamente el daño: se conservan el premio del acierto y el resultado crítico, incluida la opción de tomar una carta de Magnet. El rival automático también puede usar ambas habilidades y responder durante tu turno.
- Se permiten habilidades repetidas. Se mantienen los cuatro espacios y la enseñanza de una habilidad por turno antes de atacar, con sus costes actuales.

## Colgante de la Suerte y Senshuni

- **Colgante de la Suerte** es un equipo. Se pone desde la mano en la etapa de Acciones por **1 acción**, sin coste de PR. Solo hay un espacio: poner otro equipo envía el anterior a memoria. Durante una tirada propia, puedes destruirlo para **relanzar el dado** o, si ya fue relanzado, sumar **+1 o +2**. Las opciones se ofrecen antes de resolver el dado, incluso si ya agotaste los relanzamientos de Yuno. Un relanzamiento del Colgante no consume el uso de Yuno y no convierte el 6 en 5. La suma al dado de ataque no puede alcanzar 6 (las sumas que darían 6 o más están desactivadas); en otros dados puede llegar a 6. Un resultado obtenido con el equipo no cumple la condición de 6 natural para evolucionar. Los contadores y la parada en 1 de la habilidad de Yuno se conservan al combinar efectos.
- **Senshuni** es un código de personaje: **roba 2 cartas**. Se juega por **1 acción** y pasa a memoria; no limita el tamaño de la mano. Los robos se animan por separado y respetan reciclaje y agotamiento. También puede activarse mediante Relikia por 400 PR y dos acciones; entonces se corrompe antes de resolver el robo.

Las imágenes se limpiaron con la herramienta de edición integrada conservando los créditos impresos. El mazo local mantiene 20 cartas: 3 ataques, dos copias de cada una de las siete cartas anteriores, un Huevo Azul Explosivo, un Re-lanza y un Error de Carga.

## Caja de Cartón

Equipo real, con dos copias en el mazo local. Ponerla cuesta **1 acción**, con la misma regla de equipo único: el anterior se destruye y va a memoria. Mientras esté equipada, puedes destruirla para elegir una respuesta:

- **Cancelar el daño de un ataque:** se decide después de resolver los dados y antes de aplicar daño. Funciona contra daño reducido, normal o crítico y no necesita habilidades enseñadas, descartes, PR ni acciones adicionales. Si también tienes Destiny disponible, eliges una de las dos respuestas. Puedes dejar pasar el ataque y conservar la Caja. Solo se cancela el daño: el premio del acierto y los efectos de crítico de Magnet se mantienen.
- **Cancelar la activación de un código de tipo objeto:** se abre una respuesta antes de ejecutar el efecto, tanto al jugarlo desde la mano como al activarlo desde memoria mediante Relikia. La Caja va a tu memoria; el código queda en la memoria de su dueño sin ejecutar el efecto. Si fue activado por Relikia y se canceló, no se corrompe. Al anular el código se devuelven sus costes de PR y acciones, incluidos los de activarlo mediante Relikia. También puedes dejarlo resolver y conservar el equipo.

Los códigos registran su subtipo. **Senshuni es de personaje**, por lo que no puede ser cancelada por la Caja. **Vida Extra sí es de tipo objeto** y permite usar esa respuesta antes de lanzar su dado, sin curar si se cancela. El rival automático también utiliza la Caja para defenderse.

## Vida Extra

Código de tipo **objeto**, con dos copias en el mazo local. Jugarlo cuesta **1 acción** y pasa a memoria. Lanza un D6 y gana vidas según el resultado:

| D6 | Vidas |
| --- | --- |
| 1 | 1 |
| 2 | 2 |
| 3, 4 o 5 | 3 |
| 6 | 4 |

La curación no supera el máximo actual del Relikario: 8 en Yuno Lv 1 y 10 en Lv 2. Puede curar desde cero mientras la partida siga en curso y conserva el historial de haber llegado a cero. El dado y la barra de curación se animan; el registro muestra el resultado final y las vidas efectivamente recuperadas.

Caja de Cartón puede cancelar la activación **antes de lanzar el dado**; el código queda en memoria y no cura. Si resuelve, Yuno Lv 2 y Colgante de la Suerte pueden modificar la tirada mediante sus reglas habituales: la curación se calcula una sola vez a partir del resultado final. Yuno Lv 1 no relanza este dado de curación. Un 6 natural sin relanzar ni modificar registra la condición de evolución. Vida Extra también puede activarse desde memoria con Relikia por 400 PR y dos acciones; se corrompe antes de tirar, salvo que la Caja cancele esa activación.

## Aumento de Relick Points

Código de tipo **jugada**, con dos copias en el mazo. Jugarlo cuesta **1 acción**, pasa a memoria y lanza 1D6. Según el resultado permite aumentar o disminuir los PR de cualquiera de los dos jugadores: **1: 300 · 2: 400 · 3–4: 500 · 5: 1000 · 6: 1500**.

Después de decidir el dado final, eliges jugador y signo entre cuatro opciones. No se puede avanzar hasta resolver la elección; cerrar la ventana no aplica ninguna opción por defecto. Los PR nunca bajan de cero. Yuno Lv 2 y Colgante pueden modificar la tirada, y Relikia puede activar el código desde memoria, corrompiéndolo. Caja de Cartón no puede cancelarlo, porque no es de tipo objeto. El rival también puede resolverlo.

## Huevo Azul Explosivo y códigos combo

**Huevo Azul Explosivo** es código objeto: cuesta **1 acción y 200 PR**. Elige una no-habilidad en el campo de cualquiera de los jugadores: equipo, carta sobre el Relikario, Bonus o carta en canje. Va a la memoria de su dueño; destruir un Bonus revela el siguiente del mazo. No permite destruir habilidades en ninguna de estas zonas, Relikarios ni cartas de mano. Se necesita un objetivo válido y saldo suficiente antes de activarlo. Caja de Cartón puede cancelarlo; al anular la activación se devuelven PR y acciones. Activarlo mediante Relikia exige **600 PR en total y dos acciones**, y se corrompe si no se cancela.

Los **combos** pueden activarse en cualquier etapa de la partida en curso, incluso durante el turno enemigo, si cumplen sus requisitos. Cada jugador tiene **su propio cupo de dos acciones**, que se renueva al empezar cualquier turno. Gastar una acción antes de la etapa de Acciones reduce el cupo que quedará para esa etapa. Las cartas sin COMBO siguen limitadas a la etapa de Acciones propia.

**Re-lanza** es una excepción impresa: **no cuesta acciones**. Requiere un dado propio pendiente; no permite relanzar dados enemigos ni dados ya resueltos. Se ofrece junto a las opciones de dado, también sobre un 1 y durante un reciclaje fuera de tu turno. Relanza una vez; si la cara obtenida es **1, 2 o 3**, roba una carta. El robo respeta agotamiento y reciclaje sin perder la decisión del dado original. No consume el uso de Yuno ni convierte un 6 en 5, pero ese resultado tampoco es natural para evolucionar. Pasa a memoria al activarse y puede usarse incluso con cero acciones disponibles.

## Error de Carga y cadenas de cancelación

**Error de Carga** es un código **jugada COMBO**, cuesta **1 acción** y responde a la activación o resolución pendiente de un código rival, o a la colocación de un equipo nuevo. No cancela ataques, habilidades de Relikario ni efectos de equipos ya colocados. Al jugarlo va a memoria y el rival puede responder con otro Error de Carga si dispone de la carta y una acción propia.

La cadena se resuelve desde la última respuesta hacia la primera. Una carta anulada va a memoria, no aplica su efecto y recupera **todos los PR y acciones pagados por esa activación**. Si se anula un Error de Carga, recupera su acción y deja de cancelar la carta anterior. No se devuelven cartas a la mano. Esta regla de devolución también se aplica a la cancelación de un código objeto mediante Caja de Cartón; cancelar solo el daño de un ataque no anula el ataque ni devuelve su carta o premio.

- Huevo (A) → Error (B): A recupera la acción y los 200 PR; el objetivo sigue en el campo. Cupos finales A **2**, B **1**, si ambos empezaron con 2.
- Huevo (A) → Error (B) → Error (A): el Error de B se anula y recupera su acción; el Huevo resuelve. Cupos finales A **0**, B **2**.
- Si se anula poner un equipo, el nuevo va a memoria y **el anterior permanece equipado**. El reemplazo solo sucede si la colocación llega a resolverse.

La ventana muestra la cadena, los costes y las acciones propias disponibles. Se puede pasar sin responder. Para efectos que esperan un dado o una selección de objetivo, el botón **Error de Carga** permite interrumpir antes de aplicar el efecto. Los efectos inmediatos se responden antes de que empiecen a aplicarse; no se deshacen efectos ya resueltos. Si se cancela un código activado desde memoria, se restaura allí sin corrupción y se devuelven también los costes de Relikia. El rival automático participa en las respuestas y contrarespuestas.

## Efectos de Relikia

En Acciones, **Efectos de Relikia** muestra las cuatro opciones, sus costes y por qué alguna no está disponible. Se validaron el texto y las imágenes del [manual aportado](https://sites.google.com/view/relickjuegodecartas/reglas/manual-de-juego#h.30zn4sondl7b) el 6 de noviembre de 2026.

| Coste | Acciones | Resolución |
| --- | --- | --- |
| 100 PR | 1 | Con canje vacío, mueve el Bonus a canje. |
| 300 PR | 1 | Roba una carta y elige cualquier carta de la mano para enviarla al fondo del mazo o a canje. |
| 400 PR | 2 | Elige un código boca arriba de tu memoria, actívalo y corrómpelo. |
| 500 PR | 1 | Roba tu Bonus y lanza D6. Con 5/6, elige que el rival descarte dos cartas de su elección o elige dos códigos para corromper. |

Las elecciones se completan antes de continuar la etapa y no gastan acciones adicionales. Un envío a canje por efecto también puede completar el canje y reclamar el Bonus. Se repone el Bonus desde el mazo cada vez que se retira, conservando el comportamiento solicitado para el MVP. El rival de práctica puede usar efectos y resolver sus propias elecciones. Si hay menos de dos cartas para un descarte o corrupción, se afectan las disponibles; con cero se completa sin bloquear el turno.

La memoria y las cartas corruptas se consultan desde sus respectivos accesos y galerías. Los selectores de Relikia también muestran miniaturas de los códigos válidos para activar o corromper. Consultar o seleccionar en una galería no activa ni mueve cartas. Las corruptas se guardan boca abajo al fondo de la memoria, separadas visualmente de las cartas disponibles. **Criterio provisional:** no se pueden reactivar ni reciclar; el manual describe su posición, pero no aclara expresamente su interacción con el reciclaje por agotamiento.

**Diferencia del manual:** el texto del efecto de 500 PR dice «cualquier memoria», mientras que su imagen indica la memoria rival. Se sigue el texto y se permiten ambas memorias, pendiente de aclaración del usuario.

## Dado 3D animado

El D6 es una malla 3D con bordes redondeados, puntos en sus seis caras e iluminación.
Se dibuja sobre fondo transparente dentro del tablero 2D, con cámara elevada para destacar la cara superior.
Se muestra en una ventana temporal durante ataques de ambos jugadores o efectos con tirada,
incluido el reciclaje. La caída, tres rebotes decrecientes y el asentamiento duran 1.45 segundos.
La altura sigue arcos de gravedad; la orientación y los apoyos mantienen sus esquinas sobre la mesa.
Al detenerse muestra durante 0.9 segundos el valor
que ya calculó el motor de reglas en la **cara superior**, como un dado sobre la mesa.
La animación no consume azar del duelo. Es una trayectoria animada con resultado controlado,
no una simulación física que elija el valor del dado.
El rival espera al dado y los controles de jugada se reactivan cuando termina.

El dado desaparece con un fundido después del resultado; el resumen del combate permanece.
El sorteo inicial se consulta en el registro. Reiniciar cancela el lanzamiento en curso.

En este equipo las herramientas quedaron instaladas permanentemente para tu usuario:

- **Godot .NET 4.7.2:** `~/.local/share/godot/Godot_v4.7.2-stable_mono_linux_x86_64/`.
- **SDK de .NET 8.0.427:** `~/.dotnet/`.
- **Comandos:** `godot`, `godot-mono` y `dotnet`, disponibles mediante `~/.local/bin/`.
- **Menú de aplicaciones:** **Godot (.NET)**.

La demo ya no depende de herramientas ni paquetes guardados en `/tmp`. Las instalaciones para tu usuario no se actualizan con el gestor de paquetes del sistema.

## Alcance de esta demo

- Partida local completa: preparación, mano sin límite de cartas, combate, acciones, victoria y reinicio.
- Dos relikarios de práctica con 8 vidas en Lv 1 y 10 de máximo en Lv 2; mazos simétricos de 20 cartas: 3 ataques, dos Magnet Shuffle, dos Destiny Shuffle, dos Colgantes, dos Senshuni, dos Cajas, dos Vida Extra, dos Aumentos de PR, un Huevo Azul, un Re-lanza y un Error de Carga.
- Once definiciones reales en el mazo: Ataque, Magnet Shuffle, Destiny Shuffle, Colgante de la Suerte, Senshuni, Caja de Cartón, Vida Extra, Aumento de Relick Points, Huevo Azul Explosivo, Re-lanza y Error de Carga. Se retiraron Recuperación, Recarga, Sello, Carga y Guante de prueba.
- Dado D6, daño reducido/normal/crítico y premios PR.
- Cuatro espacios de habilidad, con costes de **0 / 300 / 500 / 1000 PR**, para ambos jugadores. En pantalla se muestran 500/1000 PR arriba y 0/300 PR abajo. Se mantiene una habilidad enseñada por turno y se permiten copias repetidas en distintos espacios. Los contadores de Destiny son independientes por copia.
- Bonus, canje y memoria; reciclaje simplificado cuando se agota el mazo.
- Rival con decisiones sencillas: enseña, ataca, pone equipos, juega Senshuni, usa los efectos de dado disponibles y evita canjear sus ataques.
- Registro de jugadas y ayuda dentro del juego.

**Todavía no hay integración con SpacetimeDB ni multijugador.** La primera entrega prioriza el duelo local elegido. El motor de reglas está separado para reutilizarlo después en el servidor.

Además de las dos habilidades aportadas, no se incluye el catálogo completo, construcción de mazos, otros niveles o Relikarios, otros equipos o cartas sobre el Relikario, áreas, combos, destreza, colección, cuentas, tienda ni clasificación. Los relikarios tienen 8 vidas y, por tanto, no requieren fichas iniciales de destreza según el manual consultado.

## Reglas y diferencias documentadas

La referencia principal es el [manual oficial actual](https://www.relicktcg.com/reglas/manual-de-juego). La [página de reglas](https://www.relicktcg.com/reglas) enlaza al [video 5.0](https://www.youtube.com/watch?v=2zzW7j0XLpw). El contenido del video no se pudo recuperar en esta sesión; no se considera validado. El video 3.0 que aparece en búsquedas es anterior al manual consultado.

Se conserva el orden robo → combate → acciones → final; el límite de dos acciones, la mano sin límite por defecto, los resultados del D6 y la condición especial de vida cero. Los PR dependen de la habilidad utilizada.

Simplificaciones intencionales:

| Aspecto | Decisión de la demo |
| --- | --- |
| Mazo | 20 cartas en vez de 42: 4 ataques, dos copias de las siete cartas anteriores y una copia de Huevo Azul Explosivo, Re-lanza y Error de Carga. |
| Mano | Sin límite por defecto. El descarte solo se permite si un efecto lo solicita, incluido el efecto de Relikia de 500 PR. Se conserva la preferencia del usuario de mano sin límite, aunque el manual ahora incluye una regla de 2026 de descartar hasta siete. |
| Habilidades | Cuatro espacios. Costes 0/300/500/1000 PR; enseñar antes de atacar, una vez por turno. |
| Cartas y personajes | Yuno tiene 8 vidas y su habilidad propia. Las cartas aportadas conservan sus efectos; no hay cartas de ejemplo en el catálogo jugable. |
| Primer reciclaje | D6 y selección aleatoria automática de memoria, para ambos jugadores. |
| Sin memoria al agotar | Se aplica la comparación final para evitar un bloqueo; decisión provisional del prototipo. |
| Bonus | Una carta por jugador, retirada del mazo después del cambio inicial. Cada dos canjes pasa a la mano y se repone con la siguiente carta del mazo. |
| Respuestas | Destiny y Caja de Cartón permiten responder a ataques; la Caja también responde a códigos objeto antes de su activación. Los códigos COMBO se activan al cumplir sus requisitos y respetan su coste impreso. |

El manual y la página de modos presentan diferencias sobre el agotamiento. Se adoptó el manual: segundo agotamiento, pérdida de dos vidas y comparación; la selección de memoria sigue siendo una simplificación de esta demo.

## Estructura

```text
core/                 Motor C# sin dependencias de Godot
  CardCatalog.cs      Cartas reales y composición del mazo
  GameState.cs        Estado de la partida
  GameEngine.cs       Validaciones y resolución de jugadas
  PracticeBot.cs      Una decisión legal del rival por llamada
scripts/              Interfaz de Godot y dado
scenes/Main.tscn       Escena principal
tests/                Pruebas ejecutables sin paquetes de terceros
docs/STACK.md         Elección de stack y siguiente etapa en línea
```

## Comprobaciones

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
dotnet build Relick.csproj
godot --headless --path . -- --smoke-test
godot --headless --path . -- --animation-test
godot --headless --path . -- --attack-timing-test
godot --headless --path . -- --pile-animation-test
godot --headless --path . -- --dice-test
godot --headless --path . -- --visual-test
godot --headless --path . -- --relikia-test
godot --headless --path . -- --opponent-hand-test
godot --headless --path . -- --memory-test
godot --headless --path . -- --draw-test
godot --headless --path . -- --skills-test
godot --headless --path . -- --yuno-test
godot --headless --path . -- --evolution-test
godot --headless --path . -- --equipment-test
godot --headless --path . -- --opening-animation-test
godot --headless --path . -- --senshuni-test
godot --headless --path . -- --box-test
godot --headless --path . -- --extra-life-test
godot --headless --path . -- --points-test
godot --headless --path . -- --combo-test
godot --headless --path . -- --load-error-test
```

El nombre del ejecutable de Godot puede variar. Usa la edición .NET. Las pruebas comprueban jugadas inválidas sin cambios de estado, daño, victoria, curación, acciones, canje, manos grandes sin descarte obligatorio, descarte solicitado por efectos, agotamiento, los cuatro efectos de Relikia, elecciones, corrupción, brillo, conservación de cartas y 2.000 duelos completos con semillas diferentes. La prueba de la escena ejecuta un duelo y vuelve a la preparación. No sustituye una revisión visual.

Las semillas permiten repetir los mismos sorteos durante las pruebas. Para el modo en línea, la fuente de azar y la aplicación de comandos deberán pertenecer exclusivamente al servidor.


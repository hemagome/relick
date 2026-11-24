# Yuno · animaciones

> **Registro histórico.** Las restauraciones con `image_gen` y la eliminación de marcas de agua que se describen aquí documentan cómo se hicieron estos recursos; no son un procedimiento a seguir. Las imágenes nuevas se añaden con su marca de agua y sin edición generativa ([Añadir un Relikario](../../../docs/ADDING_RELIKARIO.md#5-arte)). La situación de estos recursos está en [docs/PUBLICATION_AUDIT.md](../../../docs/PUBLICATION_AUDIT.md).

Exportaciones de Ludo.ai facilitadas por el usuario, con la marca de agua retirada mediante
edición generativa con la herramienta integrada `image_gen`. Se procesaron 46 bloques de cuatro
fotogramas para reconstruir las partes cubiertas sin reducir cada figura al tamaño de una hoja completa.
Los bloques se separaron y ensamblaron con ImageMagick, conservando las cuadrículas, el orden,
el lienzo de 608 × 504 por fotograma y las duraciones que utiliza el juego.
La reconstrucción introduce pequeñas variaciones de dibujo; no es una restauración idéntica píxel a píxel.
Los originales y el ZIP siguen en `output/yuno-ludo/` y las hojas anteriores están en el historial de Git.

El prompt utilizado y la corrección de transparencia están en [cleanup-prompts.txt](cleanup-prompts.txt).
Las pruebas, los bloques intermedios y las vistas de revisión locales quedan en
`artifacts/yuno-watermark-review/v2/`.

El cuarto clip, `exhausted-idle.png`, se creó con `image_gen` para representar a Yuno consciente,
adolorido, exhausto y preocupado a cero vidas: se sujeta el costado, baja el guantelete y conserva
algunos raspones superficiales. Usa ocho fases de una inhalación lenta, reproducidas en ida y vuelta
con los extremos repetidos (0…7, 7…0), formando un bucle de 16 cuadros sin salto entre final e inicio.
Se extrajo el fondo para conservar transparencia real. Los prompts están en
[exhausted-idle-prompts.txt](exhausted-idle-prompts.txt); los intermedios y la vista previa local,
en `artifacts/yuno-zero-health/`.

El quinto clip, `heal.png`, es una curación de estilo arcade creada con `image_gen`: Yuno se lleva
la mano al pecho, se endereza con alivio y aparecen corazones verdes de píxel que ascienden y se
desvanecen. Los corazones son decorativos; la vida recuperada la determina el motor y la muestra
el marcador. Son 16 poses consecutivas, sin ida y vuelta, con regreso a la postura de reposo.
Los prompts están en [heal-prompts.txt](heal-prompts.txt) y los intermedios y la vista previa local
en `artifacts/yuno-heal/`.

Los clips `hit-normal.png` y `hit-critical.png` se crearon con `image_gen` para diferenciar los
impactos normales y críticos. El normal hace retroceder a Yuno y protegerse el pecho; el crítico
lo desequilibra y lo dobla de dolor, con un destello mayor y una recuperación más lenta.
`hit.png` conserva íntegra la animación anterior para daño reducido. Sus prompts nuevos están en
[hit-normal-prompts.txt](hit-normal-prompts.txt) y [hit-critical-prompts.txt](hit-critical-prompts.txt);
los intermedios y las vistas previas, en `artifacts/yuno-impacts/`. El juego utiliza seis atlas PNG y la imagen de reposo.

| Archivo | Rejilla | Cuadro | Cuadros | Duración en el MVP | Repetición |
| --- | --- | --- | --- | --- | --- |
| idle-rest.png | imagen fija | 640 × 536 (608 × 504 + 16 de margen) | 1 | continuo | Sí; movido por sombreador |
| attack.png | 8 × 7 | 608 × 504 | 56 | 1.2 s | No; vuelve a reposo |
| hit.png (reducido) | 8 × 8 | 608 × 504 | 64 | 0.9 s | No; vuelve a reposo |
| exhausted-idle.png | 8 × 2 | 608 × 504 | 16 | 2.8 s | Sí; solo a cero vidas |
| heal.png | 8 × 2 | 608 × 504 | 16 | 1.2 s | No; vuelve a reposo |
| hit-normal.png | 8 × 2 | 608 × 504 | 16 | 0.9 s | No; vuelve a reposo |
| hit-critical.png | 8 × 2 | 608 × 504 | 16 | 1.2 s | No; vuelve a reposo |

El reposo ya no es una hoja de fotogramas. `idle-rest.png` es el primer cuadro de la antigua hoja de
Ludo.ai —boca cerrada, mirada al frente— con 16 píxeles transparentes de margen, y
`scripts/yuno_idle.gdshader` lo mueve en función del tiempo: el pecho sube y se ensancha al respirar,
la cabeza lo sigue con un pequeño retraso, las puntas del pelo y el faldón del abrigo se mecen, la
guardia flota y las celdas del guantelete laten. Al ser continuo se dibuja a la frecuencia de la
pantalla (60, 120 Hz o más) y no tiene primer ni último cuadro que unir; la boca y los ojos no cambian.
Los periodos de cada movimiento no son múltiplos entre sí, de modo que el conjunto no repite un
ciclo reconocible. Cada jugador usa un desfase distinto y el movimiento reducido baja la amplitud.
La hoja anterior de 64 cuadros (2 s, 32 cuadros por segundo, con la boca abriéndose y saltos entre
bloques reconstruidos) sigue en el historial de Git y su original en `output/yuno-ludo/`.

Orden de las hojas: izquierda a derecha, arriba abajo. Las hojas de ataque y daño reducido procedentes
de Ludo.ai se exportaron a 3 s. El MVP las reproduce aceleradas: ataque 2.5× y daño aproximadamente 3.33×.
Cada clip mantiene su velocidad (cuadros / duración en el MVP), independientemente
de que el juego se dibuje a 60 o 120 fps.
Yuno representa visualmente a ambos jugadores. El rival usa las mismas hojas, invertidas
horizontalmente al dibujar, con su propio reloj de animación. No cambia las reglas ni los valores de las cartas.
El reposo depende de la vida autorizada del jugador, tanto al recibir una actualización como al
recuperar la partida: a cero vidas usa `exhausted-idle`, y con vida positiva usa `idle`.
El último golpe termina su reacción antes de entrar al bucle agotado. Reconstruir el tablero
no reinicia la respiración; restablecer las animaciones conserva el reposo correspondiente a la vida.
La curación se activa mediante la señal pública `Heal` de una revisión aceptada, con el jugador y
la cantidad efectiva recuperada. Espera las tiradas y movimientos de cartas pendientes; al terminar
regresa al reposo que corresponda a la vida actual. Recuperar una vista no reproduce curaciones pasadas.
El impacto usa `Hit.AttackDie`: 4 → reducido, 5 → normal, 6 → crítico. Es el resultado final del
ataque confirmado por el motor, conservado durante la defensa; no se deduce del número de vidas
perdidas ni del dado adicional de daño de Magnet. Un seis convertido en cinco usa daño normal.
El daño sin tirada de ataque tiene valor 0 y conserva la reacción genérica anterior.

Las opciones de importación limitan las hojas a 2432 píxeles de ancho (320 la imagen de reposo): Godot genera
texturas a media resolución, con cuadros de 304 × 252 y la misma cuadrícula. Los PNG
fuente conservan las dimensiones de las exportaciones. El componente calcula las regiones desde el tamaño importado
y conserva el lienzo lógico de 608 × 504. Véase [interfaz y optimizaciones](../../../docs/UI.md).

## Carta de Relikario y retrato

`card-lv1.png` y `card-lv2.png` son las cartas impresas de Yuno Gyambura (TUTOYUNO-1/7 y BY5-1/14) a 697 × 987.
Se restauraron con `image_gen` para retirar la marca de agua diagonal RELICK y conservar el texto, las
estadísticas y los créditos impresos. El selector de Relikario las muestra y las alterna con un giro.

`portrait.png` es un recorte cuadrado de 212 × 212 tomado del interior de `thumbnail.png`, sin el corte pentagonal,
para las fichas cuadradas del selector.


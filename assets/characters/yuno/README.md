# Yuno · animaciones

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
en `artifacts/yuno-heal/`. El juego utiliza únicamente los cinco atlas PNG de esta carpeta.

| Archivo | Rejilla | Cuadro | Cuadros | Duración en el MVP | Repetición |
| --- | --- | --- | --- | --- | --- |
| idle.png | 8 × 8 | 608 × 504 | 64 | 2 s | Sí |
| attack.png | 8 × 7 | 608 × 504 | 56 | 1.2 s | No; vuelve a reposo |
| hit.png | 8 × 8 | 608 × 504 | 64 | 0.9 s | No; vuelve a reposo |
| exhausted-idle.png | 8 × 2 | 608 × 504 | 16 | 2.8 s | Sí; solo a cero vidas |
| heal.png | 8 × 2 | 608 × 504 | 16 | 1.2 s | No; vuelve a reposo |

Orden: izquierda a derecha, arriba abajo. Las tres hojas procedentes de Ludo.ai se exportaron a 3 s.
El MVP las reproduce aceleradas: reposo 1.5×, ataque 2.5× y daño aproximadamente 3.33×.
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

Las opciones de importación limitan las texturas a 2432 píxeles de ancho: Godot genera
hojas a media resolución, con cuadros de 304 × 252 y la misma cuadrícula. Los PNG
fuente conservan las dimensiones de las exportaciones. El componente calcula las regiones desde el tamaño importado
y conserva el lienzo lógico de 608 × 504. Véase [interfaz y optimizaciones](../../../docs/UI.md).

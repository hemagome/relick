# Yuno · animaciones

## Procedencia

- `card-lv1.png` y `card-lv2.png` son las cartas impresas de Yuno Gyambura (TUTOYUNO-1/7 y BY5-1/14), tal como se aportaron, con su marca de agua, convertidas a PNG sin otra edición. El selector de Relikario las muestra y las alterna con un giro.
- `attack.png`, `hit.png` e `idle-rest.png` proceden de exportaciones de Ludo.ai aportadas por el usuario. Su marca de agua se retiró con edición generativa (`image_gen`), por bloques de cuatro fotogramas ensamblados con ImageMagick; el dibujo puede variar ligeramente respecto al original.
- `exhausted-idle.png`, `heal.png`, `hit-normal.png` y `hit-critical.png` se crearon con `image_gen` a partir del personaje.
- `thumbnail.png` y `portrait.png` (un recorte cuadrado de 212 × 212 del interior de la miniatura, para las fichas del selector) derivan de esas imágenes.

Estos recursos derivados no llevan marca de agua y se hicieron con edición generativa antes de la norma actual del proyecto, que exige conservar la marca y no usar edición generativa en las imágenes nuevas ([Añadir un Relikario](../../../docs/ADDING_RELIKARIO.md#5-arte)). Su situación está en [docs/PUBLISHING.md](../../../docs/PUBLISHING.md).

## Clips

| Archivo | Rejilla | Cuadro | Cuadros | Duración | Repetición |
| --- | --- | --- | --- | --- | --- |
| idle-rest.png | imagen fija | 640 × 536 (608 × 504 + 16 de margen) | 1 | continuo | Sí; movido por sombreador |
| attack.png | 8 × 7 | 608 × 504 | 56 | 1,2 s | No; vuelve a reposo |
| hit.png (reducido) | 8 × 8 | 608 × 504 | 64 | 0,9 s | No; vuelve a reposo |
| hit-normal.png | 8 × 2 | 608 × 504 | 16 | 0,9 s | No; vuelve a reposo |
| hit-critical.png | 8 × 2 | 608 × 504 | 16 | 1,2 s | No; vuelve a reposo |
| heal.png | 8 × 2 | 608 × 504 | 16 | 1,2 s | No; vuelve a reposo |
| exhausted-idle.png | 8 × 2 | 608 × 504 | 16 | 2,8 s | Sí; solo a cero vidas |

Las hojas se leen de izquierda a derecha y de arriba abajo. Cada clip mantiene su velocidad (cuadros / duración) aunque el juego se dibuje a 60 o 120 fps. El rival usa las mismas hojas invertidas horizontalmente, con su propio reloj de animación.

- **Reposo.** `idle-rest.png` es un único cuadro con 16 píxeles de margen transparente. `scripts/yuno_idle.gdshader` lo mueve en función del tiempo: el pecho sube al respirar, la cabeza lo sigue con un pequeño retraso, el pelo y el abrigo se mecen, la guardia flota y las celdas del guantelete laten. Al ser continuo se dibuja a la frecuencia de la pantalla y no tiene cuadros que unir. Los periodos de cada movimiento no son múltiplos entre sí, cada jugador usa un desfase distinto y el movimiento reducido baja la amplitud.
- **Impactos.** `Hit.AttackDie` elige el clip: 4 → reducido (`hit.png`), 5 → normal, 6 → crítico. Es el resultado final del ataque confirmado por el motor, no el número de vidas perdidas ni el dado adicional de Magnet; un seis convertido en cinco usa el normal. El daño sin tirada de ataque usa la reacción genérica.
- **Cero vidas.** `exhausted-idle.png` muestra a Yuno consciente, adolorido y preocupado: ocho fases de una inhalación lenta reproducidas en ida y vuelta (0…7, 7…0). El reposo depende de la vida autorizada, también al recuperar la partida. El último golpe termina su reacción antes de entrar en el bucle, y reconstruir el tablero no reinicia la respiración.
- **Curación.** `heal.png` (16 poses, mano al pecho y corazones verdes de píxel) se activa con la señal pública `Heal` de una revisión aceptada. Espera las tiradas y movimientos de cartas pendientes y vuelve al reposo que corresponda. Recuperar una vista no reproduce curaciones pasadas.

Las opciones de importación limitan las hojas a 2432 píxeles de ancho (320 la imagen de reposo): Godot genera texturas a media resolución, con cuadros de 304 × 252 y la misma cuadrícula. El componente calcula las regiones desde el tamaño importado y conserva el lienzo lógico de 608 × 504. Véase [Interfaz](../../../docs/UI.md).

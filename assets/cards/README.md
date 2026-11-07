# Cartas

## Reverso

`card-back.png` es la conversión a PNG, sin redimensionar ni retocar, del reverso
aportado por el usuario en `/home/hemagome/Downloads/back.webp` (424 × 600 píxeles).
Sustituye la versión anterior de 212 × 300 píxeles por su mayor resolución y detalle.
La mano del adversario muestra una miniatura por carta, manteniendo la proporción de
la imagen. Las cartas ocultas no reciben datos de identidad en sus controles visuales;
solo las reveladas por efectos reciben y muestran su frente.

## Carta de ataque

`attack-clean.png` es una restauración mediante la herramienta integrada de edición de imágenes
del archivo aportado por el usuario en `/home/hemagome/Downloads/ataque`.
Se eliminó la marca diagonal conservando el diseño y los créditos impresos. Al ser una
reconstrucción generativa puede haber pequeñas diferencias en los circuitos o el símbolo.

El original y el prompt están en `output/imagegen/attack-original.jpg` y
`output/imagegen/attack-clean-prompt.txt`. En la mano se dibuja la imagen completa,
sin recortarla ni deformarla; el borde de selección, la marca y el efecto al pasar el cursor
siguen siendo controles de la interfaz.

## Magnet Shuffle y Destiny Shuffle

`magnet-shuffle-clean.png` y `destiny-shuffle-clean.png` son las versiones limpias
creadas con la herramienta integrada de edición de imágenes a partir de los PNG
aportados por el usuario. Se retiró la marca diagonal; se conservaron nombres,
texto de efectos, valores, identificadores y créditos impresos. La reconstrucción
generativa puede introducir pequeñas diferencias en la ilustración.

Los originales y los prompts completos utilizados están en
`output/imagegen/magnet-shuffle-original.png`, `output/imagegen/destiny-shuffle-original.png`,
`output/imagegen/magnet-shuffle-prompt.txt` y `output/imagegen/destiny-shuffle-prompt.txt`.
Los archivos del juego están en `assets/cards/` y no dependen de rutas externas.

## Colgante de la Suerte y Senshuni

`lucky-pendant-clean.png` y `senshuni-clean.png` son las restauraciones sin la marca
superpuesta de las imágenes aportadas por el usuario. Se editaron mediante la
herramienta integrada, preservando el diseño, el texto de efecto y los créditos
impresos; la reconstrucción puede introducir pequeñas diferencias en la ilustración.
Los originales y los prompts completos están en `output/imagegen/` como
`senshuni-original.jpg` y `senshuni-prompt.txt`. El Colgante actual se reconstruyó
posteriormente desde el PNG original de 697 × 987 píxeles, más nítido que el JPG
de igual resolución: `lucky-pendant-original.png` y `lucky-pendant-png-prompt.txt`.
Se conservan el JPG y su prompt anteriores, y la restauración previa en
`lucky-pendant-clean-from-jpg.png`, como referencia. El juego carga los PNG locales
de `assets/cards/`.

## Caja de Cartón

`cardboard-box-clean.png` es la versión limpia de `/home/hemagome/Downloads/caja.jpg`,
editada mediante la herramienta integrada para retirar la marca superpuesta y
preservar el diseño, el texto y los créditos impresos. La reconstrucción puede
introducir pequeñas diferencias en la ilustración. El original y el prompt completo
están en `output/imagegen/cardboard-box-original.jpg` y
`output/imagegen/cardboard-box-prompt.txt`.

## Vida Extra

`extra-life-clean.png` es la versión limpia de `/home/hemagome/Downloads/vida-extra.jpg`,
editada mediante la herramienta integrada para retirar la marca superpuesta y
preservar el diseño, las seis correspondencias del dado y los créditos impresos.
La reconstrucción puede introducir pequeñas diferencias en la ilustración. Se guardan
el original y el prompt completo en `output/imagegen/extra-life-original.jpg` y
`output/imagegen/extra-life-prompt.txt`.

## Aumento de Relick Points

`points-increase-clean.png` es la versión sin marca superpuesta de `/home/hemagome/Downloads/aumento.jpg`,
restaurada con la herramienta integrada conservando diseño, tabla de resultados y créditos impresos.
La reconstrucción puede introducir pequeñas diferencias en la ilustración. El original y el prompt
completo están en `output/imagegen/points-increase-original.jpg` y `output/imagegen/points-increase-prompt.txt`.

## Huevo Azul Explosivo y Re-lanza

`blue-egg-clean.png` y `reroll-clean.png` se restauraron mediante la herramienta
integrada para retirar las marcas superpuestas, conservando efectos y créditos.
La reconstrucción puede introducir pequeñas diferencias en la ilustración.
Los originales y prompts completos están en `output/imagegen/blue-egg-original.png`,
`blue-egg-prompt.txt`, `reroll-original.jpg` y `reroll-prompt.txt`.

## Error de Carga

`load-error-clean.png` es la restauración sin marca superpuesta de
`/home/hemagome/Downloads/error-de-carga.png`, realizada con la herramienta integrada.
Se conservaron diseño, rótulo COMBO, condiciones y créditos impresos. La reconstrucción
puede introducir pequeñas diferencias en la ilustración. El original y el prompt
completo están en `output/imagegen/load-error-original.png` y `load-error-prompt.txt`.

## Restauraciones actualizadas desde PNG originales

Las versiones actuales de Re-lanza, Aumento de Relick Points, Vida Extra, Caja de
Cartón y Senshuni se reconstruyeron mediante la herramienta integrada a partir de
los PNG originales aportados posteriormente. Tienen la misma resolución de origen
que los JPG (697 × 987), pero conservan mejor texto, bordes y líneas finas.
Se retiraron las marcas superpuestas, manteniendo los valores y créditos impresos.

En `output/imagegen/`, cada una conserva su nuevo original como
`<id>-original.png`, el prompt completo como `<id>-png-prompt.txt` y la versión
limpia anterior como `<id>-clean-from-jpg.png`. Los identificadores son `reroll`,
`points-increase`, `extra-life`, `cardboard-box` y `senshuni`.
Los JPG y sus prompts anteriores se conservan para referencia.

Caja de Cartón usa ahora la edición BY4-6/14 del PNG: «No recibir daño de un ataque»
y «Cancelar la activación de un código que sea tipo objeto». Esta actualización
solo sustituye la imagen y no modifica las reglas ya implementadas.

`diamond-helmet-clean.png` restaura el JPG del Casco de Diamantes (B04-6/15), retirando
la marca diagonal y conservando la ilustración, las reglas y el crédito impreso.
Se editó con `image_gen` y se normalizó a 697×987; el prompt está en
`diamond-helmet-prompt.txt`. El original aportado permanece en Downloads.
Es equipo normal: resta dos a cada daño, con mínimo cero. Un 1 en cualquier tirada
de ataque, también en un relanzamiento, destruye todos los cascos equipados y los
envía a memoria de sus dueños. Unos en otros dados no lo destruyen.
El mazo de prueba conserva 20 cartas: una copia de Casco sustituye una de Colgante.

`time-bomb-clean.png` restaura Bomba de Tiempo (B04-9/15) a 697×987 con `image_gen`,
conservando texto, ilustración y crédito del autor. Los prompts están en `time-bomb-prompt.txt`.
Es código de tipo Objeto: elige equipo, Bonus o mano al azar, de cualquiera de los jugadores.
La carta destruida va a memoria de su dueño; el Bonus se repone. La selección aleatoria se
resuelve en el servidor y no enumera cartas de la mano rival. Respeta Caja de Cartón, Error
de Carga y Relikia. Una copia sustituye una de Aumento de PR, manteniendo las 20 cartas.

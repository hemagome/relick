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

`konamy-code-clean.png` restaura Código Konamy (B04-11/15) desde el PNG aportado,
usando la herramienta integrada `image_gen` y normalizando a 697×987. Conserva la
secuencia de botones, las reglas y ambos créditos impresos. El prompt completo está
en `konamy-code-prompt.txt`; el original permanece en Downloads.
Es código de tipo Jugada, sin COMBO: cuesta una acción y permite elegir una
no-habilidad del mazo propio para añadirla a la mano. La búsqueda y la identidad de
la carta añadida son privadas. Conserva el orden del mazo restante: el texto no
indica barajarlo. Error de Carga y Relikia utilizan sus ventanas y costes comunes;
Caja de Cartón no cancela códigos Jugada. Una copia sustituye una de Caja de
Cartón, conservando veinte cartas en los mazos nuevos.

`mind-swap-clean.png` restaura Cambio de Mentes (B04-10/15) mediante la herramienta
integrada `image_gen`, normalizada a 697×987. Se retiraron las marcas superpuestas,
conservando las flechas, las reglas y los créditos de Pikisuperstar/Freepik y del
autor. El prompt está en `mind-swap-prompt.txt`; el original permanece en Downloads.
Es código Jugada, sin COMBO: una acción, cero PR, prohibido en el turno 1 global.
Intercambia las manos restantes después de las respuestas, incluso con cero cartas.
La propia carta queda en memoria o se corrompe si se activó mediante Relikia.
Las cartas previamente reveladas conservan esa condición al cambiar de mano.
Una copia sustituye una de Vida Extra en los mazos nuevos, conservando veinte cartas.

## Agilidad al 100%

`agility-100-clean.png` es la restauración del JPG aportado mediante `image_gen`,
normalizada a 697×987. Se retiró la marca diagonal, conservando el diseño, el texto
y los créditos impresos. La reconstrucción generativa puede introducir pequeñas
diferencias en la ilustración. El original y el prompt están en
`output/imagegen/agility-100-original.jpg` y `agility-100-prompt.txt`. Es código Jugada con COMBO, cuesta una acción incluso durante el ataque:
fija en 5 un dado propio pendiente de ataque o efecto, sin relanzarlo ni robar cartas.
Admite Error de Carga. Una copia sustituye una de Ataque en el mazo de 20 cartas.

## Resetear la Mente

`mind-reset-clean.png` restaura el JPG aportado mediante `image_gen`, normalizado a
697×987. Se retiró la marca de agua diagonal, conservando texto, consola, marco y
crédito impreso. La reconstrucción generativa puede introducir pequeñas diferencias.
El original y el prompt están en `output/imagegen/mind-reset-original.jpg` y
`mind-reset-prompt.txt`.

Código Jugada con COMBO, una acción. El activador elige un jugador y mira su mano
solo en su vista privada. Selecciona dos copias distintas de las memorias normales
(propias, rivales o una de cada una) y una no-habilidad de la mano objetivo. Las tres
regresan al mazo de su dueño; se barajan los mazos afectados y solo el objetivo roba
una carta. Las memorias corruptas no son seleccionables. Si al mirar solo hay
habilidades, el efecto termina sin devolver cartas ni robar. Error de Carga puede
anularlo y devolver el coste; Caja de Cartón no responde a códigos Jugada.
Una copia sustituye una de Ataque, manteniendo el mazo de veinte cartas.

## Esquivar

`dodge-clean.png` restaura la imagen aportada en Downloads/esquivar con `image_gen`,
a 697×987, sin la marca diagonal y conservando reglas y créditos impresos.
Original y prompt: `output/imagegen/dodge-original.jpg` y `dodge-prompt.txt`.
Código Jugada COMBO, una acción del defensor. Ante un ataque pendiente lanza D6:
1–3 roba una carta y conserva la decisión de defensa; 4–6 niega el ataque completo,
sin daño ni efecto crítico de Magnet y retirando el premio de ese ataque.
Admite Error de Carga y modificadores del dado. Una copia sustituye una de Senshuni
para conservar el mazo de veinte cartas.

## PC 8BITS

`pc-8bits-clean.png` restaura la imagen aportada en Downloads/pc con `image_gen`,
a 697×987, sin la marca diagonal, conservando diseño, reglas y créditos impresos.
Original y prompt: `output/imagegen/pc-8bits-original.png` y `pc-8bits-prompt.txt`.
Al equiparse, después de las respuestas de Error de Carga, lanza un dado y guarda
su resultado sobre la carta. El valor es público y persiste al reconectar.
Al decidir un dado propio de ataque o efecto puede destruirse: con 1–3 suma el valor
guardado (máximo 5); con 4–6 sustituye el resultado por ese valor. No es relanzamiento
ni convierte el resultado modificado en seis natural. La destrucción no cuesta otra
acción; manda PC a memoria y elimina el valor guardado. Reemplazarlo también lo limpia.
El dado al equipar admite los modificadores ya disponibles. Una copia sustituye
una de Magnet Shuffle, conservando veinte cartas y ambos tipos de habilidad inicial.

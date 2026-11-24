# WD

## Procedencia

- `card-lv1.png`, `card-lv2.png` y las cartas propias de WD en `assets/cards/` son los escaneos aportados por el usuario, con su marca de agua, convertidos a PNG sin otra edición.
- `portrait.png` es un recorte transparente del personaje extraído de WD Lv 1 mediante edición generativa (`image_gen`); no lleva marca de agua. Se hizo antes de la norma actual del proyecto, que exige conservar la marca y no usar edición generativa en las imágenes nuevas ([Añadir un Relikario](../../../docs/ADDING_RELIKARIO.md#5-arte)). Su situación está en [docs/PUBLISHING.md](../../../docs/PUBLISHING.md).

## En el juego

WD no tiene hojas de animación ni rig: su animación de combate y el disparo verde se dibujan en el tablero a partir del retrato.

Multi Drive calcula el daño y el premio con la mano al resolver el ataque. El daño normal es la mitad de las cartas en mano, redondeada hacia arriba. Cada fallo confirmado permite pagar un nuevo relanzamiento.

WD tiene música propia: `Combat:wd` y `Climax:wd` (véase [Audio](../../audio/README.md)).

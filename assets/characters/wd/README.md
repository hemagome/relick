# WD

> **Registro histórico.** Las restauraciones con `image_gen` y la eliminación de marcas de agua que se describen aquí documentan cómo se hicieron estos recursos; no son un procedimiento a seguir. Las imágenes nuevas se añaden con su marca de agua y sin edición generativa ([Añadir un Relikario](../../../docs/ADDING_RELIKARIO.md#5-arte)). La situación de estos recursos está en [docs/PUBLICATION_AUDIT.md](../../../docs/PUBLICATION_AUDIT.md).

`card-lv1.png`, `card-lv2.png` y las cartas propias de WD en `assets/cards/` son los escaneos aportados por el usuario, con su marca de agua, convertidos a PNG sin otra edición. El retrato se extrajo de WD Lv 1 con edición generativa antes de adoptar esa norma; el prompt de abajo es un registro histórico.

Multi Drive calcula el daño y el premio con la mano al resolver el ataque. El daño normal es la mitad de las cartas en mano, redondeada hacia arriba. Cada fallo confirmado permite pagar un nuevo relanzamiento.

WD tiene música propia: `Combat:wd` y `Climax:wd` (véase [Audio](../../audio/README.md)). Su animación de combate y el disparo verde se dibujan en el tablero a partir del retrato.

## Retrato

### wd-lv1.png → [portrait.png](portrait.png)

> Use case: background-extraction. Produce a game-ready transparent sprite of the exact fictional WD character visible in the attached WD level 1 card. Keep only the same original blond green-eyed masked character in the white WD cap and white/gray clothes with the same original comic linework, pose and fictional sci-fi device. Show his head, both arms and torso completely as in the upper character art, with a neat natural waist cutoff where the source name plate obscures the lower torso. Remove the repeated translucent watermark and every card frame, number, type label, ornament, UI element, background, border, name bar and rule box. Keep the WD letters on his cap. Center the entire cutout with narrow even transparent padding, no shadow, alpha outside the character.

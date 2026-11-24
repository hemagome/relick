# Yuno Oro

## Procedencia

- `card-lv1.png` y `card-lv2.png` proceden de YunoOroLv1.png y YunoOroLv2.png, y la variante de Ataque (`assets/cards/attack-yuno-oro-clean.png`) de YunoOroAtk.png, archivos suministrados con el diseño. Se usan tal como se aportaron, con su marca de agua, convertidos a PNG sin otra edición.
- `portrait.png` es un recorte de la carta de nivel 1, y `sprite.png` una figura de cuerpo entero con alfa, reconstruida con edición generativa (`image_gen`) a partir de la carta de nivel 1 y de Relick Gold Digger (piernas y botas). Ninguno lleva marca de agua.

Los recursos derivados se hicieron antes de la norma actual del proyecto, que exige conservar la marca y no usar edición generativa en las imágenes nuevas ([Añadir un Relikario](../../../docs/ADDING_RELIKARIO.md#5-arte)). Su situación está en [docs/PUBLISHING.md](../../../docs/PUBLISHING.md).

## Figura animada del tablero

`YunoOroPortraitRig` deforma la figura en tiempo real. Las dos formas comparten la respiración en guardia, la extensión del guante al atacar, tres reacciones al daño, la curación y el reposo a cero vidas. El proyectil nace en el guante animado. La opción de movimiento reducido atenúa la figura y omite los destellos y el proyectil.

El director musical selecciona `Combat:yuno-oro` y `Climax:yuno-oro` por la familia de ambas formas.

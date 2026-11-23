# Yuno Oro

- Las cartas de nivel 1 y 2 proceden de los archivos YunoOroLv1.png y YunoOroLv2.png suministrados con el diseño. El retrato es un recorte de la carta de nivel 1 restaurada.
- La variante de Ataque procede de YunoOroAtk.png y se registra como `attack-yuno-oro`.
- Se usó ImageGen integrado para quitar la marca diagonal de cada imagen. El prompt por archivo fue «Edit target: attached <archivo>.png. Remove only the repeated large translucent diagonal RELICK watermark, restoring the artwork beneath. Keep the source composition, card edges, aspect ratio, intentional symbols, exact printed Spanish text, and copyright and illustrator credits. Full card, no crop.» Se conservaron los recuadros de reglas originales de ambas cartas de nivel.
- El director musical selecciona `Combat:yuno-oro` y `Climax:yuno-oro` por la familia de ambas formas.

## Figura animada del tablero

`sprite.png` es un recorte de cuerpo entero con alfa. ImageGen integrado usó la carta de nivel 1 como referencia principal y Relick Gold Digger como referencia para reconstruir las piernas y las botas. Se limpiaron píxeles aislados del canal alfa y se recortó el margen lateral.

El rig de Godot deforma la figura en tiempo real. Las dos formas comparten la respiración en guardia, la extensión del guante al atacar, tres reacciones al daño, la curación y el reposo a cero vidas. El proyectil nace en el guante animado. La opción de movimiento reducido atenúa la figura y omite los destellos y el proyectil.

Prompt de extracción usado con ImageGen integrado

> Background extraction and faithful reconstruction for an existing Godot tabletop game. First image is the primary edit target: extract only Yuno Oro, retaining exactly the sharp spiky golden hair, distinctive young face and expression, golden high-collar jacket, silver horizontal upper-arm bands, gold buckle and armor, dark ink outlines, cel shading and oversized gold studded gamepad glove. Second image is supporting reference only for the boots, pant legs and glove construction. Reconstruct the hidden hair tips and continue body through legs and boots, with the full silhouette visible head-to-toe. Compact three-quarter ready-to-fight stance, looking and facing toward screen right, weight distributed with knees bent, the large gamepad glove held forward around chest height on screen right, second fist near waist, boots visibly grounded. Preserve the printed illustration's stylization and gold-on-gold palette with cream highlights and black lines. The single character fills roughly 85% of height; leave clear transparent margins around all edges. Use a genuinely transparent alpha background. No card frame, type, marks, rim sticker outline, letters, floor, shadow, effects, or other elements.

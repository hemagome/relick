# Osamu Takahashi

## Procedencia

- `card-lv1.png`, `card-lv2.png` y `attack-card.png` (la carta azul de Ataque) son los archivos aportados por el usuario, con su marca de agua, convertidos a PNG sin otra edición.
- `sprite.png` (la figura del tablero, con gafas, ojos rojos y abrigo índigo en una guardia contenida), `turbo-sword.png` (la espada como capa independiente, con su empuñadura, guarda y circuitos), `portrait.png` y `attack-projectile.png` derivan de la carta Lv 1 mediante edición generativa (`image_gen`) y no llevan marca de agua.

Los recursos derivados se hicieron antes de la norma actual del proyecto, que exige conservar la marca y no usar edición generativa en las imágenes nuevas ([Añadir un Relikario](../../../docs/ADDING_RELIKARIO.md#5-arte)). Su situación está en [docs/PUBLISHING.md](../../../docs/PUBLISHING.md).

## En el tablero

`OsamuPortraitRig` usa las dos capas en ambos niveles. Los movimientos de reposo, ataque, tres impactos, curación y cero vidas se evalúan según el tiempo transcurrido en cada fotograma. La mano y la espada comparten anclaje durante los cambios de postura. El golpe crítico termina antes de pasar al reposo agotado. El movimiento reducido atenúa los desplazamientos y destellos.

La carta azul sustituye la ilustración del Ataque clásico en los mazos de Osamu. La ilustración se resuelve desde el Relikario del dueño visible de la carta; el motor usa la definición compartida de Ataque.

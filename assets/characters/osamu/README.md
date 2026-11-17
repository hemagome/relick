# Osamu Takahashi

El [recorte transparente en la pose impresa](../../../output/imagegen/osamu-lv1-cutout.png) conserva la postura, el rostro y los objetos de la carta Lv 1. `sprite.png` adapta la figura a la guardia del tablero. `turbo-sword.png` conserva la empuñadura, la guarda y el patrón de circuitos como capa independiente. `attack-card.png` es la carta azul de Ataque restaurada del JPG aportado por el usuario.

Los PNG se crearon con `image_gen`. Para el recorte se pidió conservar la pose, la expresión y los objetos que sostiene en la carta, reconstruyendo las zonas cubiertas por los elementos impresos. Para la figura del tablero se pidió conservar sus gafas, ojos rojos y abrigo índigo, darle una guardia contenida y dejar un puño visible para articular la espada. Para la espada se pidió aislar la hoja completa y orientarla a la derecha. Para Ataque se pidió quitar la marca diagonal y conservar la espada de píxel, las dos leyendas ATAQUE y el crédito impreso.

El tablero usa las dos capas en ambos niveles. Los movimientos de reposo, ataque, tres impactos, curación y cero vidas se evalúan según el tiempo transcurrido en cada fotograma. La mano y la espada comparten anclaje durante los cambios de postura. El golpe crítico termina antes de pasar al reposo agotado. El movimiento reducido atenúa los desplazamientos y destellos.

La carta azul se usa para el Ataque básico de Osamu. La ilustración se resuelve desde el Relikario del dueño visible de la carta. El motor sigue usando la definición compartida de Ataque.

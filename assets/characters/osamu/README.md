# Osamu Takahashi

> **Registro histórico.** Las restauraciones con `image_gen` y la eliminación de marcas de agua que se describen aquí documentan cómo se hicieron estos recursos; no son un procedimiento a seguir. Las imágenes nuevas se añaden con su marca de agua y sin edición generativa ([Añadir un Relikario](../../../docs/ADDING_RELIKARIO.md#5-arte)). La situación de estos recursos está en [docs/PUBLICATION_AUDIT.md](../../../docs/PUBLICATION_AUDIT.md).

Un recorte transparente en la pose impresa, ya retirado del repositorio, conservaba la postura, el rostro y los objetos de la carta Lv 1. `sprite.png` adapta la figura a la guardia del tablero. `turbo-sword.png` conserva la empuñadura, la guarda y el patrón de circuitos como capa independiente. `card-lv1.png`, `card-lv2.png` y `attack-card.png` (la carta azul de Ataque) son los archivos aportados por el usuario, con su marca de agua, convertidos a PNG sin otra edición.

`sprite.png` y `turbo-sword.png` se crearon con `image_gen`. Para el recorte se pidió conservar la pose, la expresión y los objetos que sostiene en la carta, reconstruyendo las zonas cubiertas por los elementos impresos. Para la figura del tablero se pidió conservar sus gafas, ojos rojos y abrigo índigo, darle una guardia contenida y dejar un puño visible para articular la espada. Para la espada se pidió aislar la hoja completa y orientarla a la derecha.

El tablero usa las dos capas en ambos niveles. Los movimientos de reposo, ataque, tres impactos, curación y cero vidas se evalúan según el tiempo transcurrido en cada fotograma. La mano y la espada comparten anclaje durante los cambios de postura. El golpe crítico termina antes de pasar al reposo agotado. El movimiento reducido atenúa los desplazamientos y destellos.

La carta azul se usa para el Ataque básico de Osamu. La ilustración se resuelve desde el Relikario del dueño visible de la carta. El motor sigue usando la definición compartida de Ataque.

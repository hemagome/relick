# Yuno · animaciones

Exportaciones de Ludo.ai facilitadas por el usuario, con la marca de agua retirada mediante
edición generativa con la herramienta integrada `image_gen`. Se procesaron 46 bloques de cuatro
fotogramas para reconstruir las partes cubiertas sin reducir cada figura al tamaño de una hoja completa.
Los bloques se separaron y ensamblaron con ImageMagick, conservando las cuadrículas, el orden,
el lienzo de 608 × 504 por fotograma y las duraciones que utiliza el juego.
La reconstrucción introduce pequeñas variaciones de dibujo; no es una restauración idéntica píxel a píxel.
Los originales y el ZIP siguen en `output/yuno-ludo/` y las hojas anteriores están en el historial de Git.

El prompt utilizado y la corrección de transparencia están en [cleanup-prompts.txt](cleanup-prompts.txt).
Las pruebas, los bloques intermedios y las vistas de revisión locales quedan en
`artifacts/yuno-watermark-review/v2/`; el juego utiliza únicamente los tres PNG de esta carpeta.

| Archivo | Rejilla | Cuadro | Cuadros | Duración en el MVP | Repetición |
| --- | --- | --- | --- | --- | --- |
| idle.png | 8 × 8 | 608 × 504 | 64 | 2 s | Sí |
| attack.png | 8 × 7 | 608 × 504 | 56 | 1.2 s | No; vuelve a reposo |
| hit.png | 8 × 8 | 608 × 504 | 64 | 0.9 s | No; vuelve a reposo |

Orden: izquierda a derecha, arriba abajo. Las hojas originales se exportaron a 3 s.
El MVP las reproduce aceleradas: reposo 1.5×, ataque 2.5× y daño aproximadamente 3.33×.
Cada clip mantiene su velocidad (cuadros / duración en el MVP), independientemente
de que el juego se dibuje a 60 o 120 fps.
Yuno representa visualmente a ambos jugadores. El rival usa las mismas hojas, invertidas
horizontalmente al dibujar, con su propio reloj de animación. No cambia las reglas ni los valores de las cartas.
No hay animaciones específicas de herido persistente ni derrota; a cero vidas se conserva
el comportamiento actual del duelo y, tras la reacción de daño, se vuelve al reposo disponible.

Las opciones de importación limitan las texturas a 2432 píxeles de ancho: Godot genera
hojas a media resolución, con cuadros de 304 × 252 y la misma cuadrícula. Los PNG
fuente conservan las dimensiones de las exportaciones. El componente calcula las regiones desde el tamaño importado
y conserva el lienzo lógico de 608 × 504. Véase [interfaz y optimizaciones](../../../docs/UI.md).

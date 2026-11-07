# Yuno · prueba de animación

Exportaciones de Ludo.ai facilitadas por el usuario, sin quitar la marca de agua del plan gratuito.
Se copiaron las tres hojas del paquete conjunto para conservar el mismo lienzo y alineación.
Los originales y el ZIP siguen en `output/yuno-ludo/`.

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
fuente no se modificaron. El componente calcula las regiones desde el tamaño importado
y conserva el lienzo lógico de 608 × 504. Véase [interfaz y optimizaciones](../../../docs/UI.md).

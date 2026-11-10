# Audio de Relick

Las composiciones instrumentales y los efectos de robo, curación y ataque se sintetizan específicamente para este proyecto mediante `tools/audio/render.cpp`, sin muestras ni melodías de terceros. La música usa una estética de RPG y arcade con piano eléctrico, arpegios, bajos, pads, campanas, percusión y una voz de sintetizador principal. Los efectos de dado y barajado utilizan grabaciones con licencia CC0, detalladas abajo.

| Archivo | Título | Uso |
| --- | --- | --- |
| `music/menu.ogg` | Antes del destino | Menú común, piano y arpegios suaves con percusión contenida. |
| `music/yuno-combat.ogg` | Apuesta de acero | Duelo contra Yuno: melodía principal, bajo y batería más activos. |
| `music/yuno-climax.ogg` | La última tirada | Cero vidas contra Yuno: octavas, contrarritmos y arpegios más densos. |

Cada pista dura 48 segundos: 160 BPM, 4/4 y 128 pulsos. Comparten una estructura armónica de ocho compases desarrollada en cuatro secciones. Las reverberaciones y las colas de las notas se envuelven en el bucle para mantener continuidad. Los archivos `.import` conservan el tempo, el compás, la longitud musical y la repetición; deben versionarse junto con los OGG.

| Efecto | Duración | Diseño |
| --- | --- | --- |
| `sfx/card-draw.wav` | 0,22 s | Fricción de papel y desplazamiento breve. |
| `sfx/card-shuffle.wav` | 0,78 s | Cartas barajándose, de Brian MacIntosh (BMacZero). |
| `sfx/dice-roll.wav` | 1,37 s | Grabación real de un dado de madera rodando sobre una mesa. |
| `sfx/heal.wav` | 1,05 s | Campanas ascendentes con cola suave. |
| `sfx/attack.wav` | 0,46 s | Barrido corto y golpe de baja frecuencia. |

Los másteres son estéreo a 44,1 kHz. Las piezas se distribuyen en OGG Vorbis y los efectos en WAV PCM de 16 bits. Los generadores usan una semilla fija para que los recursos sean reproducibles.

El dado procede de [Wooden dice on wodden table roll, de Wuzzy](https://opengameart.org/content/wooden-dice-on-wodden-table-roll), archivo `Holzwürfel_auf_Holztisch_3.flac`, publicado bajo [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). El original está en `sources/wooden-dice-roll-3.flac`, con su procedencia en `sources/DICE-LICENSE.txt`. `process-recordings.py` elimina graves muy bajos, comprime suavemente los transitorios, ajusta el pico a −3 dBFS y aplica fundidos de 2 y 15 ms. Conserva el tono, la duración y el ritmo de los golpes. La carpeta de originales está excluida de la importación de Godot.

El barajado procede de [Playing Card Sounds, de Brian MacIntosh (BMacZero)](https://opengameart.org/content/playing-card-sounds), archivo `shuffle.wav`, publicado bajo CC0. El original está en `sources/card-shuffle-bmaczero.wav` y su procedencia en `sources/CARD-SHUFFLE-LICENSE.txt`. El procesado conserva su tono, duración y ritmo, con limpieza de graves, compresión suave, ajuste de nivel y fundidos breves.

## Regenerar

Desde la raíz, con g++, Python 3 y ffmpeg instalados:

```bash
bash tools/audio/build.sh
godot --headless --editor --path . --import
python3 tools/audio/check.py
```

El script compila el sintetizador en un directorio temporal, genera los másteres, codifica las pistas y elimina sus temporales. Reconstruye el dado y el barajado desde los originales CC0 incluidos. Conserva los archivos de importación existentes. No requiere red, servicios de pago, soundfonts ni paquetes de Python.

La integración, las reglas para agregar temas de otros Relikarios y los controles de volumen están documentados en [Música y efectos](../../docs/MUSIC.md).

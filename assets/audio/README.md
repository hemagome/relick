# Audio de Relick

Las composiciones instrumentales se sintetizan específicamente para este proyecto mediante `tools/audio/score.cpp`, y los efectos de robo, curación, ataque y guardado mediante `tools/audio/render.cpp`, sin muestras ni melodías de terceros. La música es orquestal, al estilo de un RPG: cuerdas, trompetas y trompas, flauta, guitarra y arpa pulsadas, piano, timbales, batería y platillos, en una sala con reverberación. Los efectos de dado y barajado utilizan grabaciones con licencia CC0, detalladas abajo.

| Archivo | Título | Uso |
| --- | --- | --- |
| `music/menu.ogg` | Antes del destino | Menú común: vals en 3/4 a 126 BPM para guitarra, flauta y cuerdas. 68,6 s. |
| `music/yuno-to-combat.ogg` | — | Puente hacia el combate: redoble, arpa y fanfarria sobre la dominante. |
| `music/yuno-combat.ogg` | Apuesta de acero | Duelo contra Yuno: 160 BPM, ostinato de cuerdas en 3+3+2 y tema de trompetas. 72 s. |
| `music/yuno-to-climax.ogg` | — | Puente hacia el clímax: tres golpes y una escala ascendente. |
| `music/yuno-climax.ogg` | La última tirada | Cero vidas contra Yuno: 168 BPM, cadencia descendente y el tema del combate a toda orquesta. 57,1 s. |
| `music/osamu-combat.ogg` | Pulso de acero | Duelo contra Osamu. 144 BPM, cuerdas graves, metales apagados y acentos de percusión metálica. 66,7 s. |
| `music/osamu-climax.ogg` | La última orden | Cero vidas contra Osamu. 174 BPM, el motivo del combate en los metales y cuerdas en semicorcheas. 44,1 s. |
| `music/ironwing-combat.ogg` | Órbita de combate | Duelo contra Ironwing. 156 BPM, sintetizador FM, bajo en octavas y un interludio de flauta. 61,5 s. |
| `music/ironwing-climax.ogg` | Última maniobra | Cero vidas contra Ironwing. 180 BPM, el mismo motivo con notas más cortas, cuerdas y timbales. 48 s. |

Las piezas comparten re como centro tonal. Los clímax retoman el motivo del combate de cada Relikario. En las piezas en bucle, las reverberaciones y las colas de las notas se pliegan sobre el comienzo para mantener continuidad. Los puentes conservan su cola después del último pulso para que suene sobre la pieza siguiente. Los archivos `.import` conservan el tempo, el compás, la longitud musical y la repetición.

| Efecto | Duración | Diseño |
| --- | --- | --- |
| `sfx/card-draw.wav` | 0,22 s | Fricción de papel y desplazamiento breve. |
| `sfx/card-shuffle.wav` | 0,78 s | Cartas barajándose, de Brian MacIntosh (BMacZero). |
| `sfx/dice-roll.wav` | 1,37 s | Grabación real de un dado de madera rodando sobre una mesa. |
| `sfx/heal.wav` | 1,05 s | Campanas ascendentes con cola suave. |
| `sfx/attack.wav` | 0,46 s | Barrido corto y golpe de baja frecuencia. |
| `sfx/deck-saved.wav` | 0,39 s | Frase ascendente de ondas de pulso con un cierre a dos voces. |

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

El script compila los dos generadores en un directorio temporal, genera los másteres, codifica las pistas y elimina sus temporales. Reconstruye el dado y el barajado desde los originales CC0 incluidos. Conserva los archivos de importación existentes. No requiere red, servicios de pago, soundfonts ni paquetes de Python.

La integración, las reglas para agregar temas de otros Relikarios y los controles de volumen están documentados en [Música y efectos](../../docs/MUSIC.md).

# Audio original de Relick

Composiciones instrumentales y efectos sintetizados específicamente para este proyecto mediante `tools/audio/render.cpp`. No se utilizan grabaciones, muestras ni melodías de terceros. Estética de RPG y arcade con piano eléctrico, arpegios, bajos, pads, campanas, percusión y una voz de sintetizador principal.

| Archivo | Título | Uso |
| --- | --- | --- |
| `music/menu.ogg` | Antes del destino | Menú común, piano y arpegios suaves con percusión contenida. |
| `music/yuno-combat.ogg` | Apuesta de acero | Duelo contra Yuno: melodía principal, bajo y batería más activos. |
| `music/yuno-climax.ogg` | La última tirada | Cero vidas contra Yuno: octavas, contrarritmos y arpegios más densos. |

Cada pista dura 48 segundos: 160 BPM, 4/4 y 128 pulsos. Comparten una estructura armónica de ocho compases desarrollada en cuatro secciones. Las reverberaciones y las colas de las notas se envuelven en el bucle para mantener continuidad. Los archivos `.import` conservan el tempo, el compás, la longitud musical y la repetición; deben versionarse junto con los OGG.

| Efecto | Duración | Diseño |
| --- | --- | --- |
| `sfx/card-draw.wav` | 0,22 s | Fricción de papel y desplazamiento breve. |
| `sfx/dice-roll.wav` | 1,42 s | Serie de contactos secos durante el giro y asentamiento del dado. |
| `sfx/heal.wav` | 1,05 s | Campanas ascendentes con cola suave. |
| `sfx/attack.wav` | 0,46 s | Barrido corto y golpe de baja frecuencia. |

Los másteres son estéreo a 44,1 kHz. Las piezas se distribuyen en OGG Vorbis y los efectos en WAV PCM de 16 bits. Los generadores usan una semilla fija para que los recursos sean reproducibles.

## Regenerar

Desde la raíz, con g++, Python 3 y ffmpeg instalados:

```bash
bash tools/audio/build.sh
godot --headless --editor --path . --import
python3 tools/audio/check.py
```

El script compila el sintetizador en un directorio temporal, genera los másteres, codifica las pistas y elimina sus temporales. Conserva los archivos de importación existentes. No requiere red, servicios de pago, soundfonts ni paquetes de Python.

La integración, las reglas para agregar temas de otros Relikarios y los controles de volumen están documentados en [Música y efectos](../../docs/MUSIC.md).

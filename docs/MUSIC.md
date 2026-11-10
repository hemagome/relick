# Música adaptativa y efectos

El autoload `MusicDirector` conserva un solo `AudioStreamPlayer` durante toda la sesión. Usa `AudioStreamInteractive` y el bus independiente `Music`. Las tres composiciones originales están incluidas y asignadas en `assets/audio/music.tres`; los cinco efectos están en `assets/audio/sfx/`. Véase [la procedencia y reproducción de los recursos](../assets/audio/README.md).

## Estados

| Clip | Música | Entrada |
| --- | --- | --- |
| 0 · Menu | Menús y espera | Inicio, salas, selección de habilidad y mulligan. También es la entrada para futuras pantallas de título y deck builder. |
| Combat:relikario | Duelo | Primera presentación del estado de duelo después de la preparación, incluyendo reconexión. Se mantiene durante robo, acciones, respuestas y turnos. |
| Climax:relikario | Momento crítico | Cualquiera de los dos Relikarios alcanza cero vidas. Se mantiene aunque se cure y durante el resultado. |

Volver efectivamente al lobby restablece `Menu` y permite un nuevo combate sin heredar el clímax. Abrir ventanas, inspeccionar cartas, cambiar de turno, reconstruir el tablero y desconectarse temporalmente no reinician la reproducción.

`DuelPlayerView.HasReachedZero` expone el indicador público que ya conserva el motor. Permite recuperar el clímax si se abre de nuevo el juego después de que un Relikario llegara a cero y se curara. Es un campo JSON opcional: las vistas anteriores siguen siendo compatibles. Para recuperar ese caso entre sesiones hace falta publicar también el servidor con la proyección actualizada; con un servidor anterior, el cliente detecta el cero actual y lo recuerda durante esa sesión.

## Música del rival

`DuelPlayerView.RelikarioId` identifica públicamente al personaje de cada jugador. El cliente consulta exclusivamente `Players[1 - MySeat].RelikarioId` para escoger los temas de combate y clímax. El personaje propio y quién llegue a cero no cambian esa selección. `yuno-lv2` es un alias musical de `yuno`, de modo que evolucionar conserva su identidad y el reloj musical.

El catálogo actual solo contiene Yuno y su evolución; se incluyen sus dos temas y el menú compartido. Las vistas de un servidor anterior que no envíe identidad usan `yuno` como valor compatible. Un personaje nuevo sin música asignada utiliza `DefaultEnemyTheme`, configurado como Yuno.

Para añadir otro Relikario, agrega dos clips en `music.tres` llamados exactamente `Combat:<id>` y `Climax:<id>`, con sus archivos correspondientes. Los índices son libres; el director busca por nombre. Las variantes pueden declararse en `ThemeAliases`, en el inspector del autoload. No hace falta cambiar el director para agregar un nuevo par de pistas.

## Transiciones

Las tres piezas comparten 160 BPM, 4/4, 128 pulsos, 32 compases, 48 segundos y una cuadrícula armónica en re menor. Cada arreglo mantiene esa posición y aumenta la instrumentación e intensidad. La regla general de la tabla contiene todas las conexiones, incluido el salto directo menú → clímax al recuperar un duelo crítico:

- Salida: **Next Bar**, al siguiente compás de la pista de origen.
- Entrada: **Same Position**, la misma posición musical en la pista destino.
- Mezcla: **Cross**, fundido de cuatro pulsos de la pista de origen.

Godot programa la transición en el hilo de audio. El cliente solicita la fase; no calcula compases con `_Process`, temporizadores o la latencia de red. Pedir otra vez la misma fase no vuelve a ejecutar `Play()` ni repite la transición.

La espera hasta el siguiente compás dura como máximo aproximadamente 1,5 segundos, seguida de un fundido de 1,5 segundos. El combate continúa mientras se completa la transición. El silencio del bus tampoco detiene el reloj: subir el volumen conserva la reproducción.

Las futuras pistas que usen la regla general deben conservar tempo, duración y cuadrícula armónica. Al importarlas activa **Loop**, **BPM = 160**, **Beat Count = 128** y **Bar Beats = 4**, con el primer pulso al comienzo del archivo. Si su composición necesita otra estructura, configura reglas explícitas y un puente musical para sus conexiones.

`AudioStreamInteractive` también admite clips **filler** para puentes compuestos específicamente. Esos clips usan nombres distintos a los de las fases y reglas de transición explícitas; no requieren cambios en las reglas del duelo.

Para una futura pantalla de menú:

```csharp
GetNode<MusicDirector>("/root/MusicDirector").ShowMenu();
```

La arena llama a `PresentDuel(RoomCode, snapshot)` al presentar un estado autorizado. La música nunca modifica vidas, decisiones, red ni el ritmo de las animaciones.

## Efectos y volumen

`SoundDirector` mantiene cinco reproductores con hasta cuatro voces simultáneas por efecto, en el bus `Sfx`:

- Robo: al comenzar a retirar cada carta de la cola de vuelos, también si su destino está fuera de pantalla. Incluye reparto inicial, mulligan y robos de ambos jugadores.
- Barajado: grabación CC0 de Brian MacIntosh (BMacZero), de 0,78 segundos. Suena una sola vez al retirar un evento `Shuffle` de la cola de animación, también si el mazo queda fuera de pantalla.
- Dado: grabación real de un dado de madera, publicada por Wuzzy bajo CC0. `DiceFace.RollStarted` la reproduce al empezar cada lanzamiento o relanzamiento, incluidos los dos dados de iniciativa. Dura 1,37 segundos y termina dentro de los 1,45 segundos de la animación. Ajustar el resultado no vuelve a lanzar el sonido.
- Curación: al comenzar la animación de una curación efectiva.
- Ataque: al comenzar la animación del ataque ejecutado.

Las vistas de recuperación y los cambios de diseño no reproducen efectos históricos: reutilizan el filtro existente de revisiones y las colas de animación.

El botón **Sonido** de la sala y **Menú → Sonido** del duelo abren el mismo diálogo persistente, independiente de la reconstrucción del tablero. Cada canal tiene un control de 0–100 % y un interruptor **Silenciar**. El interruptor conserva el porcentaje: al desactivarlo vuelve a escucharse al volumen anterior. Se puede ajustar el nivel mientras está silenciado para preparar el volumen de regreso. El volumen cero también silencia el canal. Los valores iniciales son 65 % para música y 80 % para efectos, con ambos interruptores apagados. `AudioPreferences` aplica cada cambio al instante y guarda niveles y silencios en `user://audio.cfg`, con escritura diferida mientras se arrastra y guardado inmediato al confirmar o salir. Las configuraciones anteriores que solo contienen volúmenes siguen siendo compatibles. El bus Master tiene un limitador para las superposiciones.

Al cerrar la ventana, `QuitWithAudio` guarda las preferencias, libera las voces y los recursos y permite procesar su eliminación antes de terminar Godot. La salida tarda unos 150 ms y no añade confirmaciones. Las pruebas de interfaz y audio usan esa misma salida.

## Verificación

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
dotnet build Relick.csproj
python3 tools/audio/check.py
godot --headless --log-file /tmp/relick-audio-test.log --path . -- --audio-test --audio-settings-path=/tmp/relick-audio-test.cfg
```

La prueba no requiere servidor. Valida los archivos y su información musical, selección por enemigo en ambos asientos, evolución, clímax, curación, recuperación, peticiones rápidas, efectos junto a sus animaciones, controles, persistencia y cuatro tamaños. Usa un tono acelerado en `tests/fixtures/music-clock.ogg` para algunas comprobaciones del reloj; ese tono no se asigna al juego. Los resultados correctos son `MUSIC_PASS`, `SFX_PASS` y `AUDIO_SETTINGS_PASS`.

Para capturas renderizadas añade `--audio-screenshot-dir=/tmp/relick-audio-ui` y ejecuta sin `--headless`. La comprobación de archivos valida duración, niveles, extremos de los efectos y continuidad de los bucles; la apreciación musical de los arreglos queda abierta a ajustes al escucharlos.

Referencia: [AudioStreamInteractive de Godot](https://docs.godotengine.org/en/stable/classes/class_audiostreaminteractive.html).

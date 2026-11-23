# Música adaptativa y efectos

El autoload `MusicDirector` conserva un solo `AudioStreamPlayer` durante toda la sesión. Usa `AudioStreamInteractive` y el bus independiente `Music`. El menú, los temas de cada Relikario y los puentes de Yuno se asignan en `assets/audio/music.tres`. Los efectos están en `assets/audio/sfx/`. Véase [la procedencia y reproducción de los recursos](../assets/audio/README.md).

## Estados

| Clip | Música | Entrada |
| --- | --- | --- |
| 0 · Menu | Menús y espera | Inicio, búsqueda y salas. También es la entrada para futuras pantallas de título y deck builder. |
| Combat:relikario | Duelo | Al abrirse la arena con la selección de habilidad inicial, incluyendo reconexión. Se mantiene durante mulligan, robo, acciones, respuestas y turnos. |
| Climax:relikario | Momento crítico | Cualquiera de los dos Relikarios alcanza cero vidas. Se mantiene aunque se cure y durante el resultado. |

Volver efectivamente al lobby restablece `Menu` y permite un nuevo combate sin heredar el clímax. Abrir ventanas, inspeccionar cartas, cambiar de turno, reconstruir el tablero y desconectarse temporalmente no reinician la reproducción.

`DuelPlayerView.HasReachedZero` expone el indicador público que ya conserva el motor. Permite recuperar el clímax si se abre de nuevo el juego después de que un Relikario llegara a cero y se curara. Es un campo JSON opcional: las vistas anteriores siguen siendo compatibles. Para recuperar ese caso entre sesiones hace falta publicar también el servidor con la proyección actualizada; con un servidor anterior, el cliente detecta el cero actual y lo recuerda durante esa sesión.

## Música del rival

`DuelPlayerView.RelikarioId` identifica públicamente al personaje de cada jugador. El cliente consulta exclusivamente `Players[1 - MySeat].RelikarioId` para escoger los temas de combate y clímax. El personaje propio y quién llegue a cero no cambian esa selección. `yuno-lv2` es un alias musical de `yuno`, de modo que evolucionar conserva su identidad y el reloj musical.

Las dos formas de Osamu comparten `Combat:osamu` y `Climax:osamu`. La preparación usa los Relikarios del reparto para elegir tema desde la selección de habilidad. Las vistas anteriores sin identidad y los personajes sin música asignada usan `DefaultEnemyTheme`, configurado como Yuno.

Para añadir otro Relikario, agrega dos clips en `music.tres` llamados exactamente `Combat:<id>` y `Climax:<id>`, con sus archivos correspondientes. Los índices son libres; el director busca por nombre. Las variantes pueden declararse en `ThemeAliases`, en el inspector del autoload. No hace falta cambiar el director para agregar un nuevo par de pistas. Los puentes `ToCombat:<id>` y `ToClimax:<id>` son opcionales: sin ellos se aplica el fundido general.

## Transiciones

Cada pieza tiene su propio compás y tempo. Los temas de Yuno empiezan con un puente de dos compases sobre la dominante. En los combates de Osamu, Ironwing, Yuno oro y WD, el clímax entra al siguiente compás con un fundido cruzado de dos pulsos.

| Pieza | Tempo y compás | Duración |
| --- | --- | --- |
| Menu | 126 BPM, 3/4, 144 pulsos | 68,6 s |
| Combat:yuno | 160 BPM, 4/4, 192 pulsos | 72 s |
| Climax:yuno | 168 BPM, 4/4, 160 pulsos | 57,1 s |
| ToCombat:yuno | 160 BPM, 8 pulsos y cola | 3 s + cola |
| ToClimax:yuno | 168 BPM, 8 pulsos y cola | 2,9 s + cola |
| Combat:osamu | 144 BPM, 4/4, 160 pulsos | 66,7 s |
| Climax:osamu | 174 BPM, 4/4, 128 pulsos | 44,1 s |
| Combat:ironwing | 156 BPM, 4/4, 160 pulsos | 61,5 s |
| Climax:ironwing | 180 BPM, 4/4, 144 pulsos | 48 s |
| Combat:yuno-oro | 168 BPM, 4/4, 192 pulsos | 68,6 s |
| Climax:yuno-oro | 176 BPM, 4/4, 160 pulsos | 54,5 s |
| Combat:wd | 152 BPM, 4/4, 160 pulsos | 63,2 s |
| Climax:wd | 184 BPM, 4/4, 160 pulsos | 52,2 s |

| Cambio | Salida | Puente | Llegada |
| --- | --- | --- | --- |
| Menú → combate | Siguiente pulso; el menú se apaga en tres pulsos | `ToCombat`: redoble de timbal, arpa y fanfarria ascendente | Primer pulso del combate, sin fundido |
| Combate → clímax | Siguiente compás; el combate se apaga en un pulso | `ToClimax`: tres golpes y una escala hacia la tónica | Primer pulso del clímax, sin fundido |
| Menú → clímax (recuperación) | Siguiente pulso | `ToClimax` | Primer pulso del clímax |
| Cualquier otro, incluida la vuelta al menú | Siguiente pulso | — | Comienzo de la pieza, fundido cruzado de cuatro pulsos |

`MusicDirector` solo pide el puente cuando la fase sube (menú < combate < clímax) y existe un clip `To<nombre del destino>` sin bucle cuyo avance automático apunta a esa pieza. El puente es un clip normal con **Auto Advance** hacia su destino y una regla puente → destino con salida **End**, llegada **Start** y fundido **Disabled**. No se usa el *filler clip* de la tabla: en Godot 4.7 hace entrar el destino con un fundido de un segundo, que borra el primer golpe.

Los puentes se importan sin bucle, con **BPM** y **Beat Count = 8**. Godot programa el destino al terminar esos ocho pulsos, pero un OGG sin bucle sigue sonando hasta el final del archivo: la cola de reverberación del puente se mezcla sobre el comienzo de la pieza. En `music.tres` cada puente debe tener un índice mayor que el de su destino para que la entrada sea exacta a la muestra.

Godot programa las transiciones en el hilo de audio. El cliente solicita la fase; no calcula compases con `_Process`, temporizadores o la latencia de red. Pedir otra vez la misma fase no vuelve a ejecutar `Play()` ni repite la transición. Si la fase cambia durante un puente, el destino pendiente se cancela y se aplica la regla del nuevo cambio.

Los puentes de Yuno agregan tres segundos al combate y 2,9 s al clímax. Osamu entra al combate en el siguiente pulso y pasa del combate al clímax en el siguiente compás. El duelo y el reloj del audio siguen avanzando durante los cambios y cuando el bus está silenciado.

Las piezas en bucle se importan con **Loop**, su **BPM**, **Beat Count** y **Bar Beats**, con el primer pulso al comienzo del archivo y la cola del final plegada sobre el principio.

Para una futura pantalla de menú:

```csharp
GetNode<MusicDirector>("/root/MusicDirector").ShowMenu();
```

La arena llama a `PresentDuel(RoomCode, snapshot)` al presentar un estado autorizado. La música nunca modifica vidas, decisiones, red ni el ritmo de las animaciones.

## Efectos y volumen

`SoundDirector` reproduce los efectos en el bus `Sfx`, con hasta cuatro voces simultáneas por efecto.

- Robo: al comenzar a retirar cada carta de la cola de vuelos, también si su destino está fuera de pantalla. Incluye reparto inicial, mulligan y robos de ambos jugadores.
- Barajado: grabación CC0 de Brian MacIntosh (BMacZero), de 0,78 segundos. Suena una sola vez al retirar un evento `Shuffle` de la cola de animación, también si el mazo queda fuera de pantalla.
- Dado: grabación real de un dado de madera, publicada por Wuzzy bajo CC0. `DiceFace.RollStarted` la reproduce al empezar cada lanzamiento o relanzamiento, incluidos los dos dados de iniciativa. Dura 1,37 segundos y termina dentro de los 1,45 segundos de la animación. Ajustar el resultado no vuelve a lanzar el sonido.
- Curación: al comenzar la animación de una curación efectiva.
- Ataque: al comenzar la animación del ataque ejecutado.
- Guardado de mazo. Suena una vez cuando el servidor confirma que se creó o se actualizó el mazo, incluidos los borradores.

Las vistas de recuperación y los cambios de diseño no reproducen efectos históricos: reutilizan el filtro existente de revisiones y las colas de animación.

La sección **Sonido** de Ajustes y el parlante junto a la bandera blanca y la rueda dentada del duelo muestran los mismos controles persistentes (el del duelo, en un diálogo), independientes de la reconstrucción del tablero. Cada canal tiene un control de 0–100 % y un botón con un icono de parlante para silenciarlo y reactivarlo. Cuando el canal está silenciado o el volumen está en cero, el parlante aparece tachado y la barra y el porcentaje se muestran en gris. El botón conserva el porcentaje y se puede ajustar el nivel mientras el canal está silenciado. Los valores iniciales son 65 % para música y 80 % para efectos, con ambos canales activos. `AudioPreferences` aplica cada cambio al instante y guarda niveles y silencios en `user://audio.cfg`, con escritura diferida mientras se arrastra y guardado inmediato al confirmar o salir. Las configuraciones anteriores que solo contienen volúmenes siguen siendo compatibles. El bus Master tiene un limitador para las superposiciones.

Al cerrar la ventana, `QuitWithAudio` guarda las preferencias, libera las voces y los recursos y permite procesar su eliminación antes de terminar Godot. La salida tarda unos 150 ms y no añade confirmaciones. Las pruebas de interfaz y audio usan esa misma salida.

## Verificación

```bash
dotnet run --project tests/Relick.Core.Tests.csproj
dotnet build Relick.csproj
python3 tools/audio/check.py
godot --headless --log-file /tmp/relick-audio-test.log --path . -- --audio-test --audio-settings-path=/tmp/relick-audio-test.cfg
```

La prueba no requiere servidor. Valida los archivos y su información musical, el combate desde la selección de habilidad, el paso por ambos puentes, selección por enemigo en ambos asientos, evolución, clímax, curación, recuperación, peticiones rápidas, efectos junto a sus animaciones, controles, persistencia y cuatro tamaños. Usa un tono acelerado en `tests/fixtures/music-clock.ogg` para algunas comprobaciones del reloj; ese tono no se asigna al juego. Los resultados correctos son `MUSIC_PASS`, `SFX_PASS` y `AUDIO_SETTINGS_PASS`.

Para capturas renderizadas añade `--audio-screenshot-dir=/tmp/relick-audio-ui` y ejecuta sin `--headless`. La comprobación de archivos valida duración, niveles, extremos de los efectos y continuidad de los bucles; la apreciación musical de los arreglos queda abierta a ajustes al escucharlos.

Referencia: [AudioStreamInteractive de Godot](https://docs.godotengine.org/en/stable/classes/class_audiostreaminteractive.html).

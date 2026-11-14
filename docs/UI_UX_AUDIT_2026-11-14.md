# Auditoría de UI/UX de escritorio — Relick TCG

Fecha: 14 de noviembre de 2026 · Rama `main` en `765f8ed` · Godot 4.7.2 .NET (C#), renderizador GL Compatibility.

Alcance: experiencia de **escritorio (Windows/Linux, ratón y teclado)** dentro del duelo PvP y, de forma secundaria, la sala de duelos. No se evalúa móvil. En esta fase no se modificó código ni recursos del juego. Solo se añadieron este documento y las capturas locales de `artifacts/ui-audit-2026-11-14/`, que están en `.gitignore` y no se versionan.

**Estado de implementación.** Ya están implementadas las tres oportunidades de mayor retorno:

- UX-01: aviso de rechazo en el inspector.
- UX-08: estado con texto e icono en la barra de fase y hueco estable para el botón de avance.
- UX-03: halo de carta jugable.
- UX-19: tabla de ataque.

Del paquete de lectura del tablero también están implementados:

- UX-02: contadores de mano junto a su abanico. El rival, bajo el borde superior.
- UX-07: golpe final y segunda caída.
- UX-06: ranuras vacías discretas y siguiente ranura enseñable.

Se describen en `docs/UI.md` («Estado de la decisión…») y se comprueban con `--feedback-only` y `--board-reading-only`. Los demás hallazgos siguen pendientes.

Sustituye como referencia vigente a `docs/UI_EVALUATION_2026-11-09.md`. Varios de sus hallazgos ya están resueltos: contador de acciones, inspector lateral, historial, guía de bloqueos y tablero unificado. Este informe parte del estado actual.

---

## 0. Metodología y nivel de evidencia

### Fuentes

- Código: `scripts/` (arena, presentación, widgets, animaciones), `network/LobbyConnection.cs`, `core/` (motor, proyección `DuelSnapshot`) y `project.godot`.
- Documentación: `README.md`, `docs/UI.md`, `docs/PVP.md` y `docs/UI_EVALUATION_2026-11-09.md`.
- Reglas: manual oficial v6.0 (sitio de Relick): cuatro etapas (Robo, Combate, Acción, Final), dos acciones por turno, D6 de ataque (1-3 fallo, 4 reducido, 5 normal, 6 crítico), premio en PR, velocidades RAM con resolución inversa, mano máxima de siete y la regla de **vida 0** (la primera vez sigues jugando; el siguiente daño te derrota).
- Ejecución real del cliente con los arneses del proyecto:
  - `godot --path . -- --pvp-ui-test --board-preview-dir=…` (reposo, carta seleccionada, Relikia, Disquete) a 1820×1024 y 1440×1024 lógicos.
  - Una **copia en el scratchpad** con el mismo arnés ampliado a 2280×1024 (panorámico), 1638×1024 (16:10), vista desde el asiento rival y vida 0. El repositorio no se tocó.
  - `--guidance-only`, `--history-only`, `--closure-only`, `--connection-only`, `--stage-only`, `--combo-responses-only` y `--actions-only` con `--ui-screenshot-dir`.
  - `dotnet build Relick.csproj`: correcto.

### Marcas usadas en los hallazgos

- **[V] Verificado**: observado en captura renderizada o comprobado leyendo el camino exacto del código.
- **[I] Inferido**: riesgo deducido por inspección estática, sin reproducirlo.
- **[NV] No verificado**: no se pudo comprobar en este entorno.

### Limitaciones (importante)

- **No se jugó una partida real entre dos clientes.** La prueba de dos procesos (`--pvp-input-quick`, `docs/PVP.md`) necesita un servidor SpacetimeDB local y autenticación, que no se levantaron. Las perspectivas de ambos asientos se evaluaron con los fixtures del arnés (`DuelSnapshot.For(game, seat)`), que son coherentes entre sí. Latencia, rechazos reales del servidor y reconexión real son **[NV]**.
- Los fixtures muestran «Sin conexión» y botones deshabilitados por diseño del arnés. **No son defectos.**
- Resoluciones físicas: el arnés fija `ContentScaleSize = tamaño de ventana`. Por eso 1280×720 se **simuló reduciendo** la captura de 1820×1024, que es el lienzo lógico que el juego usa en cualquier ventana 16:9 (`ArenaViewport.Configure`, línea 36). Es una aproximación fiel del tamaño físico de los textos, aunque Godot rasteriza las fuentes a resolución final. 3440×1440 se representó con el lienzo lógico de 2280×1024 que produce la fórmula. 2560×1440 y 3840×2160 usan el mismo lienzo lógico que 1920×1080 (1820×1024), así que su **composición es idéntica** y solo cambia la nitidez. Esto último es **[NV]**.
- La captura de vida 0 (`06-…`) se tomó a mitad de la animación de la barra. Es válida para la composición, pero no para el color final de la barra.
- No se evaluó el audio.

---

## I. Resumen ejecutivo

### Evaluación general

Relick tiene un **MVP funcional sólido y bien defendido técnicamente**. El servidor es autoritativo, las órdenes son idempotentes, la privacidad está cuidada, la recuperación tras reconexión funciona sin repetir efectos y hay muchas pruebas de interfaz. El tablero de escritorio ya representa todas las zonas reales de Relick en una sola pantalla, con un inspector lateral de calidad.

La distancia con un TCG digital pulido está en cuatro frentes:

1. **Feedback de errores y estados.** Un rechazo o una jugada obsoleta apenas se comunica.
2. **Señalización de lo jugable.** No se sabe qué cartas de la mano se pueden usar sin pulsarlas una a una.
3. **Jerarquía y tamaño de texto.** Las ranuras vacías dominan y las etiquetas son ilegibles a 1280×720.
4. **Ritmo.** El dado y los vuelos no se pueden omitir y no hay atajos de teclado.

**Puntuación global ponderada: 6,2 / 10** (detalle en la sección II).

### Cinco fortalezas principales

1. **Inspector lateral con lectura en paralelo** (`CardShowcase` + `CardInspector`). Al pasar el ratón por cualquier carta pública se muestra la carta grande, su tipo, nombre, efecto en texto de interfaz y coste, sin tapar el tablero. Es el equivalente directo a la lectura contextual de Runeterra o Master Duel.
2. **Acciones contextuales ancladas a la carta** (`OnlineArena.CardActions.cs`). Se selecciona la carta y aparecen sus jugadas autorizadas junto a ella, con icono y etiqueta corta. Así se evita la lista global de jugadas.
3. **Robustez multijugador visible.** Hay un aviso central único de «Sin conexión» con Reconectar (`ConnectionNoticeOverlay`), los envíos se bloquean mientras hay una jugada pendiente y una revisión nueva no reproduce efectos antiguos.
4. **Representación fiel de las reglas propias.** El tablero 2×2 de habilidades con costes 0/300/500/1000 PR, el marco Bonus/Área a caballo del divisor, el canje, la Memoria, las Corruptas, la Destreza, el control COMBO «preguntar/pasar» y la barra de etapas con las cuatro etapas del manual muestran que la UI se diseñó desde Relick y no desde otro TCG.
5. **Sistema visual centralizado** (`RelickVisuals`, `RelickPlaymat`, `State.*`): selección dorada, objetivo cian con escuadras de forma (no solo color), foco blanco y tapete por sombreador. Es una base adecuada para temas futuros.

### Cinco problemas más importantes

1. **[V] Los rechazos y errores de jugada son casi invisibles.** «La partida cambió…», los errores del reductor y «No se pudo comprobar la jugada» solo aparecen en una etiqueta gris de 12 px, recortada, junto al logotipo (UX-01).
2. **[V] El contador de la mano rival se dibuja dentro de la zona Corruptas del rival** (1820 y 2280 lógicos), y el contador propio queda en la esquina, lejos de la mano (UX-02).
3. **[V] No se distinguen las cartas jugables en la mano.** Solo se descubre si una carta tiene jugada pulsándola (UX-03).
4. **[V] Jerarquía invertida y texto diminuto.** Las ranuras vacías de habilidad tienen el texto más grande del tablero. Las etiquetas de zona miden entre 8 y 9 unidades lógicas, unos 5,6–6,3 px físicos a 1280×720. Los nombres de las cartas de la mano quedan bajo el borde de la pantalla en reposo (UX-04, UX-05, UX-06).
5. **[V] En escritorio, quién tiene el turno o la decisión se indica solo con color.** La barra de fase dice «TURNO 01» sin «TÚ/RIVAL» ni «DECIDES TÚ», a diferencia de la composición compacta. Además, el estado «Golpe final» (vida 0) no se rotula en escritorio (UX-07, UX-08).

### Tres oportunidades de mayor retorno

1. **Un canal único de feedback de estado en el riel y la barra de fase.** Un indicador de estado («TU TURNO / TU RESPUESTA / TURNO RIVAL / RESOLVIENDO / ENVIANDO / RECHAZADA») y un aviso en línea para rechazos. Esfuerzo bajo-medio. Resuelve UX-01 y UX-08 y mejora la lectura de todos los flujos.
2. **Halo de «jugable» en la mano y nombre siempre visible**, calculados con las opciones autorizadas del snapshot, sin duplicar reglas. Esfuerzo bajo. Ahorra clics de exploración en cada turno.
3. **Previsualización del resultado del ataque** al elegir habilidad: «1-3 fallo · 4 → X · 5 → Y · 6 → Z crítico · premio +N PR». Se calcula con datos públicos de la habilidad y su copia (`NormalDamageAt`, `CriticalDamageAt`). Es la ayuda de decisión más específica de Relick y no revela información oculta.

### Diagnóstico de madurez

| Capa | Estado |
|---|---|
| Funcionalidad de juego en la UI | **MVP completo**: todas las fases, respuestas, elecciones especiales y cierre son jugables. |
| Robustez de red en la UI | **Por encima del MVP**: idempotencia, recuperación y bloqueo durante animaciones o envío. |
| Claridad de decisión | **MVP con fricción**: buena lectura de cartas, pero señalización pobre de lo jugable, del turno y de los errores. |
| Pulido visual | **Pre-pulido**: identidad coherente; jerarquía y tipografía sin ajustar a resoluciones reales. |
| Ritmo y ergonomía | **MVP**: sin atajos, sin omitir animaciones, sin ajustes visuales. |

---

## II. Matriz de evaluación

| Dim. | Peso | Nota | Hallazgos clave | Severidad máx. | Referentes |
|---|---:|---:|---|---|---|
| A. Legibilidad y jerarquía | 20 % | **6,0** | Inspector excelente; ranuras vacías dominantes; etiquetas de 8–9 u; nombres de la mano ocultos; contador rival mal ubicado | P0 (UX-02) | Runeterra, Snap |
| B. Interacción y respuesta | 15 % | **6,5** | Selección y acciones contextuales claras; objetivos con latido y escuadras; sin halo de jugable; vista previa bloqueada con selección activa; clic derecho solo en habilidades | P1 | Hearthstone, Runeterra |
| C. Presentación del tablero | 15 % | **6,0** | Todas las zonas reales y simetría correcta; espacio desaprovechado en panorámico; contadores de mano desplazados; marcador de Área no implementado con mucho peso visual | P0 (UX-02) | Gwent, Runeterra |
| D. Animaciones y efectos | 5 % | **6,5** | Coherentes, derivadas de eventos autorizados y separadas del estado (`ImpactLayer`); dado de 2,35 s no omitible; vuelos secuenciales; banner en cada etapa de ambos jugadores | P1 | Master Duel, Hearthstone |
| E. Información contextual | 15 % | **7,0** | Motivos de bloqueo (`ArenaGuidance`), historial público, cadena de resolución en texto; falta previsualización de daño, pila visual y «Golpe final» | P1 | Runeterra, MTG Arena |
| F. Fluidez y ritmo | 10 % | **6,0** | Ataque en 2–3 clics, avance separado e invitado; sin atajos; animaciones bloqueantes; posible confirmación de dado sin alternativas | P1 | Snap, Pocket |
| G. Estados y multijugador | 10 % | **6,5** | Aviso de desconexión ejemplar; bloqueo durante envío; rechazos invisibles; turno rival poco visible; sin temporizador de turno (función ausente) | P0 (UX-01) | Master Duel, MTG Arena |
| H. Accesibilidad y ergonomía | 5 % | **4,5** | Foco blanco y escuadras de forma; textos pequeños; turno indicado por color; sin escala de texto, velocidad ni movimiento reducido dentro del juego; sin atajos | P1 | Runeterra, Hearthstone |
| I. Adaptabilidad a escritorio | 3 % | **5,0** | 1080p correcto; 720p ilegible en etiquetas; panorámico con bandas y riel fijo de 310 u; 4K compone igual que 1080p | P1 | Snap, MTG Arena |
| J. Consistencia visual y técnica | 2 % | **7,0** | Estilos centralizados; muchos tamaños de fuente en línea; reconstrucción completa del árbol en cada revisión; `FindChild` y trabajo por fotograma acotados | P2 | — |

Ponderación: 0,20·6 + 0,15·6,5 + 0,15·6 + 0,05·6,5 + 0,15·7 + 0,10·6 + 0,10·6,5 + 0,05·4,5 + 0,03·5 + 0,02·7 = **6,215 → 6,2 / 10**.

Todas las dimensiones tienen evidencia suficiente. La G tiene un componente **[NV]** (comportamiento con servidor real), que limita su nota a lo observable en código y fixtures.

---

## III. Hallazgos detallados

Formato por hallazgo: Problema · Evidencia · Impacto UX · Referencia · Propuesta · Implementación · Esfuerzo · Impacto · Riesgo · Prioridad. Las capturas están en `artifacts/ui-audit-2026-11-14/`.

### UX-01 · Los rechazos y errores de jugada no llegan al jugador — **P0** [V]

- **Problema.** Cuando una jugada se rechaza, queda obsoleta o falla el envío, el mensaje solo se escribe en `Connection.Message`. En escritorio ese texto se pinta en `_status`, una etiqueta de 12 px, color `Muted`, `ClipText = true`, a la derecha del logotipo (arriba a la izquierda), lejos de la mano y del riel.
- **Evidencia.**
  - `network/LobbyConnection.cs:423` («La partida cambió. Revisa el tablero antes de volver a jugar.»), `:435` («No se pudo comprobar la jugada…») y `:159` (`OnUnhandledReducerError → Message = error.Message`).
  - `scripts/OnlineArena.cs:295-297` crea `_status`; `:179` lo actualiza cada fotograma.
  - El riel solo distingue «Enviando tu decisión…» (`OnlineArena.ActionBar.cs:463`) y nunca muestra el rechazo.
- **Impacto UX.** El jugador pulsa, ve «Enviando…», el estado vuelve atrás y no sabe si la jugada se aplicó, se rechazó o debe repetirla. Es ambigüedad directa sobre el estado de la partida.
- **Referencia.** Hearthstone y MTG Arena muestran el rechazo junto al objeto («No puedes…») con un rebote de la carta. Runeterra usa un aviso breve centrado.
- **Propuesta.** Un **aviso en línea de rechazo** en la zona de instrucción del riel (`CardInspector.HintLabel`), con color `State.Danger`, icono y texto corto («Jugada rechazada: la partida cambió. Revisa el tablero.»). Debe permanecer hasta la siguiente interacción o 6 s. Además, un **rebote breve** (sacudida de 0,2 s) de la carta o zona ancla si existía `_cardActionAnchor`. `_status` queda para estados neutros de sala.
- **Implementación.**
  1. En `LobbyConnection`, exponer un evento o propiedad `LastPlayRejection { string Text; ulong Serial; }` que se rellene en los tres caminos citados (y en el rechazo confirmado por el servidor, si existe otro camino de `LastPlayAccepted = false`).
  2. En `OnlineArena._Process`, al cambiar `Serial`, llamar a `_inspector.ShowAlert(text)`, un método nuevo de `CardInspector` que reutilice `HintLabel` con un `StyleBox` de peligro.
  3. Sacudida opcional mediante un `Tween` sobre `Position` de un nodo hijo visual. Nunca sobre el área de pulsación.

  No cambia la lógica de red; solo expone un dato que ya existe.
- **Esfuerzo**: bajo. **Impacto**: alto. **Riesgo**: bajo. Hay que evitar mostrar como «rechazo» los mensajes neutros («Enviando jugada…»): conviene una propiedad separada, no analizar `Message`.

### UX-02 · Contador de mano rival dentro de la zona Corruptas rival; contador propio lejos de la mano — **P0** [V]

- **Problema.** En lienzos de 1820 y 2280 u, el icono de cartas con «5» de la mano rival cae dentro del recuadro «CORRUPTAS» del rival, que tiene su propio badge «0». El contador propio («6») queda en la esquina inferior izquierda, a unos 500 u de la mano.
- **Evidencia.**
  - Capturas `01-reposo-1820x1024.png`, `03-turno-rival-1820x1024.png` y `05-panoramico-2280x1024.png`.
  - `scripts/OnlineArena.cs:316-319`: la fila de la mano rival ocupa `x = 340 … 1100` de diseño, todo el ancho central, con el contador como primer hijo. `:505-510`: el contador se encoge al final vertical (`ShrinkEnd`) y la `HandFan` se expande, así que el contador queda en el borde izquierdo de la fila, justo sobre la columna de Memoria/Corruptas del campo rival girado (`ArenaPresentation.Field`, `x = 996` reflejado).
  - `scripts/ArenaPresentation.cs:470-478` repite el patrón en la mano propia: contador a la izquierda, abanico expandido y compensador de 44 u.
- **Impacto UX.** Atribución errónea de información pública: se puede leer «5 corruptas». La relación contador↔mano se pierde.
- **Referencia.** Runeterra y Hearthstone ponen el número de cartas de la mano rival pegado al abanico o como badge sobre la última carta.
- **Propuesta.** El contador forma parte del abanico: es un badge pegado al **extremo derecho del abanico** (o centrado bajo él en el rival y sobre él en el propio), nunca dentro de un rectángulo de zona.
- **Implementación.** En `ArenaPresentation.Hand` (rama `desktop && title == null`) y `OnlineArena.BuildOpponentHand`, sustituir la `HBoxContainer` de ancho completo por un `HBoxContainer` con `Alignment = Center` y `SizeFlagsHorizontal = ShrinkCenter` alrededor de `[HandFan, contador]`. Otra opción: que `HandFan` exponga el rectángulo ocupado y colocar el badge en `canvas.Place` con ese rectángulo. Añadir una aserción en `OnlineArena.VisualTests.cs`: el rectángulo global de `OpponentHandCounter` no se cruza con `CorruptedMemoryButton1` ni con `MemoryButton1` a 1440, 1638, 1820 y 2280 u.
- **Esfuerzo**: bajo. **Impacto**: alto. **Riesgo**: bajo (la prueba de mano que cuelga, `HandFitsScreen`, debe seguir pasando).

### UX-03 · No se señalan las cartas jugables de la mano — **P1** [V]

- **Problema.** `HandCard` no tiene estado «jugable». En el turno propio, todas las cartas se ven iguales. La única forma de saber si una tiene jugada es seleccionarla y leer el riel (`ArenaGuidance.UnavailableCard`).
- **Evidencia.** `scripts/BoardWidgets.cs:816-966` (propiedades de `HandCard`: `Chosen`, `Mulligan`, `OriginalOwner`, sin `Playable`). `OnlineArena.UpdateEnabled` (`OnlineArena.cs:565-600`) solo deshabilita todo en bloque durante una animación o un envío.
- **Impacto UX.** Exploración a ciegas en cada turno, sobre todo con siete cartas. En ventanas de respuesta (COMBO, Error de Carga) cuesta ver qué carta puede responder.
- **Referencia.** Hearthstone (borde verde en lo jugable) y Runeterra (brillo en las cartas con coste pagable).
- **Propuesta.** Halo menta y punto o icono discreto en las cartas con al menos una opción autorizada. Las demás quedan en un 85 % de brillo **solo durante tu decisión**. En ventanas de respuesta, las cartas COMBO que pueden responder llevan el mismo halo con el icono de mano alzada del control COMBO.
- **Implementación.**
  - `var playable = s.Options.Where(o => IsHandAction(o.Move.Kind) || o.Move.Kind == DuelMoveKind.PlayCode).Select(o => o.Move.Index).ToHashSet();` calculado en `BuildMoves()`, una vez por revisión.
  - Nueva propiedad `HandCard.Playable` y un nuevo estado en `RelickVisuals.Interaction`, de baja intensidad y sin latido, para no competir con `Target`.
  - Comprobar en `ArenaActionText`/`IsHandAction` qué tipos llevan `Move.Index` de mano; respuestas como `PlayCode` en `ErrorResponse` usan índice de mano (verificar).
- **Esfuerzo**: bajo. **Impacto**: alto. **Riesgo**: bajo. No duplica reglas: lee las opciones del servidor. Riesgo de saturación visual si se combina con el latido de objetivos; por eso, sin animación.

### UX-04 · Los nombres de las cartas de la mano no se ven en reposo — **P1** [V]

- **Problema.** La mano de escritorio cuelga del borde inferior: `DesktopHandVisible = 138` u de una carta de 193. Las cartas son escaneos impresos con el nombre en la franja inferior, que queda fuera de la pantalla. Solo se ven el arte y el rótulo superior de tipo.
- **Evidencia.** `scripts/ArenaPresentation.cs:567-570`; capturas `01`, `03` y `04`.
- **Impacto UX.** Las cartas se identifican por memoria visual del arte. Dos Ataques o variantes con arte parecido obligan a pasar el ratón por cada una.
- **Referencia.** Runeterra y Marvel Snap superponen en la mano el coste y el nombre como texto de interfaz.
- **Propuesta.** Una **placa de nombre en texto de interfaz** en la franja visible superior de cada carta de la mano: nombre abreviado con elipsis, 13–14 u, fondo oscuro translúcido. Se oculta al elevar la carta, porque entonces se ve el nombre impreso. Alternativa descartada: subir la mano en reposo, porque recorta el campo propio (la mano ya se superpone a la última fila).
- **Implementación.** En `HandCard._Draw`, si `Lifts && _hover < 0.5f`, dibujar `BoardArt.Plate` + texto en `y + 6` con `Card.Name`. Medir con `GetStringSize` y recortar.
- **Esfuerzo**: bajo. **Impacto**: medio-alto. **Riesgo**: bajo (tapa algo de arte; el texto debe ser legible a una escala de 0,7).

### UX-05 · Texto ilegible en portátiles (1280×720, 1366×768) — **P1** [V]

- **Problema.** En escritorio el lienzo lógico siempre mide 1024 de alto (`ArenaViewport.Configure`, `:36`). A 720 px físicos la escala es 0,70. Muchos textos del tablero miden entre 8 y 12 u: rótulos de zona «CANJE 1» y «EQUIPO 0/1» (9 u, `BoardWidgets.cs:731`), «SOBRE RELIKARIO» (8 u, `:797`), badges ×N y USADA (9 u, `:116`), subtítulo «Yuno Gyambura · Lv 1» (11 u, `ArenaPresentation.cs:114`) y estados de la barra de fase (12 u, `:242-243`). Eso equivale a 5,6–8,4 px físicos.
- **Evidencia.** Captura `04-simulacion-fisica-1280x720.png`: los rótulos de zona son ilegibles; vida, PR y el riel siguen legibles. En el código hay unas 20 llamadas a `Text(…, 12)`, 9 de 9 u, 8 de 11 u, 5 de 10 u y 3 de 8 u.
- **Impacto UX.** En los portátiles más comunes, las zonas solo se reconocen por posición. La legibilidad es la dimensión de mayor peso.
- **Referencia.** Runeterra fija un tamaño mínimo de texto de interfaz y prescinde de rótulos en zonas obvias.
- **Propuesta.**
  1. Escala tipográfica con mínimo de **13 u** para cualquier texto del tablero (≥ 9 px a 720p) y 15 u para valores.
  2. Quitar los rótulos que la forma ya comunica (por ejemplo, «CANJE 1/2» se sustituye por el icono ⇄ ya dibujado) y mantener solo los necesarios.
  3. Más adelante (P2): ajuste «Tamaño de texto» (100/115/130 %) que multiplique la escala tipográfica.
- **Implementación.** Crear `RelickVisuals.Type` con constantes (`Caption = 13`, `Body = 15`, `Value = 18`, `Title = 22`) y sustituir los literales en `BoardWidgets.cs`, `ArenaPresentation.cs` y `OnlineArena*.cs`. Para el ajuste del usuario: un factor global leído de `user://` y aplicado en `Type`.
- **Esfuerzo**: medio (muchos literales, cambios mecánicos). **Impacto**: alto. **Riesgo**: medio. Algunos textos deben recolocarse; validar con la matriz de capturas en 1440/1638/1820/2280.

### UX-06 · Jerarquía invertida: las ranuras vacías dominan el tablero — **P1** [V]

- **Problema.** Cada ranura vacía de habilidad dibuja su coste a 30 u en fuente Display dorada al 62 % (`BoardWidgets.cs:517-520`). Son los textos más grandes del tablero después del número de vidas (32 u), por encima del nombre del jugador (17 u) y de cualquier valor de estado. Con las ocho ranuras vacías del inicio, las cifras «0 / 300 / 500 / 1000 PR» compiten con todo.
- **Evidencia.** Capturas `01`, `03` y `05`.
- **Impacto UX.** La vista no va a lo importante (vidas, acciones, cartas ocupadas, decisión pendiente).
- **Referencia.** Runeterra y Gwent: zonas vacías apenas sugeridas; contenido y objetivos destacados.
- **Propuesta.** Coste de la ranura vacía a 18 u y 35 % de opacidad. **Resaltar solo la siguiente ranura enseñable**, con borde menta y coste opaco, cuando exista una opción `Learn` autorizada. Así el coste es información útil y no decoración.
- **Implementación.** En `SkillTile._Draw`, añadir la propiedad `NextLesson` y fijarla en `ArenaPresentation.Field` cuando `slot == primera posición libre && s.Options.Any(Learn)`. Hay que pasar ese dato desde `OnlineArena.Prepare`.
- **Esfuerzo**: bajo. **Impacto**: medio-alto. **Riesgo**: bajo.

### UX-07 · El estado «Golpe final» (vida 0) no se rotula en escritorio — **P1** [V]

- **Problema.** Según el manual, al llegar a 0 vidas por primera vez se sigue jugando y el siguiente daño derrota. `LifeMeter` rotula «GOLPE FINAL» solo si `!Stacked` (`BoardWidgets.cs:448`), pero en escritorio todos los medidores son `Stacked = true` (`OnlineArena.cs:223`).
- **Evidencia.** Código citado y captura `06-vida-cero-1638x1024.png` (solo «0/8»; la barra estaba a mitad de animación).
- **Impacto UX.** Es la información más decisiva del duelo (para ti y para el rival), propia de Relick. Hoy depende de conocer la regla y de interpretar un «0».
- **Referencia.** Runeterra y Hearthstone marcan el estado letal con un tratamiento persistente y textual.
- **Propuesta.** En la variante apilada: badge «GOLPE FINAL» bajo el número, contorno rojo del corazón con un latido lento (desactivado con movimiento reducido) y tooltip «Con 0 vidas sigues en juego; el próximo daño te derrota». Debe aparecer en ambos campos.
- **Implementación.** `LifeMeter._Draw`, rama `Stacked`, con texto de 12–13 u sobre la barra. Hay que confirmar la semántica exacta en el motor antes de redactar el tooltip (`GameEngine`: daño con vida 0 y curación tras llegar a 0).
- **Esfuerzo**: bajo. **Impacto**: alto. **Riesgo**: bajo.

### UX-08 · El turno y la decisión se indican solo con color; el turno rival se ve poco — **P1** [V]

- **Problema.**
  - En escritorio, la barra de fase recibe `barDetail = "TURNO 01"` (`OnlineArena.cs:323-324`). La composición compacta sí añade «· TÚ/RIVAL» y «· DECIDES TÚ / DECIDE EL RIVAL» (`:236-239`).
  - En escritorio, el dueño del turno solo se deduce del color menta o coral de la barra y del tapete, y del «»» antes del nombre.
  - Durante el turno rival, la única indicación textual es «Esperando la decisión del rival.» en el riel, a 15 u.
  - En respuestas (turno rival, decisión propia), la barra conserva el color del rival.
- **Evidencia.** Código citado; capturas `03-turno-rival-1820x1024.png` y `10-banner-etapa-rival-1440x1024.png` (el banner de etapa sí dice «TURNO 01 / RIVAL», pero dura 0,95 s).
- **Impacto UX.** Dependencia exclusiva del color (accesibilidad). La diferencia entre «turno del rival» y «me toca responder» no se lee en la barra, que es el sitio donde se mira.
- **Referencia.** MTG Arena (indicador de prioridad y de quién debe actuar) y Master Duel (indicador de fase con dueño explícito).
- **Propuesta.** Una **ficha de estado** en la celda `StageDetail` de la barra, que cambia de texto, icono y color:

  | Estado | Condición (ya disponible en el snapshot o en la conexión) | Texto |
  |---|---|---|
  | Tu turno | `CurrentPlayer == MySeat && DecisionPlayer == MySeat` | **TU TURNO · T01** |
  | Tu respuesta | `CurrentPlayer != MySeat && DecisionPlayer == MySeat` | **RESPONDE** (ámbar) |
  | Turno rival | `DecisionPlayer != MySeat` | **TURNO RIVAL · esperando** |
  | Resolviendo | `IsAnimating` | **RESOLVIENDO…** |
  | Enviando | `Connection.PlayPending` | **ENVIANDO…** |

  La celda mide hoy 150 u fijas; puede subir a unos 190 u reduciendo el ancho del botón de avance o los anchos de las etapas.
- **Implementación.** Calcular el estado en `OnlineArena._Process`, junto a `RefreshRuntimeGuidance`, y actualizar solo el `Label` existente (`StageDetail`), sin reconstruir. Añadir icono (`GameIcon.Hand` para responder) para no depender del color.
- **Esfuerzo**: bajo. **Impacto**: alto. **Riesgo**: bajo (la prueba `--stage-only` comprueba nombres de nodos; conservarlos).

### UX-09 · Animaciones bloqueantes que no se pueden omitir ni acelerar — **P1** [V]

- **Problema.**
  - El dado tarda `RollSeconds = 1.45` + `ResultSeconds = 0.9` = **2,35 s** (`DiceFace.cs:8-9`) y forma parte de `IsAnimating` (`OnlineArena.cs:50`), que bloquea todas las entradas (`UpdateEnabled`, `SelectCard`, `SelectField`).
  - Los vuelos de cartas son secuenciales, de 0,45 a 0,72 s cada uno (`OnlineArena.Animations.cs:8, 105, 252`).
  - El movimiento reducido (`relick/accessibility/reduced_motion` o la preferencia del sistema) solo afecta a `ImpactLayer`, `StageBanner` y el retrato. **No acorta el dado ni los vuelos.**
- **Evidencia.** Código citado; `grep` sin coincidencias de `Reduced` en `DiceFace.cs` ni en `OnlineArena.Animations.cs`.
- **Impacto UX.** Cada ataque suma al menos 2,35 s sin control. En partidas largas, las animaciones repetidas ralentizan y cansan, el riesgo que se señala en Master Duel y Pocket.
- **Referencia.** Hearthstone y Snap permiten acelerar con clic. Master Duel permite omitir animaciones de invocación repetidas.
- **Propuesta.**
  1. **Clic o Espacio durante una animación = avanzar hasta el estado final.** Ya existe `ResetAnimations()`, que restaura el estado confirmado sin reproducir efectos.
  2. Ajuste **«Velocidad de animaciones: Normal / Rápida (×0,5) / Mínima»**. Mínima equivale al movimiento reducido: dado sin rodar (cara final con un destello de 0,3 s) y vuelos instantáneos.
  3. El movimiento reducido existente debe aplicar la variante mínima al dado y a los vuelos.

  Omitir es **local**: el estado ya es autoritativo, así que no hay riesgo de desincronización.
- **Implementación.** `OnlineArena._GuiInput` o `_UnhandledInput`: si `IsAnimating` y llega un clic izquierdo o la acción `ui_accept`, se llama a `ResetAnimations(preserveStageAnnouncements: false)`, se fija `_die.Value = _snapshot.Die` y se reproducen los retratos pendientes en su estado final. Hay que verificar con `PlayPendingPortraits` y `ResultSettled` que el resultado final y las vidas no queden a medias. Para la velocidad, multiplicar `Duration`, `RollSeconds` y `ResultSeconds` por un factor de `AudioPreferences`, o mejor de un nuevo `VisualPreferences` autoload que siga el mismo patrón.
- **Esfuerzo**: medio. **Impacto**: alto. **Riesgo**: medio. Las pruebas que miden duración o el orden «dado → impacto → resultado» (`--portraits-only`, `--closure-only`) deben ampliarse con el caso omitido.

### UX-10 · Sin atajos de teclado en el duelo — **P1** [V]

- **Problema.** No hay `_UnhandledInput` ni acciones del `InputMap` en la arena. Solo `ResourceBadge` acepta Intro/Espacio con foco y `ConnectionNoticeOverlay` captura la navegación (`grep` de `InputEventKey` y `IsActionPressed` en `scripts/`). Avanzar de etapa, cancelar una selección o abrir el historial requiere ratón.
- **Impacto UX.** Fricción en acciones de cada turno para usuarios de escritorio.
- **Referencia.** MTG Arena (Espacio para pasar o avanzar, Z para deshacer selección) y Runeterra (Espacio para pasar).
- **Propuesta (solo los atajos con valor real):**

  | Tecla | Acción | Salvaguarda |
  |---|---|---|
  | **Espacio** | Pulsar «Avanzar etapa» | Solo si `AdvanceSuggested` (sin jugadas). Si hay jugadas, exigir **Mayús+Espacio** para evitar avances accidentales. |
  | **Esc** | Limpiar selección, cerrar popup o vista previa | Igual que `ClearContext()`. |
  | **1–7** | Seleccionar carta n de la mano | Igual que un clic en `HandCard`. |
  | **Tab / Mayús+Tab** | Recorrer objetivos válidos (`_pulsingTargets`) | Solo en elección de objetivo. |
  | **Intro** | Botón primario del riel o de la carta | Igual que un clic. |
  | **H** | Historial | — |
  | **Clic o Espacio en animación** | Omitir (UX-09) | — |
- **Implementación.** Definir acciones en `project.godot` (`[input]`: `relick_advance`, `relick_cancel`, `relick_card_1..7`, `relick_history`) y crear un nuevo `OnlineArena.Shortcuts.cs` con `_UnhandledInput`. Aplicar los mismos guardas que `UpdateEnabled` (`!Connection.Ready || PlayPending || IsAnimating`) y no actuar si un `Window` está abierto o un `LineEdit` tiene foco. Mostrar el atajo en los tooltips («Finalizar turno · Espacio»).
- **Esfuerzo**: bajo-medio. **Impacto**: medio-alto. **Riesgo**: bajo (conflicto con el foco de botones: Godot ya usa `ui_accept` sobre el botón enfocado; consumir el evento solo si nadie lo gestionó).

### UX-11 · Cada revisión reconstruye el árbol completo: se pierden el foco y el hover; redimensionar cancela animaciones — **P2** [V/I]

- **Problema.** `Render()` elimina y recrea `DuelInterface` en cada revisión aceptada (`OnlineArena.cs:209-219`). `Resized → QueueLayout → ApplyLayout` hace `ResetAnimations(); Render();` (`:123, :132`).
- **Impacto UX.**
  - [V] Cambiar el tamaño de la ventana durante un ataque corta la animación en curso.
  - [I] El foco de teclado se pierde tras cada jugada (el nodo enfocado se libera), lo que anula la navegación por teclado.
  - [I] La vista previa por hover no se reactiva hasta mover el ratón.
- **Propuesta.**
  - Corto plazo (P1, bajo): guardar `GetViewport().GuiGetFocusOwner()?.Name` antes de `Render()` y restaurarlo por nombre después. Tras reconstruir, reevaluar el hover (`Viewport.UpdateMouseCursorState()` o reenviar la última posición del ratón). En `ApplyLayout`, no reiniciar animaciones si solo cambia el tamaño: recolocar con `BoardCanvas.Arrange`, que ya escala por sí solo.
  - Largo plazo (P2-P3, alto): actualización incremental de las zonas del tablero (propiedades y `QueueRedraw`) en lugar de reconstrucción. Solo si el perfilado lo justifica; no hay medición disponible.
- **Esfuerzo**: bajo (parte corta) / alto (incremental). **Impacto**: medio. **Riesgo**: medio en la parte larga (muchas pruebas dependen de `BoardBuildCount`).

### UX-12 · Inspección inconsistente: bloqueada con selección activa y clic derecho solo en habilidades — **P2** [V]

- **Problema.**
  - `BindInspectorPreviews` ignora el hover si `HasContextSelection` (`OnlineArena.ActionBar.cs:304`). Con una carta o un objetivo seleccionado no se puede leer otra carta pasando el ratón, por ejemplo el equipo rival antes de destruirlo.
  - El clic derecho para inspeccionar solo existe en `SkillTile` (`ArenaPresentation.cs:184-187`) y abre un diálogo modal. El resto de cartas usa clic izquierdo, que a la vez selecciona.
- **Impacto UX.** Hay que deshacer la selección para consultar, y el mismo gesto tiene significados distintos según el elemento.
- **Referencia.** Runeterra: hover con lectura siempre disponible y clic derecho o mantener pulsado para ampliar, de forma uniforme.
- **Propuesta.**
  - El hover **siempre** actualiza el inspector como «vista previa temporal», con una cabecera discreta «Vista previa». Al salir, vuelve la carta seleccionada; las acciones siguen ligadas a la selección.
  - Clic derecho en **cualquier** carta visible (mano, zonas, memoria superior, carta pública de la mano rival) abre la ampliación sin seleccionar.
- **Implementación.**
  - Quitar `HasContextSelection` del guarda de `MouseEntered` y, en `RefreshInspector`, priorizar `_inspectorPreview` sobre `SelectedCardId()` **sin** tocar `_selectionCost` ni los botones. En `MouseExited`, devolver `_inspectorPreview = null`.
  - Clic derecho: un manejador genérico en `BindInspectorPreviews` que llame a `ShowCards(card.Name, [card.Id])`.
- **Esfuerzo**: bajo. **Impacto**: medio. **Riesgo**: bajo-medio. Las pruebas `--inspector-only` dan por hecho el comportamiento actual; hay que actualizarlas.

### UX-13 · El panel de resolución repite texto y no muestra la pila — **P2** [V]

- **Problema.**
  - La cabecera «RESOLUCIÓN DE COMBATE» se ve siempre, también cuando no se resuelve nada (captura `01`: cabecera sin contenido debajo).
  - En la captura `08` se lee «Ana · Disquete» y justo debajo «Ana · Disquete · Elige una carta…».
  - La cadena de respuestas (velocidades RAM, orden inverso) se cuenta en prosa (captura `09`) y no como pila numerada.
- **Evidencia.** `OnlineArena.History.cs:86-104` (`BuildRailResolution`); capturas `01`, `08` y `09`.
- **Impacto UX.** Leer una cadena con Error de Carga y contraerror exige interpretar frases. El manual define que la secuencia se resuelve en orden inverso, pero la UI no lo hace visible.
- **Referencia.** MTG Arena (pila visual con la carta superior primero) y Master Duel (cadena numerada).
- **Propuesta.**
  - Ocultar la cabecera cuando no hay resolución.
  - Pila vertical de entradas `[n] miniatura · jugador (color + TÚ/RIVAL) · carta · velocidad RAM`, con la cima arriba, un rótulo «Se resuelve primero ↑», la entrada en curso resaltada y las anuladas tachadas.
  - Eliminar la línea duplicada (prefijo jugador · carta).
- **Implementación.** Reutilizar los datos que ya consume `BuildResolution`. Hay que verificar si el snapshot expone la cadena estructurada; si solo hay texto, conviene una proyección estructurada en `DuelProjection` (cambio de proyección, no de reglas, justificado por legibilidad).
- **Esfuerzo**: medio. **Impacto**: medio-alto en partidas con COMBO. **Riesgo**: medio (privacidad: solo identidades ya autorizadas).

### UX-14 · Panorámico y 16:10: espacio desaprovechado, bandas y riel fijo — **P2** [V]

- **Problema.**
  - El ancho lógico se limita a 1440–2280 u y la altura está fija en 1024 (`ArenaViewport.cs:36`). `BoardCanvas.Unit = min(ancho/1440, alto/1024)` (`BoardCanvas.cs:53`) nunca supera 1, y el riel mide 310 u fijos (`OnlineArena.cs:354`).
  - En 21:9 (lienzo de 2280 u) el tablero queda centrado, con unas 250 u de tapete vacío a cada lado (captura `05`).
  - Con `window/stretch/aspect = "keep"` (`project.godot`), un monitor de 3440×1440 (2,39:1 > 2280/1024 = 2,23) muestra además **bandas negras laterales** de unos 117 px físicos. 32:9 lo agrava.
  - En 16:10 (1638 u) el tablero se reduce al 90 % (captura `06`).
- **Impacto UX.** Se desperdicia el espacio que precisamente sirve para leer mejor.
- **Referencia.** MTG Arena aprovecha el ancho con paneles laterales; Snap es un contraejemplo (deja vacío el espacio en PC).
- **Propuesta.**
  - Que el **riel crezca** con el ancho sobrante: `clamp(310, 0,22·W − 0, 480)` u. Eso permite una carta más grande en el inspector e historial más legible.
  - Cambiar `aspect` a `expand` para eliminar las bandas, y subir el límite de 2280 a ~2560 si el riel absorbe el exceso.
  - Opcional: en el lado izquierdo libre de panorámico, mostrar un resumen persistente de la Memoria (últimas cartas).
- **Implementación.** `OnlineArena.Render` (desktop): `rail.CustomMinimumSize = new(RailWidth(Size.X), 0)`; `ArenaViewport.Configure`; `project.godot [display] window/stretch/aspect`. Comprobar `PresentPlaymat` (`board_right`) y el sombreador del tapete.
- **Esfuerzo**: medio. **Impacto**: medio (alto para usuarios de panorámico). **Riesgo**: medio (comprobar el modo compacto, que también usa `ArenaViewport`).

### UX-15 · El resultado del duelo es poco prominente — **P2** [V]

- **Problema.** Victoria, derrota o empate se presentan en un panel del riel de 310 u (`OnlineArena.Result.cs:15-53`) y en la celda de la barra. No hay un momento central de cierre.
- **Impacto UX.** El final de partida, el momento emocional clave, se queda en un lateral. Sí se espera a que terminen el dado y el impacto final, y eso está bien hecho.
- **Referencia.** Hearthstone y Master Duel (pantalla de resultado centrada y breve, con acceso a revisar el tablero).
- **Propuesta.** Una superposición central (no bloqueante tras 1,5 s) con el título, la causa pública (`EndReason`), vidas y PR finales, y los botones «Volver al lobby», «Revisar tablero» e «Historial». El panel del riel se mantiene como versión persistente tras «Revisar». La revancha **no** es requisito del MVP; está ausente.
- **Esfuerzo**: bajo-medio. **Impacto**: medio. **Riesgo**: bajo (ver `--closure-only`).

### UX-16 · Menú de engranaje con una sola opción y sin ajustes visuales — **P2** [V]

- **Problema.** Durante el duelo, el menú solo ofrece «Historial de jugadas» (`OnlineArena.Targets.cs:30-33`), que en escritorio ya se muestra en el riel. No hay ajustes de tamaño de texto, velocidad de animación, movimiento reducido ni atajos. El sonido tiene su propio icono.
- **Propuesta.** Convertir el engranaje en un diálogo «Ajustes» (patrón de `AudioSettingsDialog`) con Sonido, **Tamaño de texto** (UX-05), **Velocidad de animaciones** y **Reducir movimiento** (UX-09), y una **lista de atajos** (UX-10). Persistir en `user://` como `AudioPreferences`.
- **Esfuerzo**: medio. **Impacto**: medio. **Riesgo**: bajo.

### UX-17 · Ilustraciones de carta sin mipmaps y muy reducidas — **P2** [I]

- **Problema.** Las ilustraciones miden ~1054×1492 (`file assets/cards/*-clean.png`) y su importación tiene `mipmaps/generate=false` (`assets/cards/attack-clean.png.import:26`). En el tablero se muestran a ~138×193 u, unos 97×136 px a 720p: una reducción de ×7–×11 sin mipmaps.
- **Impacto UX.** [I] Aliasing o centelleo en el texto impreso y en las tramas de las cartas, sobre todo durante los vuelos y los zooms del hover. En las capturas se aprecia cierto «crujido», no concluyente.
- **Propuesta.** Activar mipmaps en `assets/cards/*.import` y usar `TextureFilter = LinearWithMipmaps` en los nodos que dibujan cartas (`HandCard`, `ZoneCardButton`, `SkillTile` o su `CanvasItem` padre). Comparar capturas antes y después a 720p y 1080p, y medir el uso de VRAM.
- **Esfuerzo**: bajo. **Impacto**: medio. **Riesgo**: bajo-medio (memoria +33 % por textura; tiempo de importación).

### UX-18 · Mensajes de bloqueo con reglas replicadas en el cliente — **P2** [I]

- **Problema.** `ArenaGuidance.UnavailableCard` (`scripts/ArenaGuidance.cs`) reconstruye por qué una carta no es jugable: fase, PR, acciones, turno 1, descarte… Si una carta nueva o una regla cambian en `core/`, el mensaje puede contradecir al servidor. Por ejemplo, el caso por defecto dice «Solo puedes enseñar una habilidad por turno».
- **Impacto UX.** [I] Un motivo de bloqueo erróneo es peor que ninguno.
- **Propuesta.**
  - Corto plazo: pruebas en `tests/` que, para cada razón, construyan un estado del motor y comprueben que el motor **también** rechaza la jugada (y por esa causa si expone el motivo, como `AttackUnavailableReason`).
  - Medio plazo, opcional: que la proyección incluya `BlockReason` por carta desde el motor. Es un cambio de proyección justificado solo si aparecen discrepancias.
- **Esfuerzo**: medio. **Impacto**: medio. **Riesgo**: bajo.

### UX-19 · Previsualización del resultado del ataque ausente — **P1** [V] (oportunidad)

- **Problema.** Al elegir habilidad para atacar, el riel muestra la carta y el coste («Consume 1 Ataque»), pero no lo que puede pasar. `SkillTile` sí dibuja «NORMAL x · CRÍTICO y» cuando hay aumentos (`BoardWidgets.cs:535-539`), pero no el reducido ni el premio.
- **Impacto UX.** La decisión central del combate (con qué habilidad atacar) se toma calculando de memoria.
- **Referencia.** Runeterra (previsualización de combate) y MTG Arena (daño previsto).
- **Propuesta.** En el inspector, al resaltar una habilidad objetivo durante `_choosingAttackSkill` (y en el hover de una habilidad propia en tu Combate), una tabla:

  ```
  D6   1–3      4             5           6
       fallo    reducido 2    normal 3    crítico 5   · premio +200 PR
  Lv 2: +1 al dado (excepto 1)
  ```

  Reducido = ⌈normal/2⌉ según el manual. Se calcula con `NormalDamageAt(counters)` y `CriticalDamageAt(counters)` de la copia autorizada. Mostrar «antes de respuestas y equipos» para no prometer resultados que dependen de información oculta. Magnet («1D6») muestra su propia fila.
- **Implementación.** Nuevo bloque en `CardInspector.Present` cuando `card.Kind == Skill` y existe `counters`. Reutilizar `GameEngine.DamageFor(die, normal, critical)` (estático y público, `core/GameEngine.cs:198`) para no duplicar la fórmula.
- **Esfuerzo**: bajo-medio. **Impacto**: alto. **Riesgo**: bajo. Hay que verificar los efectos que alteran el daño (Estrella del Fontanero ×2: indicar «×2 activo» si `StarEffects > 0`).

### UX-20 · Banners de etapa en cada etapa de ambos jugadores — **P3** [V]

- **Problema.** `StageBanner` (0,95 s, `StageBanner.cs:31`) se anuncia en Robo, Combate, Acciones y Final de cada turno, también en los del rival (captura `10`). No bloquea la entrada, pero tapa el centro del tablero.
- **Propuesta.** En el turno rival, solo el banner de inicio de turno (o una versión reducida en la barra). En el turno propio, Robo/inicio y Combate. Hacer que dependa del ajuste de velocidad (UX-09).
- **Esfuerzo**: bajo. **Impacto**: bajo-medio. **Riesgo**: bajo (`--stage-only`).

### UX-21 · Coste del inspector alejado del nombre; Relikia sin motivo de deshabilitado — **P3** [V]

- **Problema.**
  - En el riel alto, la fila «Coste · 1 acción · 1 descarte» queda anclada al pie, a ~700 u del nombre (captura `02`).
  - En el menú de Relikia (captura `07`), los cuatro efectos aparecen atenuados sin explicar por qué (0 PR). Solo el riel, al pasar el ratón, muestra «500 PR · 1 acción».
- **Propuesta.**
  - Colocar el coste inmediatamente bajo tipo y nombre.
  - En el menú de Relikia, anexar el motivo a las entradas deshabilitadas: «Robar el Bonus · 500 PR (tienes 0)». Coste y PR son públicos.
- **Esfuerzo**: bajo. **Impacto**: bajo-medio. **Riesgo**: bajo.

### UX-22 · Marcador de Área sin función — **P3** [V] (funcionalidad ausente)

- **Problema.** `AreaZone` (`ArenaPresentation.cs:96`) reserva una ranura con el mismo peso visual que Bonus, pero el estado del juego aún no incluye áreas (`docs/UI.md`). El manual sí define el Área activa, así que la ranura es correcta a futuro.
- **Propuesta.** Mantener la ranura con un contraste menor (`Socket` con `strength = 0.5`) y el tooltip «Área · próximamente». No es un error de UX, sino una función pendiente.
- **Esfuerzo**: bajo. **Impacto**: bajo. **Riesgo**: nulo.

### UX-23 · Confirmación del dado posiblemente redundante — **P2** [I]

- **Problema.** Tras `Attack`, el motor pasa a `Phase.AttackRerollChoice` (`core/GameEngine.cs:192`). Si la única opción es conservar el dado (sin Destreza, PC, Colgante ni COMBO), el jugador puede tener que pulsar una confirmación sin alternativas (`ArenaGuidance`: «Confirma el resultado del dado.»). **Hay que verificar** si `ProcessAttackDie` lo resuelve solo en ese caso.
- **Propuesta.** Si se confirma la redundancia: preferencia opcional «Confirmar automáticamente el dado sin modificadores» en el cliente. Cuando `Options` = {`AttackDie` sin `Flag`, `Concede`}, se envía al terminar la presentación del dado. La contrapartida es una posible fuga de información por tiempo, la misma ya aceptada para COMBO «pasar» en `docs/UI.md`. Por eso debe ser opcional.
- **Esfuerzo**: bajo. **Impacto**: medio. **Riesgo**: bajo-medio.

### UX-24 · Prueba de conexión frágil en modo gráfico — **P2** (validación) [V]

- **Problema.** `--connection-only` con `--ui-screenshot-dir` en ejecución gráfica falla con «No se abre el menú antes del corte.» (`scripts/OnlineLobby.ConnectionTests.cs:55`). En headless pasa (`PVP_CONNECTION_ONLY_PASS`). Parece una dependencia del tiempo de apertura del popup con un compositor real (Wayland).
- **Impacto.** No es un defecto de producto observado, pero resta confianza a la matriz gráfica que se propone en VIII.
- **Propuesta.** Esperar la señal `about_to_popup` o varios fotogramas antes de la comprobación.
- **Esfuerzo**: bajo. **Riesgo**: nulo.

### Funcionalidades ausentes (no son errores de UX)

- Temporizador de turno o reloj y gestión de inactividad del rival (no hay rastro en `core/`).
- Revancha tras el duelo.
- Áreas de juego.
- Intercambio del banquillo entre duelos de un encuentro (`README.md`).
- Tutorial o introducción guiada (señalado en la evaluación del 9-11).
- Arrastrar y soltar cartas. Es una decisión de diseño válida, ver IV.
- Desempate por dados de «Relick Virtual» (el manual lo menciona; el juego registra empate). Es una cuestión de reglas, fuera del alcance de la UI.

---

## IV. Comparativa contra referentes

| Referente | Idea aplicable a Relick | Hallazgo o propuesta | No adoptar, y por qué |
|---|---|---|---|
| **Legends of Runeterra** | Lectura por hover siempre disponible, previsualización de combate, nombre y coste superpuestos en la mano, indicador de estado claro | UX-03, UX-04, UX-08, UX-12, UX-19 | Multitud de indicadores de palabras clave sobre cada unidad: Relick ya tiene cartas impresas densas. Su menú de colección complejo. |
| **Gwent** | Tablero con identidad y zonas legibles; jerarquía entre zona vacía y ocupada | UX-06, UX-22 | Cartas animadas en el tablero: coste alto y ruido sobre escaneos impresos. Habilidades poco evidentes: Relick debe seguir mostrando el texto en el riel. |
| **Hearthstone** | Halo de jugable, rebote al rechazar, acelerar animaciones con clic, objetos táctiles | UX-01, UX-03, UX-09 | Ocultar información técnica: Relick necesita mostrar contadores, PR, RAM y cadena. Arrastrar como único gesto (ver abajo). |
| **Duel Masters Play's** | Limpieza del tablero y zonas vacías discretas | UX-06 | Live2D y presentación por encima de la claridad: no prioritario. |
| **Yu-Gi-Oh! Master Duel** | Cadena numerada, panel lateral de texto, omitir animaciones repetidas | UX-09, UX-13, UX-20 | Animaciones largas de invocación y texto denso en pantalla completa. |
| **Shadowverse: Worlds Beyond** | Presentación moderna de la carta jugada | (P3) un destello breve de «carta jugada» en el centro, opcional | Alta densidad de efectos simultáneos. |
| **MTG Arena** | Prioridad y «quién debe actuar», pila visual, atajos (Espacio), ajustes de detalle | UX-08, UX-10, UX-13, UX-16 | Exceso de pasos y paradas de prioridad: Relick ya resuelve esto con el control COMBO «pasar». No añadir paradas por fase. |
| **Marvel Snap** | Partidas ágiles, números grandes y claros | UX-05, UX-09 | Desaprovechar el espacio en PC (UX-14 propone lo contrario). |
| **Pokémon TCG Pocket** | Presentación de cartas y colección | Inspector actual, ya alineado | Animaciones largas y pasos de confirmación extra (ver UX-23). |
| **The Bazaar** | Feedback audiovisual por interacción | UX-01 (rebote), sonido de rechazo | Acumulación de sistemas visuales simultáneos. |

### Disyuntiva: arrastrar frente a seleccionar y confirmar

- **Arrastrar** (Hearthstone): es rápido, pero en Relick una carta puede tener varias jugadas (Jugar, Equipar, Canjear, Activar desde Bonus), descartes como coste y elección de habilidad. El destino del arrastre sería ambiguo.
- **Seleccionar y luego acción contextual** (estado actual): es explícito, cabe cualquier número de opciones y ya está probado.
- **Recomendación**: mantener la selección. Más adelante (P3), como atajo: arrastrar una carta sobre una habilidad iluminada ejecuta el **ataque** y arrastrar a la zona de canje ejecuta **Canjear**, solo cuando la opción sea única. No es requisito.

---

## V. Propuestas de rediseño

### V.1 Barra de fase con estado explícito (UX-08)

**Estado actual:** `[TURNO 01] ROBO ✓ » COMBATE  ACCIONES  FINAL [⚡ Ir a acciones ›]`, con color menta o coral.

**Estado propuesto:**

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ● TU TURNO · T01 │ ROBO ✓  » COMBATE  ACCIONES  FINAL │ [⚡ Ir a acciones ›] │
└───────────────────────────────────────────────────────────────────────────┘
  ✋ RESPONDE · T01   (ámbar, cuando decides en turno rival)
  ⧗ TURNO RIVAL      (coral, sin botón de avance; la barra conserva su ancho)
  ⟳ RESOLVIENDO…     (durante IsAnimating)   ⇡ ENVIANDO…   ⚠ JUGADA RECHAZADA (3 s)
```

- **Comportamiento**: se actualiza por fotograma sin reconstruir. El texto y el icono no dependen del color. El botón de avance conserva su posición: reservar su hueco también en el turno rival, porque hoy desaparece y la barra se encoge (captura `03`).
- **Cambios técnicos**: `ArenaPresentation.PhaseBar` (celda `StageDetail` de 150 a ~190 u, icono a la izquierda y un hueco fijo para el avance) y `OnlineArena._Process` (cálculo del estado).
- **Impacto**: comprensión inmediata de «¿me toca?».

### V.2 Riel con aviso de rechazo y pila de resolución (UX-01, UX-13, UX-21)

```
┌ Riel (310–480 u) ───────────────────┐
│ [COMBO ✋] [⚑] [🔊] [⚙]              │
│ ┌ RESOLUCIÓN · se resuelve ↑ ─────┐ │  ← solo visible si hay cadena
│ │ 2 ▸ RIVAL Error de Carga  32 RAM│ │
│ │ 1   TÚ   <Código COMBO>   16 RAM│ │  ← ejemplo
│ └─────────────────────────────────┘ │
│ ┌ ⚠ Jugada rechazada: la partida ─┐ │  ← aviso en línea (Danger)
│ │   cambió. Revisa el tablero.    │ │
│ └─────────────────────────────────┘ │
│ [carta grande]                      │
│ Código Objeto · COMBO               │
│ Estrella del Fontanero              │
│ Coste: 1 acción · 1 descarte        │  ← junto al nombre
│ Descarta 1 carta para activar…      │
│ ─────────────────────────────────── │
│ HISTORIAL …                         │
└─────────────────────────────────────┘
```

### V.3 Mano con nombre y estado jugable (UX-03, UX-04, UX-02)

```
           ┌────────┐┌────────┐┌────────┐┌────────┐
 franja    │Ataque  ││Destiny ││Estrella││PC 8BITS│  ⧉ 6   ← contador pegado al abanico
 visible   │  ▓▓▓▓  ││  ▓▓▓▓  ││  ▓▓▓▓  ││  ▓▓▓▓  │
           └═mint═══┘└────────┘└═mint═══┘└────────┘
             jugable  (atenuada)  jugable
```

### V.4 Inspector de habilidad con previsualización de ataque (UX-19)

```
<Habilidad de ejemplo> · tu copia (1 aumento)   — valores ilustrativos
D6  1–3 fallo │ 4 reducido 2 │ 5 normal 3 │ 6 crítico 5
Premio +200 PR · Lv 2: +1 al dado salvo 1 · antes de respuestas
```

### V.5 Escritorio panorámico (UX-14)

```
21:9 actual:  |vacío 250|   TABLERO 1440   |vacío 250| riel 310 |  + bandas negras
21:9 propuesto: |vacío ~120| TABLERO 1440 |vacío ~120|  riel 480  |   sin bandas (aspect=expand)
```

---

## VI. Roadmap de implementación

### Etapa 1 — Fundamentos (legibilidad, críticos, claridad del tablero)

| Orden | Tarea | Depende de | Reutiliza |
|---|---|---|---|
| 1.1 | UX-02 contador de mano pegado al abanico | — | `HandCounter`, `HandFan` |
| 1.2 | UX-01 aviso de rechazo en el riel | — | `CardInspector.HintLabel`, `State.Danger` |
| 1.3 | UX-07 rótulo «Golpe final» en el medidor apilado | — | `LifeMeter` |
| 1.4 | UX-08 indicador de estado en la barra de fase | 1.2 (comparte el estado «rechazada») | `StageDetail` |
| 1.5 | `RelickVisuals.Type` (escala tipográfica) | — | — |
| 1.6 | UX-05 mínimos de 13 u y retirada de rótulos redundantes | 1.5 | — |
| 1.7 | UX-06 ranuras vacías discretas y «siguiente enseñable» | 1.5 | `SkillTile` |
| 1.8 | UX-24 estabilizar la prueba gráfica de conexión | — | — |

### Etapa 2 — Interacción (selección, feedback, contexto, turnos)

| Orden | Tarea | Depende de |
|---|---|---|
| 2.1 | UX-03 halo de jugable en la mano | — |
| 2.2 | UX-04 placa de nombre en la mano | 1.5 |
| 2.3 | UX-12 hover siempre disponible y clic derecho uniforme | — |
| 2.4 | UX-19 previsualización de ataque | 1.5 |
| 2.5 | UX-10 atajos de teclado | 1.4 (tooltips con atajo) |
| 2.6 | UX-11 (parte corta) restaurar foco y hover tras `Render` | antes de 2.5, para que el teclado sea usable |
| 2.7 | UX-21 coste junto al nombre y motivo en Relikia | — |

### Etapa 3 — Pulido (animaciones, transiciones, consistencia)

| Orden | Tarea | Depende de |
|---|---|---|
| 3.1 | `VisualPreferences` autoload (velocidad, movimiento reducido, tamaño de texto) | patrón de `AudioPreferences` |
| 3.2 | UX-09 omitir con clic o Espacio y velocidad de animación | 3.1, 2.5 |
| 3.3 | UX-20 banners de etapa reducidos | 3.1 |
| 3.4 | UX-13 pila de resolución | — (posible proyección estructurada) |
| 3.5 | UX-15 superposición de resultado | — |
| 3.6 | UX-17 mipmaps en cartas | — |

### Etapa 4 — Experiencia avanzada

| Orden | Tarea | Depende de |
|---|---|---|
| 4.1 | UX-16 diálogo de Ajustes completo (texto, velocidad, movimiento, atajos) | 3.1 |
| 4.2 | UX-14 riel adaptable y `aspect = expand` | 1.6 |
| 4.3 | UX-18 pruebas de coherencia de `UnavailableCard` frente al motor | — |
| 4.4 | UX-23 autoconfirmar dado (si se verifica) | 3.1 |
| 4.5 | UX-11 (parte larga) actualización incremental, solo con perfilado | 2.6 |
| 4.6 | UX-22 Área atenuada; atajo de arrastre opcional | — |

**Componentes reutilizables que conviene crear una sola vez:** `RelickVisuals.Type` (tipografía), `VisualPreferences` (ajustes), `ArenaStatus` (enum calculado una vez por fotograma y consumido por la barra, el riel y los atajos) y un estado `Playable` en `RelickVisuals.Interaction`.

---

## VII. Quick wins (las 10 de mejor relación impacto/esfuerzo)

| # | Mejora | Archivos | Esfuerzo | Criterios de aceptación verificables |
|---|---|---|---|---|
| 1 | Contador de mano pegado al abanico (UX-02) | `ArenaPresentation.cs` (`Hand`), `OnlineArena.cs` (`BuildOpponentHand`), `OnlineArena.VisualTests.cs` | 2–4 h | A 1440, 1638, 1820 y 2280 u, el rectángulo global de `OpponentHandCounter` no se cruza con ningún `MemoryButton*` ni `CorruptedMemoryButton*`; la distancia horizontal de `OwnHandCounter` al borde del abanico es ≤ 24 u. |
| 2 | Aviso de rechazo (UX-01) | `LobbyConnection.cs`, `CardInspector.cs`, `OnlineArena.ActionBar.cs` | 3–5 h | Al simular `Play` con una revisión obsoleta, el riel muestra un texto con estilo de peligro durante ≥ 3 s; `_status` no es el único sitio con el mensaje; nueva prueba `--rejection-only`. |
| 3 | «Golpe final» en escritorio (UX-07) | `BoardWidgets.cs` (`LifeMeter`) | 1–2 h | Con `Health == 0` y `Stacked`, se dibuja «GOLPE FINAL» y el tooltip explica la regla; verificado para ambos asientos en una captura. |
| 4 | Indicador de estado en la barra (UX-08) | `ArenaPresentation.cs` (`PhaseBar`), `OnlineArena.cs` | 3–4 h | `StageDetail` contiene «TU TURNO», «RESPONDE», «TURNO RIVAL», «RESOLVIENDO» o «ENVIANDO» según el estado; no cambia el ancho de la barra entre turnos; `--stage-only` pasa. |
| 5 | Halo de jugable (UX-03) | `BoardWidgets.cs` (`HandCard`), `RelickVisuals.cs`, `OnlineArena.cs` (`BuildMoves`) | 2–4 h | Para un snapshot de prueba con N cartas con opciones, exactamente esas N tienen `Playable = true`; ninguna fuera de la decisión propia. |
| 6 | Placa de nombre en la mano (UX-04) | `BoardWidgets.cs` (`HandCard._Draw`) | 2 h | En reposo, el nombre se dibuja dentro de la franja visible (y < `DesktopHandVisible`); al elevar la carta desaparece. |
| 7 | Ranuras vacías discretas y siguiente enseñable (UX-06) | `BoardWidgets.cs` (`SkillTile`), `ArenaPresentation.cs` | 2–3 h | El coste de una ranura vacía mide ≤ 18 u; solo la primera ranura libre se resalta y solo si existe la opción `Learn`. |
| 8 | Esc para limpiar y Espacio para avanzar con salvaguarda (UX-10, parcial) | `project.godot` (`[input]`), nuevo `OnlineArena.Shortcuts.cs` | 3–4 h | Esc equivale a `ClearContext()`; Espacio solo avanza si `AdvanceSuggested`; Mayús+Espacio avanza siempre que el botón esté habilitado; ninguno actúa con un `Window` visible o durante `PlayPending`. |
| 9 | Ocultar la cabecera vacía de resolución y quitar la línea duplicada (UX-13, parcial) | `OnlineArena.History.cs` | 1–2 h | Sin resolución en curso, `ResolutionPanel` no es visible; en Disquete no aparece «jugador · carta» dos veces. |
| 10 | Motivo en el menú de Relikia y coste junto al nombre (UX-21) | `OnlineArena.Relikia.cs`, `CardInspector.cs` | 2–3 h | Las entradas deshabilitadas incluyen «(tienes N PR)»; la fila de coste queda a ≤ 40 u bajo el nombre. |

---

## VIII. Plan de validación

### Pruebas automatizadas (extender los arneses existentes)

1. **Matriz de capturas de escritorio.** Ampliar `CaptureBoardPreview` (`OnlineArena.BoardPreview.cs`) a 1440, 1638, 1820 y 2280 u y añadir los estados «turno rival», «vida 0», «respuesta propia en turno rival», «elección de habilidad de ataque» y «rechazo». Revisar las capturas en cada PR de UI. Son los estados usados en esta auditoría, generados en una copia.
2. **Aserciones geométricas** en `OnlineArena.VisualTests.cs`:
   - Ningún contador de mano se cruza con zonas del campo (QW1).
   - Ningún texto del tablero mide menos de `RelickVisuals.Type.Caption`: recorrer los `Label` y comprobar `get_theme_font_size`. Los `_Draw` personalizados se comprueban vía constantes.
   - El ancho de la barra es estable entre los estados de V.1.
3. **Estado e interacción**:
   - `--rejection-only`: revisión obsoleta y error del reductor simulado; aviso visible y foco conservado.
   - `Playable` coincide con las opciones (QW5).
   - Atajos: inyectar `InputEventKey` con `GuiInputTestDriver` (ya existe y usa `PushInput`).
   - Omitir animación: tras el clic, `IsAnimating == false`, las vidas y el dado coinciden con el snapshot y no se repite ningún impacto.
4. **Coherencia de reglas en la guía** (`tests/`): por cada rama de `UnavailableCard`, el motor rechaza la jugada (UX-18).
5. **Estabilizar** `--connection-only` en modo gráfico (UX-24) antes de usar la matriz gráfica como puerta de calidad.

### Pruebas manuales

**Resoluciones** (ventana real, no `ContentScaleSize` forzado), en Windows y Linux:

| Resolución | Qué comprobar |
|---|---|
| 1280×720 | Todos los rótulos legibles a 60 cm; mano, riel y botón primario visibles sin desplazamiento. |
| 1920×1080 | Referencia: ningún solapamiento; inspector completo. |
| 2560×1440 | Composición idéntica a 1080p; nitidez de fuentes y cartas (mipmaps). |
| 3440×1440 | Sin bandas negras tras UX-14; riel ampliado; el tablero no queda descentrado. |
| 3840×2160 | Nitidez; sin texturas borrosas; rendimiento estable (anotar FPS con el monitor de Godot, sin inventar umbrales). |
| Ventana redimensionada durante un ataque | La animación no se corta (UX-11). |

**Partida entre dos clientes** (servidor SpacetimeDB local según `docs/PVP.md`, dos ventanas o dos equipos):

1. Entrar a la sala (rápida y privada) desde ambos; confirmar «Rival encontrado» una sola vez.
2. Habilidad inicial y mulligan; iniciativa con dados.
3. Turno 1 del iniciador: comprobar que el Ataque no tiene halo de jugable y que el motivo del bloqueo es correcto.
4. Turno 2: ataque con previsualización; el defensor ve «RESPONDE» si tiene COMBO.
5. Cadena Error de Carga → contraerror: los dos clientes ven la misma pila en el mismo orden.
6. Jugada obsoleta: pulsar en el cliente A justo cuando B cambia el estado; A ve el aviso de rechazo.
7. Desconectar la red de A durante el turno de B: aviso central; reconectar; sin efectos repetidos; el foco de teclado sigue utilizable.
8. Llevar a un jugador a 0 vidas: «GOLPE FINAL» visible en ambos clientes; un daño más da el resultado con superposición central en los dos.
9. Volver al lobby desde ambos (uno antes que el otro).

**Validación con personas** (repetir la propuesta del 9-11, ahora sobre escritorio): cinco personas nuevas en Relick; tareas «¿de quién es el turno?», «¿qué cartas puedes jugar?», «ataca con la mejor habilidad», «¿por qué falló tu jugada?» y «¿cuánta vida le queda al rival y qué pasa si recibe daño?». Registrar tiempo hasta responder, peticiones de ayuda y errores. Una mejora se da por validada cuando al menos cuatro de cinco completan su tarea asociada sin ayuda. Son objetivos de diseño, no métricas poblacionales.

### Cómo comprobar que cada mejora resuelve su problema

| Hallazgo | Evidencia de cierre |
|---|---|
| UX-01 | Prueba de rechazo y paso 6 de la partida manual. |
| UX-02 | Aserción geométrica y capturas a 1820/2280. |
| UX-03 / UX-04 | Prueba `Playable`, captura de la mano en reposo y tarea con personas «¿qué puedes jugar?». |
| UX-05 / UX-06 | Captura simulada y real a 1280×720; inventario de tamaños ≥ mínimo. |
| UX-07 / UX-08 | Capturas en los estados de V.1 y la tarea «¿de quién es el turno?» en ≤ 3 s. |
| UX-09 | Prueba de omisión; medir con cronómetro el tiempo de un ataque completo en Normal frente a Rápida. |
| UX-10 | Prueba con `PushInput`; jugar un turno completo sin ratón salvo para elegir objetivos. |
| UX-12 / UX-19 | Hover sobre el objetivo con una selección activa; la tabla de daño coincide con `GameEngine.DamageFor`. |
| UX-13 | Paso 5 de la partida manual: orden idéntico en ambos clientes. |
| UX-14 | Captura real a 3440×1440 sin bandas. |

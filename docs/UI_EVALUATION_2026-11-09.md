# Evaluación de UI/UX de Relick frente a TCG digitales

Fecha: 9 de noviembre de 2026. Evaluación de la copia de trabajo actual, incluidos los cambios locales de la barra contextual.

## Dictamen

**6/10 frente al nivel de interfaz de un TCG digital comercial.** La base de interacción es competente; los mayores límites son la lectura de las cartas, la pérdida de contexto al reducir el ancho y la falta de información para decidir sin memorizar reglas. La presentación de escritorio resulta más convincente que la de tablet/teléfono.

La diferencia con las referencias comerciales tiene una parte estética y otra funcional. La funcional merece resolverse primero: ver ambos campos, entender los recursos disponibles y leer la carta elegida beneficia cada decisión de la partida.

La nota es un juicio heurístico de diseño, no una medición de satisfacción o retención. No puntúa equilibrio, cantidad de cartas, calidad del servidor, monetización ni amplitud de modos.

## Alcance y evidencia

- Compilación actual: `dotnet build Relick.csproj --no-restore --nologo`, correcta, sin advertencias ni errores.
- Matriz renderizada: `godot --path . -- --pvp-ui-test --ui-screenshot-dir=/tmp/relick-ui-audit/full`, terminada con `PVP_UI_PASS`.
- Barra contextual: ejecución específica con `--context-only`, terminada con `PVP_CONTEXT_ONLY_PASS`.
- Salas: `--lobby-preview-dir`, correcta en 1280×800, 844×390 y 700×390.
- Arena: tamaños lógicos 1440×1024, 1024×768, 844×390 y 700×390, ambos asientos. Las capturas son del viewport renderizado; el compositor puede modificar el tamaño físico de la ventana.
- Revisión visual de salas, espera, selección inicial, mulligan, ataque, equipo y búsqueda de cartas; inspección del código de presentación, selección, consultas y adaptación.
- Referencias: material público de Hearthstone, Marvel Snap, Magic Arena y Yu-Gi-Oh! Master Duel. Se consultaron páginas oficiales y fichas de los editores; se inspeccionaron capturas oficiales de Snap y Master Duel. No se realizaron partidas comparativas en sus clientes.

Las pruebas usan estados sintéticos sin servidor. El texto «Sin conexión» y los botones bloqueados en esas capturas son propios del fixture y **no constituyen un defecto de conectividad o de habilitación observado en una partida real**. La matriz verifica condiciones programadas; aprobarla no demuestra que una persona nueva entienda el juego. No se validaron latencia real, hardware Android/iOS, comodidad táctil, audio o rendimiento sostenido.

Capturas conservadas en `artifacts/ui-audit-2026-11-09/`.

## Rúbrica

Escala: 4 = obstáculo frecuente; 6 = usable con fricción; 8 = claro y consistente; 10 = excelente incluso en estados difíciles.

| Criterio | Peso | Nota | Fundamento |
|---|---:|---:|---|
| Jerarquía del estado del duelo | 20% | 7 | Vida, PR y fases se distinguen en escritorio; faltan acciones restantes y una señal persistente de quién debe responder. |
| Legibilidad e inspección de cartas | 20% | 5,5 | Arte reconocible, texto impreso diminuto y habilidades giradas en la mano. La consulta textual existe, pero requiere abrir un diálogo. |
| Interacción y prevención de errores | 20% | 7,5 | Selección antes de confirmar, costes, objetivos válidos, separación del avance y confirmación de abandono. |
| Coherencia e identidad visual | 15% | 6,5 | Paleta consistente y personaje con presencia; marcos, tipografía e iconos de la UI se integran poco con las cartas. |
| Adaptación a pantallas pequeñas | 15% | 4 | Controles accesibles, pero el campo vertical oculta información táctica y obliga a desplazarse. |
| Aprendizaje y accesibilidad | 10% | 4 | Instrucciones puntuales y tooltips; falta introducción guiada, explicación de bloqueos y controles de lectura/movimiento. |

Resultado ponderado: 5,975, redondeado a **6/10**. Los pesos reflejan que decidir y leer importan más que decorar.

## Comparación con las referencias

Estas son lecciones de diseño aplicables a Relick; no son un ranking objetivo de calidad entre los otros juegos.

| Referencia | Qué aporta como referencia | Qué trasladar a Relick | Límite de la comparación |
|---|---|---|---|
| Hearthstone | Su tablero clásico organiza la confrontación alrededor de cartas, héroes y recursos, con identidad de juego físico. | Una superficie de duelo coherente, objetos interactivos reconocibles y menor protagonismo de contenedores vacíos. | Se toma el patrón clásico como referencia, no se audita su cliente actual ni su tienda. |
| Marvel Snap | En sus capturas oficiales destacan números, posiciones y estados principales. La inspección de carta da protagonismo al arte y al efecto. | Dar prioridad visual a la decisión, los valores útiles y la carta elegida. | Sus reglas más compactas permiten una densidad menor; Relick necesita conservar más zonas. |
| Magic Arena | La guía oficial contempla tutorial y Sparky para introducir un sistema complejo. | Enseñar el ciclo de turno progresivamente y presentar ayudas en el momento de uso. | No reducir las reglas de Relick para imitar Magic; adoptar el principio de aprendizaje. |
| Yu-Gi-Oh! Master Duel | Una captura oficial muestra zonas estables, valores sobre el campo y panel lateral con texto de la carta. Su modo Solo incluye tutoriales. | Lectura en paralelo con el tablero y presentación explícita de efectos/respuestas. | Su densidad también puede abrumar: no es un modelo que copiar completo. |

Relick ya aplica una buena práctica de estos juegos: seleccionar el objeto y ofrecer acciones relacionadas con él. No conviene regresar a una lista general de todas las jugadas.

## Hallazgos y prioridades

### P0 · Conservar la visión del duelo en tablet y móvil

En 1024×768, el propio Relikario ocupa gran parte del área visible y el centro/rival quedan más abajo. En 844×390 y 700×390, la mano/acciones permanecen a la derecha y el tablero se apila en un scroll a la izquierda. La selección de una habilidad puede desplazar el campo y dejar fuera tanto la cabecera propia como el rival. El resumen de vidas ayuda, pero no sustituye equipo, habilidades y Bonus visibles.

**Cambio recomendado:** diseñar una composición compacta específica con ambos jugadores visibles. Reducir retratos y espacios vacíos; mantener vidas, PR, acciones, decisión pendiente y permanentes relevantes. Memoria y otras pilas pueden abrir bandejas. Reservar desplazamiento para colecciones y detalles, evitando que sea necesario para comparar los dos campos.

**Criterio de aceptación:** en los cuatro tamaños, identificar quién decide, recursos propios y estado público relevante del rival sin desplazar verticalmente la arena. Probar también campos ocupados y una mano de siete cartas. Poder elegir un objetivo válido sin perder la instrucción que lo solicita.

### P0 · Dar a las cartas una representación digital legible

Actualmente `HandCard` dibuja la imagen completa y gira 90° las habilidades horizontales para encajarlas en la mano. Se preserva la pieza gráfica, pero se dificulta su lectura. La elección inicial resulta especialmente débil: mucho tablero vacío y dos cartas pequeñas, con texto girado, en la zona inferior.

La consulta mediante el botón de información y `ShowCards` ya presenta el texto real de la carta. El problema es su acceso y el espacio que ocupa, no la ausencia absoluta de lectura.

**Cambio recomendado:** conservar el arte y componer nombre, tipo, coste y valores importantes con texto de interfaz. Mantener las habilidades en una orientación legible. Al seleccionar, abrir un inspector estable con efecto completo; en PC, permitir previsualización al pasar el cursor. En táctil, ofrecer una acción explícita de ampliar además de la selección, sin activar una jugada accidentalmente. Usar búsqueda y filtros para galerías grandes en lugar de una única fila larga.

**Criterio de aceptación:** reconocer nombre y coste de una carta sin ampliar; leer su efecto en una interacción; comparar las habilidades iniciales con nombre, daño, crítico y premio legibles. La elección inicial merece mayor área útil aunque reutilice la arena de fondo.

### P0 · Mostrar el presupuesto de acciones y quién tiene la decisión

La proyección ya incluye `DuelPlayerView.Actions`, pero no se muestra un contador en la presentación de arena revisada. Mostrar «1 acción» como coste informa cuánto gastaría una carta, pero no cuántas acciones quedan.

Además, turno y decisión son distintos. En escritorio, la tira de fases muestra el dueño del turno y la etapa; el texto contextual de «TU DECISIÓN» no se conserva allí como sí ocurre en la versión compacta. Durante una respuesta esto puede producir incertidumbre.

**Cambio recomendado:** contador persistente «Acciones: 2» con actualización al gastar/recuperar; estado independiente «Tu turno», «Tu respuesta», «Resolviendo» o «Esperando al rival». En respuestas, presentar origen autorizado del efecto, consecuencia pendiente y significado de pasar. Para bloqueos, mensajes concretos como «Requiere 300 PR; tienes 200» o «Solo en Combate», respaldados por la lógica autorizada.

**Criterio de aceptación:** una persona puede explicar qué puede hacer y por qué una acción no está disponible sin experimentar con varias cartas. No duplicar la legalidad del motor en el cliente; extender la información autorizada si hace falta.

### P1 · Reorganizar la jerarquía visual y unir la dirección de arte

El marco oscuro, los acentos menta/coral y la vida son consistentes. Sin embargo, los contenedores redondeados, la tipografía genérica y los iconos finos transmiten una aplicación de gestión, mientras las cartas tienen tramas intensas, píxel y estética de cómic. La coexistencia puede funcionar si comparte lenguaje gráfico; hoy la unión es débil.

Los espacios de habilidades vacíos, el equipo vacío y las ranuras de canje reciben bastante área y borde. En preparación, incluso las futuras fases compiten con la elección que importa en ese momento.

**Cambio recomendado:** definir un pequeño sistema visual propio: tipografía de lectura y de títulos, iconos compatibles con las cartas, escala de bordes, estados de selección y una textura de arena discreta. Dar más presencia a objetos ocupados, objetivos válidos y acciones primarias. Mantener las ranuras necesarias para explicar reglas, con tratamiento más ligero cuando están vacías.

No hace falta convertir toda la UI en pixel art, llenar el fondo de efectos ni sustituir todas las ilustraciones. Primero debe mejorar la organización.

### P1 · Hacer comprensibles las resoluciones

Ya existen movimientos de cartas, dado y reacciones diferenciadas de impacto/curación. Conviene aprovechar esa base. La mejora es explicar causa y efecto: carta/habilidad → objetivo → dado/modificación → daño → recompensa, respetando las ventanas de respuesta reales.

**Cambio recomendado:** historial consultable de eventos autorizados, una indicación corta del efecto en curso y variaciones visibles de vida/PR. Mostrar resultados deterministas conocidos; en resultados aleatorios, explicar los posibles resultados sin simular reglas ni revelar información oculta. Ofrecer reducción de movimiento y evaluar sonidos breves para acciones relevantes.

**Criterio de aceptación:** tras una secuencia, el jugador entiende por qué perdió vida o cambió de recursos aunque no haya seguido la animación. Tras reconectar, reconstruir el historial/estado permitido sin repetir efectos como si fueran nuevos.

### P1 · Enseñar dentro del flujo y mejorar accesibilidad

Las instrucciones del mulligan y los textos contextuales ayudan, pero una persona nueva debe aprender muchos términos: PR, Destreza, Bonus, canje, Memoria, Corruptas, Relikia y respuestas.

**Cambio recomendado:** introducción guiada corta al primer duelo, ayuda contextual de términos y ejemplos de ataque/respuesta. Puede implementarse con pasos guiados y estados preparados; no exige reintroducir un rival automático. Explicar la diferencia entre Bonus y canje con un ejemplo visual, y mostrar los efectos/contadores sobre su objeto.

Verificar además foco visible en mano y habilidades: varios controles dibujados a medida eliminan los estilos de foco y no dibujan una alternativa equivalente. Añadir navegación de teclado verificable, escala de texto del inspector y redundancia de forma/icono para los colores. Evaluar contraste antes de definir umbrales finales.

### P2 · Pulir entrada y salida

El lobby es una de las pantallas más claras: nombre, crear y unirse se comprenden rápidamente. Conviene conservar esa sencillez y añadir identidad de Relick con moderación. Cuando el estado real lo permita, dar confirmación al copiar, distinguir reconexión disponible y explicar errores de código/conexión con siguiente paso concreto.

El resultado merece un cierre propio con causa, opción clara de salir y, si el producto lo contempla, revancha. El código actual presenta el resultado en textos de la arena y permite cerrar desde el menú. La ausencia de constructor de mazos pertenece al alcance de producto actual y no se usa como penalización de esta nota.

## Orden de ejecución

1. Contador de acciones y señal de decisión/respuesta: mejora frecuente y acotada.
2. Inspector y presentación legible de habilidades/cartas: reduce la necesidad de memorizar y beneficia preparación y combate.
3. Composición compacta de tablero: mayor trabajo, imprescindible antes de considerar buena la experiencia móvil.
4. Jerarquía de ranuras/objetos y sistema visual compartido.
5. Historial, explicación de resoluciones, ayudas y accesibilidad.
6. Pulido de lobby y cierre de duelo.

No se estiman días sin diseñar el layout y verificar qué información adicional necesita el servidor. Las tres primeras entregas deben comprobarse antes de invertir en un paquete grande de efectos o rehacer arte.

## Validación con personas

Hacer una ronda formativa con cinco personas que no conozcan Relick, separada de las pruebas automáticas. Es una muestra para descubrir problemas, no para estimar métricas poblacionales.

- Crear/unirse a sala y explicar qué ocurrirá a continuación.
- Comparar las dos habilidades iniciales y confirmar el mulligan.
- Identificar turno, decisión, PR y acciones restantes.
- Realizar un ataque y elegir una respuesta sin instrucciones externas.
- Inspeccionar una carta y consultar una resolución anterior.
- Repetir las tareas principales en pantalla compacta.

Como objetivos iniciales de diseño: que al menos cuatro de cinco completen las tareas básicas sin ayuda, que se reconozcan turno/decisión/recursos en unos tres segundos y que no aparezcan errores críticos de objetivo o avance. Registrar tiempo, solicitudes de ayuda y errores; ajustar objetivos después de la primera ronda. Alcanzar aproximadamente 8/10 sería una hipótesis de mejora que habría que volver a evaluar con estas evidencias, no una consecuencia garantizada de implementar una lista.

## Puntos de implementación

- `scripts/ArenaPresentation.cs`: distribución de campo, jugadores, mano y fases.
- `scripts/ArenaViewport.cs`: umbrales compactos, densidad, áreas seguras y diálogos.
- `scripts/BoardWidgets.cs`: `HandCard`, `SkillTile`, representación del texto, foco y estados.
- `scripts/OnlineArena.ActionBar.cs` y `scripts/ArenaActionText.cs`: contexto, costes y mensajes.
- `scripts/OnlineArena.cs`: integración del estado, contador y consultas de cartas.
- `core/DuelProjection.cs`: acciones ya disponibles; cualquier explicación o previsualización nueva debe respetar la información autorizada.

## Referencias públicas

- [Hearthstone, sitio oficial](https://hearthstone.blizzard.com/en-us).
- [Tablero clásico, material oficial de Blizzard de 2015](https://blizzard.gamespress.com/en/Blackrock-Mountain-Press-Kit-87837).
- [Marvel Snap, ficha del editor y material visual](https://store.steampowered.com/app/1997040/MARVEL_SNAP/).
- [Magic Arena, guía oficial de inicio](https://magic.wizards.com/en/mtgarena/getting-started).
- [Magic Arena, ficha del editor](https://store.steampowered.com/app/2141910/Magic_The_Gathering_Arena/).
- [Yu-Gi-Oh! Master Duel, sitio oficial y tutoriales](https://www.konami.com/yugioh/masterduel/us/en/).
- [Master Duel, ficha del editor y capturas](https://store.steampowered.com/app/1449850/YuGiOh_Master_Duel/).

# Revisión de DRY y oportunidades de optimización

Revisión del árbol de trabajo del 7 de noviembre de 2026, posterior a la elección de habilidad inicial. Se revisaron motor, preparación del servidor, adaptadores de red y presentación de Godot. No se cambió el comportamiento del juego ni se publicó un módulo durante esta revisión.

## Conclusión

Hay oportunidades concretas para compartir reglas y reducir trabajo repetido. Las prioridades son centralizar la preparación, reutilizar validaciones de jugadas y corregir la enumeración de códigos reutilizables desde Memoria. La presentación ya comparte `ArenaPresentation`, `ArenaMotion` y `ArenaViewport`: conviene continuar esa separación sin fusionar el controlador local con el cliente que solo recibe información autorizada.

## Hallazgos priorizados

### 1. Reglas de preparación repetidas entre motor y servidor

- `core/GameEngine.InitialSkill.cs`: obtención de habilidades, extracción de la elegida, barajado y robo de otras cuatro cartas.
- `server/spacetimedb/InitialSkills.cs`: reproduce esas decisiones trabajando sobre filas de cartas.
- `core/GameEngine.cs: Start` y `server/spacetimedb/Preparation.cs: ApplyMulligan`: devolución de seleccionadas al fondo, en orden inverso, y reemplazos.
- Bonus, iniciativa y primer robo también tienen recorridos separados.

**Propuesta:** extraer funciones puras de preparación en `core/`, que validen y produzcan un plan usando identificadores de copias, la habilidad elegida y una fuente de azar. El adaptador local aplicaría el plan a listas y emitiría eventos; el servidor lo aplicaría dentro de su transacción. Autenticación, idempotencia y acceso a tablas deben seguir en el servidor.

No basta con compartir el algoritmo de barajado: lo valioso es compartir las decisiones y el orden exacto de operaciones. La selección de una habilidad propia del Relikario o genérica debe seguir la [clasificación del usuario](CARD_TAXONOMY.md) cuando se implemente esa afinidad.

**Beneficio principal:** evitar que una regla cambie en práctica pero no en PvP. Prioridad alta de mantenimiento y corrección.

### 2. Enumeración de Memoria con recorrido potencialmente cuadrático

`DuelOptions.For` recorre Memoria y llama a `CanReplayMemoryCode` por cada posición. Este vuelve a comprobar `CanUseRelikiaEffect(ReplayCode)`, que busca una carta válida por toda la Memoria hasta encontrarla.

Si la primera carta válida está al final, se repite ese recorrido para cada posición: el número de comprobaciones sobre Memoria puede crecer como n². Además se repite el coste literal `400`, ya definido en el catálogo de efectos de Relikia.

**Propuesta:** validar una sola vez las condiciones comunes de esa consulta y después filtrar las cartas con sus condiciones individuales. Exponer una consulta de candidatos que puedan reutilizar opciones, interfaz y bot. Obtener el coste de `RelikiaEffects`, sin repetir el literal. Mantener validación completa al ejecutar el comando, porque el estado puede haber cambiado desde la consulta.

#### Ensayo aislado

Se compiló el `core/` actual en un proyecto temporal .NET 8.0.33 Release, con compilación por niveles desactivada, 30 calentamientos y 51 muestras por escenario. Estado en Acciones, 400 PR y dos acciones. Memoria contiene códigos Huevo Azul que no pueden pagarse con el coste adicional de Relikia y un Senshuni válido al final. Se compara obtener los índices mediante la consulta actual con una variante que comparte la comprobación inicial.

| Cartas en Memoria | Actual: mediana | Propuesta: mediana | Asignación actual por consulta | Asignación propuesta |
|---|---:|---:|---:|---:|
| 16 | 0,05860 ms | 0,00480 ms | 19.088 bytes | 2.168 bytes |
| 64 | 0,38950 ms | 0,01140 ms | 248.340 bytes | 7.544 bytes |
| 256 | 5,87770 ms | 0,04170 ms | 3.745.878 bytes | 29.048 bytes |

Ambas consultas devolvieron los mismos candidatos en los tres casos. El caso de 256 es estrés sintético, no un tamaño normal de Memoria del prototipo. Son mediciones de esta consulta en .NET nativo; no miden WASI, red, FPS ni latencia de una jugada completa. Tener 500 cartas en el catálogo no significa tener 500 cartas simultáneas en Memoria.

El prototipo de consulta existe solo en el ejecutable temporal; no está integrado en el juego. Antes de integrarlo hay que comprobar fases, recursos, objetivos, cartas de distintas clases y rechazos sin mutación con pruebas dirigidas. Evidencia local: `artifacts/dry-review/results.txt` y fuentes del ensayo guardadas como `.txt`, fuera de la compilación.

### 3. Condiciones de legalidad repetidas

La posibilidad de enseñar, atacar, equipar y usar efectos se expresa en varios lugares: comandos de `GameEngine`, `HasAvailablePlay`, `DuelOptions`, `PracticeBot` y controles de `Main`. Por ejemplo, los requisitos para enseñar aparecen tanto en `GameEngine.Learn` como en el generador de opciones.

**Propuesta:** validadores de dominio reutilizables por acción —con motivo de rechazo— y consultas de candidatos basadas en esos mismos validadores. El comando sigue validando antes de modificar el estado. El bot conserva su estrategia, pero elige entre acciones válidas en lugar de mantener otra definición de legalidad.

`HasAvailablePlay` excluye ciertos avances y no equivale a «hay alguna opción» porque siempre existen opciones como avanzar o abandonar. Hay que compartir las condiciones pertinentes sin mezclar esas políticas diferentes ni crear dependencias circulares entre comandos y enumeración.

**Beneficio principal:** incorporar nuevas afinidades, equipos ligeros, áreas y trampas sin modificar varias versiones de la misma condición. Prioridad alta de mantenimiento.

### 4. Recursos y geometría creados durante dibujo

`BoardArt.Plate` crea un `StyleBoxFlat` por llamada. Se utiliza desde controles de dibujo de cartas y zonas. Algunos símbolos también crean arrays de puntos en cada dibujo; el corazón genera sus 48 puntos mediante LINQ.

**Propuesta:** reutilizar estilos comunes y precomputar geometría constante. Los estilos deben tener un ciclo de vida controlado; evitar un diccionario global ilimitado con colores o valores animados como claves. Los estados visuales variables necesitan recursos propios o una estrategia acotada, sin modificar accidentalmente recursos compartidos por otros controles.

La creación repetida está confirmada por lectura del código. Falta medir su peso real con el profiler de Godot antes de prometer una mejora de FPS o atribuirle un fallo de cierre. Prioridad media de rendimiento.

### 5. Preparación consultada y comparada cada fotograma

`LobbyConnection.Hand` ordena y materializa las filas al consultarse. Durante la preparación, `OnlineArena.UpdatePreparation` la consulta desde `_Process`; `PresentPreparation` y `PresentInitialSkillChoice` construyen claves de texto para detectar que nada cambió. El acceso a `Duel` se repite también desde `Tick` y la arena; ya evita deserializar JSON idéntico, lo cual conviene conservar.

**Propuesta:** mantener un estado de presentación coherente y una revisión local que se actualicen al procesar cambios de tablas. Ordenar la mano una vez por cambio, y actualizar controles cuando cambie esa revisión. Tras reconectar debe invalidarse el estado por época/sala y mantenerse la comprobación de versiones de los paquetes privados y públicos.

No basta con cachear una lista indefinidamente: deben contemplarse cambios de sala, eliminaciones de filas y suscripciones que se retiran al empezar el combate. Prioridad media; medir asignaciones por fotograma antes y después.

### 6. Reconstrucción de controles y búsquedas del árbol

`Main.Render` y `OnlineArena.Render` recrean buena parte del árbol. Seleccionar una habilidad inicial llama a `Render`; `RefreshSelection` localiza controles mediante `FindChildren`, interpreta índices desde sus nombres y vuelve a construir botones de movimientos. Ya existe optimización para no reconstruir todo el tablero al seleccionar una carta normal y para conservar retratos; hay que extenderla, no perderla.

**Propuesta:** separar cambios de distribución, de estado y de selección. Guardar referencias tipadas a cartas, habilidades y controles; actualizar selección, etiquetas y habilitación en su lugar. Reconstruir colecciones cuando cambien realmente sus elementos y la distribución cuando cambie el tamaño.

Esto también reduce las referencias obsoletas durante eventos de red; las pruebas ya necesitan recuperar controles actuales cuando el otro jugador provoca una reconstrucción. No conviene introducir reutilización de nodos o agrupaciones complejas sin comprobar primero el coste y la claridad del código.

### 7. Proyecciones completas redundantes en el servidor

`CacheDuel` crea un snapshot público y luego uno completo por participante. Los snapshots privados se usan para obtener manos y opciones, aunque se han vuelto a copiar zonas públicas como Memoria y habilidades. Antes de ejecutar una jugada también se enumeran las opciones del actor; después se necesitan las del estado nuevo, lo que es una etapa distinta y no debe eliminarse como si fuera duplicación accidental.

**Propuesta:** separar explícitamente proyección pública, datos privados y opciones, compartiendo las reglas de visibilidad. Evitar construir snapshots completos que luego se descartan en parte. Conservar revisiones, autorización e invalidación; no reutilizar opciones anteriores después de ejecutar una jugada.

Es una oportunidad demostrada de reducir construcción redundante, pero falta perfilarla en WASI con partidas representativas para estimar su impacto. Prioridad media/baja frente a las reglas duplicadas y el recorrido de Memoria.

## Qué conviene mantener separado

- El motor puro y los adaptadores de Godot y SpacetimeDB.
- Autenticación, comprobantes de comandos y transacciones del servidor.
- Estado privado y proyecciones autorizadas de cada jugador.
- Estrategia del bot y validación de reglas.
- Bindings generados: su duplicación deriva de las interfaces del SDK y de los módulos de prueba. No editarlos manualmente para hacer DRY.
- Preparación común de pruebas y construcción interactiva de una partida: los helpers que eligen Magnet explícitamente son fixtures; no deben reintroducir una habilidad predeterminada en el juego.

## Orden propuesto

1. Optimizar la consulta de Memoria con pruebas dirigidas; es acotada y tiene evidencia de trabajo repetido.
2. Extraer la preparación compartida y sus invariantes, preservando la secuencia de azar, copias y movimientos.
3. Reutilizar validaciones y candidatos al adaptar el modelo a la clasificación de cartas.
4. Perfilar Godot para priorizar estilos, actualizaciones de controles y asignaciones por fotograma.
5. Medir las proyecciones en el servidor antes de optimizar su construcción.

La suite actual ejecutada durante la revisión pasó **123/123 comprobaciones**. La propuesta de rendimiento aún no forma parte del motor; no se hicieron cambios jugables ni despliegues. Para una implementación posterior, cada bloque debe ser revisable y probarse por separado.

## Aplicación posterior: PvP como único modo

Se retiraron el controlador de práctica, sus pantallas y el rival del ejecutable. La política de simulación queda únicamente en las pruebas. Se aplicaron la consulta lineal de candidatos de Memoria, la reutilización de referencias de controles, la selección inicial sin reconstrucción, la caché de mano por cambios de filas y las actualizaciones de preparación por revisión. La geometría constante de vida se calcula una vez. La medición anterior es la referencia previa al cambio; las demás propuestas requieren su propia validación antes de aplicarse.

Medición posterior de `ReplayableMemoryCodes`, con el mismo escenario y metodología del ensayo anterior: 16 cartas, mediana 0,00240 ms y 1.152 bytes por consulta; 64 cartas, 0,00890 ms y 3.840 bytes; 256 cartas de estrés, 0,04480 ms y 14.592 bytes. La medición compara ejecuciones locales distintas, no garantiza un factor de mejora en producción ni mide latencia completa o FPS. Resultados en `artifacts/pvp-only/memory-query-after.txt`.

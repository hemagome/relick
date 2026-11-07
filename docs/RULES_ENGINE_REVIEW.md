# Evaluación del motor para ampliar el catálogo

## Dictamen

La base actual es aprovechable y el tamaño de un catálogo de 511 definiciones no fue un problema en la prueba local. La arquitectura de efectos todavía corresponde a un subconjunto concreto del juego: no está preparada para incorporar de forma mantenible 500 cartas con comportamientos diversos mediante datos o módulos aislados.

La recomendación es conservar el motor puro, el servidor autoritativo, las proyecciones privadas y la persistencia, y evolucionar la ejecución de efectos antes de cargar el catálogo completo. No hace falta sustituir Godot ni SpacetimeDB para resolver este problema.

## Validación realizada

Se ejecutó la suite actual: **117/117 comprobaciones correctas**, incluida la simulación de 2.000 duelos del catálogo actual, las pruebas de reconstrucción y los casos de persistencia y privacidad. Estos resultados validan el conjunto implementado; no prueban interacciones con efectos todavía inexistentes.

Se creó un ejecutable temporal que compila los archivos actuales de `core/`, sin modificar el catálogo del juego ni publicar cambios en el servidor. En ese proceso aislado se añadieron 500 definiciones sintéticas parametrizadas con las operaciones existentes de robo y curación. Las 500 ejecutaron los resultados esperados.

### Medición de una jugada

.NET 8.0.33 nativo, compilación Release, 16 procesadores lógicos visibles, compilación por niveles desactivada para reducir diferencias de calentamiento; 200 ciclos de calentamiento y 3.000 muestras por escenario. El ciclo deserializa el estado, obtiene opciones del jugador 0, termina su turno, guarda el nuevo estado y genera/serializa las vistas de ambos participantes. Es un caso sintético de estado activo, no una simulación del catálogo completo.

| Escenario | Mediana | p95 | Bytes asignados por ciclo | Estado inicial JSON |
|---|---:|---:|---:|---:|
| 11 definiciones; mano propia de 7 cartas | 0,0663 ms | 0,1160 ms | 32.552 | 2.560 bytes |
| 511 definiciones; mismo estado y mismas cartas activas | 0,0659 ms | 0,0853 ms | 32.552 | 2.560 bytes |
| 511 definiciones; estrés de 500 cartas simultáneas en la mano propia | 0,3456 ms | 0,4409 ms | 333.870 | 8.353 bytes |

La diferencia entre las dos primeras muestras no demuestra una mejora por añadir cartas. Indica que, en este caso, el catálogo inactivo no añadió un coste observable relevante. El diccionario se consulta por identificador y las opciones se generan a partir de las zonas activas. El caso de 500 cartas en mano muestra que importa más el tamaño del estado activo que la cantidad total de definiciones.

Estas cifras no incluyen WASI, transacciones de SpacetimeDB, tráfico, renderizado, imágenes, ni concurrencia. No garantizan latencia de producción ni capacidad de usuarios. Tampoco representan 500 efectos distintos.

### Dos comprobaciones de extensibilidad

1. Una definición sintética con `HealFromDie: true` y `Draw: 2` fue aceptada y curó al jugador de 4 a 5, pero dejó 0 cartas en mano cuando la secuencia propuesta requería robar 2. `ResolveCode` retorna tras iniciar la curación con dado y no conserva el paso de robo. Esto expone una composición no soportada, no un error demostrado en las cartas actuales.
2. Una copia de la definición de Colgante con otro identificador se pudo equipar, pero no permitió usar su efecto. `CanUsePendant` comprueba el identificador original. Copiar los datos y el texto no registra un comportamiento reutilizable.

Los resultados y el código del ensayo quedan en `artifacts/rules-engine-review/`; los archivos fuente se guardaron como `.txt` para no incorporarlos por accidente a la compilación del juego. El ensayo se ejecuta en un proyecto temporal e incorpora `core/` directamente. No se añadieron cartas sintéticas al catálogo jugable.

## Obstáculos para 500 efectos diversos

| Obstáculo | Evidencia actual | Consecuencia al ampliar |
|---|---|---|
| Definiciones con indicadores específicos | `CardDefinition` tiene `HealFromDie`, `ChangePointsFromDie`, `DestroyNonSkill`, `RerollOwnDie`, etc. | Una mecánica nueva suele exigir cambiar el esquema y el despachador; las combinaciones no son secuencias generales. |
| Reglas ligadas a cartas concretas | Comparaciones con Magnet, Destiny, Colgante y Caja en el motor; evolución asociada a Yuno. | Reutilizar un efecto requiere extraerlo o duplicar condiciones. |
| Fases y comandos especializados | `MagnetChoice`, `PointsChoice`, `DestroyChoice`, comandos y continuaciones específicos. | Nuevas elecciones pueden obligar a tocar motor, estado persistido, opciones, protocolo, bot e interfaz. |
| Resolución limitada a los casos actuales | Hay una cadena para Error de Carga y continuaciones para dados/robos. No hay un modelo general de efectos disparados, continuos, de reemplazo o con duración. | Interacciones nuevas requieren establecer explícitamente orden, prioridades, condiciones y ciclo de vida según las reglas de Relick. |
| Identidad por definición y posición | Mano, memoria y campo guardan identificadores de definición; contadores de habilidades usan posiciones. | Seguir una copia concreta al cambiar de zona, conservar propietario/controlador o adjuntar estado temporal requiere una identidad de instancia estable. |
| Estado y protocolo ajustados al MVP | Un solo equipo; selección de comando limitada a cuatro índices; selección múltiple expresada como descartes. | Equipos ligeros múltiples, otros objetivos o elecciones mayores necesitan ampliar ambos modelos. |
| Versionado todavía básico | Formato y reglas versión 1, con rechazo de versiones desconocidas. | Hará falta definir compatibilidad de partidas y versiones del catálogo cuando cambien efectos o balance. |

Las señales actuales para animaciones son útiles para presentación. No constituyen por sí mismas un sistema general de eventos de reglas que permita registrar y ordenar efectos de cualquier carta.

## Evolución recomendada

La [clasificación de cartas del usuario](CARD_TAXONOMY.md) define las categorías que debe soportar esta evolución, incluida la distinción entre COMBO y los requisitos de acceso de las trampas desde el extradeck. Sus tipos y afinidades deben poder consultarse como datos.

1. **Clasificar una muestra representativa del catálogo real.** Separar operaciones reutilizables, condiciones, costes, objetivos, disparadores, duración, reemplazos y excepciones. Elegir cartas que cubran patrones distintos, no solo las más sencillas. Sin ver las 500 cartas no se puede estimar qué porcentaje se resolverá con operaciones comunes.
2. **Introducir definiciones e instancias separadas.** Una definición describe la carta; una instancia persistida identifica su copia y guarda propietario, controlador, zona y estado propio.
3. **Crear un sistema híbrido de efectos.** Operaciones tipadas y parametrizadas para robar, curar, dañar, modificar PR, mover, seleccionar y tirar dados; secuencias/condiciones para combinarlas; módulos C# registrados para excepciones. Evitar añadir un booleano o una fase por carta. No es necesario inventar primero un lenguaje de scripts completo.
4. **Generalizar resolución y elecciones.** Conservar las fases del turno separadas de la resolución pendiente. Una pila/cola serializable debe recordar fuente, actor, objetivos, paso y datos locales; las elecciones deben declarar participantes, restricciones, cantidades y visibilidad. Costes, cancelaciones y revalidación de objetivos necesitan semántica explícita. El cliente debe consumir decisiones autorizadas sin conocer cada efecto particular.
5. **Añadir disparadores y modificadores con orden definido.** Identificar cuándo se activan, cuándo vencen y cómo interactúan según las reglas reales. Probar ciclos y cadenas largas sin truncar silenciosamente resultados legales.
6. **Migrar gradualmente y comprobar equivalencia.** Primero trasladar cartas actuales sencillas y después dado, defensa y cancelación, manteniendo las pruebas de referencia. Cambiar el formato persistido y el protocolo con una estrategia explícita de compatibilidad.
7. **Exigir pruebas por patrón y por carta.** Camino normal, rechazos sin mutación, costes/cancelaciones, límites, orden, duplicados, privacidad y guardar/restaurar en cada pausa. Complementar con interacciones dirigidas y simulaciones; ninguna batería finita garantiza todas las combinaciones.

## Criterio para empezar la carga masiva

Antes de implementar las 500 cartas, un lote piloto de cartas reales con mecánicas representativas debe poder incorporarse principalmente mediante definiciones o módulos de efecto, sin añadir un caso por carta al motor central, al protocolo y a la interfaz. Debe conservar equivalencia de las cartas existentes y soportar la recuperación de cada decisión pendiente. Solo entonces conviene escalar la producción de contenido.

La limitación principal observada es la extensibilidad y la corrección de las interacciones; no el número de entradas del catálogo.

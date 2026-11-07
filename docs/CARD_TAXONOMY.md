# Clasificación de cartas de Relick

Especificación proporcionada por el usuario el 7 de noviembre de 2026. Es la referencia para ampliar el catálogo, los efectos y el deckbuilding. Si las categorías del MVP o documentación anterior difieren, deben adaptarse a esta clasificación. Registrar una categoría aquí no significa que su funcionamiento esté implementado.

## Categorías y propiedades

| Categoría | Clasificación y datos necesarios | Comportamiento indicado |
|---|---|---|
| Relikario | Puntos de vida, habilidad propia, requisito de evolución, evolución y personaje animado asociado. | El Relikario determina su representación animada. Actualmente solo está codificado Yuno Gyambura, con su evolución. |
| Habilidad | Genérica o específica de un Relikario. | La afinidad es un dato de la carta que debe poder consultar el deckbuilding y los efectos. Magnet Shuffle y Destiny Shuffle son específicas de Yuno; no son genéricas. |
| Ataque | Carta de ataque con ilustración asociada al Relikario. | La representación gráfica depende del Relikario. Esto no implica por sí mismo cambiar el efecto o crear una definición mecánica distinta por cada ilustración. |
| Equipo | Equipo normal o equipo ligero. | Como máximo un equipo normal equipado. Los equipos ligeros no tienen límite de cantidad y equiparlos no exige destruir el equipo normal ni los otros ligeros. |
| Área | Dos efectos diferenciados y una orientación elegida por su dueño. | Se ubica en la zona de Bonus para activar sus efectos. Uno de los efectos corresponde a cada jugador según la orientación elegida. |
| Código | Jugada, Acción o Trampa. | Jugada y Acción pueden tener o no la propiedad COMBO. Trampa nunca puede tenerla y tiene un origen y una condición de acceso distintos. |

## Relikarios y afinidad de habilidades

La identidad del personaje debe poder relacionar sus distintas formas y la evolución, su habilidad propia y su representación animada. La condición para evolucionar y la evolución resultante son datos distintos del personaje y de sus puntos de vida.

La afinidad de una habilidad debe representarse explícitamente: genérica o vinculada a un Relikario. No debe deducirse del nombre, del texto impreso, de la ilustración ni de una comparación fija con Magnet o Destiny. La relación de una habilidad de Yuno con sus formas o niveles debe ser explícita al definir las reglas de compatibilidad; no se deben inventar restricciones de nivel a partir de esta clasificación.

La ilustración de un ataque y la animación del personaje son dependencias de presentación del Relikario. El motor de reglas debe poder trabajar con identificadores estables, sin cargar texturas o escenas de Godot.

## Equipos

El equipo normal y el ligero comparten la categoría Equipo; su clase determina el límite y el comportamiento de equipar. El estado debe poder representar simultáneamente un equipo normal y una colección de equipos ligeros.

Equipar un ligero no sustituye los equipos que ya están equipados. El límite de un equipo normal no debe aplicarse a todos los equipos juntos. La incorporación de ligeros necesita adaptar también los objetivos de efectos, la consulta del campo, los movimientos a Memoria, la persistencia y la interfaz; no basta con añadir una etiqueta.

Esta clasificación no define excepciones de cartas, costes nuevos ni requisitos de los equipos ligeros. Esos detalles se implementarán a partir de las cartas y reglas correspondientes.

## Áreas y orientación

Un área contiene dos efectos identificables y una orientación que define cuál se aplica a cada jugador. Deben distinguirse el dueño de la carta, el jugador al que se aplica cada efecto y la zona donde está. El efecto asignado al rival no cambia por sí mismo el dueño del área.

La orientación elegida y la ubicación en la zona de Bonus deben formar parte del estado del juego, no ser únicamente una rotación visual. La activación de sus efectos depende de su ubicación en esa zona.

No se deducen de esta clasificación el coste de orientar, la posibilidad de reorientar después, la sustitución de áreas ni la coexistencia exacta con la carta Bonus. Esos detalles necesitan su regla concreta antes de implementarse.

## Códigos y COMBO

| Tipo de código | Puede ser COMBO | Origen indicado |
|---|---|---|
| Jugada | Sí; también puede no serlo. | Se contempla en el mazo principal, sin introducir límites adicionales aquí. |
| Acción | Sí; también puede no serlo. | Se contempla en el mazo principal, sin introducir límites adicionales aquí. |
| Trampa | No. | Extradeck: no empieza en el mazo principal. |

COMBO es una propiedad independiente del tipo Jugada/Acción. No es un cuarto tipo de código ni equivale automáticamente a coste cero, ausencia de acciones o activación en cualquier momento: cada carta debe conservar sus costes, condiciones y momentos permitidos.

«Código de tipo Acción» y «acción disponible del turno» son conceptos diferentes. El nombre del subtipo no determina por sí solo su coste de acciones.

Los códigos Trampa se pueden añadir a la mano cuando se cumplen las combinaciones específicas de cartas que la propia trampa lista para el canje. Es necesario modelar el extradeck y los requisitos de acceso de cada trampa. Esos requisitos no son la propiedad COMBO.

Las combinaciones deben poder distinguir las cartas requeridas y sus cantidades cuando la carta lo especifique. No se presupone que baste cualquier par de cartas ni que el mecanismo sea el mismo que reclamar el Bonus. Tampoco se presupone que cumplir la combinación active directamente la trampa: la transición indicada es añadirla a la mano.

Aún deben concretarse, según las trampas y sus reglas, el consumo o destino de las cartas del canje, el momento de comprobar requisitos, la existencia de combinaciones alternativas, las elecciones entre varias trampas disponibles y sus condiciones de uso después de llegar a la mano. Esta clasificación no autoriza inventar esas reglas.

## Distinciones para el modelo

Separar los siguientes conceptos para que el deckbuilding y los efectos puedan consultarlos sin interpretar el texto de las cartas:

- Categoría principal: Relikario, Habilidad, Ataque, Equipo, Área o Código.
- Afinidad de una habilidad: genérica o de un Relikario.
- Clase de equipo: normal o ligero.
- Tipo de código: Jugada, Acción o Trampa.
- Propiedad COMBO, validando que nunca se aplique a una Trampa.
- Origen de la carta y zona actual: estar ahora en la mano no convierte una Trampa en carta del mazo principal.
- Requisitos de acceso desde el extradeck, separados de los costes y condiciones de activación.
- Dueño, orientación y destinatario de los efectos de un área.
- Perfil visual asociado al Relikario, separado del comportamiento de reglas.

«Sobre el Relikario» describe una ubicación o relación de una carta. No debe convertirse por sí sola en una séptima categoría principal: la categoría original y la ubicación deben poder conservarse por separado.

No se establecen aquí tamaños de mazo/extradeck, límites de copias ni nuevas restricciones de afinidad para ataques o equipos. Esos requisitos no fueron especificados en esta clasificación.

## Diferencias con el código actual

| Modelo del MVP | Adaptación pendiente |
|---|---|
| `RelikarioDefinition` y evolución codificada para Yuno; retratos e ilustración de ataque vinculados directamente a recursos de Yuno. | Hacer explícitas las relaciones de evolución y el perfil visual por Relikario al ampliar personajes. |
| Las habilidades solo tienen `CardKind.Skill`; Magnet y Destiny no incluyen metadatos de afinidad. | Declarar que ambas son específicas de Yuno y admitir habilidades genéricas. Aplicar esa información al deckbuilding y a las validaciones correspondientes. |
| `PlayerState.Equipment` contiene una sola carta y equipar otra sustituye la anterior. | Diferenciar equipo normal y colección de ligeros, con sus límites y objetivos correctos. |
| No hay categoría ni estado de área. | Añadir categoría, dos efectos, dueño, orientación y relación con la zona Bonus. |
| `CodeType` contiene `Character`, `Object` y `Play`. Caja de Cartón comprueba específicamente `Object`. | Migrar a Jugada/Acción/Trampa y revisar las condiciones de efectos afectadas. **No asignar automáticamente Personaje u Objeto a Acción**: hace falta clasificar las cartas existentes y confirmar cómo queda el efecto de Caja. |
| `IsCombo` ya está separado del coste de acciones. | Conservar esa independencia y prohibir COMBO en Trampa cuando el nuevo modelo esté implementado. |
| No hay extradeck ni requisitos de trampas. | Añadir estado, definiciones y validación para los requisitos del canje y el paso a la mano. |
| `CardKind.Attachment` mezcla una categoría con la zona «sobre el Relikario». | Separar la clasificación de la ubicación/relación que un efecto aplique. |

Esta entrega documenta la clasificación y las adaptaciones necesarias; no modifica los efectos de las cartas existentes ni publica un módulo nuevo. La migración debe hacerse de forma coordinada entre definiciones, estado, validaciones, protocolo, presentación y pruebas, usando esta especificación junto con el [informe del motor](RULES_ENGINE_REVIEW.md).

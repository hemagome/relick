# Clasificación de cartas de Relick

Especificación de categorías aportada por el usuario. Es la referencia para ampliar el catálogo, los efectos y el deckbuilding: si el código o la documentación difieren, deben adaptarse a esta clasificación.

## Categorías y propiedades

| Categoría | Clasificación y datos necesarios | Comportamiento indicado |
|---|---|---|
| Relikario | Puntos de vida, habilidad propia, requisito de evolución, evolución y representación en el tablero. | Yuno, Osamu, Ironwing, WD y Yuno Oro tienen dos niveles jugables (`RelikarioCatalog.Playable`). Véase [Añadir un Relikario](ADDING_RELIKARIO.md). |
| Habilidad | Genérica o específica de un Relikario. | La afinidad es un dato de la carta que debe poder consultar el deckbuilding y los efectos. Magnet Shuffle y Destiny Shuffle son específicas de Yuno; no son genéricas. |
| Ataque | Carta de ataque con ilustración asociada al Relikario. | La representación gráfica depende del Relikario. Esto no implica por sí mismo cambiar el efecto o crear una definición mecánica distinta por cada ilustración. |
| Equipo | Equipo normal o equipo ligero. | Como máximo un equipo normal equipado. Los equipos ligeros no tienen límite de cantidad y equiparlos no exige destruir el equipo normal ni los otros ligeros. |
| Área | Dos efectos diferenciados y una orientación elegida por su dueño. | Se ubica en la zona de Bonus para activar sus efectos. Uno de los efectos corresponde a cada jugador según la orientación elegida. |
| Código | Jugada, Objeto, Personaje o Trampa. | Jugada, Objeto y Personaje admiten COMBO. Las trampas tienen su propia receta y mazo. |

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
| Objeto | Sí; también puede no serlo. | Mazo principal. |
| Personaje | Sí; también puede no serlo. | Mazo principal. |
| Trampa | No. | Extradeck: no empieza en el mazo principal. |

COMBO es una propiedad independiente del subtipo. Cada carta conserva sus costes,
condiciones y momentos permitidos.

El reglamento 6.0 permite crear como máximo un Código Trampa por canje si las dos
cartas que colocó el jugador cumplen su receta. El canje entrega y repone el
Bonus por el procedimiento habitual. La creación espera a que termine cualquier
cadena en resolución y coloca la trampa en el lugar que indica su carta. Una
trampa que abandona el campo queda fuera del juego. [Reglamento oficial, §§ 4.6 y 5.3](https://sites.google.com/view/relickjuegodecartas/reglas/nuevo-manual-de-juego#h.byyxqj2ib44c_l).

Si las dos cartas cumplen varias recetas, el jugador elige cuál crear. El motor guarda en `TrapCraft` todas las recetas cumplidas (`Choices`) y las dos cartas canjeadas (`Ingredients`). Ofrece una opción «Crear X» por receta, con su posición en `DuelMove.Value`, más «Pasar sin crear». Un cliente que no envía posición crea la primera receta. Con las recetas actuales (Súper Imán, Zahira Flair y Súper Ataque) no hay solapes; Súper Error de Carga (Error de Carga + un COMBO) tampoco se solaparía con ellas. Las cartas que el adversario coloque en tu canje aún no se distinguen, porque ningún efecto implementado lo hace.

## Distinciones para el modelo

Separar los siguientes conceptos para que el deckbuilding y los efectos puedan consultarlos sin interpretar el texto de las cartas:

- Categoría principal: Relikario, Habilidad, Ataque, Equipo, Área o Código.
- Afinidad de una habilidad: genérica o de un Relikario.
- Clase de equipo: normal o ligero.
- Tipo de código: Jugada, Objeto, Personaje o Trampa.
- Propiedad COMBO, validando que nunca se aplique a una Trampa.
- Origen de la carta y zona actual: estar ahora en la mano no convierte una Trampa en carta del mazo principal.
- Requisitos de acceso desde el extradeck, separados de los costes y condiciones de activación.
- Dueño, orientación y destinatario de los efectos de un área.
- Perfil visual asociado al Relikario, separado del comportamiento de reglas.

«Sobre el Relikario» describe una ubicación o relación de una carta. No debe convertirse por sí sola en una séptima categoría principal: la categoría original y la ubicación deben poder conservarse por separado.

No se establecen aquí tamaños de mazo/extradeck, límites de copias ni nuevas restricciones de afinidad para ataques o equipos. Esos requisitos no fueron especificados en esta clasificación.

## Estado en el código

- **Categorías:** `CardKind` (`Attack`, `Skill`, `Code`, `Attachment`, `Equipment`, `Area`) y `CodeType` (`Play`, `Object`, `Character`, `Trap`) en `core/CardCatalog.cs`.
- **Afinidad:** `CardDefinition.Relikario` (nula para las cartas genéricas). `DeckRules.Compatible` la aplica al construir mazos y el motor, al enseñar y atacar.
- **Equipos:** el estado distingue un equipo normal y una colección de ligeros (`LightEquipment`).
- **Áreas:** se activan en la zona Bonus con su orientación (`ActivateArea`).
- **Trampas:** mazo propio (`TrainingTraps`) y creación por receta al canjear (`TrapCraft`).
- **Relikarios:** las habilidades se expresan con datos comunes de `RelikarioDefinition` y, cuando no bastan, con un parcial `GameEngine.<Nombre>.cs` ([Añadir un Relikario](ADDING_RELIKARIO.md)).

Pendiente: `CardKind.Attachment` mezcla una categoría con la ubicación «sobre el Relikario»; habría que separar la clasificación original de la relación que aplica un efecto.

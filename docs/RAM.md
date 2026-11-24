# Respuestas en cadena (RAM)

Cómo resuelve el motor las respuestas a una activación, comparado con las secciones 6.1 a 6.7 y A.2 del reglamento. Las velocidades son 8 RAM (cartas normales), 16 RAM (COMBO) y 32 RAM (Error de Carga).

## Lo que cumple

| Regla | Comportamiento del motor |
|---|---|
| 6.3: una carta de 8 RAM no responde. | Un Código que no es COMBO solo se juega en tu etapa de acciones, sin nada pendiente. |
| 6.3: solo 32 RAM responde a 32 RAM. | Con Error de Carga arriba de la cadena, el motor solo acepta otro Error de Carga. |
| 6.4: se resuelve en orden inverso y se omite lo cancelado. | La cadena de activación se recorre del último eslabón al primero, y cada Error que resuelve cancela el anterior. |
| 6.5: Error de Carga devuelve la acción y los pagos del Código. | Se devuelven la acción y los pagos que pide el propio texto del Código. |
| 6.6: se usa el resultado final del dado. | La ventana del dado (relanzamientos del Relikario, Colgante, PC, Destreza, Re-lanza, Agilidad) termina antes de aplicar el efecto. |
| A.2: los objetivos se eligen al resolver; sin objetivo válido, el efecto falla. | Huevo Azul, Bomba de Tiempo, Konamy, Disquete y las demás cartas con objetivo eligen al resolver. Si una respuesta deja una decisión sin opciones, el efecto termina sin efecto. |

## Decisiones de interpretación

- **Error de Carga cancela equipos.** Manda el texto de la carta («Cuando un código se esté activando o cuando un equipo esté siendo equipado: cancélalo y envíalo a la memoria»), aunque el reglamento (6.5) hable solo de Códigos. Lo comprueba la prueba «Error cancela equipo nuevo».
- **Rejugar un Código con Relikia es un efecto de Relikia.** Error de Carga no puede cancelarlo (6.5, A.2). El Código se resuelve al momento, con su coste de dos acciones y 400 PR y su corrupción. Caja de Cartón sí puede responder a un Código Objeto rejugado.
- **Error de Carga después del dado.** Cuando el dado de una activación de Código es definitivo (tras los relanzamientos y modificadores de su dueño), el rival con Error de Carga puede responder antes de que siga el efecto. Si lo anula, el resto del texto no se aplica y se devuelve la acción; lo que ya ocurrió, como los robos iniciales de Carga Rápida, se queda. Un contraerror deja continuar el efecto con ese dado. La ventana no se abre durante la ventana de modificadores del dueño, ni para un Código rejugado con Relikia, ni para tiradas que no son del Código (reciclaje, Jessica Lee en juego). La vista lo indica con `ErrorAfterDie`. Re-lanza relanza un dado que ya existía, así que su ventana sigue al nuevo resultado y va antes de su robo; solo se abre si queda robo (1, 2 o 3).

## Diferencias conocidas con el reglamento

| Regla | Qué hace el motor |
|---|---|
| 6.1 y 6.3: un COMBO de 16 RAM puede responder a la activación de un Código de 8 RAM. | La ventana de respuesta a una activación solo se abre si el rival tiene una carta que pueda usarla (Error de Carga, Combo Parry, Casco de Copias o una letra RELICK). Un COMBO cualquiera no responde a un Código. |
| 6.2: mientras el jugador activo conserva la prioridad, el rival espera. | El rival puede jugar sus COMBO en cualquier momento de las etapas abiertas del jugador activo, sin que haya nada a lo que responder. |
| 6.2: antes de pasar a la siguiente etapa se pasa la prioridad y el rival puede responder. | Avanzar de etapa no abre ninguna ventana para el rival. |
| 6.2: para responder a tu propia carta, primero pasas la prioridad; si el rival pasa, puedes añadir tu respuesta. | Cuando el rival pasa, la cadena se resuelve; quien activó no recibe la prioridad. |
| 6.1: con efectos simultáneos, el jugador activo añade primero los suyos, en el orden que elija. | Se ordenan por asiento (0 y después 1), sin elegir. Solo importa si hay sustituciones que decidir, como Reverse Shuffle. |
| 5.3 y 6.3: Súper Error de Carga (32 RAM). | No existe. |

Las cuatro primeras tienen la misma causa: el motor no tiene una secuencia de RAM general con prioridad, sino ventanas concretas (la cadena de activación, Caja de Cartón, la defensa y el dado). Corregirlas requeriría:

- **Una secuencia guardada en el estado**, donde cada entrada recuerde quién la activó, la carta, su velocidad y su paso.
- **Prioridad explícita**: después de cada activación, de una tirada o del final de una etapa decide un jugador; pasar entrega la prioridad y, cuando ambos pasan, se resuelve la última entrada.
- **La velocidad como dato de la carta** (8, 16 o 32 RAM), en lugar de deducirla de `IsCombo` y `CancelActivation`.
- **Un pase automático**, que las preferencias de COMBO actuales ya permiten.

Antes de construirlo hay que decidir cuánto se quiere preguntar en cada paso: una secuencia completa añade confirmaciones a la partida en línea.

## Presentación de la cadena

La vista pública (`DuelResolutionView`) incluye el ataque pendiente al que responde la cadena (`Attack`). Al resolverse, el motor publica un evento visual `Chain` con todos los eslabones en orden de activación y cuáles quedaron anulados; todas esas cartas ya eran públicas. El cliente dibuja la cadena como módulos de memoria numerados, con el arte, el color del dueño y su velocidad, y la recorre del último eslabón al primero marcando «RESUELTO» o «ANULADO»; el ataque queda como «CONTINÚA». Con movimiento reducido el resultado aparece de una vez. Solo se muestra con dos eslabones o más y nunca tapa los botones de respuesta.

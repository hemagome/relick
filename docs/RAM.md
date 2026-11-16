# Validación de la RAM

Comparación del motor con las secciones 6.1 a 6.7 y A.2 del reglamento, del 16 de noviembre de 2026. Cada punto marcado como discrepancia se comprobó ejecutando el motor, no solo leyendo el código. No se cambió ninguna regla.

## Lo que ya cumple

| Regla | Comportamiento del motor |
|---|---|
| 6.3: una carta de 8 RAM no responde. | Un Código que no es COMBO solo se juega en tu etapa de acciones, sin nada pendiente. |
| 6.3: solo 32 RAM responde a 32 RAM. | Con Error de Carga arriba de la cadena, el motor solo acepta otro Error de Carga; Estrella del Fontanero (16 RAM) se rechaza. |
| 6.4: se resuelve en orden inverso y se omite lo cancelado. | La cadena de Error de Carga se recorre del último al primero, y cada Error que resuelve cancela el anterior. |
| 6.5: Error de Carga devuelve la acción y los pagos del Código. | Se devuelven la acción y el coste en PR de la activación. El robo opcional de Huevo Azul se paga después de resolver, así que no hay nada que devolver; los duelos grabados con el coste anterior lo devuelven. |
| 6.6: se usa el resultado final del dado. | La ventana del dado (relanzamientos del Relikario, Colgante, PC, destreza, Re-lanza, Agilidad) termina antes de aplicar el efecto. |
| A.2: los objetivos se eligen al resolver; sin objetivo válido, el efecto falla. | Huevo Azul, Bomba de Tiempo, Konamy y Disquete eligen al resolver. Si una respuesta deja una decisión sin opciones, el efecto termina sin efecto en lugar de quedarse esperando. |

## Discrepancias

| # | Regla | Qué hace el motor | Comprobación |
|---|---|---|---|
| 1 | 6.1 y 6.3: un COMBO de 16 RAM puede responder a la activación de un Código de 8 RAM. | Solo existe ventana de respuesta si el rival tiene Error de Carga, y durante ella solo se acepta Error de Carga. Sin Error, el Código se resuelve al instante. | Senshuni contra un rival con Resetear la Mente: se resuelve sin ventana. Con Error en la mano del rival, Resetear se rechaza. |
| 2 | 6.5, 6.6 y A.2: tras ver el dado de un Código, se puede responder con Error de Carga antes de continuar. | Pasar la primera ventana la cierra para siempre; tras el dado, el rival no puede jugar Error. | Vida Extra: después de pasar y ver el dado, `CanPlayLoadError` es falso. |
| 3 | 6.2: mientras el jugador activo conserva la prioridad, el rival espera. | El rival puede jugar sus COMBO en cualquier momento de las etapas abiertas del jugador activo, sin que haya nada a lo que responder. | Resetear la Mente del rival está permitido durante las acciones del jugador activo. |
| 4 | 6.2: antes de pasar a la siguiente etapa se pasa la prioridad y el rival puede responder. | Avanzar de etapa no abre ninguna ventana para el rival. | Avanzar desde Acciones cambia de etapa al momento. |
| 5 | 6.2: si quieres responder a tu propia carta, primero pasa la prioridad; si el rival pasa, puedes añadir tu respuesta. | Cuando el rival pasa, la cadena se resuelve; quien activó no recibe la prioridad. | Código propio, el rival pasa: se resuelve sin preguntar al activador. |
| 6 | 6.1: con efectos simultáneos, el jugador activo añade primero los suyos, en el orden que elija. | Se ordenan por asiento (0 y después 1), sin elegir. Solo importa si hay sustituciones que decidir, como Reverse Shuffle. | Dado de ataque 1 en el turno del asiento 1: se destruye primero el Casco del asiento 0. |
| 8 | 6.5 y A.2: Error de Carga no cancela un efecto de relikia, y solo devuelve la acción y los pagos del texto. | Un Código rejugado desde la memoria con Relikia puede cancelarse, y se devuelven las dos acciones y los 400 PR del coste de Relikia. | Prueba existente: «Konamy: Relikia corrompe el código, cobra dos acciones y 400 PR, y Error los devuelve». Hay que decidir si rejugar con Relikia es activar el Código o un efecto de relikia. |
| 9 | 5.3 y 6.3: Súper Error de Carga (32 RAM). | No existe. | Pendiente de implementar con su receta y sus devoluciones completas. |
| 10 | 6.7: clonar un efecto. | Ninguna carta clona todavía. | No aplica. |

## Decisiones tomadas

- **Error de Carga cancela equipos** (antes, discrepancia 7). Decidido el 16 de noviembre de 2026: manda el texto de la carta («antes de que un código ejecute su efecto o un equipo se equipe»), aunque el reglamento (6.5) hable solo de Códigos. El motor ya lo hacía así y lo comprueba la prueba «Error cancela equipo nuevo». No se cambió código.

## Qué haría falta

Las discrepancias 1 a 5 tienen la misma causa: el motor no tiene una secuencia de RAM general con prioridad, solo ventanas sueltas (la cadena de Error, Caja de Cartón, la defensa y el dado). Corregirlas significa:

- **Una secuencia guardada en el estado.** Cada entrada recuerda quién la activó, la carta, su velocidad y en qué paso está.
- **Prioridad explícita.** Después de cada activación, de una tirada o del final de una etapa, decide un jugador; pasar entrega la prioridad, y cuando ambos pasan se resuelve la última entrada.
- **Velocidad como dato de la carta.** 8, 16 o 32 RAM, en lugar de deducirla de `IsCombo` y `CancelActivation`.
- **Un ajuste automático para el pase.** Las preferencias de COMBO actuales ya sirven para que no haya que confirmar cada pase a mano.

Las decisiones genéricas ya facilitan una parte: una tirada de una carta migrada pasa por un único punto (`EffectContext.Roll`), así que la ventana de Error tras el dado se añadiría ahí una sola vez para todas esas cartas.

Antes de construirlo hay que decidir la discrepancia 8, y cuánto se quiere preguntar en cada paso. Una secuencia completa añade confirmaciones a la partida en línea.

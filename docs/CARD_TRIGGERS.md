# Disparadores de carta

Diseño para que los módulos de `core/Effects/` cubran también los **disparadores**, no solo las activaciones. Continúa [Efectos de carta](CARD_EFFECTS.md): allí cada carta responde a «qué hace al activarse»; aquí, a «qué hace cuando ocurre algo en la partida».

## Problema

Hoy cada momento de la partida en el que una carta puede intervenir es una cadena escrita a mano en el motor, que nombra cada carta:

```csharp
// GameEngine.DealDamage, simplificado
if (PreventDamageWithStar(p, amount, completed, attacker)) return;
if (protection is { PreventDamage: true } or { DamageReduction: > 0 }) amount = ...;
if (!TryTurboSwordDefense(target, amount, attackDie, completed))
    OfferPurpleRingDamage(target, ReduceDamageWithRobotize(target, amount, attackDie), attackDie, completed);
```

El mismo patrón se repite en el inicio del turno (Moto-Ratón), el inicio de las acciones (Perro Fantasma → Robotizar), la etapa final (Zahira Flair), la destrucción (Escudo Galáctico → Tótem de Oro → Reverse Shuffle) y la sustitución de efectos rivales (Súper Imán). A eso se suman tres despachos por identificador:

- `DecisionChoices` y `ApplyDecision` en `Decisions.cs`: Casco de Copias, Era Futurista, Moto-Ratón, Fantasma Ladrón, Robo de Equipos, Agujero Negro, Ironwing, Anillo Morado y Osamu.
- El `switch` de títulos de dado en `RollWithRelikario`.
- Las fases propias de cada disparador (`TotemChoice`, `ZahiraTarget`…), que se tratan en la fase B2.

Consecuencias:

- Una carta nueva con disparador obliga a editar el motor en uno o varios de estos puntos.
- El orden de resolución está implícito en el orden de las líneas, sin nombre.
- La lógica de una carta queda repartida entre su efecto, su `GameEngine.X.cs` y `EffectContext`.

## Requisitos

1. **Mismo resultado, byte a byte.** Los duelos guardados y las repeticiones (`DuelHistory`) se reproducen con el motor actual. Ningún paso de la migración puede cambiar el orden de los disparadores, el consumo de azar ni el texto del registro.
2. **Pausa y reanudación serializables.** Un disparador puede pedir una decisión o lanzar un dado. La cadena debe poder guardarse en ese punto y continuar después de reconectar, como ya hacen `EngineContinuation` y `PendingDecision`.
3. **Orden explícito.** El orden entre familias de disparadores lo fija el motor según las reglas, no el orden de registro de las cartas.
4. **Interfaces pequeñas.** Igual que en la fase A, una carta implementa solo los disparadores que usa.
5. **Motor puro.** Sin Godot, sin reflexión ni descubrimiento dinámico: el registro sigue siendo la lista explícita de `CardEffects`.

## Diseño

### 1. Familias de disparadores como interfaces

Cada familia es una interfaz opcional que una carta registrada implementa además de sus reglas. La base es `CardRules` (`CardId`, `Choices`, `Choose`, `Continue`). `CardEffect` la extiende con la activación (`Unavailable`, `Resolve`) y solo la usan las cartas que el jugador activa. Un equipo o un Área que solo reaccionan derivan de `CardRules`, así que el motor nunca los resuelve como un código:

```csharp
/// Momentos del turno en los que una carta en juego puede intervenir. El orden del enum es el orden de las reglas.
internal enum TurnMoment { TurnStart, DrawStage, ActionStart, FinalStage }

internal interface ITurnTrigger
{
    TurnMoment Moment { get; }
    /// Zonas desde las que dispara (Perro Fantasma: mano revelada; Robotizar: sobre el Relikario).
    IReadOnlyList<FieldZone> Zones { get; }
    bool Applies(GameEngine.EffectContext ctx, FieldTarget source);
    /// Debe terminar llamando a ctx.Continue(then), ya sea enseguida o después de una decisión o un dado.
    void Fire(GameEngine.EffectContext ctx, FieldTarget source, EngineContinuation then);
}

/// Pasos de la cadena de daño, en el orden en que se aplican hoy.
internal enum DamageStep { Amplify, Prevent, Reduce, Respond, Absorb, AfterShields, Lethal }

internal interface IDamageTrigger
{
    DamageStep Step { get; }
    IReadOnlyList<FieldZone> Zones { get; }
    /// Síncrono: devuelve el daño modificado (Robotizar resta 1 por copia).
    int Modify(GameEngine.EffectContext ctx, FieldTarget source, DamageEvent damage) => damage.Amount;
    /// Asíncrono: true si toma el control y continuará con `then` (Anillo Morado, Escudo Galáctico, Tótem).
    bool TryReplace(GameEngine.EffectContext ctx, FieldTarget source, DamageEvent damage, EngineContinuation then) => false;
}

internal enum DestructionStep { Protect, Cover, Sacrifice }

internal interface IDestructionReplacement
{
    DestructionStep Step { get; }
    IReadOnlyList<FieldZone> Zones { get; }
    bool TryReplace(GameEngine.EffectContext ctx, FieldTarget source, FieldTarget target, EngineContinuation? then);
}
```

`DamageEvent` agrupa lo que hoy viaja en parámetros sueltos (`target`, `amount`, `attackDie`, `source`, `completed`). Así un paso nuevo no cambia la firma de todos los demás.

Los títulos de dado no van en los efectos: su clave natural es el propósito del dado (`DiePurpose`). Turbo Sword tiene dos propósitos, y Reverse Shuffle o Jessica Lee no tienen módulo. `core/DiePurposes.cs` reúne en una tabla el título y la carta de cada propósito, en lugar de los dos `switch` que había en `RollWithRelikario` y en la vista de resolución de `DuelHistory`.

Las decisiones que abre un disparador ya llevan `Source = card.Id`. Con la carta registrada, `DecisionChoices` y `ApplyDecision` las envían a `Choices` y `Choose` del efecto sin cambios, y la cadena de `if` desaparece carta a carta.

### 2. Fuentes en juego

Una sola consulta del motor enumera las fuentes activas de un jugador, en un orden fijo:

```csharp
private IEnumerable<(FieldTarget Source, CardEffect Effect)> ActiveSources(int seat, IReadOnlyList<FieldZone> zones)
```

- Recorre las zonas en el orden de `FieldZone` y, dentro de cada zona, los espacios en orden ascendente.
- Omite las cartas con efectos suprimidos (`EffectsSuppressed`, Zahira Flair).
- Cada carta aparece una vez por espacio. Dos copias de Robotizar son dos fuentes, y eso sustituye a `ActiveRobotizeCopies().Count()`.

Es la misma regla que hoy aplica cada carta por su cuenta (`ActiveRobotizeCopies`, `ChargedGalacticShields`, la búsqueda de Tótem de Oro en `RequestDestruction`). Centralizarla quita esa repetición.

### 3. Ejecución de una cadena

> **Cambio al implementarlo (paso 3).** La instantánea de fuentes que proponía la primera versión no sirve para el inicio de acciones. Las copias reveladas de Perro Fantasma son índices de la mano, que se desplazan cuando una carta sale de ella; `PlayerState.GhostDogReveals` ya los desplaza con `ShiftHandSlots`, y una lista aparte de `FieldTarget` no lo haría. Por eso cada disparador guarda su propio avance, como ya hacían las dos cartas, y el ejecutor solo decide el orden entre ellas.

La interfaz implementada es más simple que la del apartado 1:

```csharp
internal enum TurnMoment { ActionStart }               // se añade un valor por momento migrado
internal interface ITurnTrigger
{
    TurnMoment Moment { get; }
    int Order { get; }                                  // fijado por las reglas
    bool Fire(GameEngine.EffectContext ctx);            // false si no tiene nada que hacer ahora
}
```

`GameEngine.TurnTriggers.cs` resuelve un momento así:

1. `RunTurnTriggers(moment, seat, from)` dispara los disparadores del momento, en su `Order`, hasta que uno devuelve `true`: ha empezado algo que volverá más tarde.
2. **Vuelta por continuación** (Robotizar): el disparador encadena sus pasos con `ctx.Then(...)`. Al terminar, llama a `ctx.FinishTurnTrigger(moment)`, que dispara los siguientes y, si ninguno empieza nada, vuelve a `CompletePendingPlay`.
3. **Vuelta por activación** (Perro Fantasma): cada copia se resuelve como una activación completa, que vuelve por `CompletePendingPlay`, no por una continuación. Para eso existe `State.ActionStartActor`: mientras está puesto, `CompletePendingPlay` vuelve a ejecutar el momento desde el primer disparador. El ejecutor lo borra en cuanto un disparador no tiene nada que hacer, igual que antes lo borraba Perro Fantasma.
4. **Límite:** la reentrada empieza siempre por el primer disparador. Por eso un disparador que ya terminó debe devolver `false` si lo vuelven a disparar. Hoy solo el primero (Perro Fantasma) depende de la reentrada. Si otro disparador posterior la necesitara, habría que guardar también su posición.

No hizo falta estado nuevo en la partida: `ActionStartActor` ya existía, y el avance de Robotizar viaja en su continuación (`Count`).

El daño y la destrucción usan el mismo ejecutor, pero sin instantánea entre pasos. Cada paso de `DamageStep` consulta sus fuentes en el momento, porque un paso anterior puede haber cambiado el campo (un Tótem destruido por contadores). Es lo que hacen hoy las llamadas encadenadas.

### 4. Compatibilidad

- **Continuaciones antiguas.** `RobotizeActionStart`, `GoldenTotemReplacement`, `BikeTurnStart` y las demás siguen en el `switch` de `EngineContinuations.cs` como adaptadores que reanudan la cadena nueva desde el punto equivalente. No se borran ni se reordenan valores de `ContinuationKind` ni de `Phase`, porque los duelos guardados los serializan.
- **Estado nuevo opcional.** Si un paso necesita estado nuevo en la partida, se omite al serializar cuando está vacío, igual que `Sleeve` o `Dice` en `PlayerState`. Así un duelo sin disparadores pendientes guarda exactamente el mismo JSON que antes. El paso 3 no necesitó ninguno.
- **Orden actual como especificación.** Antes de migrar cada familia se añade una prueba que fija el orden actual con varias cartas a la vez: el registro y el estado tras cada paso. La migración debe pasarla sin cambiarla.

## Orden actual que el diseño conserva

**Inicio de acciones:** Perro Fantasma (cartas reveladas en mano, de menor a mayor índice), después Robotizar (copias sobre el Relikario).

**Daño**, en `DealDamage` y sus continuaciones:

| Paso | Hoy | `DamageStep` |
|---|---|---|
| 1 | Límite de daño no letal | (motor, antes de la cadena) |
| 2 | Estrella del Fontanero del atacante: duplica | `Amplify` |
| 3 | Estrella del Fontanero del defensor: daño 0 | `Prevent` |
| 4 | Equipo con `PreventDamage` o `DamageReduction` | `Reduce` |
| 5 | Turbo Sword: dado de defensa | `Respond` |
| 6 | Robotizar: −1 por copia | `Reduce` (después del equipo) |
| 7 | Anillo Morado: decisión | `Absorb` |
| 8 | Escudo Galáctico: carta a mano, −2 | `Absorb` (después de Anillo) |
| 9 | Contadores de equipo (`DamageCounterLimit`) | `AfterShields` |
| 10 | Tótem de Salvación: cancelar daño letal | `Lethal` |

Los pasos 4 y 6 caen en `Reduce` pero en momentos distintos, con Turbo Sword entre ellos. Por eso cada paso necesita además un orden secundario explícito (`int Order`), en lugar de depender del orden de registro. La prueba de orden del punto 4 lo fija.

**Destrucción:** Escudo Galáctico (`Protect`) → Tótem de Oro sobre un Tótem de Salvación (`Cover`) → Reverse Shuffle (`Sacrifice`, decisión del dueño). Súper Imán actúa antes, al sustituir un efecto rival (`TryReplaceOpponentFieldEffect`), y se queda como un punto aparte: no es una destrucción, sino un efecto rival sustituido.

## Plan de migración

Cada paso es un commit propio, con compilación del juego y del servidor, los tests del motor y las pruebas de repetición (`SnapshotTests`, `ReplayQueryTests`, `DuelHistoryTests`).

1. ~~**Despacho de decisiones por efecto.**~~ Hecho para Moto-Ratón, Fantasma Ladrón y Era Futurista, que derivan de `CardRules`. Sus opciones y respuestas solo necesitaron operaciones genéricas del contexto: `DrawAtTurnStart`, `GiveHandCard`, `CompletePendingPlay`, `EffectsSuppressed` y `ActivateOutOfGameCopy`. Las demás siguen en el despacho de `Decisions.cs` hasta su paso, porque sus respuestas forman parte de otra cadena:
   - Anillo Morado: la cadena de daño (paso 6).
   - Robo de Equipos y Agujero Negro: la entrada de un Área en juego, con varias destrucciones encadenadas.
   - Casco de Copias: la secuencia de RAM, fuera de alcance.
   - Ironwing y Osamu: son Relikarios, no cartas, y necesitan un registro equivalente para Relikarios.
2. ~~**Títulos de dado.**~~ Hecho con `DiePurposes`, una tabla por propósito en lugar de un miembro de cada efecto (ver arriba). Los títulos y la carta mostrada no cambian. Agujero Negro y Perro Fantasma siguen sin nombrar carta desde el propósito, como antes.
3. ~~**Infraestructura y `ActionStart`.**~~ Hecho: `ITurnTrigger`, `CardEffects.TurnTriggers` y el ejecutor de `GameEngine.TurnTriggers.cs` (ver el apartado 3). Perro Fantasma (`Order = 0`) y Robotizar (`Order = 1`) disparan desde su módulo, y toda la cadena de la carta del tope de Robotizar vive ya en `RobotizeEffect`. `RobotizeActionStart` queda como adaptador hacia el módulo. `TurnTriggerTests` fija el orden y comprueba que un duelo guardado con la continuación anterior se reanuda. `ActiveSources` se pospone: de momento basta `ActiveAttachedCopies`, que sustituye a la búsqueda propia de Robotizar.
4. ~~**`TurnStart` y `FinalStage`.**~~ Hecho:
   - **Moto-Ratón** dispara en `TurnStart`. La caducidad de los equipos con `DestroyAtNextTurnStart` sigue en el motor, antes de los disparadores; se moverá con los indicadores de equipo del paso 6. Su respuesta lanza ella misma el robo, porque el número de cartas depende de la opción elegida. Un disparador posterior en `TurnStart` necesitaría que esa carta extra se transmitiera.
   - **Zahira Flair** (`CardRules`, sin activación) dispara en `FinalStage` con el mismo modelo de reentrada que Perro Fantasma, usando `ZahiraFinalSlot` como su avance. El resultado del dado se sigue resolviendo en el motor por su propósito (`RollResolvedByEngine`).
   - `BikeTurnStart` queda como adaptador hacia el inicio de turno y desaparece `GameEngine.MouseBike.cs`.
   - **Mira de Sniper no es un momento del turno**, sino un evento («carta descartada») con su lote de cartas, lanzado desde cuatro caminos de descarte y con su propia fase (`SniperChoice`). Irá en una familia propia (`IDiscardTrigger`) cuando se retire esa fase en la fase B2.
5. **Destrucción.** Escudo Galáctico, Tótem de Oro y Reverse Shuffle. Requiere antes la suite de UI en verde: cambia lo que la UI recibe durante una destrucción protegida.
6. **Daño.** Estrella del Fontanero, Turbo Sword, Robotizar, Anillo Morado, Escudo Galáctico y Tótem. Los indicadores `PreventDamage`, `DamageReduction` y `DamageCounterLimit` de `CardDefinition` pasan a los efectos de sus equipos, lo que cierra el punto 5 de la revisión DRY para esta familia. Es el paso de más riesgo y va el último.

Al terminar, una carta nueva con disparador será un archivo en `core/Effects/` y una línea en `CardEffects`. `EffectContext` perderá los métodos propios de Escudo Galáctico, Tótem de Oro, Combo Parry y la carta del tope de Robotizar.

## Fuera de alcance

- **Respuestas COMBO y ventana de Error de Carga** (RAM). Ya tienen su propia secuencia ([RAM](RAM.md)) y no son disparadores de campo.
- **La ventana de dado** (Colgante, PC, destreza, Re-lanza, Agilidad). Es otra cadena con su propio orden. Se puede tratar con el mismo ejecutor más adelante, pero no es necesaria para los pasos anteriores.
- **Reglas desde datos.** Este diseño mantiene las reglas en código. Expresar cartas como datos es una decisión aparte que [Evaluación del motor](RULES_ENGINE_REVIEW.md) deja abierta.

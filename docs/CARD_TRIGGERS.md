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

El motor resuelve un momento con un único ejecutor:

1. Al llegar al momento, toma una **instantánea** de las fuentes que aplican y la guarda en el estado:
   ```csharp
   public sealed class PendingTriggers { public TurnMoment Moment; public int Seat; public List<FieldTarget> Sources = []; }
   ```
   La instantánea respeta lo que ya hace Robotizar: cuenta sus copias al empezar y no las vuelve a contar a mitad.
2. Dispara la primera fuente y le pasa `then = new EngineContinuation { Kind = ContinuationKind.NextTrigger }`.
3. `NextTrigger` quita la fuente resuelta y dispara la siguiente. Una fuente que ya no está en su espacio, porque se destruyó a mitad de la cadena, se salta sin disparar, igual que hoy Perro Fantasma comprueba `PlayableHandCard` antes de resolver.
4. Con la lista vacía, el motor sigue con su flujo normal: `CompletePendingPlay` en el inicio de acciones, el robo en el inicio de turno.

El daño y la destrucción usan el mismo ejecutor, pero sin instantánea entre pasos. Cada paso de `DamageStep` consulta sus fuentes en el momento, porque un paso anterior puede haber cambiado el campo (un Tótem destruido por contadores). Es lo que hacen hoy las llamadas encadenadas.

### 4. Compatibilidad

- **Continuaciones antiguas.** `RobotizeActionStart`, `GoldenTotemReplacement`, `BikeTurnStart` y las demás siguen en el `switch` de `EngineContinuations.cs` como adaptadores que reanudan la cadena nueva desde el punto equivalente. No se borran ni se reordenan valores de `ContinuationKind` ni de `Phase`, porque los duelos guardados los serializan.
- **Estado nuevo opcional.** `PendingTriggers` se omite al serializar cuando es `null`, igual que `Sleeve` o `Dice` en `PlayerState`. Así un duelo sin disparadores pendientes guarda exactamente el mismo JSON que antes.
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
3. **Infraestructura y `ActionStart`.** `ActiveSources`, `PendingTriggers`, `ContinuationKind.NextTrigger` y la interfaz `ITurnTrigger`. Migrar Perro Fantasma y Robotizar, con `RobotizeActionStart` como adaptador. Son dos cartas ya parcialmente en su efecto, y validan el ejecutor completo, incluidas la pausa y la reanudación.
4. **`TurnStart` y `FinalStage`.** Moto-Ratón (con `BikeTurnStart` como adaptador) y Zahira Flair. Mira de Sniper dispara al descartar, no en un momento del turno. Se decide aquí si entra como un momento más de `TurnMoment` o como una familia propia, según lo que muestre su prueba de orden.
5. **Destrucción.** Escudo Galáctico, Tótem de Oro y Reverse Shuffle. Requiere antes la suite de UI en verde: cambia lo que la UI recibe durante una destrucción protegida.
6. **Daño.** Estrella del Fontanero, Turbo Sword, Robotizar, Anillo Morado, Escudo Galáctico y Tótem. Los indicadores `PreventDamage`, `DamageReduction` y `DamageCounterLimit` de `CardDefinition` pasan a los efectos de sus equipos, lo que cierra el punto 5 de la revisión DRY para esta familia. Es el paso de más riesgo y va el último.

Al terminar, una carta nueva con disparador será un archivo en `core/Effects/` y una línea en `CardEffects`. `EffectContext` perderá los métodos propios de Escudo Galáctico, Tótem de Oro, Combo Parry y la carta del tope de Robotizar.

## Fuera de alcance

- **Respuestas COMBO y ventana de Error de Carga** (RAM). Ya tienen su propia secuencia ([RAM](RAM.md)) y no son disparadores de campo.
- **La ventana de dado** (Colgante, PC, destreza, Re-lanza, Agilidad). Es otra cadena con su propio orden. Se puede tratar con el mismo ejecutor más adelante, pero no es necesaria para los pasos anteriores.
- **Reglas desde datos.** Este diseño mantiene las reglas en código. Expresar cartas como datos es una decisión aparte que [Evaluación del motor](RULES_ENGINE_REVIEW.md) deja abierta.

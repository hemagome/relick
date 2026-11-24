# Añadir un Relikario

Guía para que un desarrollador o un agente de IA programe un Relikario nuevo, con su evolución, sus poderes, sus cartas, su arte, sus animaciones y su música. Los Relikarios ya programados (Yuno, Osamu, Ironwing, WD y Yuno Oro) sirven de referencia en cada paso; el commit que habilitó a Ironwing (`e4f5a81`) y el de su retrato (`69de641`) muestran el recorrido completo.

Lee también la [clasificación de cartas](CARD_TAXONOMY.md#relikarios-y-afinidad-de-habilidades) y [Añadir una carta](ADDING_CARD.md): la mayor parte del contenido de un Relikario son cartas.

## 1. Qué pedir al diseñador antes de empezar

Reúne esto y confirma las dudas antes de programar. Un Relikario mal especificado obliga a rehacer reglas, arte y pruebas.

**Reglas del Relikario**
- Nombre completo y nombre corto para los avisos («Relanzar con Yuno»).
- Por nivel (Lv 1, Lv 2…): vidas máximas, texto exacto de su habilidad y si la habilidad es pasiva, una opción del jugador o una respuesta.
- Evolución: coste en PR, condición adicional (Ironwing necesita Agujero Negro como Área activa), qué ocurre al evolucionar (curación, búsqueda, tope de vidas) y si cambia el D6 de ataque.
- Interacciones dudosas con cartas existentes, como dados, respuestas COMBO o daño.

**Cartas propias**
- Habilidades específicas de este Relikario: texto, daños normal y crítico, premio y si son «S». Cada una se programa como una carta ([Añadir una carta](ADDING_CARD.md)).
- Ataques: los códigos impresos de cada ilustración de Ataque del Relikario (por ejemplo UT-22/50, DT-80/100 y NS-49/150) y cuál es la ilustración por defecto en un mazo nuevo.
- Otras cartas que solo tienen sentido con él (como Agujero Negro para Ironwing).

**Arte y audio**
- Escaneos de las cartas del Relikario por nivel y de cada carta propia, con su marca de agua (no se elimina).
- Referencias de la figura del tablero: pose, rasgos que deben conservarse y objetos que se animan por separado (la espada de Osamu, los cañones de Ironwing).
- Cómo ataca en pantalla: proyectil recto, en arco o láser. También cómo encaja un golpe y cómo se ve a cero vidas.
- Carácter de su música: tempo, instrumentos y ambiente del combate y del clímax.

## 2. Definición y evolución

En [core/RelikarioCatalog.cs](../core/RelikarioCatalog.cs):

1. Crea un `RelikarioDefinition` por nivel:
   - El nivel 1 lleva `EvolvesTo = "<id>-lv2"`.
   - El nivel 2 lleva `Level: 2`, `BaseForm = "<id>"` y su `EvolutionCost`.
   - Los ids son estables y en minúsculas. El de la forma base (`Family`) da nombre a la carpeta de arte, a los temas musicales y a la afinidad de las cartas.
2. Usa los datos comunes antes de escribir código:
   - relanzamientos: `CanRerollAttack`, `MaxRerolls`, `CanRerollAnyDie` y `ConvertRerolledSix`;
   - evolución: `EvolutionHealing` y `EvolutionMaxHealth`;
   - bonificación del dado de ataque: `AttackDieBonusExceptOne` y `AttackDieBonusIncludingOne`.
   
   Las fichas de destreza iniciales se derivan de las vidas (`StartingDexterity`).
3. Añade las formas a `All` y la forma base a `Playable`. En `Roster` (la lista del constructor de mazos), dale la definición a su entrada. Una entrada sin definición se muestra como «próximamente».

## 3. Poderes que no son datos

Si la habilidad necesita lógica propia, va en un archivo parcial del motor, `core/GameEngine.<Nombre>.cs`, como [GameEngine.Osamu.cs](../core/GameEngine.Osamu.cs) o [GameEngine.Ironwing.cs](../core/GameEngine.Ironwing.cs):

- **Acciones que el jugador inicia** (la búsqueda de Osamu Lv 2, los modificadores de dado de Ironwing): un valor en `DuelMoveKind` ([core/DuelProtocol.cs](../core/DuelProtocol.cs)), su comando en el motor, su opción en [core/DuelOptions.cs](../core/DuelOptions.cs) y su texto en el historial ([core/DuelHistory.cs](../core/DuelHistory.cs)).
- **Elecciones**: decisiones con `Source = <id del Relikario>`; el título sale del catálogo. Hoy `DecisionChoices` y `ApplyDecision`, en [core/Decisions.cs](../core/Decisions.cs), las reparten por identificador hacia Osamu e Ironwing. Los Relikarios aún no tienen un registro de módulos como las cartas, así que añade allí tu rama. Si lo generalizas, mejor.
- **Reacciones a momentos del juego**: reutiliza las cadenas de disparadores (`ITurnTrigger`, `IDamageTrigger`, `IFieldLeaveReplacement`) descritas en [Disparadores](CARD_TRIGGERS.md), en lugar de llamadas fijas.
- **Evolución con condiciones o efectos propios**: [core/GameEngine.Evolution.cs](../core/GameEngine.Evolution.cs).

## 4. Cartas del Relikario

- **Habilidades específicas**: `CardDefinition` con `Relikario = RelikarioCatalog.<Nombre>.Id`. Las reglas del mazo ([core/DeckRules.cs](../core/DeckRules.cs)) solo las admiten con ese Relikario, y el motor solo permite enseñarlas y atacar con ellas si son compatibles.
- **Ilustraciones de Ataque**: variantes `Attack with { Id = "attack-<id>-<código>", ArtworkLabel = "<Nombre> (<código>)", Relikario = … }` en [core/CardCatalog.cs](../core/CardCatalog.cs), cada una con su imagen `assets/cards/attack-<id>-<código>-clean.png`. Comparten la regla del Ataque.
- **Ataque por defecto**: si la ilustración por defecto no es la clásica, indícala con `DefaultAttackArtId` en la definición de la forma base, como Ironwing, WD y Yuno Oro. `DeckRules.DefaultAttackArt` y `BoardArt.LoadCardArtwork` ([scripts/BoardWidgets.cs](../scripts/BoardWidgets.cs)) la leen de ahí, sin casos especiales. Otra opción: un `attack-card.png` en la carpeta del personaje sustituye la ilustración del Ataque clásico de ese Relikario, como hace Osamu.

## 5. Arte

> **Permisos antes que píxeles.** Las cartas, retratos y figuras de los personajes de Relick son propiedad intelectual de terceros y no están cubiertos por la licencia del código ([NOTICE.md](../NOTICE.md)). Añade solo imágenes que quien las aporta tenga derecho a distribuir, documenta la autorización en el `README.md` de la carpeta y conserva la marca de agua: no la quites ni retoques las imágenes con herramientas de edición generativa (`image_gen` u otras). Sin permiso, las reglas del Relikario pueden programarse igual. `RelikarioArt.Load` devuelve `null` cuando falta un archivo, pero comprueba en las pruebas de interfaz que el retrato, el selector y la evolución se presentan sin él, y anota el hueco en [docs/PUBLICATION_AUDIT.md](PUBLICATION_AUDIT.md).

Todo el arte del personaje va en `assets/characters/<id base>/`. [scripts/RelikarioArt.cs](../scripts/RelikarioArt.cs) lo busca por nombre de archivo, y cada forma usa la carpeta de su forma base.

| Archivo | Uso | Medida |
|---|---|---|
| `card-lv1.png`, `card-lv2.png` | Carta del Relikario por nivel: inspector, constructor y evolución | 697 × 987 |
| `portrait.png` | Retrato cuadrado del selector de Relikarios | cuadrado |
| `thumbnail.png` | Miniatura junto a los mazos (si falta, se usa el retrato) | cuadrado |
| `sprite.png` | Figura del tablero, si se anima con un rig | transparente |
| `attack-projectile.png` | Proyectil del ataque | transparente |
| `attack-card.png` | Ilustración alternativa del Ataque clásico | 1054 × 1492 |

Documenta en un `README.md` de la carpeta la procedencia de cada imagen: quién la aportó, con qué permiso y qué transformación no generativa se le aplicó (recorte, escala). Las imágenes derivadas de una carta, como el retrato, conservan la marca de agua de la carta de la que salen. Las figuras y animaciones del tablero deben ser arte aportado con permiso o arte original del proyecto, no reconstrucciones generativas de las ilustraciones oficiales. Los prompts de `image_gen` que aún aparecen en los `README.md` de Yuno, Osamu, WD y Yuno Oro describen cómo se hicieron sus retratos y animaciones; son un registro histórico, no un procedimiento a seguir.

## 6. Animaciones del tablero

[scripts/RelikarioPortrait.cs](../scripts/RelikarioPortrait.cs) admite dos enfoques. Ambos deben cubrir todos los movimientos de `PortraitMotion`: reposo, ataque, golpe, golpe normal, golpe crítico, curación y reposo a cero vidas.

**Hojas de fotogramas** (Yuno). Cada clip es un PNG con columnas de 8 fotogramas de 608 × 504 y fondo transparente:

| Archivo | Fotogramas | Duración | Bucle |
|---|---|---|---|
| `idle-rest.png` | 1 (se mueve con un shader) | 3,6 s | sí |
| `attack.png` | 56 | 1,2 s | no |
| `hit.png` | 64 | 0,9 s | no |
| `hit-normal.png` / `hit-critical.png` | 16 | 0,9 s / 1,2 s | no |
| `heal.png` | 16 | 1,2 s | no |
| `exhausted-idle.png` | 16 (ida y vuelta) | 2,8 s | sí |

Con el lienzo y la duración de la tabla no hace falta código: los clips se cargan por nombre.

**Rig procedural** (Osamu, Ironwing). Una o varias capas (`sprite.png` más las piezas móviles) se animan por código según el tiempo transcurrido. Crea `scripts/<Nombre>PortraitRig.cs` siguiendo [OsamuPortraitRig](../scripts/OsamuPortraitRig.cs) o [IronwingPortraitRig](../scripts/IronwingPortraitRig.cs), y conéctalo en `LoadClips()` de `RelikarioPortrait`. Respeta `ReducedMotion` (movimiento reducido) y `FlipHorizontal` (asiento rival).

**Proyectil del ataque**: la salida y la trayectoria se eligen en [scripts/ArenaEffects.cs](../scripts/ArenaEffects.cs). Por defecto es recta; Osamu lanza en arco e Ironwing dispara un láser desde sus cañones. Añade tu caso ahí si no es el recto.

La animación de evolución ([scripts/EvolutionReveal.cs](../scripts/EvolutionReveal.cs)) usa las cartas `card-lv*.png` y no necesita nada más.

## 7. Música

Cada rival tiene un tema de combate y otro de clímax (cuando alguien llega a cero vidas). Son composiciones originales generadas por código:

1. Compón ambos en [tools/audio/score.cpp](../tools/audio/score.cpp), siguiendo los temas existentes. Comparten re como centro tonal, y el clímax retoma el motivo del combate.
2. Añade `<id>-combat` y `<id>-climax` a la lista de [tools/audio/build.sh](../tools/audio/build.sh), con su tempo y su compás en [tools/audio/check.py](../tools/audio/check.py), y ejecuta `tools/audio/build.sh` (necesita g++, ffmpeg y Python 3).
3. Registra los clips en [assets/audio/music.tres](../assets/audio/music.tres) con los nombres `Combat:<id>` y `Climax:<id>`. Opcionalmente añade los puentes `ToCombat:<id>` y `ToClimax:<id>`, como Yuno. `MusicDirector` elige el tema por la familia del rival.
4. Documenta título, tempo y duración en [assets/audio/README.md](../assets/audio/README.md) y [MUSIC.md](MUSIC.md).

## 8. Pruebas

- **Motor**: `tests/<Nombre>Tests.cs`, añadido a `tests/Program.cs`, como [IronwingTests](../tests/IronwingTests.cs). Cubre:
  - habilidades por nivel;
  - evolución (coste, condiciones, curación, tope de vidas);
  - las elecciones y su recuperación tras guardar y recargar;
  - la privacidad;
  - las reglas del mazo con sus cartas propias (en `tests/DeckRulesTests.cs`).
- **Interfaz** (`scripts/Tests/`):
  - el retrato y el ataque en los cuatro tamaños y en ambos asientos (ver `CheckOsamuPresentation` en `OnlineArena.OsamuPresentationTests.cs` y las comprobaciones de Ironwing en `OnlineArena.VisualTests.cs`), llamadas desde `ArenaUiSmokeTest`;
  - la evolución (`OnlineArena.EvolutionTests.cs`);
  - sus temas en `OnlineLobby.MusicTests.cs`, que comprueba cada clip, su tempo y su compás;
  - el selector y el constructor de mazos (`OnlineLobby.DeckTests.cs`).

## 9. Verificar

```bash
./pruebas.sh
dotnet build server/spacetimedb/StdbModule.csproj
./jugar.sh --headless --music-test
```

Juega además un duelo real contra ti mismo (`./jugar.sh` y `./jugar.sh --pvp-profile=dos`) para ver el retrato, el ataque, la evolución y la música. Si el cambio altera la forma del estado guardado, al publicar el servidor se vacían salas y partidas ([Compatibilidad](CARD_EFFECTS.md#compatibilidad)).

## Lista de comprobación

- [ ] Reglas, cartas, arte y música pedidos y confirmados con el diseñador.
- [ ] Formas en `RelikarioCatalog` (`All`, `Playable` y `Roster`), con su evolución.
- [ ] Poderes propios en `GameEngine.<Nombre>.cs`, con sus jugadas, opciones e historial.
- [ ] Habilidades específicas y variantes de Ataque en `CardCatalog`, con sus imágenes; Ataque por defecto con `DefaultAttackArtId`, sin un `if` nuevo.
- [ ] Permiso de distribución de cada imagen confirmado y documentado (o el hueco anotado como pendiente), con la marca de agua intacta.
- [ ] `assets/characters/<id>/`: cartas por nivel, retrato, miniatura, animaciones o rig, proyectil y `README.md`.
- [ ] Rig conectado en `RelikarioPortrait` y proyectil en `ArenaEffects`, si procede.
- [ ] Temas de combate y clímax generados, registrados en `music.tres` y documentados.
- [ ] Pruebas del motor, de interfaz y de música en verde.

Diferencias con una carta y lista completa (*Definition of Done*): [core/AGENTS.md](../core/AGENTS.md#relikario-frente-a-carta) y [AGENTS.md](../AGENTS.md#definition-of-done-carta-o-relikario).

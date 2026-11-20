# Stack y siguiente etapa

**Actualización, 6 de noviembre de 2026:** comenzó la etapa 2. Ya están implementadas y publicadas las salas, el reparto privado y la recuperación de identidad. La preparación y el duelo completo ya se ejecutan en SpacetimeDB, con recuperación de decisiones pendientes y arena PvP. [Estado, pruebas e instrucciones PvP](PVP.md). Las decisiones que siguen documentan el diseño aplicado y los límites de esta primera versión.

## Recomendación

**Godot .NET + C# y SpacetimeDB para los duelos en línea.** El único modo jugable es PvP; el motor C# puro se ejecuta en el servidor y en las pruebas.

El [tutorial oficial de SpacetimeDB para Godot](https://spacetimedb.com/docs/tutorials/godot/) soporta Godot 4.6.2 .NET o posterior. Su [instalación](https://spacetimedb.com/docs/tutorials/godot/part-1/) utiliza el paquete `SpacetimeDB.ClientSDK.Godot`. Se eligió inicialmente 4.6.2 como versión de referencia reproducible. El 6 de noviembre de 2026 se actualizó el editor y Godot.NET.Sdk a 4.7.2 estable; se mantiene .NET 8.

Usar C# para el cliente y el motor evita reescribir las reglas en otro lenguaje al mover la autoridad al servidor. La interfaz de Godot solo presenta el estado y solicita jugadas. `core/` no conoce el árbol de escenas ni ejecuta animaciones; sus proyecciones de red incluyen señales de presentación sin dependencias de Godot.

La arena PvP usa componentes de presentación independientes del motor; las señales visuales públicas acompañan al estado autorizado sin incluir cartas ocultas. La interfaz tiene distribución adaptable y recursos importados reducidos. [Diseño, mediciones y pruebas de interfaz](UI.md).

## Decisión sobre navegador

La [documentación de Godot](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html) indica que Godot 4 con C# no puede exportarse a web. Esta base apunta a PC.

Si el requisito principal cambia a «compartir un enlace y jugar sin instalar», habría que decidirlo antes de ampliar el cliente. Para ese objetivo consideraría un cliente web en TypeScript con SpacetimeDB. También se puede evaluar Godot con GDScript, pero la integración oficial consultada usa .NET; haría falta estudiar otro puente con el servidor. Ninguna de esas alternativas está implementada aquí.

Para un duelo 1 contra 1, un servidor sencillo con WebSocket también sería viable. No recomiendo cambiar ahora: SpacetimeDB sigue encajando si queremos estado persistente y partidas en línea, y ya era parte del stack propuesto.

## Persistencia del duelo

El servidor guarda estados completos con efectos pendientes explícitos y prepara las vistas autorizadas al aceptar cada jugada. No se reproduce el historial: cada partida empieza desde su apertura y continúa desde su estado guardado. Véase [el informe de escalabilidad](SCALABILITY.md).

## Diseño del MVP en línea

Alcance concreto: crear sala, unirse mediante código, dos jugadores, los mismos mazos de práctica y una partida completa. Después, reconexión a la misma sala.

1. Módulo C# de SpacetimeDB y SDK oficial de Godot fijados en 2.16.1, compilados con .NET 8.
2. Reutilizar las reglas C#, adaptando la persistencia al modelo de tablas del módulo. Que el lenguaje coincida no elimina este trabajo.
3. Guardar estado interno privado: mazos ordenados, manos, bonus y fuente de azar.
4. Exponer únicamente la información permitida: mano propia y estado público del duelo, mediante vistas que validen la identidad y pertenencia a la sala. Los [permisos de tablas](https://spacetimedb.com/docs/tables/access-permissions/) y las [vistas](https://spacetimedb.com/docs/functions/views/) son parte de ese diseño.
5. Recibir intenciones —enseñar, atacar, jugar código, canjear, avanzar o descartar por un efecto de carta—. La mano no tiene límite por defecto. Validar en servidor identidad, sala, turno, etapa, carta y coste. No aceptar resultados de dados ni daño calculado por el cliente.
6. Añadir un identificador de comando y revisión de partida para rechazar duplicados y selecciones obsoletas. La preparación usa identificadores de instancia. El combate encapsula los índices del motor detrás de opciones calculadas por el servidor para una revisión concreta; nunca acepta una selección de una revisión anterior. Véase la guía PvP para el estado persistido, la auditoría acotada y sus límites.
7. Suscribir el cliente únicamente a los datos autorizados. Ocultar una carta en la pantalla o filtrar una consulta desde el cliente no protege una tabla pública.

La demo local tiene el estado completo en memoria para facilitar pruebas. **Ese objeto no debe publicarse íntegro a los dos clientes.** La proyección de red filtra esa información antes de enviarla a cada jugador.

## Después del duelo local

- Elegir un único mazo oficial y transcribir sus datos: identificador, tipo, texto, daño, premio, requisitos y efectos.
- Confirmar con el creador o un jugador experto las diferencias registradas en el README: costes de espacios y agotamiento, además de ventanas de respuesta.
- Sustituir cartas de práctica por ese mazo, añadir sus efectos y validar una partida con alguien que conozca Relick.
- Revisar entonces si merece la pena añadir el segundo mazo, niveles y respuestas antes de abrir el multijugador a más personas.

No hace falta colección, sobres, economía, clasificación ni matchmaking automático para validar el primer duelo en línea.


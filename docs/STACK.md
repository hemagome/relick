# Stack y diseño del duelo en línea

## Elección

**Godot .NET con C# para el cliente y el motor, y SpacetimeDB para los duelos en línea.** El único modo jugable es PvP. El mismo motor C# puro (`core/`) se ejecuta en el servidor y en las pruebas.

- Usar C# en el cliente y en el motor evita reescribir las reglas en otro lenguaje para el servidor. La interfaz de Godot solo presenta el estado y solicita jugadas.
- `core/` no conoce el árbol de escenas ni ejecuta animaciones. Sus proyecciones de red incluyen señales de presentación sin dependencias de Godot.
- Versiones fijadas: Godot.NET.Sdk 4.7.2 y .NET 8 (`net9.0` para Android), y SpacetimeDB 2.16.1 en la CLI, el módulo del servidor y los SDK de cliente. El cliente usa el paquete `SpacetimeDB.ClientSDK.Godot`, como en el [tutorial oficial de SpacetimeDB para Godot](https://spacetimedb.com/docs/tutorials/godot/).

## Navegador

La [documentación de Godot](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html) indica que Godot 4 con C# no puede exportarse a web, así que el proyecto apunta a escritorio y Android (hay exportaciones para Windows y Android). Jugar desde un enlace sin instalar requeriría otro cliente, por ejemplo en TypeScript con el SDK web de SpacetimeDB; no hay nada de eso implementado.

## Diseño del duelo en línea

1. Un módulo C# de SpacetimeDB compila los archivos de `core/` y guarda el estado completo de cada duelo, con sus efectos pendientes explícitos. No reproduce el historial: cada partida empieza desde su apertura y continúa desde su estado guardado ([Escalabilidad](SCALABILITY.md)).
2. El estado interno (mazos ordenados, manos, Bonus, azar) es privado. Cada jugador recibe solo lo que puede ver mediante vistas filtradas por identidad y pertenencia a la sala ([permisos de tablas](https://spacetimedb.com/docs/tables/access-permissions/), [vistas](https://spacetimedb.com/docs/functions/views/)). Ocultar una carta en pantalla o filtrar una consulta en el cliente no protege una tabla pública.
3. El cliente envía intenciones: el índice de una opción calculada por el servidor para una revisión concreta, con un identificador de comando para rechazar duplicados. El servidor valida identidad, sala, turno, etapa, carta y coste, y nunca acepta dados ni daño calculados por el cliente.
4. La preparación usa identificadores de instancia para las cartas; el combate encapsula los índices del motor detrás de las opciones de cada revisión.

Funcionamiento detallado, pruebas y límites: [PvP](PVP.md).

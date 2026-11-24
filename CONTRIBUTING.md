# Cómo contribuir

Gracias por querer mejorar este simulador comunitario **no oficial** de Relick. Esta guía es para personas; si usas un agente de programación, apúntalo a [AGENTS.md](AGENTS.md), que contiene las instrucciones operativas y la lista de comprobación completa.

## Antes de empezar

- Lee el [aviso legal](NOTICE.md): el proyecto no está afiliado a Relick y la licencia del código no cubre la propiedad intelectual de Relick.
- El código está bajo **PolyForm Noncommercial 1.0.0** (*source available*, no *open source*). Lee [Licencia de las contribuciones](#licencia-de-las-contribuciones) antes de enviar nada.
- Para cambios grandes (una mecánica nueva, un Relikario, un cambio de reglas globales), abre primero una incidencia y acuerda el enfoque.

## Entorno local

| Herramienta | Versión | Para qué |
|---|---|---|
| [Godot **.NET**](https://godotengine.org/download) (no la edición estándar) | 4.7.2 | Abrir, ejecutar y probar el juego |
| [SDK de .NET](https://dotnet.microsoft.com/download/dotnet/8.0) | 8 (fijado en [global.json](global.json)) | Compilar el juego, el motor y las pruebas |
| [CLI de SpacetimeDB](https://spacetimedb.com/install) | 2.16.1 | Solo para un servidor local y las pruebas de red |
| Componente WASI de .NET | — | Solo para compilar el servidor: `dotnet workload install wasi-experimental --skip-manifest-update` |

Instrucciones por sistema operativo, ejecución y opciones: [README.md](README.md#ejecutar). Servidor local: [docs/PVP.md](docs/PVP.md#desarrollo-local).

## Ejecutar y probar

```bash
./jugar.sh                                         # compila y abre el juego
./pruebas.sh                                       # motor + interfaz sin ventana; debe terminar con PRUEBAS_PASS
dotnet build server/spacetimedb/StdbModule.csproj  # el servidor compila el mismo motor
```

Para ver un cambio en una partida real, levanta un servidor local y juega contra ti mismo (`./jugar.sh` y `./jugar.sh --pvp-profile=dos` con `RELICK_SERVER`/`RELICK_DATABASE`, ver el README). No publiques módulos en Maincloud ni en otros servidores remotos.

## Contribuir cartas y Relikarios

1. Abre una incidencia con el **texto impreso exacto**, el código de colección, la categoría y los costes. Si alguna interacción es dudosa, dilo: preferimos preguntar a inventar reglas.
2. Sigue la guía correspondiente:
   - [Añadir una carta](docs/ADDING_CARD.md)
   - [Añadir un Relikario](docs/ADDING_RELIKARIO.md)
3. Reutiliza los efectos y operaciones existentes ([core/AGENTS.md](core/AGENTS.md#reutilizar-antes-de-crear)). Las reglas viven en `core/`; la interfaz casi nunca necesita cambios.
4. Añade pruebas del motor (y de interfaz si hay presentación propia) y deja `./pruebas.sh` en verde.
5. **Imágenes**: solo si tienes derecho a distribuirlas, y siempre con su marca de agua (ver [Recursos de terceros](#recursos-de-terceros)). Una carta puede contribuirse sin imagen.

## Reportar errores de juego

Abre una incidencia con:

- Qué esperabas que pasara y qué pasó, citando el texto de la carta o el apartado del reglamento.
- Los pasos para reproducirlo: Relikarios, cartas en juego, momento del turno y decisiones tomadas.
- La versión (commit) y la plataforma.
- Si es posible, el historial del duelo o una captura. No incluyas tokens, archivos de sesión ni datos personales.

Si sospechas que el problema es una interpretación de reglas, márcalo como duda de reglas: no siempre es un error del código.

## Pull requests

- Una PR por cambio coherente (una carta, un Relikario, una corrección). Evita mezclar refactorizaciones ajenas.
- Describe qué cambia, por qué, qué pruebas añadiste y qué ejecutaste (`./pruebas.sh`, compilación del servidor).
- Señala explícitamente si cambias reglas globales, identificadores existentes, valores de enums que viajan por red o la forma del estado guardado: obliga a publicar cliente y servidor juntos ([docs/CARD_EFFECTS.md](docs/CARD_EFFECTS.md#compatibilidad)).
- Indica la procedencia y el permiso de cada recurso nuevo (imagen, audio, texto).
- Añade los `.uid` y `.import` que genera Godot en el mismo commit.

## Convenciones

- Documentación en español; identificadores y comentarios del código en inglés; textos del juego en español.
- Reglas en `core/` (C# puro, sin Godot). Una carta con reglas propias es un módulo de `core/Effects/`; nada de `if (card.Id == …)` en el motor.
- El servidor es la autoridad: el cliente no calcula dados ni resultados.
- No añadas dependencias sin justificarlo. No edites a mano `network/bindings/`.
- Más detalle: [AGENTS.md](AGENTS.md#estilo-de-código), [docs/UI.md](docs/UI.md) y los `AGENTS.md` de cada módulo.

## Recursos de terceros

No incorpores sin una autorización verificable y documentada:

- Escaneos, fotografías o ilustraciones de cartas de Relick, ni imágenes generadas o «restauradas» a partir de ellas.
- El logotipo, la marca u otros elementos gráficos oficiales.
- Textos de manuales, webs o reglamentos más allá de lo imprescindible para describir una regla.
- Audio, fuentes, código o modelos de terceros cuya licencia no permita redistribuirlos, o que sea incompatible con el uso no comercial del proyecto.

Las imágenes de cartas se añaden **con su marca de agua**: no la quites ni retoques la imagen con herramientas de edición generativa. Los recursos de terceros que sí puedan incluirse deben añadirse a [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) con su autor, origen y licencia. Ya hay recursos en el repositorio cuya situación está pendiente; están listados en [docs/PUBLISHING.md](docs/PUBLISHING.md) y no sirven de precedente.

## Licencia de las contribuciones

- Solo envía código y recursos que hayas creado tú o sobre los que tengas derecho a contribuir (por ejemplo, con permiso de tu empleador si corresponde).
- Al enviar una contribución, la ofreces bajo los mismos términos del proyecto, **PolyForm Noncommercial 1.0.0**, y confirmas que tienes derecho a hacerlo.
- **Conservas tus derechos de autor.** Contribuir no transfiere la titularidad al propietario del repositorio ni le concede derechos adicionales, como relicenciar tu aportación con fines comerciales. Si el proyecto necesitara alguna vez esos derechos, tendría que pedírtelos de forma explícita.
- No incluyas código copiado de otros proyectos salvo que su licencia lo permita y lo documentes en [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

# Auditoría previa a la publicación

Revisión del 24 de noviembre de 2026 sobre `main` (292 commits) y todo su historial, hecha al adoptar PolyForm Noncommercial 1.0.0. El repositorio remoto sigue siendo **privado**. No se ha borrado ningún recurso, no se ha reescrito el historial y no se ha publicado nada: las decisiones de esta lista corresponden al propietario.

Esto no es asesoramiento jurídico. Donde la situación depende de permisos que el repositorio no documenta, se trata como bloqueante.

## Resumen

| Área | Resultado |
|---|---|
| Credenciales y secretos | Ninguno encontrado en el árbol ni en el historial |
| Información personal | Correo personal en los metadatos de los commits; las rutas locales con el nombre de usuario ya no están en el árbol, pero sí en el historial |
| Archivos generados innecesarios | `output/imagegen/` y los prompts de restauración se retiraron del árbol el 24 de noviembre de 2026; siguen en el historial (340 MB empaquetados) |
| Propiedad intelectual de Relick | Logotipo, 102 escaneos de cartas (ahora con su marca de agua), retratos, figuras y animaciones derivados sin marca de agua, textos de reglas |
| Licencias de dependencias | Compatibles con un proyecto no comercial; SpacetimeDB usa BSL 1.1 con condiciones; licencia del SDK de cliente sin verificar |

## Bloqueantes

Deben resolverse antes de hacer público el repositorio o distribuir builds.

### B1. Ilustraciones y escaneos oficiales de Relick

- `assets/cards/*-clean.png` (92 archivos), `assets/cards/card-back.png` y las cartas `card-lv1.png`, `card-lv2.png` y `attack-card.png` de `assets/characters/*/`: escaneos de cartas impresas, con créditos como «© 2018 Fuyuhiro Gushiken» y de ilustradores. Desde el 24 de noviembre de 2026 son los archivos aportados, **con su marca de agua**, sin edición generativa.
- `assets/characters/*/` (retratos, miniaturas, figuras recortadas y hojas de animación): derivados de esas ilustraciones con edición generativa y **sin marca de agua**. No tienen un original con marca que los sustituya.
- `assets/relikia/*.webp`: procedencia no documentada.

**Agravante:** las versiones anteriores de las cartas, sin marca de agua, siguen en el historial, y los recursos derivados del tablero no tienen marca. Retirar marcas de agua de obras ajenas puede tener consecuencias legales propias, además de la infracción de derechos de autor, en algunas jurisdicciones. La norma actual del proyecto exige conservar la marca ([docs/ADDING_CARD.md](ADDING_CARD.md#3-ilustración)).

**Resolver con una de estas opciones:**
1. Obtener una autorización escrita del titular de los derechos que cubra la redistribución pública de estas imágenes (incluidas las derivadas), y documentarla en `THIRD_PARTY_NOTICES.md`. Las notas de Yuno Oro y WD mencionan archivos «suministrados con el diseño»: si existe esa autorización, debe quedar por escrito.
2. Sacar las imágenes del repositorio público ([alternativas](#alternativas-para-desacoplar-los-recursos-protegidos)).
3. Para los retratos, figuras y animaciones sin marca de agua: sustituirlos por arte original o retirarlos.

### B2. Logotipo de Relick

`assets/brand/relick-logo.png` es la cabecera de la web oficial, incorporada sin modificar. Se muestra en la barra de navegación (`RelickVisuals.Logo`) y en la pantalla de inicio de sesión. Además de los derechos de autor, usar el logotipo puede sugerir una relación oficial que el aviso niega. Necesita autorización o un sustituto propio.

### B3. Historial de Git

Aunque se retiren los recursos del árbol, los archivos de imagen que han pasado por el historial (incluidas las 102 versiones de cartas sin marca de agua, los escaneos de `output/imagegen/` y variantes ya borradas) siguen en cada commit. Publicar este repositorio con su historial publica esos recursos.

**Opciones (decisión del propietario):** publicar un repositorio nuevo a partir de un árbol limpio, sin historial; o reescribir el historial con una herramienta como `git filter-repo` **después** de hacer copia de seguridad. Esta tarea no reescribe nada.

### B4. Información personal

- Los commits contienen el nombre y el **correo personal** del autor (263 commits; los otros 29 usan la dirección `noreply` de GitHub). Publicar el historial publica el correo. Si se reescribe o se crea un repositorio nuevo, conviene usar la dirección `noreply`.
- Las rutas locales con el nombre de usuario (`/home/<usuario>/Downloads/…`) se retiraron del árbol junto con los prompts de restauración, pero siguen en el historial (ver B3).

### B5. Titular de los derechos del código

`LICENSE` contiene el texto oficial, pero el repositorio no declara quién es el licenciante ni el año. Añadirlo en `NOTICE.md` (por ejemplo con una línea `Required Notice: Copyright <año> <titular>`), con el nombre legal que el propietario elija. No se ha inventado.

### B6. Licencia del SDK de cliente de SpacetimeDB

`SpacetimeDB.ClientSDK.Godot` 2.16.1 y `SpacetimeDB.ClientSDK` 2.16.1 no incluyen archivo de licencia ni la declaran en su `.nuspec`. El SDK se distribuye dentro de cada build del juego. Confirmar su licencia en <https://github.com/clockworklabs/com.clockworklabs.spacetimedbsdk> y anotarla en `THIRD_PARTY_NOTICES.md`. Confirmar también que la CLI `spacetime generate` no impone condiciones sobre `network/bindings/`.

## Riesgos que no bloquean, pero requieren atención

- **SpacetimeDB bajo BSL 1.1.** `SpacetimeDB.Runtime` y `SpacetimeDB.BSATN.Runtime` 2.16.1 son *source available* con una *Additional Use Grant*: como máximo una instancia de SpacetimeDB en producción y nada de «Database Service» comercial. No son incompatibles con publicar el código del proyecto bajo PolyForm (no se copian ni se relicencian), pero quien despliegue un servidor debe cumplir esas condiciones.
- **Textos de reglas y nombres.** `core/CardCatalog.cs`, `core/RelikarioCatalog.cs`, `core/RelikiaEffects.cs`, los `README.md` de recursos y la documentación transcriben nombres y textos de cartas. Las mecánicas de juego no suelen protegerse por derechos de autor, pero los textos literales y los nombres sí pueden estarlo, y los nombres pueden ser marcas. Valorar con el titular o reducir los textos a resúmenes propios.
- **Nombre del proyecto.** `project.godot` (`config/name="Relick"`), el ejecutable (`Relick.exe`), el APK y el título del README usan la marca «Relick». Valorar un nombre propio que deje claro el carácter no oficial («simulador comunitario de…»). No se ha cambiado: el nombre de la carpeta de datos (`config/custom_user_dir_name`) conserva los ajustes de los jugadores y moverlo los perdería.
- **Referencias a la web y el reglamento oficiales.** Los documentos enlazan a la web y el manual oficiales; es correcto citar la fuente, pero no deben reproducirse fragmentos largos.
- **Base de desarrollo en Maincloud.** `network/server.cfg` apunta a la base de desarrollo del autor y contiene el `client_id` público de SpacetimeAuth (cliente PKCE sin secreto, no es una credencial). Un repositorio público haría que cualquier build compilada desde él se conecte a esa base. Valorar dejar la configuración vacía por defecto y documentar cómo apuntar a un servidor propio.
- **Exportaciones.** El ZIP de Windows incluye ahora `LICENSE`, `NOTICE.md` y `THIRD_PARTY_NOTICES.md`. El APK de Android no los incluye como archivos; el aviso y las licencias están dentro del juego (Ajustes → Acerca de).

## Comprobado sin hallazgos

- **Secretos:** búsqueda en los 292 commits de claves de API, tokens JWT (`eyJ…`), tokens de GitHub, claves de AWS y claves privadas; ningún resultado. Ningún archivo `.env`, de sesión o de tokens ha estado versionado. `.gitignore` excluye `.env*`, `.runtime.env`, `artifacts/` y credenciales de herramientas.
- **Keystore de Android:** `export-android.sh` genera una clave de depuración local en `artifacts/` (ignorado) con la contraseña estándar `android` del SDK; no es un secreto.
- **Audio:** las dos grabaciones de terceros son CC0 y tienen su procedencia documentada; el resto se genera con `tools/audio/`.
- **Fuentes tipográficas:** la interfaz usa la fuente integrada de Godot (`ThemeDB.FallbackFont`), cubierta por los avisos de Godot que muestra el juego.
- **Licencias del motor:** Godot y .NET son MIT; el juego muestra los avisos de Godot y de sus componentes internos.

## Alternativas para desacoplar los recursos protegidos

El código ya carga las imágenes por convención de nombre y tolera su ausencia (`BoardArt.LoadCardArtwork` y `RelikarioArt.Load` devuelven `null` si falta el archivo). Eso permite separar el arte del código sin tocar las reglas:

1. **Paquete de arte privado.** Mover `assets/cards/`, `assets/characters/`, `assets/brand/` y `assets/relikia/` a un repositorio o almacenamiento privado (o a un submódulo privado) que solo se monte en builds autorizadas. El repositorio público compila y juega sin él.
2. **Recursos de demostración originales.** Sustituir cada carta por una plantilla generada por código (marco, nombre, coste, texto y un icono de `GameIcon` por categoría) y los Relikarios por siluetas o retratos originales. La interfaz ya dibuja iconos por tipo (`BoardArt.Icon`), así que una plantilla procedural es viable sin dependencias nuevas.
3. **Logotipo propio.** Un logotipo tipográfico original con el nombre del simulador, dibujado con la fuente `RelickVisuals.Display` que ya existe.
4. **Arte aportado por el jugador.** Permitir cargar imágenes desde `user://` que cada persona aporte localmente, sin redistribuirlas.

Antes de aplicar cualquiera, revisar qué pruebas de interfaz comprueban la presencia de arte concreto (por ejemplo `OnlineArena.NewCardsTests.cs` exige el arte de Habitación Secreta) y adaptarlas.

## Lista de bloqueantes

- [ ] **B1** Autorización escrita para las ilustraciones y escaneos de Relick (las cartas ya llevan su marca de agua), o retirarlas del repositorio público; sustituir o retirar los retratos, figuras y animaciones derivados sin marca.
- [ ] **B2** Autorización para el logotipo oficial, o un logotipo propio.
- [ ] **B3** Decidir cómo publicar sin el historial que contiene esos recursos (repositorio nuevo o reescritura con copia de seguridad).
- [ ] **B4** Quitar el correo personal de los metadatos que se publiquen (y del historial las rutas locales, ya retiradas del árbol).
- [ ] **B5** Declarar el titular legal y el año en `NOTICE.md`.
- [ ] **B6** Verificar la licencia del SDK de cliente de SpacetimeDB y de los bindings generados.

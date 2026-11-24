# scripts/AGENTS.md · Interfaz (Godot)

El cliente solo **presenta** el estado autorizado y **pide** jugadas. No contiene reglas: si una pantalla necesita saber si algo es legal, la respuesta debe venir de las opciones del servidor (`DuelOptions`) o de la vista (`DuelSnapshot`).

Diseño, medidas, animaciones y validación: [docs/UI.md](../docs/UI.md).

## Mapa

| Área | Archivos |
|---|---|
| Menús, sala, cuenta, ajustes | `OnlineLobby*.cs`, `MenuShell.cs`, `MenuDialog.cs`, `MenuVisuals.cs`, `DeckScreen*.cs`, `DeckEditor*.cs`, `Wardrobe.cs` |
| Arena del duelo | `OnlineArena*.cs`, `ArenaPresentation.cs`, `ArenaEffects.cs`, `ArenaActionText.cs`, `BoardWidgets.cs`, `HandFan.cs`, `CardInspector.cs` |
| Relikarios en pantalla | `RelikarioArt.cs`, `RelikarioPortrait.cs`, `*PortraitRig.cs`, `EvolutionReveal.cs`, `OpeningRelikarioReveal.cs` |
| Arte de cartas | `BoardArt.LoadCardArtwork` (en `BoardWidgets.cs`) y `SkillArtwork.cs`: cargan `assets/cards/<id>-clean.png` por nombre |
| Audio | `MusicDirector.cs`, `SoundDirector.cs`, `AudioPreferences.cs` |
| Aviso de proyecto no oficial y licencias | `LegalNotice.cs` (textos), sección «Acerca de» en `OnlineLobby.Settings.cs` |
| Pruebas de interfaz | `scripts/Tests/` (solo en compilaciones de desarrollo: `RELICK_DEV_TOOLS`) |

## Convenciones

- La interfaz se construye por código; `scenes/` solo tiene la escena principal y el director musical.
- No hay sistema de traducción: los textos visibles están en español, escritos en el propio código. No añadas un sistema de i18n como parte de otra tarea.
- Reutiliza los ayudantes existentes (`Caption`, `Description`, `Text`, `AddButton`, `Surface`, `SettingsSection`, `MenuDialog.Fill/Show`) y los colores de `BoardArt`, `MenuVisuals` y `RelickVisuals`.
- Respeta `InterfacePreferences.ReducedMotion` en cualquier animación nueva y prueba los cuatro tamaños de referencia (1440×1024, 1024×768, 844×390, 700×390).
- Las decisiones de carta se dibujan solas según `DecisionKind`; una carta nueva normalmente no toca la interfaz ([docs/ADDING_CARD.md](../docs/ADDING_CARD.md#5-interfaz)).
- No elimines ni ocultes el aviso de proyecto no oficial (`LegalNotice`) de la pantalla de inicio, la página Jugar ni Ajustes.

## Validar

```bash
./pruebas.sh --ui                         # toda la matriz de interfaz
./pruebas.sh --checks=Nombre              # una comprobación (debe pasar también sola)
./jugar.sh -- --lobby-preview-dir=/tmp/relick-lobby   # capturas del menú (necesita pantalla)
```

Una comprobación nueva va en `scripts/Tests/` y se registra en la tabla `checks` de [Tests/OnlineLobby.UiTests.cs](Tests/OnlineLobby.UiTests.cs).

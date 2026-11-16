#!/usr/bin/env bash
# Compila y abre Relick. Uso: ./jugar.sh [--headless] [opciones del juego]
# Las opciones del juego (--pvp-ui-test, --pvp-profile=...) no necesitan `--`;
# si escribes `--`, lo anterior va a Godot y lo posterior al juego, como antes.
set -euo pipefail
relick_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$relick_root"

# Rutas locales opcionales (no versionado), igual que en export-*.sh.
[[ -f .runtime.env ]] && source .runtime.env
relick_godot="${RELICK_GODOT:-$(command -v godot || command -v godot4 || true)}"
if [[ -z "$relick_godot" || ! -x "$relick_godot" ]] || ! command -v dotnet >/dev/null; then
  echo "Necesitas Godot .NET 4.7.2 o posterior y el SDK de .NET 8. Consulta README.md."
  exit 1
fi

# Sin `--`, solo --headless es de Godot; el resto pasa al juego.
godot_args=() game_args=()
for arg in "$@"; do
  if [[ "$arg" == -- ]]; then godot_args=("$@") game_args=(); break; fi
  if [[ "$arg" == --headless ]]; then godot_args+=("$arg"); else game_args+=("$arg"); fi
done
if [[ ${#game_args[@]} -gt 0 ]]; then game_args=(-- "${game_args[@]}"); fi

dotnet build Relick.csproj --nologo
exec "$relick_godot" --path "$relick_root" "${godot_args[@]}" "${game_args[@]}"

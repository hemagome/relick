#!/usr/bin/env bash
set -euo pipefail
relick_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$relick_root"

# Optional local paths, never needed when Godot and the .NET SDK are on PATH.
if [[ -f .runtime.env ]]; then
  source .runtime.env
fi
relick_godot="${RELICK_GODOT:-}"
if [[ -z "$relick_godot" ]]; then
  relick_godot="$(command -v godot || command -v godot4 || true)"
fi
if [[ -z "$relick_godot" || ! -x "$relick_godot" ]]; then
  echo "Necesitas Godot .NET 4.7.2 o posterior. Consulta README.md."
  exit 1
fi
if ! command -v dotnet >/dev/null || [[ -z "$(dotnet --list-sdks)" ]]; then
  echo "Necesitas el SDK de .NET 8 (el runtime no basta). Consulta README.md."
  exit 1
fi
# Android y escritorio comparten la caché de NuGet, pero usan frameworks distintos.
# La restauración incremental comprueba el destino antes de reutilizarla.
dotnet build Relick.csproj --nologo
exec "$relick_godot" --path "$relick_root" "$@"

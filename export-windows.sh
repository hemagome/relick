#!/usr/bin/env bash
set -euo pipefail
relick_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$relick_root"

if [[ -f .runtime.env ]]; then
  source .runtime.env
fi
relick_godot="${RELICK_GODOT:-$(command -v godot || command -v godot4 || true)}"
if [[ -z "$relick_godot" || ! -x "$relick_godot" ]]; then
  echo "Necesitas Godot .NET 4.7.2 y sus plantillas de exportación .NET."
  exit 1
fi
command -v dotnet >/dev/null
command -v python3 >/dev/null

mkdir -p artifacts
relick_staging="$(mktemp -d "$relick_root/artifacts/windows-export.XXXXXX")"
trap 'rm -rf -- "$relick_staging"' EXIT
mkdir -p "$relick_staging/Relick"

"$relick_godot" --headless --path "$relick_root" --editor --import --log-file "$relick_staging/import.log"
"$relick_godot" --headless --path "$relick_root" --export-release "Windows Desktop" "$relick_staging/Relick/Relick.exe" --log-file "$relick_staging/export.log"

cat > "$relick_staging/Relick/LEEME.txt" <<'EOF'
Relick para Windows de 64 bits

1. Extrae todo el ZIP en una carpeta.
2. Abre Relick.exe. Conserva los demás archivos y carpetas junto al ejecutable.
3. Con internet, escribe tu nombre y crea una sala o introduce el código del rival.

No necesitas instalar Godot ni el SDK de .NET.
Ambos jugadores deben usar una versión compatible del juego.
Esta versión se conecta al servidor de desarrollo de Relick en SpacetimeDB.
EOF

python3 - "$relick_staging" "$relick_root/artifacts/Relick-windows-x86_64.zip" <<'PY'
from pathlib import Path
import os
import sys
import zipfile

staging, destination = map(Path, sys.argv[1:])
package = staging / 'Relick'
assert (package / 'Relick.exe').is_file(), 'No se generó el ejecutable'
assert (package / 'Relick.pck').is_file(), 'No se generaron los recursos'
assert list(package.rglob('Relick.dll')), 'No se generó el ensamblado del juego'
assert list(package.rglob('coreclr.dll')), 'Falta el runtime de .NET para Windows'
archive = staging / destination.name
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as output:
    for path in sorted(package.rglob('*')):
        if path.is_file():
            output.write(path, path.relative_to(staging))
with zipfile.ZipFile(archive) as output:
    assert output.testzip() is None, 'El ZIP no superó la verificación'
os.replace(archive, destination)
print(f'Paquete listo: {destination} ({destination.stat().st_size / 1024**2:.1f} MiB)')
PY

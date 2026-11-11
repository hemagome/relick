#!/usr/bin/env bash
set -euo pipefail
relick_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$relick_root"

if [[ -f .runtime.env ]]; then
  source .runtime.env
fi
if [[ -f artifacts/android-toolchain/environment.sh ]]; then
  source artifacts/android-toolchain/environment.sh
fi
relick_godot="${RELICK_GODOT:-$(command -v godot || command -v godot4 || true)}"
: "${JAVA_HOME:?Configura JAVA_HOME con la ruta de OpenJDK 17.}"
: "${ANDROID_HOME:?Configura ANDROID_HOME con la ruta del SDK de Android.}"
if [[ -z "$relick_godot" || ! -x "$relick_godot" ]]; then
  echo "Necesitas Godot .NET 4.7.2 y sus plantillas de exportación .NET."
  exit 1
fi
command -v dotnet >/dev/null
command -v python3 >/dev/null
relick_dotnet_version="$(dotnet --version)"
if (( ${relick_dotnet_version%%.*} < 9 )); then
  echo "La plantilla APK de Godot 4.7.2 requiere el SDK de .NET 9."
  exit 1
fi
relick_build_tools="$ANDROID_HOME/build-tools/35.0.1"
for relick_tool in "$JAVA_HOME/bin/keytool" "$relick_build_tools/apksigner" "$relick_build_tools/zipalign" "$relick_build_tools/aapt"; do
  if [[ ! -x "$relick_tool" ]]; then
    echo "Falta una herramienta de exportación: $relick_tool"
    exit 1
  fi
done

mkdir -p artifacts/android
relick_staging="$(mktemp -d "$relick_root/artifacts/android/export.XXXXXX")"
trap 'rm -rf -- "$relick_staging"' EXIT
# Configuración aislada: no cambia los ajustes del editor del usuario.
export XDG_CONFIG_HOME="$relick_staging/config"
export PATH="$JAVA_HOME/bin:$PATH"
relick_keystore="$relick_root/artifacts/android/debug.keystore"
if [[ ! -f "$relick_keystore" ]]; then
  "$JAVA_HOME/bin/keytool" -genkeypair -noprompt -keystore "$relick_keystore" \
    -storepass android -keypass android -alias androiddebugkey -keyalg RSA \
    -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
  chmod 600 "$relick_keystore"
fi
export GODOT_ANDROID_KEYSTORE_DEBUG_PATH="$relick_keystore"
export GODOT_ANDROID_KEYSTORE_DEBUG_USER=androiddebugkey
export GODOT_ANDROID_KEYSTORE_DEBUG_PASSWORD=android
relick_godot_version="$("$relick_godot" --version)"
relick_editor_version="$(cut -d. -f1,2 <<< "$relick_godot_version")"
mkdir -p "$XDG_CONFIG_HOME/godot"
python3 - "$XDG_CONFIG_HOME/godot/editor_settings-$relick_editor_version.tres" <<'PY'
import json
import os
import sys
from pathlib import Path

settings = {
    'export/android/java_sdk_path': os.environ['JAVA_HOME'],
    'export/android/android_sdk_path': os.environ['ANDROID_HOME'],
}
Path(sys.argv[1]).write_text('[gd_resource type="EditorSettings" format=3]\n\n[resource]\n' +
    ''.join(f'{key} = {json.dumps(value)}\n' for key, value in settings.items()))
PY

"$relick_godot" --headless --path "$relick_root" --editor --import \
  --log-file "$relick_root/artifacts/android/import.log"
"$relick_godot" --headless --path "$relick_root" --export-debug Android \
  "$relick_staging/Relick.apk" --log-file "$relick_root/artifacts/android/export.log"

"$relick_build_tools/apksigner" verify --verbose "$relick_staging/Relick.apk"
"$relick_build_tools/zipalign" -c -P 16 -v 4 "$relick_staging/Relick.apk" > artifacts/android/alignment.log
"$relick_build_tools/aapt" dump badging "$relick_staging/Relick.apk" > artifacts/android/package.txt
"$relick_build_tools/aapt" dump permissions "$relick_staging/Relick.apk" > artifacts/android/permissions.txt
python3 - "$relick_staging/Relick.apk" <<'PY'
import sys
import zipfile

with zipfile.ZipFile(sys.argv[1]) as apk:
    assert apk.testzip() is None, 'APK corrupto'
    names = apk.namelist()
    assert 'lib/arm64-v8a/libgodot_android.so' in names, 'Falta Godot para ARM64'
    assert 'lib/arm64-v8a/libmonosgen-2.0.so' in names, 'Falta el runtime de .NET'
    assert any(name.endswith('/Relick.dll') for name in names), 'Falta el juego compilado'
    assert 'assets/network/server.cfg' in names, 'Falta la conexión PvP'
print('Recursos y runtime de Android verificados.')
PY
mv -f -- "$relick_staging/Relick.apk" artifacts/Relick-android-arm64.apk
sha256sum artifacts/Relick-android-arm64.apk > artifacts/Relick-android-arm64.apk.sha256
echo "APK listo: $relick_root/artifacts/Relick-android-arm64.apk"

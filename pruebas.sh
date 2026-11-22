#!/usr/bin/env bash
# Compila una vez y ejecuta las pruebas del motor y de la interfaz; la interfaz se reparte en varios procesos de Godot.
# Uso: ./pruebas.sh [-j N] [--motor | --ui] [--checks=Nombre,Nombre]
#   -j N        procesos de Godot (por defecto, la mitad de los núcleos, hasta 8)
#   --motor     solo las reglas;  --ui  solo la interfaz
#   --checks=   solo esas comprobaciones de interfaz (los nombres salen con --lista)
#   --lista     muestra los nombres de las comprobaciones de interfaz
set -euo pipefail
relick_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$relick_root"
[[ -f .runtime.env ]] && source .runtime.env
relick_godot="${RELICK_GODOT:-$(command -v godot || command -v godot4 || true)}"

jobs=$(( $(nproc) / 2 )); (( jobs > 8 )) && jobs=8; (( jobs < 1 )) && jobs=1
run_core=1 run_ui=1 checks="" list=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    -j) jobs="$2"; shift ;;
    -j*) jobs="${1#-j}" ;;
    --motor) run_ui=0 ;;
    --ui) run_core=0 ;;
    --checks=*) checks="${1#--checks=}"; run_core=0 ;;
    --lista) list=1 ;;
    *) echo "Opción desconocida: $1. Consulta la cabecera de pruebas.sh."; exit 2 ;;
  esac
  shift
done
if [[ -z "$relick_godot" || ! -x "$relick_godot" ]] || ! command -v dotnet >/dev/null; then
  echo "Necesitas Godot .NET 4.7.2 o posterior y el SDK de .NET 8. Consulta README.md."
  exit 1
fi

logs="$(mktemp -d "${TMPDIR:-/tmp}/relick-pruebas.XXXXXX")"
pids=()
cleanup() { for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done; }
trap cleanup EXIT
trap 'exit 130' INT TERM
started=$SECONDS
failed=0

build() {
  if ! dotnet build "$1" --nologo -v quiet > "$logs/build.log" 2>&1; then
    grep -E "error" "$logs/build.log" | sort -u | head -20; echo "FALLA la compilación de $1."; exit 1
  fi
}
# Each Godot process gets its own user:// so that none touches the player's settings or another process's files.
godot_ui() {
  local data="$1"; shift
  XDG_DATA_HOME="$data" timeout 900 "$relick_godot" --headless --audio-driver Dummy --fixed-fps 145 --path "$relick_root" -- --pvp-ui-test "$@"
}

build Relick.csproj
if (( list )); then godot_ui "$logs/lista" --list-checks 2>/dev/null | sed -n 's/^PVP_UI_CHECK_NAME · //p'; exit 0; fi

if (( run_core )); then
  build tests/Relick.Core.Tests.csproj
  core_start=$SECONDS
  if dotnet tests/bin/Debug/net8.0/Relick.Core.Tests.dll > "$logs/motor.log" 2>&1; then
    echo "Motor: $(tail -1 "$logs/motor.log") ($(( SECONDS - core_start )) s)"
  else
    failed=1; grep -E "^FAIL" "$logs/motor.log" | cut -c1-400; echo "Motor: FALLA · $(tail -1 "$logs/motor.log")"
  fi
fi

if (( run_ui )); then
  if [[ -n "$checks" ]]; then names="$(tr ',' '\n' <<< "$checks")"
  else names="$(godot_ui "$logs/lista" --list-checks 2>/dev/null | sed -n 's/^PVP_UI_CHECK_NAME · //p')"; fi
  [[ -n "$names" ]] || { echo "No se obtuvo la lista de comprobaciones de interfaz."; exit 1; }
  # Longest first onto the least loaded process, using the durations of earlier runs (unknown ones count 5 s).
  times=.godot/pruebas-tiempos.tsv
  mapfile -t shards < <(awk -F'\t' -v jobs="$jobs" -v times="$times" '
    BEGIN { while ((getline line < times) > 0) { split(line, f, "\t"); ms[f[1]] = f[2] } }
    { name[++n] = $0; w[n] = ($0 in ms) ? ms[$0] : 5000 }
    END {
      if (jobs > n) jobs = n
      for (i = 1; i <= n; i++) for (j = i + 1; j <= n; j++) if (w[j] > w[i]) { t = w[i]; w[i] = w[j]; w[j] = t; t = name[i]; name[i] = name[j]; name[j] = t }
      for (i = 1; i <= n; i++) { best = 1; for (k = 2; k <= jobs; k++) if (load[k] < load[best]) best = k; load[best] += w[i]; shard[best] = shard[best] (shard[best] ? "," : "") name[i] }
      for (k = 1; k <= jobs; k++) print shard[k]
    }' <<< "$names")
  ui_start=$SECONDS
  for i in "${!shards[@]}"; do
    godot_ui "$logs/datos-$i" --checks="${shards[$i]}" > "$logs/ui-$i.log" 2>&1 &
    pids+=($!)
  done
  for i in "${!shards[@]}"; do
    rc=0; wait "${pids[$i]}" || rc=$?
    missing="$(tr ',' '\n' <<< "${shards[$i]}" | while read -r name; do grep -q "^PVP_UI_CHECK · $name · " "$logs/ui-$i.log" || echo "$name"; done)"
    if (( rc != 0 )) || ! grep -q "^PVP_UI_CHECKS_PASS" "$logs/ui-$i.log" || [[ -n "$missing" ]]; then
      failed=1
      echo "Interfaz: FALLA el proceso $i (código $rc) · sin completar: $(tr '\n' ' ' <<< "$missing")"
      grep -A6 "PVP_UI_FAIL" "$logs/ui-$i.log" | head -12 || tail -12 "$logs/ui-$i.log"
    fi
  done
  pids=()
  results="$(cat "$logs"/ui-*.log | sed -n 's/^PVP_UI_CHECK · \(.*\) · \([0-9]*\) ms$/\1\t\2/p')"
  if [[ -n "$results" ]]; then
    mkdir -p .godot
    { printf '%s\n' "$results"; if [[ -f $times ]]; then cat "$times"; fi; } | awk -F'\t' '!seen[$1]++' | sort > "$times.tmp"
    mv "$times.tmp" "$times"
    echo "Interfaz: $(wc -l <<< "$results") comprobaciones en ${#shards[@]} procesos ($(( SECONDS - ui_start )) s). Las más lentas:"
    sort -t$'\t' -k2 -rn <<< "$results" | head -10 | awk -F'\t' '{ printf "  %6.1f s  %s\n", $2 / 1000, $1 }'
  fi
fi

echo "Total: $(( SECONDS - started )) s."
if (( failed )); then echo "Registros: $logs"; exit 1; fi
rm -rf "$logs"
echo "PRUEBAS_PASS"

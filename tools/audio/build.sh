#!/usr/bin/env bash
set -euo pipefail
relick_audio_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
relick_audio_tmp="$(mktemp -d /tmp/relick-audio.XXXXXX)"
trap 'rm -rf -- "$relick_audio_tmp"' EXIT
g++ -O2 -std=c++17 "$relick_audio_root/tools/audio/render.cpp" -o "$relick_audio_tmp/render"
"$relick_audio_tmp/render" "$relick_audio_tmp/wav"
mkdir -p "$relick_audio_root/assets/audio/music" "$relick_audio_root/assets/audio/sfx"
for relick_song in menu yuno-combat yuno-climax; do
  ffmpeg -hide_banner -loglevel error -y -i "$relick_audio_tmp/wav/$relick_song.wav" -c:a libvorbis -q:a 5 -metadata artist=Relick -metadata comment='Original procedural composition; 160 BPM; D minor; 128 beats' "$relick_audio_root/assets/audio/music/$relick_song.ogg"
done
for relick_sound in card-draw dice-roll heal attack; do
  cp -- "$relick_audio_tmp/wav/$relick_sound.wav" "$relick_audio_root/assets/audio/sfx/$relick_sound.wav"
done

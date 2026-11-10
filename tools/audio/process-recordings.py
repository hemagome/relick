"""Build recorded CC0 effects from the included originals, without network access."""
import json
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[2]
recordings = [
    ("wooden-dice-roll-3.flac", "dice-roll", "highpass=f=70,acompressor=threshold=0.0630957:ratio=2.5:attack=1:release=55"),
    ("card-shuffle-bmaczero.wav", "card-shuffle", "highpass=f=70,acompressor=threshold=0.0630957:ratio=3:attack=0.1:release=45"),
]

for original, name, filters in recordings:
    filters = "aformat=sample_rates=44100:channel_layouts=stereo," + filters
    source = root / "assets/audio/sources" / original
    target = root / "assets/audio/sfx" / f"{name}.wav"
    metadata = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "json", str(source),
    ]))
    duration = float(metadata["format"]["duration"])
    # Preserve contacts, timing and pitch; only clean rumble and balance transients.
    measurement = subprocess.run([
        "ffmpeg", "-hide_banner", "-i", str(source), "-af", filters + ",volumedetect", "-f", "null", "-",
    ], capture_output=True, text=True, check=True)
    match = re.search(r"max_volume: (-?[0-9.]+) dB", measurement.stderr)
    if not match:
        raise RuntimeError(f"Could not measure the recorded {name} peak.")
    gain = -3.0 - float(match.group(1))
    filters += f",volume={gain:.3f}dB,afade=t=in:d=0.002,afade=t=out:st={duration - 0.015:.6f}:d=0.015"
    subprocess.run([
        "ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(source), "-af", filters,
        "-ar", "44100", "-ac", "2", "-c:a", "pcm_s16le", str(target),
    ], check=True)
    print(f"Recorded {name}: {duration:.3f}s; peak target -3 dBFS; {target}")

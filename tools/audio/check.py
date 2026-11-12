"""Verify the rendered masters without external Python packages."""
import array
import math
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
# Beats, tempo and whether the piece loops; these match the .import files.
music = {"menu": (144, 126, True), "yuno-combat": (192, 160, True), "yuno-climax": (160, 168, True),
         "yuno-to-combat": (8, 160, False), "yuno-to-climax": (8, 168, False)}
paths = [path for folder in ("music", "sfx") for path in (root / "assets/audio" / folder).iterdir()]
for path in sorted(paths):
    if path.suffix not in (".wav", ".ogg"):
        continue
    decoded = subprocess.check_output([
        "ffmpeg", "-hide_banner", "-loglevel", "error", "-i", str(path),
        "-f", "s16le", "-ac", "2", "-ar", "44100", "-",
    ])
    samples = array.array("h", decoded)
    duration = len(samples) / (2 * 44100)
    peak = max(abs(value) for value in samples) / 32768
    rms = math.sqrt(sum(value * value for value in samples) / len(samples)) / 32768
    assert 0.01 < rms < 0.30 and peak < 0.999, (path, rms, peak)
    if path.parent.name == "music":
        beats, bpm, loop = music[path.stem]
        if loop:
            assert abs(duration - beats * 60 / bpm) < 0.02, (path, duration)
            # A seam must be no sharper than ordinary adjacent samples in the recording.
            step = max(abs(samples[i] - samples[i - 2]) for i in range(2, len(samples)))
            seam = max(abs(samples[0] - samples[-2]), abs(samples[1] - samples[-1]))
            assert seam <= max(400, step), (path, "loop seam", seam, step)
        else:
            # A lead-in keeps a tail after its last beat, to ring over the next piece.
            assert 1 < duration - beats * 60 / bpm < 4, (path, duration)
            assert abs(samples[-1]) < 100 and abs(samples[-2]) < 100
    else:
        assert 0.1 <= duration <= 1.5
        assert abs(samples[0]) < 100 and abs(samples[-1]) < 100
    print(f"AUDIO_ASSET_PASS {path.name}: {duration:.2f}s, peak {20*math.log10(peak):.1f} dBFS, RMS {20*math.log10(rms):.1f} dBFS")

"""Encode actual native verification frames; does not edit or generate sprite artwork."""
import json
from pathlib import Path
from PIL import Image

output = Path(__file__).resolve().parent
timings = json.loads((output / "capture-timing.json").read_text())
paths = sorted(Path("/tmp/flowsim-b21-frames").glob("*.png"))
assert len(paths) == len(timings) == 48, "Run AnimationPreview first."
frames = [Image.open(path).convert("RGB") for path in paths]
palette = frames[0].quantize(colors=256)
encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
durations = [max(20, round((b["seconds"] - a["seconds"]) * 100) * 10) for a, b in zip(timings, timings[1:])]
durations.append(durations[-1])
encoded[0].save(output / "living-workers.gif", save_all=True, append_images=encoded[1:],
                duration=durations, loop=0, optimize=False, disposal=1)
print(output / "living-workers.gif")

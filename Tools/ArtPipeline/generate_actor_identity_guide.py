#!/usr/bin/env python3
"""Export the canonical actor-slinger frame as a ComfyUI identity guide."""

from __future__ import annotations

import subprocess
import tempfile
from pathlib import Path

from PIL import Image

from art_review import PROJECT_ROOT
from art_runner import aseprite_binary


SOURCE = (
    PROJECT_ROOT
    / "Assets/_Project/Art/Source/Aseprite/actor-slinger.aseprite"
)
DESTINATION = (
    PROJECT_ROOT
    / "docs/art-direction/comfyui/guides/"
    "actor-slinger-runtime-source-512-v1.png"
)
BACKGROUND = (255, 0, 255, 255)
CANVAS = (512, 512)


def main() -> int:
    if not SOURCE.is_file():
        raise SystemExit(f"missing canonical actor source: {SOURCE}")
    with tempfile.TemporaryDirectory(prefix="project-c-actor-guide-") as raw:
        exported = Path(raw) / "actor-slinger.png"
        subprocess.run(
            [
                str(aseprite_binary()),
                "--batch",
                "--oneframe",
                str(SOURCE),
                "--save-as",
                str(exported),
            ],
            cwd=PROJECT_ROOT,
            check=True,
        )
        with Image.open(exported) as opened:
            actor = opened.convert("RGBA")
        scale = min(
            CANVAS[0] // actor.width,
            CANVAS[1] // actor.height,
        )
        actor = actor.resize(
            (actor.width * scale, actor.height * scale),
            Image.Resampling.NEAREST,
        )
        background = Image.new("RGBA", CANVAS, BACKGROUND)
        background.alpha_composite(
            actor,
            ((CANVAS[0] - actor.width) // 2, CANVAS[1] - actor.height),
        )
        DESTINATION.parent.mkdir(parents=True, exist_ok=True)
        background.convert("RGB").save(DESTINATION)
    print(DESTINATION)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

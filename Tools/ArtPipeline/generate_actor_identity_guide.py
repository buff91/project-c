#!/usr/bin/env python3
"""Export one canonical actor frame as a ComfyUI identity guide."""

from __future__ import annotations

import argparse
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw

from art_review import PROJECT_ROOT
from art_runner import aseprite_binary


DEFAULT_SOURCE = (
    PROJECT_ROOT
    / "Assets/_Project/Art/Source/Aseprite/actor-slinger.aseprite"
)
DEFAULT_DESTINATION = (
    PROJECT_ROOT
    / "docs/art-direction/comfyui/guides/"
    "actor-slinger-runtime-source-512-v1.png"
)
BACKGROUND = (255, 0, 255, 255)
CANVAS = (512, 512)


def actor_image(source: Path, temporary_dir: Path) -> Image.Image:
    if source.suffix.lower() in {".ase", ".aseprite"}:
        exported = temporary_dir / f"{source.stem}.png"
        subprocess.run(
            [
                str(aseprite_binary()),
                "--batch",
                "--oneframe",
                str(source),
                "--save-as",
                str(exported),
            ],
            cwd=PROJECT_ROOT,
            check=True,
        )
        path = exported
    else:
        path = source
    with Image.open(path) as opened:
        return opened.convert("RGBA")


def parse_box(value: str) -> tuple[int, int, int, int]:
    try:
        x, y, width, height = (int(item) for item in value.split(","))
    except (TypeError, ValueError) as exc:
        raise argparse.ArgumentTypeError(
            "clear boxes must be X,Y,WIDTH,HEIGHT"
        ) from exc
    if min(x, y) < 0 or min(width, height) <= 0:
        raise argparse.ArgumentTypeError(
            "clear boxes require non-negative X/Y and positive size"
        )
    return x, y, width, height


def build_guide(
    source: Path,
    destination: Path,
    clear_boxes: list[tuple[int, int, int, int]] | None = None,
) -> Path:
    if not source.is_file():
        raise SystemExit(f"missing canonical actor source: {source}")
    with tempfile.TemporaryDirectory(prefix="project-c-actor-guide-") as raw:
        actor = actor_image(source, Path(raw))
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
        if clear_boxes:
            draw = ImageDraw.Draw(background)
            for x, y, width, height in clear_boxes:
                draw.rectangle(
                    (x, y, x + width - 1, y + height - 1),
                    fill=BACKGROUND,
                )
        destination.parent.mkdir(parents=True, exist_ok=True)
        background.convert("RGB").save(destination)
    return destination


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_DESTINATION)
    parser.add_argument(
        "--clear-box",
        action="append",
        default=[],
        type=parse_box,
        metavar="X,Y,WIDTH,HEIGHT",
        help=(
            "Replace an obsolete baked-equipment region with magenta before "
            "img2img. Repeat for multiple regions."
        ),
    )
    return parser


def main() -> int:
    args = build_parser().parse_args()
    source = args.source.expanduser().resolve()
    destination = args.output.expanduser().resolve()
    print(build_guide(source, destination, args.clear_box))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

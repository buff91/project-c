#!/usr/bin/env python3
"""Generate or publish one Project-C art asset into its Aseprite SSOT slot.

`publish` takes an existing generated PNG.
`generate` runs a ComfyUI API workflow first, then publishes one output.

This low-level command publishes one static source. Multi-shot animation and
effect key poses are expanded by art_runner.py, then handed to Aseprite as an
editable source set; interpolation and timing remain deliberate Aseprite work.
"""

from __future__ import annotations

import argparse
from collections import deque
import os
import re
import shutil
import statistics
import subprocess
import sys
import uuid
from pathlib import Path
from typing import Any

from PIL import Image

import comfy_batch


PROJECT_ROOT = Path(__file__).resolve().parents[2]
ASEPRITE_SOURCE_DIR = (
    PROJECT_ROOT / "Assets/_Project/Art/Source/Aseprite"
)
ASEPRITE_CONFORM = Path(__file__).with_name("aseprite_conform.sh")
DEFAULT_RAW_DIR = PROJECT_ROOT / "docs/art-direction/comfyui/output"
SLOT_PATTERN = re.compile(
    r"^(actor|env|item|marker|prop|fx)-[a-z0-9][a-z0-9-]*$"
)


class AssetError(RuntimeError):
    pass


def parse_hex_color(text: str) -> tuple[int, int, int]:
    value = text.strip().lstrip("#")
    if len(value) != 6 or any(ch not in "0123456789abcdefABCDEF" for ch in value):
        raise argparse.ArgumentTypeError("color must be RRGGBB or #RRGGBB")
    return tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))


def parse_key_color(text: str) -> tuple[int, int, int] | str:
    if text.strip().lower() == "auto":
        return "auto"
    return parse_hex_color(text)


def validate_slot(slot: str) -> None:
    if not SLOT_PATTERN.fullmatch(slot):
        raise AssetError(
            f"Invalid slot {slot!r}; expected actor-/env-/item-/marker-/prop-/fx- "
            "followed by lowercase kebab-case"
        )


def remove_chroma_key(
    image: Image.Image,
    key: tuple[int, int, int],
    tolerance: int,
) -> Image.Image:
    rgba = image.convert("RGBA")
    pixel_data = (
        rgba.get_flattened_data()
        if hasattr(rgba, "get_flattened_data")
        else rgba.getdata()
    )
    pixels = list(pixel_data)
    threshold = tolerance * tolerance
    output = []
    for red, green, blue, alpha in pixels:
        distance = (
            (red - key[0]) * (red - key[0])
            + (green - key[1]) * (green - key[1])
            + (blue - key[2]) * (blue - key[2])
        )
        if distance <= threshold:
            output.append((red, green, blue, 0))
        else:
            output.append((red, green, blue, alpha))
    rgba.putdata(output)
    return rgba


def detect_border_color(image: Image.Image) -> tuple[int, int, int]:
    rgb = image.convert("RGB")
    pixels = []
    for x in range(rgb.width):
        pixels.append(rgb.getpixel((x, 0)))
        pixels.append(rgb.getpixel((x, rgb.height - 1)))
    for y in range(1, rgb.height - 1):
        pixels.append(rgb.getpixel((0, y)))
        pixels.append(rgb.getpixel((rgb.width - 1, y)))
    if not pixels:
        raise AssetError("Cannot detect a chroma key from an empty border")
    return tuple(
        round(statistics.median(pixel[channel] for pixel in pixels))
        for channel in range(3)
    )


def keep_largest_alpha_component(
    image: Image.Image,
    cutoff: int,
) -> Image.Image:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    alpha = rgba.getchannel("A")
    visible = [
        alpha.getpixel((x, y)) >= cutoff
        for y in range(height)
        for x in range(width)
    ]
    visited = bytearray(width * height)
    largest: list[int] = []
    neighbors = (
        (-1, -1), (0, -1), (1, -1),
        (-1, 0),            (1, 0),
        (-1, 1),  (0, 1),   (1, 1),
    )
    for start in range(width * height):
        if visited[start] or not visible[start]:
            continue
        visited[start] = 1
        queue = deque([start])
        component: list[int] = []
        while queue:
            index = queue.popleft()
            component.append(index)
            x = index % width
            y = index // width
            for dx, dy in neighbors:
                nx = x + dx
                ny = y + dy
                if not 0 <= nx < width or not 0 <= ny < height:
                    continue
                neighbor = ny * width + nx
                if visited[neighbor] or not visible[neighbor]:
                    continue
                visited[neighbor] = 1
                queue.append(neighbor)
        if len(component) > len(largest):
            largest = component

    if not largest:
        return rgba
    keep = bytearray(width * height)
    for index in largest:
        keep[index] = 1
    pixel_data = (
        rgba.get_flattened_data()
        if hasattr(rgba, "get_flattened_data")
        else rgba.getdata()
    )
    pixels = list(pixel_data)
    output = [
        pixel if keep[index] else (pixel[0], pixel[1], pixel[2], 0)
        for index, pixel in enumerate(pixels)
    ]
    rgba.putdata(output)
    return rgba


def alpha_bounds(image: Image.Image, cutoff: int) -> tuple[int, int, int, int] | None:
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value >= cutoff else 0)
    return mask.getbbox()


def prepare_image(
    source: Path,
    destination: Path,
    *,
    width: int,
    height: int,
    fit: str,
    anchor: str,
    padding: int,
    alpha_cutoff: int,
    key_color: tuple[int, int, int] | None,
    key_tolerance: int,
    trim_detached: bool = False,
) -> None:
    if not source.is_file():
        raise AssetError(f"Generated source does not exist: {source}")
    image = Image.open(source).convert("RGBA")
    if key_color is not None:
        image = remove_chroma_key(image, key_color, key_tolerance)
    if trim_detached:
        image = keep_largest_alpha_component(image, alpha_cutoff)

    if fit == "strict":
        if image.size != (width, height):
            raise AssetError(
                f"Strict canvas mismatch for {source}: expected {width}x{height}, "
                f"got {image.width}x{image.height}"
            )
        prepared = image
    else:
        bounds = alpha_bounds(image, alpha_cutoff)
        if bounds is None:
            raise AssetError(f"Source becomes empty after alpha cleanup: {source}")
        cropped = image.crop(bounds)
        available_width = width - padding * 2
        available_height = height - padding * 2
        if available_width <= 0 or available_height <= 0:
            raise AssetError("Padding leaves no usable canvas area")
        scale = min(
            available_width / cropped.width,
            available_height / cropped.height,
        )
        resized_size = (
            max(1, round(cropped.width * scale)),
            max(1, round(cropped.height * scale)),
        )
        resized = cropped.resize(resized_size, Image.Resampling.LANCZOS)
        prepared = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        x = (width - resized.width) // 2
        if anchor == "bottom":
            y = height - padding - resized.height
        else:
            y = (height - resized.height) // 2
        prepared.alpha_composite(resized, (x, y))

    destination.parent.mkdir(parents=True, exist_ok=True)
    prepared.save(destination)


def official_output(slot: str) -> Path:
    return ASEPRITE_SOURCE_DIR / f"{slot}.aseprite"


def aseprite_binary() -> Path:
    configured = os.environ.get("PROJECTC_ASEPRITE_BIN")
    candidates = [
        Path(configured).expanduser() if configured else None,
        Path("/Applications/Aseprite.app/Contents/MacOS/aseprite"),
        Path.home() / "Applications/Aseprite.app/Contents/MacOS/aseprite",
        (
            Path.home()
            / "Library/Application Support/Steam/steamapps/common"
            / "Aseprite/Aseprite.app/Contents/MacOS/aseprite"
        ),
    ]
    discovered = shutil.which("aseprite")
    if discovered:
        candidates.append(Path(discovered))
    for candidate in candidates:
        if candidate and candidate.is_file() and os.access(candidate, os.X_OK):
            return candidate.resolve()
    raise AssetError(
        "Aseprite CLI not found; set PROJECTC_ASEPRITE_BIN"
    )


def conform_to_aseprite(
    prepared: Path,
    output: Path,
    *,
    width: int,
    height: int,
    force: bool,
) -> None:
    if output.exists() and not force:
        raise AssetError(
            f"Refusing to overwrite existing Aseprite source: {output}. "
            "Pass --force only after reviewing the replacement."
        )
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            str(ASEPRITE_CONFORM),
            str(prepared),
            str(output),
            str(width),
            str(height),
            "strict",
        ],
        cwd=PROJECT_ROOT,
        env={
            **os.environ,
            "PROJECTC_ASEPRITE_BIN": str(aseprite_binary()),
        },
        check=True,
    )
    if not output.is_file():
        raise AssetError(f"Aseprite did not create expected output: {output}")


def publish(
    args: argparse.Namespace,
    source: Path,
    *,
    raw_dir: Path,
) -> Path:
    validate_slot(args.slot)
    output = args.output or official_output(args.slot)
    prepared = args.prepared_output or raw_dir / f"{args.slot}-prepared.png"
    key_color = args.key_color
    if key_color == "auto":
        with Image.open(source) as source_image:
            key_color = detect_border_color(source_image)
    prepare_image(
        source,
        prepared,
        width=args.width,
        height=args.height,
        fit=args.fit,
        anchor=args.anchor,
        padding=args.padding,
        alpha_cutoff=args.alpha_cutoff,
        key_color=key_color,
        key_tolerance=args.key_tolerance,
        trim_detached=args.trim_detached,
    )
    conform_to_aseprite(
        prepared,
        output,
        width=args.width,
        height=args.height,
        force=args.force,
    )
    print(f"prepared: {prepared}")
    print(f"aseprite: {output}")
    return output


def run_comfy(args: argparse.Namespace, raw_dir: Path) -> list[Path]:
    prompt = comfy_batch.load_prompt(args.workflow)
    comfy_batch.apply_overrides(prompt, args.set)
    comfy_batch.apply_uploads(args.url, prompt, args.upload)
    response = comfy_batch.request_json(
        args.url,
        "/prompt",
        method="POST",
        payload={"prompt": prompt, "client_id": uuid.uuid4().hex},
        timeout=120.0,
    )
    prompt_id = response.get("prompt_id")
    if not prompt_id:
        raise AssetError(f"ComfyUI did not return a prompt_id: {response}")
    print(f"prompt_id: {prompt_id}")
    record = comfy_batch.wait_for_history(
        args.url,
        prompt_id,
        timeout=args.timeout,
        poll_interval=args.poll_interval,
    )
    outputs = comfy_batch.download_outputs(args.url, record, raw_dir)
    if not outputs:
        raise AssetError(f"ComfyUI job {prompt_id} returned no image outputs")
    for output in outputs:
        print(f"raw: {output}")
    return outputs


def add_publish_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--slot", required=True)
    parser.add_argument("--width", required=True, type=int)
    parser.add_argument("--height", required=True, type=int)
    parser.add_argument(
        "--fit",
        choices=("strict", "contain"),
        default="contain",
        help="strict requires an exact canvas; contain trims and fits the subject",
    )
    parser.add_argument(
        "--anchor",
        choices=("bottom", "center"),
        default="bottom",
    )
    parser.add_argument("--padding", type=int, default=2)
    parser.add_argument("--alpha-cutoff", type=int, default=80)
    parser.add_argument(
        "--key-color",
        type=parse_key_color,
        help="RRGGBB, #RRGGBB, or auto (median border color)",
    )
    parser.add_argument("--key-tolerance", type=int, default=8)
    parser.add_argument(
        "--trim-detached",
        action="store_true",
        help="Remove alpha components disconnected from the largest subject",
    )
    parser.add_argument("--output", type=Path)
    parser.add_argument("--prepared-output", type=Path)
    parser.add_argument("--force", action="store_true")


def command_publish(args: argparse.Namespace) -> None:
    raw_dir = args.raw_dir.resolve()
    publish(args, args.input.resolve(), raw_dir=raw_dir)


def command_generate(args: argparse.Namespace) -> None:
    raw_dir = (args.raw_dir / args.slot).resolve()
    outputs = run_comfy(args, raw_dir)
    if args.output_index < 0 or args.output_index >= len(outputs):
        raise AssetError(
            f"--output-index {args.output_index} is outside 0..{len(outputs) - 1}"
        )
    publish(args, outputs[args.output_index], raw_dir=raw_dir)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    publish_parser = subparsers.add_parser(
        "publish",
        help="Conform an existing generated PNG into an Aseprite slot",
    )
    publish_parser.add_argument("input", type=Path)
    publish_parser.add_argument("--raw-dir", type=Path, default=DEFAULT_RAW_DIR)
    add_publish_arguments(publish_parser)
    publish_parser.set_defaults(handler=command_publish)

    generate_parser = subparsers.add_parser(
        "generate",
        help="Run ComfyUI and conform one returned image",
    )
    generate_parser.add_argument("workflow", type=Path)
    generate_parser.add_argument("--url", default=comfy_batch.DEFAULT_URL)
    generate_parser.add_argument(
        "--set",
        action="append",
        default=[],
        metavar="NODE.INPUT=JSON",
    )
    generate_parser.add_argument(
        "--upload",
        action="append",
        default=[],
        metavar="NODE.INPUT=PATH",
    )
    generate_parser.add_argument(
        "--raw-dir",
        type=Path,
        default=DEFAULT_RAW_DIR,
    )
    generate_parser.add_argument("--output-index", type=int, default=0)
    generate_parser.add_argument("--timeout", type=float, default=1800.0)
    generate_parser.add_argument("--poll-interval", type=float, default=1.0)
    add_publish_arguments(generate_parser)
    generate_parser.set_defaults(handler=command_generate)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    try:
        if args.width <= 0 or args.height <= 0:
            raise AssetError("Width and height must be positive")
        if args.padding < 0:
            raise AssetError("Padding cannot be negative")
        if not 0 <= args.alpha_cutoff <= 255:
            raise AssetError("Alpha cutoff must be in 0..255")
        if not 0 <= args.key_tolerance <= 255:
            raise AssetError("Key tolerance must be in 0..255")
        args.handler(args)
    except (
        AssetError,
        comfy_batch.ComfyError,
        OSError,
        ValueError,
        subprocess.CalledProcessError,
    ) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Generate or publish one Project-C art asset into its Aseprite SSOT slot.

`publish` takes an existing generated PNG.
`generate` runs a ComfyUI API workflow first, then publishes one output.

Animated actors stop at an idle/base source. Walk/attack/hit/fall/death frames
remain deliberate Aseprite work; this tool does not synthesize animation.
"""

from __future__ import annotations

import argparse
import re
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
SLOT_PATTERN = re.compile(r"^(actor|env|item|marker|prop)-[a-z0-9][a-z0-9-]*$")


class AssetError(RuntimeError):
    pass


def parse_hex_color(text: str) -> tuple[int, int, int]:
    value = text.strip().lstrip("#")
    if len(value) != 6 or any(ch not in "0123456789abcdefABCDEF" for ch in value):
        raise argparse.ArgumentTypeError("color must be RRGGBB or #RRGGBB")
    return tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))


def validate_slot(slot: str) -> None:
    if not SLOT_PATTERN.fullmatch(slot):
        raise AssetError(
            f"Invalid slot {slot!r}; expected actor-/env-/item-/marker-/prop- "
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
) -> None:
    if not source.is_file():
        raise AssetError(f"Generated source does not exist: {source}")
    image = Image.open(source).convert("RGBA")
    if key_color is not None:
        image = remove_chroma_key(image, key_color, key_tolerance)

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
    prepare_image(
        source,
        prepared,
        width=args.width,
        height=args.height,
        fit=args.fit,
        anchor=args.anchor,
        padding=args.padding,
        alpha_cutoff=args.alpha_cutoff,
        key_color=args.key_color,
        key_tolerance=args.key_tolerance,
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
    parser.add_argument("--key-color", type=parse_hex_color)
    parser.add_argument("--key-tolerance", type=int, default=8)
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

#!/usr/bin/env python3
"""Normalize generated environment art into Project-C's runtime sprite sizes."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Temp/ArtPipeline/Environment"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"


def trim_visible(image: Image.Image, alpha_cutoff: int = 32) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A").point(lambda value: 255 if value >= alpha_cutoff else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError("source image has no visible pixels")

    rgba.putalpha(alpha)
    return rgba.crop(bbox)


def fit_to_canvas(source_name: str, output_name: str, size: tuple[int, int], margin: int = 0) -> Image.Image:
    source = trim_visible(Image.open(SOURCE / source_name))
    available_width = size[0] - margin * 2
    available_height = size[1] - margin * 2
    scale = min(available_width / source.width, available_height / source.height)
    target = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )

    sprite = source.resize(target, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - sprite.width) // 2
    y = margin
    canvas.alpha_composite(sprite, (x, y))
    canvas.save(OUTPUT / output_name, optimize=True)
    return canvas


def save_mirror(image: Image.Image, output_name: str) -> None:
    image.transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(OUTPUT / output_name, optimize=True)


def add_floor_under(asset: Image.Image, floor: Image.Image, output_name: str) -> Image.Image:
    composed = Image.new("RGBA", asset.size, (0, 0, 0, 0))
    composed.alpha_composite(floor, (0, 0))
    composed.alpha_composite(asset, (0, 0))
    composed.save(OUTPUT / output_name, optimize=True)
    return composed


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)

    floor = fit_to_canvas("floor-alpha.png", "env-floor.png", (64, 32))

    wall = fit_to_canvas("wall-right-alpha.png", "env-wall-rising-right.png", (32, 56))
    save_mirror(wall, "env-wall-rising-left.png")

    torch = fit_to_canvas("wall-torch-right-alpha.png", "env-wall-torch-rising-right.png", (32, 56))
    save_mirror(torch, "env-wall-torch-rising-left.png")

    closed_door = fit_to_canvas("door-closed-right-alpha.png", "env-door-closed-rising-right.png", (64, 80), 1)
    closed = add_floor_under(closed_door, floor, "env-door-closed-rising-right.png")
    save_mirror(closed, "env-door-closed-rising-left.png")

    opened_door = fit_to_canvas("door-open-right-alpha.png", "env-door-open-rising-right.png", (64, 80), 1)
    opened = add_floor_under(opened_door, floor, "env-door-open-rising-right.png")
    save_mirror(opened, "env-door-open-rising-left.png")

    stairs_asset = fit_to_canvas("stairs-right-alpha.png", "env-stairs-rising-right.png", (64, 56), 1)
    stairs = add_floor_under(stairs_asset, floor, "env-stairs-rising-right.png")
    save_mirror(stairs, "env-stairs-rising-left.png")

    stairs_up_asset = fit_to_canvas("stairs-up-right-alpha.png", "env-stairs-up-rising-right.png", (64, 56), 1)
    stairs_up = add_floor_under(stairs_up_asset, floor, "env-stairs-up-rising-right.png")
    save_mirror(stairs_up, "env-stairs-up-rising-left.png")

    stairs_down = fit_to_canvas("stairs-down-right-alpha.png", "env-stairs-down-rising-right.png", (64, 40))
    save_mirror(stairs_down, "env-stairs-down-rising-left.png")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Conform the first arcade-occupation roster into 96x128 static sprites.

The generated sources establish silhouette and materials only.  This deterministic
pass owns alpha, the project palette, 2x2 pixel clusters, canvas size, foot
baseline, and the small hostile IFF signal that must survive downsampling.  The
approved south-facing result is also the identity anchor for directional Aseprite
timelines.
"""

from __future__ import annotations

from dataclasses import dataclass
import math
from pathlib import Path

from PIL import Image

from torchstone_palette import load_gpl_entries, lock_rgba_to_named_palette
from art_asset import detect_border_color


ROOT = Path(__file__).resolve().parents[2]
RUNTIME_OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
PREVIEW = ROOT / "docs/captures/arcade-occupation-actors-conform-preview-v1.png"

CANVAS_SIZE = (96, 128)
GROUND_ROW = 123
ALPHA_CUTOFF = 80
PIXEL_CLUSTER = 2
PREVIEW_SCALE = 6
CHROMA_INNER_DISTANCE = 12
CHROMA_OUTER_DISTANCE = 220

# Anonymous infantry and industrial machinery share a cool cyberpunk material
# base.  The anomaly ramp is physical coated metal / display glass here, while
# bright neon remains a small cable, visor, or corporate mark.  Warning colors
# stay one hostile IFF point instead of flooding the body.
ACTOR_PALETTE_NAMES = (
    "dark-void",
    "dark-cool",
    "dark-warm",
    "grey-1",
    "grey-2",
    "grey-3",
    "grey-4",
    "grey-5",
    "grey-6",
    "rust-1",
    "rust-2",
    "rust-3",
    "rust-4",
    "fabric-1",
    "fabric-2",
    "fabric-3",
    "fabric-4",
    "fabric-5",
    "sludge-1",
    "sludge-2",
    "sludge-3",
    "sludge-4",
    "anomaly-1",
    "anomaly-2",
    "anomaly-3",
    "anomaly-4",
    "sig-warning",
    "sig-warning-deep",
    "sig-hazard",
    "sig-ice",
    "sig-teal-item",
    "sig-neon-cyan",
    "sig-neon-magenta",
)


@dataclass(frozen=True)
class ActorAccent:
    anchor: tuple[float, float]
    color_names: tuple[str, ...]


@dataclass(frozen=True)
class ActorSpec:
    source: Path
    output_name: str
    visible_max: tuple[int, int]
    signal_anchor: tuple[float, float]
    signal_length: int = 1
    palette_profile: str = "corporate"
    accents: tuple[ActorAccent, ...] = ()


SPECS = (
    ActorSpec(
        ROOT / "docs/art-direction/project-c-occupation-assault-source-v1.png",
        "actor-goblin",
        (56, 94),
        (0.50, 0.13),
        palette_profile="occupation",
        accents=(
            ActorAccent((0.23, 0.36), ("sig-neon-magenta",) * 3),
        ),
    ),
    ActorSpec(
        ROOT / "docs/art-direction/project-c-corporate-marksman-source-v1.png",
        "actor-slinger",
        (56, 96),
        (0.50, 0.15),
        2,
        accents=(
            ActorAccent((0.52, 0.24), ("sig-neon-cyan",) * 3),
        ),
    ),
    ActorSpec(
        ROOT / "docs/art-direction/project-c-corporate-riot-robot-source-v1.png",
        "actor-skeleton",
        (64, 100),
        (0.50, 0.14),
        2,
        accents=(
            ActorAccent((0.50, 0.28), ("sig-neon-cyan",) * 4),
        ),
    ),
    ActorSpec(
        ROOT / "docs/art-direction/project-c-corporate-pursuit-drone-source-v2.png",
        "actor-slime",
        (64, 56),
        (0.17, 0.46),
        palette_profile="pursuit",
        accents=(
            ActorAccent((0.17, 0.57), ("sig-neon-cyan",) * 3),
            ActorAccent((0.52, 0.18), ("sig-neon-magenta",) * 3),
        ),
    ),
    ActorSpec(
        ROOT / "docs/art-direction/project-c-arc-inspection-drone-source-v1.png",
        "actor-arc-drone",
        (68, 52),
        (0.24, 0.48),
        accents=(
            ActorAccent((0.50, 0.24), ("sig-neon-cyan",) * 4),
            ActorAccent((0.74, 0.48), ("sig-neon-magenta",) * 2),
        ),
    ),
    ActorSpec(
        ROOT / "docs/art-direction/project-c-cyberpsycho-warden-source-v1.png",
        "actor-grave-warden",
        (72, 104),
        (0.50, 0.16),
        3,
        palette_profile="cyberpsycho",
        accents=(
            ActorAccent((0.31, 0.38), ("sig-neon-magenta",) * 4),
            ActorAccent((0.70, 0.43), ("sig-neon-cyan",) * 3),
        ),
    ),
)


COMMON_COOL_REMAP = {
    "dark-warm": "dark-cool",
    "fabric-1": "grey-1",
    "fabric-2": "grey-2",
}

PROFILE_REMAPS = {
    "occupation": {
        "fabric-3": "grey-3",
        "fabric-4": "anomaly-2",
        "fabric-5": "grey-5",
        "sludge-1": "anomaly-1",
        "sludge-2": "anomaly-2",
        "sludge-3": "anomaly-3",
        "sludge-4": "anomaly-4",
    },
    "corporate": {
        "fabric-3": "anomaly-2",
        "fabric-4": "anomaly-3",
        "fabric-5": "grey-6",
        "sludge-1": "anomaly-1",
        "sludge-2": "anomaly-2",
        "sludge-3": "anomaly-3",
        "sludge-4": "anomaly-4",
    },
    "pursuit": {
        "fabric-3": "grey-3",
        "fabric-4": "anomaly-2",
        "fabric-5": "grey-5",
        "sludge-1": "grey-1",
        "sludge-2": "grey-2",
        "sludge-3": "anomaly-2",
        "sludge-4": "anomaly-3",
        "rust-3": "grey-3",
        "rust-4": "grey-5",
    },
    "cyberpsycho": {
        "fabric-3": "anomaly-1",
        "fabric-4": "anomaly-2",
        "fabric-5": "grey-5",
        "sludge-1": "dark-cool",
        "sludge-2": "anomaly-1",
        "sludge-3": "anomaly-2",
        "sludge-4": "anomaly-3",
    },
}


def _remove_generated_chroma(image: Image.Image) -> Image.Image:
    """Build a soft alpha matte from the generated image's sampled border.

    ImageGen's magenta plate is deliberately not a single exact RGB value.  The
    soft distance matte removes that variation, while the magenta-dominance gate
    strips the last saturated fringe without touching red IFF or cyan arc pixels.
    """
    rgba = image.convert("RGBA")
    # 이미 투명화한 승인 소스에는 키 색 정보가 없다. 투명 border의 RGB(0,0,0)를
    # 키로 오인하면 검은 장비 암부까지 지워지므로, 완전 불투명 생성 원본에만 매트를 만든다.
    if rgba.getchannel("A").getextrema()[0] < ALPHA_CUTOFF:
        return rgba
    key = detect_border_color(rgba)
    pixels = list(rgba.get_flattened_data())
    output: list[tuple[int, int, int, int]] = []
    distance_span = CHROMA_OUTER_DISTANCE - CHROMA_INNER_DISTANCE
    for red, green, blue, alpha in pixels:
        distance = math.sqrt(
            (red - key[0]) ** 2
            + (green - key[1]) ** 2
            + (blue - key[2]) ** 2
        )
        strongly_magenta = (
            red >= 120
            and blue >= 100
            and min(red, blue) - green >= 60
        )
        if distance <= CHROMA_INNER_DISTANCE or strongly_magenta:
            matte_alpha = 0
        elif distance >= CHROMA_OUTER_DISTANCE:
            matte_alpha = alpha
        else:
            matte_alpha = round(
                alpha
                * (distance - CHROMA_INNER_DISTANCE)
                / distance_span
            )
        output.append((red, green, blue, matte_alpha))
    rgba.putdata(output)
    return rgba


def _harden_alpha(image: Image.Image) -> Image.Image:
    result = image.convert("RGBA")
    result.putalpha(
        result.getchannel("A").point(
            lambda value: 255 if value >= ALPHA_CUTOFF else 0
        )
    )
    return result


def _lock_actor_palette(image: Image.Image) -> Image.Image:
    return lock_rgba_to_named_palette(image, ACTOR_PALETTE_NAMES)


def _apply_role_palette(image: Image.Image, spec: ActorSpec) -> Image.Image:
    """Move generated beige/brown masses into the actor's semantic cool ramp."""
    try:
        profile = PROFILE_REMAPS[spec.palette_profile]
    except KeyError as error:
        raise ValueError(
            f"unknown actor palette profile: {spec.palette_profile}"
        ) from error

    entries = dict(load_gpl_entries())
    mapping_names = {**COMMON_COOL_REMAP, **profile}
    mapping = {
        entries[source_name]: entries[target_name]
        for source_name, target_name in mapping_names.items()
    }
    result = image.convert("RGBA").copy()
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            red, green, blue, alpha = pixels[x, y]
            replacement = mapping.get((red, green, blue))
            if alpha != 0 and replacement is not None:
                pixels[x, y] = (*replacement, alpha)
    return result


def _paint_opaque_run(
    image: Image.Image,
    anchor: tuple[float, float],
    colors: tuple[tuple[int, int, int], ...],
) -> Image.Image:
    """Paint the nearest opaque working-grid run without growing the silhouette."""
    result = image.convert("RGBA").copy()
    alpha = result.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("cannot paint an empty actor")

    length = len(colors)
    target_x = bounds[0] + anchor[0] * (bounds[2] - bounds[0] - 1)
    target_y = bounds[1] + anchor[1] * (bounds[3] - bounds[1] - 1)
    candidates: list[tuple[float, int, int]] = []
    for y in range(bounds[1], bounds[3]):
        for x in range(bounds[0], bounds[2] - length + 1):
            if all(alpha.getpixel((x + offset, y)) != 0 for offset in range(length)):
                center_x = x + (length - 1) / 2
                distance = abs(center_x - target_x) + abs(y - target_y) * 1.35
                candidates.append((distance, y, x))
    if not candidates:
        raise ValueError("actor has no opaque accent run")

    _, y, x = min(candidates)
    pixels = result.load()
    for offset, color in enumerate(colors):
        pixels[x + offset, y] = (*color, 255)
    return result


def _apply_accents(image: Image.Image, spec: ActorSpec) -> Image.Image:
    entries = dict(load_gpl_entries())
    result = image
    for accent in spec.accents:
        result = _paint_opaque_run(
            result,
            accent.anchor,
            tuple(entries[name] for name in accent.color_names),
        )
    return result


def _restore_hostile_signal(image: Image.Image, spec: ActorSpec) -> Image.Image:
    """Restore an IFF point without adding pixels to the actor silhouette."""
    entries = dict(load_gpl_entries())
    warning = entries["sig-warning"]
    warning_deep = entries["sig-warning-deep"]
    length = max(1, spec.signal_length)
    colors = tuple(
        warning if offset == length // 2 else warning_deep
        for offset in range(length)
    )
    return _paint_opaque_run(image, spec.signal_anchor, colors)


def extract_subject(source: Image.Image) -> Image.Image:
    subject = _harden_alpha(_remove_generated_chroma(source))
    bounds = subject.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("occupation actor source contains no visible subject")
    return subject.crop(bounds)


def build_actor(source: Image.Image, spec: ActorSpec) -> Image.Image:
    subject = extract_subject(source)
    working_max = (
        spec.visible_max[0] // PIXEL_CLUSTER,
        spec.visible_max[1] // PIXEL_CLUSTER,
    )
    scale = min(
        working_max[0] / subject.width,
        working_max[1] / subject.height,
    )
    size = (
        max(1, round(subject.width * scale)),
        max(1, round(subject.height * scale)),
    )
    subject = _harden_alpha(subject.resize(size, Image.Resampling.BOX))
    subject = _lock_actor_palette(subject)
    subject = _apply_role_palette(subject, spec)
    subject = _harden_alpha(subject)

    working_canvas = (
        CANVAS_SIZE[0] // PIXEL_CLUSTER,
        CANVAS_SIZE[1] // PIXEL_CLUSTER,
    )
    canvas = Image.new("RGBA", working_canvas, (0, 0, 0, 0))
    left = (working_canvas[0] - subject.width) // 2
    ground_row = GROUND_ROW // PIXEL_CLUSTER
    top = ground_row - subject.height + 1
    if top < 0:
        raise ValueError(
            f"occupation actor exceeds canvas: {spec.output_name} {subject.size}"
        )
    canvas.alpha_composite(subject, (left, top))
    canvas = _apply_accents(canvas, spec)
    canvas = _restore_hostile_signal(canvas, spec)
    return canvas.resize(CANVAS_SIZE, Image.Resampling.NEAREST)


def build_preview(actors: list[Image.Image]) -> Image.Image:
    cell_width = CANVAS_SIZE[0] * PREVIEW_SCALE
    cell_height = CANVAS_SIZE[1] * PREVIEW_SCALE
    preview = Image.new(
        "RGBA",
        (cell_width * len(actors), cell_height),
        (5, 7, 12, 255),
    )
    for index, actor in enumerate(actors):
        preview.alpha_composite(
            actor.resize((cell_width, cell_height), Image.Resampling.NEAREST),
            (index * cell_width, 0),
        )
    return preview.convert("RGB")


def main() -> None:
    actors: list[Image.Image] = []
    RUNTIME_OUTPUT.mkdir(parents=True, exist_ok=True)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    for spec in SPECS:
        if not spec.source.exists():
            raise FileNotFoundError(spec.source)
        actor = build_actor(Image.open(spec.source).convert("RGBA"), spec)
        actor.save(RUNTIME_OUTPUT / f"{spec.output_name}.png", optimize=True)
        actors.append(actor)

    build_preview(actors).save(PREVIEW, optimize=True)
    print(f"wrote {len(actors)} arcade occupation actors to {RUNTIME_OUTPUT}")
    print(f"wrote preview: {PREVIEW}")


if __name__ == "__main__":
    main()

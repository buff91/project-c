#!/usr/bin/env python3
"""Build the first production directional timeline for actor-knight.

ImageGen reference sheets provide pose and silhouette candidates only.  This
processor performs the deterministic production pass: cell extraction,
background removal, shared actor-palette locking, 48x64 working-grid grounding,
2x2 cluster expansion, frame QA, and an Aseprite animation manifest.

The official .aseprite is intentionally not overwritten here.  The manifest is
assembled into a scratch source with aseprite_build_animation.lua, visually
reviewed in Unity, and only then promoted while preserving the existing .meta.
"""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass
import json
from pathlib import Path
from statistics import median

from PIL import Image, ImageDraw

from torchstone_palette import load_gpl_entries


ROOT = Path(__file__).resolve().parents[2]
REFERENCE_ROOT = ROOT / "docs/art-direction/reference"
ACTION_REFERENCE = REFERENCE_ROOT / "ref-expeditioner-directional-actions-v1.png"
WALK_REFERENCE = REFERENCE_ROOT / "ref-expeditioner-directional-walk-v1.png"
BASE_SOUTH = ROOT / "Assets/_Project/Art/Runtime/actor-knight.png"
PALETTE = (
    ROOT / "Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl"
)
OUTPUT_ROOT = (
    ROOT / "docs/art-direction/comfyui/output/actor-knight-directional-v1"
)
FRAME_ROOT = OUTPUT_ROOT / "frames"
MANIFEST = OUTPUT_ROOT / "animation-manifest.json"
DRAFT_ASEPRITE = OUTPUT_ROOT / "actor-knight-directional-v1.aseprite"
PREVIEW = ROOT / "docs/captures/actor-knight-directional-conform-preview-v1.png"

CANVAS_SIZE = (96, 128)
WORKING_SIZE = (48, 64)
VISIBLE_MAX = (82, 116)
WORKING_VISIBLE_MAX = (VISIBLE_MAX[0] // 2, VISIBLE_MAX[1] // 2)
GROUND_ROW = 123
WORKING_GROUND_ROW = GROUND_ROW // 2
TARGET_UPRIGHT_HEIGHT = 58
ALPHA_CUTOFF = 72
PREVIEW_SCALE = 1

# Keep the same material ramps as the approved grounded Frame_0.  Directional
# frames may use fewer colors, never the entire project/UI palette.
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
    "fabric-1",
    "fabric-2",
    "fabric-3",
    "fabric-4",
    "fabric-5",
    "sig-warning",
    "sig-warning-deep",
    "skin-1",
    "skin-2",
    "skin-3",
    "hair-blonde-1",
    "hair-blonde-2",
)

DIRECTIONS = ("north", "east", "south", "west")


@dataclass(frozen=True)
class FrameSpec:
    tag: str
    index: int
    image: Image.Image
    duration_ms: int


class ReferenceGrid:
    def __init__(self, path: Path, rows: int, columns: int) -> None:
        if not path.exists():
            raise FileNotFoundError(path)
        self.path = path
        self.image = Image.open(path).convert("RGB")
        self.rows = rows
        self.columns = columns
        self._cache: dict[tuple[int, int], Image.Image] = {}

    def subject(self, row: int, column: int) -> Image.Image:
        if not 0 <= row < self.rows or not 0 <= column < self.columns:
            raise IndexError((row, column))
        key = (row, column)
        cached = self._cache.get(key)
        if cached is not None:
            return cached.copy()

        left = round(column * self.image.width / self.columns)
        right = round((column + 1) * self.image.width / self.columns)
        top = round(row * self.image.height / self.rows)
        bottom = round((row + 1) * self.image.height / self.rows)
        subject = extract_subject(self.image.crop((left, top, right, bottom)))
        self._cache[key] = subject
        return subject.copy()

    def upright_scale(self, row: int, anchor_column: int) -> float:
        subject = self.subject(row, anchor_column)
        return TARGET_UPRIGHT_HEIGHT / subject.height


def _background_candidate(pixel: tuple[int, int, int]) -> bool:
    low = min(pixel)
    high = max(pixel)
    mean = sum(pixel) / 3
    # Both generated references use a neutral charcoal radial field.  Very dark
    # outline pixels and brighter neutral jacket pixels deliberately fall
    # outside this band, so the border flood stops on the authored silhouette.
    return high - low <= 13 and 24 <= mean <= 88


def _border_flood_background(image: Image.Image) -> set[tuple[int, int]]:
    pixels = image.load()
    width, height = image.size
    queue: deque[tuple[int, int]] = deque()
    seen: set[tuple[int, int]] = set()

    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))

    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not _background_candidate(pixels[x, y]):
            continue
        seen.add((x, y))
        if x > 0:
            queue.append((x - 1, y))
        if x + 1 < width:
            queue.append((x + 1, y))
        if y > 0:
            queue.append((x, y - 1))
        if y + 1 < height:
            queue.append((x, y + 1))
    return seen


def _largest_component(mask: Image.Image) -> set[tuple[int, int]]:
    pixels = mask.load()
    width, height = mask.size
    remaining = {
        (x, y)
        for y in range(height)
        for x in range(width)
        if pixels[x, y] != 0
    }
    largest: set[tuple[int, int]] = set()
    while remaining:
        seed = remaining.pop()
        component = {seed}
        queue = deque([seed])
        while queue:
            x, y = queue.popleft()
            for neighbor in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    component.add(neighbor)
                    queue.append(neighbor)
        if len(component) > len(largest):
            largest = component
    return largest


def extract_subject(cell: Image.Image) -> Image.Image:
    rgb = cell.convert("RGB")
    background = _border_flood_background(rgb)
    mask = Image.new("L", rgb.size, 255)
    mask_pixels = mask.load()
    for x, y in background:
        mask_pixels[x, y] = 0

    # Generated sheets occasionally contain a detached one-pixel antialias or
    # faint ground speck.  Retain only the connected actor silhouette.
    component = _largest_component(mask)
    clean_mask = Image.new("L", rgb.size, 0)
    clean_pixels = clean_mask.load()
    for x, y in component:
        clean_pixels[x, y] = 255

    bounds = clean_mask.getbbox()
    if bounds is None:
        raise ValueError("reference cell contains no foreground subject")
    rgba = rgb.convert("RGBA")
    rgba.putalpha(clean_mask)
    return rgba.crop(bounds)


def actor_palette() -> tuple[list[tuple[int, int, int]], Image.Image]:
    entries = dict(load_gpl_entries())
    missing = [name for name in ACTOR_PALETTE_NAMES if name not in entries]
    if missing:
        raise ValueError(f"actor palette entries missing: {missing}")
    colors = [entries[name] for name in ACTOR_PALETTE_NAMES]
    palette = Image.new("P", (1, 1))
    flat = [channel for color in colors for channel in color]
    flat += list(colors[0]) * (256 - len(colors))
    palette.putpalette(flat)
    return colors, palette


def lock_palette(image: Image.Image, palette: Image.Image) -> Image.Image:
    source = image.convert("RGBA")
    alpha = source.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    background = Image.new("RGB", source.size, (5, 7, 12))
    background.paste(source.convert("RGB"), mask=alpha)
    locked = background.quantize(
        palette=palette,
        dither=Image.Dither.NONE,
    ).convert("RGBA")
    locked.putalpha(alpha)
    return locked


def _foot_anchor_x(alpha: Image.Image) -> int:
    bounds = alpha.getbbox()
    if bounds is None:
        return alpha.width // 2
    bottom = bounds[3] - 1
    band_top = max(bounds[1], bottom - 4)
    points = [
        x
        for y in range(band_top, bottom + 1)
        for x in range(alpha.width)
        if alpha.getpixel((x, y)) != 0
    ]
    return round(median(points)) if points else alpha.width // 2


def conform_subject(
    subject: Image.Image,
    scale: float,
    palette: Image.Image,
    *,
    mirror: bool = False,
    anchor: str = "feet",
    shift: tuple[int, int] = (0, 0),
) -> Image.Image:
    if mirror:
        subject = subject.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

    scale = min(
        scale,
        WORKING_VISIBLE_MAX[0] / subject.width,
        WORKING_VISIBLE_MAX[1] / subject.height,
    )
    size = (
        max(1, round(subject.width * scale)),
        max(1, round(subject.height * scale)),
    )
    reduced = subject.resize(size, Image.Resampling.BOX)
    reduced = lock_palette(reduced, palette)
    bounds = reduced.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("conformed subject became empty")

    canvas = Image.new("RGBA", WORKING_SIZE, (0, 0, 0, 0))
    if anchor == "center":
        left = (WORKING_SIZE[0] - reduced.width) // 2
    elif anchor == "feet":
        left = WORKING_SIZE[0] // 2 - _foot_anchor_x(reduced.getchannel("A"))
    else:
        raise ValueError(f"unsupported anchor: {anchor}")
    left += shift[0]
    left = max(2, min(left, WORKING_SIZE[0] - reduced.width - 2))
    top = WORKING_GROUND_ROW - reduced.height + 1 + shift[1]
    top = max(0, min(top, WORKING_SIZE[1] - reduced.height - 2))
    canvas.alpha_composite(reduced, (left, top))
    return canvas.resize(CANVAS_SIZE, Image.Resampling.NEAREST)


def conform_action_subject(
    subject: Image.Image,
    palette: Image.Image,
    *,
    mirror: bool = False,
) -> Image.Image:
    """Fit a readable key pose without shortening the actor.

    Wide punches previously hit the width cap through uniform scaling and made
    the actor lose almost a quarter of their height.  Action poses instead lock
    to the canonical 58px authoring height and, only when necessary, compress
    horizontal reach into the 41px silhouette budget.
    """
    if mirror:
        subject = subject.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    vertical_scale = TARGET_UPRIGHT_HEIGHT / subject.height
    width = min(
        WORKING_VISIBLE_MAX[0],
        max(1, round(subject.width * vertical_scale)),
    )
    reduced = subject.resize(
        (width, TARGET_UPRIGHT_HEIGHT),
        Image.Resampling.BOX,
    )
    reduced = lock_palette(reduced, palette)
    canvas = Image.new("RGBA", WORKING_SIZE, (0, 0, 0, 0))
    left = (WORKING_SIZE[0] - reduced.width) // 2
    top = WORKING_GROUND_ROW - reduced.height + 1
    canvas.alpha_composite(reduced, (left, top))
    return canvas.resize(CANVAS_SIZE, Image.Resampling.NEAREST)


def anchored_transform(
    image: Image.Image,
    *,
    head_shift_x: float = 0.0,
    vertical_scale: float = 1.0,
) -> Image.Image:
    """Lean/breathe one approved pose while keeping the boots on the pivot.

    The transform runs on the 48x64 authoring grid.  Horizontal shear is zero
    at the ground row and reaches ``head_shift_x`` at the silhouette top;
    vertical scale is also anchored at the same row.  It therefore creates a
    readable anticipation/recoil without changing anatomy or sliding feet.
    """
    working = image.resize(WORKING_SIZE, Image.Resampling.NEAREST)
    bounds = working.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("cannot transform an empty actor frame")

    if vertical_scale <= 0:
        raise ValueError("vertical_scale must be positive")
    if abs(vertical_scale - 1.0) > 0.0001:
        inverse_scale = 1.0 / vertical_scale
        working = working.transform(
            WORKING_SIZE,
            Image.Transform.AFFINE,
            (
                1.0,
                0.0,
                0.0,
                0.0,
                inverse_scale,
                WORKING_GROUND_ROW * (1.0 - inverse_scale),
            ),
            resample=Image.Resampling.NEAREST,
        )

    if abs(head_shift_x) > 0.0001:
        height = max(1, WORKING_GROUND_ROW - bounds[1])
        slope = head_shift_x / height
        working = working.transform(
            WORKING_SIZE,
            Image.Transform.AFFINE,
            (
                1.0,
                slope,
                -slope * WORKING_GROUND_ROW,
                0.0,
                1.0,
                0.0,
            ),
            resample=Image.Resampling.NEAREST,
        )
    return working.resize(CANVAS_SIZE, Image.Resampling.NEAREST)


def rotate_and_ground(
    image: Image.Image,
    angle: float,
    *,
    preserve_volume: bool = False,
) -> Image.Image:
    """Create a collapse pose without subpixel interpolation.

    Late death poses must fit a 41px authoring width even though the upright
    body is 58px tall.  Uniform fitting makes the corpse visibly shrink, so the
    late pose folds horizontal reach while retaining at least 29px of body
    depth — the read is bent knees/torso, not a miniature actor.
    """
    working = image.resize(WORKING_SIZE, Image.Resampling.NEAREST)
    source_bounds = working.getchannel("A").getbbox()
    if source_bounds is None:
        raise ValueError("cannot rotate an empty actor frame")
    subject = working.crop(source_bounds)
    rotated = subject.rotate(
        angle,
        resample=Image.Resampling.NEAREST,
        expand=True,
    )
    bounds = rotated.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("collapse rotation produced an empty frame")
    subject = rotated.crop(bounds)
    if preserve_volume:
        target_size = (
            min(WORKING_VISIBLE_MAX[0], subject.width),
            min(WORKING_VISIBLE_MAX[1], max(29, subject.height)),
        )
        if target_size != subject.size:
            subject = subject.resize(target_size, Image.Resampling.NEAREST)
    else:
        scale = min(
            1.0,
            WORKING_VISIBLE_MAX[0] / subject.width,
            WORKING_VISIBLE_MAX[1] / subject.height,
        )
        if scale < 1.0:
            subject = subject.resize(
                (
                    max(1, round(subject.width * scale)),
                    max(1, round(subject.height * scale)),
                ),
                Image.Resampling.NEAREST,
            )
    canvas = Image.new("RGBA", WORKING_SIZE, (0, 0, 0, 0))
    left = (WORKING_SIZE[0] - subject.width) // 2
    top = WORKING_GROUND_ROW - subject.height + 1
    canvas.alpha_composite(subject, (left, top))
    return canvas.resize(CANVAS_SIZE, Image.Resampling.NEAREST)


def _save_frame(tag: str, index: int, image: Image.Image) -> Path:
    path = FRAME_ROOT / f"{tag}-{index:02d}.png"
    image.save(path, optimize=True)
    return path


def _direction_row(direction: str) -> int:
    return DIRECTIONS.index(direction)


def build_frames() -> list[FrameSpec]:
    _, palette = actor_palette()
    action = ReferenceGrid(ACTION_REFERENCE, rows=4, columns=4)
    walk = ReferenceGrid(WALK_REFERENCE, rows=4, columns=3)
    frames: list[FrameSpec] = []

    # Directional canonical poses are the single source of anatomy for idle,
    # attack and hit.  East/west are an exact mirror pair; south remains the
    # approved grounded Frame_0 so enabling the animator cannot swap identity.
    canonical: dict[str, Image.Image] = {
        "south": Image.open(BASE_SOUTH).convert("RGBA"),
    }
    north_scale = TARGET_UPRIGHT_HEIGHT / action.subject(0, 0).height
    canonical["north"] = conform_subject(
        action.subject(0, 0),
        north_scale,
        palette,
        anchor="center",
    )
    east_scale = TARGET_UPRIGHT_HEIGHT / action.subject(1, 0).height
    canonical["east"] = conform_subject(
        action.subject(1, 0),
        east_scale,
        palette,
        anchor="center",
    )
    canonical["west"] = canonical["east"].transpose(
        Image.Transpose.FLIP_LEFT_RIGHT
    )

    for direction in DIRECTIONS:
        idle = canonical[direction]
        idle_frames = (
            idle,
            anchored_transform(idle, vertical_scale=0.985),
            idle,
            anchored_transform(idle, vertical_scale=0.97),
        )
        for index, image in enumerate(idle_frames):
            frames.append(FrameSpec(f"idle-{direction}", index, image, 140))

    for direction in DIRECTIONS:
        row = 1 if direction == "west" else _direction_row(direction)
        mirror = direction == "west"
        scale = walk.upright_scale(row, 1)
        for index in range(3):
            image = conform_subject(
                walk.subject(row, index),
                scale,
                palette,
                mirror=mirror,
            )
            frames.append(FrameSpec(f"walk-{direction}", index, image, 100))

    attack_windup = {
        "north": (-2.0, 0.95),
        "east": (-3.0, 0.96),
        "south": (2.0, 0.95),
        "west": (3.0, 0.96),
    }
    attack_release = {
        direction: conform_action_subject(
            action.subject(_direction_row(direction), 2),
            palette,
        )
        for direction in ("north", "east", "south")
    }
    attack_release["west"] = attack_release["east"].transpose(
        Image.Transpose.FLIP_LEFT_RIGHT
    )
    for direction in DIRECTIONS:
        idle = canonical[direction]
        windup = attack_windup[direction]
        attack_frames = (
            anchored_transform(
                idle,
                head_shift_x=windup[0],
                vertical_scale=windup[1],
            ),
            attack_release[direction],
            idle,
        )
        for index, image in enumerate(attack_frames):
            frames.append(FrameSpec(f"attack-{direction}", index, image, 95))

    hit_strong = {
        direction: conform_action_subject(
            action.subject(_direction_row(direction), 3),
            palette,
        )
        for direction in ("north", "east", "south")
    }
    hit_strong["west"] = hit_strong["east"].transpose(
        Image.Transpose.FLIP_LEFT_RIGHT
    )
    for direction in DIRECTIONS:
        idle = canonical[direction]
        strong = hit_strong[direction]
        hit_frames = (
            strong,
            anchored_transform(strong, vertical_scale=0.94),
            idle,
        )
        for index, image in enumerate(hit_frames):
            frames.append(FrameSpec(f"hit-{direction}", index, image, 70))

    fall_frames: dict[str, tuple[Image.Image, Image.Image]] = {}
    death_frames_by_direction: dict[str, tuple[Image.Image, ...]] = {}
    fall_sign = {"north": -1.0, "east": -1.0, "south": 1.0, "west": 1.0}
    for direction in ("north", "east", "south"):
        sign = fall_sign[direction]
        start = rotate_and_ground(canonical[direction], 18.0 * sign)
        collapse = rotate_and_ground(canonical[direction], 48.0 * sign)
        fall_frames[direction] = (start, collapse)
        death_frames_by_direction[direction] = (
            anchored_transform(
                canonical[direction],
                head_shift_x=3.0 * sign,
                vertical_scale=0.95,
            ),
            start,
            collapse,
            rotate_and_ground(
                canonical[direction],
                70.0 * sign,
                preserve_volume=True,
            ),
            rotate_and_ground(
                canonical[direction],
                88.0 * sign,
                preserve_volume=True,
            ),
        )

    fall_frames["west"] = tuple(
        image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        for image in fall_frames["east"]
    )
    death_frames_by_direction["west"] = tuple(
        image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        for image in death_frames_by_direction["east"]
    )

    for direction in DIRECTIONS:
        start, collapse = fall_frames[direction]
        frames.append(FrameSpec(f"fall-{direction}", 0, start, 90))
        frames.append(FrameSpec(f"fall-{direction}", 1, collapse, 120))

    for direction in DIRECTIONS:
        death_frames = death_frames_by_direction[direction]
        durations = (70, 80, 90, 110, 180)
        for index, (image, duration) in enumerate(
            zip(death_frames, durations, strict=True)
        ):
            frames.append(FrameSpec(f"death-{direction}", index, image, duration))

    return frames


def assert_frame_contract(image: Image.Image, allowed: set[tuple[int, int, int]]) -> None:
    if image.size != CANVAS_SIZE:
        raise AssertionError(f"frame canvas mismatch: {image.size}")
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise AssertionError("empty frame")
    width = bounds[2] - bounds[0]
    height = bounds[3] - bounds[1]
    if width > VISIBLE_MAX[0] or height > VISIBLE_MAX[1]:
        raise AssertionError(f"visible bounds exceed contract: {bounds}")
    if bounds[3] > GROUND_ROW + 1:
        raise AssertionError(f"frame crosses ground baseline: {bounds}")
    if set(alpha.get_flattened_data()) - {0, 255}:
        raise AssertionError("frame alpha is not hard")

    pixels = rgba.load()
    for y in range(0, CANVAS_SIZE[1], 2):
        for x in range(0, CANVAS_SIZE[0], 2):
            block = {
                pixels[x, y],
                pixels[x + 1, y],
                pixels[x, y + 1],
                pixels[x + 1, y + 1],
            }
            if len(block) != 1:
                raise AssertionError(f"frame breaks 2x2 clusters at {(x, y)}")
    used = {
        (red, green, blue)
        for red, green, blue, alpha_value in rgba.get_flattened_data()
        if alpha_value != 0
    }
    illegal = used - allowed
    if illegal:
        raise AssertionError(f"frame contains off-palette colors: {sorted(illegal)}")


def build_manifest_payload(frames: list[FrameSpec], *, save_frames: bool) -> dict:
    grouped: dict[str, list[FrameSpec]] = {}
    order: list[str] = []
    for frame in frames:
        if frame.tag not in grouped:
            grouped[frame.tag] = []
            order.append(frame.tag)
        grouped[frame.tag].append(frame)

    clips = []
    for tag in order:
        clip_frames = []
        for frame in grouped[tag]:
            source = (
                _save_frame(tag, frame.index, frame.image)
                if save_frames
                else FRAME_ROOT / f"{tag}-{frame.index:02d}.png"
            )
            clip_frames.append(
                {"source": str(source), "duration_ms": frame.duration_ms}
            )
        clips.append(
            {
                "tag": tag,
                "loop": tag.startswith("idle-") or tag.startswith("walk-"),
                "frames": clip_frames,
            }
        )

    return {
        "schema_version": 1,
        "canvas": list(CANVAS_SIZE),
        "palette": str(PALETTE),
        "output": str(DRAFT_ASEPRITE),
        "leading_frames": [
            {"source": str(BASE_SOUTH), "duration_ms": 180}
        ],
        "clips": clips,
    }


def write_manifest(frames: list[FrameSpec]) -> None:
    payload = build_manifest_payload(frames, save_frames=True)
    MANIFEST.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def write_preview(frames: list[FrameSpec]) -> None:
    by_tag = {(frame.tag, frame.index): frame for frame in frames}
    columns = tuple(
        (state, index)
        for state, count in (
            ("idle", 4),
            ("walk", 3),
            ("attack", 3),
            ("hit", 3),
            ("fall", 2),
            ("death", 5),
        )
        for index in range(count)
    )
    label_height = 14
    cell_width = CANVAS_SIZE[0] * PREVIEW_SCALE
    cell_height = CANVAS_SIZE[1] * PREVIEW_SCALE + label_height
    preview = Image.new(
        "RGB",
        (cell_width * len(columns), cell_height * len(DIRECTIONS)),
        (5, 7, 12),
    )
    draw = ImageDraw.Draw(preview)
    for row, direction in enumerate(DIRECTIONS):
        for column, (state, index) in enumerate(columns):
            frame = by_tag[(f"{state}-{direction}", index)]
            enlarged = frame.image.resize(
                (CANVAS_SIZE[0] * PREVIEW_SCALE, CANVAS_SIZE[1] * PREVIEW_SCALE),
                Image.Resampling.NEAREST,
            )
            x = column * cell_width
            y = row * cell_height
            preview.paste(enlarged, (x, y), enlarged)
            draw.text(
                (x + 4, y + CANVAS_SIZE[1] * PREVIEW_SCALE),
                f"{state}-{direction} {index}",
                fill=(223, 231, 242),
            )
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview.save(PREVIEW, optimize=True)


def main() -> None:
    FRAME_ROOT.mkdir(parents=True, exist_ok=True)
    frames = build_frames()
    colors, _ = actor_palette()
    allowed = set(colors)
    for frame in frames:
        assert_frame_contract(frame.image, allowed)

    tags = {frame.tag for frame in frames}
    expected = {f"{state}-{direction}" for state in (
        "idle", "walk", "attack", "hit", "fall", "death"
    ) for direction in DIRECTIONS}
    if tags != expected:
        raise AssertionError(
            f"directional tag set mismatch: missing={sorted(expected - tags)}, "
            f"extra={sorted(tags - expected)}"
        )

    write_manifest(frames)
    write_preview(frames)
    print(f"wrote {len(frames)} directional frames: {FRAME_ROOT}")
    print(f"wrote 24-tag animation manifest: {MANIFEST}")
    print(f"wrote conform preview: {PREVIEW}")


if __name__ == "__main__":
    main()

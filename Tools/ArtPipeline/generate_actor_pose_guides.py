#!/usr/bin/env python3
"""Generate deterministic OpenPose maps used by Project-C actor workflows."""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = (
    ROOT
    / "docs/art-direction/comfyui/guides/actor-slinger-openpose.png"
)
STYLE_SOURCE = (
    ROOT
    / "docs/art-direction/comfyui/guides/actor-slinger-style-source.png"
)
GOBLIN_SOURCE = ROOT / "Assets/_Project/Art/Runtime/actor-goblin.png"

# OpenPose BODY_18 order.
JOINTS = {
    0: (260, 82),   # nose
    1: (250, 130),  # neck
    2: (205, 145),  # right shoulder
    3: (170, 92),   # right elbow
    4: (215, 38),   # right wrist — raised above head
    5: (295, 145),  # left shoulder
    6: (335, 190),  # left elbow
    7: (360, 235),  # left wrist
    8: (225, 270),  # right hip
    9: (205, 365),  # right knee
    10: (195, 470), # right ankle
    11: (275, 270), # left hip
    12: (305, 365), # left knee
    13: (330, 470), # left ankle
    14: (250, 75),  # right eye
    15: (270, 75),  # left eye
    16: (238, 81),  # right ear
    17: (282, 81),  # left ear
}

LIMBS = [
    (1, 2), (1, 5), (2, 3), (3, 4), (5, 6), (6, 7),
    (1, 8), (8, 9), (9, 10), (1, 11), (11, 12), (12, 13),
    (1, 0), (0, 14), (14, 16), (0, 15), (15, 17),
]

COLORS = [
    (255, 0, 0), (255, 85, 0), (255, 170, 0), (255, 255, 0),
    (170, 255, 0), (85, 255, 0), (0, 255, 0), (0, 255, 85),
    (0, 255, 170), (0, 255, 255), (0, 170, 255), (0, 85, 255),
    (0, 0, 255), (85, 0, 255), (170, 0, 255), (255, 0, 255),
    (255, 0, 170),
]


def main() -> None:
    image = Image.new("RGB", (512, 512), (0, 0, 0))
    draw = ImageDraw.Draw(image)
    for (start, end), color in zip(LIMBS, COLORS, strict=True):
        draw.line((JOINTS[start], JOINTS[end]), fill=color, width=10)
    for index, point in JOINTS.items():
        color = COLORS[min(index, len(COLORS) - 1)]
        x, y = point
        draw.ellipse((x - 7, y - 7, x + 7, y + 7), fill=color)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT)
    print(OUTPUT)

    goblin = Image.open(GOBLIN_SOURCE).convert("RGBA")
    goblin = goblin.resize(
        (goblin.width * 3, goblin.height * 3),
        Image.Resampling.NEAREST,
    )
    style = Image.new("RGBA", (512, 512), (255, 0, 255, 255))
    style.alpha_composite(
        goblin,
        ((style.width - goblin.width) // 2, style.height - goblin.height - 32),
    )
    style.convert("RGB").save(STYLE_SOURCE)
    print(STYLE_SOURCE)


if __name__ == "__main__":
    main()

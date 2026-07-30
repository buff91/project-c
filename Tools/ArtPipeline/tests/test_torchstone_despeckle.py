from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from torchstone_palette import despeckle


RED = (200, 40, 40, 255)
BLUE = (40, 60, 200, 255)
CLEAR = (0, 0, 0, 0)


class DespeckleTests(unittest.TestCase):
    def test_isolated_pixel_merges_into_neighbor_majority(self) -> None:
        image = Image.new("RGBA", (5, 5), RED)
        image.putpixel((2, 2), BLUE)

        result = despeckle(image)

        self.assertEqual(RED, result.getpixel((2, 2)))
        self.assertEqual({RED}, set(result.get_flattened_data()))

    def test_two_by_two_cluster_is_preserved(self) -> None:
        image = Image.new("RGBA", (6, 6), RED)
        cluster = ((2, 2), (3, 2), (2, 3), (3, 3))
        for x, y in cluster:
            image.putpixel((x, y), BLUE)

        result = despeckle(image)

        for x, y in cluster:
            self.assertEqual(BLUE, result.getpixel((x, y)))

    def test_transparent_pixels_are_untouched(self) -> None:
        image = Image.new("RGBA", (5, 5), CLEAR)
        image.putpixel((2, 2), RED)

        result = despeckle(image)

        # 고립 스펙클이라도 이웃에 불투명 후보가 없으면 색을 바꿀 수 없고,
        # 알파는 어떤 경우에도 바뀌지 않는다(스프라이트 실루엣 보존).
        self.assertEqual(RED, result.getpixel((2, 2)))
        self.assertEqual(
            list(image.getchannel("A").get_flattened_data()),
            list(result.getchannel("A").get_flattened_data()),
        )

    def test_edge_speckle_merges_but_alpha_stays(self) -> None:
        image = Image.new("RGBA", (5, 5), CLEAR)
        for x, y in ((1, 1), (2, 1), (1, 2), (2, 2)):
            image.putpixel((x, y), RED)
        image.putpixel((3, 3), BLUE)

        result = despeckle(image)

        # (3,3)의 불투명 이웃은 (2,2) RED 하나뿐 — 다수결로 RED에 병합되고
        # 2×2 RED 블록과 알파 채널은 그대로다.
        self.assertEqual(RED, result.getpixel((3, 3)))
        for x, y in ((1, 1), (2, 1), (1, 2), (2, 2)):
            self.assertEqual(RED, result.getpixel((x, y)))
        self.assertEqual(
            list(image.getchannel("A").get_flattened_data()),
            list(result.getchannel("A").get_flattened_data()),
        )


if __name__ == "__main__":
    unittest.main()

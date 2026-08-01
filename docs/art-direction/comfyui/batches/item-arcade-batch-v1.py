#!/usr/bin/env python3
"""배치 ④ — 아이템 11종을 item-static-v1 레인으로 일괄 재발주한다.

reskin 표 §4 정체성(subjects/item-*.yaml)을 method 프롬프트에 합성해 ComfyUI 로컬
서버로 직렬 생성한다. 후보는 subject 당 3장(sequential-random-base 시드), 결과와
시드는 매니페스트 JSON에 남긴다. 실행:

    python3 docs/art-direction/comfyui/batches/item-arcade-batch-v1.py

정체성을 고쳐 일부만 재발주할 때는 대상과 리비전을 준다(기존 후보를 덮지 않도록
출력 폴더·매니페스트가 리비전으로 갈린다):

    python3 .../item-arcade-batch-v1.py --revision v2 --subjects item-bomb,item-coin-pouch
"""

import argparse
import json
import random
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / "Tools/ArtPipeline"))
from comfy_batch import DEFAULT_URL, execute_prompt  # noqa: E402

COMFY = ROOT / "docs/art-direction/comfyui"
OUTPUT = COMFY / "output"
CANDIDATES_PER_ITEM = 3
SUBJECT_IDS = (
    "item-bomb", "item-frost-bomb", "item-oil-flask", "item-throwing-knife",
    "item-recall-scroll", "item-coin-pouch", "item-gemstone", "item-relic",
    "item-herb", "item-blast-powder", "item-frost-shard",
)


def join_prompt(*parts: str) -> str:
    return ", ".join(part.strip().strip(",") for part in parts if part.strip())


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--revision", default="v1",
                        help="출력 폴더·매니페스트 접미사 (기본 v1)")
    parser.add_argument("--subjects", default=",".join(SUBJECT_IDS),
                        help="쉼표로 구분한 subject id (기본: 11종 전체)")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    subject_ids = [s.strip() for s in args.subjects.split(",") if s.strip()]
    revision = args.revision
    method = yaml.safe_load((COMFY / "methods/item-static-v1.yaml").read_text())
    bindings = method["pipeline"]["bindings"]
    generation = method["generation"]
    base = json.loads((COMFY / "item-static.api.json").read_text())

    # 같은 리비전을 일부 subject만 다시 돌리는 일이 잦다 — 덮어쓰면 앞선 채택본의
    # 시드 기록이 사라지므로 기존 매니페스트에 이어 붙인다.
    manifest_path = OUTPUT / f"item-arcade-batch-{revision}-manifest.json"
    manifest = (json.loads(manifest_path.read_text())
                if manifest_path.exists() else [])
    for subject_id in subject_ids:
        subject = yaml.safe_load(
            (COMFY / f"subjects/{subject_id}.yaml").read_text()
        )
        positive = join_prompt(
            method["prompt"]["prefix"],
            subject["prompt"]["positive"],
            method["prompt"]["suffix"],
        )
        negative = join_prompt(
            method["prompt"]["negative"],
            subject["prompt"].get("negative", ""),
        )
        seed_base = random.SystemRandom().randrange(1, 1_000_000)
        out_dir = OUTPUT / f"{subject_id}-arcade-{revision}"
        for index in range(CANDIDATES_PER_ITEM):
            prompt = json.loads(json.dumps(base))
            assignments = {
                "positive": positive,
                "negative": negative,
                "seed": seed_base + index,
                "width": generation["width"],
                "height": generation["height"],
                "steps": generation["steps"],
                "cfg": generation["cfg"],
                "denoise": generation["denoise"],
                "sampler": generation["sampler"],
                "scheduler": generation["scheduler"],
                "checkpoint": method["pipeline"]["checkpoint"],
            }
            for key, value in assignments.items():
                node_id, input_name = bindings[key].split(".", 1)
                prompt[node_id]["inputs"][input_name] = value
            prompt_id, written = execute_prompt(
                DEFAULT_URL, prompt, out_dir, timeout=3600.0
            )
            entry = {
                "subject": subject_id,
                "seed": seed_base + index,
                "prompt_id": prompt_id,
                "files": [str(p.relative_to(ROOT)) for p in written],
            }
            manifest.append(entry)
            print(json.dumps(entry, ensure_ascii=False), flush=True)
            manifest_path.write_text(
                json.dumps(manifest, ensure_ascii=False, indent=2)
            )
    print(f"done: {len(manifest)} generations recorded in {manifest_path.name}")


if __name__ == "__main__":
    main()

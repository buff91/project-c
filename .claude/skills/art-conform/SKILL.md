---
name: art-conform
description: 아트 시안을 게임에 들어가는 스프라이트로 마감하는 절차 — 팔레트 잠금, 시트 슬라이스, Unity 임포트 규격, 검증. 사용자가 스프라이트/타일/아이콘을 새로 넣거나 갱신할 때, "아트 반영해줘", "시트 처리해줘", "팔레트 맞춰줘"라고 할 때 사용한다.
---

# 아트 conform (시안 → 게임 에셋)

원리는 **비협상**이다: AI/시안은 실루엣·재료·명암까지만 맡고,
그리드 스냅·피벗·팔레트 잠금·애니메이션은 **결정론적 마감**이 한다.

상세 SSOT:
- 규격: `docs/art-direction/asset-spec-sheet.md`
- 개념 워크플로: `docs/art-direction/ai-to-aseprite-workflow.md`
- 통제형 생성: `docs/art-direction/comfyui-to-aseprite-pipeline.md`
- 방향/레퍼런스: `docs/art-direction/project-c-postapoc-art-direction-v1.md`
- 팔레트: `Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl` (18색)

## 0. 전제 — 어느 슬롯인가

| 대상 | 경로 |
|---|---|
| 최종 아트 SSOT | `Assets/_Project/Art/Source/Aseprite/*.aseprite` |
| 원본 없는 슬롯의 폴백 | `Assets/_Project/Art/Runtime/*.png` |

`Art/Runtime` PNG는 **폴백이다.** 최종본으로 직접 손보지 않는다 — `.aseprite` 원본이 SSOT다.

정적(환경 타일·소품·아이템·UI 아이콘 베이스)은 생성 파이프라인으로 대량 처리해도 된다.
**애니 액터**(idle/walk/attack/hit/fall/death)는 idle 베이스 포즈까지만 생성이고,
프레임 간 발 고정·실루엣 일관은 Aseprite 손 애니가 맡는다.

## 1. 마감 스크립트 실행

```bash
python3 -m pip install --user pillow      # PIL 없으면 (환경에 없을 수 있다)
python3 Tools/ArtPipeline/process_postapoc_environment_v2.py
```

- 프로세서들은 **인자를 받지 않는다** — 소스/출력 경로가 스크립트에 하드코딩돼 있다.
  대상 시트를 바꾸려면 스크립트 상단의 `SOURCE` 상수를 확인한다.
- 리포 어디서 실행해도 된다(`torchstone_palette` 임포트는 스크립트 위치 기준).
- 용도별 프로세서: `process_postapoc_{environment,actors,props,support}_v2.py`,
  `process_items_lock_v1.py`, `process_ui_icons_v1.py`, `build_ui_nineslice_v1.py`.

**모든 프로세서는 `torchstone_palette.lock_to_palette`를 거쳐야 한다.**
시트마다 독립 quantize를 하면 팔레트가 드리프트해서 에셋이 서로 안 붙는다 —
이게 과거에 실제로 깨진 지점이다. 새 프로세서를 쓸 때도 이 잠금을 반드시 통과시킨다.

## 2. Unity 임포트 규격

`.aseprite`/`.ase`는 `com.unity.2d.aseprite 5.0.3`이 직접 임포트하고,
`ProjectCAsepritePipeline`이 규격을 강제한다:

- Filter **Point** · PPU **64** · Compression **None** · Mip Maps **Off**
- Pivot: Canvas Pivot (캐릭터는 발 중앙)
- 정식 파일명의 첫 프레임은 `ProjectCEnvironmentCatalog`에 **자동 연결**된다

즉 파일명이 규격을 벗어나면 카탈로그 연결이 조용히 빠진다. 파일명을 먼저 확인한다.

## 3. 검증

1. `./Tools/CoreTests/run-core-tests.sh` — `DungeonSurfaceFor`의 깊이별 석재색 고정 등
   팔레트 관련 규칙 테스트가 여기서 걸린다.
2. Unity MCP가 있으면 `read_console`로 임포트 에러 확인 + **PC 가로 Game View** 캡처.
   (현재 우선순위상 모바일 세로·다해상도 회귀 검증은 생략한다 — `CLAUDE.md` 참조.)
3. 톤 확인: 청흑 void 바탕 + 횃불에 데워진 웜 그레이/토프 석재, 물리 광원은 토치 골드,
   마법/출구는 틸. 깊이 변주의 통로는 **셋뿐**이다 — 밴드 스프라이트 슬롯, 구조(캐치워크 길이),
   광원 밀도. 석재색 자체를 깊이별로 바꾸지 않는다.

## 4. 마감

`feature-done` 스킬을 따른다. 아트 변경은 보통 `docs/STATUS.md`와
필요 시 `docs/art-direction/`의 리스킨 표를 갱신한다.

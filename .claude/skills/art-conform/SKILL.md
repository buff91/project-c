---
name: art-conform
description: 아트 시안을 게임에 들어가는 스프라이트로 마감하는 절차 — 발주(ComfyUI) 진입점, 팔레트 잠금, 시트 슬라이스, Unity 임포트 규격, 검증. 사용자가 스프라이트/타일/아이콘을 새로 넣거나 갱신할 때, "아트 반영해줘", "시트 처리해줘", "팔레트 맞춰줘"라고 할 때 사용한다.
---

# 아트 conform (시안 → 게임 에셋)

원리는 **비협상**이다: AI/시안은 실루엣·재료·명암까지만 맡고,
그리드 스냅·피벗·팔레트 잠금·애니메이션은 **결정론적 마감**이 한다.

상세 SSOT:
- 규격: `docs/art-direction/asset-spec-sheet.md`
- 개념 워크플로: `docs/art-direction/ai-to-aseprite-workflow.md`
- 통제형 생성: `docs/art-direction/comfyui-to-aseprite-pipeline.md`
- **발주·리뷰 운영 명령표**: `docs/art-direction/ART_REVIEW_AUTOMATION.md`
- 방향/레퍼런스: `docs/art-direction/project-c-postapoc-art-direction-v1.md`
- 세계관(생성 프롬프트) SSOT: `docs/art-direction/comfyui/worlds/arcade-tower-v1.yaml`
- 팔레트: `Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl` (v2 램프 구조)

## 0. 전제 — 어느 슬롯인가

| 대상 | 경로 |
|---|---|
| 최종 아트 SSOT | `Assets/_Project/Art/Source/Aseprite/*.aseprite` |
| 원본 없는 슬롯의 폴백 | `Assets/_Project/Art/Runtime/*.png` |

`Art/Runtime` PNG는 **폴백이다.** 최종본으로 직접 손보지 않는다 — `.aseprite` 원본이 SSOT다.

정적(환경 타일·소품·아이템·UI 아이콘 베이스)은 생성 파이프라인으로 대량 처리해도 된다.
**애니 액터**(idle/walk/attack/hit/fall/death)는 idle 베이스 포즈까지만 생성이고,
프레임 간 발 고정·실루엣 일관은 Aseprite 손 애니가 맡는다.

> 카탈로그 슬롯명에 `hospital*`이 남아 있는 건 **구판 이름의 잔재**다(아케이드 재발주 예정).
> 이름을 보고 폐병원 테마로 되돌리지 않는다 — 톤 판정은 §4를 따른다.

## 1. 시안이 아직 없으면 — 발주 (ComfyUI)

마감할 시안이 없으면 발주부터다. 명령표 전체는 `ART_REVIEW_AUTOMATION.md` §3이 소유한다.
**순서만 여기서 지킨다**(§3-d):

1. ComfyUI Desktop과 백그라운드 서비스가 켜져 있는지 확인.
2. `python3 Tools/ArtPipeline/art_runner.py recipes` — recipe ID 확인.
3. **가장 싼 단일 샷 먼저**: `art_runner.py submit <recipe-id> --shot <shot-id> --count 1`.
   액터·이펙트는 전체 세트를 바로 만들지 않는다(비용이 크다).
4. 판정: `python3 Tools/ArtPipeline/art_runner.py review` (로컬 브라우저 뷰어) 또는 Slack 카드.
   후보 ID를 옮겨 적는 대신 별칭(`latest`·`^2`)을 쓴다.
5. 설정이 읽힐 때만 전체 세트: `art_runner.py submit <recipe-id> --count 1`.
6. `prepare <candidate-id>`(Aseprite 소스 세트) → `animation` 순으로 인계.
7. `approve <candidate-id>` — **승인만으로 Unity 파일은 바뀌지 않는다.** 반영은 §2 이후다.

워크플로 JSON을 고쳤다면 캔버스(`*.workflow.json`)와 실행본(`*.api.json`)을 **항상 함께** 남긴다.
훅이 어긋남을 막지만, 캔버스에서 고치고 Export를 잊은 경우는 못 잡는다 — 그때는 전체 스윕:
`python3 Tools/ArtPipeline/comfy_batch.py validate`

## 2. 마감 스크립트 실행

```bash
python3 -m pip install --user pillow      # PIL 없으면 (환경에 없을 수 있다)
python3 Tools/ArtPipeline/process_postapoc_environment_v2.py
```

- 프로세서들은 **인자를 받지 않는다** — 소스/출력 경로가 스크립트에 하드코딩돼 있다.
  대상 시트를 바꾸려면 스크립트 상단의 `SOURCE` 상수를 확인한다.
- 리포 어디서 실행해도 된다(`torchstone_palette` 임포트는 스크립트 위치 기준).
- 프로세서는 계속 늘어난다 — 목록은 `ls Tools/ArtPipeline/process_*.py`로 확인한다.
  용도별 대표: `process_postapoc_{environment,actors,props,support}_v2.py`,
  `process_items_lock_v1.py`, `process_ui_icons_v1.py`, `process_ui_backdrops_v1.py`,
  `build_ui_nineslice_v1.py`, `process_band_floors_v1.py`(깊이 밴드 바닥).

**모든 프로세서는 `torchstone_palette.lock_to_palette`를 거쳐야 한다.**
시트마다 독립 quantize를 하면 팔레트가 드리프트해서 에셋이 서로 안 붙는다 —
이게 과거에 실제로 깨진 지점이다. 새 프로세서를 쓸 때도 이 잠금을 반드시 통과시킨다.

## 3. Unity 임포트 규격

`.aseprite`/`.ase`는 `com.unity.2d.aseprite 5.0.3`이 직접 임포트하고,
`ProjectCAsepritePipeline`이 규격을 강제한다:

- Filter **Point** · PPU **128** (`ui-*`만 64) · Compression **None** · Mip Maps **Off**
- Pivot: Canvas Pivot (캐릭터는 발 중앙)
- 정식 파일명의 첫 프레임은 `ProjectCEnvironmentCatalog`에 **자동 연결**된다

즉 파일명이 규격을 벗어나면 카탈로그 연결이 조용히 빠진다. 파일명을 먼저 확인한다.

## 4. 검증

1. `./Tools/CoreTests/run-core-tests.sh` — `DungeonSurfaceFor`의 깊이별 색 고정 등
   팔레트 관련 규칙 테스트가 여기서 걸린다.
2. `Tools/ArtPipeline/**`(후처리 스크립트 포함)를 고쳤으면 **파이썬 회귀도 돌린다** —
   실행 형태는 `test` 스킬 2단계. 여기엔 세션 종료 훅이 없어서 안 돌리면 아무도 안 잡는다.
3. Unity MCP가 있으면 `read_console`로 임포트 에러 확인 + **PC 가로 Game View** 캡처.
   (현재 우선순위상 모바일 세로·다해상도 회귀 검증은 생략한다 — `CLAUDE.md` 참조.)
   Unity 참조가 없는 보관본 캡처는 즉시 `docs/captures/`로 옮긴다.
4. **톤 확인** — 테마는 **아포칼립스 + 사이버펑크(폐 아케이드 복합타워)**다.
   구판 판타지 어휘(횃불·석재·마법 포탈)로 판정하지 않는다:

   - **바탕**: 청흑(blue-black charcoal) void — 이 구조만 판타지 시절에서 그대로 계승됐다.
   - **중간톤**: 저채도 콘크리트 회색 · 벽돌 적갈 · 녹 주황. 풍화 3종은
     ① 녹 ② 균열(벽·바닥) ③ 자연 잠식(담쟁이·잡초).
   - **광원**: 웜(형광등·비상등·방폭 표시등·드럼통 불) + **쿨 네온(시안·마젠타) 이원**.
     단 **국소 웅덩이로만** — 시안 스트립 하나, 마젠타 사인 글로우 하나 수준이다.
     바닥이 네온에 잠기거나 채도가 올라가면 **기각**이다
     (`arcade-tower-v1.yaml`의 negative가 `neon-flooded floors`·`saturated floor`를 명시한다).
   - **신호색**: 청록(틸)은 **"열림·통과·냉각"** 기능 신호다(Hole·해금된 출구·게이트·빙결).
     v0.3.4에서 초자연이 삭제됐으므로 "이상 현상" 표시가 아니다. 주황은 위험/화상.
   - **명도**: "판독 가능한 어둠" — 어둡되 형태가 읽혀야 한다.
   - **깊이 변주**: 바닥 기본색을 층별로 바꾸지 않는다(`DungeonSurfaceFor`가 테스트로 고정).
     층대 구분은 **밴드 바닥 스프라이트(오염·마모·장비 밀도) · 구조 · 광원 밀도**로만 낸다.

## 5. 마감

`feature-done` 스킬을 따른다. 아트 변경은 보통 `docs/STATUS.md`와
필요 시 `docs/ART_PIPELINE.md` · `docs/art-direction/`의 리스킨 표를 갱신한다.

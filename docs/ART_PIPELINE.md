# Project-C 아트 제작 파이프라인

> 목표: AI 시안을 그대로 게임에 넣는 것이 아니라, 시안에서 방향을 고른 뒤 Aseprite에서 일관된 모듈형 픽셀아트로 재제작한다.

## 1. 고정 제작 규격

| 항목 | 프로토타입 기준 |
|---|---:|
| 아이소 바닥 타일 | 128×64 px (2:1) — 2026-07 상향, 구 64×32의 정확히 ×2 |
| Pixels Per Unit | 128 (`ui-*`만 64) |
| elevation 1단 | 화면상 32 px |
| 캐릭터 작업 캔버스 | 96×128 px |
| 캐릭터 Pivot | 발 중앙 |
| PC 메뉴 배경 런타임 | 960×540 px (`ui-*`, PPU 64) |
| 기본 방향 수 | 4방향 |
| 애니메이션 | 8~12 FPS |
| Texture Filter | Point |
| Compression | None |
| Mip Maps | Off |

`IsoGrid(tileWidth=1, tileHeight=0.5, elevationStep=0.25)`가 이 비율을 사용한다. 아트 규격을 바꾸려면 에셋을 그리기 전에 세 값을 함께 검증한다.

## 2. AI를 사용하는 범위

AI 이미지 생성에 맡긴다:

- 전체 분위기와 팔레트 탐색
- 방 구성과 소품 실루엣 아이디어
- 벽돌·금속·나무의 재질 밀도 비교
- 캐릭터 비율과 장비 형태 후보

Aseprite에서 직접 만든다:

- 반복 가능한 128×64 바닥 타일
- 좌·우 벽면과 안/밖 모서리
- 방향별 계단과 문
- 캐릭터 방향 및 애니메이션 프레임
- 정확한 Pivot, 타일 경계, 충돌 기준이 필요한 모든 에셋

AI 결과를 그대로 잘라 쓰면 투영각, 광원, 픽셀 크기와 타일 경계가 서로 달라진다. AI 결과는 원화로만 사용한다.

### 단계 게이트 (중간 수정 시)

에셋 묶음은 `소스 승인 → conform/팔레트 잠금 → Unity 슬롯 연결 → PC Game View 캡처` 네 단계로
진행한다. 앞 단계가 수정되면 뒤 단계 결과는 전부 **stale**로 보고 다시 만든다. 한 단계의 출력과
캡처가 합격하기 전에는 다음 에셋군을 실행하지 않는다.

1. **소스 승인** — 레퍼런스 역할, 프롬프트, 캔버스, 색 면적 제한을 고정한다.
2. **conform 승인** — 최종 해상도·알파·공용 `.gpl`·피벗을 검사한다.
3. **Unity 연결 승인** — `IsoVisualCatalog`/USS 참조와 Point·PPU·Mip·압축을 확인한다.
4. **화면 승인** — 현재 우선순위인 PC 가로 Game View에서 실제 크기·톤·UI 충돌을 캡처한다.

환경→UI→액터처럼 서로 영향을 주는 묶음도 같은 순서로 직렬 승인한다. 소스가 바뀌었는데 이전
Runtime PNG나 캡처를 그대로 통과 기록으로 쓰지 않는다.

## 3. 첫 번째 실제 제작 묶음

한 번에 전체 던전을 만들지 않는다. 아래 묶음으로 8×8 테스트 룸을 먼저 완성한다.

1. 바닥 3종: 기본, 금 간 바닥, 이끼
2. 벽: 좌면, 우면, 바깥 모서리, 안쪽 모서리
3. 높이: 16 px 단차 블록, 4방향 계단
4. 특수 타일: Hole, WeakFloor, StairsUp(청색 표식), StairsDown(주황색 표식)
5. 소품: DoorClosed, DoorOpen, Crate, ExplosiveBarrel, Torch
6. 캐릭터: Player와 Goblin의 Idle 4방향

이 묶음이 Unity에서 정상적으로 정렬되고 반복된 뒤 Walk/Attack/Hit/Fall 애니메이션을 추가한다.

### 현재 프로젝트의 실제 아트 발주 순서

현재 구현 기준의 우선순위는 `docs/ROADMAP.md`와
`docs/art-direction/project-c-art-improvement-plan-v2.md`가 소유한다.

1. **메인 원정자**: `actor-knight`의 96×128 컨셉과 기본 스프라이트를 먼저 확정한다.
   직업 실루엣을 고정하지 않고 장비가 정체성을 지도록 중립적인 생존자 체형·복장만 잠근다.
2. **적 액터**: `actor-slinger`, `actor-grave-warden`의 96×128 기본 스프라이트를 확정한다.
   지금은 절차 폴백 크기가 기존 자산 액터보다 작아 플레이 화면에서 바로 결손으로 보인다.
3. **환경 판독 자산**: mid/deep/boss 바닥 기본·raised 6종, `env-hole`,
   `env-weak-floor`, `env-ladder`.
4. **환경 루프**: `prop-campfire`, `prop-portal`, 좌·우 상승 벽 횃불의 `idle`
   키프레임을 만들고 Aseprite에서 4~8프레임 루프로 마감한다.
5. **아케이드 소품/낙하 연출** (v0.3.3 개정 — 구 병원 소품 발주는 집행 전 폐기,
   `docs/ROADMAP.md` 「아트」와 동기): 자판기, 죽은 네온 간판, 홀로 패널, 셔터 내린 점포,
   엘리베이터 통로, 구멍 깊이 표현.
6. **아이템 12종**: 64×64 포스트아포 리스킨. fallback vertical slice는 완료했으며
   `item-sources-v3/`의 항목별 단일 소스를 `process_items_v3.py`로 마감한다.
   이후 Aseprite 원본으로 승격할 때도 파일명·피벗 계약은 유지한다.
7. **액터 애니메이션**: 기본 스프라이트가 승인된 액터부터
   `idle/walk/attack/hit/fall/death`를 Aseprite 원본으로 마감.
8. **전투 VFX 6슬롯**: physical/heavy/fire/frost impact와 burn/freeze status.
   승인 키프레임을 `burst` 또는 `idle-loop`로 마감한다.

각 항목을 생성 큐에 넣기 전에 최소한 다음 여섯 가지가 있어야 한다.

- 대상 계약: Unity 슬롯, 캔버스, 피벗, 게임에서 읽혀야 할 실루엣
- 화풍 계약: 픽셀 클러스터, 에지, 명도 단계, 최종 해상도에서의 렌더링 문법
- 세계관 계약: 폐 아케이드 복합타워(네온 아포칼립스)의 재료 어휘, Torchstone 팔레트, 신호색 한 점

### UI 스프라이트의 웜 → 쿨 리맵 (v1.8, 2026-08-01)

- **소스 시트는 여전히 웜이다.** `docs/art-direction/project-c-torchstone-ui-icons-source-v1.png` 는
  판타지 시절 색으로 생성됐고 **재발주하지 않았다**. 팔레트만 바꿔서는 아이콘이 안 따라오므로
  `process_ui_icons_v1.py` 의 `cool_shift()` 가 마감 단계에서 옮긴다.
- **일괄 시프트 금지 — 휴 대역 + 채도로 게이트한다.** 휴 15°~70° **그리고** 채도 ≤ 0.38 만 옮기고
  명도는 보존한다(셰이딩 구조 유지). 골드(0.67)·토치(0.75)·틸·HP·러스트는 대역 밖이라 안 걸린다.
  근거: `process_hospital_dressing_v1.py` 주석 — 과거 전역 `WARM_GAIN` 시프트가 모든 패널을 웜
  브라운으로 밀어 네온 시설을 일반 폐허로 만들었다.
- **축소 전에 리맵한다.** 축소 뒤에는 웜/쿨이 섞인 중간 픽셀이 생겨 게이트를 통과하지 못하고 얼룩으로 남는다.
- 계약은 `tests/test_process_ui_icons.py` 의 `UiIconCoolShiftTests` 가 고정한다(신호색 5종 불변·
  투명 보존·명도 순서). **소스 시트를 재발주하는 날 이 패스를 지울지 먼저 판단한다** — 소스가 이미
  쿨이면 게이트가 걸릴 픽셀이 없어 무해하지만, 남겨 두면 "왜 두 번 옮기나"가 된다.
- 생성 입력: style, world, subject, method, 캐릭터/대상 정의, 이번 생성 내용,
  positive/negative
- 재현값: checkpoint/LoRA 버전, seed, Steps, CFG, denoise, sampler/scheduler
- 다음 단계 입력: 승인 후보 ID 또는 기존 소스시트/OpenPose 가이드

## 4. Aseprite 작업 순서

1. 128×64 다이아몬드를 템플릿 레이어로 만든다.
2. 모든 에셋에 같은 좌상단 광원을 사용한다.
3. `Art/Source/Aseprite/project-c-torchstone.gpl` 안에서만 색을 고른다. 재료 램프 단위로
   묶여 있으니 **한 재료는 한 램프 안에서** 칠한다 — 램프를 넘나들면 재료가 뭉갠다.
4. 타일 경계 픽셀을 복사해 이웃 타일과 맞춘다.
5. 캐릭터 발 중앙을 동일한 좌표에 둔다.
6. `.aseprite` 원본을 Unity에 직접 임포트한다. PNG export는 외부 전달/검수용일 때만 만든다.
7. Unity 테스트 룸에서 100%와 현재 검증 타깃인 PC 가로 크기로 확인한다.

### 던전 공통 톤 SSOT

정확한 색 인덱스의 SSOT는
`Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl`이다.
`ProjectCEnvironmentCatalog.asset`의 `dungeon*` 필드는 던전 런타임에서 사용할
Torchstone 역할색을 직렬화한 미러다. 색 역할과 사용 규칙은 `GDD.md` §6을 따른다.

- 바닥·벽·문틀은 같은 웜 그레이·토프 석재군을 공유한다.
- 좌상단 한 방향 광원과 재료별 **4~6단 램프**를 유지한다.
- 단차는 별도 갈색 바닥이 아니라 같은 석재의 명도 상승과 16px 측면으로 표현한다.
- 횃불의 호박색과 마법의 청록색은 국소 신호색이며 바닥 원본 전체에 굽지 않는다.
- 깊이별 변주는 공통 팔레트를 대체하지 않고 제한된 보정과 소품 밀도로만 만든다.

#### 팔레트 v2 — 램프 구조 (2026-07)

v1은 재료당 2~3단에 램프 안 휴 시프트가 6~8°였다. 밝기 슬라이더나 마찬가지여서
형태(베벨·균열·원통)를 그릴 수 없었고, 소스 시트가 아무리 좋아도 잠금 단계에서 뭉갰다.
v2는 **광원이 앰버라는 사실을 램프 구조로 굽는다** — 그림자는 청보라로 떨어지고
하이라이트는 앰버로 튼다(휴 스팬 25~40°), 채도는 중간에서 최고·하이라이트에서 하강.

색을 더하거나 고칠 때 지킬 규칙과 그 근거는 **`.gpl` 파일 헤더 주석이 소유한다** —
데이터 옆에 두려고 거기 뒀다. 여기 복제하지 않는다. 요지만: 암부는 저채도로 공유,
같은 색 계열을 두 램프로 쪼개지 않음, `sig-*`는 램프가 아니라 포인트.

`ui-*` 토큰도 같은 파일에 있고 스프라이트 잠금에 함께 들어간다 — UI 스프라이트
생성기(`build_ui_nineslice_v1` 등)가 같은 `lock_to_palette`를 쓰기 때문이다. 분리하면 그쪽이 깨진다.

> 잠금은 아직 `dither=NONE`이다. 넓은 평면에 오더드 디더를 허용하는 것은 다음 작업이며,
> 실루엣 경계·신호색은 그때도 디더에서 제외해야 한다.

권장 폴더:

```text
Assets/_Project/Art/
  Source/Aseprite/      # 런타임 아트 SSOT, Unity가 직접 임포트
  Sprites/Tiles/        # 외부 전달/검수용 export만
  Sprites/Characters/   # 외부 전달/검수용 export만
  Sprites/Props/        # 외부 전달/검수용 export만
  Palettes/
  Runtime/              # 게임에 연결된 128 PPU 검증 세트(재생성 가능, ui-*만 64)
```

## 5. AI 시안용 프롬프트 템플릿

```text
Create a clean modular isometric pixel-art asset reference board for a post-apocalyptic
cyberpunk derelict arcade-tower roguelike (dead neon signs, concrete, emergency lights).
Use an exact-looking 2:1 diamond tile projection and one consistent upper-left light source.
Show isolated floor, left/right wall faces, corner, stairs, hole, weak floor, door, crate,
barrel, player and goblin on a flat dark background with generous spacing.
Use chunky deliberate pixel clusters and a limited 16-24 color palette.
No text, labels, UI, watermark, smooth painting, 3D rendering or overlapping assets.
This is an art-direction reference for rebuilding precise 128×64 assets in Aseprite,
not a ready-to-slice sprite sheet.
```

한 번에 여러 애니메이션 프레임을 요구하지 않는다. 캐릭터 디자인을 확정한 뒤 `Idle 4방향`, `Walk 4방향`처럼 묶음을 나눠 생성하고 Aseprite에서 다시 맞춘다.

## 6. Unity 교체 원칙

- 논리 데이터는 `TileKind`만 보유한다.
- `TileKind → Sprite` 매핑은 `IsoVisualCatalog` ScriptableObject가 담당한다.
- 바닥, 벽면, 오브젝트와 캐릭터는 같은 타일에서도 별도 렌더러로 둔다.
- 캐릭터와 높은 소품은 발 중앙 Pivot으로 정렬한다.
- 시안 교체 시 Core 로직과 `IsoGrid.SortingOrder`는 수정하지 않는다.
- 문은 닫힘/열림 두 스프라이트를 `IsoVisualCatalog`에 별도 연결한다. 상태 판정은 Core의 `TileKind`가 담당한다.
- 닫힌 문은 바닥 데칼이 아니라 발 중앙 기준의 세워진 문짝·문틀 실루엣이어야 한다. 열린 문은 중앙 통로를 비우고 문짝을 측면에 표시한다.
- 아이소 문은 정면 직사각형 한 장으로 만들지 않는다. 통로 축에 수직인 `↗ / ↖` 두 사선 평면의 닫힘·열림 세트를 제작하고, 시점 회전 시 대응 방향을 선택한다.
- 플레이어 고정 표식은 청록 발판+머리 위 화살표, 선택/공격 대상 표식은 주황 링으로 색 역할을 분리한다.
- 수직 연결은 선만 그리지 않는다. 시작/도착 발판, 방향 화살표, 두 층을 잇는 점선 리본을 한 세트로 제작한다. Hole은 청록, 안전 계단은 주황을 사용한다.
- 인접층 미리보기용 바닥/벽은 현재층보다 명도와 알파를 낮추되 최소 5×5 타일 조각이 읽혀야 한다. 전체 층을 축소 표시하지 않는다.

Unity Project 창에서 `Create > Project-C > Isometric Visual Catalog`를 선택하고 완성한 스프라이트를 슬롯에 넣은 뒤, `IsoPrototypeDemo.visualCatalog`에 연결한다. 일부 슬롯만 연결해도 나머지는 임시 아트로 표시되므로 한 에셋씩 교체하며 비교할 수 있다.

### Unity 2D Aseprite Importer 직접 연결

프로젝트에는 `com.unity.2d.aseprite 5.0.3`이 설치되어 있다.
`Assets/_Project/Art/Source/Aseprite` 아래에 정식 이름의 `.aseprite`/`.ase` 원본을 저장하면
`ProjectCAsepritePipeline`이 다음을 자동 처리한다.

- Animated Sprite + Merge Frame
- Point filter, PPU 128, Full Rect, Mip Map Off, Clamp
- Standalone/iOS/Android 무압축
- 프레임 사이에서 흔들리지 않는 Canvas 기준 Pivot
- Aseprite Tag 기반 AnimationClip 생성
- 첫 프레임 Sprite를 공용 `ProjectCEnvironmentCatalog` 슬롯에 자동 연결

원본 파일명은 Catalog 계약이다. 예를 들어 `actor-knight.aseprite`는 `knight`,
`env-floor.aseprite`는 `floor`, `prop-campfire.aseprite`는 `hubCampfire` 슬롯을 교체한다.
전체 목록과 Tag 규칙은 `Assets/_Project/Art/Source/Aseprite/README.md`를 따른다.
메뉴 `Project-C > Art > Aseprite > Validate Sources`로 중복 이름, 미지원 이름,
임포트 규격과 Sprite 생성 여부를 검사한다. `.aseprite`가 없는 슬롯은 현재 PNG를 유지한다.

정적 PNG를 원본까지 한 번에 승격할 때는 슬롯의 최종 캔버스가 확정된 뒤 아래 진입점을 쓴다.

```bash
python3 Tools/ArtPipeline/art_asset.py publish INPUT.png \
  --slot env-floor --width 128 --height 64 --fit strict --anchor center
```

로컬 Aseprite 앱은 `/Applications`, `~/Applications`, Steam 설치 경로 순으로 자동 탐색하며,
별도 위치만 `PROJECTC_ASEPRITE_BIN`으로 지정한다. 명령이
`Art/Source/Aseprite/<slot>.aseprite`를 만들면 열린 Unity가 이를 감지해 위 임포터와 카탈로그
동기화를 실행한다. 자동 새로고침이 멈춘 경우에만
`Project-C > Art > Aseprite > Reimport and Sync Catalog`를 한 번 실행한다.

이 명령은 **정적 한 프레임 전용**이다. 기존 `actor-*`에 `--force`로 쓰면 태그와 모든 애니메이션
프레임이 사라진다. 액터는 `art_runner.py animation`으로 초안을 만든 뒤 Aseprite에서 발 기준선과
프레임 정체성을 마감하고 정식 슬롯으로 승격한다.

## 7. 현재 레퍼런스

- 현재 통합 화면 방향:
  `docs/art-direction/project-c-integrated-postapoc-gameplay-target-v2.png`
- 메인 메뉴 배경 소스/프롬프트:
  `docs/art-direction/project-c-main-menu-backdrop-source-v1.png` /
  `project-c-main-menu-backdrop-source-v1.prompt.md`
- 임시 통합 패스 Unity 캡처:
  `docs/captures/integrated-art-pass-main-menu.png`,
  `integrated-art-pass-hub.png`, `integrated-art-pass-dungeon.png`
- 전체 게임 화면 방향: `docs/art-direction/project-c-artstyle-concept-v1.png`
- 허브 웜 다크 판타지 디오라마 타깃:
  `docs/art-direction/project-c-warm-diorama-hub-target-v1.png`
- 허브 1차 런타임 적용 캡처:
  `docs/art-direction/project-c-warm-diorama-hub-runtime-v1.png`
- 현재 캐릭터·허브·아이템 통합 제작 보드: `docs/art-direction/project-c-runtime-asset-board-v2.png`
- 첫 모듈형 아트 키트 방향: `docs/art-direction/project-c-starter-art-kit-v1.png`
- 회전 가능한 던전의 최종 밀도 타깃: `docs/art-direction/project-c-rotatable-dungeon-target-v1.png`
- 현재 Unity 절차식 구현 캡처: `docs/art-direction/iso-prototype-room.png`

현재 런타임 임시 아트에도 석재 타일 변형, 16 px 단차 측면, 연속 후면 벽, 횃불, 상·하행 계단 색, Hole 강조, 기사·고블린 실루엣이 적용되어 있다. 목적은 최종 그림을 절차 생성하는 것이 아니라 다음을 먼저 검증하는 것이다.

- 어떤 시점에서도 벽·단차·캐릭터 정렬이 깨지지 않는가
- Hole과 계단이 작은 모바일 화면에서도 즉시 구분되는가
- 실제 Aseprite 스프라이트를 `IsoVisualCatalog`에 한 칸씩 넣어도 Core 로직을 수정하지 않아도 되는가

AI 타깃 이미지는 한 장의 완성 장면이므로 직접 슬라이스하지 않는다. `project-c-starter-art-kit-v1.png`와 `project-c-runtime-asset-board-v2.png`를 형태 참고로 삼아 바닥/벽/코너/계단/캐릭터를 각각 최종 픽셀 해상도에 다시 그린 뒤 Catalog 슬롯으로 교체한다.

현재 `Assets/_Project/Art/Runtime` 세트는 실제 Aseprite 원본이 없는 슬롯의 폴백이다.
정적 세트는 `process_postapoc_environment_v2.py`, `process_hospital_dressing_v1.py`,
`process_postapoc_actors_v2.py`,
`process_postapoc_support_v2.py`, `process_postapoc_props_v2.py`, `process_items_v3.py`로,
UI는 `process_ui_icons_v1.py`, `build_ui_nineslice_v1.py`,
`generate_ui_action_hex_v1.py`, `process_ui_backdrops_v1.py`로 재생성한다. 모든 프로세서는
`torchstone_palette`의 공용 잠금 함수를 거친다.
`ProjectCArtImporter`가 Point filter, PPU 128(`ui-*`는 64), 무압축, Mip Map Off를 강제하고,
피벗은 `ProjectCArtPivots`(Aseprite 파이프라인과 공유하는 단일 SSOT)에서 가져온다.
`Art/Environment/` PNG도 같은 임포터가 강제한다.

드레싱(바닥 3 + 하단 벽 3) 슬롯의 구판 소스와 생성 프롬프트는
`docs/art-direction/project-c-hospital-dressing-source-v1.{png,prompt.md}`가 소유한다
(구 폐병원 보드 — 셀 배치 계약의 원본이라 스타일 트랜스퍼 입력으로 유지하며, 아케이드
재발주는 `environment-neon-dressing-v1` 레시피가 소유한다 — M5 잔여 범위는 `docs/ROADMAP.md`).
3×2 소스의 바닥 3셀은 공용 `env-floor` 위에 합성해 완전한 128×64 타일로 만들고,
벽 3셀은 64×112 좌/우 방향형으로 마감한다. 바닥 소스를 통째로 교체하면 생성 여백이
투명한 void 구멍으로 보이므로 합성 단계를 제거하지 않는다.
아이템 v3의 생성 방식·항목별 최종 프롬프트 세트는
`docs/art-direction/item-sources-v3/README.md`가 소유한다. 한 소스에 여러 아이템을 넣지 않고,
각 소스를 독립 생성한 뒤 크로마 제거→축소→하드 알파→공용 팔레트→피벗 여백 순서로 conform한다.
최종 아트의 SSOT는 `Art/Source/Aseprite`이고, PNG를 수정해 최종본처럼 유지하지 않는다.
허브와 전투 씬은 반드시 같은 `ProjectCEnvironmentCatalog`를 참조하며,
씬별 독자 카탈로그나 캐릭터 색조 복제는 만들지 않는다.

허브 웜 디오라마 패스는 `IsoPrototypeDemo`의 허브 모드에서만 적용한다. 2:1 다이아 투영과
공용 액터/소품 카탈로그는 유지하고, 바닥·후면 벽의 Torchstone 석재 팔레트와 모닥불/포탈 타일
광원 오버레이를 분기한다. 전면 경계 타일은 16px 석재 측면을 그려 디오라마 두께를 만들고,
후면 벽은 횃불/배너/연금술 선반/무기 장식 모듈을 조합한다. 광원 오버레이는 정렬 오프셋 `-1`,
바닥은 `-2`, 액터/소품은 `+1`을 사용하며 시점 회전 시 모두 같은 `GridPos`로 재투영한다.
이 분리를 없애고 던전 바닥에 따뜻한 광원색을 굽지 말 것. FOV·기름·물·화상·빙결 색 판독이 우선이다.

## 8. 로컬 자동화 진입점

- 생성: ComfyUI Desktop의 `127.0.0.1:8188` REST API +
  `python3 Tools/ArtPipeline/comfy_batch.py`
- 정적 마감: `Tools/ArtPipeline/aseprite_conform.sh INPUT OUTPUT WIDTH HEIGHT strict`
- 상세 실행법: `docs/art-direction/comfyui/README.md`
- 레시피·수동 배치·Slack 리뷰·Codex Spark 반영:
  `docs/art-direction/ART_REVIEW_AUTOMATION.md`

ComfyUI 워크플로는 Desktop에서 **API 형식으로 export**한 JSON만 자동 실행한다. Civitai 모델은
체크포인트·LoRA·ControlNet·IPAdapter의 base model 세대를 맞추고, 채택 시 모델 버전·URL·
라이선스를 prompt 문서에 기록한다. Aseprite conform은 캔버스·알파·공용 팔레트를 강제한 뒤
정식 파일명의 `.aseprite`로 저장한다. 멀티샷 레시피는 ComfyUI 키포즈/이펙트 슬롯 생성과
샷별 Aseprite 소스 인계, 검수용 FrameTag·duration·GIF 초안까지 자동화한다.
`quality_gates.color_area_limits`가 있는 레시피는 conform 결과의 불투명 픽셀만 세어
신호색 그룹별 최대 면적을 넘긴 후보를 리뷰 전에 거절한다. 팔레트 안에 색이 존재하는 것과
캐릭터 전체가 그 색으로 물드는 것은 다른 문제이므로, 액터의 국소 teal·warning 역할을 이 단계에서 고정한다.
보간 프레임·발 기준선·실루엣·최종 타이밍은 사람이 Aseprite에서 마감한다.

### 애니메이션/이펙트 운영 규칙

- ComfyUI는 키프레임 후보 생성에 집중하고, 프레임 연출/루프는 Aseprite에서 마감한다.
- Slack `/art new`에서는 **화풍(렌더링 문법) → 세계관(테마·재료) → 제작 대상(어떤
  캐릭터/Unity 슬롯/에셋) → 제작 방법 → 캐릭터/대상 정의 → 이번 생성 내용**을
  각각 나눠 입력한다.
  모든 job은 실제 positive/negative, checkpoint/LoRA, seed, Steps, CFG, denoise와 승인 소스
  후보 ID를 스냅샷한다. 원본 YAML이 아니라 `/art job <job-id>`가 실제 실행 기록이다.
- 캐릭터는 `컨셉 승인 → 기본 스프라이트 승인 → 액션 키프레임 → Aseprite`, VFX는
  `VFX 컨셉 승인 → img2img 키프레임 정제 → burst/idle-loop` 순서로 계보를 잇는다.
- **액터 비율·포즈 일관성(v0.3.3)**: 모든 액터 발주는 `actor-chibi-base-v1` 레시피 계보를
  탄다 — 치비 BODY_18 골격 4방향(`guides/openpose/actor-chibi-*`)이 관절을, 96×128 행 계약
  (crown 8/feet 122, `output.normalize_rows`)이 체감 비율을 고정하고, 리뷰 시트에 게임 스케일
  프리뷰가 자동 동봉된다. 상세 근거는
  `docs/art-direction/project-c-actor-appeal-restyle-v1.md` §2-b.
- 정적 환경은 `environment-concept-sdxl-v1 → 승인 →
  environment-static-refine-v1 → Aseprite/Unity 슬롯` 순서로 잇는다.
- 환경 루프는 `environment-loop-concept-sdxl-v1 → 승인 →
  environment-idle-keyframes-v1 → Aseprite idle 태그 → Unity` 순서로 잇는다.
- 캐릭터는 `idle` 중심 + `walk/attack/hit/fall/death` 보조 프레임을 모은 뒤 FrameTag를 붙인다.
- 이펙트는 `burst`/`idle-loop` 중심으로 먼저 4~8프레임 후보를 만들고, 반복/톤은 Aseprite에서 정제한다.
- 기본 슬롯(`allow_replace: false`)은 승인 전 교체 금지.
- 참조:
  - `docs/art-direction/animation-effect-workflow.md`
  - `docs/art-direction/comfyui/recipes/actor-slinger-animation-v5.yaml`
  - `docs/art-direction/comfyui/recipes/fx-impact-suite-v2.yaml`

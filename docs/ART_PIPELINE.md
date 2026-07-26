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

## 3. 첫 번째 실제 제작 묶음

한 번에 전체 던전을 만들지 않는다. 아래 묶음으로 8×8 테스트 룸을 먼저 완성한다.

1. 바닥 3종: 기본, 금 간 바닥, 이끼
2. 벽: 좌면, 우면, 바깥 모서리, 안쪽 모서리
3. 높이: 16 px 단차 블록, 4방향 계단
4. 특수 타일: Hole, WeakFloor, StairsUp(청색 표식), StairsDown(주황색 표식)
5. 소품: DoorClosed, DoorOpen, Crate, ExplosiveBarrel, Torch
6. 캐릭터: Player와 Goblin의 Idle 4방향

이 묶음이 Unity에서 정상적으로 정렬되고 반복된 뒤 Walk/Attack/Hit/Fall 애니메이션을 추가한다.

## 4. Aseprite 작업 순서

1. 128×64 다이아몬드를 템플릿 레이어로 만든다.
2. 모든 에셋에 같은 좌상단 광원을 사용한다.
3. `Art/Source/Aseprite/project-c-torchstone.gpl`의 18색 인덱스에서만 색을 고른다.
4. 타일 경계 픽셀을 복사해 이웃 타일과 맞춘다.
5. 캐릭터 발 중앙을 동일한 좌표에 둔다.
6. `.aseprite` 원본을 Unity에 직접 임포트한다. PNG export는 외부 전달/검수용일 때만 만든다.
7. Unity 테스트 룸에서 100%와 실제 모바일 크기로 확인한다.

### 던전 공통 톤 SSOT

정확한 색 인덱스의 SSOT는
`Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl`이다.
`ProjectCEnvironmentCatalog.asset`의 `dungeon*` 필드는 던전 런타임에서 사용할
Torchstone 역할색을 직렬화한 미러다. 색 역할과 사용 규칙은 `GDD.md` §6을 따른다.

- 바닥·벽·문틀은 같은 웜 그레이·토프 석재군을 공유한다.
- 좌상단 한 방향 광원과 `Outline → Shadow → Base → Light` 4단 명도 구조를 유지한다.
- 단차는 별도 갈색 바닥이 아니라 같은 석재의 명도 상승과 16px 측면으로 표현한다.
- 횃불의 호박색과 마법의 청록색은 국소 신호색이며 바닥 원본 전체에 굽지 않는다.
- 깊이별 변주는 공통 팔레트를 대체하지 않고 제한된 보정과 소품 밀도로만 만든다.

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
Create a clean modular isometric pixel-art asset reference board for a dark fantasy mobile roguelike.
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

## 7. 현재 레퍼런스

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
`Tools/ArtPipeline/generate_runtime_art_v2.py`로 결정론적으로 재생성하며,
`ProjectCArtImporter`가 Point filter, PPU 128(`ui-*`는 64), 무압축, Mip Map Off를 강제하고,
피벗은 `ProjectCArtPivots`(Aseprite 파이프라인과 공유하는 단일 SSOT)에서 가져온다.
`Art/Environment/` PNG도 같은 임포터가 강제한다.
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
- 레시피·Slack 리뷰·Codex Scheduled:
  `docs/art-direction/ART_REVIEW_AUTOMATION.md`

ComfyUI 워크플로는 Desktop에서 **API 형식으로 export**한 JSON만 자동 실행한다. Civitai 모델은
체크포인트·LoRA·ControlNet·IPAdapter의 base model 세대를 맞추고, 채택 시 모델 버전·URL·
라이선스를 prompt 문서에 기록한다. Aseprite conform은 캔버스·알파·공용 팔레트를 강제한 뒤
정식 파일명의 `.aseprite`로 저장한다. 멀티샷 레시피는 ComfyUI 키포즈/이펙트 슬롯 생성과
샷별 Aseprite 소스 인계, 검수용 FrameTag·duration·GIF 초안까지 자동화한다.
보간 프레임·발 기준선·실루엣·최종 타이밍은 사람이 Aseprite에서 마감한다.

### 애니메이션/이펙트 운영 규칙

- ComfyUI는 키프레임 후보 생성에 집중하고, 프레임 연출/루프는 Aseprite에서 마감한다.
- 캐릭터는 `idle` 중심 + `walk/attack/hit/fall/death` 보조 프레임을 모은 뒤 FrameTag를 붙인다.
- 이펙트는 `burst`/`idle-loop` 중심으로 먼저 4~8프레임 후보를 만들고, 반복/톤은 Aseprite에서 정제한다.
- 기본 슬롯(`allow_replace: false`)은 승인 전 교체 금지.
- 참조:
  - `docs/art-direction/animation-effect-workflow.md`
  - `docs/art-direction/comfyui/recipes/actor-slinger-animation-v5.yaml`
  - `docs/art-direction/comfyui/recipes/fx-impact-suite-v2.yaml`

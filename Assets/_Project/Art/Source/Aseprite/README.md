# Project-C Aseprite 원본

이 폴더의 `.aseprite`/`.ase` 파일은 Unity `2D Aseprite Importer`가 직접 읽는다.
저장한 변경을 Unity가 감지하면 `ProjectCAsepritePipeline`이 같은 임포트 사이클에서 규격을
적용하고, 임포트가 끝난 뒤 카탈로그 동기화를 예약한다. PNG export를 카탈로그에 다시 꽂지 않는다.

## 팔레트

기준 팔레트는 `project-c-torchstone.gpl` (= `UI/DesignSystem.uss` 토큰, 씬 실측).
새 원본은 이 팔레트를 로드해 **Indexed 모드**로 작업한다. 규격·워크플로 상세는
`docs/art-direction/asset-spec-sheet.md`, `docs/art-direction/ai-to-aseprite-workflow.md`.

## 사용 순서

1. 아래 정식 파일명으로 이 폴더에 Aseprite 원본을 저장한다.
2. Unity가 파일 변경을 감지하면 Point, PPU 128, Mip Map Off, 무압축, Canvas Pivot이 자동 적용된다.
   `env-floor*`/`env-wall-*`은 런타임 톤매핑을 위해 Read/Write도 자동으로 켜진다.
3. 임포트가 끝나면 지연 콜백이 카탈로그 동기화를 실행해 첫 프레임 Sprite를 공용
   `ProjectCEnvironmentCatalog`의 대응 슬롯에 자동 연결한다.
4. 여러 프레임과 Tag가 있으면 AnimationClip도 같은 `.aseprite` 에셋의 sub-asset으로 생성되고,
   액터/환경 애니메이션 세트가 같은 동기화에서 다시 구워진다.
5. 문제가 있으면 `Project-C > Art > Aseprite > Validate Sources`를 실행한다.

평소 저장에는 수동 재연결이 필요 없다. Unity의 자동 새로고침이 꺼졌거나 기존 원본에 새 임포트
규칙을 일괄 적용해야 할 때만 `Project-C > Art > Aseprite > Reimport and Sync Catalog`를 사용한다.
정식 원본을 삭제하거나 SourceRoot 밖으로 옮기면 자동 연결했던 슬롯만 같은 이름의 Environment/
Runtime PNG로 복구하고, 해당 PNG도 없으면 비운다. 다른 경로를 수동 참조한 슬롯은 보존한다.

## 핵심 파일명

- 배경: `env-dungeon-backdrop` (미탐색 구조 없는 전체 생성 영역)
- 타일: `env-floor`, `env-floor-raised`, `env-floor-lower`, `env-hole`, `env-weak-floor`
- 수직 이동: `env-stairs`, `env-ladder`, `env-stairs-up`, `env-stairs-down`
- 방향형: 기존 `env-*-rising-right/left`
- B2 시점 방향형: `env-floor-b2-{parking-stop,fallen-sign}-view-0..3`
- B2 2×2 연속 바닥: `env-floor-b2-macro-role-{0..3}-view-{0..3}`
- 액터: `actor-player`, `actor-knight`, `actor-ranger`, `actor-alchemist`,
  `actor-goblin`, `actor-skeleton`, `actor-slime`, `actor-slinger`,
  `actor-arc-drone`, `actor-grave-warden`, `actor-merchant`
- 허브/소품: `prop-campfire`, `prop-stash`, `prop-portal`, `prop-explosive-barrel`
- 아이템/마커: 기존 `item-*`, `marker-player`, `marker-target`. 두 마커는 각각 틸/앰버의 열린
  코너 틱이며 바닥 전체를 두르는 링으로 만들지 않는다.

파일명은 `IsoVisualCatalog` 슬롯 계약이다. 같은 이름의 원본을 두 폴더에 중복 저장하지 않는다.
원본이 아직 없는 슬롯은 기존 PNG/런타임 임시 아트를 그대로 사용한다.

## Aseprite 내부 규칙

- 레이어는 숨김 레이어를 제외하고 프레임마다 합쳐진다.
- 캐릭터 프레임은 동일한 Canvas에서 발 위치를 고정한다.
- 애니메이션 Tag는 `idle`, `walk`, `attack`, `hit`, `fall`, `death`를 사용한다.
- 반복하지 않을 Tag는 Aseprite Tag의 Repeat를 `1`로 지정한다.
- 레이어 UUID를 켜서 레이어 이름 변경 뒤에도 Unity Sprite 참조가 유지되게 한다.

## 현재 검증 스냅샷

- 미커밋 작업 트리(2026-08-02)에서 `Validate Sources`가
  **Aseprite 원본 64개 / 카탈로그 슬롯 64개**를 통과했다. 현재 집합에는 열린 코너 마커 2종과
  B2 원통형 연료 셀 `prop-explosive-barrel.aseprite`가 포함된다.
- B2 주차 범퍼와 쓰러진 안내판은 각각 `view-0..3` 네 방향 슬롯을 가지며, 네 슬롯이 모두
  있을 때만 현재 시점의 90도 회전 수로 하나를 고른다. 부분 승격이면 기존 무방향 슬롯을 네
  시점에 공통 사용하고, 그것도 없으면 같은 화면축 parity → 첫 존재 슬롯 순으로 안전 폴백한다.
  v2 원본은 단순 mirror 반복이 아니라 2×2 생성 보드의 실제 네 시점을 유지한다.
  `Validate Sources`는 불완전한 `view-0..3` 원본 세트를 경고한다.
- `env-floor-b2-cracked`는 방향 seam과 측면 두께가 없는 단일 평면 마모 슬롯이다. 네 시점이 같은
  Aseprite를 공유하며, 전용 슬롯이 없을 때만 구 전역 `hospitalFloorCracked`로 폴백한다.
- `env-floor-b2-macro-role-{0..3}-view-{0..3}`는 한 장의 top-down 2×2 재질을 네 시점으로 먼저
  투영한 뒤 네 물리 역할로 자른 완전 세트다. 16개 중 하나라도 빠지면 부분 매크로를 그리지 않고
  해당 네 셀을 모두 일반 바닥으로 폴백한다.

## B2 배경 프롭 v2 제작 계보

승인 방향판과 당시 실제 q0에서 만든 제작 원화는
`docs/art-direction/project-c-b2-prop-production-sheet-v2.{png,prompt.md}`다. 이 원화는 직접
slice하지 않는다. `process_b2_prop_quality_v4.py`가 최종 캔버스에서 기본/작업등/설비/단말/
서비스 벽 14종과 원통형 연료 셀을 다시 만들고, `process_b2_parking_dressing_v3.py`가 낮은
범퍼·안내판의 네 view를 만든다. `promote_b2_prop_quality_v2.sh`만 런타임 PNG와 이 폴더의
Aseprite를 함께 승격하는 정식 진입점이다.

현재 화면 승인본은 `docs/captures/b2-prop-quality-q{0,1,2,3}-live-v3.png`다. B2 배치에서 연료
셀만 blocking/interactable이고, 범퍼·안내판·벽 단말은 비기능 드레싱이다.

## 원정자 애니메이션 안전장치

현재 `actor-knight.aseprite`는 접지 품질을 먼저 잠근 `96×128` 승인 `Frame_0`을 태그 밖 첫
프레임으로 보존하고, 뒤에 4방향 6상태 태그 프레임 80개를 둔다. 정식 태그는
`idle/walk/attack/hit/fall/death × north/east/south/west` 24개이며 idle/walk만 반복한다.
모든 프레임은 하드 알파·24색 이하 역할 팔레트·2×2 클러스터·발 `y=123`을 지킨다.

접지 원본 계보는 `docs/art-direction/project-c-expeditioner-grounded-source-v1.{png,prompt.md}`가,
방향 레퍼런스·보정·프레임 수는
`docs/art-direction/reference/ref-expeditioner-directional-animation-v1.prompt.md`가 소유한다.
PC 화면 승인까지 끝나 `SurvivorAnimationApproved`는 `true`이고 플레이어에
`SpriteClipAnimator`를 붙인다. 적 애니메이션에는 이 게이트가 적용되지 않는다.

## 아케이드 적군 방향 애니메이션 안전장치

`actor-goblin`·`actor-skeleton`·`actor-slime`·`actor-slinger`·`actor-arc-drone`·
`actor-grave-warden`은 모두 `96×128` 정식 방향 원본이다. 각 파일은 승인 정적 런타임 PNG를
태그 밖 `Frame_0`과 `idle-south[0]`에 픽셀 일치로 보존하고, 뒤에 4방향 6상태 태그 프레임 80개를
둔다. 정식 태그는 `idle/walk/attack/hit/fall/death × north/east/south/west` 24개이며
idle/walk만 반복한다. east/west는 월드 손잡이가 아니라 **화면 방향 기준 exact mirror**다.

파일당 81프레임·24태그, 여섯 원본 합계는 486프레임·144태그다. identity 마감은
`Tools/ArtPipeline/process_arcade_occupation_actors_v1.py`, 방향 프레임과 manifest 생성은
`Tools/ArtPipeline/build_arcade_enemy_directional_v1.py`, Aseprite 조립은
`Tools/ArtPipeline/aseprite_build_animation.lua`가 소유한다. 전수 미리보기는
`docs/captures/arcade-enemy-directional-conform-preview-v1.png`다.

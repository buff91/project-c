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
- 액터: `actor-player`, `actor-knight`, `actor-ranger`, `actor-alchemist`,
  `actor-goblin`, `actor-skeleton`, `actor-slime`, `actor-slinger`,
  `actor-grave-warden`, `actor-merchant`
- 허브/소품: `prop-campfire`, `prop-stash`, `prop-portal`, `prop-explosive-barrel`
- 아이템/마커: 기존 `item-*`, `marker-player`, `marker-target`

파일명은 `IsoVisualCatalog` 슬롯 계약이다. 같은 이름의 원본을 두 폴더에 중복 저장하지 않는다.
원본이 아직 없는 슬롯은 기존 PNG/런타임 임시 아트를 그대로 사용한다.

## Aseprite 내부 규칙

- 레이어는 숨김 레이어를 제외하고 프레임마다 합쳐진다.
- 캐릭터 프레임은 동일한 Canvas에서 발 위치를 고정한다.
- 애니메이션 Tag는 `idle`, `walk`, `attack`, `hit`, `fall`, `death`를 사용한다.
- 반복하지 않을 Tag는 Aseprite Tag의 Repeat를 `1`로 지정한다.
- 레이어 UUID를 켜서 레이어 이름 변경 뒤에도 Unity Sprite 참조가 유지되게 한다.

## 현재 검증 스냅샷

- `36a49a3` + 미커밋 작업 트리(2026-08-01)에서 `Validate Sources`가
  **Aseprite 원본 28개**를 통과했다(`env-*` 26개 + 액터 2개).
- B2 주차 범퍼와 쓰러진 안내판은 각각 `view-0..3` 네 방향 슬롯을 가지며, 네 슬롯이 모두
  있을 때만 현재 시점의 90도 회전 수로 하나를 고른다. 부분 승격이면 기존 무방향 슬롯을 네
  시점에 공통 사용하고, 그것도 없으면 같은 화면축 parity → 첫 존재 슬롯 순으로 안전 폴백한다.
  `Validate Sources`는 불완전한 `view-0..3` 원본 세트를 경고한다.

## 원정자 애니메이션 안전장치

현재 `actor-knight.aseprite`의 멀티프레임 자동 조립 초안은 프레임 사이 해부·실루엣이 깨져 있다.
정식 방향별 Aseprite 타임라인이 화면 승인을 받기 전까지 `SurvivorAnimationApproved`는 `false`이며,
플레이어에는 `SpriteClipAnimator`를 붙이지 않고 카탈로그 첫 Sprite인 `Frame_0`만 표시한다. 적
애니메이션에는 이 게이트가 적용되지 않는다.

런타임에서 캐릭터 위에 덧그리던 `PlayerCyberAccent`는 제거했다. 흉부 트리아지 화면·비대칭 의료
리그 같은 정체성 악센트는 승인된 방향별 Aseprite 프레임 자체에 흡수한다.

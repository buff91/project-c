# Project-C Aseprite 원본

이 폴더의 `.aseprite`/`.ase` 파일은 Unity `2D Aseprite Importer`가 직접 읽는다.
PNG export를 카탈로그에 다시 꽂지 않는다.

## 팔레트

기준 팔레트는 `project-c-torchstone.gpl` (= `UI/DesignSystem.uss` 토큰, 씬 실측).
새 원본은 이 팔레트를 로드해 **Indexed 모드**로 작업한다. 규격·워크플로 상세는
`docs/art-direction/asset-spec-sheet.md`, `docs/art-direction/ai-to-aseprite-workflow.md`.

## 사용 순서

1. 아래 정식 파일명으로 Aseprite 원본을 저장한다.
2. Unity로 돌아오면 Point, PPU 64, Mip Map Off, 무압축, Canvas Pivot이 자동 적용된다.
3. 첫 프레임 Sprite가 공용 `ProjectCEnvironmentCatalog`의 대응 슬롯에 자동 연결된다.
4. 여러 프레임과 Tag가 있으면 AnimationClip도 같은 `.aseprite` 에셋의 sub-asset으로 생성된다.
5. 문제가 있으면 `Project-C > Art > Aseprite > Validate Sources`를 실행한다.

## 핵심 파일명

- 타일: `env-floor`, `env-floor-raised`, `env-floor-lower`, `env-hole`, `env-weak-floor`
- 수직 이동: `env-stairs`, `env-ladder`, `env-stairs-up`, `env-stairs-down`
- 방향형: 기존 `env-*-rising-right/left`
- 액터: `actor-player`, `actor-knight`, `actor-ranger`, `actor-alchemist`,
  `actor-goblin`, `actor-skeleton`, `actor-slime`, `actor-merchant`
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

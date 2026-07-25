# Project-C — Claude / Codex 작업 지침

> 이 파일은 Claude Code와 Codex(및 기타 AI 코딩 에이전트)가 공통으로 읽는 **진입점**이다. 얇게 유지한다.
> `AGENTS.md`는 이 파일을 가리키는 심볼릭 링크다.

## 참조 문서 (상세는 여기로)

- `GDD.md` — 게임 기획서. **설계 결정의 최종 출처(SSOT).** 코드/기획 판단 시 먼저 참조.
- `docs/ROADMAP.md` — 마일스톤 + 현재 진행/환경 상태.
- `docs/SYSTEMS.md` — 시스템 설계 요약(격자·FOV·낙하·상태이상·AI·크로스플랫폼).
- `docs/CODE_STRUCTURE.md` — 파일/파셜 레이아웃 지도 + SSOT 위치("어떤 코드가 어디에 사는가").
- `docs/UI_ARCHITECTURE.md` — UI 이원화(UI Toolkit/UGUI) 방침 + Claude 디자인 워크플로. **UI 판단 SSOT.**
- `Assets/_Project/M0_SETUP.md` — 씬 연결 가이드.

## 프로젝트 개요

- **장르**: 모바일 아이소메트릭 다층(elevation) 던전 크롤러 (Shattered Pixel Dungeon 계보 + 층간 낙하 전투)
- **엔진/언어**: Unity 2D (Isometric Tilemap) · C# · Unity 6000.5.4f1
- **플랫폼**: iOS / Android (PC 동시 지원 고려 → 입력 추상화 필수)
- **개발 인원**: 1인
- **한 판 목표**: 최심층 도달 (로그라이트 메타 프로그레션, **포스트 아포칼립스/이상 미궁 테마** —
  판타지에서 전환, 리스킨 진행 예정) — 상세 `docs/ROADMAP.md`, GDD §10 v0.3.

## 핵심 설계 기둥 (모든 결정의 기준)

1. **입체 공간(Verticality)** — 층 간 + 한 층 내 높이차(elevation). 낙하는 상호작용의 하나.
2. **상호작용 & 상태이상** — 화상/빙결/폭발 + 요소 반응(불+기름, 물+빙결 등).
3. **제한된 시야(FOV)** — Recursive Shadowcasting, 안개 3상태(Unknown/Explored/Visible).
4. **파밍 & 조합** — 자원 수집 + 조합 + 메타 프로그레션.

> 창발적 전술은 위 기둥이 충돌해 생기는 결과 — 목표이자 검증 기준.

## 아키텍처 규칙 (반드시 준수)

- **로직 ↔ 비주얼 분리**: 순수 C# 로직(`Scripts/Core`)은 UnityEngine 의존 최소화. 비주얼/씬 연동은 `Scripts/Gameplay`.
- **정렬(Sorting) 규칙은 `IsoGrid`에 집중**: floor(elevation) 우선 + (x+y) 정렬. 흩뿌리지 말 것.
- **타입 → 스프라이트/애니 매핑은 ScriptableObject**로. 데이터 중심.
- **입력 추상화**: 터치/마우스/키보드를 입력 레이어에서 액션 단위로 통일 (게임 로직에 플랫폼 분기 금지).
- **성능**: 모바일이 하한선. "보이는 층 ≠ 활성 층", 몬스터 활성 반경/컬링 전제.

## 현재 개발 우선순위 (임시 — PC 우선)

> **모바일은 당분간 보류한다.** 매 작업마다 모바일 세로·다해상도 검증과 이원 View 유지가 개발을
> 지나치게 느리게 해서, 한동안 **PC(데스크톱/포인터)를 유일한 개발·검증 타깃**으로 둔다.
> 이 절은 임시이며 모바일 재개 시 삭제한다.

- **검증**: 새 UI/씬 변경은 **PC 가로 Game View만** 캡처 검증한다. 모바일 세로·터치 렌더와
  다해상도(360×640 등) 회귀 검증은 생략한다. HUD 검증 기본값은 `PC`.
- **패리티 불필요**: 새 기능을 모바일 배치(`PrototypeHUD.Mobile.uxml`/`ui-touch`/56px 터치 타깃)에 맞춰
  이식하지 않는다. 기존 모바일 배치는 **깨지지 않을 정도로만** 둔다.
- **유지(삭제 금지)**: 입력 추상화(`IsoTapInput`)·`ResponsiveUiLayout`·이원 UXML·세이브 포맷 독립성은
  그대로 둔다 — 저렴하고 되돌리기 쉬우며, 재개 시 재작업을 막는 자산이다.
- 아키텍처 규칙의 "입력 추상화 / 게임 로직에 플랫폼 분기 금지"는 유효하다. 단 "모바일이 성능 하한선"과
  "모바일 세로·PC 가로 각각 검증"은 이 기간 동안 완화한다.

## 현재 구현 스냅샷 (2026-07-24)

> 아래는 빠른 인수인계용 요약이다. 세부 규칙과 완료 이력은
> `docs/SYSTEMS.md`, `docs/UI_ARCHITECTURE.md`, `docs/ROADMAP.md`를 따른다.

- **씬 흐름**: Build Settings `0 MainMenu → 1 Hub → 2 IsoPrototype`.
  - 새 게임: `MainMenu → Hub → IsoPrototype`.
  - 이후 프롤로그/세계관은 `MainMenuController.StartNewGame()`과 Hub 사이에 별도 씬으로 삽입한다.
  - 던전의 `로비로 가기`는 Hub, 게임오버의 `메뉴로`는 MainMenu로 이동한다.
- **UI/해상도**: 화면공간 UI는 UI Toolkit. `MainMenuHUD`, `HubHUD`,
  `PrototypeHUD.Mobile/Desktop`이 공용 `DisplaySettings`와 `ResponsiveUiLayout`을 사용한다.
  에디터/개발 빌드 설정창에서 `AUTO/MOBILE/PC`와 대표 해상도를 즉시 바꿀 수 있다. 모든 화면 루트는
  `ui-touch/ui-pointer` 입력 프로필을 받는다. 터치 표준 컨트롤은 논리 56px, 밀집 슬롯은 최소 44px,
  백팩 셀은 52px을 유지하고 짧은 화면에서는 본문을 스크롤한다.
- **다층 월드 입력**: `IsoTapInput.TilePicker`가 실제 렌더된 아이소 다이아몬드를
  `VisualPosition` 기준으로 고른다. 겹치면 **현재 활성 층 → Hole 미리보기 층 →
  같은 레이어의 렌더 정렬 순서**다. 전체 elevation 역산 방식으로 되돌리지 말 것.
- **수직 이동 의미**:
  - `Stairs`: 같은 던전 층의 elevation을 걸어서 이동.
  - `Ladder`: 해당 타일에서 자기 탭/Space로 명시적 링크 이동. 비주얼 길이는 실제 단차까지만.
  - `StairsUp/Down`: 입구를 밟는 즉시 반대편 링크까지 한 행동으로 처리하는 던전 층 전환.
  - `Hole`: 유일하게 위·아래 국소 시야와 낙하를 허용하는 실제 개구부.
  - PLAY에서는 현재 층만 기본 표시하며 다른 층은 Hole 국소 미리보기 외에는 숨긴다.
- **FOV/전투 정보**: Unknown/Explored/Visible 3상태. 시야 밖 적의 피해·사망 UI는
  공개하지 않으며, 시체는 기본 3턴 뒤 월드와 탭 대상에서 제거한다.
  시야선·수직 개구부 투시·근접 도달 기하·FOV 컬럼 해석의 SSOT는 모두 `SightRules`다
  (`CombatRules`·`GridVisibility`는 위임). 수직은 실제 개구부만 통과하고, 컬럼은 span으로 봐서
  지면과 머리 위 구조물(캐치워크)이 함께 잡힌다.
- **첫 던전/보스**: `forgotten-catacombs`는 B1~B10 단일 던전이다. B10의 `묘지기`를
  처치하기 전에는 최심층 출구가 붉게 봉인되고, 처치 후 청록 해금 연출과 전용 HUD가 갱신된다.
  아레나에는 생성기가 고른 제단이 서고(처치 후 신호색이 식음), 바로 위층(B9)에 들어서면
  접근 전조를 한 판에 한 번 알린다(`DungeonBossArenaRules`).
  최심층 도착만으로 승리하지 않으며 출구 모달의 `던전 정복`을 선택해야 정산·런 종료가 확정된다.
  체크포인트는 `dungeonId/stageCount/bossDefeated`를 보존한다.
- **전투 표현**: `CombatPresentationRules`가 물리/화염/냉기/강타를 분리한다. Gameplay는
  근접 돌진·스쿼시/플래시·픽셀 버스트·감쇠 카메라 흔들림을 적용한다. 화상은 주황 불꽃 고리,
  빙결은 청록 결정 고리이며 부여/연장/상쇄를 구분한다. 적 FX도 반드시 FOV를 따른다.
- **플레이테스트 계측**: `RunTelemetry`가 층별 시간·턴·피해·처치·아이템(획득/사용/조합)·휴식·
  숨은 방·낙하·상태/원소 반응을 체크포인트와 함께 누적하고, `RefreshBands()`가 이를 깊이 구간
  (Shallow B1~B3 / Mid B4~B6 / Deep B7~B9 / Boss B10+)으로 롤업한다 — 구간 값은 **파생**이라
  따로 기록하지 않는다. 개발 디버그 창에서 구간 비교/수동 저장하며, 판 종료 시
  `development-profile/telemetry`에 JSON 리포트를 자동 확정한다.
- **숨은 방**: B1~B9 중 seed로 고른 3개 층에 `SecretDoor` 막다른 방이 생긴다. 공개 전에는
  벽처럼 이동·FOV를 막고, 인접 균열의 `수상한 벽 조사` 또는 폭발로 `SecretPassage`가 된다.
  `SecretRoomRules`와 `DungeonFloorInfo.SecretDoor/SecretReward`를 우회해 별도 판정을 만들지 않는다.
- **아트 방향(전환 중)**: 테마를 **포스트 아포칼립스/이상 미궁**으로 전환 확정(GDD §10 v0.3).
  방향·레퍼런스 SSOT는 `docs/art-direction/project-c-postapoc-art-direction-v1.md`, 리스킨 표는
  `...postapoc-reskin-table-v1.md`. 팔레트 *원리*(청흑 바탕+국소 호박 광원+신호색 1개)는 유지하고
  재료 어휘(석재→콘크리트/벽돌/녹, 횃불→비상등/네온, 마법 포탈→이상 균열)만 바꾼다.
  **현재 구현은 아직 판타지 웜 다크 디오라마**다 — 허브는
  `docs/art-direction/project-c-warm-diorama-hub-target-v1.png`
  기준으로 횃불에 데워진 석재 + 토치 골드 모닥불/횃불 + 틸 포탈을 사용한다.
  `IsoPrototypeDemo`의 허브 바닥/전면 두께/장식 벽/로컬 광원만 분기하며, 던전 카탈로그와
  FOV·상태 색은 건드리지 않는다. 광원 타일과 허브 소품은 시점 회전 때 같은 GridPos로 다시 투영한다.
- **던전 공통 톤**: 모든 깊이는 `project-c-torchstone.gpl`의 18색 마스터 팔레트와
  `ProjectCEnvironmentCatalog`의 런타임 역할색을 공유한다. 기본 환경은 청흑 void와
  횃불에 데워진 웜 그레이·토프 석재, 물리 광원은 토치 골드, 마법/출구는 틸로 읽히게 한다.
  깊이별 변주는 이 공통 톤 위에서만
  제한적으로 적용하며, 같은 던전 층의 `LocalHeight`는 색상 테마가 아니라 명도와 전면 두께로 구분한다.
  **깊이 변주의 통로는 세 가지뿐이다** — 밴드 스프라이트 슬롯, 구조(캐치워크 길이), 광원 밀도(등잔 희소도).
  `DungeonSurfaceFor`의 석재색은 모든 깊이에서 같아야 한다(테스트로 고정). 값은 `DungeonBandProfile`.
- **Aseprite 파이프라인**: `com.unity.2d.aseprite 5.0.3`을 사용한다.
  최종 아트 SSOT는 `Assets/_Project/Art/Source/Aseprite`의 `.aseprite`/`.ase` 원본이다.
  `ProjectCAsepritePipeline`이 Point/PPU 64/Canvas Pivot/무압축/AnimationClip을 강제하고
  정식 파일명의 첫 프레임을 공용 `ProjectCEnvironmentCatalog`에 자동 연결한다.
  `Art/Runtime` PNG는 원본이 없는 슬롯의 폴백이며 최종본으로 직접 수정하지 않는다.
- **장비**: 무기 1 + 보조 1 슬롯. **어떤 장비도 공격력을 올리지 않는다** — 사거리 2(긴 파이프),
  명중 넉백(대형 렌치), 피해 -1(표지판 방패), 안전 낙하 +2(완충 부츠)처럼 규칙만 바꾼다.
  대장간이 골드로 제작·장착을 관리하고(`ForgeRules`), 옛 영구 스탯 강화는 제거했다(GDD §11).
  장착 장비는 백팩 공간을 쓰지 않으며 출정 준비 격자에도 나오지 않는다.
  **장비도 익스트랙션 규칙을 탄다** — 장착 = 반입이고, 죽거나 포기하면 잃는다.
  살아 나와야 창고로 돌아오며, 창고에 남긴 예비 장비만 안전하다.
- **백팩/창고**: 던전 백팩은 `BackpackRules` 6×4 멀티슬롯(1×1/1×2/2×2)이며
  `BackpackLayout` 자동 배치를 UI가 그대로 그린다. 공간 부족 시 월드 아이템은 남고,
  허브 창고는 종류별 중첩 저장을 유지한다. `ExpeditionLoadoutRules`가 창고와 출정 백팩 사이의
  이동·영웅 기본 지급품·초과분 복귀를 담당한다. 허브에서 선택한 물품만 던전 진입 시 반입하고
  나머지는 창고에 보존한다. 모바일은 선택 후 반대편 탭, PC는 버튼/드래그를 사용한다.
- **최근 검증 기준**: EditMode `ProjectC.Tests.EditMode` **673/673 통과**,
  PlayMode `ProjectC.Tests.PlayMode` **1/1 통과**. 변경 후에는 숫자를 맹신하지 말고 둘 다 다시 실행한다.
- **작업 트리 주의**: 현재 여러 기능 변경이 아직 커밋되지 않은 상태일 수 있다.
  작업 시작 시 `git status`/`git diff`를 확인하고 기존 변경을 reset/checkout으로 지우지 않는다.

## 디렉터리 구조

```
Assets/_Project/
  Art/Source/Aseprite/ # 최종 픽셀아트 SSOT. Unity 2D Aseprite Importer 직접 임포트
  Editor/ArtPipeline/  # Aseprite 임포트 규격·Catalog 자동 연결·검증 메뉴
  Scripts/Core/       # 순수 C# 로직 — 격자(GridPos/TileData/GridMap/IsoGrid), 시야(GridVisibility),
                      # 경로(GridPathfinder), 절차 생성(DungeonLayout), 전투/상태(CombatantState/StatusEffects),
                      # 낙하·넉백(FallRules), AI(MonsterBrain/MonsterRoster/MonsterActivation), 아이템(Items)
  Scripts/Gameplay/   # MonoBehaviour — GridManager, IsoTapInput, IsoVisualCatalog, PrototypeHudController,
                      # IsoPrototypeDemo(partial 5개: 본체/Enemies/Falls/Visibility/Sprites),
                      # MainMenuController, HubHudController, ResponsiveUiLayout
  Tests/EditMode/     # EditMode 테스트 (규칙별 *Tests.cs)
  Tests/PlayMode/     # 실제 씬 흐름 통합 스모크 테스트
  Scenes/             # MainMenu.unity, Hub.unity, IsoPrototype.unity
  UI/                 # MainMenuHUD, HubHUD, PrototypeHUD.Mobile/Desktop, DisplaySettings
  M0_SETUP.md         # 씬 연결 가이드
docs/                 # ROADMAP, SYSTEMS, UI_ARCHITECTURE (에이전트 참조 문서)
GDD.md                # 게임 기획서 (SSOT)
```

asmdef 5개: `ProjectC.Core`, `ProjectC.Gameplay`, `ProjectC.ArtPipeline.Editor`,
`ProjectC.Tests.EditMode`, `ProjectC.Tests.PlayMode`.

## Unity MCP

- 이 리포는 MCP for Unity 자동화 경로를 사용한다 (**연결됨**). 씬 셋업/테스트/스크린샷 검증을 MCP로.
- 스크립트 생성/수정 후에는 `read_console`로 컴파일 에러 확인.
- 씬/UI 변경은 가능하면 실제 Play와 모바일 세로·PC 가로 Game View에서 각각 캡처 검증.
- 전체 회귀 테스트는 EditMode `ProjectC.Tests.EditMode`와 PlayMode
  `ProjectC.Tests.PlayMode`를 모두 실행한다.

## 작업 컨벤션

- 주변 코드의 스타일(네이밍, 주석 밀도)을 따른다.
- 새 로직에는 EditMode 테스트를 함께 추가.
- 범위를 잔인할 정도로 좁게 유지. 기둥 4개에 부합하지 않는 기능은 보류.
- 커밋/푸시는 사용자가 요청할 때만.

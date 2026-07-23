# Project-C — Claude / Codex 작업 지침

> 이 파일은 Claude Code와 Codex(및 기타 AI 코딩 에이전트)가 공통으로 읽는 **진입점**이다. 얇게 유지한다.
> `AGENTS.md`는 이 파일을 가리키는 심볼릭 링크다.

## 참조 문서 (상세는 여기로)

- `GDD.md` — 게임 기획서. **설계 결정의 최종 출처(SSOT).** 코드/기획 판단 시 먼저 참조.
- `docs/ROADMAP.md` — 마일스톤 + 현재 진행/환경 상태.
- `docs/SYSTEMS.md` — 시스템 설계 요약(격자·FOV·낙하·상태이상·AI·크로스플랫폼).
- `docs/UI_ARCHITECTURE.md` — UI 이원화(UI Toolkit/UGUI) 방침 + Claude 디자인 워크플로. **UI 판단 SSOT.**
- `Assets/_Project/M0_SETUP.md` — 씬 연결 가이드.

## 프로젝트 개요

- **장르**: 모바일 아이소메트릭 다층(elevation) 던전 크롤러 (Shattered Pixel Dungeon 계보 + 층간 낙하 전투)
- **엔진/언어**: Unity 2D (Isometric Tilemap) · C# · Unity 6000.5.4f1
- **플랫폼**: iOS / Android (PC 동시 지원 고려 → 입력 추상화 필수)
- **개발 인원**: 1인
- **한 판 목표**: 최심층 도달 (로그라이트 메타 프로그레션, 정통 판타지 테마) — 상세 `docs/ROADMAP.md`.

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

## 현재 구현 스냅샷 (2026-07-23)

> 아래는 빠른 인수인계용 요약이다. 세부 규칙과 완료 이력은
> `docs/SYSTEMS.md`, `docs/UI_ARCHITECTURE.md`, `docs/ROADMAP.md`를 따른다.

- **씬 흐름**: Build Settings `0 MainMenu → 1 Hub → 2 IsoPrototype`.
  - 새 게임: `MainMenu → Hub → IsoPrototype`.
  - 이후 프롤로그/세계관은 `MainMenuController.StartNewGame()`과 Hub 사이에 별도 씬으로 삽입한다.
  - 던전의 `로비로 가기`는 Hub, 게임오버의 `메뉴로`는 MainMenu로 이동한다.
- **UI/해상도**: 화면공간 UI는 UI Toolkit. `MainMenuHUD`, `HubHUD`,
  `PrototypeHUD.Mobile/Desktop`이 공용 `DisplaySettings`와 `ResponsiveUiLayout`을 사용한다.
  에디터/개발 빌드 설정창에서 `AUTO/MOBILE/PC`와 대표 해상도를 즉시 바꿀 수 있다.
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
- **백팩/창고**: 던전 백팩은 `BackpackRules` 6×4 멀티슬롯(1×1/1×2/2×2)이며
  `BackpackLayout` 자동 배치를 UI가 그대로 그린다. 공간 부족 시 월드 아이템은 남고,
  허브 창고는 종류별 중첩 저장을 유지한다. `ExpeditionLoadoutRules`가 창고와 출정 백팩 사이의
  이동·영웅 기본 지급품·초과분 복귀를 담당한다. 허브에서 선택한 물품만 던전 진입 시 반입하고
  나머지는 창고에 보존한다. 모바일은 선택 후 반대편 탭, PC는 버튼/드래그를 사용한다.
- **최근 검증 기준**: EditMode `ProjectC.Tests.EditMode` **517/517 통과**,
  Unity 콘솔 오류 0건. 변경 후에는 숫자를 맹신하지 말고 전체 테스트를 다시 실행한다.
- **작업 트리 주의**: 현재 여러 기능 변경이 아직 커밋되지 않은 상태일 수 있다.
  작업 시작 시 `git status`/`git diff`를 확인하고 기존 변경을 reset/checkout으로 지우지 않는다.

## 디렉터리 구조

```
Assets/_Project/
  Scripts/Core/       # 순수 C# 로직 — 격자(GridPos/TileData/GridMap/IsoGrid), 시야(GridVisibility),
                      # 경로(GridPathfinder), 절차 생성(DungeonLayout), 전투/상태(CombatantState/StatusEffects),
                      # 낙하·넉백(FallRules), AI(MonsterBrain/MonsterRoster/MonsterActivation), 아이템(Items)
  Scripts/Gameplay/   # MonoBehaviour — GridManager, IsoTapInput, IsoVisualCatalog, PrototypeHudController,
                      # IsoPrototypeDemo(partial 5개: 본체/Enemies/Falls/Visibility/Sprites),
                      # MainMenuController, HubHudController, ResponsiveUiLayout
  Tests/EditMode/     # EditMode 테스트 (규칙별 *Tests.cs)
  Scenes/             # MainMenu.unity, Hub.unity, IsoPrototype.unity
  UI/                 # MainMenuHUD, HubHUD, PrototypeHUD.Mobile/Desktop, DisplaySettings
  M0_SETUP.md         # 씬 연결 가이드
docs/                 # ROADMAP, SYSTEMS, UI_ARCHITECTURE (에이전트 참조 문서)
GDD.md                # 게임 기획서 (SSOT)
```

asmdef 3개: `ProjectC.Core`, `ProjectC.Gameplay`, `ProjectC.Tests.EditMode`.

## Unity MCP

- 이 리포는 MCP for Unity 자동화 경로를 사용한다 (**연결됨**). 씬 셋업/테스트/스크린샷 검증을 MCP로.
- 스크립트 생성/수정 후에는 `read_console`로 컴파일 에러 확인.
- 씬/UI 변경은 가능하면 실제 Play와 모바일 세로·PC 가로 Game View에서 각각 캡처 검증.
- 전체 회귀 테스트는 EditMode assembly `ProjectC.Tests.EditMode`를 실행한다.

## 작업 컨벤션

- 주변 코드의 스타일(네이밍, 주석 밀도)을 따른다.
- 새 로직에는 EditMode 테스트를 함께 추가.
- 범위를 잔인할 정도로 좁게 유지. 기둥 4개에 부합하지 않는 기능은 보류.
- 커밋/푸시는 사용자가 요청할 때만.

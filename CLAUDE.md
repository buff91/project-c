# Project-C — Claude / Codex 작업 지침

> 이 파일은 Claude Code와 Codex(및 기타 AI 코딩 에이전트)가 공통으로 읽는 **진입점**이다. 얇게 유지한다.
> `AGENTS.md`는 이 파일을 가리키는 심볼릭 링크다.

## 참조 문서 (상세는 여기로)

- `GDD.md` — 게임 기획서. **설계 결정의 최종 출처(SSOT).** 코드/기획 판단 시 먼저 참조.
- `docs/STATUS.md` — **현재 구현 스냅샷.** 작업 시작 시 먼저 읽는다("지금 뭐가 돌아가나").
- `docs/ROADMAP.md` — 마일스톤 + 현재 진행/환경 상태("무엇을 다음에 하나").
- `docs/SYSTEMS.md` — **시스템별 설계 규칙**(텔레메트리·익스트랙션·배고픔·장비·숨은 방·FOV·낙하·AI…).
  "이 규칙이 어떻게 동작해야 하나"를 볼 때.
- `docs/ARCHITECTURE.md` — **코드 아키텍처 지도**(계층·좌표 기반·생성·전투·프레젠테이션).
  "이 로직이 어느 계층에 살고 무엇에 의존하나"를 볼 때. SYSTEMS와 주제가 겹치면
  **규칙은 SYSTEMS, 코드 배치는 ARCHITECTURE**를 믿는다.
- `docs/CODE_STRUCTURE.md` — 파일/파셜 레이아웃 지도 + SSOT 표("어떤 코드가 어느 파일에 사는가").
- `docs/UI_ARCHITECTURE.md` — UI 이원화(UI Toolkit/UGUI) 방침 + Claude 디자인 워크플로. **UI 판단 SSOT.**
- `docs/UI_DESIGN_SYSTEM.md` — 팔레트/토큰/컴포넌트 등 UI 디자인 시스템 값.
- `docs/ART_PIPELINE.md` — 아트 생성→마감 파이프라인 개요(상세는 `docs/art-direction/`).
- `Assets/_Project/M0_SETUP.md` — 씬 연결 가이드.

## 프로젝트 개요

- **장르**: 모바일 아이소메트릭 다층(elevation) 던전 크롤러
  (Shattered Pixel Dungeon 계보 + 지형·원소·높이 상호작용 전투)
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

## 현재 구현 스냅샷 → `docs/STATUS.md`

지금 코드가 어디까지 와 있는지의 요약은 **`docs/STATUS.md`** 에 있다 (진입점을 얇게 두려고 분리했다).
작업을 시작할 때 그 파일을 먼저 읽는다. 한 줄 요약:

- 첫 던전은 **폐병원(상승, `B2 → … → 8F` + 옥상 출구)** 10개 층 + 최상층 보스(`감시자`)이며
  (코드 ID `forgotten-catacombs` 유지, 생성기 상승 전환은 미착수), FOV·낙하·상태이상/원소 반응·
  장비·배고픔·익스트랙션·백팩/창고·숨은 방·텔레메트리가 붙어 있다.
- 아트는 **판타지 웜 다크 디오라마 구현 상태**이고, 테마는 포스트 아포칼립스로 전환 확정 — 리스킨 진행 중이다.
- **작업 트리 주의**: 커밋되지 않은 변경이 남아 있을 수 있다. 시작 시 `git status`/`git diff`를 확인하고
  기존 변경을 reset/checkout으로 지우지 않는다.

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
Tools/
  ArtPipeline/        # 아트 후처리 Python (팔레트 잠금·시트 슬라이스·9-slice 빌드)
  CoreTests/          # Unity 없이 Core 규칙 테스트를 돌리는 dotnet shim
docs/                 # STATUS, ROADMAP, SYSTEMS, ARCHITECTURE, CODE_STRUCTURE,
                      # UI_ARCHITECTURE, UI_DESIGN_SYSTEM, ART_PIPELINE (에이전트 참조 문서)
GDD.md                # 게임 기획서 (SSOT)
```

asmdef 5개: `ProjectC.Core`, `ProjectC.Gameplay`, `ProjectC.ArtPipeline.Editor`,
`ProjectC.Tests.EditMode`, `ProjectC.Tests.PlayMode`.

## 테스트 (두 경로)

- **Unity 없이 — Core 규칙**: `./Tools/CoreTests/run-core-tests.sh` (`dotnet test`).
  `Scripts/Core`가 UnityEngine에 거의 의존하지 않는 덕에 규칙 테스트를 에디터 없이 돌린다.
  **에디터가 없는 환경(웹/원격 세션)에서는 이게 유일한 검증 경로다** — 돌리지 않았으면
  "테스트 통과"라고 쓰지 않는다. 경계는 `Tools/CoreTests/ProjectC.CoreTests.csproj` 주석 참조.
- **에디터에서 — 전체 회귀**: EditMode `ProjectC.Tests.EditMode` + PlayMode
  `ProjectC.Tests.PlayMode`를 **모두** 실행한다. shim은 이걸 대체하지 않는다
  (씬·스프라이트·HUD·UI Toolkit 계약은 에디터에서만 검증된다).
- 새 로직에는 EditMode 테스트를 함께 추가한다. Core 규칙 테스트라면 UnityEngine 타입을
  쓰지 않는 편이 좋다 — 그러면 shim에서도 자동으로 돌아간다.

## 자동 방어선 (훅 · CI)

검증을 사람의 기억이나 에이전트의 주장에 맡기지 않는다. 두 층으로 기계가 확인한다.

- **로컬 훅** (`.claude/settings.json` → `Tools/Hooks/`) — 모든 브랜치에서 항상 돈다.
  - `PostToolUse`(`check-cs-edit.sh`): `Scripts/Core`의 UnityEngine 의존,
    `Assets` 아래 `.meta` 누락, Unity 의존 EditMode 테스트의 shim 제외 누락을 잡는다.
  - `Stop`(`verify-core-tests.sh`): `.cs`를 건드린 세션이 **테스트 실패 상태로 끝나지 못한다.**
    dotnet이 없으면 조용히 건너뛴다(설치는 `run-core-tests.sh`가 한다).
- **CI** (`.github/workflows/core-tests.yml`) — **`release/**` 브랜치 한정**
  (push + release를 타깃하는 PR). Core 테스트 + Core 순수성 + `.meta` 누락을 검사한다.
  `main`과 작업 브랜치에는 CI가 없다 — 그 구간은 위 로컬 훅이 유일한 방어선이므로
  훅을 끄고 작업하지 않는다.

## Unity MCP

- 이 리포는 MCP for Unity 자동화 경로를 사용한다 (**연결됨**). 씬 셋업/테스트/스크린샷 검증을 MCP로.
- 스크립트 생성/수정 후에는 `read_console`로 컴파일 에러 확인.
- 씬/UI 변경은 가능하면 실제 Play와 PC 가로 Game View에서 캡처 검증(현재 우선순위 절 참조).

## 스킬 (반복 절차는 여기에 있다)

Claude Code에서 `/이름`으로 호출한다. 절차를 매번 산문 문서에서 재현하지 말고 이걸 쓴다.

- `/test` — 테스트를 올바른 경로로 실행하고 실패를 판정한다(shim / 에디터 회귀 구분).
- `/feature-done` — 기능 마감 체크리스트(테스트·문서·파일 크기·범위·커밋).
- `/art-conform` — 아트 시안 → 게임 에셋 마감(팔레트 잠금·임포트 규격·검증).

정의는 `.claude/skills/<이름>/SKILL.md`. 절차가 바뀌면 문서가 아니라 **스킬을 고친다.**

## 작업 컨벤션

- 주변 코드의 스타일(네이밍, 주석 밀도)을 따른다.
- 새 로직에는 EditMode 테스트를 함께 추가.
- **범위를 잔인할 정도로 좁게 유지.** 기둥 4개에 부합하지 않는 기능은 보류.
  한 세션에서 무관한 시스템 여러 개를 건드리지 않는다 — 되돌릴 수 없게 엉킨다.
- **설계 결정은 코드보다 먼저.** 규칙이 확정되지 않았으면 구현 대신 먼저 확정한다
  (구현 뒤에 오는 설계 변경은 `fix:` 커밋으로 위장해서 나타난다).
- 기능·테스트·문서는 **같은 커밋에** 넣는다. 사후에 기억으로 쓰는 문서는 실제와 어긋난다.
- 커밋/푸시는 사용자가 요청할 때만.
- 커밋 메시지는 한국어로 쓴다(제목의 `feat:`/`fix:` 같은 타입 접두사는 영어 유지).

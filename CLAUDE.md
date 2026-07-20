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
- **엔진/언어**: Unity 2D (Isometric Tilemap) · C# · Unity 6000.5.0f1
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

## 디렉터리 구조

```
Assets/_Project/
  Scripts/Core/       # 순수 C# 로직 — 격자(GridPos/TileData/GridMap/IsoGrid), 시야(GridVisibility),
                      # 경로(GridPathfinder), 절차 생성(DungeonLayout), 전투/상태(CombatantState/StatusEffects),
                      # 낙하·넉백(FallRules), AI(MonsterBrain/MonsterRoster/MonsterActivation), 아이템(Items)
  Scripts/Gameplay/   # MonoBehaviour — GridManager, IsoTapInput, IsoVisualCatalog, PrototypeHudController,
                      # IsoPrototypeDemo(partial 5개: 본체/Enemies/Falls/Visibility/Sprites)
  Tests/EditMode/     # EditMode 테스트 (규칙별 *Tests.cs)
  UI/                 # PrototypeHUD.uxml/.uss (UI Toolkit 검증 HUD)
  M0_SETUP.md         # 씬 연결 가이드
docs/                 # ROADMAP, SYSTEMS, UI_ARCHITECTURE (에이전트 참조 문서)
GDD.md                # 게임 기획서 (SSOT)
```

asmdef 3개: `ProjectC.Core`, `ProjectC.Gameplay`, `ProjectC.Tests.EditMode`.

## Unity MCP

- 이 리포는 MCP for Unity 자동화 경로를 사용한다 (**연결됨**). 씬 셋업/테스트/스크린샷 검증을 MCP로.
- 스크립트 생성/수정 후에는 `read_console`로 컴파일 에러 확인.

## 작업 컨벤션

- 주변 코드의 스타일(네이밍, 주석 밀도)을 따른다.
- 새 로직에는 EditMode 테스트를 함께 추가.
- 범위를 잔인할 정도로 좁게 유지. 기둥 4개에 부합하지 않는 기능은 보류.
- 커밋/푸시는 사용자가 요청할 때만.

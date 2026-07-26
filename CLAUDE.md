# Project-C — Claude / Codex 작업 지침

> Claude Code와 Codex(및 기타 AI 코딩 에이전트)가 공통으로 읽는 **진입점**이다. **얇게 유지한다** — 자라나는 서술은
> 소유 문서에 쓰고 여기서는 링크만 건다. `AGENTS.md`는 이 파일의 심볼릭 링크다.

## 문서 지도 (무엇을 어디서 찾나)

- `GDD.md` — 게임 기획서. **설계 결정의 최종 출처(SSOT).** 기획/규칙 판단은 여기부터.
- `docs/STATUS.md` — **현재 구현 스냅샷**("지금 뭐가 돌아가나"). 작업 시작 시 먼저 읽는다.
- `docs/ROADMAP.md` — 마일스톤 + 진행 상태("무엇을 다음에 하나"). **환경 현황**(Unity 버전·MCP 연결 여부·DOTween·
  asmdef 목록)도 여기가 소유한다 — 이 파일에 복제하지 않는다.
- `docs/SYSTEMS.md` — **시스템별 설계 규칙**("이 규칙이 어떻게 동작해야 하나"). 수치·판정은 전부 여기.
- `docs/ARCHITECTURE.md` — **코드 아키텍처 지도**("이 로직이 어느 계층에 살고 무엇에 의존하나"). SYSTEMS와 겹치면
  **규칙은 SYSTEMS, 코드 배치는 ARCHITECTURE**를 믿는다.
- `docs/CODE_STRUCTURE.md` — 파일/파셜 레이아웃 지도 + SSOT 표("어떤 코드가 어느 파일에 사는가").
- `docs/UI_ARCHITECTURE.md` — UI 이원화 방침 + 디자인 워크플로. **UI 판단 SSOT.** 값은 `docs/UI_DESIGN_SYSTEM.md`.
- `docs/ART_PIPELINE.md` — 아트 생성→마감(상세 `docs/art-direction/`). `Assets/_Project/M0_SETUP.md` — 씬 연결.

## 프로젝트 개요

- **장르/엔진**: 아이소메트릭 다층(elevation) 던전 크롤러 — SPD 계보 + 지형·원소·높이 상호작용 전투. Unity 2D
  (Isometric Tilemap) · C#. 1인 개발. iOS/Android 지향이나 **현재 검증 타깃은 PC 단독**(아래 절).
- **핵심 설계 기둥**(모든 결정의 기준, 상세 GDD §2) — **① 입체 공간(Verticality) · ② 상호작용 & 상태이상 ·
  ③ 제한된 시야(FOV) · ④ 파밍 & 조합.** 창발적 전술은 이 넷이 충돌해 생기는 결과 — 목표이자 검증 기준.
- **한 판 목표**: 첫 던전 **최상층 보스 처치 후 출구 정복** — 도달만으로는 승리하지 않는다(GDD §10). 진행 구간은
  방향 중립어(초반/중반/후반/보스)로 부른다 — 첫 던전이 **상승**이라 깊이 어휘는 거짓이 된다.
- **테마**: 포스트 아포칼립스 / 이상 미궁 (정통 판타지에서 전환 — GDD §10). 로그라이트 메타 프로그레션.
- **현재 상태 한 줄**(상세 `docs/STATUS.md`): 첫 던전은 **폐병원(상승, `B2 → … → 8F` + 옥상 출구)** 10개 층 + 최상층
  보스 `감시자`(코드 ID `forgotten-catacombs` 유지, **생성기가 방향을 매개변수로 받는다**). 플레이어는 **단일 원정자**
  (직업/영웅 선택 없음 — 정체성은 장비가 진다). 아트는 **포스트아포 마감 자산**으로 수렴, 잔여 발주가 남았다.

## 아키텍처 규칙 (반드시 준수)

- **로직 ↔ 비주얼 분리**: 순수 C# 로직(`Scripts/Core`)은 UnityEngine 의존 최소화. 씬 연동은 `Scripts/Gameplay`.
- **정렬(Sorting) 규칙은 `IsoGrid`에 집중**: floor(elevation) 우선 + (x+y) 정렬. 흩뿌리지 말 것.
- **타입 → 스프라이트/애니 매핑은 ScriptableObject**로. 데이터 중심.
- **입력 추상화**: 터치/마우스/키보드를 입력 레이어에서 액션 단위로 통일(게임 로직에 플랫폼 분기 금지).
- **성능**: 모바일이 하한선. "보이는 층 ≠ 활성 층", 몬스터 활성 반경/컬링 전제.

## 현재 개발 우선순위 (임시 — PC 우선)

> **모바일은 당분간 보류한다.** 모바일 세로·다해상도 검증과 이원 View 유지가 개발을 지나치게 느리게 해서, 한동안
> **PC(데스크톱/포인터)를 유일한 개발·검증 타깃**으로 둔다. 재개 시 이 절을 삭제한다.
> (`docs/ROADMAP.md`·`docs/UI_ARCHITECTURE.md`가 상세 규칙으로 이 절을 가리킨다 — 헤딩을 옮기면 그쪽이 깨진다.)

- **검증**: 새 UI/씬 변경은 **PC 가로 Game View만** 캡처 검증. 모바일 세로·다해상도 회귀는 생략. HUD 기본값 `PC`.
- **패리티 불필요**: 새 기능을 모바일 배치(`PrototypeHUD.Mobile.uxml`/`ui-touch`/56px)에 이식하지 않는다 — 기존
  배치는 **깨지지 않을 정도로만** 둔다.
- **유지(삭제 금지)**: `IsoTapInput`·`ResponsiveUiLayout`·이원 UXML·세이브 포맷 독립성 — 저렴하고 되돌리기 쉬우며
  재개 시 재작업을 막는다. 완화되는 것은 위 규칙 중 "모바일 성능 하한선"과 "세로·가로 각각 검증"뿐이고, 입력
  추상화와 "플랫폼 분기 금지"는 그대로 유효하다.

## 디렉터리 (얇은 지도 — 파일 단위는 `docs/CODE_STRUCTURE.md`)

```
Assets/_Project/
  Scripts/Core/ · Scripts/Gameplay/  # 순수 C# 규칙(격자·시야·경로·생성·전투/상태·낙하·AI·아이템)
                                     # / MonoBehaviour·씬 연동(IsoPrototypeDemo 는 관심사별 partial)
  Tests/EditMode/ · Tests/PlayMode/  # 규칙별 *Tests.cs · 씬 흐름 통합 스모크
  Scenes/ · UI/ · Editor/ArtPipeline/  # 씬 · UXML/USS · Aseprite 임포트 메뉴
  Art/Source/Aseprite/               # 원본이 도착할 자리 + 기준 팔레트 .gpl — 아직 .aseprite 원본은 없다
  Art/Runtime/ · Art/Environment/    # 실제로 게임에 연결된 PNG + 환경 카탈로그 (현재 동작 경로)
Tools/  ArtPipeline(후처리 파이썬) · CoreTests(에디터 없이 도는 dotnet shim) · Hooks(로컬 검증 훅)
docs/ · GDD.md                       # 위 「문서 지도」 (+ art-direction/ · captures/ 검증 참고 이미지)
```

## 검증 (테스트 · 훅 · CI · MCP)

- 테스트 경로는 **두 개**다 — Unity 없이 도는 **Core shim**(`./Tools/CoreTests/run-core-tests.sh`)과 에디터의 **전체
  회귀**(EditMode + PlayMode 둘 다). 절차·한계·보고 형식은 `/test` 스킬이 소유한다. shim은 회귀를 대체하지 않는다
  (씬·스프라이트·HUD·UI Toolkit 계약은 에디터에서만 검증된다). **돌리지 않은 것을 "테스트 통과"라고 쓰지 않는다.**
- **로컬 훅**(`.claude/settings.json` → `Tools/Hooks/`, 모든 브랜치) — `PostToolUse`(`check-cs-edit.sh`)는
  `Scripts/Core`의 UnityEngine 의존·`Assets` 아래 `.meta` 누락·Unity 의존 EditMode 테스트의 shim 제외 누락을 잡고,
  `Stop`(`verify-core-tests.sh`)은 `.cs`를 건드린 세션이 **테스트 실패 상태로 끝나지 못하게** 한다.
- **CI**(`.github/workflows/core-tests.yml`)는 **`release/**` 브랜치 한정**이다. `main`과 작업 브랜치에는 CI가 없어
  위 훅이 유일한 방어선 — **훅을 끄고 작업하지 않는다.**
- **Unity MCP**는 붙어 있는 세션에서만 쓴다(연결 여부는 `docs/ROADMAP.md`「환경 현황」 — 웹/원격 세션엔 에디터가
  없다). 스크립트 수정 후 `read_console`로 컴파일 에러 확인. 캡처(`manage_ui render_ui`)는 도구 제약상
  `Assets/Screenshots/`에 임시로 떨어지지만 **Unity 참조가 없는 보관본은 즉시 `docs/captures/`로 옮긴다.**
  임시 경로는 리포에서 제외한다(`.gitignore`).

## 스킬 (반복 절차는 여기에 있다)

`/test`(테스트 실행·실패 판정) · `/feature-done`(기능 마감 체크리스트) · `/art-conform`(아트 시안 → 에셋 마감).
정의는 `.claude/skills/<이름>/SKILL.md`. 절차를 산문으로 재현하지 말고 이걸 쓰며, 바뀌면 **스킬을 고친다.**

## 작업 컨벤션

- 주변 코드의 스타일(네이밍, 주석 밀도)을 따른다.
- 새 로직에는 EditMode 테스트를 함께 추가한다. Core 규칙 테스트는 UnityEngine 타입을 피하면 shim에서도 돈다.
- **범위를 잔인할 정도로 좁게 유지.** 기둥 4개에 안 맞는 기능은 보류. 한 세션에서 무관한 시스템 여러 개를
  건드리지 않는다 — 되돌릴 수 없게 엉킨다.
- **설계 결정은 코드보다 먼저.** 규칙이 확정되지 않았으면 먼저 확정한다(구현 뒤에 오는 설계 변경은 `fix:` 커밋으로
  위장해서 나타난다).
- 기능·테스트·문서는 **같은 커밋에** 넣는다. 사후에 기억으로 쓰는 문서는 실제와 어긋난다.
- **낡을 수치는 문서에 심지 않는다.** 줄 수·파일 수·테스트 개수를 꼭 써야 하면 기준 커밋을 함께 적는다.
- **작업 트리 주의**: 커밋 안 된 변경이 남아 있을 수 있다 — 시작 시 `git status`/`git diff`를 확인하고 기존 변경을
  reset/checkout으로 지우지 않는다.
- 커밋/푸시는 사용자가 요청할 때만. 커밋 메시지는 한국어로 쓴다(`feat:`/`fix:` 타입 접두사는 영어 유지).

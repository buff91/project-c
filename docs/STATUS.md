# 현재 구현 스냅샷

> **역할**: 지금 코드가 실제로 어떤 상태인지에 대한 빠른 인수인계 요약이다.
> CLAUDE.md에서 분리해 나왔다 — 진입점은 얇게 유지하고, 자라나는 이력은 여기에 쌓는다.
> 세부 규칙과 완료 이력의 SSOT는 여전히 `docs/SYSTEMS.md`, `docs/UI_ARCHITECTURE.md`,
> `docs/ROADMAP.md`이며, 설계 결정의 최종 출처는 `GDD.md`다.
> 충돌하면 이 문서가 아니라 위 문서들을 믿는다.

- **씬 흐름**: Build Settings `0 MainMenu → 1 Hub → 2 IsoPrototype`.
  - 새 게임: `MainMenu → Hub → IsoPrototype`.
  - 이후 프롤로그/세계관은 `MainMenuController.EnterCamp()`와 Hub 사이에 별도 씬으로 삽입한다.
  - **타이틀은 앱을 켤 때 한 번 지나는 문이다.** `게임 시작`은 언제나 Hub로 가고,
    `이어하기`는 던전 중간 저장이 있을 때만 나타나 던전으로 직행한다
    (`TitleEntryRouting`). 체크포인트가 없으면 회색 비활성이 아니라 **숨긴다.**
  - 던전의 `로비로 가기`와 게임오버의 `캠프로 돌아가기`는 모두 Hub로 간다.
    던전 씬을 바로 리로드하는 재도전 버튼은 두지 않는다(방금 번 골드·해금을 건너뛴다).
- **UI/해상도**: 화면공간 UI는 UI Toolkit. `MainMenuHUD`, `HubHUD`,
  `PrototypeHUD.Mobile/Desktop`이 공용 `DisplaySettings`와 `ResponsiveUiLayout`을 사용한다.
  PC 기본 창과 개발 Game View는 **16:9를 유지한 2560×1440**이며, 설정창에서
  `AUTO/MOBILE/PC`와 대표 해상도를 즉시 바꿀 수 있다. 모든 화면 루트는
  `ui-touch/ui-pointer` 입력 프로필을 받는다. 짧은 화면에서는 본문을 스크롤한다.
  (터치 크기 하한 56/44/52px은 이 수치들은 **540 논리 높이** 시절 기준이다. 캔버스가 360 으로 줄어 같은 물리 크기는 56→37 · 44→29 · 52→35 로 환산된다(× 360/540). 실제 값은 백팩 셀 피치 40(누름 36)이라 환산 하한을 넘는다 — 화면 비율로는 오히려 넓어졌다.)
  **논리 캔버스는 640×360이다** — `PanelSettings`가 `ConstantPixelSize`이고 배율은 `UiPanelScale`이
  화면 짧은 축에서 정수로 정한다(1280×720·1920×1080·2560×1440이 전부 640×360으로 떨어져 배치가
  하나다). PC 최소 지원 창은 1280×720. 캔버스가 960×540에서 줄어든 만큼 같은 논리 px가 화면에서
  약 50% 커진 것이 시인성 대책의 실체다 — 정수 배율이 픽셀 폰트를 선명하게 만들진 않는다(실측 기각).
  PC 던전 HUD는 **Field Deck 4구역 + 1과도 밴드**다: 160px 바이탈(좌상, 5칸 분절 HP+상태이상 칩) ·
  256px 과도 밴드(상단 중앙, 보스/입장·발견 카드 공유) · 176×104 계기 묶음(우상, 층 눈금+
  `▲/◆/▼` 보기+56×56 미니맵+도구/회전) · 208×52 메시지 로그(좌하, 4줄) · 자원 184px/
  행동 268px 하단 두 레일. 크롬 어휘는 **플레이트와 창 둘뿐**이며, 열린 쿨 스틸 모서리 옆의 짧은
  마젠타 틱만 UI 액센트를 쓴다. Desktop 공용 행동 glyph는 축소본이 아닌 네이티브 12×12다.
  액션 휠은 여섯 셀 전체 footprint로 화면 경계와 현재 보이는 고정 HUD를 피한다. 화면 앵커·HUD 예약 영역·
  휠 `left/top`을 모두 휠 부모 로컬 좌표로 통일해 safe-area inset에서도 같은 계산을 쓴다. 처음 열려 geometry가
  아직 0인 고정 HUD에는 휠 표시를 한 레이아웃 패스만 보류해 한 프레임 겹침도 막는다.
  후속 작업 트리에서 Cmd/Ctrl 충돌 입력과 캐릭터 클릭 고정 경로를 제거하고 **Tab 홀드 전용**으로 바꿨다.
  발견 카드에는 PC 전용 `×`를 추가해 현재 카드만 닫고 큐는 보존한다.
  미니맵은 현재 활성 층 `MappedSilhouette`를 기본 윤곽으로 그리고 그 위에 `Explored`/`Visible`을
  덧씌운다. 내부 `B2` 층 배지·북쪽 `N`·7px 플레이어 마커를 얻었다. PC에서는 표식 그림은 유지한 채
  15px 클릭 영역으로 넓혀 자유 카메라를 플레이어에게 되돌린다. 평상시는 `B2 · 현재 층`, 수직
  관찰 때만 `플레이 B2 · 보기 B1`을 쓴다. 좌표는 디버그 패널에만 둔다. 행동 피드백은 3초 뒤 강조만 빠지고
  줄은 메시지 로그에 남는다. 입장/수직 이동 발견 카드는 FIFO로 덮어쓰지 않으며, 보스가 열리면 즉시
  숨겨 두 패널의 겹침을 막고 남은 노출 시간부터 재개한다. 실제 장치 위에서는 문맥 한 줄만 다시 보인다.
  `IsoPrototype.unity`와 씬 빌더는 Mobile/Desktop wrapper를 모두 명시적으로 연결한다. 2026-08-03
  Field Deck 기준선은 PC 2560×1440에서 정상 HUD·발견 카드+휠·실제 8F 보스+휠을 캡처했고, 과도 패널과 휠 셀의
  교차 면적은 모두 0이었다. 상시 플레이트는 96%, 보스/발견 플레이트는 완전 차폐해 월드 라벨과
  네온이 UI 신호색처럼 비치는 문제도 닫았다. 근거는
  `docs/captures/ui-field-deck-pc-qhd-2026-08-03.png`와
  `docs/captures/ui-field-deck-{notice-wheel,boss-wheel}-pc-qhd-2026-08-03.png`다. 후속의 기본 닫힘 상태와
  발견 카드 `×`·미니맵 내부 층/N/마커는
  `docs/captures/project-c-followup-{ui,discovery}-pc-qhd-2026-08-03.png`에서 다시 확인했다. 같은 작업 트리의
  최신 전체 Unity EditMode/PlayMode 회귀와 스크립트 컴파일 오류 검사도 통과했다. 정적 캡처에 담기지 않는 보행/층 전환
  시간 연출은 정상 진행 플레이테스트에서 따로 확인한다. 적 6종의 방향 상태에 이어 같은 날짜의
  **기업 추적 드론 교체와 전 적군 팔레트 재작업도 PC 런타임 카탈로그 캡처와 전체 회귀로 재승인했다**
  (아래 「액터 애니메이션」).
  메인 메뉴는 팔레트 잠금된 `ui-main-menu-backdrop.png`를 화면 비율에 맞춰 crop하고,
  중앙 저정보 영역 위에 기존 패널을 놓는다. 배경 소스·프롬프트·프로세서는 각각
  `docs/art-direction/project-c-main-menu-backdrop-source-v1*`와
  `Tools/ArtPipeline/process_ui_backdrops_v1.py`에 있다.
  메인·허브·던전은 같은 `PrototypePanelSettings`와 현재 Screen/Game View 해상도를 이어받는다.
  **던전 화면 톤**: 세 화면 모두 화면공간 비네트(`.pc-vignette`, 9-slice 스프라이트)를 루트 첫
  자식으로 깐다 — URP 포스트프로세싱은 켜지 않는다(근거는 `docs/UI_DESIGN_SYSTEM.md`).
  벽 등잔은 **아트와 빛이 같은 판정**(`Core/SconcePlacement.cs`)을 쓴다. 예전엔 두 해시가
  독립이라 그려진 램프는 빛을 내지 않고 빛 웅덩이엔 광원이 안 보였으며, 아트 해시가
  `viewQuarterTurns`를 포함해 시점을 돌리면 램프가 순간이동했다.
  등잔 자리는 **격자**다(`x*3 + y + seed오프셋`). 흩뿌리는 해시로 골랐더니 평균은 맞아도
  한 방에 보이는 뒷벽이 10칸 남짓이라 "한 개도 안 걸리는 방"이 꾸준히 나왔다(rarity 5에서 약 9%).
  실제로 시작 방 B2가 그렇게 비었다. 격자는 같은 밀도에서 **간격을 보장**한다 —
  `IsSconce_LeavesNoLongEmptyRunAlongAWall`이 빈 구간이 rarity를 넘지 않음을 단언한다.
  덕분에 `sconceLightIntensity`(0.8)와 `WallSconceRarity`(5/6/8/9) **값은 하나도 바꾸지 않았다** —
  문제는 광량이 아니라 분포였고, 깊이 밴드 정체성 그라디언트도 그대로다.

  **PC 월드 카메라는 허브·던전 모두 `playCameraSize` 2.3을 그대로 쓴다.** 허브는 13×9 캠프를
  한 화면에 넣으려고 물러나는 auto-fit을 제거하고 던전처럼 플레이어를 추종한다. 따라서 같은 타일과
  액터는 씬을 바꿔도 같은 화면 크기로 보인다. 전체 조감 배율 `debugCameraSize`는 던전 DebugAll에서만
  허용한다. 이 분기는 `OrthographicCameraFramingTests`가 고정하며, 동일 1280×720 비교본은
  `docs/captures/lobby-game-scale-{hub,dungeon}-1280x720.png`다.
  던전 PLAY에서는 PC **중클릭 드래그**로 현재 활성 층의 `MappedSilhouette` 투영 경계 안에서 카메라 중심만
  임시 분리할 수 있다. 버튼을 놓아도 위치를 유지하고 `Home`/`Escape`/미니맵 플레이어 마커 클릭,
  수락된 플레이어 행동,
  시점 회전·DebugAll 전환·수직 보기·투척 조준에서 즉시 플레이어 추종으로 돌아온다. 팬 중에도
  턴·플레이어 위치·FOV·AI·활성 층·미니맵과 기존 구도의 배율은 바뀌지 않으며, 드래그는 월드 탭을
  만들지 않는다. 단위/PlayMode 회귀와 PC 비교본은
  `docs/captures/camera-look-{follow,panned}-pc-2026-08-03.png`와 실제 마커 클릭 전후
  `docs/captures/camera-minimap-recenter-{panned,follow}-pc-2026-08-03.png`로 확인했다.
- **던전 화면 톤 / 메인 원정자**: PC Game View는 청흑 void·불투명 panel 안개 위에
  웜 그레이 콘크리트를 놓고, 호박색 물리광과 청록 신호색은 국소 표식에만 쓴다. 안개 다이아몬드는
  `Dungeon Backdrop` Sorting Layer에서 `Default` 월드보다 항상 뒤에 그린다. 교체 가능한
  `dungeonBackdrop` 슬롯에는 팔레트 잠금 128×64 배경판을 연결하고 런타임 알파를 25%로 제한해,
  스스로 미탐색 구조를 노출하지 않으면서 순흑색과 바닥 사이에 낮은 대비의 재질층만 만든다. 공개 가능한
  현재 층 토폴로지는 별도의 공용 저대비 `MappedSilhouette`가 담당한다. 예전의 큰 음수
  `sortingOrder`는 SpriteRenderer 범위에서 양수로 되감겨 바닥 앞에 겹쳤다.
  Facility 지역은 `hospitalFloor{Grate,Cracked,Service}`와
  `hospitalWall{Pipes,Window,Cabinet}Rising*` 9개 교체 슬롯을 seed 고정으로 희소 배치한다.
  바닥 드레싱은 공용 바닥 위에 합성한 완전한 타일이라 장식의 투명 여백이 void 구멍으로 뚫리지 않는다.
  얕은 층 앰비언트 0.5·플레이어 광원 반경 2·벽 등잔 세기 0.8로 중앙 광원 웅덩이와 어두운 벽을
  분리하며, 적·소품의 `actorVisualScale`은 0.72를 유지한다. 메인 원정자만
  `playerVisualScale` 0.80으로 키워 회색 벽 앞에서도 실루엣을 읽히게 하고, HP·마커·접촉 그림자는
  격자 크기에 남긴다.
  메인 원정자는 `project-c-expeditioner-grounded-source-v1.{png,prompt.md}`를
  `process_actor_knight_grounded_v1.py`로 마감한 `96×128` 승인 `Frame_0`에서 출발한다.
  `build_actor_knight_directional_v1.py`가 이 첫 프레임과 정체성을 보존하면서 4방향 6상태의 태그
  프레임 80개를 만들고, 정식 `actor-knight.aseprite`는 태그 밖 첫 프레임을 포함해 81프레임·
  24태그를 가진다. 전 프레임은 하드 알파·24색 이하 역할 팔레트·2×2 클러스터·발 `y=123` 계약을
  지킨다. `SurvivorAnimationApproved=true`라 던전 플레이어에 `SpriteClipAnimator`가 붙으며,
  월드 방향을 시점 회전 뒤 화면 방향으로 바꿔 해당 idle/walk/attack/hit/fall/death 클립을 고른다.
  이동은 한 칸 0.18초 위치·카메라 동시 보간을 유지하되, 정적 컷 전용 `Art` 자식 보행 변형은
  방향 클립이 있을 때 자동으로 꺼진다. 층 전환은 몸체·
  HP바·위치 표식·접촉 그림자를 함께 출발점 페이드아웃 → 완전 비가시 상태에서 플레이어와 카메라 재배치 →
  도착점 페이드인으로 읽히게 한다. 안정 상태
  몸체색은 상태색 × elevation tint × `TileLightColor`이고, 두 발 사이에는 작은 3단 접촉 AO만
  남긴다. 상시 플레이어 표식과 선택/공격 대상 표식도 각각 틸/앰버의 열린 코너 틱으로 줄였으며
  `marker-player.aseprite`·`marker-target.aseprite`가 정식 원본이다.
- **다층 월드 입력**: `IsoTapInput.TilePicker`가 실제 렌더 타일과 현재 활성 층 mapped 실루엣의
  아이소 다이아몬드를 `VisualPosition` 기준으로 고른다. 겹치면 **현재 활성 층 → Hole 미리보기 층 →
  같은 레이어의 렌더 정렬 순서**다. 전체 elevation 역산 방식으로 되돌리지 말 것.
- **위험 프롭 시작 배치**: 폭발통은 `DungeonPropPlacementRules`가 시작점에서 최소 2칸 떨어진
  일반 바닥 중 적·아이템·계단·시설 좌표가 아닌 곳을 고른다. 격자상 다른 칸이어도 90도 회전
  시 입구와 같은 화면 세로축에 포개지는 대각선 좌표는 제외하며, 안전 좌표가 없으면 생성하지
    않는다. PC 시작 화면 근거는 `docs/captures/barrel-safe-start-v2.png`다.
- **첫 던전 층내 평탄화**: 폐 아케이드 복합타워의 모든 생성 타일은 해당 층 base elevation
  (`LocalHeight 0`)에 있다. 일반 층내 `Stairs`·층내 사다리·캐치워크는 생성하지 않는다.
  `ElevationsPerFloor=4`, `StairsUp/Down`, Hole/WeakFloor, 창문 낙하, 엘리베이터는 그대로다.
  층내 높이 규칙과 raised 자산은 다른 원정지·수제 세트피스를 위한 엔진 능력으로 남아 있다.
- **수직 이동 의미**:
  - `Stairs`: 층내 높이를 허용한 원정지에서 같은 던전 층의 elevation을 **걸어서** 이동한다.
    **±1 단만** 담당한다.
  - `Ladder`: 층내 높이를 허용한 원정지의 엔진 규칙이며 **계단과 다른 것이다** — 한 번에 여러 단을
    오르고, **명시적 링크로만** 통과한다
    (`GridPathfinder`의 걷기 인접 규칙에서 빠져 있다. 사다리 칸 위에 걸어 올라서는 것은 그대로 된다).
    첫 입력은 사다리 발판까지 이동해 부착하는 데서 멈추고, 해당 타일에서 **두 번째 자기 탭/Space**로
    오르내린다. 비주얼 길이는 실제 단차까지만.
    캐치워크가 있는 층은 바닥(+0) → 캐치워크(+2)를 **곧장** 잇고 중간 발판(+1)은 링크에서 뺀다.
    **몬스터는 `MonsterArchetype.CanClimb`가 true인 종만 오른다**(기본값 false — 새 아키타입이
    조용히 전부 오르면 이 축이 죽는다). 인간형(점거군 돌격병·기업 보안 사수)만 오르고 기계
    (기업 진압 로봇·기업 추적 드론·합선 검사 드론)는 못 오른다 — **실루엣과 일치**시켜
    배우지 않아도 읽힌다. 일반 추격뿐 아니라 **원거리 사격 자리를 다시 잡는 경로도**
    같은 `CanClimb` 값을 전달하므로 캐치워크가 드론에 대한 피난처가 된다.
    층 전환 계단 링크는 어느 쪽 끝도 사다리가 아니라 이 제한에 걸리지 않는다
    (안 그러면 못 오르는 적이 자기 층에 갇힌다).
  - `StairsUp/Down`: 입구를 밟는 즉시 반대편 링크까지 한 행동으로 처리하는 던전 층 전환.
  - `Hole`: 유일하게 위·아래 국소 시야와 낙하를 허용하는 실제 개구부. **점이 아니라 개구부다** —
    앵커 한 칸에서 한 칸씩 자라 seed마다 **2~3칸**이며 상한이 3이다(자리가 없으면 1칸으로 끝난다).
    `DungeonFloorInfo.HoleTiles`가 1급이고 `Hole`은 대표 칸으로 남는다(샤프트 연출·엘리베이터
    충돌처럼 한 점이면 충분한 곳용). 집합 판정이 필요한 두 곳만 목록을 쓴다 — 2층 관통 금지와
    약한 바닥(개구부 **둘레**). 약한 바닥은 밟으면 개구부가 되므로 둘이 같은 판정 함수를 쓴다.
    현재 층의 보이는 Hole은 포인터 hover에서 착지 창과 청록 충돌점 마커를 미리 강조하고, 첫 탭에서
    목적 층·후퇴/지름길/위험·장비 반영 예상 피해를 호박색 armed 상태로 고정한다. 이 첫 탭은 이동/턴을
    쓰지 않으며 같은 Hole 재클릭 또는 Space에서만 접근 후 낙하한다. 다른 입력/Escape는 취소한다.
  - PLAY에서는 현재 층만 기본 표시하며 다른 층은 Hole 국소 미리보기 외에는 숨긴다.
- **FOV/전투 정보**: Unknown/Explored/Visible 3상태. `MappedSilhouette`는 네 번째 FOV 상태가 아니라
  현재 활성 층의 별도 지도 지식이다. Unknown인 일반 방·복도·문 윤곽도 공용 저대비 범주로 표시하고
  클릭해 자동 이동할 수 있지만, 실제 타일 종류·재질·원소 상태·적·아이템·프롭은 숨긴다. 미공개
  비밀문 좌표와 비밀방 footprint는 조사/폭발 공개 전까지 mapped 집합에서 빠져 평범한 외곽 경계에 묻힌다.
  미확인 층 전환 계단은 공용 바닥처럼 보이지만 실제 FOV로 정체를 확인하기 전 자동 경로가 밟지 않는다.
  일반 닫힌 문은 mapped 이동 경로에서 문 앞 접근 → 열기 1턴 → FOV 갱신 → 적 턴 → 인터럽트 평가 →
  경로 재계산으로 통과하며, 피해·새 적·새 아이템이 생기면 즉시 멈춘다. 문 직후 새로 보인 적이 자기 턴에
  시야를 벗어나도 발견 사건은 보존한다. 사다리 링크는 자동 통과하지 않고 발판에서 자기 탭/Space를
  요구한다. 비활성 층은 기존 Hole 국소
  미리보기 외에 mapped 표시·입력이 없다. PC 기준 화면은
  `docs/captures/mapped-silhouette-pc-2026-08-04.png`다.
  시야 밖 적의 피해·사망 UI는
  공개하지 않으며, 시체는 기본 3턴 뒤 월드와 탭 대상에서 제거한다.
  **개구부 너머 미리보기도 진짜 FOV다** — 반대편 층에서 `GridVisibility.Compute`를 한 번 더
  돌린 결과를 쓴다. 예전의 착지점 중심 정사각 박스는 차폐를 아예 보지 않아 벽 뒤와 닫힌 문 뒤
  방까지 드러났다(같은 코드가 지키는 불변식과 정면으로 충돌했다). 반경 상한(1~6, 기본 4,
  씬 직렬화 값도 4)은 남긴다 — 이제 "박스 크기"가 아니라 **FOV 사거리**이며, 플레이어 시야(6)보다
  짧게 둬서 개구부 너머는 "엿보는" 정보로 남는다.
  미리보기 FOV 안의 다른 층 적은 중립색 몸체만 반투명 표시하고 HP·상태·인지 정보는 숨긴다. 명시적으로 겨눈
  층간 폭발은 실루엣 피격 플래시와 맞은 수만 확인시키되 피해 수치·상태 결과는 공개하지 않는다. AI는
  같은 층 활성 규칙을 유지하며 아이템도 숨긴다. passive 타일/적은 읽기 전용이다. PC `▲/◆/▼` 층 보기로
  실제 개구부가 보이는 인접 층만 무턴 포커스할 수 있고, 이때도 active floor는 불변이며 이동·대기·상호작용은
  잠긴다. HUD는 현재/보기 층을 동시에 쓰고 잠긴 행동 버튼도 비활성화한다. focus는 passive FOV의 교집합만
  밝히므로 벽 뒤 정보는 추가로 열리지 않는다.
  시야선·수직 개구부 투시·근접 도달 기하·FOV 컬럼 해석의 SSOT는 모두 `SightRules`다
  (`CombatRules`·`GridVisibility`는 위임). 수직은 실제 개구부만 통과하고, 컬럼은 span으로 봐서
  지면과 머리 위 구조물(캐치워크)이 함께 잡힌다. 첫 던전은 캐치워크를 생성하지 않지만 이 규칙은
  다른 원정지를 위한 엔진 계약으로 유지한다.
- **투척 조준**: 폭발물·냉각재 수류탄·연료통은 조준에 들어가면 현재 FOV 안에서 실제로 투척 가능한
  타일을 아이템 색의 낮은 알파 바닥 마커로 표시한다. 투척 볼트는 사거리·시야선이 성립하는
  보이는 적 칸만 표시한다. 표시는 `BombRules.ForEachThrowTarget`/`CombatRules`와 실제 판정을
  공유하므로 벽 뒤·사거리 밖·Unknown 타일을 정보로 누설하지 않는다. 2560×1440 PC HUD와
  폭탄 조준을 함께 승인한 실화면 근거는 `docs/captures/throw-range-hud-qhd.png`다.
  층 보기 중에는 `VerticalThrowRules`의 실제 Hole 경로·양쪽 LoS·경로 비용을 통과한 선택 층 바닥만
  표시하며, 미리보기와 확정이 같은 `VerticalThrowPath`를 쓴다. 폭발물·냉각재·기름만 가능하고 투척 볼트와
  일반 원거리 공격은 다른 층에 닿지 않는다. 투사체는 입구→반대편 endpoint→목표로 꺾여 날아간다.
  양방향 PC 실화면은 `docs/captures/vertical-look-{down,up}-throw.png`다. 둘 다 active floor·턴 불변 상태에서
  현재/보기 층 표기·잠긴 행동 레일·실제 유효 타일을 함께 보인다.
- **첫 던전/보스**: 첫 목적지는 **폐 아케이드 복합타워(상승, `B2 → … → 8F` + 옥상 출구)** 10개 층 단일 던전이다
  (GDD §10.1). **생성기가 방향을 읽으므로 표시와 구조가 일치한다** — 층 인덱스가 0에서 +9로 올라가고
  진출 계단이 `StairsUp`이며 진행 최종 층(+9)은 공간 최하단(0)과 다르다.
  코드 ID `forgotten-catacombs`·seed·층 수는 유지한다.
  최상층의 `감시자`를
  처치하기 전에는 최종 구역 출구가 붉게 봉인되고, 처치 후 청록 해금 연출과 전용 HUD가 갱신된다.
  아레나에는 생성기가 고른 제단이 서고(처치 후 신호색이 식음), **아레나 바로 앞 층**
  (진행 지수 = 층수−2, 엘리베이터 탑승구와 같은 층 — 층 이름은 아래 엘리베이터 항목에만 둔다)에
  들어서면 접근 전조를 한 판에 한 번 알린다(`DungeonBossArenaRules`).
  **전조 문구는 방향을 탄다** — 상승 "천장 너머가 낮게 울린다 — 한 층 위에서", 하강 "바닥이 낮게
  울린다 — 한 층 아래에서", 진입 깊이 "울림이 벽을 타고 번진다 — 다음 구역에서".
  예전엔 "한 층 아래"로 고정이라 상승 던전에서 거짓말이었다 — `FallMeaningHint`와 같은 규약으로,
  규칙은 방향을 모르지만 **안내 문구는 반드시 탄다**.
  최종 구역 도착만으로 승리하지 않으며 출구 모달의 `던전 정복`을 선택해야 정산·런 종료가 확정된다.
  체크포인트는 `dungeonId/stageCount/bossDefeated`를 보존한다.
- **진행 방향 / 진행 지수 (v0.3.2)**:
  - **진행 방향은 던전별 데이터다**(`DungeonDirectionRules`) — 하강 / 상승 / **진입 깊이**(고도가
    진행 축이 아닌 던전, `1구역` 식 표기) 셋이 공존하며 **전역 스위치가 아니다**.
    폐 아케이드 복합타워=상승, 침수된 금고=진입 깊이. 잿불 성채는 미정(기본값 하강).
    **생성기가 이 값을 매개변수로 받는다** — `DungeonGenerator.Generate(..., direction)`.
    "출구"를 찾을 때는 `floor.DownStairs`가 아니라 `DungeonLayout.OnwardStairOf(floor)`를 쓴다.
  - **낙하 배치는 진행이 아니라 공간 순서다.** 구멍은 방향과 무관하게 아래로 떨어지므로 생성기가
    층을 `FloorIndex` 내림차순(위 → 아래)으로 순회한다. 상승 던전에서는 진행 순서와 반대다.
    보스 아레나에는 구멍을 두지 않는다(하강에서는 공간 최하단이라 자동, 상승에서는 명시 조건).
  - **"도달 층"은 진행 지수로 판정한다** — `RunSummary.FurthestProgressIndex`와
    `RunTelemetry.deepestProgressIndex` 둘 다. 예전의 층 인덱스 최솟값은 상승 던전에서
    영원히 시작 층을 가리켰다. 세이브도 `deepestProgressIndex`를 함께 담아
    이어하기가 도달 층을 되돌리지 않는다. 현상금 `DeepestDepth`도 진행 지수를 읽는다
    (부호 뒤집기 역산이면 상승 던전에서 의뢰가 영원히 미완이었다).
  - **방향 중립 문구**: 의뢰·게임오버·출구 버튼에서 "깊이/최심층/더 깊이"를 걷었다 —
    첫 던전이 위로 올라가므로 거짓말이 된다. 최종 출구의 고정 `▼`도 상승 던전에서 반대를
    가리켜 제거했다. 구간 이름은 초반/중반/후반/보스를 쓴다. 중간 탈출구의
    `더 나아가면` 문구와 PC 가로 모달 레이아웃은
    `docs/captures/pc-mid-extraction-direction-neutral-2026-07-31.png`로 확인했다.
  - **텔레메트리 층 라벨은 당시 값으로 동결한다(v6).** 새 리포트는 최초 진입 때 보인
    `B2`/`8F`/`N구역`을 저장해 카탈로그 변경 뒤에도 과거 표기가 바뀌지 않는다. 라벨 없는
    구 리포트는 현재 `DungeonCatalog`로 한 번 해석해 필드에 물질화하고, 사라진 ID는 방향 중립
    `N구역`으로 동결한다. v1~v4는 당시 `-floorIndex`로 진행 지수를 먼저 복원하고, 버전만 v6으로
    찍힌 초기 빈 라벨도 같은 경로로 수선한다. 새 층 진입은 실제 화면 라벨을 전달하므로 2단계의
    `B2`가 누적 진행 지수 때문에 `9F`로 굳지 않는다.
  - **미래 버전 저장은 원문을 보존한다.** 현재 빌드보다 새로운 런 루트·중첩 텔레메트리는
    이어하기와 체크포인트 덮어쓰기를 막고, 미래 메타 저장도 알려진 값만 읽되 다시 쓰지 않는다.
    주 파일과 백업을 함께 검사하며, 미래 메타에서는 허브 변경·출정도 막는다. 런 도중 미래 메타가
    나타나도 정산 저장이 성공하기 전에는 인벤토리와 체크포인트를 지우지 않는다. `JsonUtility`가
    모르는 필드를 조용히 버리는 하향 실행 손상과 보상 소실/반입품 복제를 파일·씬 회귀로 고정했다.
  - **종료 정산은 `runId` 영수증으로 한 번만 반영한다(세이브 v3).** 전리품·소모품·장비
    반환/소실·의뢰·기록·해금과 `RunSettlementEntry`를 한 메타 저장에 넣고, 성공한 뒤에만
    런 체크포인트를 지운다. 저장 직후 앱이 종료되어 구 체크포인트가 남아도 재개 정산은 영수증을
    먼저 찾아 보상을 다시 주지 않으며, 이런 잔여 파일은 일반 `이어하기`에도 노출하지 않는다.
    크래시 창 PlayMode 회귀는 장비·전리품·의뢰·기록을 함께 검증한다.
  - **진행 지수 ≠ 고도.** 난이도·구간 판정(적 혼합·휴식처·탈출구·장비 드랍·숨은 방·밴드·보스)은
    `DungeonFloorInfo.ProgressIndex`만 쓰고 **elevation 으로 역산하지 않는다**.
    `DungeonDepthBandRules.ForFloor`는 이 결함(`Max(0, -floorIndex)`) 때문에 삭제됐다.
  - **공간 ≠ 진행.** `StairsUp/Down`은 공간 이름이라 고정이고 "다음 층으로 가는 계단"만 방향을 탄다
    (`OnwardStair`/`BackStair`). 같은 이유로 `FinalFloorIndex`(진행 최종)와
    `BottomFloorIndex`(공간 최하단)는 다른 값이다 — 하강 던전에서만 우연히 같다.
  - **던전 출구는 타일 종류로 판정하지 않는다.** "진행 최종 층의 링크 없는 진출 계단"이며
    판정은 `IsDungeonExitTile` 하나다. 종류(`StairsDown`)로 분기하면 상승 던전에서 출구를 밟아도
    아무 일이 없다 — 실제로 그랬고, 스모크가 치트 훅만 검증해 놓쳤다.
    지금은 스모크가 `InteractAdjacent()`(SPACE 경로)까지 검증한다.
  - **중력은 방향을 타지 않는다.** `FallRules`·`SightRules`는 던전 방향을 모른다.
    다만 **낙하의 의미**는 방향을 탄다(`FallMeaningFor`) — 하강=지름길, 상승=후퇴로,
    진입깊이=지형 위험. 안내 문구는 `FallMeaningHint` 하나에서만 나온다.
  - **엘리베이터**(`ElevatorShaftRules`)는 **던전당 한 대**이고 보스를 잡아 건물 전원이
    들어온 뒤에만 움직인다. 탑승구는 보스 아레나 바로 앞 층(폐 아케이드 복합타워 7F), 도착은 B1.
    **고도가 진행 축인 던전에만 놓인다**(`AppliesTo`/`AppliesToDungeon`) — `Inward`인
    침수된 금고에는 없다. 층을 관통하는 승강 연출이 `1구역` 표기와 어긋나기 때문이다.
    **복귀 전용·한 방향**이라 진행의 반대로만 간다(상승=아래, 하강=위).
    생성기는 설비 타일만 놓고 **링크를 만들지 않는다** — 링크가 곧 "움직인다"이며
    `GridPathfinder`가 링크를 따라가므로 전원 전에 링크가 있으면 즉시 지름길이 된다.
    전원은 `PowerElevatorIfUnlocked`가 보스 처치·이어하기 복원 양쪽에서 넣는다.
    낙하가 아니라 탑승이므로 낙뎀 곡선은 건드리지 않았다 — 3층 자유낙하는 12 피해로
    원정자 HP(10)를 넘어 "뛰어내려 빠르게 하강"이 성립하지 않는다.
  - **지상 진입(B1 → 1F)은 한 판에 한 번 알린다**(`CrossesIntoAboveGround`).
    상승 구조가 공짜로 주는 전환점이라 여기서 짚으면 건물을 타고 오른다는 구조가 읽힌다.
- **지역(원정지) 정체성**: 콘텐츠 변주 표가 **(지역 × 깊이)** 두 축이다 —
  `DungeonBandProfiles.ForDepth(region, depth)`. 깊이가 기울기를 주고 지역이 기준선을 옮긴다.
  `Facility`(폐 아케이드 복합타워 — 기준선) · `Flooded`(침수된 금고: 웅덩이↑·광원↓·사수↓) ·
  `Ember`(잿불 성채: 웅덩이↓·밀도↑·광원↑). 던전 → 지역은 `DungeonDefinition.Region` →
  `DungeonLayout.Region`으로 실려가 생성기·런타임 스폰·광원이 같은 출처를 본다.
  - **지역은 혼합·밀도·무대 확률·드롭 표를 가른다.** 일반 아키타입 스탯·행동 트리는 전 지역
    공용이지만, Flooded 중반부터는 지역 전용 원거리 적 `ArcDrone`이 합류한다. 명중점 주변과
    이어진 웅덩이를 `ShockRules`로 통전시키며, 같은 물 위의 다른 적도 피해를 받아 역이용할 수 있다.
  - **월드 아이템 드롭 편성은 `DungeonLootRules`가 지역별로 가른다** — 다만 **모든 지역이 같은
    23칸 롤을 소비**해서 지역을 바꿔도 생성기 RNG 스트림이 흔들리지 않는다(호출은
    `DungeonGenerator.Placement`). 침수된 금고는 연료통을 빼고 그 자리를 냉기 도구에 넘긴다 —
    기름이 물 위에서 지역 반응을 흐리기 때문이다.
  - **지역 인자에 기본값을 두지 않는다**(옛 `ForFloor` 역산 붕괴의 재발 방지).
  - **폐 아케이드 복합타워 출력은 골든 지문으로 고정한다**(`DungeonGeneratorGoldenTests`) — 세이브가
    seed 재생성이라 조용한 배치 변화는 불변식 테스트만으로 잡히지 않는다. 층내 평탄화로 상승 10층
    지문을 `1df3a0399ab01947`로 의도적으로 갱신했다. 변경 범위는 raised row·층내 계단/사다리·캐치워크
    제거와 보스 제단의 base elevation 이동이며, 방 계획의 RNG 소비·층간 링크·Hole 규칙은 유지한다.
    비평탄 생성 회귀는 `02411906bef8b09f`(일반 하강 3층)와 `5548343b47f0621a`(Flooded 상승 10층)로
    별도 고정한다. 기존 체크포인트는 좌표가 아니라 seed·진행 층을 저장하므로 무효화하지 않고,
    기록된 층 입구에서 새 평탄 지형을 재생성해 이어간다.
  - **침수된 금고 개방**: `Flooded`는 `isAvailable: true`이며 허브 선택 → 10개 구역 →
    보스 없는 최종 구역 출구 완주 경로를 PlayMode 스모크로 고정했다. 중반부터 `ArcDrone`과
    웅덩이·빙결·감전 무대가 함께 나타나며, 2026-07-26 플레이 캡처에서 지역 신호색과
    적 실루엣이 한 화면에서 구분되는 것을 확인했다. `Ember`만 아직 비활성이고,
    기름 타일 무대는 없다(기름은 아이템뿐 — 게다가 침수된 금고에서는 그 아이템조차 일반 드롭
    표에서 빠진다. 숨은 방 보상 표는 별개다).
- **전투 표현**: `CombatPresentationRules`가 물리/화염/냉기/강타를 분리한다. Gameplay는
  근접 돌진·스쿼시/플래시·픽셀 버스트·감쇠 카메라 흔들림을 적용한다. 화상은 주황 불꽃 고리,
  빙결은 청록 결정 고리이며 부여/연장/상쇄를 구분한다. 적 FX도 반드시 FOV를 따른다.
- **플레이테스트 계측**: `RunTelemetry`가 층별 시간·턴·피해·처치·아이템(획득/사용/조합)·휴식·
  숨은 방·낙하·상태/원소 반응을 체크포인트와 함께 누적하고, `RefreshBands()`가 이를 진행 구간
  (초반 1~3번째 / 중반 4~6번째 / 후반 7~9번째 / 보스 10번째+)으로 롤업한다 — 구간 값은 **파생**이라
  따로 기록하지 않는다. 화면·리포트 라벨은 `DungeonDepthBandRules.BandLabel`/`RangeLabel`이 만든다 —
  예전의 `B1~B3` 표기는 던전이 상승·평면일 수 있으므로 화면에 거짓이 된다. 열거자 이름
  (Shallow/Mid/Deep/Boss)은 과거 리포트 호환 때문에 JSON 키로 그대로 남는다.
  개발 디버그 창에서 구간 비교/수동 저장하며, 판 종료 시
  `development-profile/telemetry`에 JSON 리포트를 자동 확정한다.
- **숨은 방**: 보스 아레나를 뺀 **1~9번째 층**(진행 지수 0 ~ 층수−2) 중 seed로 고른 3개 층에
  `SecretDoor` 막다른 방이 생긴다. **아직 구출하지 않은 NPC가 갇힌 층은 후보에서 빠지고**
  (이유는 아래 「시설은 구출로 열린다」에 있다) 구출한 뒤에는 후보로 돌아온다. 공개 전에는
  벽처럼 이동·FOV를 막고, 인접 균열의 `수상한 벽 조사` 또는 폭발로 `SecretPassage`가 된다.
  `SecretRoomRules`와 `DungeonFloorInfo.SecretDoor/SecretReward`를 우회해 별도 판정을 만들지 않는다.
- **아트 방향(전환 중)**: **v0.3.3(2026-07-30) 사이버펑크 피벗 확정** — 테마가
  **아포칼립스 + 사이버펑크**로 승격되고("SF 얇게" 조항 폐기), 첫 던전이 폐병원 →
  **폐 아케이드 복합타워**로 재설정됐다(GDD §10.1 — 구조·생성 규칙·골든 지문 불변, 정체성만 교체).
  액터는 스트리트웨어 계약 v2(`...actor-appeal-restyle-v1.md`), 1순위 레퍼런스는
  `reference/ref-cyberpunk-*` 5장. **표시 문자열은 전환 완료** — 던전(폐 아케이드 복합타워)·
  입장 서사·허브/메인메뉴 UXML·장비 4종(빔 랜스/임팩트 렌치/전광판 방패/서스펜션 부츠,
  리스킨 표 §4-b)이 코드에 반영됐고 전체 회귀 그린. 환경 네온 전환(M5)의 **정의·잔여
  범위의 SSOT는 `docs/ROADMAP.md` 「아트」의 「환경 네온 전환(M5)」 항목이다.**
  - **M5 1차 슬라이스 반영됨(2026-07-30)** — 공용 환경 6셀(바닥·좌우 벽·문 2종·계단)이
    네온 아케이드 v3 소스(`environment-neon-style-v1` 레시피, C03 채택)로 재마감돼
    `Art/Environment` 16종이 교체됐다. 원정자도 치비 라이더(`actor-knight.aseprite` 자동
    조립 초안)로 교체 완료. 캡처: `docs/captures/neon-environment-live-v1.png` ·
    `rider-ingame-live-v1.png`.
  - **원정자 정식 디자인 채택·초안 교체(2026-07-31)** — 필드 메딕 생존자(블론드+주황 팁
    포니테일)를 정식 컨셉으로 채택하고 `actor-knight.aseprite`를 라이더 초안 → **메딕 자동
    조립 초안**(11프레임/6태그, 4방향 기본 C01 + 액션 키포즈 9종, seed 2130163433 계보)으로
    교체했다. 팔레트에 `hair-blonde-1/2` append(절도 검사 통과). 채택 근거·게이트 기록은
    액터 계약 §4-c와 `reference/ref-cyberpunk-05-expeditioner-medic-concept.prompt.md` 소유.
    Unity 임포트·카탈로그 연결은 완료됐지만, 프레임 사이 해부·실루엣이 깨져 당시 멀티프레임 재생은
    승인하지 않았고 플레이어를 카탈로그 첫 Sprite `Frame_0`에 정지시켰다. 임시 런타임 오버레이
    `PlayerCyberAccent`도 제거했으며,
    흉부 트리아지 화면·비대칭 의료 리그는 승인 프레임 자체에 흡수해야 한다.
  - **원정자 접지 정적 v1(2026-08-02)** — 위 자동 조립 초안은 현재 런타임 원본이 아니다.
    승인 컨셉·실화면 카메라·픽셀 재질 레퍼런스를 함께 사용한 built-in ImageGen 소스를
    `process_actor_knight_grounded_v1.py`로 `96×128` 단일 프레임에 마감하고 정식 Aseprite로
    교체했다. 플레이어에도 적과 같은 local light를 적용하고 접촉 그림자를 작은 AO로 줄였다.
    틸 상시 발판과 앰버 대상 링은 열린 코너 틱으로 교체해 발·AO·바닥 재질을 가리지 않는다.
    피벗·배율·격자·이동·공격·FOV는 불변이며 이 단계에서는 애니메이션 승인 게이트를 닫아 두었다.
  - **원정자 4방향 타임라인 승인(2026-08-03)** — `idle 4 / walk 3 / attack 3 / hit 3 /
    fall 2 / death 5`를 방향마다 제작해 태그 프레임 80개와 태그 밖 승인 `Frame_0`을 한 원본에
    조립했다. east/west는 전 상태 바이트 미러, attack/hit은 방향별 키포즈, fall/death는 같은
    canonical 체형을 접어 부피를 보존한다. `SurvivorAnimationApproved=true`로 게이트를 열고
    Unity 카탈로그에 24클립을 베이크했다. 전체 제작 시트는
    `docs/captures/actor-knight-directional-conform-preview-v1.png`, PC 실화면은
    `docs/captures/actor-directional-{walk,attack,hit,fall}-runtime-pc-2026-08-03.png`다. 짧은 `fall`
    클립은 끝 프레임을 잡아 두고 0.48초 월드 낙하가 착지한 뒤 idle로 복귀하므로 공중 idle 팝이 없다.
  - **적군 6종 4방향 타임라인 — 정체성·팔레트 2차 승인(2026-08-04 작업 트리)** —
    점거군 돌격병(`goblin`)·기업 진압 로봇(`skeleton`)·기업 추적 드론(`slime`)·기업 사수(`slinger`)·
    합선 검사 드론(`arcDrone`)·사이버사이코 감시자(`graveWarden`)의 정식 Aseprite는 기존
    `81프레임·24태그`, `idle/walk`만 루프, 동서 화면 미러·별도 후면 계약을 유지하도록 재작업했다.
    `Slime` 얼굴은 낮은 사족 보안 드론으로 교체해 쐐기형 센서 헤드·노출 서보 척추·손상된
    군중제압제 카트리지의 제압제 주입턱을 갖는다. 전 적군은 차콜·건메탈을 주재료로, 녹·갈색은
    마모로만 제한하고 국소 시안/마젠타와 작은 적색 IFF를 쓴다. 새 전수 시트·동작 GIF는
    `docs/captures/arcade-enemy-directional-{conform-preview-v1.png,motion-preview-v1.gif}`이며,
    실제 Unity 카탈로그의 여섯 정적 프레임은
    `docs/captures/enemy-cyberpunk-runtime-catalog-pc-2026-08-04.png`에서 다시 확인했다.
  - **적군 테마 누출 경로 폐쇄(2026-08-04)** — 정식 카탈로그가 비거나 슬롯 연결이 끊겼을 때
    노출되던 녹색 고블린·해골 검사·눈 달린 슬라임·투석끈 사수 절차 폴백을 각각 점거군 병사·기업
    진압 로봇·기업 추적 드론·카빈 사수로 교체했다. 감시자 폴백도 궤도 로봇에서 등반 가능한
    인간형 사이버사이코 외골격으로 맞췄고, 미등록 ID는 다른 적 얼굴 대신 중립 보안 드론을 쓴다.
    범용 PNG 생성기 두 곳의 구 적 출력도 제거해 재실행 덮어쓰기를 막았다. 실제 카탈로그의 6개
    `Frame_0`·24클립 연결과 표시명 금지어는 자동 테스트가 검사한다. 이번 재작업 뒤 Core shim
    1203/1203, ArtPipeline 312/312, Telemetry 18/18, Unity EditMode 1496/1496, PlayMode 24/24를
    통과했고 스크립트 컴파일 오류는 0건이었다.
  - **M5 2차 슬라이스 반영됨(2026-07-30)** — hospital* 드레싱 9슬롯(바닥 그레이트/균열/
    서비스 + 상승 벽 3종×좌우)이 `environment-neon-dressing-v1` 레시피(C03 채택)로
    아케이드 어휘(자판기·꺼진 홀로 패널·상태 패널, 바닥은 균열선+전단지)로 교체됐다 —
    코드 슬롯명·출력 파일명 계약은 구판 유지. 채택은 §1-d 게이트(후보 바닥 3×3 + 라이더
    합성, `docs/captures/arcade-dressing-gate-v1.png`)를 거쳤고, 마감 프로세서
    (`process_hospital_dressing_v1.py`)에 despeckle 패스를 추가했다.
    - **정합 패스(같은 날, 현재 계약 교정)** — 첫 반영본에서 드레싱 바닥이 기본 바닥
      (V≈0.40)보다 밝아 체커보드 얼룩이 떠 바닥 오버레이 명도만 ×0.92로 낮췄다. 시험했던
      전역 `WARM_GAIN`은 네온 시설의 쿨 grey-* 몸통까지 웜 브라운으로 밀어 제거했다.
      현재 `process_hospital_dressing_v1.py`는 벽 몸통의 쿨 그레이와 시안/마젠타 악센트를
      보존하고, 바닥만 명도 정합한다(회귀 테스트 포함).
  - **M5 3차 슬라이스 반영됨(2026-07-30)** — 기존 소품 4종(모닥불 드럼·폭발 배럴·
    포탈·은닉처)을 `arcade-props-neon-v1` 레시피(C04 채택, 라이더 합성 게이트)로
    §1-d 플랫 클러스터 문법으로 재마감했다. 실루엣·역할색(모닥불 토치 골드 = 허브 웜
    디오라마 조항, 포탈 틸, 배럴 hazard+해골 데칼) 보존, `process_postapoc_props_v2.py`
    소스 교체 + despeckle 추가로 `Art/Runtime/prop-*.png` 4종 교체. 생성 배경이
    불투명하게 나와 승격 시 플러드 알파 전처리를 거쳤다(소스 prompt.md 참조).
    캡처: 던전 `docs/captures/arcade-dressing-live-v3.png` ·
    허브 `docs/captures/arcade-props-hub-live-v1.png`.
  - **B2 시작방 배치 정합(2026-08-01)** — 폭발통은 단순 장식이 아니라 밀기·낙하·화염
    연쇄를 잇는 위험 프롭으로 유지하되, 접근해서 일반 바닥으로 밀 수 있는 벽/외곽 후보를
    우선하고 액터와 같은 시각 스케일을 적용했다. 주차 범퍼와 쓰러진 안내판은 기본 바닥에
    합성한 비충돌 완성형 타일로 추가했으며, 입구·계단·주요 동선·적·아이템·폭발통 주변을
    예약해 결정론적으로 배치한다. 두 드레싱은 각각 `view-0..3` 네 방향 슬롯을 가지며 완전
    세트일 때 현재 시점의 90도 회전 수로 선택한다. 부분 세트는 기존 무방향 슬롯을 전 시점에
    쓰고, 그것도 없으면 같은 화면축 parity → 첫 존재 슬롯 순으로 내려가 사라지지 않는다.
    캡처: `docs/captures/b2-dressing-placement-v1.png`,
    `docs/captures/b2-aseprite-axis-q0-v1.png`, `docs/captures/b2-aseprite-axis-q1-v1.png`.
    - **히어로 룸 군집 패스(2026-08-02)** — 위 요소를 개별 해시로 흩뿌리지 않고
      `B2HeroRoomLayoutRules`의 한 계획으로 묶었다. 적용 범위는 첫 던전 진행 지수 0뿐이며,
      다른 던전의 첫 구역에는 B2 주차장 아트가 새지 않는다. 현행 6×5 시작방과 지형은 그대로 두고,
      닫힌 문을 열고 지나갈 실제 Entry→진출 계단 경로를 clear spine으로 예약한다. 폭발통은 왼쪽
      service/grate 군집, 범퍼·쓰러진 안내판은 오른쪽 진출 측 군집에 두며 중앙은 기본 바닥으로
      비운다. 시작방 벽도 시점 해시를 빼고, 복도로 열린 +Y 면이 아니라 실제 외곽인 입구 뒤 -Y 벽의
      고정 비상등 양옆 두 칸만 서비스 패널로 묶는다. 목표 시안은
      `docs/art-direction/project-c-b2-hero-room-target-v2.png`, PC 가로 실화면은
      `docs/captures/b2-hero-room-layout-q0-v1.png`·`docs/captures/b2-hero-room-layout-q1-v1.png`다.
      두 회전에서 같은 물리 벽 군집과 같은 좌우 기능 구역이 유지된다. map 타일·링크·RNG는 수정하지
      않아 생성 골든과 체크포인트 형상은 불변이다.
    - **B2 연속 서비스 벽 승격(2026-08-02)** — 두 개의 큰 발광 패널과 중앙 generic 등잔을
      나란히 놓던 임시 조합을, `env-wall-b2-service-segment-{0,1,2}-rising-{right,left}`
      여섯 Aseprite 원본으로 교체했다. 방향별 세 장은 `192×176` master에서 한 번에 팔레트
      잠금·despeckle한 뒤 잘라 상부 캡·케이블 트레이·하부 배관이 경계를 공유한다. 카탈로그는
      여섯 슬롯 완전 세트에서만 켜고, 누락 시 세 칸 전체를 기존 벽으로 폴백한다. 중앙 `(3,0)`의
      고정 sconce 판정은 실제 앰버 광원을 위해 유지하되 generic torch 애니메이션과 대형 네온
      오버레이/바닥광은 전용 벽 위에 얹지 않는다. 실화면은
      `docs/captures/b2-service-wall-q0-live-v1.png`·`b2-service-wall-q1-live-v1.png`, 반대편
      누출 검사는 `b2-service-wall-q2-ghost-check-v1.png`다. 지형·이동·FOV 좌표는 불변이다.
    - **B2 배럴 유출 방지 베이 승격(2026-08-02)** — 기존 베이지 service/grate 두 셀을
      `env-floor-b2-barrel-bay-{service,drain}-view-{0,1,2,3}` 여덟 Aseprite 원본으로 교체했다.
      각 시점의 두 셀은 `192×96` master에서 공용 바닥 합성·팔레트 잠금·despeckle을 먼저 거친 뒤
      `128×64` 셀로 잘라 호스와 녹 배수 띠가 경계에서 이어진다. 여덟 슬롯 완전 세트에서만 켜며,
      배럴은 밀기·폭발 가능한 별도 프롭으로 남고 지형·충돌·이동·공격·FOV는 불변이다. 인접 고정
      sconce는 실제 앰버 광원을 유지하되 벽 강조와 generic idle 애니메이션을 낮추고, 코너에서는
      service→drain 축의 바깥 벽 한 면에만 패널을 그려 중복을 막는다. 실화면은
      `docs/captures/b2-barrel-bay-q{0,1,2,3}-live-v1.png`다.
    - **B2 오른쪽 진출 드레싱 v3(2026-08-02)** — 주차 범퍼·쓰러진 안내판을 최종 캔버스에서
      직접 만든 4시점 Aseprite로 다시 마감했다. 범퍼는 낮은 검정 고무/강철과 앰버 끝단,
      안내판은 바닥에 누운 비대칭 파손 판재다. `process_b2_parking_dressing_v3.py`는 1~2px
      클러스터·hard alpha·공용 팔레트를 고정하며 두 역할만 `Signal` 톤매핑을 탄다. 배치는
      카탈로그 순서가 아니라 named role로 `(5,2)` 범퍼·`(5,1)` 안내판·`(5,3)` cracked를 고정하고,
      벽 매립 단말 아래 `(5,0)`은 비운다. 지형·충돌·이동·공격·FOV는 불변이다. conform 비교는
      `docs/captures/b2-right-dressing-conform-preview-v3.png`, 최종 4시점은
      `docs/captures/b2-prop-quality-q{0,1,2,3}-live-v3.png`다.
    - **B2 진출 균열 바닥 v1(2026-08-02)** — `(5,1)`이 전역 구판
      `env-floor-cracked`의 밝고 두꺼운 파손판처럼 보이던 문제를 전용
      `env-floor-b2-cracked` Aseprite로 교체했다. 공용 바닥 실루엣 위에 작은 박리·가는 균열·
      극소량 녹만 남기고 측면·구멍·커버·앰버 신호를 제거했다. 방향 seam이 없는 평면 손상이라
      단일 슬롯을 네 시점에 공유하고, 슬롯 누락 시에만 전역 cracked로 폴백한다. 배치 좌표와
      지형·충돌·이동·공격·FOV는 불변이며 실화면은
      `docs/captures/b2-cracked-floor-q{0,1,2,3}-live-v1.png`다.
    - **B2 2×2 연속 바닥 Macro v1(2026-08-02)** — 시작방의 일반 바닥 30장이 각각 다른 명암과
      중앙 무늬를 가진 체크무늬처럼 읽히는 문제를 줄이기 위해, 기본 seed 1977의 깨끗한
      `(3,1)·(4,1)·(3,2)·(4,2)` 네 셀에 하나의 연속 마모 덩어리를 배치했다. top-down master를
      시점별로 먼저 회전·투영하고 마지막에 네 물리 역할로 잘라 만든
      `env-floor-b2-macro-role-{0..3}-view-{0..3}` 16개 Aseprite가 모두 있을 때만 켠다. clean
      2×2 블록이 없거나 슬롯 하나라도 빠지면 네 셀 전부 일반 바닥으로 원자 폴백한다. 기존
      clear spine·특수 드레싱·지형·높이·이동·공격·FOV·정렬은 바꾸지 않았다. conform 비교와
      q0..q3 실화면은 `docs/captures/b2-macro-floor-conform-preview-v1.png` 및
      `docs/captures/b2-macro-floor-q{0,1,2,3}-live-v1.png`다.
    - **B2 방 단위 바닥 조명 v1(2026-08-02)** — 매크로 밖에 남은 큰 회색/갈색 체크무늬는
      바닥 원본이 아니라 `TileLightColor`가 플레이어·sconce·방향 그림자를 셀마다 단색으로 곱한
      결과였다. `dungeonDarkness`만 끈 진단본
      `docs/captures/b2-floor-light-diagnostic-darkness-off-v1.png`에서 동일 원본 바닥이 한 면으로
      읽히는 것으로 분리 확인했다. 이제 현재 보이는 B2 시작방 Floor들의 local RGB 평균을 방 기준광으로
      삼고, 바닥 렌더러에만 local 차이를 20% 남겨 합성한다. 방 평균 밝기는 보존하면서 큰 셀 간 명암
      분산만 1/5로 줄인다. 벽·적 액터·아이템·배럴은 기존 국소광을 유지하고, FOV 알파·wet/oiled 색·
      이동/공격 오버레이·비B2 바닥·Core 조명 규칙은 불변이다. 4시점 실화면은
      `docs/captures/b2-floor-light-coherence-q{0,1,2,3}-live-v1.png`다.
    - **B2 바닥 foundation 접지 v1(2026-08-02)** — 셀별 윗면과 분리된 순수 프레젠테이션 루트가
      현재 화면의 실제 전면 두 면에만 `64×42`·PPU 64·pivot y=26 face-only 스프라이트를 붙인다.
      10 logical px fascia는 이웃 셀 사이에서 이어지고 이음매는 드물게만 나타난다. 월드의 같은 볼록
      모서리에 고정되는 `12×38` 지지대도 화면 앞쪽에 보이는 것만 남긴다. 면/지지대는
      `Dungeon Backdrop` sorting order 1/2라 Default 바닥 뒤, 안개 백드롭 앞에 있으며 별도
      collider·입력·격자·FOV·전투 상태를 만들지 않는다. 4시점 PC 실화면은
      `docs/captures/foundation-grounding-q{0,1,2,3}-live-v2.png`다.
    - **B2 배경 프롭 품질 수직 슬라이스 v2(2026-08-02)** — 승인 q0 방향판을 입력으로 만든
      built-in ImageGen 제작 원화 `project-c-b2-prop-production-sheet-v2.png`를 직접 잘라 쓰지 않고,
      `process_b2_prop_quality_v4.py`가 당시 64×112 벽 14종과 128×128 원통형 연료 셀을 최종 캔버스에서
      재구성했다. `promote_b2_prop_quality_v2.sh`가 런타임 PNG와 정식 Aseprite를 함께 승격하며,
      구 canonical writer들도 v4 생성기로 위임되어 재생성 때 구 아트로 되돌아가지 않는다.
      뒤쪽 봉인 단말·좌측 서비스 스파인·오른쪽 낮은 군집을 물리 좌표에 고정했고, 반대 두 벽에는
      비기능 저채도 설비 패널만 하나씩 둔다. B2 안에서는 HUD 안전영역에 방+가장 가까운 실제 문을
      맞추는 4시점 카메라를 쓰며 수직 보기 우선순위는 유지한다. 정식 원본 64/64 검증과 PC 4시점
      화면 승인을 마쳤다: `docs/captures/b2-prop-quality-q{0,1,2,3}-live-v3.png`.
    - **B2 벽체 연속성·접합 리듬 교정(2026-08-03 작업 트리)** — 제작 원화의 독립 패널 알파를 벽 외곽으로
      쓰던 탓에 64×32px 인접 투영 간격에서 셀마다 검은 틈이 생기던 문제를 고쳤다. v4 writer가
      모든 벽의 RGB 디테일은 보존하되 알파를 하나의 64×112 아이소 벽면 계약으로 고정하고, 비어 있던
      가장자리는 구조 셸 재료로 연장한다. 서비스 세 칸은 192×176 master에서 공용 상단 캡·하부
      kick plate를 적용한 뒤 다시 분할하며, 테스트가 양 seam 78행 이상 접촉·단일 연결요소·중앙
      foot datum y=96..98을 고정한다. 런타임 벽 피벗도 타일 중심→바깥 중심의 0.5 지점에 놓아 바닥
      실제 경계와 일치시켰다. 후속 RGB 마감은 원화의 독립 카드용 전고 밝은 end-cap을 방향별 12px
      결합면에서 공통 저채도 접합주로 바꾸고, face-relative 2px cap과 어두운 연속 plinth로 셀 반복과
      밝은 점선형 발선을 줄였다. 중앙 호스·벤트·단말, exact alpha, 피벗·PPU·서비스 master는 유지한다.
      게임 규칙·격자·정렬·FOV는 불변이다. 2026-08-03 작업 트리에서 ArtPipeline 276/276,
      Core shim 1127/1127, Unity EditMode 1367/1367, PlayMode 11/11, Aseprite 64/64 및 콘솔 에러 0을
      확인했다. 최종 4시점은 `docs/captures/b2-wall-joinery-q{0,1,2,3}-live-v5.png`다.
    - **B2 기본 벽 저주파 재질 변주 v1(2026-08-03 작업 트리)** — built-in ImageGen 소스와 생성 기록을
      `project-c-b2-wall-material-source-v1.{png,prompt.md}`에 보관하고, `process_b2_prop_quality_v4.py`가
      조용한 기본 셸과 작은 수리판이 있는 비발광 유지보수 셸 두 종으로 결정론적 마감한다. 둘은 같은
      64×112 알파·12px 결합면·상단 cap·하단 plinth를 유지하며, 생성 보드의 대각 균열 후보는
      비밀문·파괴 가능 벽 신호로 오독될 수 있어 승격하지 않았다. 기존 `env-wall-window-rising-*`
      legacy 슬롯은 B2에서 창이나 광원이 아닌 두 번째 유지보수 재질로 재사용한다.
      `B2HeroRoomLayoutRules`는 서비스 세그먼트·설비·단말을 우선한 뒤 남은 물리 벽 bay의 홀짝을
      월드 좌표로 고정해 기본/유지보수 재질을 고른다. seed와 카메라 회전에 독립적이며 벽 배치·충돌·
      정렬·FOV·이동·공격 규칙은 불변이다. 구 v1/32×56 생성 진입점도 현재 v4 writer로 위임해
      재생성 회귀를 막았다. 4시점 실기 승인본은
      `docs/captures/b2-wall-material-q{0,1,2,3}-live-v1.png`다. Core shim 1127/1127,
      ArtPipeline 279/279, Telemetry 18/18, Unity EditMode 1367/1367, PlayMode 11/11,
      Aseprite 64/64 및 최종 콘솔 에러 0을 확인했다.
    - **공용 바닥 재료 품질 v1(2026-08-02)** — built-in ImageGen의
      `project-c-shared-floor-material-source-v1.{png,prompt.md}`에서 저주파 마모 배치만 취하고,
      `process_shared_floor_material_v1.py`가 기존 128×64·4,098px hard-alpha 다이아와 공용 팔레트,
      외곽 3px 중간톤을 결정론적으로 다시 만들었다. 원본 가시 픽셀은 `grey-4` 91.996%와 세 개의
      넓은 마모 덩어리 `grey-3` 8.004%이며, 두 값 모두 런타임 `.28-.50` 구간의 `Stone` 한 역할로
      매핑돼 Shadow/Light/Outline 얼룩을 타일마다 찍지 않는다. 정식 `env-floor.aseprite`와 PNG
      폴백은 가시 픽셀이 같고, `floor`는 Aseprite, `raisedFloor/lowerFloor`는 전용 원본 전까지 같은
      PNG를 가리킨다. 새 base를 굽는 밴드·Facility·B2 파생 바닥도 함께 재생성했다. 지형·피벗·
      정렬·이동·공격·FOV는 불변이며 4시점 실화면은
      `docs/captures/shared-floor-material-q{0,1,2,3}-live-v1.png`다.
  - **M5 4차 슬라이스 반영됨(2026-07-30)** — 하행 계단 특수 소스를
    `environment-neon-stairs-v1`(C04 채택)로 교체해 `env-stairs-down-rising-*` 2종
    재마감 — 마지막 병원판 환경 소스 소거. 판정은 최종 크기 드라이런 비교
    (`docs/captures/arcade-stairs-conform-v1.png`)로 했고, 피트 내부 틸 광 후보는
    Hole=틸 신호 예약과 충돌해 기각했다(안전 계단=앰버 유지, §1-c).
    잔여는 메인 메뉴 배경 재발주뿐이다(SSOT: ROADMAP M5 잔여 범위).
  - **M5 5차 슬라이스 반영됨(2026-07-30)** — 메인 메뉴 배경을 `ui-menu-backdrop-v2`
    레시피(C02 채택)의 네온 스카이라인 소스
    (`project-c-main-menu-backdrop-source-v2.png`)로 교체하고
    `process_ui_backdrops_v1.py`로 재마감(`ui-main-menu-backdrop.png`, 960×540 규격
    불변, off-palette 0). 4후보는 실제 conform 경로(480×270 팔레트 잠금)로 마감해
    비교했고, 타이틀·카피 자리가 사는 어두운 중앙 협곡 구도를 골랐다.
    실화면 `docs/captures/main-menu-backdrop-v2-live.png`.
    **마지막 구테마 유저 노출 자산이 소거돼 M5 재발주 범위가 닫혔다.**
  **v0.3.4(2026-08-01) 세계관 개정** — 미궁·이상(異常) 현상을 삭제하고 **세력 + 픽서 청부**로
  바꿨다(GDD §10). **월드 아트 자산은 영향 없다**(재료 어휘 불변) — 바뀐 것은 **틸의 근거**뿐이다.
  "이상 현상"이 아니라 **"열림·통과·냉각" 기능 신호**이고 예약 대상은 그대로다.
  - **UI 는 영향을 받았다 — Torchstone v1.8(2026-08-01).** 본문·프레임 계조가 판타지 시절 웜
    토프/크림이라 청흑 바탕 + 네온과 색온도가 부딪쳤다. 쿨로 옮겼다:
    `--pc-text` `#EADFC8`→`#DFE7F2` · `--pc-dim`→`#949BA1` · `--pc-stone`→`#545B61` ·
    `-lit`→`#DFE7F2` · `-dim`→`#2C3138`. 신규 색은 `.gpl` 의 **`ui-text-cool` 하나뿐**이고
    나머지는 기존 grey 램프다. 웜 `ui-text` 는 **남긴다** — 지우면 아직 그 값으로 구워진
    스프라이트가 off-palette 가 된다.
  - **네온 용도를 세 갈래로 갈랐다** — 시안 네온 = 월드 장식 / **마젠타(`--pc-ui-accent`) = UI 크롬** /
    틸 = 판정 신호. v1.7 의 "네온은 전부 장식 광원" 조항이 이 장르의 서명인 UI 크롬의 네온을 막아
    화면을 중성 회색으로 끌어내리고 있었다. 규칙 SSOT 는 `docs/UI_DESIGN_SYSTEM.md`「용도 세 갈래」.
  - **UI 스프라이트를 재생성했다** — `ui-*.png` 아이콘 9종 + 9-slice 프레임이 쿨로 넘어갔고
    (`ui-settings` 실측 웜 27 / 쿨 384), 신호색은 보존됐다(`ui-glow-frame` 골드 196px 유지).
    코너 브래킷 `ui-bracket-frame.png` 를 신설해 던전 HUD 의 `minimap-panel`·`message-log` 에 배선했다.
  - **Game View 확인 완료(2026-08-03)** — Field Deck 정상/발견/보스 3상태에서 브래킷 고정,
    네이티브 glyph, 패널 차폐와 액션 휠 무충돌을 승인했다. 근거와 최종 회귀 수치는 위 UI/해상도 절과
    ROADMAP 「UI v1.8 쿨 전환 + Field Deck v1」이 소유한다.
  이전 확정(유지): 테마를 **포스트 아포칼립스**로 전환(GDD §10 v0.3).
  - **밝기 완화 패스 적용됨(v0.3.3)** — 탐색 잔상·심층 앰비언트·높이차 틴트·수직 미리보기의
    어둡기 하한을 일괄 상향해 "판독 가능한 어둠"으로 옮겼다(FOV 3상태 구분은 유지). 값은
    `IsoPrototypeDemo`/`DisplaySettingsData`와 씬 직렬화가 소유하고, 사용자 저장값 덮어쓰기를
    끊기 위해 PlayerPrefs 키를 `-v2`로 올렸다. 캡처: `docs/captures/dungeon-brightness-relaxed-v1.png`.
  방향·레퍼런스 SSOT는 `docs/art-direction/project-c-postapoc-art-direction-v1.md`, 리스킨 표는
  `...postapoc-reskin-table-v1.md`. 팔레트 *원리*(청흑 바탕+국소 호박 광원+신호색 1개)는 유지하고
  재료 어휘(석재→콘크리트/벽돌/녹, 횃불→비상등/네온, 마법 포탈→고장 게이트/전력 누설)만 바꾼다.
  구현은 포스트아포 마감 자산(스타일 트랜스퍼 → 팔레트 잠금 PNG)으로 수렴했고, 2026-07에
  **128×64 타일 / PPU 128 레짐으로 상향**했다(`ui-*`만 64 유지, 절차 생성 폴백은 64-레짐인 채
  스프라이트별 PPU로 공존). 가독성 규칙·발주 순서는
  `docs/art-direction/project-c-art-improvement-plan-v2.md` 참조.
  - **적군 정체성 정적 로스터(2026-08-03 기준, 2026-08-04 재작업)** — 코드 ID·전투 스탯·혼합표는 보존하고
    일반 적 5종과 보스의 96×128 남향 정적 자산을 모두 교체했다. 점거군 돌격병·기업 보안 사수·
    기업 진압 로봇·기업 추적 드론·합선 검사 드론과 `감시자`(사이버사이코 집행관)가 각자 전용
    런타임 스프라이트로 카탈로그에 연결된다. 인간형 감시자의 `CanClimb`만 실루엣 계약에 맞춰
    true로 바꿨지만 현 보스 아레나에는 사다리가 없다. 2026-08-04 정체성·팔레트 재작업은 정적 기준과
    방향 타임라인에 반영됐고 같은 날짜의 새 PC 런타임 카탈로그 캡처로 재승인했다.
  - 적도 생성 시 `SpriteClipAnimator`를 붙인다. Unity `Animator`가 `null`인 것은 의도된 구조이며,
    클립 없는 PNG/절차 폴백은 같은 재생기에서 정지 1프레임 no-op으로 동작한다.
    허브 웜 디오라마 패스는 유지 — 허브는
  `docs/art-direction/project-c-warm-diorama-hub-target-v1.png`
  기준으로 횃불에 데워진 석재 + 토치 골드 모닥불/횃불 + 틸 포탈을 사용한다.
  `IsoPrototypeDemo`의 허브 바닥/전면 두께/장식 벽/로컬 광원만 분기하며, 던전 카탈로그와
  FOV·상태 색은 건드리지 않는다. 광원 타일과 허브 소품은 시점 회전 때 같은 GridPos로 다시 투영한다.
- **던전 공통 톤**: 모든 깊이는 `project-c-torchstone.gpl` 마스터 팔레트와
  `ProjectCEnvironmentCatalog`의 런타임 역할색을 공유한다. 팔레트는 **재료별 램프**로 묶인다
  (v2, 2026-07) — 재료마다 4~6단, 그림자는 청보라·하이라이트는 앰버로 트는 구조다.
  잠금은 값 기준 최근접이라 **인덱스 순서에 의존하지 않는다**(재배치 후 EditMode 전체 통과 확인).
  색 목록과 설계 규칙의 SSOT는 `.gpl` 파일 자신이다 — 헤더 주석이 규칙을 소유한다.
  기본 환경은 청흑 void와
  횃불에 데워진 웜 그레이·토프 석재, 물리 광원은 토치 골드, 마법/출구는 틸로 읽히게 한다.
  깊이별 변주는 이 공통 톤 위에서만 제한적으로 적용한다. 층내 높이를 쓰는 다른 원정지는
  `LocalHeight`를 색상 테마가 아니라 명도와 전면 두께로 구분한다. 첫 던전의 깊이 변주는
  **밴드 스프라이트/드레싱과 광원 밀도**로 만든다. 구조(캐치워크 길이)는 비평탄 던전에서만 사용한다.
  **밴드 바닥 6종이 도착했다(2026-07-30, 플랜 v2 배치 1-1)** — `env-floor-{mid,deep,boss}(-raised)`가
  `environment-band-floors-v1` 레시피(2라운드 C04 채택)로 마감돼 카탈로그 밴드 슬롯 6개에
  연결됐고, 절차 오버레이(`BandOverlayColor`) 임시 대행은 자동 비활성됐다
  (`BandFloorFallsBackToShared` — 오버레이 코드는 폴백 방어선으로 남는다). 어휘는
  mid 냉각수 얼룩 / deep 균열+철근+물때 / boss hazard 조각+틸 심 하나. conform이
  웜 가드·non-boss 틸 억제·명도 게이트(§1-c)를 강제한다 — 판정·마감 근거는
  `docs/captures/band-floors-{gate,conform}-v1.png` · 소스 prompt.md.
  `-raised` 3종은 첫 던전에서는 비활성이며, 층내 높이를 허용한 원정지용 교체 자산으로 보존한다.
  `DungeonSurfaceFor`의 석재색은 모든 깊이에서 같아야 한다(테스트로 고정). 값은 `DungeonBandProfile`.
- **Aseprite 파이프라인**: `com.unity.2d.aseprite 5.0.3`을 사용한다.
  최종 아트 SSOT는 `Assets/_Project/Art/Source/Aseprite`의 `.aseprite`/`.ase` 원본이다.
  `ProjectCAsepritePipeline`이 Point/PPU 128/Canvas Pivot/무압축/AnimationClip을 강제하고
  정식 파일명의 첫 프레임을 공용 `ProjectCEnvironmentCatalog`에 자동 연결한다. 이 폴더에
  원본을 저장하면 Unity의 전처리에서 임포트 규격을 적용하고, 후처리 지연 콜백에서 카탈로그와
  애니메이션 세트를 동기화하므로 평소 PNG export나 수동 재연결은 필요 없다. 바닥은 128×64
  중앙 피봇, 액터는 96×128 캔버스를 검증하며 `env-floor*`/`env-wall-*`은 런타임 톤매핑을 위해
  Read/Write를 자동 활성화한다. 초기 `e0c0967`(2026-08-01)의 28개 기준에서 확장되어,
  2026-08-02 작업 트리의 `Validate Sources`는 Aseprite 원본 **61개 / 카탈로그 슬롯 61개**를
  통과했다. 2026-08-03 작업 트리는 원정자 방향 원본과 신규 액터 소스를 포함한 63개 원본을
  다시 통과했고 Unity 콘솔 경고·오류는 0건이었다. 2026-08-04 기업 추적 드론·전 적군 팔레트 재작업 뒤
  `Reimport and Sync Catalog`와 `Validate Sources`를 다시 실행해 **69개 원본 / 69개 슬롯 / 7개 애니메이션
  세트**를 확인했다. GUID 고정 EditMode 계약을 포함한 전체 Unity 회귀 뒤 콘솔 오류도 0건이었다.
  나아가 임포터가 만든 AnimationClip에서 **sprite 커브만** 뽑아 태그 클립으로 굽는다. 임포터가
  authored 프레임 뒤에 덧붙이는 마지막 유지 키는 베이크 프레임에서 제거하되 `clip.length`는
  보존한다. 액터는 `catalog.actorAnimations`, 환경 루프는 `catalog.environmentAnimations`에 싣는다 —
  transform/color 커브는 의도적으로 버린다(position·scale은 전투 FX, 안정 상태 color는
  `ApplyPlayerVisuals`/`ApplyEnemyVisuals`가 소유하므로 여기서 건드리면 둘이 싸운다). 원샷 태그
  (attack/hit/fall/death)가 루프로 임포트되거나, 태그 규약 밖 클립이 있거나, 태그 클립이 있는데
  idle이 없으면 파이프라인 검증에서 걸린다. 환경은 `prop-campfire`, `prop-portal`,
  좌·우 상승 벽 횃불 4슬롯의 `idle` 태그만 베이크하며, 태그가 없으면 첫 프레임 정적 폴백을
  유지한다. 허브·휴식 지점·가시 벽 소품은 같은 `SpriteClipAnimator` 계약으로 자동 재생한다.
  로컬 제작 쪽은 ComfyUI `127.0.0.1:8188` REST → YAML 레시피/SQLite 큐 → 샷별
  Aseprite conform → Lua 타임라인·1×/8× GIF 초안 → Slack Socket Mode 리뷰로 연결돼 있다.
  모든 실행 그래프는 `NAME.workflow.json`(편집 SSOT) + `NAME.api.json`(실행본) 쌍으로
  보존되고 `comfy_batch.py validate`가 계약을 검사한다. 발주는 `_runs/<prompt-id>/`에
  그래프·이벤트·진행을 남기고 생성 PNG에 캔버스를 심는다.
  **발주 비용의 지배 요인은 체크포인트 재로드다** — 배치 드라이버들이 서로를 모른 채 같은
  큐에 교대 발주하면 매 장 6.9GB를 다시 읽어 장당 221초가 381초가 된다(`e0c0967`, 2026-08-01
  실측 n=21 vs 37). `execute_prompt`가 `CheckpointLease`로 큐를 체크포인트 단위(기본 4잡)로
  점유해 막으며 드라이버는 수정할 게 없다. 아이템은 64px로 끝나므로 `item-static-v2`가
  768/20으로 발주해 2.1배 빠르다(213초 → 99.7초). 메모리 확보와 `--lowvram`은 둘 다 효과가
  없었고 후자는 오히려 느렸다 — 실측 표와 측정 함정은 `art-direction/comfyui/README.md`
  §7-a-1이 소유한다.
  Slack 생성 폼은 기본 화풍·세계관을 자동 선택하고 새 작업에서는 승인 소스 없는 컨셉 방법만
  보여준다. 승인 후보 카드의 `다음 단계 생성`은 대상·화풍·세계관·후보 ID와 권장 후속 방법을
  자동 계승한다. 기본 화면은 대상·이번 내용·결과 다양성만 받고 모델·전체 프롬프트·seed·Steps·
  CFG·denoise는 `고급 설정`에서만 편집한다. 메인 원정자 `actor-knight`, 정적 환경 9슬롯,
  환경 idle 루프 4슬롯이 이 합성 레지스트리에 등록돼 있다.
  2026-07-28 vertical slice와 2026-07-31의 11프레임 메딕 자동 조립은 생성·판정 이력으로만
  남는다(`docs/art-direction/player-character-vertical-slice.md`). 현재 `knight` 카탈로그 슬롯은
  승인 접지 `Frame_0`과 그 정체성을 보존한 4방향 6상태 `actor-knight.aseprite`를 보며,
  `actorAnimations`는 플레이어용 24개 방향 클립을 제공한다.
  `style-sampler` 수동 배치는 액터 콘셉트·런타임 액터·환경을 한 장씩, 이펙트와 애니 키포즈는
  실행마다 다음 shot 한 장씩 큐에 넣는다. 채택 후보는 승인 스냅샷으로 보관되며, 별도
  `apply_requests`만 Codex Spark Scheduled가 실제 Unity/Aseprite 참조를 조사해 적용한다.
  봇은 `com.project-c.art-review` launchd 서비스로 상시 실행하며, 최종 보간·발 기준선·실루엣과
  정식 슬롯 승격은 여전히 사람의 명시적 채택과 게임 반영 요청 뒤에만 수행한다.
  `Art/Runtime` PNG는 원본이 없는 슬롯의 폴백이며 최종본으로 직접 수정하지 않는다.
  2026-07-28 임시 통합 패스에서는 환경→UI→액터 순서로 각 단계를 Unity 캡처 승인한 뒤 다음
  프로세서를 실행했다. 메인 메뉴 배경, UI 아이콘/9-slice/행동 프레임, 환경·액터·소품·아이템
  폴백을 모두 공용 `.gpl`에 다시 잠갔고, 결과는
  `docs/captures/integrated-art-pass-{main-menu,hub,dungeon}.png`에 보관한다.
  후속 hero-room 패스는 기준 이미지+기존 환경 시트를 참조한 built-in ImageGen 소스
  `project-c-hospital-dressing-source-v1.png`를 `process_hospital_dressing_v1.py`로 conform하고,
  `docs/captures/hospital-hero-room-art-quality-v1.png`에서 카메라·조명·드레싱을 함께 승인했다.
  아이템 fallback도 같은 단계 게이트를 따른다. `docs/art-direction/item-sources-v3/`의 단일
  오브젝트 소스 12장은 현재 **ComfyUI `item-static-v1` 10종 + 2026-08-03 built-in ImageGen 2종**이다.
  자연물 버섯/마법 결정처럼 읽히던 `item-herb`와 `item-frost-shard`를 지혈 패치와 구리 루프형 냉각
  코일로 교체했으며 enum 숫자·레거시 파일 슬롯·조합 규칙은 유지한다. 과거 1차 신구 비교본은
  `docs/captures/item-arcade-reskin-v1.png`, 포션 단독 게이트 근거는
  `docs/captures/item-potion-comfy-gate-v2.png`다. `process_items_v3.py`는 두 소스 계열을
  64×64/하드 알파/공용 팔레트/아이템별
  피벗 여백으로 마감하며,
  `IsoVisualCatalog`와 인벤토리 USS의 기존 12슬롯 파일명을 그대로 교체한다. 구 액션 UI 9종은
  32×32로 남아 Mobile 배치가 사용하고, PC Field Deck은 별도 `ui-field-*.png` 12×12 9종을 쓴다.
  기존 아이템 Unity 실화면 근거는 `docs/captures/item-ui-integrated-{hud,inventory}-v3.png`다.
  새 지혈 패치/냉각 코일 런타임 PNG는 시각·파이프라인 계약을 확인했고, Field Deck 실화면 근거는
  `docs/captures/ui-field-deck-pc-qhd-2026-08-03.png`와
  `docs/captures/ui-field-deck-{notice-wheel,boss-wheel}-pc-qhd-2026-08-03.png`다.
- **액터 애니메이션**: 공식 태그 6종(idle/walk/attack/hit/fall/death)은 Core `SpriteClipTags`
  하나가 소유한다 — 베이크(에디터)와 재생 트리거(게임플레이)가 같은 상수를 봐야 문자열이 갈라져
  클립이 조용히 무시되는 일이 없다. 시간 → 프레임 선택 수학은 `SpriteClipRules.FrameAt`이며
  UnityEngine 무의존이라 shim에서 경계까지 검증된다.
  - 재생기 `SpriteClipAnimator`는 **Animator를 쓰지 않고 `renderer.sprite`만 만진다.**
    position·scale은 전투 FX가, 안정 상태 color는 `ApplyPlayerVisuals`/`ApplyEnemyVisuals`가
    소유하므로 침범하지 않는다.
  - 방향 태그는 `idle-north`·`attack-west`처럼 `상태-화면방향`이다. 월드 방향은
    `ActorFacingRules`가, 카메라 회전에 따른 화면 방향은 `IsoPrototypeDemo.ActorPresentation`이
    소유한다. 방향 클립을 우선하고 기존 무방향 태그로 폴백하며, 방향 태그가 하나라도 있는 정식
    원본은 6상태×4방향 완전 세트여야 검증을 통과한다.
  - 시야 밖(`renderer.enabled == false`)에서는 얼어붙었다가 다시 보이면 그 프레임부터 이어간다 —
    그래서 재동기화 코드가 없고, 안 보이는 액터의 Update가 이른 반환으로 끝난다.
  - 클립 없는 태그 요청은 전부 no-op이라 PNG 폴백(정지 1프레임) 액터와 그대로 공존한다.
    현재 재생 트리거는 walk·attack·hit·fall·death에 붙어 있다. 프레임 클립이 없는 적 정적 액터도
    근접 방향 돌진+베기 궤적, 원거리 총구 섬광+반동, 피격 원점 반대쪽 반동+플래시·스쿼시·방향 버스트를
    최소 표시 프레임으로 재생한다. 칼·폭탄·냉각탄·기름병은 실제 아이템 실루엣 투사체를 사용한다.
    PC QHD 근거는 `docs/captures/directional-{melee,ranged,hit}-pc-2026-08-03.png`다.
  - 적 6종은 모두 전용 방향 세트를 사용한다. 이동·공격·피격은 현재 화면 facing의 클립과 기존
    방향성 근접 궤적/총구 섬광/충격 버스트를 함께 재생한다. 적 낙하 피해는 `ShowEnemyHit`의 피해·
    버스트는 유지하되 `hit` 클립만 억제한다. walk는 클립 한 주기를 한 칸 이동 시간에 정규화해 짧은
    적 이동(기본 0.135초)에서도 세 키포즈가 모두 보인다. 보이는 적 낙하는 0.22초 위치·배율·알파 하강을
    먼저 재생해 목적지 층이 FOV 밖이어도 같은 프레임에 idle/death로 덮이지 않는다. 생존 착지에서 idle로
    돌아가며 사망이면 하강 뒤 death 마지막 프레임이 우선한다. 실제 PC QHD 근거는
    `docs/captures/enemy-directional-{walk,attack-fx,hit-fx,fall-death}-runtime-pc-v1.png`다. 2D Aseprite
    Importer 5.0.x의 stale rect/UV 재사용으로 새 atlas와 옛 Sprite 경계가 어긋나던 문제는 actor
    재임포트 때 이전 atlas 크기를 한 번 무효화해 고쳤다. Sprite ID/GUID는 유지되며 대표 방향
    프레임의 rect-알파 tightness를 EditMode에서 회귀 검사한다.
  - 플레이어는 승인 게이트의 예외 경로다. 현재 정식 방향 타임라인의 전수 프레임·PC 화면 승인이
    끝나 `SurvivorAnimationApproved=true`이며 재생기를 붙인다. 태그 밖 `Frame_0`은 허브·카탈로그
    정적 소비자를 위해 보존한다. 적 재생 경로에는 이 게이트가 적용되지 않는다.
- **배고픔/중간 생환**: `HungerRules`가 포만→배고픔(경고)→굶주림(주기적 HP 감소)을 소유한다.
  주기가 짧아(가득 찬 배 100턴) 중간중간 통조림을 먹는 리듬이며, 판 전체를 관통하고 모닥불로는
  배가 차지 않는다. `ExtractionRules`의 비상 탈출구는 **4번째·8번째 층**(진행 지수 3·7 —
  `ExtractionDepths`. 폐 아케이드 복합타워에서는 2F·6F다) 두 곳뿐이고 최종 구역은 보스를 잡아야
  나간다. 비상 송출기(어디서든 생환)는 상점과 숨은 방에서 아주 가끔만 나온다 —
  정산은 모두 `ExtractRun` 하나로 모인다. 하드 타이머를 쓰지 않는 이유는
  파밍(기둥 4)을 죽이지 않기 위해서다.
  **HUD에서 배고픔은 위치가 아니라 활력이다** — `HungerLabel`/`HungerIsWarning`을 따로 내고
  vitals의 `hunger-label`이 받는다(평소 muted 회색, 경고 단계부터 색). `LocationLabel`은 위치만
  낸다("B2 · HEIGHT 0 · (1,1)"). 포만이 위치 줄에 붙어 있어 `depth-panel`을 넘치던 것을
  분류로 고쳤고 폭은 148px로 되돌렸다(넘침 안전망은 남긴다).
- **원정자(직업 없음)**: 영웅 3종을 걷어내고 `SurvivorProfile` 상수 하나로 대체했다
  (HP 10 · 근접 3 · 원거리 1 · 응급 키트 1 — 옛 기사 값 그대로라 기본 경로를 밟던 플레이어의
  밸런스가 안 바뀐다). **정체성은 캐릭터가 아니라 장비가 진다** — 옛 영웅은 숫자만 달라
  고르는 행위가 전술이 아니라 난이도 선택이었다. 캠프에서 영웅 프롭·선택/해금 모달이 사라졌고
  허브 상호작용에 `hero:` 라우팅이 없다(상인·창고·대장간·의뢰·기록실만 남는다).
  세이브·텔레메트리의 `heroId`/`unlockedHeroes`는 제거했고 **옛 세이브는 그 필드를 무시하고
  로드된다**(마이그레이션 없음). `IsoVisualCatalog.HeroFor` → `SurvivorSprite`이며 knight 슬롯을
  그대로 써서 씬 인스펙터 연결이 끊기지 않는다(ranger/alchemist 슬롯은 지역별 스킨용으로 남겼다).
- **장비**: 무기 1 + 보조 1 슬롯. **어떤 장비도 공격력을 올리지 않는다** — 사거리 2(빔 랜스),
  명중 넉백(임팩트 렌치), 피해 -1(전광판 방패), 안전 낙하 +2(서스펜션 부츠),
  원거리 사격 자체(아크 캐스터)처럼 규칙만 바꾼다.
  - **원거리는 충전형 2티어다(M4, 2026-07-31)** — 무제한 무료 원거리를 폐지하되
    **모두에게 내장 이미터**(사거리 3·충전 2·6턴당 1칸)를 쥐어 준다. `아크 캐스터`
    (무기 슬롯, 제작 145G)는 사거리 5·충전 4·4턴당 1칸으로 그 축을 깊게 만든다.
    근접 무기를 껴도 원거리는 기본형으로 남는다.
    - 처음엔 탄약(에너지 셀) 전용으로 만들었다가 갈아엎었다 — **탄이 떨어진 판은 원거리가
      통째로 사라져 그 축을 저울질할 수 없다**는 문제가 실제로 더 컸다. 셀은 이제
      "사격 횟수"가 아니라 **기다림을 사는 급속 충전재**(즉시 만충, 칸당 2회분)다.
    - 충전은 적 페이즈마다 턴으로 차고 판을 관통해 이월된다(`RunSaveData.rangedCharges`) —
      층마다 리셋되면 계단 앞에서 기다리는 게 최적해가 된다. 만충 중에는 회복 카운터를
      재워 사격 직후 공짜 재충전이 터지지 않게 했다. 체크포인트·던전 전환은 객체를 공유하지
      않고 스냅샷을 저장하며 남은 충전과 회복 턴을 함께 보존한다. 세이브 v2 마이그레이션은
      `JsonUtility`가 누락 중첩 필드를 0/0 객체로 만드는 동작은 원문 키 존재 여부로 구분한다.
      필드 없는 구세이브만 만충으로 복원하고, 실제 저장된 0충전/0회복 턴은 그대로 보존한다.
    - 판정·소비는 `RangedWeaponRules.TryFire` 하나가 원자적으로 하며 **명중 뒤에만
      충전을 깎는다**(빗나간 사격·접근 이동은 충전을 안 먹는다). 액션 휠은 `사격 n/N`으로
      남은 충전을 이고 있다. 모든 피해 경로가 `MonsterBrain.OnDamaged`를 불러 지각 밖
      저격도 추격을 유발한다(도주 중 개체는 예외). 투척 볼트는 이 게이트와 무관하다.
    - 수치는 전부 실플레이 전 임시 — **조정 축은 판당 사격 횟수 하나**다.
      SSOT는 `docs/SYSTEMS.md` 「원거리 사격」.
  대장간이 골드로 제작·장착을 관리하고(`ForgeRules`), 옛 영구 스탯 강화는 제거했다(GDD §11).
  **영웅 해금(200G)이 사라진 몫을 제작비로 옮겼다**: 4종 55/65/50/45 → 105/125/95/85
  (합계 215G → 410G). 골드가 남아돌면 "무엇을 걸고 나갈지"가 판돈이 아니게 된다.
  값은 임시 — 생환 밸런스 재조정 때 함께 본다.
  장착 장비는 백팩 공간을 쓰지 않으며 출정 준비 격자에도 나오지 않는다.
  **장비도 익스트랙션 규칙을 탄다** — 장착 = 반입이고, 죽거나 포기하면 잃는다.
  살아 나와야 창고로 돌아오며, 창고에 남긴 예비 장비만 안전하다.
- **아이템 정의**: `ItemCatalog`의 `ItemDefinition` 표가 분류·생환 가치·상점가·이름·짧은 라벨·설명·
  백팩 크기를 함께 소유한다. 새 `ItemKind`가 표에 없거나 중복되면 카탈로그 초기화에서 실패하고,
  미등록 값을 조회해도 기본 소모품/1×1로 조용히 흘려보내지 않는다. 장비 고유 행동과 제작가는
  중복하지 않고 `EquipmentCatalog`에서 가져온다. 저장 호환용 내부 ID `Herb=9`/`FrostShard=11`은
  그대로지만 화면 표시 계약은 각각 **지혈 패치/PATCH**와 **냉각 코일/COIL**이다.
- **백팩/창고**: 던전 백팩은 `BackpackRules` 6×4 멀티슬롯(1×1/1×2/2×2)이며
  `BackpackLayout` 자동 배치를 UI가 그대로 그린다. 공간 부족 시 월드 아이템은 남고,
  허브 창고는 종류별 중첩 저장을 유지한다. `ExpeditionLoadoutRules`가 창고와 출정 백팩 사이의
  이동·기본 지급품(이제 영웅별이 아니라 하나다)·초과분 복귀를 담당한다. 허브에서 선택한 물품만 던전 진입 시 반입하고
  나머지는 창고에 보존한다. 모바일은 선택 후 반대편 탭, PC는 버튼/드래그를 사용한다.
- **저장 안전성**: 런 체크포인트와 메타 창고 JSON은 `AtomicJsonStore`가 같은 디렉터리의
  `.tmp`에 먼저 쓰고 디스크 flush 뒤 교체한다. 기존 정상본은 `.bak`으로 남기며, 기본 파일이
  없거나 파싱되지 않으면 백업을 읽어 기본 파일까지 복원한다. 판 종료/초기화는 기본·백업·임시 파일을
  함께 지워 끝난 런이 되살아나지 않게 한다.
- **절차 생성 임시 아트는 `IsoPrototypeDemo` 밖에 있다**: `PrototypeSpriteCanvas`(프리미티브 +
  절차 생성 64-레짐 상수 SSOT — 카탈로그 자산은 128-레짐이며 스프라이트별 PPU로 공존),
  `PrototypeSpriteCache`, `PrototypePalette`(역할색),
  `PrototypeActorSprites`, `PrototypeEnvironmentSprites`. 이 클래스들은 **격자·던전·플레이어를
  참조하지 않으며**, 필요한 격자 사실은 호스트가 `TileVisualFacts`로 풀어 넘긴다.
  `IsoPrototypeDemo.Sprites.cs`는 그 변환만 하는 123줄 어댑터다 — 픽셀을 다시 이 파일로
  들이지 말 것. 그리기 코드를 손댈 때는 테스트가 아니라 **씬 렌더 지문**으로 확인한다
  (`docs/CODE_STRUCTURE.md` "절차 생성 임시 아트" 참조).
- **허브 월드 생성과 공유 상태도 실제 타입 경계를 얻었다**: `HubWorldPresenter`가 시설 개방 불변
  스냅샷과 주입된 씬/비주얼 의존성만 받아 프롭·광원을 만들고, 상호작용과 재투영 앵커를
  `HubWorldRegistry`에 함께 등록한다. Hub 파셜에는 `MetaStore → HubFacilitySnapshot` 변환과
  `ApplySurvivorStats`만 남는다. Registry는 공용 빌드 초기화에서 참조만 비우며, 오브젝트 수명은
  기존 `Generated Visuals` 루트가 계속 소유한다. Interaction/View 파셜은 조회·재투영만 요청한다.
- **해금 축 (진행 중)**: 도구 5종이 **조건 달성으로 열리고 다음 판부터** 드랍 풀에 들어온다
  (`ItemUnlockRules`). 계측은 `RunTelemetry` + `BountyMetric`을 재사용하며 새로 만들지 않았다.
  판정은 `FinishRunTelemetry` 한 곳이고 **사망에도 저장한다**(실패한 판도 전진).
  드랍 풀 게이트는 `DungeonMetaContext`로 넘기고 **롤 결과만 치환**해 RNG 스트림을 보존한다 —
  그래서 "전부 해금 = 게이트 없음"이고 지형·아이템 위치는 해금 상태와 무관하다.
  - **지켜야 할 제약 둘**: ① 조건은 `StarterReachableMetrics`(시작 풀로 달성 가능한 축)에서만
    고른다 — 빙결·기름·물을 쓰면 그 도구가 없어 영원히 못 여는 순환이다.
    ② **해금 안내를 의뢰로 주지 않는다** — 의뢰 게시판은 B단계에서 잠기는 시설이라 순환이 된다.
    안내는 판 종료 화면과 기록실이 맡는다. **둘은 같은 축으로 잰다** —
    "다음 목표"(`ItemUnlockRules.ClosestPending`)도 거리를 `RemainingFor` 하나로 재기 때문이다.
    예전에는 판 종료 안내만 이번 판 계측(`BountyRules.ReadMetric`)을 읽어서, 나쁜 판 뒤에는
    같은 조건을 두 화면이 다른 숫자로 말했고 기록실에 투입한 기록이 안내에 아예 안 잡혔다.
  - **판정은 「이번 판」이 아니라 「역대 최고 + 투입 기록」이다.** 조건이 한 판 기준이라
    화상을 11번 입히고 죽으면 그 판이 통째로 버려졌다 — "실패한 판도 전진"이 문서에만 있었다.
    한 판에 몰아치는 도전은 그대로 **빠른 길**로 남는다(최고 기록은 여전히 한 판의 값이다).
  - **기록(records)** — 죽음이 먹이는 축(`RunRecordRules`). 판이 끝날 때마다 적립되고
    기록실에서 조건에 투입한다(1 기록 = 조건 진행 1).
    `기록 = 도달 층 × 1 + 개척한 층 × 3 + 숨은 방 × 2`이며 **개척은 역대 최고를 넘은 층만** 센다
    (도달 층에만 비례하면 1~3층 왕복이 최적 파밍이 된다). 반복에도 0은 아니다.
    적립은 **정산과 무관하다** — 죽음만 보상하면 자살이 전략이 되고 생환만 보상하면 원래 문제다.
    **왜 물자가 아니라 기록인가**: 소지품 전손이 익스트랙션의 긴장이고 GDD §11이 영구 스탯 강화를
    금지하므로, 죽음이 남기는 것은 물자도 능력치도 아니어야 한다 — 늘어나는 것은 숫자가 아니라
    선택지다. 판 종료 화면이 "기록 +N"을 알린다(이 줄이 없으면 죽음이 "아무것도 못 건졌다"로 읽힌다).
    수치(1/3/2, 1 기록 = 진행 1)는 실플레이 전 임시값이다.
  - **기록실**(`HubLayout.Codex`, `hub-codex-modal`)이 조건·최고 기록을 보여주고 **기록을 투입한다**.
    항상 열려 있다. 진행값은 **최고 기록**(`MetaSaveData.unlockProgress`, 단조 증가)이다 —
    조건이 한 판 기준이라 지난 판 값을 쓰면 나쁜 판 뒤에 0으로 돌아가 안내가 죽는다.
    - 헤더가 보유 기록을 함께 낸다("해금 1/7 · 기록 7"). 진행 표시는 판정과 같은 식이라
      투입이 섞이면 "진행 12/12 (최고 10 + 기록 2)"로 **어떤 숫자가 움직일지 예측된다**.
    - 버튼은 **부족분과 보유량 중 작은 쪽**을 한 번에 넣는다(1개씩이면 목표 20에서 스무 번을
      누르고, 부족분보다 많이 받으면 기록을 버린다). 모자라도 실패시키지 않는다 —
      넣은 만큼 진행이 남는 것이 이 축의 요점이다. 충족되는 순간 그 자리에서 해금된다.
    - **판정은 Core 한 곳뿐이다**(`ItemUnlockRules.InvestRecords`/`RemainingFor`). UI가 자체 판정을
      들면 "기록실에선 열리는데 판 끝나면 안 열린다"가 생긴다 — 두 경로가 같은
      `RunRecordRules.IsConditionMet`을 쓴다는 것을 테스트로 고정했다.
      기록실을 열 때 메타를 다시 읽는다(던전에서 막 돌아온 직후엔 캐시가 낡았다).
  - **시설은 구출로 열린다**(`ShelterNpcRoster`): 대장장이→대장간, 연락책→의뢰 게시판.
    미구출 시설은 허브에 프롭도 상호작용도 없다. **장비 4종은 대장간에 종속**되며
    드랍 게이트는 롤을 그대로 소비하고 결과만 막는다. 구출은 즉시 저장한다(죽어도 남는다).
    갇힌 방은 **확률이 아니라 보장**이고 숨은 방과 겹치지 않는다 — 둘 중 하나라도 어기면
    시설이 영원히 안 열릴 수 있다(테스트로 고정).
    상인·창고·기록실은 잠그지 않는다.
  - **남은 것**: 실플레이로 조건 수치(화상 12·처치 20 등)와 구출 층(2·5) 조정.
    계획 전문은 `~/.claude/plans/calm-mapping-storm.md`.
- **최근 검증 기준** — **숫자에는 반드시 기준 커밋 해시를 함께 적는다.** 해시 없는 개수는
  언제 잰 것인지 알 수 없어 검증이 아니라 소문이 되고, 실제로 낡은 채 여러 세션을 살아남았다.
  - Core shim `./Tools/CoreTests/run-core-tests.sh` **1126/1126 통과** (`35c6316` +
    2026-08-02 작업 트리 실측).
  - ArtPipeline Python 회귀 **237/237 통과** (`35c6316` + 2026-08-02 작업 트리 실측).
    `COMFY_LEASE_CHUNK=0` 으로 돌린다 — 리스가 켜져 있으면 실제 발주가 쥔 락에 걸린다.
  - Telemetry Python 회귀 **18/18 통과** (`35c6316` + 2026-08-02 작업 트리 실측).
  - Unity EditMode `ProjectC.Tests.EditMode` **1362/1362 통과** (`35c6316` +
    2026-08-02 작업 트리 에디터 실측, 31.2s).
  - Unity PlayMode `ProjectC.Tests.PlayMode` **11/11 통과** (`35c6316` + 같은 작업 트리·세션,
    13.3s) —
    ① 허브 시설 생성·회전 재투영·상인 id/라벨 배선 → 폐 아케이드 복합타워 B2 → 8F 보스 →
    출구(치트 훅과 SPACE 경로 양쪽)
    ② 침수된 금고 1구역 → 보스 없는 최종 구역 출구
    ③ 원거리 충전/회복 턴 체크포인트·던전 전환 이월
    ④ v1 구세이브의 누락 원거리 충전 만충 복원
    ⑤ v1에 실제 저장된 0충전/0회복 턴 보존
    ⑥ 중간 탈출구 인접 접근 → 방향 중립 모달
    ⑦ 미래 메타가 새 원정 진입을 막고 원문을 보존
    ⑧ 런 도중 미래 메타가 나타난 정산은 인벤토리·체크포인트를 보존
    ⑨ 메타 정산 성공 뒤 체크포인트가 남은 크래시 재현 → 같은 `runId`의 전리품·반입 장비·
    완료 의뢰·기록을 재지급하지 않음
    ⑩ 같은 허브 인스턴스에서 시설 잠금→개방→재잠금을 재빌드해 최신 메타·프롭·상호작용·
    광원 수와 `Generated Visuals` 단일 루트를 보존
    ⑪ 아래 보기에서 일반 월드 행동을 막고 수직 폭발물만 한 턴을 소비한 뒤 현재층으로 복귀.
  - Aseprite `Validate Sources` **61개 원본 통과**, 최종 재임포트·검증 뒤 Unity 콘솔
    **에러 0** (`35c6316` + 2026-08-02 작업 트리 에디터 실측).
    128-레짐 전환(`4ab4438`) 뒤 재임포트·컴파일도 당시 세션에서 **에러 0**을 확인했다.
    최초 확인 기준은 `9d6a37e`였고, 그 전 세 커밋(`4ab4438`·`581445a`·`fa6ac29`)은 에디터
    검증 없이 shim만 돌았던 구간이다.
  - 그 앞 기준선은 `42266cc`의 EditMode 1043, 개구부 확장 `3744f04`의 Core 881 / EditMode 1005였다.
  변경 후에는 숫자를 맹신하지 말고, 최소한 shim을 돌리고 에디터 회귀도 다시 실행한다.
- **모달 렌더 검증 (2026-07-29 에디터 세션, `9e21b1c` + 작업 트리)** — 게임 메뉴·출구·결과 모달은
  **닫힘이 기본값이라 여태 한 번도 캡처된 적이 없었다.** 열어 놓고 찍자 결함 둘이 바로 나왔다.
  기준본은 `docs/captures/modal-{game-menu,exit,run-result}-2560x1440.png`.
  - **사인이 영문 토큰으로 떴다** — `RunSummary.FormatCause` 표에 `Starving`·`Poison`·`ArcShock`이
    없어 한국어 UI에 "사인: Starving"이 그대로 나왔다. 예외가 안 나서 테스트로도 안 잡히던 종류다.
    표를 채웠고 `FormatCause_LeavesNoEnglishTokenOnTheResultScreen`이 소스 전체를 순회해 못박는다.
  - **결과 모달의 세 줄만 왼쪽 정렬이었다** — `.gameover-card`가 `align-items: center`라 한 줄짜리
    라벨은 상자가 글자에 붙어 저절로 가운데로 보이고, 여러 줄이 붙는 순간(처치 + 기록 + 다음 해금)
    그 줄들만 어긋났다. `.gameover-detail`에 `-unity-text-align: middle-center`를 줬다.
  - 세 캡처 모두 이벤트 핸들러를 직접 불러 띄웠다. 특히 **출구 캡처는 보스가 살아 있는데도
    "봉인 해제된 출구 · 감시자 처치 완료"로 나온다** — 실제 흐름에서는 출구가 봉인돼 있어
    이 문구가 뜰 수 없다(핸들러가 `HasBoss`만 보고 `BossDefeated`는 안 보는데, 호출 게이트가
    그걸 대신 지킨다). 캡처가 검증하는 것은 배치이지 상태의 진위가 아니다.
  - **모달 스크림은 정상이다**(`rgba(5,7,12,0.82)`). 어두운 팔레트 때문에 눈으로는 안 걸리는데,
    캡처 픽셀 비교로 뒤 HUD가 약 45%로 어두워지는 것을 확인했다(하트 164 → 74).
  - ✅ **해소 — 출정 준비 모달이 세로로 넘치던 것**(같은 날 이어진 세션). 증상은 상세 패널(y 557)과
    이동 버튼(y 562·594)이 640×360 밖이라 버튼 경로가 도달 불가였던 것 — 캔버스가 960×540 →
    640×360으로 줄어든 뒤 이 모달을 재검증하지 않은 결과였다. 갱신된 기준본은 같은 경로
    `docs/captures/loadout-unit-transfer-2560x1440.png`이고, 지금은 헤더·격자·상세·버튼이 전부
    화면 안이다(측정: 카드 11.5→348.5, 버튼 280.5→308.5).
    - **카드는 처음부터 제대로 잘려 있었다.** `max-height`도 스크롤바도 정상이었다 —
      진짜 원인은 **상세 + 이동 버튼까지 본문 ScrollView 안에 있었던 것**이다(콘텐츠가 뷰포트의
      약 2.8배라 그 줄이 접힌 채로 잠들었다). 「짧은 화면」 정책이 헤더와 **주요 버튼**을 고정하라고
      말하는 이유가 이것 — 고정 대상을 빠뜨리면 정책을 지킨 것처럼 보이면서 그대로 사라진다.
    - 고친 방식: ① 상세·버튼 줄을 ScrollView 밖으로 빼 카드에 고정, ② 창고 격자(48칸 = 5열×10줄,
      440px)를 **격자 안에서** 스크롤(백팩 격자와 같은 160px 창) — 바깥 본문이 스크롤하면 드롭
      대상인 백팩이 화면에서 사라져 옮기기 자체가 불가능해진다, ③ 이동 버튼을 가로로 눕혀
      고정 줄을 64px → 28px로(회분 표기도 이제 안 잘린다), ④ 백팩 페인의 캡션을 헤더 한 줄로
      합쳐 격자 자리를 만들었다. 결과적으로 본문은 194 = 뷰포트 194로 **스크롤 없이** 들어간다.
    - **잠금 안내 상시 문장은 뺐다**(“기본 지급품은 잠금 표시되며 창고로 옮길 수 없습니다.”).
      두 줄 34px이 백팩 격자의 마지막 줄과 맞바꿔야 하는 값이었고, 같은 내용을 「기본」 배지와
      잠금 칸 선택 시 상세(“영웅 기본 지급”) + 비활성 버튼이 이미 말한다.
    - 세로를 짜내는 동안 **카드 단위 override 세 개가 조용히 무시됐다** — 프로필 규칙
      `.hud-root.is-short .settings-header` · `.hud-root.is-landscape … .inventory-section-header` ·
      `.hud-root.is-short .inventory-grid`가 전부 (0,3,0)이라 `.expedition-card …`(0,2,0)를 이긴다.
      짧은 화면 프로필이 PC의 기본값이 된 뒤로 **카드별 조정은 같은 급 이상으로 써야 한다.**
- **가림·조준 확인 (2026-07-29 에디터 세션, `9d6a37e` + 작업 트리)** — "표지가 화면을 가린다"와
  "폭탄 영향 범위가 안 보인다" 두 신고에서 출발했다. 기준본은
  `docs/captures/throw-blast-preview-2560x1440.png`(B2, 조준점 (0,3), 사거리 마커 17칸 +
  영향 범위 6칸 — 3×3 중 x=-1 열이 맵 밖이라 6칸이 맞다).
  - ✅ **영향 범위 미리보기를 들였다.** 사거리(성긴 점)와 3×3(촘촘한 해칭 + 진한 테두리)이
    스프라이트부터 갈리고, 포인터가 실제로 던질 수 있는 칸 위에 있을 때만 그린다.
    규칙은 `ThrowAimPreviewRules`(Core), 호버는 `IsoTapInput.trackHover`(조준 중에만 켠다).
  - ✅ **수직 표지가 액터를 삼키던 것 — 두 갈래로 고쳤다.** ① micro 정렬 슬롯을 +1(액터와 동률)
    에서 0으로 내려 같은 칸·같은 깊이에서 액터가 항상 이긴다, ② 표지가 플레이어보다 앞칸이라
    정렬상 정당하게 이기는 경우는 벽과 같은 가림 페이드에 넣었다(`fadePlayerOccluders` 토글을
    그대로 따른다). 층 전환 아치는 58×72px = 타일 두 장이 넘는 불투명 기둥이라 둘 다 필요하다.
  - ✅ **`sortingOrder` int16 뒤집힘 해소(2026-08-01).** 예전
    `(elevation·1000 + (x+y)·16)·8 + micro`는 e4에서 32767을 넘어 앞칸을 맨 뒤로
    보냈다. `IsoGrid` 하나에서 정수 깊이만 보존하도록 값을 1·39·5
    (`DepthResolution`·`ElevationSortBand`·`MicroResolution`)로 압축했다. elevation →
    회전된 (x+y) → micro(-2..+2) 우선순위는 그대로고, 현재 인스펙터 상한
    20층·층당 elevation 6·20×20 지원 범위의 4방향 전체가 int16 안에 든다.
  - ✅ **이동 중 목적지 정렬 선적용 해소(2026-08-01).** 예전에는 트윈을 시작하기 전에
    목적지 `sortingOrder`를 넣어, 뒤쪽 칸으로 걷는 캐릭터가 발을 떼자마자 앞 타일 아래로
    들어갔다. 이제 발 피벗이 두 칸의 경계를 넘는 eased progress 0.5에서만 출발→목적지
    정렬을 전환한다. 플레이어·보이는 적·넉백·낙하가 같은 `IsoGrid` 규칙을 쓰며, 층 전환과
    귀환도 위치와 정렬을 함께 갱신한다. PC Game View의 뒤쪽 이동 중첩 확인본은
    `docs/captures/player-sorting-normal-input-mid-v2.png`다(플레이어 전용 0.80 배율 포함).
- **화면 확인 결과 (2026-07-26 `59c5f80` 에디터 세션)** — 규칙은 살아 있는 던전에서 확인했고,
  아트 쪽에서 결손 둘이 드러났다.
  - **개구부 차폐 — 확정.** 실제 미리보기 **36칸** vs 옛 박스 방식이었다면 42칸이고, 가려진 6칸이
    전부 **닫힌 문(9,7) 너머 북쪽 방**이었다. 문 타일 자체는 미리보기에 들어온다("그 칸까지만").
  - **사다리 추격 단절 — 엔진 규칙 확정.** 같은 출발·도착에 `canClimb=true`는 2칸 경로,
    `false`는 **경로 없음**. 생성된 던전의 사다리 링크가 밴드별로 갈리는 것도 확인했다 —
    초반은 `e0→e1`(±1단), 중후반은 `e12→e14`(+2단 곧장)이고 중간 발판은 **링크가 없다**.
    이 캡처는 비평탄 생성 능력의 과거 검증이며 현재 첫 던전에서는 해당 구조를 생성하지 않는다.
  - **`CanClimb` 기본값 false가 값을 했다.** 신규 합선 검사 드론이 명시하지 않아 자동으로 못 오른다 —
    인간형(점거군 돌격병·기업 보안 사수·감시자)은 true, 기계(기업 진압 로봇·기업 추적 드론·
    합선 검사 드론)는 false. 첫 던전 보스 아레나는 층내 사다리를 만들지 않는다.
  - ✅ **보스 접근 전조 — PC 화면 확인(2026-08-04).** 6F까지 개발 이동한 뒤 플레이어를 실제
    상행 계단에 인접 배치하고 일반 `HandleTileTapped` → `MovePlayerPath` 전환을 실행했다. 6F에서는
    미표시, 7F(진행지수 8) 도착 시 `천장 너머가 낮게 울린다 — 한 층 위에서 감시자의 신호가 잡힌다`가
    한 번만 기록됐다. 7F 한 칸 이동과 전조 갱신 반복 뒤에도 로그는 1건이며, 보스 처치 후 플래그를
    다시 검사하면 미표시다. 전환 전/후 PC QHD 근거는
    `docs/captures/boss-approach-{before-6f,arrival-7f}-pc-2026-08-04.png`; 캡처 도구를 제외한 동일
    전환 재실행은 Unity 콘솔 오류 0건이었다.
  - **남은 것**: 첫 던전의 깊이별 벽 등잔 밀도 기울기는 이 세션에서 판단하기 어려웠다.
    캐치워크 격자 룩은 층내 높이를 실제 사용하는 원정지의 백로그로 이동한다.

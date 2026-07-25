# 현재 구현 스냅샷

> **역할**: 지금 코드가 실제로 어떤 상태인지에 대한 빠른 인수인계 요약이다.
> CLAUDE.md에서 분리해 나왔다 — 진입점은 얇게 유지하고, 자라나는 이력은 여기에 쌓는다.
> 세부 규칙과 완료 이력의 SSOT는 여전히 `docs/SYSTEMS.md`, `docs/UI_ARCHITECTURE.md`,
> `docs/ROADMAP.md`이며, 설계 결정의 최종 출처는 `GDD.md`다.
> 충돌하면 이 문서가 아니라 위 문서들을 믿는다.

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
- **첫 던전/보스**: 첫 목적지는 **폐병원(상승, `B2 → … → 8F` + 옥상 출구)** 10개 층 단일 던전이다
  (GDD §10.1 — 확정, **생성기는 아직 방향을 읽지 않는다**). 코드 ID `forgotten-catacombs`·seed·층 수는 유지한다.
  최상층의 `감시자`를
  처치하기 전에는 최심층 출구가 붉게 봉인되고, 처치 후 청록 해금 연출과 전용 HUD가 갱신된다.
  아레나에는 생성기가 고른 제단이 서고(처치 후 신호색이 식음), 바로 위층(B9)에 들어서면
  접근 전조를 한 판에 한 번 알린다(`DungeonBossArenaRules`).
  최심층 도착만으로 승리하지 않으며 출구 모달의 `던전 정복`을 선택해야 정산·런 종료가 확정된다.
  체크포인트는 `dungeonId/stageCount/bossDefeated`를 보존한다.
- **진행 방향 / 진행 지수 (v0.3.2)**:
  - **진행 방향은 던전별 데이터다**(`DungeonDirectionRules`) — 하강 / 상승 / **진입 깊이**(고도가
    진행 축이 아닌 던전, `1구역` 식 표기) 셋이 공존하며 **전역 스위치가 아니다**.
    폐병원=상승, 침수된 금고=진입 깊이. 잿불 성채는 미정(기본값 하강).
  - **진행 지수 ≠ 고도.** 난이도·구간 판정(적 혼합·휴식처·탈출구·장비 드랍·숨은 방·밴드·보스)은
    `DungeonFloorInfo.ProgressIndex`만 쓰고 **elevation 으로 역산하지 않는다**.
    `DungeonDepthBandRules.ForFloor`는 이 결함(`Max(0, -floorIndex)`) 때문에 삭제됐다.
  - **공간 ≠ 진행.** `StairsUp/Down`은 공간 이름이라 고정이고 "다음 층으로 가는 계단"만 방향을 탄다
    (`OnwardStair`/`BackStair`). 같은 이유로 `FinalFloorIndex`(진행 최종)와
    `BottomFloorIndex`(공간 최하단)는 다른 값이다 — 하강 던전에서만 우연히 같다.
  - **중력은 방향을 타지 않는다.** `FallRules`·`SightRules`는 던전 방향을 모른다.
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
- **배고픔/중간 생환**: `HungerRules`가 포만→배고픔(경고)→굶주림(주기적 HP 감소)을 소유한다.
  주기가 짧아(가득 찬 배 100턴) 중간중간 통조림을 먹는 리듬이며, 판 전체를 관통하고 모닥불로는
  배가 차지 않는다. `ExtractionRules`의 비상 탈출구는 B4·B8 두 곳뿐이고 최심층은 보스를 잡아야
  나간다. 비상 송출기(어디서든 생환)는 상점과 숨은 방에서 아주 가끔만 나온다 —
  정산은 모두 `ExtractRun` 하나로 모인다. 하드 타이머를 쓰지 않는 이유는
  파밍(기둥 4)을 죽이지 않기 위해서다.
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
- **절차 생성 임시 아트는 `IsoPrototypeDemo` 밖에 있다**: `PrototypeSpriteCanvas`(프리미티브 +
  64×32·PPU 상수 SSOT), `PrototypeSpriteCache`, `PrototypePalette`(역할색),
  `PrototypeActorSprites`, `PrototypeEnvironmentSprites`. 이 클래스들은 **격자·던전·플레이어를
  참조하지 않으며**, 필요한 격자 사실은 호스트가 `TileVisualFacts`로 풀어 넘긴다.
  `IsoPrototypeDemo.Sprites.cs`는 그 변환만 하는 123줄 어댑터다 — 픽셀을 다시 이 파일로
  들이지 말 것. 그리기 코드를 손댈 때는 테스트가 아니라 **씬 렌더 지문**으로 확인한다
  (`docs/CODE_STRUCTURE.md` "절차 생성 임시 아트" 참조).
- **최근 검증 기준**(2026-07-25, `6b1230f` + 스프라이트 분리 기준 — 세 경로 모두 실제 실행해 확인):
  - Core shim `./Tools/CoreTests/run-core-tests.sh` **758/758 통과**(진행 방향 계약 테스트 포함).
  - Unity EditMode `ProjectC.Tests.EditMode` **875/875 통과**. 컴파일 에러 없음.
  - Unity PlayMode `ProjectC.Tests.PlayMode` **1/1 통과**(`FirstDungeonSmokeTests`).
  - 옛 "673/673" 기록은 낡은 값이었다 — 실제 EditMode는 875개다.
  변경 후에는 숫자를 맹신하지 말고, 최소한 shim을 돌리고 에디터 회귀도 다시 실행한다.
- **작업 트리 주의**: 현재 여러 기능 변경이 아직 커밋되지 않은 상태일 수 있다.
  작업 시작 시 `git status`/`git diff`를 확인하고 기존 변경을 reset/checkout으로 지우지 않는다.


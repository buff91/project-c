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
  (GDD §10.1). **생성기가 방향을 읽으므로 표시와 구조가 일치한다** — 층 인덱스가 0에서 +9로 올라가고
  진출 계단이 `StairsUp`이며 진행 최종 층(+9)은 공간 최하단(0)과 다르다.
  코드 ID `forgotten-catacombs`·seed·층 수는 유지한다.
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
    첫 던전이 위로 올라가므로 거짓말이 된다. 구간 이름은 초반/중반/후반/보스를 쓴다.
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
    들어온 뒤에만 움직인다. 탑승구는 보스 아레나 바로 앞 층(폐병원 7F), 도착은 B1.
    **복귀 전용·한 방향**이라 진행의 반대로만 간다(상승=아래, 하강=위).
    생성기는 설비 타일만 놓고 **링크를 만들지 않는다** — 링크가 곧 "움직인다"이며
    `GridPathfinder`가 링크를 따라가므로 전원 전에 링크가 있으면 즉시 지름길이 된다.
    전원은 `PowerElevatorIfUnlocked`가 보스 처치·이어하기 복원 양쪽에서 넣는다.
    낙하가 아니라 탑승이므로 낙뎀 곡선은 건드리지 않았다 — 3층 자유낙하는 12 피해로
    영웅 HP(8~10)를 넘어 "뛰어내려 빠르게 하강"이 성립하지 않는다.
  - **지상 진입(B1 → 1F)은 한 판에 한 번 알린다**(`CrossesIntoAboveGround`).
    상승 구조가 공짜로 주는 전환점이라 여기서 짚으면 건물을 타고 오른다는 구조가 읽힌다.
- **지역(원정지) 정체성**: 콘텐츠 변주 표가 **(지역 × 깊이)** 두 축이다 —
  `DungeonBandProfiles.ForDepth(region, depth)`. 깊이가 기울기를 주고 지역이 기준선을 옮긴다.
  `Facility`(폐병원 — 기준선) · `Flooded`(침수된 금고: 웅덩이↑·광원↓·사수↓) ·
  `Ember`(잿불 성채: 웅덩이↓·밀도↑·광원↑). 던전 → 지역은 `DungeonDefinition.Region` →
  `DungeonLayout.Region`으로 실려가 생성기·런타임 스폰·광원이 같은 출처를 본다.
  - **지역은 혼합·밀도·무대 확률만 가른다** — 아키타입 스탯·행동 트리는 전 지역 공용이다.
  - **지역 인자에 기본값을 두지 않는다**(옛 `ForFloor` 역산 붕괴의 재발 방지).
  - **폐병원 출력은 불변임을 지문으로 고정했다**(`DungeonGeneratorGoldenTests`) — 세이브가
    seed 재생성이라 조용한 배치 변화가 곧 이어하기 붕괴다. 불변식 테스트로는 안 잡힌다.
  - **아직 안 열렸다**: `Flooded`/`Ember` 던전은 `isAvailable: false`라 이 값들은 미검증이다.
    기름 타일 무대도 없다(기름은 아이템뿐). 다음은 지역 전용 적 + 침수된 금고 개방.
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
  구현은 포스트아포 마감 자산(스타일 트랜스퍼 → 팔레트 잠금 PNG)으로 수렴했고, 2026-07에
  **128×64 타일 / PPU 128 레짐으로 상향**했다(`ui-*`만 64 유지, 절차 생성 폴백은 64-레짐인 채
  스프라이트별 PPU로 공존). 가독성 규칙·발주 순서는
  `docs/art-direction/project-c-art-improvement-plan-v2.md` 참조. 허브 웜 디오라마 패스는 유지 — 허브는
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
  절차 생성 64-레짐 상수 SSOT — 카탈로그 자산은 128-레짐이며 스프라이트별 PPU로 공존),
  `PrototypeSpriteCache`, `PrototypePalette`(역할색),
  `PrototypeActorSprites`, `PrototypeEnvironmentSprites`. 이 클래스들은 **격자·던전·플레이어를
  참조하지 않으며**, 필요한 격자 사실은 호스트가 `TileVisualFacts`로 풀어 넘긴다.
  `IsoPrototypeDemo.Sprites.cs`는 그 변환만 하는 123줄 어댑터다 — 픽셀을 다시 이 파일로
  들이지 말 것. 그리기 코드를 손댈 때는 테스트가 아니라 **씬 렌더 지문**으로 확인한다
  (`docs/CODE_STRUCTURE.md` "절차 생성 임시 아트" 참조).
- **해금 축 (진행 중)**: 도구 5종이 **조건 달성으로 열리고 다음 판부터** 드랍 풀에 들어온다
  (`ItemUnlockRules`). 계측은 `RunTelemetry` + `BountyMetric`을 재사용하며 새로 만들지 않았다.
  판정은 `FinishRunTelemetry` 한 곳이고 **사망에도 저장한다**(실패한 판도 전진).
  드랍 풀 게이트는 `DungeonMetaContext`로 넘기고 **롤 결과만 치환**해 RNG 스트림을 보존한다 —
  그래서 "전부 해금 = 게이트 없음"이고 지형·아이템 위치는 해금 상태와 무관하다.
  - **지켜야 할 제약 둘**: ① 조건은 `StarterReachableMetrics`(시작 풀로 달성 가능한 축)에서만
    고른다 — 빙결·기름·물을 쓰면 그 도구가 없어 영원히 못 여는 순환이다.
    ② **해금 안내를 의뢰로 주지 않는다** — 의뢰 게시판은 B단계에서 잠기는 시설이라 순환이 된다.
    안내는 판 종료 화면과 기록실이 맡는다.
  - **기록실**(`HubLayout.Codex`, `hub-codex-modal`)이 조건·최고 기록을 보여준다. 항상 열려 있다.
    진행값은 **최고 기록**(`MetaSaveData.unlockProgress`, 단조 증가)이다 — 조건이 한 판
    기준이라 지난 판 값을 쓰면 나쁜 판 뒤에 0으로 돌아가 안내가 죽는다.
  - **시설은 구출로 열린다**(`ShelterNpcRoster`): 대장장이→대장간, 연락책→의뢰 게시판.
    미구출 시설은 허브에 프롭도 상호작용도 없다. **장비 4종은 대장간에 종속**되며
    드랍 게이트는 롤을 그대로 소비하고 결과만 막는다. 구출은 즉시 저장한다(죽어도 남는다).
    갇힌 방은 **확률이 아니라 보장**이고 숨은 방과 겹치지 않는다 — 둘 중 하나라도 어기면
    시설이 영원히 안 열릴 수 있다(테스트로 고정).
    상인·창고·기록실은 잠그지 않는다.
  - **남은 것**: 실플레이로 조건 수치(화상 12·처치 20 등)와 구출 층(2·5) 조정.
    계획 전문은 `~/.claude/plans/calm-mapping-storm.md`.
- **최근 검증 기준**(2026-07-25, 지역 축 도입 후 — 세 경로 모두 실제 실행해 확인):
  - Core shim `./Tools/CoreTests/run-core-tests.sh` **851/851 통과**.
  - Unity EditMode `ProjectC.Tests.EditMode` **968/968 통과**. 컴파일 에러 없음.
  - Unity PlayMode `ProjectC.Tests.PlayMode` **1/1 통과**(`FirstDungeonSmokeTests` —
    폐병원 B2 → 8F 보스 → 출구까지, 치트 훅과 SPACE 경로 양쪽).
  - 같은 날 지역 축 도입 **직전** 기준선은 Core 844였다(옛 "673/673" 기록은 낡은 값).
  변경 후에는 숫자를 맹신하지 말고, 최소한 shim을 돌리고 에디터 회귀도 다시 실행한다.
- **작업 트리 주의**: 현재 여러 기능 변경이 아직 커밋되지 않은 상태일 수 있다.
  작업 시작 시 `git status`/`git diff`를 확인하고 기존 변경을 reset/checkout으로 지우지 않는다.


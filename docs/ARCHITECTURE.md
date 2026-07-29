# 코드 아키텍처 지도 (Architecture)

> **목적**: "이 로직이 어느 계층에 살고 무엇에 의존하나"를 코드 관점에서 압축한 인수인계 문서.
> 설계 결정의 최종 출처는 `GDD.md`(SSOT), **규칙 서술은 `docs/SYSTEMS.md`**, 파일 지도는
> `docs/CODE_STRUCTURE.md`다. 같은 사실을 두 번 적지 않고 소유 문서로 링크한다.
> 관련: [`GDD.md`](../GDD.md) · [`SYSTEMS.md`](SYSTEMS.md) · [`ROADMAP.md`](ROADMAP.md) ·
> [`CODE_STRUCTURE.md`](CODE_STRUCTURE.md) · [`UI_ARCHITECTURE.md`](UI_ARCHITECTURE.md)

---

## 1. 한눈에 보기 → [`CLAUDE.md`](../CLAUDE.md)

장르·엔진 버전·어셈블리 5개·핵심 설계 기둥 4개는 **`CLAUDE.md`가 SSOT**이고 실측 규모는
`docs/CODE_STRUCTURE.md`와 소스 트리를 따른다. 씬 흐름만 아래에서 계속 쓰이므로 적어 둔다:
`MainMenu`(0) → `Hub`(1) → `IsoPrototype`(2).

---

## 2. 아키텍처 규칙 → [`CLAUDE.md`](../CLAUDE.md) · 계층/의존 방향

준수 대상 규칙(로직↔비주얼 분리 · 정렬 SSOT는 `IsoGrid` · 입력 추상화 · 데이터 중심 · 성능)의
문장은 `CLAUDE.md`에 있다. 이 문서가 더하는 것은 **그 규칙이 코드 어디에 앉아 있는가**다.
데이터 중심의 실제 카탈로그는 `MonsterRoster` · `ItemCatalog` · `EquipmentCatalog`(전투 보정은
`CombatLoadout` 한 구조체로 전달) · `SurvivorProfile` · `DungeonCatalog` · `DungeonBandProfiles`
(지역 × 밴드)이며, `ForDepth/ForBand`는 지역을 **기본값 없이** 받는다 — 기본값을 주면 새 지역이
조용히 기준 지역으로 흐른다. 단 `Generate`의 `region` 인자만 `Facility` 기본값이다(호출부 보존).

### 계층/의존 방향

```
UI (UXML/USS)  ──▶  Gameplay (MonoBehaviour)  ──▶  Core (순수 C#)
                         │                            ▲
                         └── Stores/Services ─────────┘
                             (파일 I/O, 씬 상태)
Editor (ArtPipeline, SceneBuilder) ──▶ Core/Gameplay (에디터 전용)
Tests.EditMode ──▶ Core + 일부 Gameplay   ·   Tests.PlayMode ──▶ 실제 씬
```

의존은 항상 한 방향(Gameplay → Core)이다. Core는 Gameplay/Unity를 **모른다**. 크로스-레이어가
필요하면 Core가 콜백(`Func`/`Action`)을 인자로 받는다 (예: `MonsterBrainContext.SeenByPlayer`).
위 그림의 `Editor`는 실제로 **어셈블리 둘**이다 — `Editor/ArtPipeline/`(asmdef
`ProjectC.ArtPipeline.Editor`)와 asmdef 없는 `Editor/*.cs`(기본 `Assembly-CSharp-Editor`).

**Gameplay 안에서도 같은 원리를 쓴다.** UnityEngine 타입이 필요해 Core로 못 내리는 코드라도 게임
상태를 몰라도 되면 씬 참조 없는 별 타입으로 뺀다 — `Prototype*Sprites` 계열은 `Texture2D`/`Sprite`를
다뤄 Gameplay에 남지만 격자·던전·플레이어를 참조하지 않고 사실을 `TileVisualFacts`로 받는다.
"Unity에 의존한다"와 "게임 상태에 의존한다"는 **다른 축**이고, 후자를 끊는 것만으로 신(神) 클래스가 멈춘다.

---

## 3. 디렉터리 구조 → [`CLAUDE.md`](../CLAUDE.md) · [`CODE_STRUCTURE.md`](CODE_STRUCTURE.md)

트리는 `CLAUDE.md`, 파일별 지도는 `docs/CODE_STRUCTURE.md`가 SSOT다. 이 문서의 절 번호가
그 트리에 대응한다: `Scripts/Core` = §4~§10, `Scripts/Gameplay` + `UI` = §11, `Editor`/`Tools` = §12.

---

## 4. 좌표·공간 기반 (Spatial Foundation)

논리 좌표는 `GridPos { int x, y, elevation }`이며 코드 전체의 딕셔너리 키다.

### 4.1 GridPos — 좌표 규약
- `x`, `y` = 평면 좌표. `elevation` = **연속 높이값(층 번호 아님)**. 한 층 안에도 높이차가 있다.
- 방향 규약: `North=+y`, `South=-y`, `East=+x`, `West=-x`. `Offset(dx,dy)`는 elevation 유지.
- 거리 `ManhattanTo`/`ChebyshevTo`는 **둘 다 elevation을 무시**하고, 해시는 다항식 해시다(키 분포용).

### 4.2 TileData / TileKind — 타일 의미론
`TileKind` 15종과 그로부터 파생되는 술어(predicate)가 **이동·시야·낙하의 불변식**을 만든다:

| 술어 | 정의 |
|---|---|
| `IsSolidGround` | Floor, Stairs, Ladder, StairsUp/Down, DoorClosed/Open, SecretDoor/Passage, WindowBroken |
| `IsWalkable` | `(IsSolidGround && ≠DoorClosed && ≠SecretDoor) \|\| WeakFloor` |
| `BlocksSight` | Wall, DoorClosed, SecretDoor |
| `BlocksVerticalSight` | `≠Hole` (온전한 바닥은 수직으로 막는다 — §8.3) |
| `CanBreak` | Window (온전한 창문만) |
| `CausesFall` | Empty, Hole |

> 핵심 함정: `WeakFloor`/`Hole`/`Empty`는 **SolidGround가 아니다**(낙하가 이들을 관통한다).
> `DoorClosed`/`SecretDoor`는 solid ground이지만 walkable이 아니고 시야도 막는다.
> `Window`는 이동을 막으면서 **수평 시야는 통과**하는 유일한 타일이라 `BlocksSight`에 없다 —
> 깨면 `WindowBroken`(통로, 되돌릴 수 없음)이 되고 창밖이 허공이면 그대로 낙하로 이어진다(`WindowRules`).

`TileData`는 추가로 `oiled`(기름)/`wet`(물) 요소 플래그를 가진다.

### 4.3 GridMap — 희소 타일 저장 + 링크 그래프
- 희소 `Dictionary<GridPos,TileData>` + 명시적 연결 `Dictionary<GridPos,List<GridPos>>`.
- **두 종류의 인접성이 공존**한다: (1) 공간 4-이웃(±1 elevation은 계단/사다리에서만), (2) `Connect()`로 만든
  명시적 링크(층 전환, 사다리, 포탈). `Remove()`는 역링크까지 지워 그래프를 대칭 유지.
- **`FindLandingBelow`** — 모든 낙하/수직 시야의 원시 연산: 같은 (x,y) 컬럼을 아래로 훑어 첫
  `IsSolidGround`를 반환하고 없으면 `null`(무저갱)이다.

### 4.4 IsoGrid — 아이소 투영 + 정렬 (SSOT)
- **투영** `GridToWorld`: `wx=(x'−y')·½W`, `wy=−(x'+y')·½H + elevation·step`(x'는 시점 회전 적용 좌표).
  **역투영** `WorldToGrid`는 호출자가 elevation 평면을 지정한다(위→아래로 후보 시도).
- **정렬** `SortingOrder = elevation·elevationSortBand + 깊이` — **elevation이 우선**, 같은 층은 (x+y)가
  클수록 앞. 세부 정렬은 `SortingOrder(pos)·MicroResolution + microOffset`(바닥 데칼을 캐릭터 뒤로 등).
  밴드 폭은 `elevationSortBand > 깊이 해상도 × (maxX+maxY)`라는 불변식으로 정해져 인접 층이 안 섞인다.
- **시점 회전**: 카메라를 돌리지 않고 `viewQuarterTurns(0..3)`로 (x,y)를 피벗 기준 90° 투영하며,
  위 세 함수가 같은 회전값을 공유한다.

`GridSortingObject`(Gameplay)가 이 규칙을 씬 스프라이트에 적용하는 유일한 지점 — "아이소 정렬 지옥" 방지.

### 4.5 DungeonHeightModel — elevation ↔ (floorIndex, localHeight)
- `ElevationsPerFloor`(기본 4)가 elevation 축을 던전 층으로 분할한다(floor 0 = e0..e3, floor −1 = e−4..e−1…).
- `FloorIndex(e)` = **음수 안전 내림 나눗셈**(나머지가 음수면 −1 보정 — C# `/`는 0 방향 절삭이라 필수).
  `LocalHeight(e) = e − floorIndex·stride` ∈ `[0,stride)`. `SameFloor` = 두 floorIndex 동일.
- `DungeonVisualContext`가 `FloorIndex`/`ProgressIndex`/`Elevation`/`LocalHeight`를 **분리해** 제공 —
  비주얼 카탈로그가 raw elevation의 부호로 진행을 추론하지 않게 한다.

> **진행 지수와 고도는 분리되어 있다.** `DungeonFloorInfo.ProgressIndex`는 생성기가 경로 순서대로
> 부여하는 1급 데이터이고 난이도·구간·휴식처·탈출구·드랍·보스 판정이 이 값을 쓴다.
> `FloorIndex`/`Elevation`은 공간 배치·정렬·시야·낙하 전용이다. 과거의
> `DepthIndex = Max(0, -floorIndex)` 역산과 `DungeonDepthBandRules.ForFloor`는 상승 던전에서
> 모든 진행을 0으로 만들던 결함이어서 **제거됐다** — 세이브도 진행 지수를 직접 저장한다(§10.5).
> 내부 호환 이름 `Shallow`/`Mid`/`Deep`을 보더라도 고도나 표시명으로 해석하지 않는다.

---

## 5. 시야 (Field of View)

### 5.1 GridVisibility — Recursive Shadowcasting
- (x,y) 2D **8옥탄트** 캐스팅(Björn Bergström식). 반경은 체비셰프. 결과는 보이는 **표면 GridPos** 집합.
- **차폐 판정은 자기가 갖지 않는다** — 컬럼 해석을 `SightRules.ViewColumn`에 위임하고(§8.3) 그 덕에
  FOV·전투 LoS·폭탄 사거리가 **같은 기하 하나**를 본다. 판정 내용은
  [`SYSTEMS.md` — 시야/안개](SYSTEMS.md)가 소유한다.
- 3상태(Unknown/Explored/Visible)의 **"Visible"만** 계산한다. Explored 누적/렌더 정책은
  호출자(Gameplay)와 `FloorVisibilityRules`가 담당 — 이 분리가 "렌더 ≠ 시뮬" 불변식의 경계다.

### 5.2 FloorVisibilityRules — 무엇을 그릴지
순수 boolean 정책 하나: `debugAll`이면 전부, 활성 층이면 `visible || explored`, 그 외 층이면 `verticalPreview`(Hole 국소 미리보기)만.

> **미리보기 집합도 FOV로 만든다** (`IsoPrototypeDemo.Visibility`): 반대편 층의 elevation 대역에서
> `GridVisibility.Compute`를 한 번 더 돌린 결과를 쓴다. 예전에는 착지점 중심 체비셰프 박스를 전부
> 넣어 **차폐를 아예 보지 않았고**, 벽 뒤와 닫힌 문 뒤 방까지 드러나 "void=불투명" 불변식과 충돌했다.
> 반경 설정은 남기되 이제 **박스 크기가 아니라 FOV 사거리**다. 비용은 층 하나 FOV 1회.

---

## 6. 경로·이동 (Pathfinding & Movement)

### 6.1 GridPathfinder — 균일 비용 탐색(사실상 Dijkstra)
- A* 골격이지만 **휴리스틱 = 0**(명시적 링크가 elevation/xy를 임의로 점프하므로 최단 보장을 위해 Dijkstra
  형태). 비용 = **스텝당 1**(계단·링크 점프 포함). **4방향만**. open set은 선형 스캔 `List`(작은 격자라 충분).
- 이웃 생성 `EnumerateNeighbors`: 4방향 × elevation delta{−1,0,+1}. **높이 변화(±1)는 현재/후보 타일이
  `Stairs`일 때만** 허용 — **`Ladder`는 여기 없다.** 이후 `LinksFrom(current)`의 명시적 링크를 비용 1로 더한다.
- `openClosedDoors`: 몬스터 추격이 닫힌 문을 통과 경로로 계획하게 한다.
- `canClimb`(**기본 true**): false면 `IsLadderLink`인 링크를 건너뛴다. 기본이 true인 이유는 호출부
  대부분이 플레이어 이동/도달성 검사라서이고 덕분에 기존 도달성 불변식이 그대로 통과했다.
  **몬스터만 자기 `MonsterArchetype.CanClimb`를 넘긴다.** 층 전환 계단 링크는 어느 쪽 끝도 사다리가
  아니라 걸리지 않는다(걸리면 못 오르는 적이 자기 층에 갇힌다).

### 6.2 TravelRules — SPD식 자동 이동 게이팅
- `AllowedSteps` = 적이 보이면 탭당 **1스텝**, 아니면 경로 전체. 인터럽트 우선순위는 **피해 > 새로 보인 적 > 새로 보인 아이템**이고 `TravelInterrupt`의 **enum 값이 곧 우선순위**다.

### 6.3 VerticalTraversalRules — 층 전환/사다리
- `TryGetAutomaticFloorDestination`: 밟은 타일이 `StairsUp/Down`이고 링크가 있으면 즉시 링크 목적지로
  (진입 = 한 행동으로 층 전환). Stairs/Ladder/Hole은 자동 전환 대상이 아니다.
- `LadderWorldHeight`가 사다리 비주얼 길이를 실제 단차에서 계산한다(스프라이트 길이를 손으로 안 맞춘다).
- **사다리는 계단과 다르다**: 계단은 ±1단을 걸어서(A\*), 사다리는 여러 단을 **링크로만**·오를 수 있는
  종만. 캐치워크 층에서 `PlaceCatwalk`이 바닥↔캐치워크를 잇고 중간 발판 링크를 끊는 것
  (`Disconnect` 후 `Connect`)이 이 대비를 "높은 곳은 사다리로만"으로 굳힌다.

### 6.4 StairTopology — 계단 착지 지점
- `TryGetHigherLanding`: `Stairs` 타일의 4방향 중 한 단 위이며 walkable인 첫 칸(방향 배열 순서가 타이브레이크 — 같은 seed가 같은 결과를 내야 한다).

---

## 7. 절차적 던전 생성 (Procedural Generation)

`DungeonGenerator.Generate(map, w, h, floorCount, elevationsPerFloor=4, seed=1977,`
`direction=Descend, firstBuildingFloor=-1, meta=default, region=Facility)` — **아직 BSP도 랜덤워크도
아니다 — 의도된 발판(§7.4).** **축 정렬 3방 고정 템플릿 + 1칸 복도**를 유지한 채 단일
`System.Random(seed)`가 모든 치수를 흔든다. 같은 seed = 같은 던전(그리기 순서가 고정이라 재현 보장).

뒤쪽 인자 넷이 던전별 정체성이다: `direction`·`region`은 §7.5, `firstBuildingFloor`는 첫 층의 공간 층
인덱스(폐병원 −2 = 지하 2층 시작), `meta`(`DungeonMetaContext`)는 미구출 NPC 층 같은 **판 간 상태를
생성에 주입**한다. `meta`의 기본값은 "아무것도 해금 안 됨"이 아니라 **"제약 없음"**이라 테스트·미리보기가
메타 없이 옛 던전을 그대로 낸다. 코드는 파셜 넷이고 경계 기준은 한 줄이다 — **타일을 바꾸면
`.Carving`, 좌표만 고르면 `.Placement`**(치수 뽑기는 `.Planning`, 층을 엮는 `Generate` 본체는
`DungeonLayout.cs`). `map.Set`을 하지 않는 `PlaceRestSite`가 `.Placement`로 옮겨 간 것이 그 판례다.

### 7.1 층 템플릿
```
(NW 분기 방·옵션, 비밀이면 SecretDoor) ─ 북쪽 방: 적 스폰 · 뒤쪽 한 단 높음(▲Stairs 걷기 / ▮Ladder 링크) · ○Hole 후보
                                        └─ [문] ─ 세로 복도 ─┐
  남서 입구 방 (Entry / 귀환 Back, 물 웅덩이 후보) ─[문]─복도─ 남동 방 (아이템, 진출 Onward)
```
- 방 사이 최소 1칸 간격 → 연결은 반드시 **문**을 통과한다(방 밀봉 불변식, 문은 `DoorClosed`로 생성).
- 북쪽 방 X 범위는 **윗층과 겹치도록 제약**해 Hole 착지 컬럼이 항상 존재하게 하고, 뒤쪽 한 줄
  (`RaisedY`)은 한 단 올려 `Stairs`(걷기)와 `Ladder`(링크 `Connect`)로 잇는다.
- 층 전환 샤프트는 depth 홀짝에 따라 좌/우 컬럼을 번갈아 놓는다(같은 타일에 겹치지 않게).
  연결 축은 공간이 아니라 **진행**이다 — `FloorPlan.Onward`↔다음 층 `Back`을 `map.Connect`로 잇고,
  **마지막 진행 층의 `Onward`만 링크가 없다 = 원정지 출구**다. 하강 던전에서는 그 층이 최하층이고
  **상승 던전(폐병원)에서는 최상층**이라 "바닥 층"이 아니다. `DungeonFloorInfo`가 방향을 보고
  `Onward`/`Back`을 다시 `UpStairs`/`DownStairs`(공간 이름)로 되돌려 놓인 타일 종류와 맞춘다.

### 7.2 생성 파이프라인 (순서 중요 — 뒤 패스가 앞 타일 상태를 읽음)
1. **비밀 층 선택** `PickSecretDepths` — `SecretRoomRules.DesiredCount`만큼, **마지막 진행 층과
   구출 NPC 층을 뺀** 후보에서. NPC를 못 찾을 수 있는 숨은 방에 두면 해금이 영영 막힌다.
2. **층별 계획+카브(진행 순)** — `PlanFloor` → `CarveFloor`. 방/복도/문/올림 바닥/계단/사다리/샤프트.
   NW 분기 방(옵션, 비밀이면 `SecretDoor`). 이어서 `Onward`↔`Back` 링크를 잇는다(§7.1).
3. **개구부 + WeakFloor**(모든 층 카브 후) — 순회는 진행 순이 아니라 **공간 순 위→아래**다.
   각 층이 "바로 윗층 착지 칸"을 피해야 하는데(2층 관통 금지) 그 값은 윗층을 먼저 처리해야 생긴다.
   하강 던전에서는 이 순서가 옛 진행 순 순회와 같아 **같은 seed가 같은 던전을 낸다.** 보스 아레나
   층은 건너뛴다 — 상승 던전에서는 아레나가 최상층이라 조건이 없으면 구멍이 뚫려 보스전 중 낙하로
   아레나를 벗어난다. 앵커는 `LandsOneFloorBelow`(정확히 한 층 아래 walkable 착지, 윗층 **개구부
   전체** 컬럼 제외)를 만족하는 칸이고 거기서 **한 칸씩 자란다**(같은 조건 +
   `KeepsUpperRoomConnected` 플러드 필) — 2×2 같은 모양을 강제하면 최소 크기 던전에서 후보가 0이
   되거나 도달성이 깨진다. **성장 루프는 난수를 쓰지 않아 RNG 스트림이 그대로다.** WeakFloor는
   개구부 **둘레**에 **같은 판정 함수**로 붙는다 — 밟으면 개구부가 되니까.
4. **엘리베이터 통로**(`ElevatorShaftRules.AppliesToDungeon`일 때만). 스폰보다 **먼저** 놓아야 사다리
   타일이 `IsFreeForSpawn`에 걸러져 적·아이템이 통로에 갇히지 않는다. 링크는 여기서 잇지 않는다 —
   보스를 잡을 때 Gameplay가 `Connect`한다(§10.7). 해당 없는 던전에서는 패스가 아예 돌지 않는다.
5. **층별 배치 10패스**(전부 최종 타일 상태 위에서) — 구출 NPC → 휴식처 → 물 웅덩이 → 적 스폰
   (문 뒤 북쪽 방만) → 아이템 → 장비 → 중간 탈출구 → 보스 제단 → 캐치워크 → 창문.
   가운데 다섯(휴식처~장비)만 RNG를 쓰고 나머지는 **결정론 패스**라 지문(골든 테스트)을 흔들지 않는다.
6. **조립** — `DungeonFloorInfo` → `DungeonLayout`.

### 7.3 관련 규칙 클래스
- **SecretRoomRules** — `DesiredCount` · `CanInvestigate`(같은 elevation·맨해튼 1) ·
  `TryReveal`(`SecretDoor`→`SecretPassage` in-place, **멱등**) · `RevealInBlast`.
  비밀 분기 보상은 RNG 없이 **진행 지수로 갈린다**(깊을수록 유물).
- **DungeonBossRules** — `TrySelectSpawn`(입구에서 맨해튼 최대 후보) · `CanUseExit`(보스 없거나 처치 시).
  보스 **표시명은 `MonsterRoster.GraveWarden`이 소유하고 카탈로그가 참조**한다 — 두 곳에 적으면
  화면에 코드 ID가 샌다.
- **DungeonBossArenaRules** — 아레나 층 판정(`IsArenaFloor`)과 전조 문구(`TryApproachCue`). 전조는
  **진행 방향을 인자로 받는다**(상승/하강/`Inward`가 각각 다른 문구, 원문은 `SYSTEMS.md`) —
  예전에는 "한 층 아래"로 고정이라 상승 던전에서 화면이 거짓말을 했다.
- **DungeonCatalog** — `폐병원`(`forgotten-catacombs`, Ascend, 보스 감시자, region 기본값 Facility)과
  `침수된 금고`(`flooded-vault`, Inward, 보스 없음, region Flooded)가 available이고
  `잿불 성채`(`ember-keep`, region Ember)만 `isAvailable: false`다. 코드 ID는 세이브 호환을 위해
  리스킨 전 이름을 유지하고, `ById`는 없으면 `All[0]` 폴백이다.
- **AreaSpawnBonus** — 면적 비례 스폰 스케일(작은 테스트 던전이 과밀해지지 않게).

### 7.4 발판 → BSP 전환 (배치 관점)

3방 고정 템플릿은 **버릴 임시 코드가 아니라 생성 불변식을 먼저 못박기 위한 발판**이다. 배치 관점으로
남길 사실은 하나 — **교체 대상은 `.Planning`/`.Carving`의 알고리즘뿐이고, 불변식은 계약이라
`ProceduralDungeonTests`가 구현과 무관하게 강제한다**(문 밀봉, Hole은 정확히 한 층 아래 walkable 착지,
층 간 기둥 겹침으로 착지 컬럼 보존, 적은 문 뒤에만). 계획·난도·선행 작업은 `ROADMAP.md`의
"던전 생성기 BSP/룸-앤-코리더 전환"이 소유한다.

### 7.5 진행 방향과 지역 프로파일 (던전별 정체성 축)

던전은 독립된 두 축으로 정체성을 갖고, 둘 다 **전역 스위치가 아니라 `DungeonCatalog`의 던전별
데이터**로 `Generate` 인자에 실린다(§7). 두 축의 의미·라벨 규약은 [`SYSTEMS.md` — 다층
격자](SYSTEMS.md)가 소유하고, 여기서는 **누가 어디서 읽는가**만 적는다.
- **`DungeonProgressDirection`**(`DungeonDirectionRules.cs`) — `Descend`/`Ascend`/`Inward`.
  코드에서 이 값이 실제로 바꾸는 곳은 넷뿐이다: 진출 계단이 `Onward`/`Back` 중 어느 공간 이름으로
  되돌아가는가(§7.1) · 층 라벨(`FloorLabelFor`) · `ElevatorShaftRules.AppliesTo` ·
  `DungeonBossArenaRules.TryApproachCue`. 그 밖의 판정은 **언제나 `ProgressIndex`**를 쓴다(§4.5).
  `FallRules`·`SightRules`는 이 enum을 아예 모르며(중력은 방향을 타지 않는다), `FallMeaning`도
  판정이 아니라 **안내 문구의 의미**라 소비처가 copy뿐이다.
- **`DungeonRegionProfile`**(`DungeonBandProfile.cs`) — 던전 ID가 아니라 **프로파일**이라 여러 던전이
  같은 결을 공유해도 표가 늘지 않는다. `ForDepth(region, depth)`가 적 조합 가중치·`ExtraEnemies`·
  분기/웅덩이 확률·캐치워크 길이의 SSOT이고 `MonsterRoster.PickForDepth`와 생성기 배치 패스가 이것만
  읽는다. 밴드 **경계**는 `DungeonDepthBandRules`가 따로 소유한다(섞으면 지역 수만큼 고쳐야 한다).

---

## 8. 입체 공간 & 낙하 (Verticality & Falling)

### 8.1 FallRules — 모든 낙하의 수렴점
- **낙뎀은 `DamageForDrop(dropCells, EPF, safeFallHeight)` 한 점으로 수렴한다** — 층이 아니라 실제
  낙하 **칸수** 기준 가속 곡선이고 `TryFall`의 유일한 피해 경로다. 곡선 수치는
  [`SYSTEMS.md` — 낙하](SYSTEMS.md)가 소유하고, `safeFallHeight`는 액터가 아니라 `CombatLoadout`이
  장비에서 실어 온다(§10.5). 층 단위 `DamageForFloors`는 **테스트 오라클로만 남은 기준 곡선**이다 —
  프로덕션 호출부가 없고 `FallRulesTests`가 칸 곡선의 층 낙하값을 이 값과 대조한다(지우면 대조가 사라진다).
- `TryFall` 파이프라인: `FindLandingBelow`(없으면 null·낙하 안 함) → `floorsFallen`(연출·텔레메트리용) →
  낙뎀 → **착지 충돌**(착지칸 산 점유자에 같은 피해) → 점유자 생존 시 낙하자는 인접 빈 칸으로 밀림.
  **연쇄 낙하는 재귀가 아니다** — 한 호출은 첫 단단한 바닥까지고 여러 층 관통은 `floorsFallen>1`이다.

### 8.2 KnockbackRules — 폭발 넉백
- `PushDirection`은 `target−center`의 우세 축 1칸(동률은 x 우선), `Resolve`는 벽/닫힌 문/점유 → `None`, walkable → `Pushed`, 구멍/void → `PushedIntoFall`(호출자가 `TryFall`로 잇는다).

### 8.3 SightRules — 시야·도달 판정의 단일 출처
**FOV·전투 LoS·개구부·근접 도달의 기하가 전부 여기 하나로 모인다**(옛 `VerticalOpeningRules` 흡수).
흩어져 있을 때는 "폭탄은 닿는데 화살은 안 닿는" 식으로 축마다 답이 갈렸다. 판정 내용은
[`SYSTEMS.md` — 시야/안개](SYSTEMS.md)가 소유하고 여기서는 위임 관계만 적는다.
- `HasLineOfSight`(수평·경사, 같은 컬럼이면 `HasVerticalSight`로 넘김) · `HasVerticalSight`
  ("void=불투명"은 컬럼을 벽으로 읽는 **수평** 규칙이라 수직에는 적용하지 않는다) ·
  `CanReachAcross`(근접 단차 타격 기하 — `CombatRules.AreAdjacent`가 위임).
- `ViewColumn` → `ColumnView` — 컬럼을 눈높이 기준 **span**(지면 + 머리 위 구조물 + 너머 차단)으로
  해석하며 `GridVisibility` 셰도우캐스팅이 이 판정만 쓴다(§5.1). `ViewFromFloor`는 **오직 `Hole`만**
  시야 포털로 인정하고(StairsUp/Down은 아님) `isVisible` 델리게이트로 FOV를 존중한다.

### 8.4 VerticalRouteCue — 최초 발견 카드 copy
- `TryCreate(kind, viewedFromBelow, destinationLabel)`이 타일 종류를 카드 copy로 매핑한다(계단=WALK,
  사다리=CLIMB, 층 전환=`목적지 ▲/▼`, Hole=위/아래 시야). **층 라벨 문자열은 호출자가 넘긴다** —
  여기서 만들면 방향·구역 표기가 또 한 벌 생긴다(§7.5).

---

## 9. 전투·상태이상·AI

### 9.1 전투 (`CombatantState.cs` = 엔티티 / `CombatRules.cs` = 규칙)
- **엔티티와 규칙이 파일부터 갈려 있다.** `CombatantState`는 불변 스탯 + `Hp`뿐이고 방어/치명타가
  없으며 사망은 **이벤트 없이** `Hp==0`(`IsAlive`)으로만 표현한다(이벤트를 두면 구독자마다 순서 문제).
- `AreAdjacent`는 판정을 직접 하지 않고 `SightRules.CanReachAcross`에 위임한다(기하 SSOT는 §8.3).
  `TryRanged`도 도달 비용 + `SightRules` 시야선이라 **폭탄·화살·FOV가 같은 기하를 본다**.
- `FindFiringPosition`은 **결정적 타이브레이크**(경로 길이→표적 근접→x→y)를 쓴다 — AI가 같은 상황에서
  같은 칸을 고르지 않으면 테스트가 오라클이 되지 못한다. 수치는 [`SYSTEMS.md` — 전투](SYSTEMS.md).

### 9.2 상태이상 & 요소 반응
- **StatusEffects** — `Burn`/`Freeze`/`Poison`. 지속 턴·틱 피해 수치와 "왜 이 셋인가"는
  [`SYSTEMS.md` — 상태이상 & 요소 반응](SYSTEMS.md)이 소유하고 여기서는 구조만 본다.
  `Apply`는 **상쇄 우선**(상쇄 쌍은 `StatusReactions.CancelPairs` 하나 — `Burn↔Freeze` 상호 소멸,
  `Poison`은 표에 없어 독립 지속) 아니면 **max로 연장**(단축 없음)이고, `Tick`은 감소 **전에**
  출력을 계산해(마지막 틱도 발동) `StatusTick`으로 `BurnDamage`/`PoisonDamage`/`Frozen`을
  **분리해** 낸다 — 팝업이 원인별로 갈리기 때문이다.
- **요소(타일) 반응은 StatusEffects가 아니라 별도 규칙**이고 셋 다 **칸 목록만 반환하고 연출은
  호출부(Gameplay)**라는 같은 계약을 쓴다 — `OilRules`(`Interactions.cs`, 살포·발화) ·
  `WaterRules`(웅덩이 결빙·증발) · `ShockRules`(통전, 트리거는 `MonsterRangedEffect.ConductiveShock`).
- **공용 이터레이터 둘이 이 셋을 묶는다**: 3×3 블라스트 순회 `BombRules.ForEachBlastCell`와 젖은
  웅덩이 4방 확산 `WetPoolFlood.Collect`(파일은 `WaterRules.cs` 안). 예전에는 같은 이중 루프/BFS가
  여러 곳에 복제돼 범위 규칙을 바꿀 때 한 곳씩 어긋났다.
- **CombatPresentationRules** — source 문자열을 `Physical/Fire/Frost/Heavy`로 분류하고 FX 수치를
  제공한다. 로직 없는 순수 룩업이라 Core에 있어도 비주얼 결정이 Gameplay로 새지 않는다.

### 9.3 턴 파이프라인 (TurnManager)
- **엄격 2단계**(에너지/속도 없음) `Player` → `Enemies`. 적 하나는 **활성화 → 상태이상 틱 → Decide → 실행** 순이고, 휴면(컬링) 중이면 틱도 멈춘다.

### 9.4 몬스터 AI (MonsterBrain) — Behavior Tree
- **아키텍처: Behavior Tree**(손으로 쓴 FSM에서 이관 — 콘텐츠가 늘어도 분기 가독성이 유지된다).
  경량 프리미티브 `BehaviorNode/Selector/Condition/Leaf`(`BehaviorTree.cs`, 즉시 결정형). `Decide`는
  우선순위 Selector 트리를 돌고 **의도**(`Wait/Step/OpenDoor/Attack`)만 반환하며 씬을 만지지 않는다.
  FSM 상태 `MonsterMood`는 **블랙보드**로 남겼고(부수효과 노드가 매 틱 갱신), 공개 API·동작은
  이관 전후 완전 동일하다 — 테스트가 그 오라클이었다.
- **트리 우선순위**: 사망→대기 · 불붙으면 물로 도주해 소화 · 지각/기분 갱신 · 도주 · 추격 · 순찰.
  판정 수치와 "왜 그렇게 행동하나"는 [`SYSTEMS.md` — 몬스터 AI](SYSTEMS.md)가 소유한다.
- **지각은 플레이어 FOV의 대칭**(`SeenByPlayer` 콜백)이지 `HasLineOfSight`가 아니다 — 계단/단차 위
  플레이어에게 지각이 끊기지 않게 하려는 의도적 비대칭이다.
- 경로 판단은 전부 `GridPathfinder`에 위임하고(추격 중에만 닫힌 문 개방 플래그, 종별 `CanClimb`),
  **약한 바닥은 자진 회피**한다 — 낙하는 플레이어의 밀기/넉백으로 유도하는 것이 정석이라서다.

### 9.5 로스터·활성화
- **MonsterRoster** — 일반 5종(Goblin/Skeleton/Slime/Slinger/**ArcDrone**) + 보스 GraveWarden.
  **스탯·표시명의 SSOT는 이 파일 하나**이고 규칙 서술은 [`SYSTEMS.md` — 몬스터 AI](SYSTEMS.md)가
  소유한다 — 수치를 여기 복제하면 한쪽만 갱신된다(실제로 그랬다). 배치 관점의 사실 셋:
  **등반 가능 여부는 실루엣과 일치**시켜 인간형만 `CanClimb`(규칙을 배우지 않고 눈으로 판단하게),
  원거리 명중 효과는 `MonsterRangedEffect`(합선 드론의 `ConductiveShock`가 웅덩이를 통전시킨다),
  **혼합 가중치는 로스터가 아니라 `DungeonBandProfiles`가 소유**하고 `PickForDepth`는 한 번만 롤한다(§7.5).
- **원거리 몬스터** — `IsRanged`면 브레인이 `DecideRanged`를 먼저 탄다(거리 벌리기 → 사격 →
  사선 잡는 한 걸음, 셋 다 실패하면 일반 추격). 판정은 플레이어와 **같은 `CombatRules`**를 쓴다.
- **MonsterActivation.IsActive** = **같은 층 && 활성 반경**. 비활성은 `Decide` 자체를 스킵(성능 핵심).
- **거리 metric 규약**: 지각/어그로/배회/도주/활성화 = **체비셰프(8방)**, 인접/사거리/실제 이동 = **맨해튼(4방)**. 의도적 비대칭.

### 9.6 프레젠테이션 게이팅
- **EnemyPresentationRules** — `ShouldShowFeedback`(FOV·같은 층만)·시체 수명/투명도. Core에 두어 "시야 밖 피드백은 숨긴다"가 렌더 코드로 흩어지지 않는다.

---

## 10. 아이템·인벤토리·조합·메타

### 10.1 아이템 (`Items.cs` — 상호작용 규칙은 `Interactions.cs`)
- **ItemKind**(값 load-bearing)는 `ItemCategory` 4종(소모품·전리품·재료·장비)으로 갈린다.
  종류 목록과 가격·용도는 [`SYSTEMS.md` — 백팩/창고](SYSTEMS.md)가 소유한다.
- **ItemCatalog가 아이템 정의의 단일 출처다** — 분류·골드·상점가·백팩 면적·표시명이 한 표에 있고
  `CategoryOf`가 유일한 분류 판정이다. **종류를 늘릴 때 손댈 곳은 `ItemKind` + 이 표 한 줄뿐**이며
  미등록 enum은 기본값으로 조용히 흐르지 않고 즉시 실패한다. 예전에는 "전리품인가? 재료인가?"가
  여러 조건문에 흩어져 하나만 빠뜨려도 아이템이 조용히 사라졌다.
  `AllKinds` 순서가 **정본 반복/타이브레이크 순서**(백팩 팩킹·세이브·텔레메트리 공용)다.
- **BombRules**(`Interactions.cs`, `BombResult`와 동거) — 3×3 순회는 `ForEachBlastCell`(§9.2),
  `Detonate`는 **본인 포함** 피해 + 빈 WeakFloor→Hole 붕괴.

### 10.2 백팩 자동 배치 (BackpackRules) — 빈 패킹
- 격자 **6×4=24셀**, 회전 없음. **면적의 SSOT는 `ItemCatalog.For(kind).Footprint`**이고
  `BackpackRules.Footprint`은 기존 호출부용 위임일 뿐이다 — 칸 수를 바꾸려면 카탈로그를 고친다.
  **장비도 면적을 먹는다**(장착 중인 것만 예외 — §10.5). 칸 수 표는 `SYSTEMS.md`가 소유.
- `TryCreateLayout` = **결정적·큰 것 우선·행 우선 그리디**. 정렬 키(면적↓ → 높이↓ → KindOrder↑ →
  InstanceIndex↑)와 `AllKinds` 반복 순서 덕에 **같은 아이템 집합은 항상 같은 배치**가 나온다.
  하나라도 못 놓으면 전체 실패(null) — all-or-nothing이라 부분 결과가 UI로 새지 않는다.
  면적이 남아도 조각화로 실패할 수 있다(설계상 배치 시점에 false).
- `Inventory`는 **종류별 count 스택**(인스턴스 상태 없음). `TryAdd`가 bounded면 매번 `TryCreateLayout`로 검증·롤백.

### 10.3 조합 (CraftingRules)
- 레시피 표는 `SYSTEMS.md`가 소유하고 매칭은 순서 무관. `TryCraft`는 재료 소비 → 산출 `TryAdd` 뒤 **원자적 롤백**을 건다 — 백팩이 꽉 차면 재료를 되돌려 손실이 없다.

### 10.4 출정 로드아웃 (ExpeditionLoadoutRules)
- 창고 ↔ 출정 백팩의 1개 단위 이동. **전리품은 창고/로드아웃에 못 들어간다**(항상 골드). `CreateInventory` =
  기본 지급품(`SurvivorProfile.StarterCount` — **hero 인자를 받지 않는다**) + 선택 로드아웃이고,
  `Reconcile`이 초과분을 창고로 되돌리며 `ConsumeLoadout`이 진입 시 반입을 확정한다.

### 10.5 세이브·메타
- **세이브는 아이템을 `List<ItemStack>`으로 담고 연산을 `ItemStorage`에 위임한다**(런 세이브·창고·
  로드아웃 공용). **종류를 늘려도 세이브 필드를 추가하지 않는다** — 예전에는 클래스마다 종류별 int
  필드와 switch가 있어 여섯 곳을 고쳐야 했고, 한 곳만 빠뜨리면 아이템이 조용히 사라졌다.
- **RunSaveData**(`[Serializable]`) — 층 체크포인트. **지형/적/아이템 배치는 저장 안 함**(seed로 재생성)이라
  이어하기는 "현재 층을 층 입구에서 다시 시작"이다. 계약은 `dungeonId`/`stageCount`/`bossDefeated` +
  `currentProgressIndex`·`deepestProgressIndex`(고도로 역산할 수 없어 **진행 지수를 직접 저장** — §4.5) +
  `carriedWeaponId`/`carriedGearId`(반입 장비는 죽으면 잃으므로 런 상태) + `hunger`(층·던전을 넘어 이어진다) +
  `usedRestFloorIndices` + `items`(전리품 포함 — 아직 환금 전).
  `RunStartRules.ResolvePreviewDepth`: 새 판=0, 이어하기=저장된 `currentProgressIndex`.
- **MetaSaveData**(`[Serializable]`) — 판 종료(사망 포함)에도 유지되는 은행. `gold`·`stash`/`loadout`·
  장비 슬롯·`activeBountyIds`·`unlockedItems`·`unlockProgress`(조건별 역대 최고, 단조 증가)·
  `unlockInvested`(투입분 — 출처가 달라 따로 둔다: 하나는 달성한 값, 하나는 산 값)·`rescuedNpcs`·
  `records`·`deepestFloorsEver`. `AddCount`가 전리품을 걸러 창고에 남기지 않는다(생환 정산에서 골드가
  되므로 남으면 이중 계산). 옛 `unlockedHeroes`/`heroId`는 **제거** — 무시하고 로드되어 마이그레이션 없음.
- **SurvivorProfile** — 원정자 기본값 상수 **하나**. 직업도 프리셋도 없다(정체성은 캐릭터가 아니라
  장비가 진다) — 옛 `HeroRoster`/`HeroSelection`을 대체한 자리다.
- **Equipment / ForgeRules** — 무기 1 + 보조 1 슬롯. 장비는 **공격력을 올리지 않고 규칙만 바꾸며**
  (옛 영구 스탯 강화 `SmithyRules`는 제거), 보정은 `CombatLoadout` 한 구조체로 모여
  `CombatRules`·`FallRules` 호출부가 **파라미터로** 받는다 — 전역 상태로 두면 몬스터에도 새기 때문이다.
  장착 장비만 백팩 공간을 쓰지 않는다(§10.2).

### 10.6 텔레메트리 (RunTelemetry)
- 순수 데이터/집계(Unity 시간·파일 모름). 스키마 버전은 `RunTelemetry.CurrentSchemaVersion`이 단일
  출처이고 기록 항목은 [`SYSTEMS.md` — 텔레메트리](SYSTEMS.md)가 소유한다.
- **사중 기록**: 런 총계 + 층별(`RunFloorTelemetry`) + 구간별(`RunBandTelemetry`) + 소스별/아이템별.
- **구간(밴드) 롤업은 파생 값이다** — `RefreshBands()`가 층별 기록을 `DungeonDepthBandRules` 경계로
  다시 묶고 따로 기록하지 않는다. 경계를 바꾸면 과거 리포트도 같은 규칙으로 다시 묶인다.
  **경계와 사람이 읽는 라벨의 SSOT도 `DungeonDepthBandRules`**이며 라벨은 진행 순서 기준이다 —
  `B1~B3` 식 지하 층 표기는 상승·평면 던전에서 화면에 거짓이 되어 폐기했고, 열거자 이름
  (Shallow/Mid/Deep)만 과거 리포트 호환을 위해 JSON 키로 남는다.
- 판 종료 시 `development-profile/telemetry`에 JSON 자동 확정. `RunSummary`는 게임오버/승리 모델(첫 결과 latch).

### 10.7 그 밖의 Core 규칙 소유자 (한 줄 색인)

위 절에 이름이 없는 Core 규칙 파일 — **어느 규칙이 어느 파일에 사는가**만 적는다(수치·판정은
`SYSTEMS.md`, 파일 단위 SSOT 표는 `CODE_STRUCTURE.md`).
- **진행·생환**: `ExtractionRules`(중간 탈출구 층 선정) · `HungerRules`(배고픔 단계·패널티, 탈출구와
  짝) · `ElevatorShaftRules`(**복귀 전용** 통로 — 진행 방향으로 태우면 지름길이 되어 페이싱이 무너진다.
  생성기는 설비만 놓고 링크는 Gameplay가 넣는다) · `WindowRules`(창문 파괴·수평 시야 포털, §4.2).
- **메타 진행**: `RunRecordRules`(죽음이 남기는 것은 물자도 스탯도 아닌 **기록**) · `ItemUnlockRules`
  (기록 → 도구 해금. `ClosestPending(meta)`는 `RemainingFor`(역대 최고 + 투입) **한 축**으로 잰다 —
  예전에는 이번 판 계측도 읽어 기록실 표시와 실제 판정이 갈렸다) · `BountyRules`(의뢰, 완료 판정
  백엔드는 `RunTelemetry`) · `ShelterNpcRoster`(구출 대상·해금 시설).
- **던전 데이터**: `DungeonLootRules`(지역별 드롭 편성) · `DungeonMetaContext`(판 간 상태 주입, §7) ·
  `DungeonBandProfile`·`DungeonDirectionRules`·`DungeonBossArenaRules`(§7.5·§7.3).
- **공용 연산·표현**: `ItemStorage`(수량 목록 연산, §10.5) · `SpriteClipRules`(§11.4) · `HubLayout`
  (허브 고정 레이아웃 — 던전 렌더러를 그대로 태우려고 층 1개짜리 `DungeonLayout`으로 낸다) ·
  `GridLighting`(0..1 밝기 한 축 — FOV의 "무엇이 보이나"와 **직교**한다. 차폐는 계산하지 않는다:
  광원이 이미 FOV로 걸러진 가시 집합에만 적용되므로 공짜로 따라온다).

---

## 11. Gameplay·프레젠테이션 계층

### 11.1 IsoPrototypeDemo — 오케스트레이터 (관심사별 partial)
던전/허브를 실제로 세우고 굴리는 중심 MonoBehaviour. Core 규칙을 씬·스프라이트·연출로 번역하며
관심사별 파셜(입력·이동·행동·적·낙하·시야·조명·생환·런 수명주기·보스·전투 FX…)로 갈라져 있다.
**파셜 목록은 자주 변하므로 `docs/CODE_STRUCTURE.md`가 SSOT다.**

> **파셜 분할은 탐색성을 높이지만 상태 결합을 줄이지 않는다.** 게임 상태를 몰라도 되는 코드는
> 파셜이 아니라 별 타입으로 뺀다(§2) — `IsoPrototypeDemo.Sprites.cs`가 픽셀을 직접 그리지 않고
> 격자 사실만 스프라이트 팩토리에 넘기는 어댑터인 것이 그 형태다.

- 내부 에이전트(경량 뷰 홀더) `EnemyAgent`/`ItemAgent`/`RestSiteAgent`/`VerticalLandmarkAgent`와
  이벤트(`PlayerHpChanged`·`ActiveFloorChanged`·`ExitChoiceRequested`…)로 HUD와 느슨 결합한다.

### 11.2 씬 얇은 진입점 & 서비스
- **GridManager** — `GridMap`+`IsoGrid` 소유, 좌표 변환 헬퍼. **IsoTapInput** — 입력→`GridPos`/액션(장치 추상화).
- **입력 픽킹의 판정은 Core `WorldInputRules`가 소유한다** — 아이소 다이아몬드 히트 테스트,
  우선순위(**LayerPriority↓ → SortingOrder↓ → 중심 근접**), `IsMapTile`(검은 여백은 격자 좌표로
  환산돼도 맵 입력이 아니다). `IsoTapInput`은 이를 호출해 액션으로 바꾸기만 하고, 적을 먼저 집는
  규칙은 호스트가 `ActorPicker` 델리게이트로 주입한다 — 그래야 입력 레이어가 게임 상태를 모른다.
- **정적 서비스**: `AtomicJsonStore`(임시 파일 교체 + 백업 복구)를 바닥에 두고 `RunSaveStore`·
  `MetaStore`·`DisplaySettingsStore`·`RunTelemetryStore`가 그 위에 앉는다. `DevelopmentSaveProfile`·
  `DevelopmentViewportService`는 개발 전용(격리 저장 루트·에디터 해상도 강제)이다.
- **씬 라우팅**: `FrontEndFlow`(씬 이름 상수)·`TitleEntryRouting`(타이틀 목적지 + `이어하기` 노출
  규칙)·`DungeonSelection`·`MainMenuController`.

### 11.3 UI (UI Toolkit 화면 HUD)
- 컨트롤러: `PrototypeHudController`(던전 HUD·액션 휠) · `HubHudController` ·
  `InventoryPanelController`(6×4 백팩+조합) · `DisplaySettingsPanelController` · `DebugPanelController`.
- **ResponsiveUiLayout** — UI Toolkit엔 런타임 미디어쿼리가 없어 패널 논리 크기를 USS 클래스로 바꾸고
  `Screen.safeArea`를 패널 좌표로 환산해 노치에 대응한다. 임계값은 `UI_ARCHITECTURE.md`가 소유.
- **OrthographicCameraFraming** — 플레이어 추종 중심과 직교 카메라 크기를 묶는다.
  **허브/던전 플레이 배율은 `playCameraSize` 하나**이며 허브도 맵 경계 auto-fit 없이 플레이어를
  추종한다. 전체 맵을 보이는 `debugCameraSize`는 던전 DebugAll에서만 쓴다. 패리티는
  `OrthographicCameraFramingTests`가 고정하고 회귀 사례 서술은 `STATUS.md`가 소유한다.
- 방침: 화면공간 평면 = UI Toolkit, 월드 앵커/추종 = UGUI (상세 `UI_ARCHITECTURE.md`).
  **인터랙션 트위닝은 UGUI/월드 UI에만 DOTween**(`DOTweenBootstrap`)이고, UI Toolkit
  `VisualElement`엔 DOTween이 직접 붙지 않아 `experimental.animation`/USS transition을 쓴다.

### 11.4 액터 애니메이션 계층 (Animator 미사용)

스프라이트 클립 재생은 네 층이고 각 층의 소유물이 하나씩이다.
- `Core/SpriteClipRules` — 시간 → 프레임 인덱스(`FrameAt`) + 공식 태그 6종. UnityEngine 무의존이라 shim 테스트에 그대로 올라간다.
- `Gameplay/ActorAnimationSet` — 베이크 산출물 그릇(`SpriteClip` = 프레임 배열 + **프레임 시작 시각** +
  클립 길이). 지속시간이 가변인 Aseprite 타이밍을 무손실로 옮기려 "시작 시각" 형태로 저장한다.
- `Gameplay/SpriteClipAnimator` — 경량 재생기. **`renderer.sprite`만 만진다** — position·scale·color는
  `CombatFx`가 소유하므로 겹치면 둘이 싸운다. 시야 밖에서는 시간이 얼어붙었다 이어가 재동기화가 없고,
  클립 없는 태그는 no-op라 PNG 폴백(정지 1프레임) 액터와 공존한다.
- `Editor/ArtPipeline/ActorAnimationBake` — Aseprite 태그 → 카탈로그 슬롯 베이크. 배선은
  `IsoPrototypeDemo.AttachActorAnimator`(클립이 없으면 컴포넌트를 안 붙인다).

---

## 12. 아트 파이프라인 & 툴링 (Editor)

- **ProjectCAsepritePipeline**(`AssetPostprocessor`) — 임포트 규격을 강제하고 정식 파일명의 첫 프레임을
  `ProjectCEnvironmentCatalog`에 자동 연결한다. `Art/Runtime` PNG는 원본이 없는 슬롯의 폴백이고,
  **두 아트 레짐이 스프라이트별 PPU로 같은 월드 크기에 공존한다**(규격 수치는 `docs/ART_PIPELINE.md`).
- **에디터 도구는 어셈블리 둘로 갈린다**(§2). `Editor/ArtPipeline/`(asmdef 있음): 위 파이프라인 ·
  `ProjectCArtPivots`(피벗 SSOT — 파이프라인과 PNG 임포터가 **같은 값을 공유**해야 스프라이트가 반 칸
  어긋나지 않는다) · `ActorAnimationBake`(§11.4). `Editor/` 루트(asmdef 없음, 기본 어셈블리):
  `IsoPrototypeSceneBuilder`/`MainMenuSceneBuilder` · `PlayFromMainMenu` · `ArtStyleCapture` ·
  `ProjectCArtImporter`.
- `Tools/ArtPipeline/*.py` — 아트 **후처리**(팔레트 잠금·시트 슬라이스·9-slice). 절차 생성 폴백 아트만 `generate_*`.

---

## 13. 테스트 → [`CLAUDE.md`](../CLAUDE.md)

테스트를 **어떤 경로로 돌리는지**(Core shim / 에디터 회귀)와 그 경계는 `CLAUDE.md`와 `/test` 스킬이
SSOT다. 배치 관점으로 남길 것은 하나 — **EditMode 테스트가 규칙 클래스와 1:1로 매핑**되고
(`ShadowcastFovTests`·`ProceduralDungeonTests`·`FallRulesTests`…) 그중 `ProceduralDungeonTests`가
생성기 구현과 무관한 **불변식 계약**을 진다(§7.4). PlayMode는 격리 개발 프로필에서
`MainMenu→Hub→폐병원→보스 처치→옥상 출구 정복` 전 구간 스모크다.

---

_세부 규칙이 코드와 어긋나면 코드가 정답이고, 설계 의도가 어긋나면 `GDD.md`가 정답이다._

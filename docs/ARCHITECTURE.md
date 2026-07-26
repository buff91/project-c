# 코드 아키텍처 지도 (Architecture)

> **목적**: 코드베이스 전체 구조와 주요 알고리즘을 한 곳에 정리한 인수인계/참조 문서.
> 설계 결정의 최종 출처는 여전히 `GDD.md`(SSOT)와 `docs/SYSTEMS.md`다. 이 문서는 "무엇이 어디에
> 있고 어떻게 동작하는가"를 코드 관점에서 압축한다. 수치·공식은 코드에서 직접 확인해 옮겼다.

관련 문서: [`GDD.md`](../GDD.md) · [`SYSTEMS.md`](SYSTEMS.md) · [`ROADMAP.md`](ROADMAP.md) ·
[`UI_ARCHITECTURE.md`](UI_ARCHITECTURE.md) · [`ART_PIPELINE.md`](ART_PIPELINE.md)

---

## 1. 한눈에 보기

| 항목 | 값 |
|---|---|
| 장르 | 아이소메트릭 다층(elevation) 던전 크롤러 (턴제 로그라이트, 현재 PC 우선 검증) |
| 엔진 | Unity 6000.5.4f1 · C# · 2D Isometric · URP 17.5 · Input System 1.19 |
| C# 규모 | 계속 변하는 실측치는 `docs/CODE_STRUCTURE.md`와 소스 트리를 따른다 |
| 테스트 | Core shim + EditMode 규칙 테스트 + PlayMode 실제 씬 스모크 |
| 어셈블리 | `ProjectC.Core`, `ProjectC.Gameplay`, `ProjectC.ArtPipeline.Editor`, `ProjectC.Tests.EditMode`, `ProjectC.Tests.PlayMode` |
| 씬 흐름 | `MainMenu`(0) → `Hub`(1) → `IsoPrototype`(2) |

### 핵심 설계 기둥 (모든 판단 기준)

1. **입체 공간(Verticality)** — 층 간 + 한 층 내 높이차(elevation). 낙하는 상호작용의 하나.
2. **상호작용 & 상태이상** — 화상/빙결/폭발 + 요소 반응(불+기름, 물+빙결).
3. **제한된 시야(FOV)** — Recursive Shadowcasting, 안개 3상태.
4. **파밍 & 조합** — 자원 수집 + 조합 + 메타 프로그레션.

---

## 2. 아키텍처 규칙 (준수 대상)

- **로직 ↔ 비주얼 분리 (최상위 규칙).** `Scripts/Core`는 순수 C#다. `UnityEngine` 의존은
  `IsoGrid`(Vector2/Mathf만)와 직렬화 클래스의 `[Serializable]` 정도로 최소화한다. Core는
  **의도(intent)·수치·좌표 목록**만 반환하고, 씬/스프라이트/연출 변경은 전부 `Scripts/Gameplay`가 한다.
- **정렬(Sorting) 규칙은 `IsoGrid` 한 곳에** 집중 — floor(elevation) 우선 + (x+y). 흩뿌리지 않는다.
- **입력 추상화** — 터치/마우스/키보드를 입력 레이어(`IsoTapInput`)에서 `GridPos`/액션으로 통일. 게임 로직에 플랫폼 분기 금지.
- **데이터 중심** — 밸런스 수치는 정적 카탈로그(`MonsterRoster`/`ItemCatalog`/`SurvivorProfile`/`DungeonCatalog`)에.
- **성능** — "보이는 층 ≠ 활성 층". 시뮬레이션은 플레이어가 있는 한 층만. 몬스터는 활성 반경 밖이면 휴면.

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

**Gameplay 안에서도 같은 원리를 쓴다.** UnityEngine 타입이 필요해서 Core로 못 내리는 코드라도,
게임 상태를 몰라도 되면 씬 참조 없는 별 타입으로 뺀다. 절차 생성 임시 아트가 그 예다 —
`PrototypeSpriteCanvas`·`PrototypePalette`·`PrototypeActorSprites`·`PrototypeEnvironmentSprites`는
`Texture2D`/`Sprite`를 다루므로 Gameplay에 남지만 격자·던전·플레이어를 참조하지 않고,
필요한 사실은 `TileVisualFacts`로 받는다. "Unity에 의존한다"와 "게임 상태에 의존한다"는
**다른 축**이고, 후자를 끊는 것만으로 신(神) 클래스의 성장이 멈춘다.

---

## 3. 디렉터리 구조

```
Assets/_Project/
  Art/Source/Aseprite/   # 최종 픽셀아트 SSOT (.aseprite 원본)
  Editor/                # ArtPipeline(Aseprite 임포트 규격·Catalog 자동 연결), 씬 빌더, PlayFromMainMenu
  Scripts/Core/          # 순수 C# 로직 — 아래 §4~§9
  Scripts/Gameplay/      # MonoBehaviour + 서비스 — 아래 §10
  Tests/EditMode/        # 규칙별 *Tests.cs
  Tests/PlayMode/        # 실제 씬 흐름 통합 스모크
  Scenes/                # MainMenu.unity, Hub.unity, IsoPrototype.unity
  UI/                    # MainMenuHUD, HubHUD, PrototypeHUD.Mobile/Desktop, DisplaySettings, DesignSystem.uss
docs/                    # 에이전트 참조 문서
Tools/ArtPipeline/       # 플레이스홀더 스프라이트 생성 파이썬 스크립트
```

---

## 4. 좌표·공간 기반 (Spatial Foundation)

논리 좌표는 `GridPos { int x, y, elevation }`이며 코드 전체의 딕셔너리 키다.

### 4.1 GridPos — 좌표 규약
- `x`, `y` = 평면 좌표. `elevation` = **연속 높이값(층 번호 아님)**. 한 층 안에도 높이차가 있다.
- 방향 규약: `North=+y`, `South=-y`, `East=+x`, `West=-x`. `Offset(dx,dy)`는 elevation 유지.
- 거리: `ManhattanTo = |dx|+|dy|`, `ChebyshevTo = max(|dx|,|dy|)` — **둘 다 elevation을 무시**한다.
- 해시: `17` 시드 · `31` 승수 다항식 해시 (딕셔너리 키 분포용).

### 4.2 TileData / TileKind — 타일 의미론
`TileKind` 13종과 그로부터 파생되는 술어(predicate)가 **이동·시야·낙하의 불변식**을 만든다:

| 술어 | 정의 |
|---|---|
| `IsSolidGround` | Floor, Stairs, Ladder, StairsUp/Down, DoorClosed/Open, SecretDoor/Passage (설 수 있는 바닥) |
| `IsWalkable` | `(IsSolidGround && ≠DoorClosed && ≠SecretDoor) \|\| WeakFloor` |
| `BlocksSight` | Wall, DoorClosed, SecretDoor |
| `CausesFall` | Empty, Hole |

> 핵심 함정: `WeakFloor`/`Hole`/`Empty`는 **SolidGround가 아니다** — 그래서 낙하가 이들을 관통한다.
> `DoorClosed`/`SecretDoor`는 solid ground이지만 walkable이 아니고 시야도 막는다.

`TileData`는 추가로 `oiled`(기름)/`wet`(물) 요소 플래그를 가진다.

### 4.3 GridMap — 희소 타일 저장 + 링크 그래프
- `Dictionary<GridPos,TileData> _tiles` (희소) + `Dictionary<GridPos,List<GridPos>> _links`(명시적 연결).
- **두 종류의 인접성이 공존**한다: (1) 공간 4-이웃(±1 elevation은 계단/사다리에서만), (2) `Connect()`로 만든
  명시적 링크(층 전환 StairsUp↔Down, 사다리, 포탈). `Remove()`는 역링크까지 지워 그래프를 대칭 유지.
- **`FindLandingBelow(from, minElevation)`** — 모든 낙하/수직 시야의 원시 연산:
  같은 (x,y) 컬럼을 아래로 훑어 첫 `IsSolidGround`를 반환, 없으면 `null`(무저갱).

### 4.4 IsoGrid — 아이소 투영 + 정렬 (SSOT)
상수: `tileWidth=1.0`, `tileHeight=0.5`(2:1 다이아몬드), `elevationStep=0.25`, `elevationSortBand=1000`.

- **투영** `GridToWorld`: `wx=(x'−y')·½W`, `wy=−(x'+y')·½H + elevation·0.25` (x'는 시점 회전 적용 좌표).
- **역투영** `WorldToGrid(world, elevation)`: 호출자가 elevation 평면을 지정(위→아래로 후보 시도).
- **정렬** `SortingOrder = elevation·1000 + round((x'+y')·16)` — **elevation이 우선**, 같은 층은 (x+y)가 클수록 앞.
  세부 정렬은 `SortingOrder(pos)·8 + microOffset`(−3..+3, 바닥 데칼을 캐릭터 뒤로 등).
- **시점 회전**: 카메라를 돌리지 않고 `viewQuarterTurns(0..3)`로 (x,y)를 피벗 기준 90° 투영.
  `GridToWorld`/`WorldToGrid`/`SortingOrder`가 같은 회전값을 공유한다.

`GridSortingObject`(Gameplay)가 이 규칙을 씬 스프라이트에 적용하는 유일한 지점 — "아이소 정렬 지옥" 방지.

### 4.5 DungeonHeightModel — elevation ↔ (floorIndex, localHeight)
- `ElevationsPerFloor`(기본 4)가 elevation 축을 던전 층으로 분할. B1=floor 0(e0..e3), B2=floor −1(e−4..e−1)…
- `FloorIndex(e)` = **음수 안전 내림 나눗셈** (`e/stride`, 나머지가 음수면 −1 보정 — C# `/`는 0 방향 절삭이라 필수).
- `LocalHeight(e) = e − floorIndex·stride` ∈ `[0,stride)`. `SameFloor(a,b)` = 두 floorIndex 동일.
- `DungeonVisualContext`가 `FloorIndex`/`DepthIndex`/`Elevation`/`LocalHeight`를 분리 제공 —
  비주얼 카탈로그가 raw elevation의 부호로 깊이를 추론하지 않게 한다.

> **진행 지수와 고도는 분리되어 있다.** `DungeonFloorInfo.ProgressIndex`는 생성기가 경로 순서대로
> 부여하는 1급 데이터이고, 난이도·구간·휴식처·탈출구·드랍·보스 판정은 이 값을 사용한다.
> `FloorIndex`/`Elevation`은 공간 배치·정렬·시야·낙하 전용이다. 과거의
> `DepthIndex = Max(0, -floorIndex)` 역산과 `DungeonDepthBandRules.ForFloor`는 상승 던전에서
> 모든 진행을 0으로 만들던 결함이어서 제거됐다. 내부 호환 이름 `DepthIndex`/`Shallow`/`Deep`을
> 보더라도 고도나 사용자 표시명으로 해석하지 않는다.

---

## 5. 시야 (Field of View)

### 5.1 GridVisibility — Recursive Shadowcasting
- (x,y) 2D **8옥탄트** 캐스팅(Björn Bergström식). 반경은 체비셰프. 결과는 보이는 **표면 GridPos** 집합.
- **표면 판정** `TryGetSurface`: 컬럼의 `maxElevation→minElevation`을 훑어 존재하는 **최상단 타일**을 표면으로.
- **불투명 규칙** `IsOpaque = 표면 없음(void) OR tile.BlocksSight`.
  - **void(타일 부재) = 불투명** — 이 던전은 방 경계를 벽이 아니라 **타일 부재**로 표현하므로(벽은 비주얼 전용),
    이 규칙이 "닫힌 문 뒤 방 = Unknown" 불변식의 핵심이다.
  - 닫힌 문/벽/비밀문은 그 칸까지만 보이고 너머 차단. Hole/약한 바닥/계단/열린 문은 투과
    (`CombatRules.HasLineOfSight`와 동일 기준 → 원거리/폭탄과 시야 일치).
- 3상태(Unknown/Explored/Visible)의 "Visible"만 계산한다. Explored 누적/렌더 정책은 호출자(Gameplay)와
  `FloorVisibilityRules`가 담당.

### 5.2 FloorVisibilityRules — 무엇을 그릴지
순수 boolean 정책:
```
debugAll                → 전부 그림
tileFloor == activeFloor → visible || explored
그 외 층                 → verticalPreview (Hole 국소 미리보기일 때만)
```

> **미리보기 집합도 FOV로 만든다** (`IsoPrototypeDemo.Visibility`): 반대편 층의 elevation 대역에서
> `GridVisibility.Compute(center, …, verticalPreviewRadius)`를 한 번 더 돌린 결과를 쓴다.
> 예전에는 착지점 중심 체비셰프 박스를 전부 넣어 **차폐를 아예 보지 않았고**, 벽 뒤와 닫힌 문 뒤
> 방까지 드러나 바로 위 "void=불투명 / 닫힌 문 뒤 방 = Unknown" 불변식과 충돌했다.
> 반경(1~6, 기본 4)은 남기되 이제 **박스 크기가 아니라 FOV 사거리**이며, 플레이어 시야(6)보다 짧다.
> 비용은 층 하나 FOV 1회.

> **3D(높이 인식) 시야선 — 완료(1·2·3단계).** 전투 LoS는 높이 보간(복셀 차폐), 수평·경사·수직·
> 개구부·근접 도달 기하는 `SightRules`로 통합(`VerticalOpeningRules` 흡수), FOV 셰도우캐스팅의
> 컬럼 해석은 `SightRules.ViewColumn` 위임 — 컬럼을 span(지면 + 머리 위 구조물)으로 본다.
> void=불투명·렌더≠시뮬 불변식은 그대로다. 남은 것은 이 토대 위의 입체 전투 콘텐츠.

---

## 6. 경로·이동 (Pathfinding & Movement)

### 6.1 GridPathfinder — 균일 비용 탐색(사실상 Dijkstra)
- A* 골격이지만 **휴리스틱 = 0**(명시적 링크가 elevation/xy를 임의로 점프하므로 최단 보장을 위해 Dijkstra 형태).
  비용 = **스텝당 1**(계단·링크 점프 포함). **4방향만**(대각선 없음). open set은 선형 스캔 `List`(작은 격자라 충분).
- 이웃 생성 `EnumerateNeighbors`: 4방향 × elevation delta{−1,0,+1}. **높이 변화(±1)는 현재/후보 타일이
  `Stairs`일 때만** 허용 — **`Ladder`는 여기 없다.** 이후 `LinksFrom(current)`의 명시적 링크
  (층 전환·사다리)를 비용 1로 추가한다.
- `openClosedDoors` 플래그: 몬스터 추격이 닫힌 문을 통과 경로로 계획하게 함.
- `canClimb` 플래그(**기본 true**): false면 `IsLadderLink`(양 끝 중 하나가 `Ladder`)인 링크를 건너뛴다.
  기본값이 true인 이유는 호출부 대부분이 플레이어 이동/도달성 검사라 그쪽이 정상값이기 때문이고,
  덕분에 기존 도달성 불변식이 그대로 통과했다. **몬스터만 자기 `MonsterArchetype.CanClimb`를 넘긴다.**
  층 전환 계단 링크는 어느 쪽 끝도 사다리가 아니라 걸리지 않는다(걸리면 못 오르는 적이 자기 층에 갇힌다).

### 6.2 TravelRules — SPD식 자동 이동 게이팅
- `AllowedSteps` = 적이 시야에 있으면 탭당 **1스텝**, 없으면 경로 전체.
- `Evaluate` 인터럽트 우선순위: **피해 > 새로 보인 적 > 새로 보인 아이템** (`TravelInterrupt` enum 값 = 우선순위).

### 6.3 VerticalTraversalRules — 층 전환/사다리
- `TryGetAutomaticFloorDestination`: 밟은 타일이 `StairsUp/Down`이고 링크가 있으면 즉시 링크 목적지로
  (진입 = 한 행동으로 층 전환). Stairs/Ladder/Hole은 자동 전환 대상 아님.
- `LadderWorldHeight` = 실제 elevation 차 × step + 타일 높이 35% 겹침, 하한 0.28 — 사다리 비주얼을 실제 단차에 맞춤.
- **사다리는 계단과 다르다**: 계단 ±1 단·걸어서(A\*) / 사다리 여러 단·**링크로만**·오를 수 있는 종만.
  사다리 칸에 **걸어 올라서는 것은 그대로** 되고 막히는 것은 "타고 오르기"다.
  캐치워크 층에서는 `PlaceCatwalk`이 바닥(+0)↔캐치워크(+2)를 잇고 중간 발판(+1) 링크는 끊는다
  (`Disconnect` 후 `Connect`) — 계단이 ±1만 담당하므로 이 대비가 "높은 곳은 사다리로만"을 성립시킨다.

### 6.4 StairTopology — 계단 착지 지점
- `TryGetHigherLanding`: `Stairs` 타일의 4방향 중 `elevation+1`이며 walkable인 첫 칸(방향 배열 순서 우선).

---

## 7. 절차적 던전 생성 (Procedural Generation)

`DungeonGenerator.Generate(map, w, h, floorCount, elevationsPerFloor=4, seed=1977)` — **아직 BSP도 랜덤워크도 아니다 — 의도된 발판(§7.4).**
**축 정렬 3방 고정 템플릿 + 1칸 복도**를 유지한 채 단일 `System.Random(seed)`가 모든 치수를 흔든다.
같은 seed = 같은 던전(그리기 순서가 고정이라 재현 보장).

### 7.1 층 템플릿
```
        ┌─────────── 북쪽 방 (적 스폰, 계단/사다리로 뒤쪽 한 단 높음, Hole 후보) ──────────┐
        │   ▲ Stairs(걸어서)   ▮ Ladder(링크)         ○ Hole → 정확히 한 층 아래       │
(NW 분기 방·옵션) ─ SecretDoor?         │ (세로 복도 + 문)                             │
        └──────────── [문] ────────────┴──────────────────────────────────────────────┘
  ┌── 남서 입구 방 (Entry/StairsUp) ──┐ [문]  ┌── 남동 방 (아이템, StairsDown) ──┐
  │           (물 웅덩이 후보)         │──복도─│                                  │
  └───────────────────────────────────┘       └──────────────────────────────────┘
```
- 방 사이 최소 1칸 간격 → 연결은 반드시 **문**을 통과(방 밀봉 불변식). 문은 `DoorClosed`로 생성.
- 북쪽 방 X 범위는 **윗층과 겹치도록 제약** → Hole 착지 컬럼이 항상 존재.
- 뒤쪽 한 줄(`RaisedY`)은 `elevation+1`로 올리고 `Stairs`(걷기)와 `Ladder`(링크 `Connect`)로 연결.
- 층 전환 샤프트 `StairsUp/Down`은 depth 홀짝에 따라 좌/우 컬럼을 번갈아(같은 타일에 겹치지 않게).
  각 층 `Down` ↔ 다음 층 `Up`을 `map.Connect(...)`로 양방향 연결. **바닥 층 Down은 링크 없음 = 다음 던전 출구.**

### 7.2 생성 파이프라인 (순서 중요 — 뒤 패스가 앞 타일 상태를 읽음)
1. **비밀 층 선택** `PickSecretDepths` — `SecretRoomRules.DesiredCount`(10층=3)만큼 B10 제외 깊이에서.
2. **층별 계획+카브(위→아래)** — 방/복도/문/올림 바닥/계단/사다리/샤프트. NW 분기 방(옵션, 비밀이면 `SecretDoor`).
3. **개구부 + WeakFloor**(0..N−2층, 모든 층 카브 후) — 앵커 후보는 `LandsOneFloorBelow`(정확히 한 층 아래
   walkable 착지, 윗층 **개구부 전체** 컬럼 제외, 2층 관통 방지)를 만족.
   **앵커에서 한 칸씩 자란다(상한 3)** — 같은 조건 + `KeepsUpperRoomConnected`(플러드 필로 방이 잘리는지 검사)를
   통과하는 인접 칸만 붙이고 없으면 멈춘다. 2×2 같은 모양을 강제하지 않는 이유는 최소 크기 던전에서 북쪽 방
   밴드가 얕고 Y축 층간 겹침이 보장되지 않아(X축만 제약) 후보가 0이 되거나 도달성이 깨지기 때문이다.
   **성장 루프는 난수를 쓰지 않아 RNG 스트림이 그대로다**(앵커 1 + WeakFloor 1). WeakFloor는 개구부
   **둘레**의 4방 인접 중 같은 조건 — 밟으면 개구부가 되므로 **같은 판정 함수를 공유**한다.
   결과는 `DungeonFloorInfo.HoleTiles`(1급 목록)이고 `Hole`은 대표 칸(샤프트 연출·엘리베이터 충돌용).
4. **프롭·스폰** — 휴식처(`DungeonRestRules.ShouldPlace`: B4/B7) → 물 웅덩이(50%, 랜덤워크 2~4칸) →
   적 스폰(문 뒤 북쪽 방만, `1+rand+depth/2+면적보너스`, **하행 계단 경비병** `1+depth` 추가) →
   아이템(`RollKind` 18분모 분포, 분기 방 보상 보장).
5. **조립** — `DungeonFloorInfo` → `DungeonLayout`.

### 7.3 관련 규칙 클래스
- **SecretRoomRules** — `DesiredCount`(≥8층=3), `CanInvestigate`(같은 elevation·맨해튼 1),
  `TryReveal`(`SecretDoor`→`SecretPassage` in-place, 멱등), `RevealInBlast`(3×3).
  비밀 분기 보상은 결정적: **B4+ Relic, 그 외 Gemstone**.
- **DungeonBossRules** — `TrySelectSpawn`(입구에서 맨해튼 최대 후보), `CanUseExit`(보스 없거나 처치 시).
  진행 최종 층(폐병원 8F)의 `감시자`(코드 ID `grave-warden`).
- **DungeonCatalog** — `폐병원`(코드 ID `forgotten-catacombs`, seed 1977, 10층, 상승, 감시자)만 available.
  `침수된 금고`/`잿불 성채`는 `isAvailable: false`. `ById`는 없으면 `All[0]` 폴백.
- **AreaSpawnBonus** = `max(0,(w·h−121)/60)` — 면적 비례 스폰 스케일.

### 7.4 발판 → BSP 전환 (확정 목표)

현재의 3방 고정 템플릿은 **버릴 임시 코드가 아니라, 다층 던전의 생성 불변식을 먼저 못박기 위한 발판**이다.
최종 목표는 `GDD.md` §5.8의 **진짜 룸-앤-코리더/BSP 생성기**이며(근거: `DungeonLayout.cs:82` 주석,
`SYSTEMS.md`의 "이후 확장(진짜 룸-앤-코리더/BSP)에서도 이 연결 그래프와 생성 불변식을 유지한다"),
교체되는 것은 **생성 알고리즘뿐**이다.

- **불변식이 계약이다.** BSP로 갈아끼워도 아래는 `ProceduralDungeonTests`가 그대로 강제해야 한다 —
  문 밀봉(열면 도달·닫으면 북쪽 방 차단), Hole은 정확히 한 층 아래 walkable 착지(2층 관통 금지),
  층 간 기둥 겹침으로 착지 컬럼 보존, 적은 문 뒤에만·하행 계단 경비·분기/비밀 방 보상.
- **난도 핵심은 층-간 정렬.** 각 층을 독립 BSP로 돌리면 "구멍이 아래층 진짜 바닥에 떨어진다"가 공짜로 나오지
  않는다. 낙하·층간 시야·비밀방·AI가 전부 이 정렬에 의존하므로, BSP 분할 결과 위에서 이 정렬을 보장하는
  방식이 전환 설계의 핵심이다.
- **권장 선행 작업**: 불변식 테스트를 제너레이터 구현과 분리(계약 테스트화)해 안전망을 먼저 확보.
- 추적: `ROADMAP.md` → "향후 기술 과제 — 던전 생성기 BSP/룸-앤-코리더 전환".

---

## 8. 입체 공간 & 낙하 (Verticality & Falling)

### 8.1 FallRules — 모든 낙하의 수렴점
- **낙뎀 공식**: `floors≤0 ? 0 : floors·(floors+1)` → 1층 **2**, 2층 **6**, 3층 **12**, 4층 20, 5층 30. 플레이어/몬스터 동일.
  - **향후(확정): 높이 기반으로 전환(가속 곡선)** — 지금 층 값(1층 2·2층 6·3층 12)을 유지하되 층 안 낙하도 데미지.
    `eff = max(0, 낙차칸 − SafeFallHeight)`, `데미지 = round(eff/4 × (eff/4+1))`, 기본 SafeFallHeight 0. `ROADMAP.md` 참조.
- `TryFall` 파이프라인: `FindLandingBelow`(없으면 null·낙하 안 함) → `floorsFallen` → 낙뎀 →
  **착지 충돌**(착지칸 산 점유자에 같은 피해, `CrushedOccupant`) → 점유자 생존 시 낙하자는 인접 빈 칸으로 밀림.
- **연쇄 낙하는 재귀 아님** — 한 호출은 첫 단단한 바닥까지. 여러 층 관통은 `floorsFallen>1`로 표현(void 컬럼 위 낙하).

### 8.2 KnockbackRules — 폭발 넉백
- `PushDirection`: `target−center`의 우세 축 1칸(동률은 x 우선). `Resolve`:
  벽/닫힌 문/점유 → `None`, walkable → `Pushed`, 구멍/void → `PushedIntoFall`(호출자가 `TryFall`로 이음).

### 8.3 SightRules — 시야·도달 판정의 단일 출처
- `HasLineOfSight` — 수평·경사는 2D 브레젠험 + 시선 elevation 보간(복셀 차폐), void=불투명.
  같은 컬럼이면 `HasVerticalSight`로 넘긴다.
- `HasVerticalSight` — 낮은 쪽 바로 위부터 **높은 쪽 칸까지** 모두 뚫려 있어야 한다.
  허공(타일 없음)은 통로, 온전한 바닥은 차단(`TileData.BlocksVerticalSight`) — 즉 실제 `Hole`만 층을 잇는다.
  "void=불투명"은 컬럼을 벽으로 읽는 수평 규칙이라 수직에는 적용하지 않는다.
- `CanReachAcross(from, to, maxStepHeight)` — 근접 단차 타격의 기하(평면 인접+높이차). `CombatRules.AreAdjacent`가 위임.
- `ViewColumn(map, x, y, origin, min, max)` → `ColumnView` — 컬럼을 눈높이 기준 **span**으로 해석한다.
  **지면**(눈높이 이하 최고 타일) + **머리 위 구조물**(눈높이 위 첫 타일) + 너머 차단 여부.
  지면 아래 타일은 덮여서 내지 않는다. `GridVisibility` 셰도우캐스팅이 이 판정만 쓴다(3단계).
  눈높이 여유는 `HeightBlockThreshold`(1) — 1단 단차는 안 막고 2단 이상만 막는다.
- `ViewFromFloor` — **오직 `Hole`만** 시야 포털(StairsUp/Down은 아님). 관찰자가 Hole 층이고 보이면 `Downward`,
  착지 층이고 보이면 `Upward`. 개구부↔착지 사이가 실제로 뚫려 있어야 하며 `isVisible` 델리게이트로 FOV를 존중.

### 8.4 VerticalRouteCue — 최초 발견 카드 copy
- `VerticalRouteRole` 6종. `TryCreate(kind, viewedFromBelow, dest)`가 계단=발판·WALK, 사다리=레일·CLIMB,
  층 전환=아치·`Bn ▲/▼`, Hole=깨진 테두리·아래위 시야로 매핑 (7초 비차단 카드).

---

## 9. 전투·상태이상·AI

### 9.1 전투 (CombatantState / CombatRules)
- **스탯 모델**: `Id/MaxHp/AttackPower` 불변, `Hp`는 `TakeDamage`(실제 감소분 반환)/`Heal`(죽으면 0). 방어/치명타 없음.
  사망은 **이벤트 없이** `Hp==0`(`IsAlive`)으로만 표현.
- `AreAdjacent` = 맨해튼 1(대각 비인접) & 높이차 ≤ `MeleeReachHeight`(1) — 기하는 `SightRules.CanReachAcross`가 소유.
  `TryMelee` = 인접 시 `AttackPower` 피해, 위에서 아래로 치면 `DownStrikeBonus`(+1).
- `TryRanged` = `RangedReachCost`(맨해튼+|Δe|) ≤ range & **높이 인식 시야선**(`SightRules`).
  원거리는 별도(더 낮은) 피해로 카이팅 방지, 높이 이점은 사거리 예산으로 과금.
- `FindFiringPosition` = 발사 가능 위치를 맨해튼 다이아몬드로 탐색, **결정적 타이브레이크**(경로 길이→표적 근접→x→y).
- `DiagnoseRanged` 우선순위: 도달 비용(사거리+높이) → LoS.

### 9.2 상태이상 & 요소 반응
- **StatusEffects** — `Burn`(턴당 `BurnDamagePerTurn=1` 고정), `Freeze`(그 턴 행동 스킵) 2종만.
  - `Apply`: **상쇄 우선** — `Burn↔Freeze`는 상호 소멸(`CancelledOpposite`, 둘 다 안 남음). 아니면 **max로 연장**(단축 없음).
  - `Tick`: 감소 **전에** 출력 계산(마지막 틱도 발동) → 감소. `Applied/Refreshed/CancelledOpposite` 결과로 팝업 구분.
- **요소(타일) 반응은 StatusEffects가 아니라 별도 규칙**:
  - **OilRules**(Items.cs): `Splash`(3×3, wet 타일은 기름 거부), `Ignite`(기름 제거·발화).
  - **WaterRules**: `ChainFreeze`(wet 타일 4연결 BFS로 **반경 무제한** 결빙), `Evaporate`(3×3 국소 건조).
- **CombatPresentationRules** — source 문자열을 `Physical/Fire/Frost/Heavy`로 분류하고 FX 수치(플래시 펄스,
  버스트 광선 수 6~12, 카메라 흔들림 0.025~0.065)를 제공. 로직 없음, 순수 룩업.

### 9.3 턴 파이프라인 (TurnManager)
- **엄격 2단계**(에너지/속도 없음): `Player` → `Enemies`. `TurnNumber`는 적 페이즈 완료 시 +1.
- 전체 파이프라인: **활성화 → 상태이상 틱 → Decide → 실행**. 휴면(컬링) 중이면 틱도 멈춘다.

### 9.4 몬스터 AI (MonsterBrain) — Behavior Tree
- **아키텍처: Behavior Tree** (2026-07-25, 손으로 쓴 FSM에서 이관 — 콘텐츠가 늘어도 분기 가독성 유지).
  경량 프리미티브 `BehaviorNode/Selector/Condition/Leaf`(즉시 결정형: `Tick` → 행동 or null,
  `BehaviorTree.cs`). `Decide`는 우선순위 Selector 트리를 돌고 **의도**(`Wait/Step/OpenDoor/Attack`)만
  반환. 새 행동은 `When/Do` 가지로 선언적으로 추가한다. FSM 상태 `MonsterMood { Patrol, Chase, Flee }`는
  블랙보드로 유지(트리의 부수효과 노드가 매 틱 갱신). 공개 API·동작은 이관 전후 완전 동일(테스트가 오라클).
- **트리 우선순위**: 사망→대기 · **불붙으면 물로 도주해 소화**(정상보다 우선) · 지각/기분 갱신 · 도주 · 추격 · 순찰.
- **지각 = 플레이어 FOV의 대칭**(`SeenByPlayer` 콜백) + 어그로 반경(체비셰프). 높이 인식 `HasLineOfSight`가
  아니라 FOV 대칭을 쓰는 이유: 계단/단차 위 플레이어에도 지각이 끊기지 않게.
- **Chase**: 인접(높이차 ≤ `MeleeReachHeight` 단차 포함)이면 Attack, 아니면 공격 위치 경로(보이면)/마지막
  목격점 수색(안 보이면). **추격 중에만 닫힌 문 개방**. 크로스-층 스텝 거부.
- **Flee**: `FleeThreshold` 아래면 체비셰프 최대화 후퇴, 궁지(안전한 후퇴 없음)면 반격.
- **Patrol**: 결정적 RNG(항상 1회 draw), `PatrolRadius` 내 배회.
- **위험 회피(공통)**: 순찰·도주·추격 경로 모두 **약한 바닥(밟으면 붕괴→낙하)을 자진 회피** — 낙하는
  플레이어의 밀기/넉백으로 유도하는 게 정석(GDD §5.3).

> **프레젠테이션 아키텍처 결정 (2026-07-25, 구현은 Unity 세션):**
> - **캐릭터 애니메이션 = Aseprite 클립 파이프라인** (현행 절차적 트윈 `CombatStatusFxAnimator`를 정식 `.aseprite`
>   애니 프레임 → `AnimationClip` → Animator로 대체). 상태 FX 정도의 미세 연출은 절차적 유지 가능.
> - **UI 인터랙션 = DOTween** — 단 화면 UI는 **UI Toolkit**이라 DOTween이 직접 안 붙는다: UGUI/월드 UI엔
>   DOTween, UI Toolkit VisualElement엔 `experimental.animation`/USS transition을 쓴다.

### 9.5 로스터·활성화
- **MonsterRoster** — 약탈자(Goblin, HP5·공2·도주0.3)/낡은 경비 드론(Skeleton, HP8·공2·비도주)/
  누출 오염 슬러지(Slime, HP3·공1·넓은 배회)/**투석 약탈자(Slinger, HP4·근접1·원거리2·사거리4·유지2)**/
  감시자(GraveWarden, HP20·공3). `PickForDepth`가 밴드별 확률 혼합(깊을수록 드론·사수 비중↑).
- **원거리 몬스터** — `MonsterArchetype.IsRanged`면 브레인이 `DecideRanged`를 먼저 탄다:
  ① `KeepAwayRange` 안이면 거리 벌리기(도주 규칙 재사용, 막히면 근접) ② `CanFireFrom`이면 사격
  ③ 아니면 `FindFiringPosition`으로 사선 잡는 한 걸음. 셋 다 실패하면 일반 추격으로 흘린다.
  판정은 플레이어와 같은 `CombatRules`(도달 비용에 높이차 포함)를 쓴다.
- **MonsterActivation.IsActive** = **같은 층 && 활성 반경(체비셰프)**. 비활성은 `Decide` 자체를 스킵(모바일 성능 핵심).
- **거리 metric 규약**: 지각/어그로/배회/도주/활성화 = **체비셰프(8방)**, 인접/사거리/실제 이동 = **맨해튼(4방)**. 의도적 비대칭.

### 9.6 프레젠테이션 게이팅
- **EnemyPresentationRules** — `ShouldShowFeedback`(FOV·같은 층만), `IsCorpseExpired`/`CorpseAlpha`
  (시체 수명 3턴, alpha `0.2+0.5·remaining`).

---

## 10. 아이템·인벤토리·조합·메타

### 10.1 아이템 (Items.cs)
- **ItemKind 12종**(값 load-bearing): Potion/Bomb/FrostBomb/OilFlask/ThrowingKnife/RecallScroll(소모품),
  CoinPouch/Gemstone/Relic(전리품, 골드 환산 10/25/60), Herb/BlastPowder/FrostShard(재료).
- **ItemCatalog** — `AllKinds` 순서가 **정본 반복/타이브레이크 순서**(백팩 팩킹, 세이브, 텔레메트리 공용).
  `GoldValue`/`ShopPrice`(재료 < 완성품 유지)/`IsTreasure`/`IsMaterial`.
- **BombRules** — `BlastRadius=1`(3×3), `Detonate`는 **본인 포함** 피해 + 빈 WeakFloor→Hole 붕괴.

### 10.2 백팩 자동 배치 (BackpackRules) — 빈 패킹
- 격자 **6×4=24셀**. footprint: Relic **2×2**, OilFlask/ThrowingKnife/RecallScroll **1×2**, 그 외 **1×1**. 회전 없음.
- `TryCreateLayout` = **결정적·큰 것 우선·행 우선** 그리디:
  1. count → 인스턴스 전개(`AllKinds` 순). 2. 면적 초과 즉시 실패.
  3. 정렬: **면적↓ → 높이↓ → KindOrder↑ → InstanceIndex↑**.
  4. `bool[col,row]` 점유 격자에 각 아이템을 **y 바깥·x 안쪽**(위→아래, 왼→오른) 첫 적합에 배치.
  5. 하나라도 실패 → 전체 실패(null). all-or-nothing.
- **주의**: 면적 통과해도 조각화로 배치 실패 가능(설계상 배치 시점에 false). 같은 아이템 집합은 항상 같은 배치(모바일 안정성).
- `Inventory`는 **종류별 count 스택**(인스턴스 상태 없음). `TryAdd`가 bounded면 매번 `TryCreateLayout`로 검증·롤백.

### 10.3 조합 (CraftingRules)
- 레시피 3종: `약초×2→물약`, `화약×2→폭탄`, `폭탄+서리수정→냉기폭탄`. 순서 무관 매칭.
- `TryCraft`: 재료 소비 → 산출 `TryAdd`. **원자적 롤백** — 백팩이 꽉 차 산출이 안 들어가면 재료 재추가(재료 손실 없음).

### 10.4 출정 로드아웃 (ExpeditionLoadoutRules)
- 창고(무제한 `Inventory`) ↔ 출정 백팩(6×4)의 1개 단위 이동. **전리품은 창고/로드아웃에 못 들어감**(항상 골드).
- `CreateInventory` = 기본 지급품(`SurvivorProfile.StarterCount` — **hero 인자를 받지 않는다**) +
  선택 로드아웃(24셀 예산). `Reconcile`(초과분 창고 복귀),
  `ConsumeLoadout`(던전 진입 시 실제 반입, 초과분 창고로).

### 10.5 세이브·메타
- **RunSaveData**(`[Serializable]`) — 층 체크포인트. **지형/적/아이템은 저장 안 함**(seed로 재생성).
  이어하기 = "현재 층을 층 입구에서 다시 시작". 체크포인트 계약 `dungeonId/stageCount/bossDefeated` + 12종 인벤(전리품 포함).
  `RunStartRules.ResolvePreviewDepth`: 새 판=0, 이어하기=`max(0,−currentFloorIndex)`.
- **MetaSaveData**(`[Serializable]`) — 판 종료(사망 포함)에도 유지되는 은행. `gold`, `records`,
  `unlockProgress`(조건별 역대 최고, 단조 증가) + 투입 기록,
  창고 9종 + 로드아웃 9종(전리품 필드 없음). `TrySpend`(상점), `AwardRecords`/`InvestRecords`.
  옛 `unlockedHeroes`/`heroId`는 **제거**했다 — 옛 세이브는 그 필드를 무시하고 로드되므로 마이그레이션 없음.
- **SurvivorProfile** — 원정자 기본값 상수 하나(HP10·근접3·원거리1·물약1, 옛 기사 값 그대로).
  **직업도 프리셋도 없다** — 정체성은 캐릭터가 아니라 장비가 진다. 옛 `HeroRoster`/`HeroSelection`을 대체.
- **Equipment / ForgeRules** — 무기 1 + 보조 1 슬롯. 장비는 **공격력을 올리지 않고** 규칙만 바꾼다
  (사거리 2·명중 넉백·피해 -1·안전 낙하 +2). 대장간이 골드로 제작하고 슬롯에 끼우며,
  옛 영구 스탯 강화(`SmithyRules`)는 제거했다. 전투 보정은 `CombatLoadout` 한 구조체로 모아
  `CombatRules.TryMelee/TryRanged`가 파라미터로 받는다. 장착 장비는 백팩 공간을 쓰지 않는다.

### 10.6 텔레메트리 (RunTelemetry)
- 순수 데이터/집계(Unity 시간·파일 모름, 스키마 v4). Gameplay가 이벤트+unscaled delta를 먹인다.
- **사중 기록**: 런 총계 + 층별(`RunFloorTelemetry`) + 구간별(`RunBandTelemetry`) + (피해/아이템) 소스별/아이템별.
- 층별 시간·턴·피해·처치·획득·아이템 사용/조합·휴식·숨은 방, 낙하(플레이어/적/의도적),
  화상/빙결 부여, 기름 발화·물 결빙/증발, 치트.
- **구간(밴드) 롤업은 파생 값이다** — `RefreshBands()`가 층별 기록을 `DungeonDepthBandRules`로 다시 묶는다
  (Shallow B1~B3 / Mid B4~B6 / Deep B7~B9 / Boss B10+). 따로 기록하지 않으므로 경계를 바꾸면 과거 리포트도
  같은 규칙으로 다시 묶이고, 저장(`RunTelemetryStore.Save`)·요약 직전에 재계산한다. 방문하지 않은 구간은 넣지 않는다.
- 판 종료 시 `development-profile/telemetry`에 JSON 자동 확정. `RunSummary`는 게임오버/승리 모델(첫 결과 latch).
- **WorldInputRules** — 화면 탭 → 투영 타일 픽킹. 아이소 다이아몬드 히트 테스트(`|dx|/½W+|dy|/½H ≤ 1`),
  우선순위 **LayerPriority↓ → SortingOrder↓ → 중심 근접**(현재 활성 층 → Hole 미리보기 → 렌더 정렬).

---

## 11. Gameplay·프레젠테이션 계층

### 11.1 IsoPrototypeDemo — 오케스트레이터 (관심사별 partial)
던전/허브를 실제로 세우고 굴리는 중심 MonoBehaviour. Core 규칙을 씬·스프라이트·연출로 번역한다.

> 파셜 파일 수·줄 수·정확한 책임 목록은 자주 변하므로 `docs/CODE_STRUCTURE.md`를 SSOT로 삼는다.
> 파셜 분할은 탐색성을 높이지만 상태 결합을 줄이지 않으며, 게임 상태를 몰라도 되는 코드는
> `Prototype*Sprites`처럼 별 타입으로 추출한다.

현재 파셜은 입력·이동·행동·적·낙하·시야·조명·생환·런 수명주기·보스·전투 FX 등으로 나뉜다.
`IsoPrototypeDemo.Sprites.cs`는 픽셀을 직접 그리지 않고 격자 사실을 별도 스프라이트 팩토리에 넘기는
어댑터다. 최신 전체 목록과 줄 수는 `docs/CODE_STRUCTURE.md`를 따른다.

- 내부 에이전트(경량 뷰 홀더): `EnemyAgent`, `ItemAgent`, `RestSiteAgent`, `VerticalLandmarkAgent`.
- 이벤트 다수(`PlayerHpChanged`, `ActiveFloorChanged`, `ExitChoiceRequested`…)로 HUD와 느슨 결합.

### 11.2 씬 얇은 진입점 & 서비스
- **GridManager** — `GridMap`+`IsoGrid` 소유, 좌표 변환 헬퍼. **IsoTapInput** — 입력→`GridPos`/액션(장치 추상화), `ActorPicker` 주입.
- **정적 서비스**: `AtomicJsonStore`(임시 파일 교체 + 백업 복구)·`RunSaveStore`(체크포인트)·
  `MetaStore`(영속 창고)·`DisplaySettingsStore`·`RunTelemetryStore`,
  `DevelopmentSaveProfile`(격리 개발 저장 루트)·`DevelopmentViewportService`(에디터 해상도/모드 강제).
- **씬 라우팅**: `FrontEndFlow`(씬 이름 상수)·`TitleEntryRouting`(타이틀 목적지 + `이어하기`
  노출 규칙)·`DungeonSelection`(선택 던전 전달)·`MainMenuController`.

### 11.3 UI (UI Toolkit 화면 HUD)
- 컨트롤러: `PrototypeHudController`(던전 HUD·액션 휠), `HubHudController`, `InventoryPanelController`(6×4 백팩+조합),
  `DisplaySettingsPanelController`, `DebugPanelController`(치트/텔레메트리 요약).
- **ResponsiveUiLayout** — UI Toolkit엔 런타임 미디어쿼리가 없으므로 패널 논리 크기를 USS 클래스로 변환:
  `is-narrow(<520)`/`is-short(<700)`/`is-landscape`/`is-expanded(짧은 축≥590)`/`is-tall`/`is-ultrawide`.
  `Screen.safeArea`를 패널 좌표로 환산해 노치 대응. 모든 루트에 `ui-touch`/`ui-pointer` 부여.
- **OrthographicCameraFraming** — 월드 경계를 화면비에 맞춰 직교 카메라 중심/크기 계산(허브 고정 구도).
  `Fit = max(minimumSize, halfHeight, halfWidth/aspect)`이고 **minimumSize는 던전 카메라 크기
  (`playCameraSize`)를 그대로 넘긴다** — 전용 필드(`hubCameraMinimumSize`)를 두면 값이 두 벌이 되어
  한쪽이 흘러내려도 아무도 모른다(실제로 허브가 1.4배 확대돼 보이는 버그가 그렇게 생겼다).
  `max` 덕분에 PC 가로에서는 최소값이 지배해 던전과 정확히 같고, 세로로 긴 창에서만 필요한 만큼
  물러난다. 패리티는 `OrthographicCameraFramingTests`가 고정한다.
- 방침: 화면공간 평면 = UI Toolkit, 월드 앵커/추종 = UGUI (상세 `UI_ARCHITECTURE.md`).

---

## 12. 아트 파이프라인 & 툴링 (Editor)

- **ProjectCAsepritePipeline** (`AssetPostprocessor`) — `com.unity.2d.aseprite 5.0.3`. Point/Canvas Pivot/
  무압축/AnimationClip을 강제하고, 정식 파일명의 첫 프레임을 `ProjectCEnvironmentCatalog`에 자동 연결한다.
  `Art/Runtime` PNG는 원본이 없는 슬롯의 폴백이다.
- **최종 월드 아트 레짐**은 128×64/PPU 128이고 `ui-*`는 64를 유지한다. 절차 생성 폴백은
  64×32 레짐으로 남아 있으며 스프라이트별 PPU로 같은 월드 크기에 공존한다. 정확한 임포트 규격은
  `docs/ART_PIPELINE.md`, 파일별 구조는 `docs/CODE_STRUCTURE.md`를 따른다.
- 에디터: `IsoPrototypeSceneBuilder`/`MainMenuSceneBuilder`(씬 자동 구성), `PlayFromMainMenu`, `ArtStyleCapture`.
- `Tools/ArtPipeline/*.py` — 플레이스홀더 스프라이트 생성(AI 소스 확보 시 교체).

---

## 13. 테스트

- **EditMode `ProjectC.Tests.EditMode`** — 규칙별 1:1 매핑(`ShadowcastFovTests`,
  `ProceduralDungeonTests`, `FallRulesTests`, `MonsterBrainTests`, `BackpackRulesTests`, `TravelRulesTests`…).
  생성 불변식(문 열면 전 walkable 도달·문 닫으면 북쪽 방 차단·Hole 정확히 한 층·2층 관통 방지)을 고정한다.
- **PlayMode `ProjectC.Tests.PlayMode`** — 격리 개발 프로필에서 `MainMenu→Hub→폐병원 B2→8F→보스 처치→옥상 출구 정복` 스모크.
- 변경 후에는 숫자를 맹신하지 말고 둘 다 재실행(CLAUDE.md).

---

## 14. 관찰 & 정리 제안 (Observations)

> 현재 구조는 로직/비주얼 분리, 규칙별 SSOT, 데이터 중심 카탈로그가 잘 지켜지는 **일관된 코드베이스**다.
> 아래는 리스크가 낮은 개선 후보로, 즉시 실행보다는 검토 대상이다(범위를 좁게 유지).

1. **`IsoPrototypeDemo` 비대화.** 던전·허브·전투·연출·세이브를 한 클래스가 소유한다.
   partial로 분할돼 있으나 여전히 상태 공유가 넓다. 후속 리팩터 후보: 허브 로직(`_hub*` 필드군)을 별도
   컴포넌트로, 낙하/폭발 시퀀스를 서비스로 추출. **단, 씬 와이어링 리스크가 커서 테스트 보강 후 진행**을 권장.
2. **문서의 변동 수치 최소화.** Unity 버전은 `ProjectSettings/ProjectVersion.txt`, 파일/파셜 지도는
   `docs/CODE_STRUCTURE.md`, 현재 동작은 `docs/STATUS.md`를 SSOT로 삼아 중복 실측치가 다시 낡지 않게 한다.
3. **`DungeonFloorInfo.EnemySpawn`** 단일-적 호환 shim(`EnemySpawns[0]`)은 다중 스폰 전환 잔재 — 사용처 정리 후 제거 검토.
4. **`CombatRules`/`KnockbackRules`/`BombRules`/`OilRules` 등 규칙 클래스가 파일명과 불일치**
   (예: `CombatRules`는 `CombatantState.cs`에). 탐색성만 보면 규칙별 파일 분리가 낫지만, 현재 응집도도 합리적 —
   변경 시 asmdef/테스트 영향 없음.
5. **크로스-층 몬스터 활성화**(보이는 Hole 인접 아래층)는 `MonsterActivation`에 TODO로 남아 있음(설계상 M4 확장 자리).

---

_이 문서는 코드에서 직접 확인한 알고리즘·수치를 옮긴 것이다. 세부 규칙이 코드와 어긋나면 코드가 정답이며,
설계 의도가 어긋나면 `GDD.md`가 정답이다._

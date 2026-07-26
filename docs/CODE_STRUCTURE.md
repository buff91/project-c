# Project-C 코드 구조 (2026-07 리팩토링 후)

> 이 문서는 2026-07 구조 리팩토링으로 정리된 **현재 파일/파셜 레이아웃의 지도**다.
> "어떤 코드가 어디에 사는가"를 빠르게 찾기 위한 참조용이며, 설계 결정의 근거는
> `GDD.md`·`docs/SYSTEMS.md`·`CLAUDE.md`를 따른다.

## 리팩토링 원칙 (이 구조가 나온 이유)

- **로직 ↔ 비주얼 분리**는 그대로: 순수 C#은 `Scripts/Core`, MonoBehaviour/씬 연동은 `Scripts/Gameplay`.
- 3,000줄급 **신(神) 클래스는 우선 `partial` 파일로 분할**했다.
  컴파일러가 파셜을 이어붙이므로 **순수 코드 이동 = 동작 불변**이고, 필드·이벤트를
  모든 파셜이 공유한다. 큰 파일을 관심사별로 나누되 타입 경계는 건드리지 않는 선택이다.
- **다만 파셜 분할로는 결합이 줄지 않는다** — 모든 파셜이 같은 필드에 손댈 수 있어서
  "격자를 안 봐야 하는 코드"가 격자를 본다. 그래서 파셜 수가 19개까지 자란 뒤
  **스프라이트 생성은 실제 별 클래스로 추출**했다(아래 "절차 생성 임시 아트"). 판단 기준은
  *그 코드가 게임 상태를 알아야 하는가*다 — 몰라도 되는 것은 타입 경계 밖으로 내보낸다.
- 흩어진 상수·매핑은 **단일 출처(SSOT)**로 모았다(아래 표 참조).

---

## `IsoPrototypeDemo` — 관심사별 17개 파셜

한 `partial class IsoPrototypeDemo`(MonoBehaviour)를 다음 파일들이 나눠 소유한다.
상태(필드·프로퍼티·이벤트 ~60개)와 방 빌드·수명주기는 본체에, 나머지는 관심사별 파셜에 있다.

| 파일 | 줄수 | 담당 |
|------|-----:|------|
| `IsoPrototypeDemo.cs` | ~1275 | 상태·필드·이벤트·수명주기(Awake/Start/Update/LateUpdate)·방 빌드(BuildPrototype/CreateActorsAndProps)·카메라·공용 헬퍼·`OverlaySorting` 상수 |
| `IsoPrototypeDemo.Debug.cs` | 231 | 디버그 창 전용 치트 API (`DebugGodMode`·`DebugJumpFloor` 등) |
| `IsoPrototypeDemo.View.cs` | 190 | 시점 회전/모드 토글·`ApplyVisualSettings`·`ApplyViewToVisuals`·카메라 구도(허브 auto-fit 최소값 = `playCameraSize`) |
| `IsoPrototypeDemo.Interaction.cs` | 437 | 탭/스텝/인접 상호작용·커넥터 판정·`HandleTileTapped` |
| `IsoPrototypeDemo.Actions.cs` | 412 | 아이템/전투/조합/투척 행동 코루틴(`RangedAttack`·`FireRanged`·`ThrowBomb` 등) |
| `IsoPrototypeDemo.Movement.cs` | 415 | 경로 이동·문/비밀문/낙하 접근·auto-travel·플로어 전환 |
| `IsoPrototypeDemo.RunLifecycle.cs` | 250 | 세이브/체크포인트/이어하기·던전 전환·정산/생환·텔레메트리 종료 |
| `IsoPrototypeDemo.Hub.cs` | 132 | 허브 프롭/포탈 (영웅 프롭·잠금은 제거됨) |
| `IsoPrototypeDemo.Enemies.cs` | 572 | 적 스폰·AI 턴·활성화 |
| `IsoPrototypeDemo.Falls.cs` | 406 | 낙하/넉백/폭발 해소·`ApplyStatusToCombatantsInRegion` |
| `IsoPrototypeDemo.RestSites.cs` | 155 | 휴식 지점(모닥불) |
| `IsoPrototypeDemo.Extraction.cs` | 132 | 비상 탈출구·비상 송출기 렌더와 생환 선택 진입 |
| `IsoPrototypeDemo.BossArena.cs` | 106 | 최심층 제단 렌더·FOV 추종·아레나 접근 전조 알림 |
| `IsoPrototypeDemo.CombatFx.cs` | 453 | 전투/상태이상 연출 |
| `IsoPrototypeDemo.Visibility.cs` | ~1233 | FOV·수직 포털(개구부 미리보기 = 반대편 층 FOV 재계산)·후면 벽·플레이어 가림 |
| `IsoPrototypeDemo.Lighting.cs` | 159 | 지하 어둠·정적 광원·접촉/방향성 그림자 *(main 브랜치 기능, 병합됨)* |
| `IsoPrototypeDemo.Sprites.cs` | 123 | **어댑터** — 격자 질의(`DoorPlaneRisesRight`·`IsSecretDoorHinted`·`VisualContext`)를 풀어 스프라이트 팩토리에 넘긴다. 픽셀은 그리지 않는다 |

> 본체 파일 클래스 요약 주석에 위 목록이 최신으로 유지된다.

## 절차 생성 임시 아트 — `IsoPrototypeDemo` **밖의** 독립 클래스

외부 아트가 없을 때 64×32 규격으로 그리는 런타임 스프라이트. 파셜이 아니라 별 타입이며,
**격자·던전·플레이어를 참조하지 않는다** — 필요한 사실은 인자로 받는다. 이 무지(無知)가
경계를 지키는 장치다. 다시 `IsoPrototypeDemo`로 끌어들이면 신 클래스로 되돌아간다.

| 파일 | 줄수 | 담당 |
|------|-----:|------|
| `PrototypeSpriteCanvas.cs` | 140 | 저수준 드로잉 프리미티브(`NewTexture`·`FillRect`·`DrawThickLine`·`Blend`)와 **64×32/PPU 상수 SSOT**. `using static`으로 끌어다 쓴다 |
| `PrototypeSpriteCache.cs` | 26 | 키 → 스프라이트 캐시. 두 팩토리가 공유한다 |
| `PrototypePalette.cs` | 147 | 던전 역할색 해석 — `IsoVisualCatalog` 슬롯이 있으면 그 값, 없으면 인스펙터 폴백. 그리기 코드는 여기만 묻는다 |
| `PrototypeActorSprites.cs` | 942 | 액터·몬스터·아이템·프롭·랜드마크·FX. 팔레트도 안 쓰고 **캐시만** 의존한다 |
| `PrototypeEnvironmentSprites.cs` | 774 | 타일·벽·문·비밀문·안개·광원 타일. 캐시 + 팔레트 의존 |
| `TileVisualFacts.cs` | 49 | 호스트가 풀어 넘기는 격자 사실 묶음(진행 맥락·전면 여부·평면 방향·비밀문 힌트·허브 여부) |

> 리팩토링 시 픽셀 동일성은 **씬 렌더 지문**으로 검증했다 — `IsoPrototype`/`Hub` 씬을 빌드해
> 생성된 모든 텍스처를 RenderTexture 로 되읽어 해시했고 전/후가 같았다
> (`871de8c9bc421ffe` / `ab629c527bea784c`). 테스트는 이 그림 변화를 잡지 못하므로
> 그리기 코드를 손댈 때는 같은 방식으로 확인한다.

## HUD 컨트롤러 파셜

| 파일 | 줄수 | 담당 |
|------|-----:|------|
| `HubHudController.cs` | 353 | 수명주기·라우팅·메뉴/던전 선택·골드/이어하기 (`hero:` 라우팅 없음) |
| `HubHudController.Vendors.cs` | 386 | 상점·대장간·현상금·기록실 모달(기록 투입 포함) |
| `HubHudController.Preparation.cs` | 444 | 창고·출정 백팩·드래그드롭 엔진 |
| `PrototypeHudController.cs` | 509 | 수명주기·문서 바인딩·컨트롤 콜백·Update·입력·메뉴 |
| `PrototypeHudController.Handlers.cs` | 146 | 데모 이벤트 핸들러(`Handle*`)·미니맵·HP 표시 |
| `PrototypeHudController.ActionWheel.cs` | 199 | 액션 휠 빌드/배치 |
| `PrototypeHudController.EndGame.cs` | 151 | 출구 선택·보스 패널·게임오버 |
| `PrototypeHudController.Labels.cs` | 109 | 라벨 갱신(`Update*Label`)·상호작용 버튼 |

## Core 모듈 정리

- **전투**: `CombatantState.cs`(엔티티만) · `CombatRules.cs`(사거리·피해·`RangedBlockReason`) —
  기존 한 파일에서 규칙을 분리. 시야선·도달 기하 자체는 `SightRules.cs`가 소유하고 `CombatRules`는 위임한다.
- **시야**: `SightRules.cs`(수평·경사·수직 시야선 + 개구부 투시 + 근접 도달 기하 + 컬럼 span
  해석 `ViewColumn`) · `GridVisibility.cs`(옥탄트 셰도우캐스팅 골격만; 컬럼 판정은 위임) —
  옛 `VerticalOpeningRules`는 `SightRules`에 흡수됐다.
- **아이템/상호작용**: `Items.cs`(`ItemKind`·`ItemCategory`·`ItemCatalog`·`Inventory`·`ItemSpawn`) ·
  `ItemStorage.cs`(수량 목록 저장 연산) · `Interactions.cs`(`OilRules`·`BombRules`·`BombResult`).
  **아이템 종류를 늘릴 때 손댈 곳은 `ItemKind` + `ItemCatalog` 뿐이다** — 세이브·창고·로드아웃은
  목록 기반이라 필드를 추가하지 않는다.
- **던전 생성**: `DungeonLayout.cs`(`DungeonFloorInfo`·`DungeonLayout`·`DungeonGenerator.Generate`+헬퍼)
  + `DungeonGenerator.Planning.cs` / `.Carving.cs` / `.Placement.cs` — `partial static class`로 단계 분할.
- **조명**: `GridLighting.cs` *(main 브랜치, 병합됨)*.

---

## 단일 출처(SSOT) 지도 — "여기만 고치면 된다"

| 관심사 | SSOT 위치 |
|--------|-----------|
| 오버레이(UI) 정렬값 | `IsoPrototypeDemo` 중첩 `OverlaySorting` 상수 |
| 타일 픽셀 규격(64×32·PPU 64) | `PrototypeSpriteCanvas` 상수 (`IsoPrototypeDemo`의 동명 상수가 이 값을 참조) |
| 절차 생성 아트의 던전 역할색 | `PrototypePalette` (카탈로그 슬롯 → 없으면 인스펙터 폴백) |
| 저수준 픽셀 드로잉 | `PrototypeSpriteCanvas` (`FillRect`·`DrawThickLine`·`Blend` 등) |
| 월드 정렬 배수·대역 불변식 | `IsoGrid.DepthResolution` / `MicroResolution` |
| 백팩 ↔ 세이브 아이템 수량 매핑 | `RunSaveData.WriteItems` / `AddItemsTo` |
| 몬스터 표시명·피해소스 매칭 | `MonsterArchetype.DisplayName` + `MonsterRoster.MatchSource` |
| 아이템 짧은 라벨(HUD) | `ItemCatalog.ShortLabel` |
| 아이템 표시 정보(이름·설명·가격) | `ItemCatalog` |
| 원소 반응 상태 부여(폭발 후) | `IsoPrototypeDemo.Falls.ApplyStatusToCombatantsInRegion` |
| 원거리 명중 연출 | `IsoPrototypeDemo.Actions.FireRanged` |
| 시야선·수직 개구부·근접 도달 기하·컬럼 span 해석 | `SightRules` (`CombatRules`·`GridVisibility`가 위임) |
| 눈높이 초과 차폐 임계 | `SightRules.HeightBlockThreshold` |
| 수직 시야 차단 여부(타일) | `TileData.BlocksVerticalSight` |
| 깊이 구간 경계·라벨 | `DungeonDepthBandRules` (판정과 `RangeLabel`이 같은 상수 사용) |
| 콘텐츠 변주 수치 (지역 × 깊이) | `DungeonBandProfiles` (지역은 필수 인자 — 기본값 없음) |
| 던전 → 지역 매핑 | `DungeonDefinition.Region` → `DungeonLayout.Region` (생성기·런타임 스폰 공용) |
| 생성기 출력 회귀 | `DungeonGeneratorGoldenTests` 지문 (불변식 테스트가 못 잡는 배치 변화용) |
| 텔레메트리 구간 롤업 | `RunTelemetry.RefreshBands` (파생 값 — 저장·요약 직전 재계산) |
| 보스 접근 전조 문구 | `DungeonBossArenaRules.TryApproachCue` |
| 장비 정의·효과 | `EquipmentCatalog` (전투 보정은 `CombatLoadout`) |
| 장비 제작·장착 | `ForgeRules` (+ `MetaSaveData.equippedWeaponId/GearId`) |
| 아이템 백팩 면적 | `BackpackRules.Footprint` |
| 아이템 분류(소모품/전리품/재료/장비) | `ItemCatalog.CategoryOf` |
| 아이템 수량 저장·복원 | `ItemStorage` (`MetaSaveData.stash/loadout`·`RunSaveData.items` 공유) |
| 플레이어 기본 수치·기본 지급품 | `SurvivorProfile` (영웅 3종/`HeroRoster`를 대체 — 직업 없음) |
| 사다리 등반 가능 여부 | `MonsterArchetype.CanClimb`(기본 false) → `GridPathfinder(canClimb:)`(기본 true) |
| 개구부 칸 집합 | `DungeonFloorInfo.HoleTiles` (대표 칸은 `Hole`; 성장·약한 바닥은 `DungeonGenerator.Carving`의 같은 판정 함수) |
| 기록 적립 공식 | `RunRecordRules.Award` (도달 1 · 개척 3 · 숨은 방 2) |
| 해금 조건 판정·기록 투입 | `ItemUnlockRules.InvestRecords`/`RemainingFor` (UI는 자체 판정을 들지 않는다) |
| 허브/던전 카메라 배율 | `playCameraSize` 하나 (허브 `OrthographicCameraFraming.Fit`의 minimumSize로 전달) |

## 아직 흩어져 있어 통합 후보인 것 (Unity 검증 필요)

리팩토링 감사에서 식별했으나, 순회 순서·타이밍·런타임 입력에 의존해 **테스트 실행 없이는
안전을 증명할 수 없어 보류**한 항목. 반영 시 EditMode/PlayMode로 가드할 것.

- `ItemDefinition` 단일 표(Gold/Shop/Desc/Footprint 스위치 통합) — `ShortLabel`만 선반영.
- 블라스트 3×3 · 젖은 웅덩이 BFS 공용 이터레이터(`ShockRules`/`WaterRules`/`Items`/`SecretRoomRules`).
- 공용 `Tween(duration, step)` 코루틴(수기 애니메이션 ~8곳).
- 입력 소스 추상화(`IsoTapInput`의 `#if` 5중 + HUD 포인터/키보드 헬퍼).
- 에디터 도구 asmdef + 피벗/캡처 중복 제거.

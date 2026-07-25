# Project-C 코드 구조 (2026-07 리팩토링 후)

> 이 문서는 2026-07 구조 리팩토링으로 정리된 **현재 파일/파셜 레이아웃의 지도**다.
> "어떤 코드가 어디에 사는가"를 빠르게 찾기 위한 참조용이며, 설계 결정의 근거는
> `GDD.md`·`docs/SYSTEMS.md`·`CLAUDE.md`를 따른다.

## 리팩토링 원칙 (이 구조가 나온 이유)

- **로직 ↔ 비주얼 분리**는 그대로: 순수 C#은 `Scripts/Core`, MonoBehaviour/씬 연동은 `Scripts/Gameplay`.
- 3,000줄급 **신(神) 클래스는 새 클래스로 추출하지 않고 `partial` 파일로 분할**했다.
  컴파일러가 파셜을 이어붙이므로 **순수 코드 이동 = 동작 불변**이고, 필드·이벤트를
  모든 파셜이 공유한다. 큰 파일을 관심사별로 나누되 타입 경계는 건드리지 않는 선택이다.
- 흩어진 상수·매핑은 **단일 출처(SSOT)**로 모았다(아래 표 참조).

---

## `IsoPrototypeDemo` — 관심사별 17개 파셜

한 `partial class IsoPrototypeDemo`(MonoBehaviour)를 다음 파일들이 나눠 소유한다.
상태(필드·프로퍼티·이벤트 ~60개)와 방 빌드·수명주기는 본체에, 나머지는 관심사별 파셜에 있다.

| 파일 | 줄수 | 담당 |
|------|-----:|------|
| `IsoPrototypeDemo.cs` | ~1150 | 상태·필드·이벤트·수명주기(Awake/Start/Update/LateUpdate)·방 빌드(BuildPrototype/CreateActorsAndProps)·카메라·공용 헬퍼·`OverlaySorting` 상수 |
| `IsoPrototypeDemo.Debug.cs` | 231 | 디버그 창 전용 치트 API (`DebugGodMode`·`DebugJumpFloor` 등) |
| `IsoPrototypeDemo.View.cs` | 178 | 시점 회전/모드 토글·`ApplyVisualSettings`·`ApplyViewToVisuals`·카메라 구도 |
| `IsoPrototypeDemo.Interaction.cs` | 437 | 탭/스텝/인접 상호작용·커넥터 판정·`HandleTileTapped` |
| `IsoPrototypeDemo.Actions.cs` | 412 | 아이템/전투/조합/투척 행동 코루틴(`RangedAttack`·`FireRanged`·`ThrowBomb` 등) |
| `IsoPrototypeDemo.Movement.cs` | 415 | 경로 이동·문/비밀문/낙하 접근·auto-travel·플로어 전환 |
| `IsoPrototypeDemo.RunLifecycle.cs` | 250 | 세이브/체크포인트/이어하기·던전 전환·정산/생환·텔레메트리 종료 |
| `IsoPrototypeDemo.Hub.cs` | 155 | 허브 프롭/포탈/영웅 잠금 |
| `IsoPrototypeDemo.Enemies.cs` | 572 | 적 스폰·AI 턴·활성화 |
| `IsoPrototypeDemo.Falls.cs` | 406 | 낙하/넉백/폭발 해소·`ApplyStatusToCombatantsInRegion` |
| `IsoPrototypeDemo.RestSites.cs` | 155 | 휴식 지점(모닥불) |
| `IsoPrototypeDemo.BossArena.cs` | 106 | 최심층 제단 렌더·FOV 추종·아레나 접근 전조 알림 |
| `IsoPrototypeDemo.CombatFx.cs` | 453 | 전투/상태이상 연출 |
| `IsoPrototypeDemo.Visibility.cs` | ~1137 | FOV·수직 포털·후면 벽·플레이어 가림 |
| `IsoPrototypeDemo.Lighting.cs` | 159 | 지하 어둠·정적 광원·접촉/방향성 그림자 *(main 브랜치 기능, 병합됨)* |
| `IsoPrototypeDemo.Sprites.cs` | 875 | 런타임 스프라이트 — 환경·타일·벽·문·광원 타일 |
| `IsoPrototypeDemo.Sprites.Actors.cs` | 795 | 런타임 스프라이트 — 플레이어·몬스터·아이템·프롭·FX·`GetContactShadowSprite` |
| `IsoPrototypeDemo.Sprites.Primitives.cs` | 128 | 저수준 드로잉 프리미티브(`NewTexture`·`FillRect`·`Blend` 등) |

> 본체 파일 클래스 요약 주석에 위 목록이 최신으로 유지된다.

## HUD 컨트롤러 파셜

| 파일 | 줄수 | 담당 |
|------|-----:|------|
| `HubHudController.cs` | 359 | 수명주기·라우팅·메뉴/던전 선택·골드/이어하기 |
| `HubHudController.Vendors.cs` | 296 | 상점·대장간·현상금·영웅 모달 |
| `HubHudController.Preparation.cs` | 446 | 창고·출정 백팩·드래그드롭 엔진 |
| `PrototypeHudController.cs` | 510 | 수명주기·문서 바인딩·컨트롤 콜백·Update·입력·메뉴 |
| `PrototypeHudController.Handlers.cs` | 146 | 데모 이벤트 핸들러(`Handle*`)·미니맵·HP 표시 |
| `PrototypeHudController.ActionWheel.cs` | 199 | 액션 휠 빌드/배치 |
| `PrototypeHudController.EndGame.cs` | 117 | 출구 선택·보스 패널·게임오버 |
| `PrototypeHudController.Labels.cs` | 109 | 라벨 갱신(`Update*Label`)·상호작용 버튼 |

## Core 모듈 정리

- **전투**: `CombatantState.cs`(엔티티만) · `CombatRules.cs`(사거리·피해·`RangedBlockReason`) —
  기존 한 파일에서 규칙을 분리. 시야선·도달 기하 자체는 `SightRules.cs`가 소유하고 `CombatRules`는 위임한다.
- **시야**: `SightRules.cs`(수평·경사·수직 시야선 + 개구부 투시 + 근접 도달 기하 + 컬럼 span
  해석 `ViewColumn`) · `GridVisibility.cs`(옥탄트 셰도우캐스팅 골격만; 컬럼 판정은 위임) —
  옛 `VerticalOpeningRules`는 `SightRules`에 흡수됐다.
- **아이템/상호작용**: `Items.cs`(`ItemKind`·`ItemCatalog`·`Inventory`·`ItemSpawn`) ·
  `Interactions.cs`(`OilRules`·`BombRules`·`BombResult`) — 데이터와 상호작용 로직 분리.
- **던전 생성**: `DungeonLayout.cs`(`DungeonFloorInfo`·`DungeonLayout`·`DungeonGenerator.Generate`+헬퍼)
  + `DungeonGenerator.Planning.cs` / `.Carving.cs` / `.Placement.cs` — `partial static class`로 단계 분할.
- **조명**: `GridLighting.cs` *(main 브랜치, 병합됨)*.

---

## 단일 출처(SSOT) 지도 — "여기만 고치면 된다"

| 관심사 | SSOT 위치 |
|--------|-----------|
| 오버레이(UI) 정렬값 | `IsoPrototypeDemo` 중첩 `OverlaySorting` 상수 |
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
| 텔레메트리 구간 롤업 | `RunTelemetry.RefreshBands` (파생 값 — 저장·요약 직전 재계산) |
| 보스 접근 전조 문구 | `DungeonBossArenaRules.TryApproachCue` |
| 장비 정의·효과 | `EquipmentCatalog` (전투 보정은 `CombatLoadout`) |
| 장비 제작·장착 | `ForgeRules` (+ `MetaSaveData.equippedWeaponId/GearId`) |
| 아이템 백팩 면적 | `BackpackRules.Footprint` |

## 아직 흩어져 있어 통합 후보인 것 (Unity 검증 필요)

리팩토링 감사에서 식별했으나, 순회 순서·타이밍·런타임 입력에 의존해 **테스트 실행 없이는
안전을 증명할 수 없어 보류**한 항목. 반영 시 EditMode/PlayMode로 가드할 것.

- `ItemDefinition` 단일 표(Gold/Shop/Desc/Footprint 스위치 통합) — `ShortLabel`만 선반영.
- 블라스트 3×3 · 젖은 웅덩이 BFS 공용 이터레이터(`ShockRules`/`WaterRules`/`Items`/`SecretRoomRules`).
- 공용 `Tween(duration, step)` 코루틴(수기 애니메이션 ~8곳).
- 입력 소스 추상화(`IsoTapInput`의 `#if` 5중 + HUD 포인터/키보드 헬퍼).
- 에디터 도구 asmdef + 피벗/캡처 중복 제거.

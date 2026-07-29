# Project-C 코드 구조 (2026-07 리팩토링 후)

> 이 문서는 2026-07 구조 리팩토링으로 정리된 **현재 파일/파셜 레이아웃의 지도**다.
> "어떤 코드가 어디에 사는가"를 빠르게 찾기 위한 참조용이며, 설계 결정의 근거는
> `GDD.md`·`docs/SYSTEMS.md`·`CLAUDE.md`를 따른다. 규칙은 `docs/SYSTEMS.md`,
> 계층·의존 방향은 `docs/ARCHITECTURE.md`가 소유한다 — 여기는 **파일 이름**이 주어다.

> **줄 수를 적지 않는다.** 예전에는 표마다 `줄수` 열이 있었는데 31개 중 20개가 낡았고
> 최대 오차가 297줄이었다. `docs/ARCHITECTURE.md`가 이미 "문서의 변동 수치 최소화"를
> 원칙으로 세웠는데 그 열이 정확히 그걸 어겼다 — 크기가 필요하면 그때 잰다:
> `find Assets/_Project/Scripts -name '*.cs' | xargs wc -l | sort -rn`.

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

## `IsoPrototypeDemo` — 관심사별 19개 파셜

한 `partial class IsoPrototypeDemo`(MonoBehaviour)를 다음 파일들이 나눠 소유한다.
상태(필드·프로퍼티·이벤트)와 방 빌드·수명주기는 본체에, 나머지는 관심사별 파셜에 있다.

> **이 표가 파셜 목록의 SSOT다.** 예전에는 본체(`IsoPrototypeDemo.cs`) 클래스 요약 주석이
> 같은 목록을 들고 있었고 이 문서가 "주석이 최신으로 유지된다"고 보증했는데, 파셜이 늘 때마다
> 한쪽만 갱신돼서 결국 주석에는 14개만 남아 있었다. 목록을 두 벌로 유지하는 대신
> 주석을 **이 문서를 가리키는 포인터 한 줄**로 줄였다.

| 파일 | 담당 |
|------|------|
| `IsoPrototypeDemo.cs` | 상태·필드·이벤트·수명주기(Awake/Start/Update/LateUpdate)·방 빌드(BuildPrototype/CreateActorsAndProps)·카메라·공용 헬퍼·`OverlaySorting` 상수 |
| `IsoPrototypeDemo.Debug.cs` | 디버그 창 전용 치트 API (`DebugGodMode`·`DebugJumpFloor` 등) |
| `IsoPrototypeDemo.View.cs` | 시점 회전/모드 토글·`ApplyVisualSettings`·`ApplyViewToVisuals`·카메라 구도(허브/던전 고정 `playCameraSize` + 플레이어 추종) |
| `IsoPrototypeDemo.Interaction.cs` | 탭/스텝/인접 상호작용·커넥터 판정·`HandleTileTapped` |
| `IsoPrototypeDemo.Actions.cs` | 아이템/전투/조합/투척 행동 코루틴(`RangedAttack`·`FireRanged`·`ThrowBomb` 등) |
| `IsoPrototypeDemo.Targeting.cs` | 투척 가능 칸/FOV 기반 월드 조준 범위 마커의 생성·정리·시점 회전 추종 |
| `IsoPrototypeDemo.Movement.cs` | 경로 이동·문/비밀문/낙하 접근·auto-travel·플로어 전환 |
| `IsoPrototypeDemo.RunLifecycle.cs` | 세이브/체크포인트/이어하기·던전 전환·정산/생환·텔레메트리 종료 |
| `IsoPrototypeDemo.Hub.cs` | 허브 프롭/포탈 (영웅 프롭·잠금은 제거됨) |
| `IsoPrototypeDemo.Enemies.cs` | 적 스폰·AI 턴·활성화 |
| `IsoPrototypeDemo.Falls.cs` | 낙하/넉백/폭발 해소·`ApplyStatusToCombatantsInRegion` |
| `IsoPrototypeDemo.RestSites.cs` | 휴식 지점(모닥불) |
| `IsoPrototypeDemo.Extraction.cs` | 비상 탈출구·비상 송출기 렌더와 생환 선택 진입 |
| `IsoPrototypeDemo.Rescue.cs` | 갇힌 동료 프롭 렌더·구출 처리. 배치·판정은 Core(`ShelterNpcRoster`·`DungeonFloorInfo.RescueNpc`)가 소유한다 — `BossArena`와 같은 모양. 한 판에 동료가 여럿이라 **상태를 목록으로 든다**(스칼라 한 벌이면 뒤 NPC가 앞 것을 덮어써 참조 잃은 GameObject 가 씬에 남았다) |
| `IsoPrototypeDemo.BossArena.cs` | 최심층 제단 렌더·FOV 추종·아레나 접근 전조 알림 |
| `IsoPrototypeDemo.CombatFx.cs` | 전투/상태이상 연출 |
| `IsoPrototypeDemo.Visibility.cs` | FOV·수직 포털(개구부 미리보기 = 반대편 층 FOV 재계산)·후면 벽·플레이어 가림 |
| `IsoPrototypeDemo.Lighting.cs` | 지하 어둠·정적 광원·접촉/방향성 그림자 *(main 브랜치 기능, 병합됨)* |
| `IsoPrototypeDemo.Sprites.cs` | **어댑터** — 격자 질의(`DoorPlaneRisesRight`·`IsSecretDoorHinted`·`VisualContext`)를 풀어 스프라이트 팩토리에 넘긴다. 픽셀은 그리지 않는다 |

## 절차 생성 임시 아트 — `IsoPrototypeDemo` **밖의** 독립 클래스

외부 아트가 없을 때 64×32 규격으로 그리는 런타임 스프라이트. 파셜이 아니라 별 타입이며,
**격자·던전·플레이어를 참조하지 않는다** — 필요한 사실은 인자로 받는다. 이 무지(無知)가
경계를 지키는 장치다. 다시 `IsoPrototypeDemo`로 끌어들이면 신 클래스로 되돌아간다.

| 파일 | 담당 |
|------|------|
| `PrototypeSpriteCanvas.cs` | 저수준 드로잉 프리미티브(`NewTexture`·`FillRect`·`DrawThickLine`·`Blend`)와 **64×32/PPU 상수 SSOT**. `using static`으로 끌어다 쓴다 |
| `PrototypeSpriteCache.cs` | 키 → 스프라이트 캐시. 두 팩토리가 공유한다 |
| `PrototypePalette.cs` | 던전 역할색 해석 — `IsoVisualCatalog` 슬롯이 있으면 그 값, 없으면 인스펙터 폴백. 그리기 코드는 여기만 묻는다 |
| `PrototypeActorSprites.cs` | 액터·몬스터·아이템·프롭·랜드마크·FX. 팔레트도 안 쓰고 **캐시만** 의존한다 |
| `PrototypeEnvironmentSprites.cs` | 타일·벽·문·비밀문·안개·광원 타일. 캐시 + 팔레트 의존 |
| `TileVisualFacts.cs` | 호스트가 풀어 넘기는 격자 사실 묶음(진행 맥락·전면 여부·평면 방향·비밀문 힌트·허브 여부) |

> 리팩토링 시 픽셀 동일성은 **씬 렌더 지문**으로 검증했다 — `IsoPrototype`/`Hub` 씬을 빌드해
> 생성된 모든 텍스처를 RenderTexture 로 되읽어 해시했고 전/후가 같았다
> (`871de8c9bc421ffe` / `ab629c527bea784c`). 테스트는 이 그림 변화를 잡지 못하므로
> 그리기 코드를 손댈 때는 같은 방식으로 확인한다.

## HUD 컨트롤러 파셜

| 파일 | 담당 |
|------|------|
| `HubHudController.cs` | 수명주기·라우팅·메뉴/던전 선택·골드/이어하기 (`hero:` 라우팅 없음) |
| `HubHudController.Vendors.cs` | 상점·대장간·현상금·기록실 모달(기록 투입 포함) |
| `HubHudController.Preparation.cs` | 창고·출정 백팩·드래그드롭 엔진 |
| `PrototypeHudController.cs` | 수명주기·문서 바인딩·컨트롤 콜백·Update·입력·메뉴 |
| `PrototypeHudController.Handlers.cs` | 데모 이벤트 핸들러(`Handle*`)·미니맵·HP 표시 |
| `PrototypeHudController.ActionWheel.cs` | 액션 휠 빌드/배치 |
| `PrototypeHudController.EndGame.cs` | 출구 선택·보스 패널·게임오버 |
| `PrototypeHudController.Labels.cs` | 라벨 갱신(`Update*Label`)·상호작용 버튼 |

## Gameplay — 그 밖의 씬 서비스·패널·스토어

위 세 표(파셜 19 + 절차 아트 6 + HUD 8 = 33개)에 잡히지 않는 나머지 `Scripts/Gameplay` 파일들.
예전에는 이 표가 없어서 Gameplay 절반이 문서 어디에도 없었다 —
`InventoryPanelController` 같은 큰 파일을 이름으로 찾을 방법이 없었다.

| 파일 | 담당 |
|------|------|
| `GridManager.cs` | 격자 데이터(`GridMap`)와 변환 규칙(`IsoGrid`)을 씬에서 소유하는 얇은 진입점. 로직은 Core, 여기서는 Unity와 이어주기만 |
| `GridSortingObject.cs` | 격자 위 스프라이트의 월드 위치·`sortingOrder`를 `IsoGrid` 규칙으로 갱신. 정렬 계산을 개별 오브젝트가 하지 않게 하는 장치 |
| `IsoGridGizmo.cs` | Scene 뷰에서 아이소 격자를 다이아몬드로 그려 좌표 변환을 눈으로 검증(에셋 없이) |
| `IsoTapInput.cs` | 탭/클릭 → `GridPos` 역변환. **입력 추상화 레이어** — Input System 패키지가 있으면 그걸, 없으면 레거시 `Input`을 쓰고(`#if` 6곳) 게임 로직에는 `GridPos`만 넘긴다 |
| `IsoVisualCatalog.cs` | `ScriptableObject` — 논리 타일/오브젝트 → 교체 가능한 픽셀아트 스프라이트 매핑 + 던전 역할색 슬롯. 빈 슬롯은 절차 생성 스프라이트로 대체된다(`PrototypePalette`가 여기를 먼저 묻는다) |
| `ActorAnimationSet.cs` | `ScriptableObject` — Aseprite 태그 하나를 구운 프레임 시퀀스(`SpriteClip`). 타이밍을 "프레임 시작 시각 + 클립 총 길이"로 저장해 가변 지속시간을 무손실로 옮긴다 |
| `SpriteClipAnimator.cs` | 베이크된 태그 클립의 경량 재생기 — **Animator를 쓰지 않고 `renderer.sprite`만 만진다**(position·scale·color는 CombatFx 코루틴 소유라 겹치면 싸운다). 시야 밖에서는 얼어붙는다 |
| `CombatStatusFxAnimator.cs` | 액터에 붙는 상태이상 픽셀 아이콘. 파티클 시스템 없이 위치·크기·알파만 갱신 |
| `FloatingTextSpawner.cs` | 피격/회복 수치를 머리 위로 띄우는 플로팅 텍스트(`FloatingTextKind`) |
| `DOTweenBootstrap.cs` | DOTween 전역 초기화(`RuntimeInitializeOnLoadMethod`). **현재 실사용처는 없다** — 트윈은 전부 수기 코루틴이다(아래 "통합 후보") |
| `InventoryPanelController.cs` | 인벤토리/조합 모달(UI Toolkit). 슬롯을 `ItemCatalog.AllKinds`에서 생성해 **아이템을 늘려도 UXML을 고치지 않는다**. 아이템 → 아이콘 USS 클래스 매핑도 여기 있고 허브 상점/창고 슬롯이 공유한다 |
| `DebugPanelController.cs` | 개발용 디버그 창(Cmd/Ctrl+D·F1). 치트를 `Register` 목록으로 등록하면 패널이 버튼을 만든다 — 추가가 한 줄. HUD와 `UIDocument`를 공유하되 코드는 분리 |
| `DisplaySettingsPanelController.cs` | 허브·던전이 공유하는 표시 설정 모달의 바인딩·저장(`IDisposable`) |
| `DisplaySettingsStore.cs` | 그 설정값(`DisplaySettingsData`)의 기본값·영속화 |
| `DevelopmentViewportService.cs` | 에디터/개발 빌드에서 HUD 프레젠테이션 모드와 Game View 해상도를 즉시 바꾼다(`DevelopmentViewportPreset`). 릴리스 빌드는 저장된 오버라이드를 무시한다 |
| `ResponsiveUiLayout.cs` | UI Toolkit에 런타임 미디어 쿼리가 없어서, 패널 논리 크기를 USS 클래스로 변환하고 Safe Area를 같은 논리 좌표로 환산한다 |
| `HudPresentation.cs` | `HudPresentationMode`(Auto/Mobile/Desktop) 해석 한 함수 |
| `MainMenuController.cs` | 타이틀 씬의 얇은 라우터. `게임 시작`은 항상 캠프로, `이어하기`는 던전 중간 저장이 있을 때만 |
| `FrontEndFlow.cs` | 씬 이름 상수와 씬을 넘나드는 최소 static 상태 + 그 리셋. **도메인 리로드를 끈 채 Play 하기 때문에** 필드 초기화자에만 맡기면 이전 Play 값이 남는다 |
| `AtomicJsonStore.cs` | JSON을 임시 파일에 먼저 쓰고 교체, 이전 파일은 `.bak`으로. 중단된 쓰기·손상 JSON에서 복구한다 |
| `MetaStore.cs` | 메타 창고(`MetaSaveData`) 파일 입출력. 판 종료(사망 포함)에도 유지 |
| `RunSaveStore.cs` | 층 체크포인트(`RunSaveData`) 입출력 + `ContinueRequested` 플래그. 판 종료 시 삭제 |
| `RunTelemetryStore.cs` | 플레이테스트 리포트를 `development-profile/telemetry` 아래 사람이 읽을 JSON으로. 에디터/개발 빌드에서만 동작 |
| `DevelopmentSaveProfile.cs` | 개발 저장 루트를 실제 플레이 저장과 분리한다. 선택값만 `PlayerPrefs`, 데이터는 별도 디렉터리 |
| `OrthographicCameraFraming.cs` | 허브/던전 플레이의 동일 직교 배율과 던전 DebugAll 예외를 고정하는 순수 계산 |
| `DungeonFogBackdropLayout.cs` | 층 전체 안개 배경의 월드 영역. **실제 타일 위치를 쓰지 않고** x/y 전체 가능 영역의 다이아몬드를 계산해 미탐색 구조가 드러나지 않게 한다. `Dungeon Backdrop` Sorting Layer 이름도 소유해 넓은 elevation 정렬값과 분리한다 |
| `SpriteOcclusion.cs` | 정렬 순서 + 화면 겹침으로 플레이어 가림 후보를 판정하는 순수 함수 |
| `AssemblyInfo.cs` | `InternalsVisibleTo("ProjectC.Tests.EditMode")` — 절차 생성 스프라이트의 내부 규격 계약을 EditMode 테스트가 직접 고정할 수 있게 연다 |

## Core — 전 파일 지도

`Scripts/Core`는 순수 C#이다. **`IsoGrid.cs` 하나만 `UnityEngine`을 참조**하고(Vector 타입만)
나머지는 Unity 없이 `Tools/CoreTests` shim으로 돌아간다 — 훅(`Tools/Hooks/check-cs-edit.sh`)이
이 경계를 지킨다. 아래에서 SSOT 표에 이미 있는 항목은 중복 서술하지 않고 그쪽을 가리킨다.

### 격자·좌표·경로

| 파일 | 담당 |
|------|------|
| `GridPos.cs` | 논리 좌표 `(x, y, elevation)`. elevation은 층 번호가 아니라 **연속 높이값**이라 한 층 안에서도 높이차가 있다 |
| `GridMap.cs` | Dictionary 기반 **희소** 타일 저장소 + 칸 간 명시적 링크(사다리·층 전환). 넓고 대부분 비어 있는 다층 던전이라 배열을 쓰지 않는다 |
| `TileData.cs` | `TileKind` 열거와 칸 속성. 수직 시야 차단은 SSOT 표 참조 |
| `IsoGrid.cs` | 격자 ↔ 월드/화면 좌표 변환과 정렬값 계산. **정렬 규칙이 흩어지지 않도록 여기 한 곳**(SSOT 표 참조) |
| `DungeonHeightModel.cs` | 연속 elevation을 (던전 층, 층 내부 높이)로 해석. stride 기본 4 |
| `StairTopology.cs` | 같은 층 안의 계단 타일과 한 단 위 착지 타일의 연결 해석 |
| `GridPathfinder.cs` | 결정론적 A*. **사다리는 걸어서 못 지나간다** — 예전에는 계단과 같은 조건식에 묶여 사다리가 스프라이트만 다른 계단이었다. 이제 명시적 링크로만 통과하고 그 링크는 `canClimb`가 연다(SSOT 표 참조) |
| `VerticalTraversalRules.cs` | 수직 이동 수단의 자동 발동 범위와 사다리 월드 표현 크기. 층 전환 계단은 밟는 즉시, 사다리는 명시적 상호작용 |
| `VerticalRouteCue.cs` | 수직 이동 수단을 처음 봤을 때의 짧은 설명(`VerticalRouteRole` 6종). 색이 아니라 "어떻게 생겼고 무엇을 하면 어디로 가는지"를 말한다 |
| `TravelRules.cs` | 자동 이동 스텝 수와 인터럽트 우선순위(피해 > 새 적 > 새 아이템). 스냅샷은 호출부가 만든다 |
| `WorldInputRules.cs` | 화면에 겹친 타일 후보들 중 하나 고르기 — 조작 층에 가까운 것(`LayerPriority`) 우선, 같으면 렌더 정렬 순서 |

### 시야·조명·표현 규칙

| 파일 | 담당 |
|------|------|
| `SightRules.cs` | 시야선·수직 개구부 투시·근접 도달 기하·컬럼 span 해석(SSOT 표 참조). 옛 `VerticalOpeningRules`를 흡수했다 |
| `GridVisibility.cs` | Recursive Shadowcasting 8옥탄트 **골격만**. 컬럼 판정은 `SightRules.ViewColumn`에 위임해 전투 LoS와 출처를 공유한다 |
| `FloorVisibilityRules.cs` | 월드 지오메트리를 실제로 그릴 범위. 탐색 기록은 층별로 보존하되 렌더는 현재 층 + 수직 개구부만 |
| `GridLighting.cs` | 타일 단위 광량 0..1. Light2D를 쓰지 않고 `SpriteRenderer.color` 틴트에 곱한다 — FOV가 "무엇이 보이나"라면 여기는 "얼마나 밝은가"이고 안개 3상태(알파)와 직교한다 |
| `SpriteClipRules.cs` | `SpriteClipTags` 6종 상수 + 시간 → 프레임 인덱스(`FrameAt`). 베이크(에디터)와 재생(Gameplay)이 이 상수를 공유한다 — 문자열이 갈라지면 클립이 **조용히** 무시된다 |
| `CombatPresentationRules.cs` | 같은 피해 처리 위에 얹는 연출 계열(`CombatImpactKind`). 전투 판정과 분리 |
| `EnemyPresentationRules.cs` | FOV에 종속되는 적 전투 피드백 공개 여부 + 턴 기반 시체 수명 |

### 전투·상태이상·상호작용

| 파일 | 담당 |
|------|------|
| `CombatantState.cs` | 전투 참가자 엔티티(위치·HP·공격력)**만**. 연출은 Gameplay |
| `CombatRules.cs` | 사거리·피해·`RangedBlockReason`·방어 감산(최소 1은 남긴다). 도달 기하 자체는 `SightRules`에 위임 |
| `TurnManager.cs` | 플레이어 행동 1회 + 적 전체를 한 턴으로 묶는 상태 머신(`TurnPhase`) |
| `StatusEffects.cs` | `StatusKind`(화상·빙결·중독)와 부여/상쇄. 중독은 화염·빙결과 무관하게 독립 지속 |
| `FallRules.cs` | **모든 낙하 트리거의 수렴점 `TryFall`**. 낙하 칸수 → 낙뎀 곡선 → 착지 충돌을 한 곳에서. 플레이어와 몬스터가 같은 경로 |
| `Interactions.cs` | `OilRules`(기름 살포·발화) · `BombRules`(+`BombResult`). **`BombRules.ForEachBlastCell`이 3×3 순회 SSOT**(SSOT 표 참조) |
| `WaterRules.cs` | 젖음·연쇄 결빙 + **`WetPoolFlood`(젖은 웅덩이 4방향 확산 SSOT)가 이 파일에 산다** — 파일 이름과 타입 이름이 다르니 찾을 때 주의 |
| `ShockRules.cs` | 감전. 3×3 블라스트로 직접 지지고, 닿은 젖은 웅덩이 전체를 통전시킨다. 마른 칸엔 전파되지 않아 "적을 웅덩이로 모는" 셋업 전술이 된다 |
| `WindowRules.cs` | 창문 깨기. 온전한 창문은 이동을 막고 시야는 통과, 깨지면 통로(되돌릴 수 없다). 밖이 허공이면 그대로 `FallRules`로 이어진다 |
| `HungerRules.cs` | 배고픔 단계(`HungerStage`)·감소·회복과 `HungerState`. 하드 타이머가 아니라 자원 압박인 이유가 주석에 있다 |

### AI

| 파일 | 담당 |
|------|------|
| `BehaviorTree.cs` | Running 상태가 없는 즉시형 BT 노드(Selector/Condition/Leaf). 결정이 즉시형이라 `Tick`은 행동 또는 null만 낸다 |
| `MonsterBrain.cs` | 몬스터 한 마리의 결정. 위치·HP를 직접 바꾸지 않고 **행동 의도**(`MonsterActionKind`)만 반환한다 — 빙결·화상·낙하를 파이프라인에 끼울 수 있게. 사수 교전 순서도 여기 |
| `MonsterArchetype.cs` | 종류 하나의 스탯·행동 파라미터 + `CanClimb`·`DisplayName`·`MonsterRangedEffect` |
| `MonsterRoster.cs` | 명단과 깊이별 혼합. 등반 가능 여부를 **실루엣과 일치**시켜 규칙을 배우지 않고 눈으로 판단하게 한다. 보스 `GraveWarden`의 표시명은 SSOT 표 참조 |
| `MonsterActivation.cs` | 활성/컬링 판정. 비활성 몬스터는 `Decide` 호출 자체를 건너뛴다(휴면) |
| `ShelterNpcRoster.cs` | 갇힌 동료 명단과 쉘터 시설(`ShelterFacility`: 대장간·의뢰 게시판). **미구출 시설은 허브에 프롭도 상호작용도 없다** |

### 던전 생성

| 파일 | 담당 |
|------|------|
| `DungeonLayout.cs` | `DungeonFloorInfo`·`DungeonLayout`·`FloorPlan`·`DungeonGenerator.Generate`+헬퍼. 난이도·콘텐츠의 유일한 키는 `ProgressIndex`(고도가 아니다). 방 기하는 `FloorPlan`이 소유한다(SSOT 표 참조) |
| `DungeonGenerator.Planning.cs` | seed로 층 하나의 골격 치수를 뽑는다 |
| `DungeonGenerator.Carving.cs` | **타일을 실제로 쓴다**(`map.Set`) — 바닥·벽·계단·개구부 조각 |
| `DungeonGenerator.Placement.cs` | **좌표만 고른다** — 아이템·적·휴식처·탈출구·비밀문 자리. 파셜 경계 기준은 SSOT 표 참조 |
| `DungeonCatalog.cs` | 허브에서 고르는 원정지 목록(`DungeonDefinition`). 지역·보스·층수·seed가 여기서 묶인다 |
| `DungeonDirectionRules.cs` | `DungeonProgressDirection`(하강/상승/Inward)과 방향 의존 문구·층 라벨. **규칙은 방향을 타지 않지만 안내 문구는 반드시 탄다** |
| `DungeonMetaContext.cs` | 생성기가 알아야 하는 판 넘는 진행 상태를 값 하나로 묶는다. `default`는 "아무것도 해금 안 됨"이 아니라 **"제약 없음"** — 테스트·미리보기가 메타 없이도 같은 던전을 만들어야 해서다 |
| `DungeonBandProfile.cs` | 지역 프로파일(`DungeonRegionProfile`) × 깊이의 콘텐츠 변주. **스탯·AI는 지역을 타지 않는다** — 지역이 가르는 것은 혼합·밀도·무대 확률(SSOT 표 참조) |
| `DungeonVisualContext.cs` | `DungeonDepthBand`(초반/중반/후반/보스)와 `DungeonDepthBandRules`(SSOT 표 참조) |
| `DungeonLootRules.cs` | 지역별 일반 드롭 편성. **모든 지역이 같은 23칸 롤을 소비**해 지역을 바꿔도 생성기 RNG 스트림이 흔들리지 않는다 |
| `DungeonBossRules.cs` | 보스 스폰 칸 선택과 최심층 출구 봉인. 보스가 없는 던전은 출구가 상시 개방 |
| `DungeonBossArenaRules.cs` | 아레나 층 판정(상대 깊이 = 마지막 층)과 접근 전조(SSOT 표 참조). 시각용 `DungeonDepthBand.Boss`와는 **다른 축** |
| `DungeonRestRules.cs` | 던전 내부 제한 휴식처의 배치 간격과 회복량 |
| `DungeonPropPlacementRules.cs` | 위험 프롭이 입구·필수 점유 좌표를 덮지 않도록 안전한 일반 바닥 후보를 고른다 |
| `SecretRoomRules.cs` | 비밀문 개수와 발견 판정. 공개 전에는 벽처럼 막고 공개되면 열린 문이 된다 |
| `ExtractionRules.cs` | 중간 생환 층. 잦으면 판돈이 사라지고 없으면 최심층까지 한 번의 결정이라 **정해진 층에만** 둔다 — 배고픔과 짝을 이룬다 |
| `ElevatorShaftRules.cs` | 보스를 잡아 전원이 들어온 뒤에야 움직이는 복귀 수단. GDD의 "통로로 뛰어내려 하강"을 **수치가 막아서**(낙뎀 곡선 vs HP) 낙하가 아니라 탑승이 됐다 |
| `HubLayout.cs` | 허브 캠프의 고정 레이아웃. 기존 던전 렌더러를 그대로 태우려고 층 1개짜리 `DungeonLayout` 형태로 만든다 |

### 아이템·장비·인벤토리

| 파일 | 담당 |
|------|------|
| `Items.cs` | `ItemKind`·`ItemDefinition`·`ItemCatalog`·`Inventory`·`ItemSpawn`. **종류를 늘릴 때 손댈 곳은 `ItemKind` + `ItemCatalog` 정의 한 줄뿐이다** — 분류·경제·표시·백팩 크기가 `ItemDefinition` 하나에 모여 있고 미등록 enum은 즉시 실패한다 |
| `ItemStorage.cs` | `ItemStack` 목록 기반 수량 저장 연산. `JsonUtility`가 Dictionary를 직렬화하지 못해 목록으로 뒀고, 덕분에 세이브 클래스가 아이템마다 필드를 늘리지 않는다 |
| `BackpackRules.cs` | 백팩 격자(`ItemFootprint`, 회전 없음)와 배치 판정 |
| `Equipment.cs` | `EquipmentSlot`·`EquipmentDefinition`·`CombatLoadout`·`EquipmentCatalog` + 던전 드랍 규칙. **어떤 장비도 공격력을 올리지 않는다** — 숫자 대신 행동 규칙(사거리·넉백·방어·안전 낙하)을 바꿔 영구 스탯 크리프를 피한다 |
| `ForgeRules.cs` | 대장간 — 골드로 장비 제작·장착(`ForgeResult`). 옛 영구 스탯 강화를 대체한다 |
| `Crafting.cs` | 조합 레시피(재료 2 + 산출 1). 산출물은 **기존 아이템만** 써서 조합이 새 효과 구현을 요구하지 않게 한다 |
| `ExpeditionLoadoutRules.cs` | 허브 창고 ↔ 출정 백팩 이동(`LoadoutTransferResult`). 기본 지급품도 같은 용량에 포함해 실제 던전 백팩과 결과를 맞춘다 |
| `ItemUnlockRules.cs` | 해금 조건(`ItemUnlockCondition`)·판정·기록 투입. 계측 축은 `BountyMetric`을 재사용한다 — 의뢰와 같은 값을 읽으므로 계측 시스템을 새로 만들지 않는다(SSOT 표 참조) |

### 메타·세이브·계측

| 파일 | 담당 |
|------|------|
| `SurvivorProfile.cs` | 원정자 기본값(HP·공격력·근접 사거리·기본 지급품). **직업도 프리셋도 없다** — 정체성은 장비가 지고, 옛 영웅 3종은 숫자만 달라 난이도 선택에 가까웠다 |
| `MetaSaveData.cs` | 판 사이에 유지되는 창고·골드·해금 목록·해금 최고 기록·장착 슬롯. 해금은 **죽어도 남는다** |
| `RunSaveData.cs` | 층 단위 체크포인트. 던전은 seed로 재생성하므로 지형·적·아이템 배치를 저장하지 않는다 — 이어하기 = "현재 층을 층 입구에서 다시 시작" |
| `RunSummary.cs` | 한 판의 결과. "가장 멀리 간 층"을 **진행 지수**로 잰다 — 예전에는 층 인덱스 최솟값이었는데 상승 던전에서 시작 층이 영원히 최솟값이라 도달 층이 첫 층에 붙어 있었다 |
| `RunTelemetry.cs` | 한 판의 플레이테스트 계측(층별·구간별·피해·아이템). Unity 시간·파일 API는 모르고 Gameplay가 값을 넣는다. 구간 롤업은 파생 값(SSOT 표 참조) |
| `RunRecordRules.cs` | **기록** — 죽음이 먹이는 유일한 축. 세 진행 축이 전부 성공을 요구해서 초반에 죽으면 아무것도 안 남던 문제의 답이다(SSOT 표 참조) |
| `BountyRules.cs` | 의뢰(현상금). 완료 판정 축 `BountyMetric`은 전부 `RunTelemetry` 누적값에 매핑되고 해금 조건도 같은 열거를 쓴다 |

---

## 단일 출처(SSOT) 지도 — "여기만 고치면 된다"

| 관심사 | SSOT 위치 |
|--------|-----------|
| 오버레이(UI) 정렬값 | `IsoPrototypeDemo` 중첩 `OverlaySorting` 상수 |
| 타일 픽셀 규격(64×32·PPU 64) | `PrototypeSpriteCanvas` 상수 (`IsoPrototypeDemo`의 동명 상수가 이 값을 참조) |
| 절차 생성 아트의 던전 역할색 | `PrototypePalette` (카탈로그 슬롯 → 없으면 인스펙터 폴백) |
| 저수준 픽셀 드로잉 | `PrototypeSpriteCanvas` (`FillRect`·`DrawThickLine`·`Blend` 등) |
| `IsoPrototypeDemo` 파셜 목록 | **이 문서의 파셜 표** (본체 요약 주석은 여기를 가리키는 포인터다) |
| 월드 정렬 배수·대역 불변식 | `IsoGrid.DepthResolution` / `MicroResolution` |
| 백팩 ↔ 세이브 아이템 수량 매핑 | `RunSaveData.WriteItems` / `AddItemsTo` |
| 몬스터 표시명·피해소스 매칭 | `MonsterArchetype.DisplayName` + `MonsterRoster.MatchSource` |
| 보스 표시명 | `MonsterRoster.GraveWarden`의 `displayName`(= "감시자"). `DungeonCatalog`가 문자열을 다시 적지 않고 이 값을 참조한다 — 주지 않으면 화면에 코드 ID `GraveWarden`이 뜬다 |
| 아트 슬롯 ID 발급 (슬롯 이름 → `IsoVisualCatalog` 필드) | `ProjectCAsepritePipeline.CatalogSlots`. 아트 파이프라인(`Tools/ArtPipeline/art_review.py`의 `SlotCatalog`)이 이 목록을 **복제하지 않고 파싱해서** 레시피 슬롯을 검증한다 — 여기 없는 슬롯에 게시하면 Unity가 읽지 않는 `.aseprite`가 생긴다. 새 슬롯은 여기 먼저 등록한다 |
| 아이템 짧은 라벨(HUD) | `ItemCatalog.ShortLabel` |
| 아이템 분류·표시·경제·백팩 크기 | `ItemCatalog`의 `ItemDefinition` 표 |
| 블라스트 3×3 순회 | `BombRules.ForEachBlastCell(center, visit)` (`Interactions.cs`). `OilRules`·`ShockRules`·`WaterRules`·`SecretRoomRules`가 전부 이걸 부른다 — 예전에는 같은 이중 루프가 여러 파일에 손으로 적혀 있어 한 곳만 반경이 달라져도 조용히 갈렸다 |
| 투척 가능 칸 순회 | `BombRules.ForEachThrowTarget(map, from, maxRange, visit)` — 실제 `CanThrow`와 조준 월드 마커가 공유한다 |
| 젖은 웅덩이 4방향 확산 | `WetPoolFlood.Collect(map, center, onVisit)` — **파일은 `Core/WaterRules.cs` 안에 있다.** `WaterRules.ChainFreeze`(결빙)와 `ShockRules.DischargeDetailed`(감전)가 공유한다. 두 벌로 두면 같은 웅덩이가 얼 때와 통전될 때 다른 모양이 된다 |
| 북쪽 방 입구 칸·방 rect 순회 | `FloorPlan.UpperRoomEntrance` / `IsUpperRoomEntrance(pos)` / `UpperRoomCells()` / `BranchCells()`. 생성기의 하드코딩 방 기하가 전부 여기로 모였다 — 예전에는 `(VerticalX, UpperMinY)` 비교가 손으로 적혀 있어 방 형상을 바꿀 때 하나만 빠뜨리면 입구가 막힌 층이 나왔다. **순회 순서(`x` 외곽 → `y` 내곽)가 곧 RNG 소비 순서**라 바꾸면 생성기 지문이 깨진다 |
| 생성기 파셜 경계 | **타일을 바꾸면 `Carving`, 좌표만 고르면 `Placement`.** `PlaceRestSite`가 `map.Set`을 한 번도 하지 않는데 Carving에 있어서 이 기준으로 옮겼다 |
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
| 보스 접근 전조 문구 | `DungeonBossArenaRules.TryApproachCue` — **진행 방향을 인자로 받는다.** 예전에는 "한 층 아래"로 고정이라 상승 던전(폐병원)에서 정반대를 가리켰다 |
| 장비 정의·효과 | `EquipmentCatalog` (전투 보정은 `CombatLoadout`) |
| 장비 제작·장착 | `ForgeRules` (+ `MetaSaveData.equippedWeaponId/GearId`) |
| 아이템 백팩 면적 | `ItemCatalog.For(kind).Footprint` (`BackpackRules.Footprint`은 호환 위임) |
| 아이템 분류(소모품/전리품/재료/장비) | `ItemCatalog.CategoryOf` |
| 아이템 수량 저장·복원 | `ItemStorage` (`MetaSaveData.stash/loadout`·`RunSaveData.items` 공유) |
| 플레이어 기본 수치·기본 지급품 | `SurvivorProfile` (영웅 3종/`HeroRoster`를 대체 — 직업 없음) |
| 사다리 등반 가능 여부 | `MonsterArchetype.CanClimb`(기본 false) → `GridPathfinder(canClimb:)`(기본 true) |
| 개구부 칸 집합 | `DungeonFloorInfo.HoleTiles` (대표 칸은 `Hole`; 성장·약한 바닥은 `DungeonGenerator.Carving`의 같은 판정 함수) |
| 기록 적립 공식 | `RunRecordRules.Award` |
| 해금 조건 판정·기록 투입 | `ItemUnlockRules.InvestRecords`/`RemainingFor` (UI는 자체 판정을 들지 않는다). 기록실이 보여주는 "가장 가까운 조건"도 `ClosestPending(meta)`가 같은 `RemainingFor`로 잰다 — 예전에는 이번 판 계측을 따로 읽어 기록실과 축이 갈렸다 |
| 허브/던전 카메라 배율 | `playCameraSize` 하나 (`OrthographicCameraFraming.Follow`, 양쪽 모두 플레이어 추종) |

## 아직 흩어져 있어 통합 후보인 것 (Unity 에디터 검증 필요)

리팩토링 감사에서 식별했으나 **Gameplay 코드라 shim이 컴파일하지 않아** 보류한 항목.
`Tools/CoreTests`는 `Scripts/Core`만 포함하므로 여기 것들은 에디터 EditMode/PlayMode(그리고
렌더가 걸린 것은 씬 캡처)로만 안전을 증명할 수 있다. 반영 시 그 가드를 먼저 세울 것.

- **공용 `Tween(duration, step)` 코루틴** — 수기 애니메이션 17곳(6파일 · 14메서드:
  `IsoPrototypeDemo.CombatFx`/`Movement`/`Actions`/`Enemies`/`Falls` · `FloatingTextSpawner`).
  단순 치환이 아니다: `CombatFx`의 5개 루프는 **루프 안에 프레임별 생존 확인**(`if (target == null) yield break`)을
  들고 있어 step 콜백이 취소를 신호할 수 있어야 하고, `Falls`의 `ShiftPlayerTo`는 `float t` 없이
  `Clamp01`을 Lerp 인자에 인라인해 형태가 다르다. 참고로 `DOTweenBootstrap`이 DOTween을 초기화해
  두었지만 **실사용처는 아직 0곳**이다 — 통합할 때 수기 코루틴 vs DOTween을 먼저 정한다.
- **입력 소스 추상화** — `IsoTapInput`의 `#if ENABLE_INPUT_SYSTEM` 분기 6곳 + HUD 쪽 포인터/키보드
  헬퍼. 입력 경로는 실제 Play로만 확인되고, 지금은 **PC 우선 기간**이라 터치 경로를 건드리면
  검증 없이 깨진 채 남는다(`CLAUDE.md` "현재 개발 우선순위" 참조).
- **에디터 도구 asmdef** — `Assets/_Project/Editor` 루트의 5개 파일(`ArtStyleCapture`·
  `IsoPrototypeSceneBuilder`·`MainMenuSceneBuilder`·`PlayFromMainMenu`·`ProjectCArtImporter`)이
  asmdef 없이 `Assembly-CSharp-Editor`에 실린다(자체 asmdef를 가진 것은 `Editor/ArtPipeline`뿐).
  asmdef를 새로 만들면 참조 그래프가 바뀌므로 에디터 컴파일로만 확인할 수 있다.
  함께 볼 중복: `ArtStyleCapture`와 `IsoPrototypeSceneBuilder`가 같은 RenderTexture 캡처 절차를
  각자 들고 있다. (피벗 중복은 이미 해소됐다 — `ProjectCArtImporter`가 `ProjectCArtPivots`에 위임한다.)

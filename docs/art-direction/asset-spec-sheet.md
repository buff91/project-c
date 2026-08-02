# Project-C 아트 에셋 슬롯 규격 시트 (발주/제작용) — 128-레짐

> **출처(SSOT)**: `Assets/_Project/Editor/ArtPipeline/ProjectCAsepritePipeline.cs`의
> `CatalogSlots`(파일명→슬롯) + `Assets/_Project/Editor/ArtPipeline/ProjectCArtPivots.cs`(피벗 단일 SSOT)
> + 기존 폴백 PNG 실측 해상도. 이 표와 코드가 어긋나면 **코드가 정답**이다.
>
> **2026-07 해상도 상향**: 바닥 타일 64×32 → **128×64**, PPU 64 → **128**. 모든 월드 캔버스가
> 구 규격의 정확히 ×2다(월드 크기·정규화 피벗 불변). UI 아이콘(`ui-*`)만 64-레짐에 남는다.

## 공통 규격 (모든 슬롯)

- **PPU 128** · **Point** 필터 · **MipMap Off** · **무압축**(OSX/Android/iOS) · **Canvas 피벗**
- 파일 위치: `Assets/_Project/Art/Source/Aseprite/<파일명>.aseprite` (최종 SSOT)
- **파일명 = 슬롯 계약.** 같은 이름을 두 곳에 두지 않는다. 원본 없으면 `Art/Runtime`·`Art/Environment` PNG 폴백 사용.
- 피벗 표기: `(x, y_norm)` = 정규화. `발 y(px)`는 캔버스 아래에서부터의 픽셀.
  파이프라인에 피벗이 없는 슬롯은 기본 **`(0.5, 0)` 바닥 중앙** (PNG 폴백 임포터 기본은 액터 접지 `(0.5, 0.04)`).
- Unity 복귀 후 `Project-C > Art > Aseprite > Validate Sources`로 규격 검증.

## 캔버스 관례 (한눈에)

| 종류 | 캔버스(W×H) | 발/기준선 |
|------|------------|-----------|
| 던전 배경 다이아 | 128×64 | 중앙 |
| 바닥 다이아 | 128×64 | 중앙 |
| 후면 벽 | 64×112 | 아래에서 16px |
| 계단(같은층/상행) | 128×112 | 아래에서 32px |
| 계단(하행) | 128×80 | 아래에서 32px |
| 문 | 128×160 | 아래에서 32px |
| 액터 | 96×128 | 아래에서 ~5px (0.04) |
| 소품 | 128×128 (포탈만 128×160) | 개별 |
| 아이템 | 64×64 | 개별(4~12px) |
| 마커 | 128×64 | 중앙 |

---

## 0. 던전 배경

| 파일명 | 슬롯 | 캔버스 | 피벗 | 상태 |
|--------|------|--------|------|------|
| `env-dungeon-backdrop` | dungeonBackdrop | 128×64 | (0.5, 0.5) 중앙 | ✅ 팔레트 잠금 PNG 폴백 |

실제 방/복도 모양을 담지 않고 전체 생성 가능 영역 한 장만 그린다. Runtime에서는 25% 알파로
`Dungeon Backdrop` Sorting Layer에 놓는다. 정식 Aseprite 원본이 도착하면 같은 파일명으로
저장해 임시 PNG 슬롯을 교체한다.

---

## 1. 바닥·기본 타일 (정적 · 1프레임)

`env-*-rising-*` 방향형이 실제 렌더에 쓰이고, 아래 **기본형**은 방향형이 없을 때의 폴백이다.
`hole/weak-floor/ladder`는 방향형이 없어 이 기본형이 곧 최종본.

| 파일명 | 슬롯 | 캔버스 | 피벗 | 상태 |
|--------|------|--------|------|------|
| `env-floor` | floor | 128×64 | (0.5, 0.5) 중앙 | ✅ Aseprite v1 + 동일 PNG 폴백 |
| `env-floor-raised` | raisedFloor | 128×80 *(권장)* | (0.5, 0.5) | ⚠️ 원본 없음 |
| `env-floor-lower` | lowerFloor | 128×64 *(권장)* | (0.5, 0.5) | ⚠️ 원본 없음 |
| `env-stairs` | stairs | 128×112 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-ladder` | ladder | 64×112 — **랜드마크 용도** (§1-a) | (0.5, 0.08) 발 기준 | ⚠️ 원본 없음 |
| `env-stairs-up` | stairsUp | 128×112 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-stairs-down` | stairsDown | 128×80 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-hole` | hole | **128×64 고정** — 깊이 표현은 타일이 아니라 랜드마크 오브젝트 소관(배치 3). 2:1을 벗어나면 톤매핑 경로가 조용히 원본 폴백한다 | (0.5, 0.5) | ⚠️ 원본 없음 |
| `env-weak-floor` | weakFloor | **128×64 확정** | (0.5, 0.5) | ⚠️ 원본 없음 |

`env-floor`는 기존 4,098px hard-alpha 다이아와 외곽 3px 중간톤을 유지한다. 원본 가시 픽셀은
`grey-4` 91.996%와 세 개의 넓은 마모 덩어리 `grey-3` 8.004%만 사용한다. 둘 다 런타임
톤매핑의 `.28-.50` 구간이라 최종 역할색은 `Stone` 100%이며, 방향 신호·테두리·측면 두께·
고립 1px 노이즈와 Shadow/Light/Outline 마모 도장은 허용하지 않는다. 정식 `floor` 슬롯은
Aseprite 첫 프레임을 쓰고, `raisedFloor/lowerFloor`는 전용 원본 전까지 픽셀이 같은
`env-floor.png`를 공유한다.

### 1-a. `env-ladder`의 의미 (확정)

`ladder` 슬롯의 주인은 **바닥 타일이 아니라 "세워진 사다리" 랜드마크 오브젝트**다.
사다리는 "두 발판 사이에 세워진 별도 월드 오브젝트" 관례를 따르며, 사다리 발밑 타일은
일반 바닥으로 그린다. 랜드마크 렌더가 카탈로그 `ladder` 슬롯을 먼저 보고 없으면 절차
아트로 폴백한다. 세로는 `LadderScaleY`가 실측 월드 높이 기준으로 자동 보정하므로 캔버스
높이가 정확히 112일 필요는 없다(64×112 권장).
| `env-door-closed` | doorClosed | 128×160 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-door-open` | doorOpen | 128×160 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |

**깊이 밴드 바닥 (신규 발주 대상)** — 슬롯·파일명 계약·피벗이 코드에 전부 등록돼 있어
정식 파일명으로 저장만 하면 자동 연결된다. 비어 있으면 `floor`로 폴백된다(그동안은 절차
밴드 오버레이가 임시 대행). 석재 기본색은 유지하고 **오염/마모/장비 밀도**로 층대(帶)를
구분한다 (`FloorFor`가 밴드×raised로 선택).

| 파일명 | 슬롯 | 캔버스 | 피벗 |
|--------|------|--------|------|
| `env-floor-mid` / `env-floor-mid-raised` | midFloor / midRaisedFloor | 128×64 | (0.5, 0.5) |
| `env-floor-deep` / `env-floor-deep-raised` | deepFloor / deepRaisedFloor | 128×64 | (0.5, 0.5) |
| `env-floor-boss` / `env-floor-boss-raised` | bossFloor / bossRaisedFloor | 128×64 | (0.5, 0.5) |

**Facility 드레싱 (아케이드 재발주 대상)** — 공용 바닥을 대체하는 새 지형이 아니라 seed 고정
희소 변주다. 바닥 PNG는 `process_hospital_dressing_v1.py`가 `env-floor` 위에 합성한다.
슬롯명 `hospital*`은 구 폐병원 시절 명명을 유지한 것이다(리스킨 표 §5 — 개명은 콘텐츠가
늘어난 뒤 일괄). 내용물은 M5 재발주에서 아케이드 어휘(자판기·죽은 네온 간판·홀로 패널)로 교체한다.
따라서 공용 `env-floor`가 바뀌면 Facility·밴드·B2 합성 바닥은 원본 변경으로 간주해 전부 다시 생성한다.

| 파일명 | 슬롯 | 캔버스 | 피벗 | 상태 |
|--------|------|--------|------|------|
| `env-floor-grate` | hospitalFloorGrate | 128×64 | (0.5, 0.5) | ✅ Aseprite + 동일 PNG 폴백 |
| `env-floor-cracked` | hospitalFloorCracked | 128×64 | (0.5, 0.5) | ✅ PNG 폴백 |
| `env-floor-service` | hospitalFloorService | 128×64 | (0.5, 0.5) | ✅ Aseprite + 동일 PNG 폴백 |

**B2 오른쪽 진출 드레싱 — 독립 1셀**

| 파일명 | 슬롯 | 캔버스 | 피벗 | 상태 |
|--------|------|--------|------|------|
| `env-floor-b2-parking-stop` | b2ParkingWheelStopFloor | 128×64 | (0.5, 0.5) | ✅ legacy PNG 폴백 |
| `env-floor-b2-parking-stop-view-{0,1,2,3}` | b2ParkingWheelStopFloorView0..3 | 128×64 | (0.5, 0.5) | ✅ v3 낮은 고무/강철 범퍼 Aseprite |
| `env-floor-b2-fallen-sign` | b2FallenWayfindingFloor | 128×64 | (0.5, 0.5) | ✅ legacy PNG 폴백 |
| `env-floor-b2-fallen-sign-view-{0,1,2,3}` | b2FallenWayfindingFloorView0..3 | 128×64 | (0.5, 0.5) | ✅ v3 바닥에 눕힌 파손 판재 Aseprite |
| `env-floor-b2-cracked` | b2CrackedFloor | 128×64 | (0.5, 0.5) | ✅ 평평한 무방향 균열·마모 Aseprite |

범퍼와 안내판은 배치 좌표와 world-facing이 독립이므로 케이블·연속 페인트 seam으로 서로
연결하지 않는다. v3는 최종 캔버스에서 네 view를 직접 만들며, 완전한 4슬롯 세트에서만 방향형을
켠다. 두 역할은 런타임 `Signal` 톤매핑을 타되 바닥 본체는 공용 중성 램프를 유지한다. named
배치는 범퍼 `(5,2)`·안내판 `(5,1)`이며 카탈로그 슬롯 누락이 서로의 역할을 바꾸지 않는다.
균열 타일은 선형 seam·문자·방향 신호와 측면 두께가 없는 중앙형 표면 손상이라
단일 슬롯을 모든 시점에 사용한다. 전용 슬롯이 없을 때만 구 전역 `hospitalFloorCracked`로
폴백한다.

**B2 배럴 유출 방지 베이 — 전용 2셀×4시점**

| 파일명 | 슬롯 | 캔버스 | 피벗 | 상태 |
|--------|------|--------|------|------|
| `env-floor-b2-barrel-bay-service-view-{0,1,2,3}` | b2BarrelBayServiceFloorView0..3 | 128×64 | (0.5, 0.5) | ✅ service/ring 셀 |
| `env-floor-b2-barrel-bay-drain-view-{0,1,2,3}` | b2BarrelBayDrainFloorView0..3 | 128×64 | (0.5, 0.5) | ✅ drain/grate 셀 |

두 역할의 여덟 슬롯이 모두 있을 때만 활성화한다. 네 시점 각각을 `192×96` master로 먼저
마감한 뒤 분할하며, 배럴 스프라이트는 바닥에 굽지 않는다.

**B2 2×2 연속 바닥 Macro — 4셀×4시점**

| 파일명 | 슬롯 | 캔버스 | 피벗 | 상태 |
|--------|------|--------|------|------|
| `env-floor-b2-macro-role-{0,1,2,3}-view-{0,1,2,3}` | b2MacroFloorRole0View0..Role3View3 | 128×64 | (0.5, 0.5) | ✅ 16개 Aseprite 완전 세트 |

role 0..3은 월드 2×2 블록의 네 물리 셀이다. `256×256` top-down source를 시점별로 먼저 회전·
투영해 `256×128` master를 만들고, master 전체를 마감한 뒤에만 네 셀로 자른다. 16슬롯이 전부
있고 특수 드레싱·clear spine과 겹치지 않는 깨끗한 2×2 블록을 찾았을 때만 활성화한다. 하나라도
누락되거나 블록이 없으면 네 셀 전부 일반 바닥으로 원자 폴백한다. 렌더는 일반 mapped floor 경로를
사용하므로 전면 경계 extrusion을 유지하며, 별도 높이·구멍·엄폐·판정 신호를 뜻하지 않는다.

## 2. 방향형 타일 (정적 · 1프레임) — 화면 기준 좌/우 상승

| 파일명 | 슬롯 | 캔버스 | 피벗 (발 px) | 상태 |
|--------|------|--------|--------------|------|
| `env-stairs-rising-right` | stairsRisingRight | 128×112 | (0.5, 0.286) 32px | ✅ |
| `env-stairs-rising-left` | stairsRisingLeft | 128×112 | (0.5, 0.286) 32px | ✅ |
| `env-stairs-up-rising-right` | stairsUpRisingRight | 128×112 | (0.5, 0.286) 32px | ✅ |
| `env-stairs-up-rising-left` | stairsUpRisingLeft | 128×112 | (0.5, 0.286) 32px | ✅ |
| `env-stairs-down-rising-right` | stairsDownRisingRight | 128×80 | (0.5, 0.4) 32px | ✅ |
| `env-stairs-down-rising-left` | stairsDownRisingLeft | 128×80 | (0.5, 0.4) 32px | ✅ |
| `env-door-closed-rising-right` | doorClosedRisingRight | 128×160 | (0.5, 0.2) 32px | ✅ |
| `env-door-closed-rising-left` | doorClosedRisingLeft | 128×160 | (0.5, 0.2) 32px | ✅ |
| `env-door-open-rising-right` | doorOpenRisingRight | 128×160 | (0.5, 0.2) 32px | ✅ |
| `env-door-open-rising-left` | doorOpenRisingLeft | 128×160 | (0.5, 0.2) 32px | ✅ |

## 3. 후면 벽 (정적 · 1프레임) — 광원 변형은 애니 권장

| 파일명 | 슬롯 | 캔버스 | 피벗 (발 px) | 비고 |
|--------|------|--------|--------------|------|
| `env-wall-rising-right` | rearWallRisingRight | 64×112 | (0.5, 0.143) 16px | ✅ v4 저주파 기본 셸 Aseprite |
| `env-wall-rising-left` | rearWallRisingLeft | 64×112 | (0.5, 0.143) 16px | ✅ v4 저주파 기본 셸 Aseprite |
| `env-wall-torch-rising-right` | rearWallTorchRisingRight | 64×112 | (0.5, 0.143) 16px | ✅ v4 작업등 Aseprite |
| `env-wall-torch-rising-left` | rearWallTorchRisingLeft | 64×112 | (0.5, 0.143) 16px | ✅ v4 작업등 Aseprite |
| `env-wall-pipes-rising-right/left` | hospitalWallPipesRisingRight/Left | 64×112 | (0.5, 0.143) 16px | ✅ v4 설비 패널 Aseprite |
| `env-wall-window-rising-right/left` | hospitalWallWindowRisingRight/Left | 64×112 | (0.5, 0.143) 16px | ✅ v4 비발광 유지보수 셸 Aseprite(legacy 슬롯) |
| `env-wall-cabinet-rising-right/left` | hospitalWallCabinetRisingRight/Left | 64×112 | (0.5, 0.143) 16px | ✅ v4 벽 매립 단말 Aseprite |
| `env-wall-b2-service-segment-0-rising-right/left` | b2ServiceWallSegment0RisingRight/Left | 64×112 | (0.5, 0.143) 16px | ✅ B2 연속벽 왼쪽·시안 진단등 |
| `env-wall-b2-service-segment-1-rising-right/left` | b2ServiceWallSegment1RisingRight/Left | 64×112 | (0.5, 0.143) 16px | ✅ B2 연속벽 중앙·앰버 작업등 |
| `env-wall-b2-service-segment-2-rising-right/left` | b2ServiceWallSegment2RisingRight/Left | 64×112 | (0.5, 0.143) 16px | ✅ B2 연속벽 오른쪽·마젠타 상태점 |

B2 서비스 벽은 여섯 슬롯 완전 세트일 때만 켠다. 세 장은 셀별 FOV·정렬·가림을 유지하지만,
방향별 `192×176` master에서 팔레트 잠금·despeckle을 먼저 거친 뒤 잘라 이음선을 공유한다.
현재 기본/유지보수/작업등/설비/단말/서비스 벽 16종의 제작 계보는
`project-c-b2-prop-production-sheet-v2.{png,prompt.md}`와
`project-c-b2-wall-material-source-v1.{png,prompt.md}` → `process_b2_prop_quality_v4.py`다.
`hospitalWallWindow*`라는 legacy 슬롯명은 B2에서 실제 창·발광·상호작용을 뜻하지 않는다. 서비스
세그먼트와 설비·단말 배치를 우선한 뒤 남은 물리 벽 bay의 월드 좌표 홀짝으로 기본/유지보수 셸을
고정 선택하며, 두 셸의 알파·피벗·PPU·결합면과 벽의 충돌·FOV 계약은 같다.

## 4. 액터 (애니메이션) — 캔버스 96×128, 피벗 (0.5, 0.04)

**모든 프레임 발 위치 고정**(온니언스킨). 태그: `idle`·`walk`·`attack`·`hit`·`fall`·`death`.
반복 안 하는 태그(attack/hit/fall/death)는 Aseprite **Repeat=1**. **Layer UUID 켜기**.

| 파일명 | 슬롯 | 필요 태그 | 비고 |
|--------|------|-----------|------|
| `actor-player` | player | idle/walk/attack/hit/fall/death | 기본 폴백 영웅 |
| `actor-knight` | knight | idle/walk/attack/hit/fall/death | |
| `actor-ranger` | ranger | idle/walk/attack/hit/fall/death | 원거리 모션 |
| `actor-alchemist` | alchemist | idle/walk/attack/hit/fall/death | |
| `actor-goblin` | goblin | idle/walk/attack/hit/death | 약탈자(근접 기준몹) |
| `actor-skeleton` | skeleton | idle/walk/attack/hit/death | 낡은 경비 드론(탱커) |
| `actor-slime` | slime | idle/walk/hit/death | 오염 슬러지 — 공격 모션 단순 가능 |
| `actor-slinger` | slinger | idle/walk/attack/hit/death | ★ 신규. 투석 약탈자 — 치켜든 팔/투척 실루엣 필수 |
| `actor-grave-warden` | graveWarden | idle/walk/attack/hit/death | ★ 신규. 보스 감시자 — 일반 몹보다 큰 실루엣, 경고 네온 외눈 |
| `actor-merchant` | merchant | idle *(+옵션 gesture)* | 허브 NPC, 전투 없음 |

`actor-knight`는 접지 품질을 먼저 잠근 의도적 예외다. 현재 정식 원본은 하드 알파·24색 역할
팔레트·2×2 클러스터·한 발 기준선의 단일 `Frame_0`이며, 위 6태그는 Aseprite 수작업과 PC 화면
승인이 끝난 뒤에만 활성화한다(`SurvivorAnimationApproved=false`).

## 5. 소품 (일부 애니)

| 파일명 | 슬롯 | 캔버스 | 피벗 (발 px) | 애니 |
|--------|------|--------|--------------|------|
| `prop-campfire` | hubCampfire | 128×128 | (0.5, 0.094) 12px | ★ `idle` 드럼 화로 루프 |
| `prop-portal` | hubPortal | 128×160 | (0.5, 0.075) 12px | ★ `idle` 이상 게이트 루프 |
| `prop-explosive-barrel` | explosiveBarrel | 128×128 | (0.5, 0.078) 10px | ✅ v4 원통형 연료 셀 Aseprite |
| `prop-stash` | hubStash | 128×128 | (0.5, 0.172) 22px | 정적 |

## 6. 마커 (정적 · 바닥 데칼)

| 파일명 | 슬롯 | 캔버스 | 피벗 |
|--------|------|--------|------|
| `marker-player` | playerFootprint | 128×64 | (0.5, 0.5) 중앙 |
| `marker-target` | selection | 128×64 | (0.5, 0.5) 중앙 |

두 마커는 각각 틸/앰버의 **열린 코너 틱** 정식 Aseprite다. 발·접촉 AO·바닥 재질을 덮는 완전한
링으로 닫지 않는다.

### 6-a. B2 foundation (절차 프레젠테이션 · 카탈로그 슬롯 아님)

- 전면: face-only `64×42`, PPU 64, pivot y=26, 연속 10 logical px fascia
- 지지대: `12×38`, top-center pivot, 회전과 무관한 같은 월드 볼록 모서리에 고정
- 정렬: 별도 `B2 Floor Foundation` 루트, `Dungeon Backdrop` order 1/2
- 의미: collider·입력·격자·FOV·전투 상태를 만들지 않는 B2 시작방 전용 표현

## 7. 아이템 (정적 · 64×64) — 인벤/월드 공용

| 파일명 | 슬롯 | 피벗 (발 px) |
|--------|------|--------------|
| `item-potion` | potion | (0.5, 0.125) 8px |
| `item-bomb` | bomb | (0.5, 0.125) 8px |
| `item-frost-bomb` | frostBomb | (0.5, 0.125) 8px |
| `item-oil-flask` | oilFlask | (0.5, 0.125) 8px |
| `item-throwing-knife` | throwingKnife | (0.5, 0.0625) 4px |
| `item-recall-scroll` | recallScroll | (0.5, 0.094) 6px |
| `item-coin-pouch` | coinPouch | (0.5, 0.188) 12px |
| `item-gemstone` | gemstone | (0.5, 0.0625) 4px |
| `item-relic` | relic | (0.5, 0.094) 6px |
| `item-herb` | herb | (0.5, 0.156) 10px |
| `item-blast-powder` | blastPowder | (0.5, 0.156) 10px |
| `item-frost-shard` | frostShard | (0.5, 0.125) 8px |

## 8. UI 아이콘 (별도 · UI Toolkit) — **32px 소스 / 24px PC 표시**

카탈로그 슬롯이 아니라 `Art/Runtime`에서 직접 참조. 도트 스프라이트.
UI Toolkit이 픽셀 크기로 소비하므로 `ui-*` 액션 아이콘은 **32×32로 마감한 뒤 PC HUD에서 24×24로 표시**한다.
임포터는 PPU 64를 유지한다.

| 파일명 | 캔버스 | 용도 |
|--------|--------|------|
| `ui-heart-full` | 24×21 | HP 하트(채움) |
| `ui-heart-empty` | 24×21 | HP 하트(빈) |
| `ui-settings` / `ui-menu` | 32×32 | 아이콘 전용 전역 도구 |
| `ui-rotate-left` / `ui-rotate-right` | 32×32 | 아이콘 전용 시점 회전 |
| `ui-backpack` / `ui-wait` | 32×32 | 아이콘+텍스트 행동 |
| `ui-melee` / `ui-ranged` | 32×32 | 전투 자세 토글 |
| `ui-interact` | 32×32 | 문맥 행동의 공용 손 아이콘 |
| `ui-action-hex` / `ui-action-hex-hover` | 72×64 | 방사형 문맥 메뉴의 기본/호버 육각 프레임 |
| `ui-main-menu-backdrop` | 960×540 | PC 메인 메뉴 16:9 배경 (`process_ui_backdrops_v1.py`) |
| *(확장 예정)* | 32×32 계열 | 행동별 문맥 아이콘, 9-slice 창 모서리, 다이아 글로우 |

UI 아이콘은 의미를 파일명/UXML이 소유하고 `DesignSystem.uss`가 Sprite만 연결한다.
설명·경고·층/높이 같은 정보까지 아이콘화하지 않는다.

---

## 제작 체크리스트 (슬롯 1개당)

1. 위 표의 **캔버스 크기**로 새 Aseprite 문서 (Point, PPU 개념상 1칸=1px)
2. **팔레트 인덱스** 고정 (→ `ai-to-aseprite-workflow.md`)
3. 프레임/태그 작성 — 애니면 발 위치 고정 + 태그 Repeat 설정 + Layer UUID
4. **정식 파일명**으로 `Art/Source/Aseprite/`에 저장
5. Unity 복귀 → 자동 임포트·카탈로그 연결 → `Validate Sources`
6. MCP Play 캡처(현 우선순위: PC 가로) → EditMode/PlayMode 회귀

> ⚠️ 캔버스 크기·피벗을 바꾸면 `ProjectCArtPivots`(피벗 SSOT 코드)도 함께 고쳐야 한다. 이 표는 현재 코드 기준.

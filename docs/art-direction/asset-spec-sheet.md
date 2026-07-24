# Project-C 아트 에셋 슬롯 규격 시트 (발주/제작용)

> **출처(SSOT)**: `Assets/_Project/Editor/ArtPipeline/ProjectCAsepritePipeline.cs`의
> `CatalogSlots`(파일명→슬롯) + `CustomPivots`(피벗) + 기존 폴백 PNG 실측 해상도.
> 이 표와 코드가 어긋나면 **코드가 정답**이다. 슬롯 51개 전체.

## 공통 규격 (모든 슬롯)

- **PPU 64** · **Point** 필터 · **MipMap Off** · **무압축**(OSX/Android/iOS) · **Canvas 피벗**
- 파일 위치: `Assets/_Project/Art/Source/Aseprite/<파일명>.aseprite` (최종 SSOT)
- **파일명 = 슬롯 계약.** 같은 이름을 두 곳에 두지 않는다. 원본 없으면 `Art/Runtime`·`Art/Environment` PNG 폴백 사용.
- 피벗 표기: `(x, y_norm)` = 정규화. `발 y(px)`는 캔버스 아래에서부터의 픽셀.
  파이프라인에 피벗이 없는 슬롯은 기본 **`(0.5, 0)` 바닥 중앙**.
- Unity 복귀 후 `Project-C > Art > Aseprite > Validate Sources`로 규격 검증.

## 캔버스 관례 (한눈에)

| 종류 | 캔버스(W×H) | 발/기준선 |
|------|------------|-----------|
| 바닥 다이아 | 64×32 | 중앙 |
| 후면 벽 | 32×56 | 아래에서 8px |
| 계단(같은층/상행) | 64×56 | 아래에서 16px |
| 계단(하행) | 64×40 | 아래에서 16px |
| 문 | 64×80 | 아래에서 16px |
| 액터 | 48×64 | 아래에서 ~2.6px (0.04) |
| 소품 | 64×64 (포탈만 64×80) | 개별 |
| 아이템 | 32×32 | 개별(2~6px) |
| 마커 | 64×32 | 중앙 |

---

## 1. 바닥·기본 타일 (정적 · 1프레임)

`env-*-rising-*` 방향형이 실제 렌더에 쓰이고, 아래 **기본형**은 방향형이 없을 때의 폴백이다.
`hole/weak-floor/ladder`는 방향형이 없어 이 기본형이 곧 최종본.

| 파일명 | 슬롯 | 캔버스 | 피벗 | 상태 |
|--------|------|--------|------|------|
| `env-floor` | floor | 64×32 | (0.5, 0.5) 중앙 | ✅ PNG 실측 |
| `env-floor-raised` | raisedFloor | 64×40 *(권장)* | (0.5, 0) | ⚠️ 원본 없음 |
| `env-floor-lower` | lowerFloor | 64×32 *(권장)* | (0.5, 0) | ⚠️ 원본 없음 |
| `env-stairs` | stairs | 64×56 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-ladder` | ladder | 32×56 *(권장, 결정 필요)* | (0.5, 0) | ⚠️ 원본 없음 |
| `env-stairs-up` | stairsUp | 64×56 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-stairs-down` | stairsDown | 64×40 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-hole` | hole | 64×32 *(+깊이 결정 필요)* | (0.5, 0) | ⚠️ 원본 없음 |
| `env-weak-floor` | weakFloor | 64×32 *(권장)* | (0.5, 0) | ⚠️ 원본 없음 |
| `env-door-closed` | doorClosed | 64×80 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |
| `env-door-open` | doorOpen | 64×80 *(권장)* | (0.5, 0) | ⚠️ 폴백용 |

## 2. 방향형 타일 (정적 · 1프레임) — 화면 기준 좌/우 상승

| 파일명 | 슬롯 | 캔버스 | 피벗 (발 px) | 상태 |
|--------|------|--------|--------------|------|
| `env-stairs-rising-right` | stairsRisingRight | 64×56 | (0.5, 0.286) 16px | ✅ |
| `env-stairs-rising-left` | stairsRisingLeft | 64×56 | (0.5, 0.286) 16px | ✅ |
| `env-stairs-up-rising-right` | stairsUpRisingRight | 64×56 | (0.5, 0.286) 16px | ✅ |
| `env-stairs-up-rising-left` | stairsUpRisingLeft | 64×56 | (0.5, 0.286) 16px | ✅ |
| `env-stairs-down-rising-right` | stairsDownRisingRight | 64×40 | (0.5, 0.4) 16px | ✅ |
| `env-stairs-down-rising-left` | stairsDownRisingLeft | 64×40 | (0.5, 0.4) 16px | ✅ |
| `env-door-closed-rising-right` | doorClosedRisingRight | 64×80 | (0.5, 0.2) 16px | ✅ |
| `env-door-closed-rising-left` | doorClosedRisingLeft | 64×80 | (0.5, 0.2) 16px | ✅ |
| `env-door-open-rising-right` | doorOpenRisingRight | 64×80 | (0.5, 0.2) 16px | ✅ |
| `env-door-open-rising-left` | doorOpenRisingLeft | 64×80 | (0.5, 0.2) 16px | ✅ |

## 3. 후면 벽 (정적 · 1프레임) — 횃불 변형은 애니 권장

| 파일명 | 슬롯 | 캔버스 | 피벗 (발 px) | 비고 |
|--------|------|--------|--------------|------|
| `env-wall-rising-right` | rearWallRisingRight | 32×56 | (0.5, 0.143) 8px | 정적 |
| `env-wall-rising-left` | rearWallRisingLeft | 32×56 | (0.5, 0.143) 8px | 정적 |
| `env-wall-torch-rising-right` | rearWallTorchRisingRight | 32×56 | (0.5, 0.143) 8px | ★ 횃불 `idle` 루프 권장 |
| `env-wall-torch-rising-left` | rearWallTorchRisingLeft | 32×56 | (0.5, 0.143) 8px | ★ 횃불 `idle` 루프 권장 |

## 4. 액터 (애니메이션) — 캔버스 48×64, 피벗 (0.5, 0.04)

**모든 프레임 발 위치 고정**(온니언스킨). 태그: `idle`·`walk`·`attack`·`hit`·`fall`·`death`.
반복 안 하는 태그(attack/hit/fall/death)는 Aseprite **Repeat=1**. **Layer UUID 켜기**.

| 파일명 | 슬롯 | 필요 태그 | 비고 |
|--------|------|-----------|------|
| `actor-player` | player | idle/walk/attack/hit/fall/death | 기본 폴백 영웅 |
| `actor-knight` | knight | idle/walk/attack/hit/fall/death | |
| `actor-ranger` | ranger | idle/walk/attack/hit/fall/death | 원거리 모션 |
| `actor-alchemist` | alchemist | idle/walk/attack/hit/fall/death | |
| `actor-goblin` | goblin | idle/walk/attack/hit/death | |
| `actor-skeleton` | skeleton | idle/walk/attack/hit/death | |
| `actor-slime` | slime | idle/walk/hit/death | 공격 모션 단순 가능 |
| `actor-merchant` | merchant | idle *(+옵션 gesture)* | 허브 NPC, 전투 없음 |

## 5. 소품 (일부 애니)

| 파일명 | 슬롯 | 캔버스 | 피벗 (발 px) | 애니 |
|--------|------|--------|--------------|------|
| `prop-campfire` | hubCampfire | 64×64 | (0.5, 0.094) 6px | ★ `idle` 불꽃 루프 |
| `prop-portal` | hubPortal | 64×80 | (0.5, 0.075) 6px | ★ `idle` 소용돌이 루프 |
| `prop-explosive-barrel` | explosiveBarrel | 64×64 | (0.5, 0.078) 5px | 정적(+옵션 반짝임) |
| `prop-stash` | hubStash | 64×64 | (0.5, 0.172) 11px | 정적 |

## 6. 마커 (정적 · 바닥 데칼)

| 파일명 | 슬롯 | 캔버스 | 피벗 |
|--------|------|--------|------|
| `marker-player` | playerFootprint | 64×32 | (0.5, 0.5) 중앙 |
| `marker-target` | selection | 64×32 | (0.5, 0.5) 중앙 |

## 7. 아이템 (정적 · 32×32) — 인벤/월드 공용

| 파일명 | 슬롯 | 피벗 (발 px) |
|--------|------|--------------|
| `item-potion` | potion | (0.5, 0.125) 4px |
| `item-bomb` | bomb | (0.5, 0.125) 4px |
| `item-frost-bomb` | frostBomb | (0.5, 0.125) 4px |
| `item-oil-flask` | oilFlask | (0.5, 0.125) 4px |
| `item-throwing-knife` | throwingKnife | (0.5, 0.0625) 2px |
| `item-recall-scroll` | recallScroll | (0.5, 0.094) 3px |
| `item-coin-pouch` | coinPouch | (0.5, 0.188) 6px |
| `item-gemstone` | gemstone | (0.5, 0.0625) 2px |
| `item-relic` | relic | (0.5, 0.094) 3px |
| `item-herb` | herb | (0.5, 0.156) 5px |
| `item-blast-powder` | blastPowder | (0.5, 0.156) 5px |
| `item-frost-shard` | frostShard | (0.5, 0.125) 4px |

## 8. UI 아이콘 (별도 · UI Toolkit)

카탈로그 슬롯이 아니라 `Art/Runtime`에서 직접 참조. 도트 스프라이트.

| 파일명 | 캔버스 | 용도 |
|--------|--------|------|
| `ui-heart-full` | 24×21 | HP 하트(채움) |
| `ui-heart-empty` | 24×21 | HP 하트(빈) |
| *(확장 예정)* | 24×21 계열 | 검·장화 등 액션 아이콘, 9-slice 창 모서리, 다이아 글로우 |

---

## 제작 체크리스트 (슬롯 1개당)

1. 위 표의 **캔버스 크기**로 새 Aseprite 문서 (Point, PPU 개념상 1칸=1px)
2. **팔레트 인덱스** 고정 (→ `ai-to-aseprite-workflow.md`)
3. 프레임/태그 작성 — 애니면 발 위치 고정 + 태그 Repeat 설정 + Layer UUID
4. **정식 파일명**으로 `Art/Source/Aseprite/`에 저장
5. Unity 복귀 → 자동 임포트·카탈로그 연결 → `Validate Sources`
6. MCP Play 캡처(모바일 세로·PC 가로) → EditMode 673 / PlayMode 1 회귀

> ⚠️ 캔버스 크기·피벗을 바꾸면 `CustomPivots`(코드)도 함께 고쳐야 한다. 이 표는 현재 코드 기준.

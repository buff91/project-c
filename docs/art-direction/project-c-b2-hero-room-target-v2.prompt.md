# B2 히어로 룸 목표 v2 — 단일 평면 서비스 체크포인트

- **생성 방식**: Codex 내장 ImageGen 이미지 편집 2회. 첫 편집에서 공간 덩어리와 광원 위계를 잡고,
  두 번째 편집에서 현행 시작방 크기와 굵은 픽셀 클러스터로 교정했다.
- **레이아웃 기준**: `docs/captures/first-dungeon-flat-v1.png`
- **스타일 참고**: 사용자 제공 `IMG_5322.JPG`(폐 지하철 재질·국소 네온),
  `IMG_5326.JPG`(아이소메트릭 실내의 외곽 밀도와 빈 중앙)
- **산출물**: `project-c-b2-hero-room-target-v2.png`
- **용도**: 런타임 에셋이 아니라 B2 시작방 배치·벽 군집·광원 위계의 승인 기준.

## 채택할 것

1. 한쪽에 연속된 **서비스 벽 덩어리**를 두고 나머지 벽은 조용하게 유지한다.
2. 폭발통 쪽은 설비/그레이트, 진출 쪽은 범퍼/쓰러진 안내판으로 **두 기능 구역**을 만든다.
3. 플레이어에서 닫힌 문·진출 계단으로 이어지는 **2칸 폭의 시각적 주동선**에는 프롭을 놓지 않는다.
4. 시안 서비스광 1구역 + 앰버 비상등 1구역 + 마젠타 잔재 1개만 사용한다.
5. 바닥 풍화는 균등한 잔금이 아니라 2×2px 이상 덩어리 패치·배수 스트립·주차선으로 묶는다.

## 채택하지 않을 것

- 시안이 넓혀 보인 바닥 외곽과 벽 길이는 개념적 과장이다. 실제 13×13 생성 지형과 6×5 시작방을
  늘리지 않는다.
- 차량·기둥·새 계단·사다리·단차·캐치워크는 이번 패스에 추가하지 않는다.
- HUD·미니맵·선택 마커의 ImageGen 변형은 구현 근거가 아니다.

## 최종 교정 프롬프트

```text
Use case: precise-object-edit
Asset type: implementable B2 hero-room gameplay paintover
Input images:
- Image 1 is the approved composition and lighting direction. Preserve its continuous service-wall mass,
  quiet central route, left barrel utility cluster, right exit cluster, localized cyan light, localized amber
  light, and tiny magenta accent.
- Image 2 is the strict geometry and scale authority.

Primary request: Make Image 1 implementable on Image 2's exact existing room. Compress the environment
composition back onto Image 2's original floor silhouette and original wall footprint. The B2 starting
chamber is only a compact 6-by-5-tile room connected to the existing corridor; do not enlarge it, add floor
tiles, or move any wall, door, stair, player, barrel, marker, HUD element, or minimap geometry. Keep a clean
two-tile-wide four-direction movement spine through the center.

Style/medium: crisp authentic low-resolution isometric pixel art with visibly larger deliberate pixel clusters
and flat one-to-two-step color ramps.
Targeted correction: remove most micro-cracks, speckle and tiny debris from Image 1. Replace them with a few
grouped 2x2-or-larger cracked patches, broad drainage bands and simple parking paint so each tile reads cleanly
at game scale.
Constraints: change only environment surface art and local light treatment. Preserve all UI and Korean text.
Single flat plane only. No cars, pillars, extra props, new stairs, ladders, platforms, height changes, grid
overlay, diagonal arrows, readable sign text, logos, watermark, smooth 3D shading, painterly detail, grain,
dithering, or global bloom.
```

# B2 배럴 유출 방지 베이 소스 v1

- **생성 방식**: 2026-08-02 Codex 내장 ImageGen 신규 생성 2회. 1차 후보 보드에서 C0의
  구조를 고른 뒤, 그 구조를 고정한 4시점 보드를 2차 생성했다.
- **입력 참고**:
  - `docs/captures/b2-service-wall-q0-live-v1.png` — 현재 배럴·service/grate의 배율과 분리 문제.
  - `project-c-b2-hero-room-target-v2.png` — 낮은 산업 설비 밀도와 조용한 중앙.
  - `env-floor-service.png` · `env-floor-grate.png` — 교체할 두 바닥의 투영.
  - `prop-explosive-barrel.png` — 별도 런타임 프롭의 스케일 참고.
- **산출물**:
  - `project-c-b2-barrel-bay-candidates-v1.png` — 1차 `1536×1024`, `512×512` 3열×2행 후보.
  - `project-c-b2-barrel-bay-source-v1.png` — 최종 `1536×1024`, `768×512` 2열×2행 4시점.
- **채택 후보**: 1차 좌상단 C0. 다른 후보보다 경고색이 적고 고정링–호스–배수구의 연결이
  가장 명확하다.
- **회전 규칙**: 최종 보드의 좌상=view 0, 우상=view 1, 좌하=view 2, 우하=view 3이다.
  각 사분면을 독립적으로 정규화해, 반대 시점에서도 바닥 전면 두께가 화면 아래에 남고 호스가
  실제 공유 모서리에서 연결되게 한다.
- **용도**: B2 시작방의 폭발통 `Service` 셀과 인접 `Grate` 셀 전용. 배럴 자체는 굽지 않는다.

## 채택 근거

1. 두 셀의 경계에서 굵은 호스와 녹 띠가 이어져 베이지색 카펫처럼 보이던 기존 패치를 한 설비로 묶는다.
2. 배럴 고정링은 비어 있어 폭발통이 밀리거나 파괴된 뒤에도 바닥 설비로 자연스럽게 남는다.
3. 설비 높이가 낮고 각 셀 중앙의 어두운 면이 넓어 이동·공격 표식과 액터 발을 가리지 않는다.
4. 시안 점 하나 외에는 새 발광색이 없어 서비스 벽과 게임 신호보다 먼저 읽히지 않는다.

## 1차 후보 생성 프롬프트

```text
Use case: stylized-concept
Asset type: production source board for a two-cell isometric pixel-art floor replacement, ultimately split into two 128x64 cell-owned Unity floor sprites at PPU 128
Input images:
- Image 1: current runtime problem reference. Match its playable isometric scale and replace only the visually isolated beige barrel/service/grate floor island; do not reproduce the HUD or room.
- Image 2: approved material-density and composition reference. Borrow its dark industrial cyberpunk service-bay language, restrained hazard markings, and quiet gameplay center.
- Image 3: current service floor tile geometry reference.
- Image 4: current grate floor tile geometry reference.
- Image 5: explosive barrel scale/style reference only. DO NOT draw the barrel into the floor asset.

Primary request: Create an exact 3-column by 2-row candidate board containing six variants of the SAME two-cell "barrel spill-containment bay." In every candidate, show exactly two complete adjacent 2:1 isometric floor diamonds in the view-0 arrangement: the barrel-host/service cell is upper-right and the connected drain/grate cell is lower-left. The two diamonds share one edge and must read as one continuous low industrial installation.
Upper-right service cell: dark blue-black/gunmetal shallow L-shaped containment pan, 2-4 pixel rim after downscale, one empty circular floor anchor ring where the separate runtime barrel will stand, bolted plates, restrained rust.
Lower-left drain cell: a broad rectangular drainage grate occupying at most 40% of the tile, one thick corrugated hose or cable trunk with a single 90-degree elbow terminating at the grate.
Across the shared tile edge: one continuous 6-8 pixel-wide drainage band or rust channel and one continuous low conduit, aligned exactly across the seam. Keep at least 55% of each tile center dark and visually quiet for movement/attack markers. The installation must stay below character ankle height and never obscure the barrel silhouette or skull decal.

Scene/backdrop: perfectly flat solid #ff00ff chroma-key background only. No floor plane outside the two diamonds, no shadows, gradients, texture, reflections, or lighting variation in the background.
Style/medium: authentic chunky low-resolution 2:1 isometric pixel art, deliberate 2-4 pixel clusters after downscale, hard pixel edges, no antialiasing, no painterly noise, no smooth 3D render.
Color palette: blue-black charcoal and low-saturation cool steel grey about 80%; rusty orange-brown wear about 15%; at most one short worn hazard-yellow edge mark; one tiny 2x4 cyan diagnostic LED only; zero magenta.
Materials/textures: battered steel floor plates, shallow containment lip, drain bars, thick rubber conduit, bolts, restrained rust streak and grime. The base floor should be dark and compatible with the Project-C room, not beige concrete.
Composition/framing: exact 3x2 equal candidate grid filling a 3:2 landscape canvas, no gutters and no visible grid lines. Center one complete two-diamond pair inside each candidate region with generous flat-magenta clearance. All six are subtle variants of the same geometry, not different objects.
Constraints: six candidates only; exactly two adjacent floor diamonds per candidate; no walls; no wall panels; no characters; no barrel; no crates; no second prop; no text, letters, numbers, logos, or watermark. No new light pool or glow.
Avoid: carpet-like beige islands, full-tile black-yellow stripes, bright orange rectangles, vending machines, tall pumps, long wall cables, oil puddles, cyan or magenta floor flooding, tiny one-pixel speckle, photorealism.
```

## 2차 4시점 생성 프롬프트

```text
Use case: stylized-concept
Asset type: four-view production source board for one two-cell isometric pixel-art floor installation
Input images:
- Image 1: approved design reference. Preserve its exact dark containment pan, empty circular barrel anchor ring, thick elbow hose, broad drain grate, restrained rust, and tiny cyan indicator.
- Image 2: canonical Project-C floor diamond silhouette and front-edge thickness reference.
- Image 3: barrel scale reference only. Never draw the barrel into the floor.

Primary request: Redraw the SAME two-cell barrel spill-containment bay in four camera-quarter views on one exact 2-column by 2-row board.
Upper-left quadrant = view 0: service/ring cell upper-right, drain/grate cell lower-left.
Upper-right quadrant = view 1: service/ring cell upper-left, drain/grate cell lower-right.
Lower-left quadrant = view 2: service/ring cell lower-left, drain/grate cell upper-right.
Lower-right quadrant = view 3: service/ring cell lower-right, drain/grate cell upper-left.
In every quadrant the two complete 2:1 isometric floor diamonds share one edge. The same thick hose and rust/drain band cross that shared edge without a gap. The ring always belongs to the service cell; the broad grate always belongs to the drain cell. Rotate the world installation correctly between views; do not merely turn the entire screen image upside down. Every floor diamond must retain a normal top-down isometric view with its thin visible front edge on the screen-bottom sides.

Scene/backdrop: perfectly flat solid #ff00ff chroma-key background only, no shadows, gradients, texture, reflections, or lighting variation.
Style/medium: authentic chunky low-resolution 2:1 isometric pixel art, hard pixel clusters, no antialiasing, no painterly noise, no smooth 3D render.
Color palette: blue-black charcoal and cool steel grey dominant, restrained rusty orange-brown, at most one tiny cyan diagnostic LED, zero magenta inside the assets.
Composition/framing: exact 2x2 equal quadrants filling a 3:2 landscape canvas, no gutters, no grid lines, no labels. Center one complete paired installation in each quadrant with generous flat-magenta clearance. Keep scale, silhouette, materials, and detail placement consistent across all four views.
Gameplay constraints: installation stays below ankle height; at least 55% of each cell center remains dark; the empty anchor ring and runtime barrel footprint remain clear; no new glow or light pool.
Constraints: exactly four paired installations; exactly two floor diamonds per installation; no walls, wall panels, characters, barrels, crates, pumps, text, letters, numbers, logos, or watermark.
Avoid: inconsistent redesigns between quadrants, upside-down floor thickness, carpet-like beige tiles, large hazard stripes, bright orange rectangles, oil puddles, cyan flooding, one-pixel speckle.
```

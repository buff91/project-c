# B2 연속 서비스 벽 소스 v1

- **생성 방식**: 2026-08-02 Codex 내장 ImageGen 신규 생성 1회.
- **입력 참고**:
  - `project-c-arcade-dressing-source-v1.png` — 크로마 보드와 금속 재질 문법.
  - `project-c-b2-hero-room-target-v2.png` — 연속 서비스 벽의 덩어리와 국소 조명 위계.
  - `docs/captures/b2-hero-room-layout-q1-v1.png` — 현재 게임 배율과 분리된 대형 패널 문제.
- **산출물**: `project-c-b2-service-wall-source-v1.png` (`1536×1024`, `512×512` 3열×2행).
- **선택 규칙**: 상단 rising-right의 C0 왼쪽 구간, C1 중앙 구간, C2 오른쪽 구간을 한 벽으로
  합친다. 하단 rising-left도 같은 열 선택을 독립 적용한다. 최종 런타임은 방향별
  `192×176` master를 먼저 마감한 뒤 셀별 `64×112` 세 장으로 분할한다.
- **결정론적 교정**: 하단 C2의 마젠타 점이 선택 창 밖에 생성돼, 반대 시점에서도 같은 정보가
  남도록 팔레트의 `sig-neon-magenta` 4×4 클러스터를 C2 캐비닛 면에 복원한다. 전체 master를
  `96×88`에서 잠금·despeckle한 뒤 2배 nearest 확대해 기존 벽의 굵은 픽셀 문법에 맞춘다.
- **용도**: B2 시작방의 물리 `-Y` 벽 `(2,0)→(4,0)` 전용. 일반 Facility 벽 슬롯은 덮지 않는다.

## 채택 근거

1. 상·하단 모두 동일한 상부 캡, 케이블 트레이, 하부 배관, 녹 띠를 공유해 세 칸이 한 설비벽으로 읽힌다.
2. 신호색은 왼쪽의 짧은 시안 진단등, 중앙의 앰버 작업등, 오른쪽의 작은 마젠타 상태점으로 제한된다.
3. 큰 발광 사각 패널이나 문자가 없어 이동·공격 표식보다 먼저 읽히지 않는다.
4. 낮은 해상도로 줄여도 넓은 철판 면과 굵은 구조 리브가 남을 정도로 덩어리가 크다.

## 원본 생성 프롬프트

```text
Use case: stylized-concept
Asset type: production source board for six isometric pixel-art rear-wall tiles, each ultimately conformed to 64x112 pixels at PPU 128
Input images:
- Image 1: format and material-language reference for isolated arcade-tower floor and wall assets on chroma magenta; do not copy its unrelated floor tiles.
- Image 2: approved composition reference for one continuous dense service-wall mass, quiet center, localized cyan and amber light.
- Image 3: runtime scale and problem reference; replace the three disconnected bright wall machines with one coherent service bay, but do not reproduce HUD or room.

Primary request: Create one exact 3-column by 2-row sprite source board. Each row is a continuous three-tile rear service wall divided into three equal cells. Top row is the rising-right screen orientation; bottom row is the matching rising-left orientation. In each row:
A / left cell = pipe and cable intake with one small cyan diagnostic strip;
B / center cell = the same continuous wall with a compact amber industrial work light and breaker box;
C / right cell = the same continuous wall ending in a closed service cabinet with only a tiny dim magenta status residue.
The wall top cap, structural ribs, horizontal cable tray, lower conduit, grime band, rust streaks, and panel seams must visibly continue across both cell boundaries. Each cell must still be usable as a standalone 64x112 wall sprite and share the same bottom contact baseline.

Scene/backdrop: perfectly flat solid #ff00ff chroma-key background only. No floor plane, shadows, gradients, texture, reflections, or lighting variation in the background.
Style/medium: authentic chunky low-resolution isometric pixel art, 2:1 dimetric rear wall, deliberate 2-4 pixel clusters after downscale, hard pixel edges, no antialiasing, no painterly texture, no photorealism, no smooth 3D render.
Color palette: blue-black charcoal, low-saturation concrete grey, rusty orange-brown wear; localized cyan strip, localized amber lamp, tiny muted magenta residue.
Materials/textures: battered steel panels, cracked concrete shell, cable tray, pipes, vents, bolted maintenance plates, restrained rust and grime.
Composition/framing: exact 3x2 equal grid filling a 3:2 landscape canvas, no gutters and no visible grid lines. Keep generous flat-magenta clearance around each wall silhouette, but make matching conduits meet precisely at the vertical cell boundaries.
Constraints: six tiles only; no floor tiles; no characters; no barrels; no signs with words; no readable letters or numbers; no logos; no watermark. Keep both rows identical in design and only change the isometric slope/orientation. Do not flood the wall or background with neon.
Avoid: six unrelated vending machines, floating poster panels, large X symbols, giant glowing rectangles, synthwave neon flooding, tiny noisy pixels, high-rise wall towers, extra story props.
```

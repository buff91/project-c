# B2 출구 바닥 균열 v1 — ImageGen 소스 기록

- 생성 방식: OpenAI 내장 ImageGen
- 용도: `(B2HeroFloorPatchKind.Cracked)` 전용 저돌출 바닥 드레싱의 conform 입력
- 런타임 파일: `env-floor-b2-cracked.png`
- 최종 SSOT: `env-floor-b2-cracked.aseprite`
- 후보 보드: `project-c-b2-cracked-floor-candidates-v1.png`
- 채택 소스: `project-c-b2-cracked-floor-source-v1.png`

## 1차 후보 발주

```text
Use case: stylized-concept
Asset type: Project-C game environment pixel-art source board for one B2 exit-floor dressing tile
Input images: canonical 2:1 floor silhouette/pixel scale; current flawed raised cracked tile as an avoid reference; q0/q2 live-game palette context.
Primary request: four distinct candidates for one flat damaged concrete tile in a 2×2 board.
Subject: zero-height 2:1 diamonds; one shallow spall, two or three hairline cracks, a few embedded aggregate pixels, and under 3% restrained rust-orange abrasion.
Style: crisp chunky pixel art; blue-black charcoal and desaturated concrete grey.
Constraints: clearly walkable; no side face, rim, bevel, platform thickness, raised debris, cyan/teal/magenta, glow, labels, or text.
Avoid: hole, pit, trench, cover, bright amber patch, hazard stripe, target/interaction cue, neon-flooded floor.
```

좌하단 후보가 가장 조용했지만 두 타원형 박리가 발자국처럼 읽힐 수 있어 한 번 더 좁혔다.

## 최종 단일 소스 발주

```text
Use case: precise-object-edit
Asset type: final source concept for one Project-C 128×64 isometric B2 floor tile
Input images: the lower-left candidate from the 2×2 board; canonical floor silhouette.
Primary request: keep the quiet damage density, but merge the paired oval marks into one small irregular shallow concrete spall.
Subject: one zero-height 2:1 floor diamond; an 8–12% asymmetric spall, two branching hairline cracks, 2–4 tiny aggregate clusters, and at most three tiny restrained rust-orange clusters.
Constraints: flat and walkable; exact 2:1 projection; no side face, bevel, rim, platform thickness, raised rubble, signal colors, text, labels, or watermark.
Avoid: footprints, tire tracks, hole, trench, step, cover, bright amber, hazard/interaction cues, neon floor.
```

ImageGen은 손상 실루엣과 재료 밀도만 소유한다. 실제 다이아·알파·팔레트·손상 면적 상한은
`process_b2_cracked_floor_v1.py`가 공용 `env-floor`를 기준으로 결정론적으로 다시 만든다.

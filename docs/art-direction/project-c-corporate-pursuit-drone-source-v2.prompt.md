# 기업 추적 드론 정적 소스 v2

- 생성: built-in ImageGen (2026-08-04)
- 입력: `project-c-cleaner-crawler-source-v1.png` — 렌더링 밀도와 교체 대상의 캔버스만 참조
- 출력: `project-c-corporate-pursuit-drone-source-v2.png`
- 용도: 호환 코드 ID `Slime`의 사용자 표시·정적 런타임 폴백 `actor-slime.png`
- 상태: **현행 채택** — 사족 기업 보안 하운드로 정체성을 교체했다. 코드 ID·스탯·시드·세이브는 유지한다.

## 최종 프롬프트

```text
Use case: precise-object-edit
Asset type: Project-C game enemy source art for deterministic pixel-sprite conform
Input image: Image 1 is the edit target and rendering-density reference only.
Primary request: Replace the entire tracked cleaning machine with one unmistakable corporate pursuit drone: a compact low quadruped robotic guard hound used to hunt intruders in an abandoned cyberpunk arcade tower.
Subject: four clearly articulated mechanical legs with visible joint gaps and planted feet; narrow wedge-shaped sensor head; armored charcoal torso; exposed servo spine and small cable bundle; one red hostile optical eye; one short cyan scanning slit; one restrained magenta corporate identification strip. No animal flesh or fur. It must read immediately as a combat robot, not a cleaning appliance, blob, bug, tank, or vehicle.
Style/medium: premium hand-authored chunky isometric pixel art matching Image 1's pixel density and material detail; crisp clustered pixels; hard readable silhouette at very small game scale; worn industrial cyberpunk rather than glossy sci-fi.
Composition/framing: one isolated full-body subject, three-quarter isometric south/front view, centered with generous padding; body low but legs clearly separate from the torso; no cropping.
Lighting/mood: readable blue-black shadows, cool steel midtones, small localized cyan and magenta electronics, red hostile optic; restrained rust only on damaged edges.
Color palette: dominant blue-black charcoal and cool gunmetal; secondary cold steel; small cyan/magenta accents and one red eye; substantially less beige, brown, tan, and olive than Image 1.
Background: perfectly flat uniform solid #ff00ff chroma-key background, no shadows, gradients, texture, reflections, floor plane, or lighting variation. Do not use #ff00ff inside the subject.
Constraints: preserve the overall sprite-source scale and three-quarter isometric viewpoint; no text, labels, logos, UI, border, frame, extra props, cast shadow, contact shadow, watermark.
Avoid: crawler tracks, wheels, cleaning tank, detergent bladder, hose, vacuum nozzle, construction vehicle, animal face, organic monster, slime, mushroom, insect anatomy, medieval fantasy, glossy anime mecha, neon-flooded armor, large glowing panels.
```

생성본의 크로마키는 설치된 imagegen `remove_chroma_key.py`의 border 자동 샘플,
soft matte, threshold `12/220`, despill로 제거한다. 최종 96×128 팔레트·2×2 클러스터·발 기준선과
역할별 시안/마젠타 신호는 `Tools/ArtPipeline/process_arcade_occupation_actors_v1.py`가 소유한다.

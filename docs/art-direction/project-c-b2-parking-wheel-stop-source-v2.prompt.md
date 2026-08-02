# Project-C B2 parking wheel stop source v2

- Generator: OpenAI built-in ImageGen
- Mode: canonical precise-object edit, followed by a four-view board generation
- Runtime use: deterministic conform only (`process_b2_parking_dressing_v2.py`)
- Canonical intermediate: `project-c-b2-parking-wheel-stop-canonical-v2.png`
- Approved four-view source: `project-c-b2-parking-wheel-stop-source-v2.png`
- Reference roles:
  - v1 source: edit target
  - `project-c-b2-hero-room-target-v2.png`: art-direction reference
  - `docs/captures/b2-barrel-bay-q0-live-v1.png`: current in-game integration reference

## Canonical edit prompt

```text
Use case: precise-object-edit
Asset type: Project-C 128x64 isometric game floor dressing source
Input images: Image 1 is the edit target; Image 2 is the approved B2 room art-direction reference; Image 3 is the current in-game integration reference.
Primary request: Redesign only the isolated parking/loading-bay wheel stop from Image 1 as a lower, slimmer, less rigid abandoned infrastructure prop that belongs in the dark room of Images 2 and 3.
Subject: one low-profile charcoal rubber-and-oxidized-steel wheel stop, compact anchor feet, chipped edges, subtle asymmetry and wear. Reduce apparent height and bulk by about 25 percent. Replace the large bright yellow blocks with two narrow, heavily worn non-luminous amber paint remnants confined near the ends.
Style/medium: chunky hand-pixelled 2:1 isometric game sprite source; deliberate large pixel clusters and stepped edges; matte post-apocalyptic cyberpunk materials; readable at 128x64 after downsampling.
Composition/framing: preserve the same overall 2:1 isometric facing as Image 1; center exactly one complete object with generous margin.
Scene/backdrop: perfectly flat solid #FF00FF chroma background.
Lighting/mood: restrained readable darkness; no light emission.
Constraints: change only the prop design; keep it isolated; crisp silhouette; no floor tile, shadow, contact shadow, reflection, text, symbols, logo, character, UI, neon, cyan, magenta on the prop, glow, bloom, grain, dithering, semitransparency, antialiasing, photorealism, glossy sci-fi detail, or watermark. Background must be uniform #FF00FF with no texture or lighting variation.
```

## Final four-view board prompt

```text
Use case: stylized-concept
Asset type: Project-C four-view isometric game sprite source board
Input images: Image 1 is the approved canonical wheel-stop design; Images 2 and 3 are art-direction and in-game scale references.
Primary request: Create one clean 2-by-2 source board showing the exact same low charcoal rubber-and-oxidized-steel wheel stop from Image 1 in four consistent camera-quarter isometric views.
Layout: four equal quadrants on one canvas with no labels and no dividers. Top-left = view 0, long axis slopes down-right. Top-right = view 1, long axis slopes down-left. Bottom-left = view 2, long axis slopes down-right from the opposite side, with world-fixed chips and end wear correctly reversed in depth. Bottom-right = view 3, long axis slopes down-left from the opposite side. One complete object centered in each quadrant, identical scale and ground contact, generous margin.
Style/medium: chunky hand-pixelled 2:1 isometric game sprite source; deliberate large pixel clusters and stepped edges; matte post-apocalyptic cyberpunk materials. Keep the object low and slim. Only two narrow heavily worn non-luminous amber paint remnants near the ends.
Scene/backdrop: every quadrant uses the same perfectly flat solid #FF00FF chroma background.
Constraints: same object identity and proportions in all four views; no floor tiles, shadows, reflections, labels, numbers, text, symbols, logo, character, UI, neon, cyan, magenta on the prop, glow, bloom, grain, dithering, semitransparency, antialiasing, photorealism, glossy detail, watermark, borders, or extra objects.
```

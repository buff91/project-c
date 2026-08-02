# Project-C B2 fallen wayfinding source v2

- Generator: OpenAI built-in ImageGen
- Mode: canonical precise-object edit, one geometry correction, then four-view board generation
- Runtime use: deterministic conform only (`process_b2_parking_dressing_v2.py`)
- Canonical intermediate: `project-c-b2-fallen-wayfinding-canonical-v2.png`
- Approved four-view source: `project-c-b2-fallen-wayfinding-source-v2.png`
- Reference roles:
  - v1 source: edit target
  - `project-c-b2-hero-room-target-v2.png`: art-direction reference
  - `docs/captures/b2-barrel-bay-q{0,3}-live-v1.png`: current in-game integration references

## Canonical generation and correction

The first edit removed the bright off-white face and cyan light, reduced the bulk,
and changed the material to charcoal steel and smoked-black glass. The selected
canonical uses the following single-change correction:

```text
Use case: precise-object-edit
Asset type: Project-C 128x64 isometric game floor dressing source
Input images: Image 1 is the edit target; Images 2 and 3 are style and in-game scale references.
Primary request: Change only the geometry and pose of the fallen wayfinding fixture. Collapse it much farther so it lies almost flat on the ground plane: a wide, thin, asymmetrical bent sheet-metal sign skin with a broken frame, one folded corner, two short trailing cable stubs, and visible negative gaps. Maximum visible height must be less than one quarter of its width. It must not have an upright rectangular front face and must not resemble a parking stop, barricade, chest, weapon, loot, or cover object.
Style/medium: chunky hand-pixelled 2:1 isometric game sprite source; deliberate large pixel clusters and stepped edges; matte charcoal steel, cracked smoked-black insert, restrained rust and tiny worn non-luminous amber paint fragments.
Composition/framing: preserve the 2:1 isometric ground-plane facing; center exactly one complete prop with generous margin.
Scene/backdrop: perfectly flat solid #FF00FF chroma background.
Constraints: change only the prop geometry/pose; keep it isolated; no floor tile, shadow, contact shadow, reflection, readable text, arrow, glyph, logo, character, UI, neon, cyan, magenta on the prop, glow, bloom, grain, dithering, semitransparency, antialiasing, photorealism, glossy sci-fi detail, or watermark. Uniform #FF00FF background with no texture or lighting variation.
```

## Final four-view board prompt

```text
Use case: stylized-concept
Asset type: Project-C four-view isometric game sprite source board
Input images: Image 1 is the approved canonical flattened fallen-sign design; Images 2 and 3 are art-direction and in-game scale references.
Primary request: Create one clean 2-by-2 source board showing the exact same almost-flat collapsed wayfinding plate from Image 1 in four consistent camera-quarter isometric views. Preserve its world-fixed torn corner, bent frame, cracked smoked-black insert, negative gaps, and two short cable stubs as the object rotates; maximum visible height remains less than one quarter of width.
Layout: four equal quadrants on one canvas with no labels and no dividers. Top-left = view 0, long axis slopes down-right. Top-right = view 1, long axis slopes down-left. Bottom-left = view 2, long axis slopes down-right from the opposite side. Bottom-right = view 3, long axis slopes down-left from the opposite side. One complete object centered in each quadrant, identical scale and ground contact, generous margin.
Style/medium: chunky hand-pixelled 2:1 isometric game sprite source; deliberate large pixel clusters and stepped edges; matte charcoal steel, restrained rust, tiny worn non-luminous amber paint fragments. Clearly decorative, non-interactive, and not cover.
Scene/backdrop: every quadrant uses the same perfectly flat solid #FF00FF chroma background.
Constraints: same object identity and proportions in all four views; no upright rectangular face, floor tiles, shadows, reflections, readable text, arrow, glyph, logo, character, UI, neon, cyan, magenta on the prop, glow, bloom, grain, dithering, semitransparency, antialiasing, photorealism, glossy detail, watermark, borders, or extra objects.
```

# B2 quiet wall material source v1

- Use case: `stylized-concept`
- Tool path: built-in ImageGen
- Geometry/style reference: `project-c-b2-prop-production-sheet-v2.png`
- Finish reference: `project-c-b2-prop-quality-target-q0-v1.png`
- Runtime diagnosis reference: `docs/captures/b2-wall-joinery-q0-live-v5.png`
- Source board: `project-c-b2-wall-material-source-v1.png`

## Prompt

```text
Use case: stylized-concept
Asset type: production source board for modular isometric pixel-art game wall sprites
Primary request: Create one clean source board containing exactly three isolated QUIET base-wall material modules for the B2 underground parking/service level. These are non-interactive background wall variants, not terminals, doors, lamps, windows, or props.
Input images: Image 1 is the approved production-sheet style and right-rising wall geometry reference; Image 2 is the desired in-game material density and restrained industrial quality reference; Image 3 is the current runtime context showing the repeated center-panel problem.
Scene/backdrop: perfectly flat solid black background, no floor plane, no shadows outside each module.
Subject: three separate full-height isometric wall-face modules in a single horizontal row, each using the same proportions, right-rising top edge, wall thickness, and grounded plinth as Image 1. Variant A: broad welded steel sheet with one low horizontal seam and a small off-center repair plate. Variant B: two large low-contrast steel/concrete fields divided by an asymmetric vertical weld, with one broad chipped patch. Variant C: broad quiet service cladding with a narrow recessed maintenance strip offset to one side. Every module must remain visually quiet.
Style/medium: native-looking deliberate pixel art; clean flat color planes; large 2x2-or-larger pixel clusters; sparse precise edge highlights; no painterly texture.
Lighting/mood: readable blue-black industrial darkness, subtle cool ambient light, no baked glow.
Color palette: low-saturation charcoal, blue-black, cool gray, tiny restrained rust-brown wear only.
Materials/textures: large low-frequency patches and seams; no centered framed rectangle repeated on every module; no full-height bright end caps.
Composition/framing: equal scale, frontally comparable, generous black separation, all modules fully visible.
Constraints: preserve the modular isometric wall proportions from Image 1; keep top cap and bottom plinth consistent across all three; no text, glyphs, signage, screens, cables, vents, handles, lamps, doors, windows, neon, gameplay signals, floor tiles, characters, UI, watermark, or logos.
Avoid: high-frequency speckle, dithering, grain, noisy grime, photorealism, smooth vector art, soft antialiasing, saturated colors, isolated bright dots, card-like white borders, centered inset picture-frame panels.
```

The checked-in board is a material source, not a runtime spritesheet. The v4
conform processor black-keys fixed source windows, collapses them to deliberate
2×2 clusters, copies only the safe center field, and preserves the shared alpha,
12px join band, cap, plinth, pivot, and mirrored direction contract.

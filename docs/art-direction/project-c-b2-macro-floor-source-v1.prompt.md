# B2 macro floor source v1

- Generation mode: built-in ImageGen (`stylized-concept`)
- Runtime role: source-only top-down board for one continuous 2×2 B2 parking-floor patch
- References:
  - `docs/captures/b2-cracked-floor-q0-live-v1.png` — current scale/palette diagnostic only
  - `docs/art-direction/project-c-b2-hero-room-target-v2.png` — approved material density
  - `Assets/_Project/Art/Environment/env-floor.png` — canonical pixel density and cool concrete palette

## Prompt

```text
Use case: stylized-concept
Asset type: Project-C game environment source texture for one 2×2 B2 parking-floor macro patch
Input images: Image 1 is the current live B2 diagnostic and supplies only scale/palette context; do not reproduce its UI, room, walls, props, character, or camera. Image 2 is the approved material-density target. Image 3 is the canonical floor and supplies chunky pixel density and cool concrete palette.
Primary request: create exactly one centered square, strict orthographic top-down floor-material patch representing a continuous 2×2 area. It will later be rotated and isometrically projected by code.
Subject: one continuous cool charcoal concrete/asphalt slab, with no four-cell checkerboard. Add one broad shallow irregular wear/oil-rub shape crossing the center, one restrained hairline crack cluster, a few tiny aggregate/rust chips, and one broken muted grey-white parking line that enters along the right outer edge and makes a shallow L turn near the lower outer edge.
Style/medium: crisp chunky authored pixel art, Project-C post-apocalyptic cyberpunk industrial parking deck; readable darks, sparse material clusters, no painterly blur.
Composition/framing: perfectly square face-on top-down texture, centered with generous flat chroma border; no perspective and no isometric projection.
Color palette: blue-black charcoal, desaturated concrete grey, very limited dark rust; parking paint is muted grey-white, never bright.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background with no gradient, texture, shadow, reflection, or lighting variation.
Constraints: the floor patch itself contains no outer bevel, thickness, rim, side face, raised debris, hole, pit, cover, arrows, letters, numbers, logos, watermark, or visible 2×2 cell grid. Keep the center-crossing wear low contrast. No cyan, teal, magenta, bright amber, yellow hazard stripe, neon glow, interaction cue, or wet mirror reflection. Use #ff00ff only in the background, never inside the floor patch.
```

The generated bitmap is a staging source. `process_b2_macro_floor_v1.py` owns rotation, isometric projection, canonical floor compositing, palette lock, hard alpha, and the 16 runtime cells.

# Collapsed Transit environment source v2 — ImageGen prompt

- Tool: built-in ImageGen, image edit mode
- Input image: six original Project-C environment sprites arranged in a fixed
  3×2 sheet on a flat `#ff00ff` background
- Post-process: chroma-key removal only. Runtime resizing and color reduction are
  handled by `Tools/ArtPipeline/process_postapoc_environment_v2.py`.

```text
Use case: style-transfer
Asset type: six production game environment sprite source assets arranged in a fixed 3-by-2 sheet
Input images: Image 1 is the edit target and layout/style anchor
Primary request: Restyle only the six existing isolated isometric pixel-art assets from medieval stone dungeon into a cohesive abandoned underground transit facility while preserving their exact cell positions, scale, 2:1 isometric perspective, silhouettes, and transparent-space margins.
Cell content, fixed in place: top-left cracked concrete platform floor diamond; top-center damaged concrete transit wall panel; top-right the same wall with a compact amber emergency lamp; bottom-left closed heavy steel blast door in a concrete/metal frame; bottom-center the same blast door open with the matching frame; bottom-right worn concrete service stairs with dark steel stair nosings.
Style/medium: polished chunky isometric pixel art matching the source image's natural hand-painted pixel clusters and dimensional shading; richer material texture and depth than simple geometric icons; crisp hard edges; no smooth vector look.
Color/material direction: blue-black and charcoal shadows, desaturated concrete gray and warm taupe, oxidized dark steel, restrained rust-orange wear; saturated amber only for the physical emergency light and tiny status lamps; one very subtle desaturated teal service stripe, not a glowing magic accent.
Constraints: change only the material language and details; preserve all six silhouettes, their orientation, relative size, baseline, cell separation, and the exact 3-column by 2-row arrangement; each asset must remain completely isolated from the others; maintain readable floor/wall value separation; use sparse cracks, chipped edges, bolts, seams, cable conduit, and limited hazard markings; no characters; no extra props; no text; no labels; no watermark.
Background: keep the entire background a perfectly flat uniform solid #ff00ff chroma-key color. No shadows, gradients, texture, glow, reflections, grid lines, panels, or lighting variation in the background. Do not use #ff00ff anywhere inside the assets.
Avoid: fantasy masonry, wood planks, medieval iron straps, torches, arches, runes, excessive dithering, white outline noise, cyberpunk neon overload, photorealism, smooth 3D rendering.
```

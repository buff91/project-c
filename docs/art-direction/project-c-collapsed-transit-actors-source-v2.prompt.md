# Collapsed Transit actors source v2 — ImageGen prompt

- Tool: built-in ImageGen, image edit mode
- Input image: four original Project-C actor sprites arranged in a fixed 2×2
  sheet on a flat `#ff00ff` background
- Post-process: chroma-key removal only. Runtime sizing, grounding, and color
  reduction are handled by `Tools/ArtPipeline/process_postapoc_actors_v2.py`.

```text
Use case: style-transfer
Asset type: four production isometric pixel-art character sprites in a fixed 2-by-2 sheet
Input images: Image 1 is the edit target and layout anchor
Primary request: Restyle the four isolated fantasy sprites into the first-dungeon cast for a grounded post-apocalyptic anomalous transit facility. Preserve the exact 2-by-2 cell layout, each character's center and ground baseline, overall height, padding, three-quarter isometric-facing pose, and clear gameplay silhouette.
Fixed cell content: top-left becomes a human Bulwark expedition survivor wearing layered scrap armor, a compact respirator helmet, a broad patched metal shield, and a short industrial baton; top-right becomes a hooded human scavenger in worn utility clothing with a half-mask, small buckler, and hooked scrap weapon; bottom-left becomes a squat obsolete security sentry drone with an armored boxy torso, one amber optical lens, small side shield plate, and four short articulated legs while occupying the same cell height and baseline; bottom-right becomes a low leaking contamination ooze, oily charcoal-green rather than magical blue, with embedded rusted metal fragments and two simple dark eyes.
Style/medium: polished chunky game-ready isometric pixel art; natural hand-placed pixel clusters, readable at 48x64; the same material richness, edge wear, concrete-gray/taupe/charcoal palette, rust accents, and controlled lighting as the Collapsed Transit environment; crisp silhouette with selective highlights, not flat geometric icons.
Lighting/mood: dim cold ambient light with one restrained warm edge light; amber is used only for lenses, buckles, and equipment indicators; no large glowing areas.
Constraints: change only the four sprites; keep every sprite isolated inside its original cell with no overlap; preserve the 2-by-2 arrangement, baseline, relative scale, view direction, generous magenta space, and transparent-space margins; no cast shadows; no floor plane; no extra props outside the silhouettes; no text; no labels; no UI; no watermark.
Background: keep the whole background perfectly flat, uniform solid #ff00ff for chroma-key removal, with no gradients, texture, glow, shadows, reflections, borders, or panels. Do not use #ff00ff inside any character.
Avoid: medieval knight armor, goblin anatomy, exposed skeleton bones, cute glossy slime, fantasy weapons, cyberpunk neon, excessive micro-detail, white outlines, noisy dithering, painterly blur, smooth 3D rendering.
```

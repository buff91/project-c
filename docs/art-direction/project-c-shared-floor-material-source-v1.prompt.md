# Shared floor material source v1

- Generation mode: built-in ImageGen (`stylized-concept`)
- Generated artifact: `/Users/buff/.codex/generated_images/019fbc3f-dea1-7461-a00b-dc8da45e4d73/exec-8e9cf6af-b598-488e-8c11-70cc99d59b6d.png`
- Project copy: `docs/art-direction/project-c-shared-floor-material-source-v1.png`
- Runtime role: source-only top-down material field for the shared `env-floor`. The generated bitmap is not a ready-to-import sprite; deterministic conform owns chroma removal, low-frequency clustering, the single-role-safe source ramp, the canonical 2:1 alpha mask, palette lock, and Unity output.
- References:
  - `Assets/_Project/Art/Environment/env-floor.png` — current pixel density and geometry diagnostic only
  - `docs/captures/foundation-grounding-q0-live-v2.png` — live scale and surrounding palette only
  - `/Users/buff/Downloads/IMG_5322.JPG` — broad ruined-concrete plane hierarchy
  - `/Users/buff/Downloads/IMG_5324.JPG` — cool cyberpunk industrial mood only
  - `/Users/buff/Downloads/IMG_5326.JPG` — authored pixel-cluster language

## Prompt

```text
Use case: stylized-concept
Asset type: Project-C source-only material swatch for the shared env-floor game sprite
Input images:
- Image 1 is the current env-floor sprite and provides only the 128x64 pixel-density and neutral gameplay scale contract; explicitly do not copy its brown pointillist noise or diamond silhouette.
- Image 2 is the current live B2 game capture and provides only in-game readability and surrounding palette context; do not reproduce its UI, room, walls, props, character, markers, camera, or isometric composition.
- Image 3 is the ruined metro platform reference and provides the hierarchy of broad continuous concrete planes with restrained wear.
- Image 4 is the neon cyberpunk city reference and provides only the cool charcoal industrial color mood; do not put neon or signage on the floor.
- Image 5 is the isometric cyberpunk room reference and provides chunky authored pixel-cluster language and restrained material density.
Primary request: create exactly one centered square, strict orthographic face-on top-down seamless material swatch for a quiet post-apocalyptic cyberpunk parking-deck floor. It will later be projected into a 2:1 isometric diamond by deterministic code.
Subject: one continuous blue-charcoal concrete/industrial epoxy surface, visually premium but deliberately quiet. Use one dominant midtone plane, two or three broad low-frequency darker abrasion fields, and one or two restrained lighter worn fields. The swatch must be seamless on all four edges and must not contain a recognizable central landmark.
Style/medium: crisp authored pixel art with large coherent 4px-and-larger clusters, clean hard-edged shapes, no painterly blur, no dithering, no isolated dust pixels, no pointillism.
Composition/framing: a perfectly square top-down material patch centered with generous flat chroma padding; no perspective, no isometric projection, no cast shadow.
Color palette: cool desaturated graphite, concrete grey, blue-black shadow; no warm beige base. Very sparse dark rust is acceptable only as a broad subdued trace, never as orange dots.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background with no gradient, texture, shadow, reflection, floor plane, or lighting variation.
Constraints: no outer rim, border, bevel, thickness, side face, panel grid, tile seam, diamond outline, cracks, holes, drains, grates, oil puddles, water, parking paint, hazard stripe, arrows, text, letters, numbers, debris, props, vegetation, neon, cyan, teal, magenta inside the swatch, bright amber, yellow, glow, reflection, watermark, signature, or UI. The floor patch itself must not use #ff00ff.
```

## Acceptance contract

The conform result stays `128x64`, PPU 128 and pivot `(0.5, 0.5)`, with the existing 4,098-pixel hard-alpha diamond. Its visible source pixels are exactly 91.996% `grey-4` midtone plus 8.004% `grey-3` wear arranged as three broad connected masses. Both values stay inside the runtime `.28-.50` luminance bucket, so the environment tone map produces 100% `Stone` and 0% `StoneShadow`, `StoneLight`, or `Outline`; the wear must never become stamped runtime-role contrast. The outer three-pixel material band is `grey-4`; isolated one-pixel noise, a closed outline, direction cues, and gameplay signals are forbidden.

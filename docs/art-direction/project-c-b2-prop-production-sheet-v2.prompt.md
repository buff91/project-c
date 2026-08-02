# B2 prop production sheet v2

- Use case: `stylized-concept`
- Tool path: built-in ImageGen
- Style reference: `project-c-b2-prop-quality-target-q0-v1.png`
- Scale/composition reference: live Unity `b2-prop-quality-q0-live-v1.png`
- Chroma source: `project-c-b2-prop-production-sheet-v2-chroma.png`
- Alpha source: `project-c-b2-prop-production-sheet-v2.png`

## Prompt

```text
Use case: stylized-concept
Asset type: production reference sheet for modular Unity 2D isometric pixel-art environment sprites
Input images: Image 1 is the approved visual target and is authoritative for finish, material language, palette, and prop silhouettes. Image 2 is the current live game room and is authoritative only for camera angle and approximate runtime scale.
Primary request: create a clean modular asset sheet that isolates the exact environment pieces needed to bring Image 2 close to Image 1: (1) a richly framed dark metal wall panel, (2) a left service-wall panel with large amber hose reel, hanging hose and a small lower service box, (3) a pipe/utility wall panel, (4) a quiet wall panel with deep inset frame, (5) a large dead arcade-service terminal/door wall panel, (6) a compact cylindrical portable fuel cell with top cap and amber chevrons, (7) a low heavy parking stop with amber stripe, and (8) a fallen floor direction sign.
Scene/backdrop: perfectly flat uniform #00ff00 chroma-key background, no floor plane, no cast shadows, no gradients, no background texture.
Style/medium: polished hand-authored isometric pixel art, crisp deliberate pixel clusters, strongly readable at game scale, not painterly, not smooth 3D, no anti-aliased blur. Match Image 1's dark charcoal steel, restrained warm ivory highlights, small amber hazard accents, and extremely sparse cyan indicator lights.
Composition/framing: arrange all eight assets as separate non-overlapping cutouts in two orderly rows with generous green spacing. Every wall asset uses the same 2:1 isometric projection, height, baseline, thickness, and modular edge alignment. Show one consistent visible wall face orientation only; do not build a room.
Materials/textures: inset steel panels, narrow bevel highlights, structural vertical ribs, small bolts, restrained edge wear, sparse rust only in creases. Rich material detail but no noisy random speckles.
Constraints: preserve the approved target's grounded industrial arcade-service-room identity. The fuel cell must clearly read as a short cylinder rather than a vending machine. The service wall must be the strongest localized story cluster; quiet panels stay quieter. No baked contact shadow. Crisp edges and generous padding for later background removal.
Avoid: characters, UI, labels, captions, logos, watermarks, readable text, whole rooms, perspective mismatch, magenta neon, saturated cyberpunk rainbow, fantasy, organic shapes, excessive rust, tiny illegible clutter, soft lighting, photorealism.
```

The alpha source was produced from the flat chroma source with the bundled
ImageGen chroma-key remover. Runtime sprites are not cut straight from the
sheet: the conform processor fixes crop boxes, palette, hard alpha, canvas,
pivot contract, and pixel-cluster size before Aseprite promotion.

# Collapsed Transit actors source v2 — legacy ImageGen reference

> 이 2×2 보드는 현행 적 로스터의 원본이 아니다. 현재는 좌상단 원정자 셀만 호환 참고로 남기며,
> 적 6종은 `process_arcade_occupation_actors_v1.py`의 개별 사이버펑크 소스가 단독 소유한다.
> 우상단/하단 세 칸은 비운다. 구형 인물·생물형 적 시안을 다시 만들거나 게임 에셋으로 게시하지 않는다.

- Tool: built-in ImageGen, image edit mode
- Input image: four original Project-C actor sprites arranged in a fixed 2×2
  sheet on a flat `#ff00ff` background
- Post-process: chroma-key removal only. Runtime sizing, grounding, and color
  reduction for the retained player cell are handled by
  `Tools/ArtPipeline/process_postapoc_actors_v2.py`.

```text
Use case: style-transfer
Asset type: one production isometric pixel-art player sprite in the top-left cell of a fixed 2-by-2 sheet
Input images: Image 1 is the edit target and layout anchor
Primary request: Restyle only the top-left character into the player expeditioner for a grounded post-apocalyptic cyberpunk arcade-tower setting. Clear the other three cells to the exact flat magenta background. Preserve the top-left character's center, ground baseline, overall height, padding, three-quarter isometric-facing pose, and clear gameplay silhouette.
Fixed cell content: top-left becomes a human Bulwark expedition survivor wearing layered scrap armor, a compact respirator helmet, a broad patched metal shield, and a short industrial baton. Top-right, bottom-left, and bottom-right contain no character, prop, residue, shadow, or glow—only flat #ff00ff.
Style/medium: polished chunky game-ready isometric pixel art; natural hand-placed pixel clusters, readable at 48x64; the same material richness, edge wear, concrete-gray/taupe/charcoal palette, rust accents, and controlled lighting as the Collapsed Transit environment; crisp silhouette with selective highlights, not flat geometric icons.
Lighting/mood: dim cold ambient light with one restrained warm edge light; amber is used only for lenses, buckles, and equipment indicators; no large glowing areas.
Constraints: change only the retained top-left sprite and erase all content from the other three cells; preserve the 2-by-2 canvas arrangement, retained baseline, scale, view direction, generous magenta space, and transparent-space margins; no cast shadows; no floor plane; no extra props outside the silhouette; no text; no labels; no UI; no watermark.
Background: keep the whole background perfectly flat, uniform solid #ff00ff for chroma-key removal, with no gradients, texture, glow, shadows, reflections, borders, or panels. Do not use #ff00ff inside any character.
Avoid: medieval knight armor, goblin anatomy, exposed skeleton bones, cute glossy slime, fantasy weapons, cyberpunk neon, excessive micro-detail, white outlines, noisy dithering, painterly blur, smooth 3D rendering.
```

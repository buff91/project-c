# Field Deck HUD concept v3 — prompt and provenance

- Generated: 2026-08-02
- Tool: built-in ImageGen
- Use case: `precise-object-edit`
- Status: adopted visual target; concept only, not a runtime asset
- Output: `project-c-field-deck-hud-concept-v3.png`

## Inputs

1. `project-c-field-deck-hud-concept-v2.png` — layout and visual language to preserve.
2. `docs/captures/shared-floor-material-q0-live-v1.png` — unchanged live-world reference.

## Final correction prompt

Preserve the complete v2 game view and Field Deck HUD. Apply only two contract corrections.

1. At current floor B2, leave `▲` normally available, keep `◆` as the only current view with a short magenta
   underline, and dim the impossible `▼` direction to approximately 28% opacity.
2. Keep the lower-left log anchored above the resource rail but grow it rightward/upward to approximately
   208×52 logical px on the 640×360 HUD. It must accommodate four Galmuri9 lines at 12px line height; show two
   cyan example lines and leave two quiet rows.

Do not change the world, camera, five filled health cells, floor ruler, current/view text, minimap, rails, settings
glyph, color hierarchy, line weights, open corners or central play-space clearance. Generated Korean text is a
placement reference only; runtime UXML remains the content source.

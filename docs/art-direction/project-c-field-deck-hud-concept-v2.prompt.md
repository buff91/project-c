# Field Deck HUD concept v2 — prompt and provenance

- Generated: 2026-08-02
- Tool: built-in ImageGen
- Use case: `ui-mockup` + precise correction passes
- Status: QA predecessor; superseded by `project-c-field-deck-hud-concept-v3.png`
- Output: `project-c-field-deck-hud-concept-v2.png`

## Inputs

1. `docs/captures/shared-floor-material-q0-live-v1.png` — exact live-game world, camera and composition.
2. `/Users/buff/Downloads/IMG_5324.JPG` — restrained cyberpunk density and emissive hierarchy only.
3. Earlier Field Deck iterations — structure-preserving correction targets.

## Final prompt set

Replace only the screen-space HUD while preserving the world, camera, character, selection marker and clear
central play space. Build a repaired fixer field deck from matte blue-black translucent plates, hairline cool-gray
structure, open asymmetric corners and short magenta data ticks. Amber is reserved for current floor or confirmed
selection; teal is reserved for valid system/context state.

- Upper left: five filled red segmented health cells and exact `10/10`.
- Upper right: one integrated instrument with a floor ruler whose top is `8F` and bottom/current is `B2`, exact
  `현재 B2 · 보기 B2`, vertical `▲ ◆ ▼` view control with only the center state underlined magenta,
  `HEIGHT 0`, `VIEW 1/4`, rotate glyphs, three-slider settings glyph, menu glyph and compact minimap.
- Lower left: one resource rail for medkit, bomb, coolant and `백팩 I`; no false zero count on backpack.
- Lower right: one action rail for close combat, wait and turn state. Close combat stays neutral white/magenta in
  this exploration state; do not mark both floor and melee as amber.
- Message readout remains a quiet two-line plate above the resource rail.

Avoid thick metal frames, bevels, individual chunky button boxes, miniature painted-object icons, gradients,
bloom, blur, glass cards, a full-width opaque footer, debug FOV controls, trademarks and watermarks.

The generated text is a placement reference only. Runtime strings and values remain data-bound UXML content.

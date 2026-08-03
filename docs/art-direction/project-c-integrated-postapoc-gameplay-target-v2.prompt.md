# Integrated post-apocalyptic gameplay target v2 — built-in ImageGen prompts

> **사용 범위**: 이 이미지의 월드 재료·밀도·조명·캐릭터 스케일만 참고한다.
> 생성된 UI의 배치와 크기는 실제 패널 좌표계를 반영하지 못했으므로 UI 구현 레퍼런스로
> 사용하지 않는다. UI는 `docs/UI_ARCHITECTURE.md`와 실제 PC Game View 캡처를 따른다.

## Pass 1 — world and UI cohesion

```text
Use case: style-transfer
Asset type: shippable 16:9 PC game vertical-slice art-direction target, isometric pixel-art dungeon crawler
Input images: Image 1 is the current game capture and the composition/edit target; Image 2 is the environment density, material, lighting, and character-scale style reference; Image 3 is the crisp chunky pixel-art, cutaway-diorama, decay, and localized amber-light reference.
Primary request: Repaint the entire current game screen into one cohesive post-apocalyptic cyberpunk faction-controlled arcade-tower visual target. Preserve the isometric camera, the player-centered room, the functional HUD zones (health at upper left, floor/minimap tools at upper right, status near the room, action dock along the bottom), and the basic navigable room geometry. Redesign the art, not the game rules.
Scene/backdrop: a collapsed underground service level inside an abandoned arcade complex, with cracked concrete slabs, broken ceramic tile, exposed rebar and cables, rusted pipes, a damaged blast door, sparse weeds or damp growth, and a visible lower-level breach. One restrained teal access/cooling signal contrasts with practical amber emergency lights.
Composition/framing: enlarge the playable diorama by roughly 35 percent so it dominates the central screen and fills about two thirds of the canvas width; reduce dead black space; keep enough clear edge space for HUD. Make three elevation levels immediately readable through silhouette, front-face thickness, and light.
Style/medium: crisp hand-authored-looking isometric pixel art with chunky clean pixels and unified outline weight; production game screenshot, not painterly concept art, not 3D render. All environment tiles, props, player, enemy, effects, and UI icons must feel from one artist and one pixel grid.
Characters: the player is a readable practical expeditioner. Every hostile must read immediately as an occupation soldier, corporate robot, inspection drone, or cyberpsycho; no fantasy creature, mutant, fungus, undead, or living ooze. Player and enemy must separate clearly from the floor at gameplay scale.
UI direction: slim industrial survival-interface overlay using charcoal steel, faded off-white, restrained hazard amber, and small teal system accents. Flat pixel frames, clipped corners, stencil/raster typography, compact icon clusters. Remove medieval stone, parchment, ornate gold, thick beige bevels, and brown fantasy panels. Keep the existing information hierarchy but make the UI visually subordinate to the dungeon. Preserve existing visible labels where possible; do not add new words or logos.
Lighting/mood: cold blue-black ambient darkness with localized amber pools that reveal material texture; teal only for access/cooling/system signals; retain readable midtones instead of crushing everything to black.
Constraints: preserve 16:9 game-screen framing and isometric gameplay readability; no extra UI panels; no cinematic camera change; no photorealism; no smooth gradients; no watermark; no logo.
Avoid: generic glossy sci-fi, medieval fantasy, uniform black walls and floors, tiny room floating in empty space, muddy outlines, random high-frequency pixel noise, excessive bloom, oversized HUD, generated gibberish text, ornate gold borders.
```

## Pass 2 — UI-only refinement

```text
Use case: precise-object-edit
Asset type: final 16:9 PC game art-direction target
Input images: Image 1 is the edit target.
Primary request: Change only the HUD and UI styling. Preserve the entire isometric world, room geometry, lighting, characters, props, camera, pixel-art rendering, and composition exactly as Image 1.
UI redesign: replace the remaining thick gold-outlined rectangular fantasy buttons with a cohesive minimal industrial survival HUD. Use charcoal steel, faded off-white, thin desaturated teal system lines, tiny hazard-amber selection marks, clipped/chamfered corners, subtle rivet or stencil details, and crisp pixel icons. Make panels visually lighter and flatter, not ornamental.
Bottom layout: consolidate consumables and backpack into one continuous compact quick-slot rail at bottom left; consolidate combat, wait, context, and turn state into one compact action rail at bottom right. Keep generous space between the two rails. Active/selected state may use a small amber edge or tab, never a full brown fill.
Upper layout: keep health at upper left as small pixel hearts; combine floor information and minimap tools into one compact upper-right instrument cluster with thin teal grid lines and no beige stone frame.
Floating status: turn READY and route hint into two small translucent signal chips near the room edge, not large framed boxes.
Typography: narrow raster/stencil pixel font, off-white or teal; amber only for current selection. Preserve existing visible wording where possible and do not invent new labels.
Constraints: edit UI only; exact same world pixels and lighting; no new characters or props; no medieval gold bevels; no brown fantasy panels; no glossy sci-fi; no rounded mobile-app cards; no smooth gradients; no extra UI; no logo; no watermark.
```

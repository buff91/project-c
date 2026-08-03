# 합선 검사 드론 정적 소스 v1

- 생성: built-in ImageGen (2026-08-03)
- 참조: `project-c-arcade-occupation-roster-concept-v3.png` — 화풍·카메라·상대 배율만 참조
- 용도: 코드 ID `ArcDrone`의 정적 런타임 폴백 `actor-arc-drone.png`
- 상태: **정적 소스 채택** — `process_arcade_occupation_actors_v1.py` conform 완료, 애니메이션은 별도 승인

## 프롬프트

> Create one isolated full-body game actor source sprite for Project-C, using the provided roster concept only as the style, camera, pixel-scale, palette, and world reference. The subject is `ArcDrone`, a short-circuited industrial inspection drone from an abandoned arcade tower in a post-apocalyptic cyberpunk city. Give it a low, horizontally wide silhouette, clearly smaller and shorter than a person: compact rectangular/oval armored inspection chassis, an exposed sparking discharge coil on top or rear, a small sensor cluster, and insulated flotation pods or compact insulated wheels. It must read as a hovering or rolling inspection machine, not a tracked cleaning robot. Add exactly one tiny dark-red corporate IFF indicator as the only saturated red accent, with worn dark charcoal steel, muted concrete gray, restrained dirty amber safety marks, and small cyan insulation details. No weapon arm, gun, humanoid limb, cleaning hose, broom, scrubber, tank tread, forklift form, organic part, monster face, fantasy, supernatural energy, or magic. Show exactly one centered actor, fully visible with comfortable margins, in 3/4 isometric south/front view matching the reference roster scale. Use crisp hard-edged modern pixel art with deliberate 2×2-feeling clusters and a compact readable silhouette. Put it on one flat uniform pure `#ff00ff` chroma-key background. No shadow, floor plane, pedestal, scenery, text, label, UI, frame, border, sprite sheet, or alternate view. Keep every coil spark and antenna away from the canvas edges.

생성본은 설치된 imagegen `remove_chroma_key.py`의 border 자동 샘플, soft matte,
threshold `12/220`, despill로 크로마키를 제거한다. 최종 런타임 규격은 후속 conform 프로세서가 소유한다.

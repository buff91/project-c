# 기업 진압 로봇 정적 소스 v1

- 생성: built-in ImageGen (2026-08-03)
- 참조: `project-c-arcade-occupation-roster-concept-v3.png` — 화풍·카메라·상대 배율만 참조
- 용도: 코드 ID `Skeleton`의 정적 런타임 폴백 `actor-skeleton.png`
- 상태: **정적 소스 채택** — `process_arcade_occupation_actors_v1.py` conform 완료, 애니메이션은 별도 승인

## 최초 생성 프롬프트

> Create a single production source sprite concept for Project-C, using the attached roster image ONLY as the visual reference for pixel-art rendering, muted industrial palette, 3/4 isometric south/front camera, chibi scale, and readable silhouette. Subject: one HEAVY CORPORATE RIOT ROBOT, code identity Skeleton, deployed inside a ruined arcade-tower cyberpunk setting. Exactly one full-body upright machine, centered and fully visible, standing in a weighty enforcement-ready pose. The robot is 2.5–3 heads tall, unmistakably mechanical and nonhuman: broad armored torso, thick segmented corporate riot plating, reinforced shoulders, piston joints, compact sensor head with opaque dark faceplate, heavy planted mechanical feet. One forearm integrates a compact powered shock/baton striking unit or blunt shock arm; it must read as a non-bladed riot-control weapon. NO SHIELD, no firearm, no sword, no axe. Add exactly one tiny dark-red IFF indicator light as a minor accent. Pixel art with deliberate chunky clusters, crisp hard edges, readable at game-sprite scale, no antialiasing haze, no photorealism. Background must be one perfectly flat solid chroma color #ff00ff edge-to-edge. No shadow, no cast shadow, no contact shadow, no floor, no platform, no environment, no gradient, no glow spill, no particles, no text, no label, no border, no frame, no UI, no sprite sheet. Absolutely no human skin, no exposed flesh, no bones, no skeletal anatomy, no skull face or skull motif. No fantasy, no magic, no medieval elements.

## 둔기형 충격 팔 교정 프롬프트

> Edit this exact single robot sprite while preserving its composition, pose, pixel-art style, flat #ff00ff background, armor, proportions, camera, and all other details. Change ONLY the weapon end on the large integrated forearm at image-left: remove the pointed cone/drill/nozzle completely. Replace it with a short, thick, BLUNT riot-control shock ram or baton head with a flat capped rectangular/rounded end, clearly non-bladed and non-projectile. It must not resemble a drill, gun barrel, sword, spear, axe, spike, or cutting tool. Keep exactly one tiny dark-red IFF sensor light. Maintain exactly one robot, no shield, no floor, no shadow, no text, no frame, no additional objects, no human skin, no bone or skull motif, no fantasy.

투명 소스 목표 경로는 `project-c-corporate-riot-robot-source-v1.png`다. 설치형 imagegen
`remove_chroma_key.py`가 복구되면 border 자동 샘플, soft matte, threshold `12/220`, despill로
크로마키를 제거하고 알파·투명 모서리·마젠타 프린지를 검증한다.

# 기업 보안 사수 정적 소스 v1

- 생성: built-in ImageGen (2026-08-03)
- 참조: `project-c-arcade-occupation-roster-concept-v3.png` — 화풍·카메라·상대 배율만 참조
- 용도: 코드 ID `Slinger`의 정적 런타임 폴백 `actor-slinger.png`
- 상태: **정적 소스 채택** — `process_arcade_occupation_actors_v1.py` conform 완료, 애니메이션은 별도 승인

## 최종 프롬프트

> Create a single production source sprite concept for Project-C, using the attached roster image ONLY as the visual reference for pixel-art rendering, muted industrial palette, 3/4 isometric south/front camera, chibi scale, and readable silhouette. Subject: one CORPORATE SECURITY MARKSMAN, code identity Slinger, in a ruined arcade-tower cyberpunk setting. Exactly one full-body character, centered and fully visible, standing in a stable ranged-combat ready pose. The character is 2.5–3 heads tall, with a clearly ranged silhouette distinct from a melee assault soldier: shouldered compact ARC CARBINE held across the upper torso and aimed slightly down/front, stock visibly seated at shoulder, compact barrel, no sling weapon and no fantasy bow. Wear disciplined corporate ballistic armor, tactical chest rig, sealed opaque visor plus respirator, fitted armored trousers and boots, restrained utility pouches; no hooded fantasy raider look. Add exactly one tiny dark-red IFF indicator light as a minor accent. Pixel art with deliberate chunky clusters, crisp hard edges, readable at game-sprite scale, no antialiasing haze, no photorealism. Background must be one perfectly flat solid chroma color #ff00ff edge-to-edge. No shadow, no cast shadow, no contact shadow, no floor, no platform, no environment, no gradient, no glow spill, no particles, no text, no label, no border, no frame, no UI, no sprite sheet. No fantasy, no magic, no medieval elements, no exposed skin, no skull motif.

투명 소스 목표 경로는 `project-c-corporate-marksman-source-v1.png`다. 설치형 imagegen
`remove_chroma_key.py`가 복구되면 border 자동 샘플, soft matte, threshold `12/220`, despill로
크로마키를 제거하고 알파·투명 모서리·마젠타 프린지를 검증한다.

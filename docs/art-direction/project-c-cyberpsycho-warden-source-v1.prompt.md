# 감시자 사이버사이코 정적 소스 v1

- 생성: built-in ImageGen (2026-08-03)
- 참조: `project-c-arcade-occupation-roster-concept-v3.png` — 화풍·카메라·상대 배율만 참조
- 용도: 코드 ID `GraveWarden`, 표시명 `감시자`의 정적 런타임 폴백 `actor-grave-warden.png`
- 상태: **정적 소스 채택** — `process_arcade_occupation_actors_v1.py` conform 완료, 애니메이션은 별도 승인

## 프롬프트

> Create one isolated full-body boss actor source sprite for Project-C, using the provided roster concept only as the style, camera, pixel-scale, palette, and world reference. The subject is `GraveWarden`, displayed in game as `감시자`: a human cyberpsycho corporate enforcer in the abandoned arcade tower, not a supernatural grave creature. Give the human a massive three-heads-tall silhouette, much broader and heavier than an ordinary occupation soldier, with the entire body sealed and no exposed skin or visible face. Fit a bulky dark-charcoal heavy exoskeleton over obvious human anatomy, with oversized shoulder, forearm, and leg reinforcement, worn corporate restraint/control hardware, a locking collar, restraint cabling, and dampener modules. Use a narrow red visor across a clearly helmeted human head and no more than one additional tiny dark-red corporate IFF indicator. The pose should communicate unstable aggression while remaining grounded on two boots with recognizable human joints, two arms, and two legs. Materials are blackened steel, concrete gray, muted dirty amber hazard markings, and tiny cold-cyan restraint status accents. No carried fantasy weapon; integrated reinforced blunt gauntlets are acceptable. This is a human in an exoskeleton, not a robot, mech, or tracked vehicle. No wheels, tank treads, organic core, glowing flesh, tentacle, skull, bone, grave motif, magic, supernatural aura, fantasy armor, or horn. Show exactly one centered actor, fully visible head-to-boots with generous margins, in 3/4 isometric south/front view matching the reference roster scale. Use crisp hard-edged modern pixel art with deliberate 2×2-feeling clusters and a compact readable boss silhouette. Put it on one flat uniform pure `#ff00ff` chroma-key background. No shadow, floor plane, pedestal, scenery, text, label, UI, frame, border, sprite sheet, or alternate view. Keep all restraint cables attached to the silhouette and away from the canvas edges.

생성본은 설치된 imagegen `remove_chroma_key.py`의 border 자동 샘플, soft matte,
threshold `12/220`, despill로 크로마키를 제거한다. 최종 런타임 규격은 후속 conform 프로세서가 소유한다.

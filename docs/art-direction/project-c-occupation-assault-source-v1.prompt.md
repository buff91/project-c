# 점거군 돌격병 정적 소스 v1

- 생성: built-in ImageGen (2026-08-03)
- 참조: `project-c-arcade-occupation-roster-concept-v3.png` — 화풍·카메라·상대 배율만 참조
- 용도: 코드 ID `Goblin`의 정적 런타임 폴백 `actor-goblin.png`
- 상태: **B2 대표 적 1차 채택** — 애니메이션/정식 Aseprite 승격은 별도 승인

## 프롬프트

> Create one anonymous human occupation-force assault soldier for a post-apocalyptic cyberpunk arcade tower. Lean 2.5-to-3-head-tall infantry silhouette; sealed charcoal tactical jacket and light composite plates; compact asymmetric salvage pack; lower-face respirator and narrow opaque visor; one visibly mechanical forearm; straight compact powered shock baton held low, with a simple cylindrical silhouette and no hook, blade, axe head, wedge, or pry-bar curve. Faded corporate-surplus construction, worn amber chevron, exactly one tiny dark-red IFF lamp. Agile, lighter than a riot robot. Three-quarter isometric south/front pose, one isolated full-body subject, crisp flat cel-shaded clustered shapes. Perfectly flat `#ff00ff` chroma-key background. No shield, axe, sword, bow, cape, robe, medieval armor, fantasy motif, skull, horn, magic, mushroom, exposed friendly face, shadow, floor, text, watermark, extra prop, or frame.

초기 생성본의 손 장비가 도끼처럼 읽혀 아래 국소 편집 프롬프트로 무기만 다시 고정했다.

> Edit only the weapon in the soldier's hand. Replace it with a straight compact powered shock baton: one simple cylindrical metal rod with a short insulated grip and a tiny amber electrical contact at the tip. No hook, blade, axe head, wedge, pry-bar curve, sword edge, or oversized weapon. Preserve the exact soldier, pose, proportions, armor, palette, lighting, pixel-cluster style, magenta background, framing, and every other detail unchanged.

생성본의 크로마키는 설치된 imagegen `remove_chroma_key.py`의 border 자동 샘플,
soft matte, threshold `12/220`, despill로 제거했다. 최종 96×128 팔레트·2×2 클러스터·발 기준선은
`Tools/ArtPipeline/process_arcade_occupation_actors_v1.py`가 소유한다.

# 기업 오염처리 크롤러 정적 소스 v1 (폐기)

- 생성: built-in ImageGen (2026-08-03)
- 참조: `project-c-arcade-occupation-roster-concept-v3.png` — 화풍·카메라·상대 배율만 참조
- 용도: 코드 ID `Slime`의 정적 런타임 폴백 `actor-slime.png`
- 상태: **2026-08-04 폐기** — 정체가 모호한 청소기 실루엣과 갈색 재료 비중 때문에
  `project-c-corporate-pursuit-drone-source-v2.png`로 대체했다. 아래 프롬프트는 생성 이력 보존용이며
  런타임 재생성에 사용하지 않는다.

## 프롬프트

> Create one low, wide, broken corporate-facility cleaning robot for a post-apocalyptic cyberpunk arcade tower. Compact tracked industrial cleaner chassis; low rectangular body; cracked but clearly mechanical detergent/coolant tank; short suction hose tucked close to the silhouette; worn intake grille; faded charcoal gray metal; one worn amber hazard chevron; exactly one tiny dark-red IFF status lamp. It must read instantly as a robot, never as a slime, mushroom, animal, monster flesh, or living blob. Three-quarter isometric south/front view, one isolated subject, flat cel-shaded clustered shapes, no fantasy or supernatural motifs. Perfectly flat `#ff00ff` chroma-key background, no shadow, floor, text, watermark, magenta subject color, teal glow, weapon, extra prop, or frame.

생성본의 크로마키는 설치된 imagegen `remove_chroma_key.py`의 border 자동 샘플,
soft matte, threshold `12/220`, despill로 제거했다. 최종 96×128 팔레트·2×2 클러스터·발 기준선은
`Tools/ArtPipeline/process_arcade_occupation_actors_v1.py`가 소유한다.

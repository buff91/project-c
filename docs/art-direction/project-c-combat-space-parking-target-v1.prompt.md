# 전투 공간 타깃 v1 — 지하 주차장 (B2~B1 밴드) — 생성 기록

- **용도**: 전투 공간 정체성 층위(개선 플랜 v2 §배치 5-2). "CP2077 전투 장소 → 턴제
  아이소" 번역의 지하 밴드 기준 — 기둥 엄폐, 폐차, 침수 웅덩이(프로파일 A 감전 반응 무대),
  hazard 마킹 절제, 앰버+시안 광원 웅덩이. **타일+소품 조합으로 구현**하며 별도 씬을
  만들지 않는다. M5 환경 시트 재생성과 소품 발주가 이 타깃을 기준으로 삼는다.
- **상태**: 방향 타깃 승격(2026-07-30). 그대로 슬라이스하지 않는다. 간판의 뭉개진
  글자는 마감에서 제거 대상.
- **재현값**: checkpoint `zavychromaxl_v100.safetensors` + LoRA
  `project-c-pixelart-redmond-sdxl-v1-lite64` 0.45/0.45, 1152×896, seed `61003`,
  dpmpp_2m/karras, steps 28, cfg 6.0, denoise 1.0.
- **positive**: Pixel Art, PixArFK, polished isometric pixel art diorama, abandoned
  underground parking garage at night, turn-based tactics game battle arena, readable
  2:1 tile floor, concrete pillars as cover, wrecked cars, cable trays, puddles
  reflecting cyan neon service lights, amber hazard lamp, dark blue-black shadows,
  crisp hard pixel clusters, restrained palette
- **negative**: text, letters, words, signage text, watermark, logo, photorealism,
  smooth 3d render, vector art, anti-aliasing haze, blurry, low quality, fantasy,
  medieval, characters, people, portrait

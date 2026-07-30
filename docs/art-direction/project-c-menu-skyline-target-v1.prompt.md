# 메인 메뉴 네온 스카이라인 타깃 v1 — 생성 기록

- **용도**: 무드 백드롭 층위(개선 플랜 v2 §배치 5-3). 메인 메뉴 배경 v2 소스 후보 —
  구 `project-c-main-menu-backdrop-source-v1`을 대체한다. `ui-*` 960×540/PPU 64 규격으로
  마감 시 `process_ui_backdrops_v1.py` 경로를 따른다.
- **상태**: 방향 타깃 승격(2026-07-30). 그대로 슬라이스하지 않는다.
- **재현값**: checkpoint `zavychromaxl_v100.safetensors` + LoRA
  `project-c-pixelart-redmond-sdxl-v1-lite64` 0.45/0.45, 1344×768, seed `61001`,
  dpmpp_2m/karras, steps 28, cfg 6.0, denoise 1.0.
- **positive**: Pixel Art, PixArFK, cinematic wide pixel art matte painting, ruined
  cyberpunk megacity skyline at night seen from a rooftop, rain haze, half-broken
  holographic billboards, magenta and cyan neon glow pools, amber emergency lights,
  dark blue-black sky, abandoned overgrown towers, moody atmospheric lighting,
  crisp hard pixel clusters, restrained palette
- **negative**: text, letters, words, signage text, watermark, logo, photorealism,
  smooth 3d render, vector art, anti-aliasing haze, blurry, low quality, fantasy,
  medieval, characters, people, portrait

# 전투 공간 타깃 v1 — 아케이드 홀 (1F~5F 밴드) — 생성 기록

- **용도**: 전투 공간 정체성 층위(개선 플랜 v2 §배치 5-2). 지상 밴드 기준 — 셔터 점포·
  자판기·네온 간판 국소 광원 풀 + 저채도 젖은 콘크리트 바닥. 타일+소품 조합으로 구현.
- **상태**: 방향 타깃 승격(2026-07-30, v3 재추출본 — v1은 바닥 마젠타 범람, v2는 무드
  소실로 기각). 간판 글자는 뭉개진 채 생성되므로 마감에서 무문자 간판으로 교체한다.
  렌더가 다소 회화적이나 재료·광원 언어 기준으로 사용하고 픽셀 문법은
  environment-neon-style-v1 스타일 트랜스퍼가 진다.
- **재현값**: checkpoint `zavychromaxl_v100.safetensors` + PixelArtRedmond 0.45,
  1152×896, seed `61021`, dpmpp_2m/karras, steps 28, cfg 6.0, denoise 1.0.
- **positive**: (아케이드 v3 프롬프트 — dark desaturated concrete gray floor, deep
  blue-black night shadows dominate, one flickering cyan neon sign and one magenta
  neon sign casting small local light pools, one amber emergency light 핵심)
- **negative 핵심**: pink/magenta/saturated floor, bright scene, daylight, neon flood

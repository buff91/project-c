# 메인 메뉴 배경 소스 v2 — 네온 스카이라인 (M5 잔여 마감)

- **채택**: 2026-07-30, art_runner job `ART-20260730-070942-2c2580` 후보 **C02**.
  레시피 `ui-menu-backdrop-v2`(별도 세션이 커밋 2bf8bfb로 신설·큐 투입, 이 세션이
  실행·판정·채택) — txt2img 1344×768, 타깃 `project-c-menu-skyline-target-v1.png`의
  재현값. 구 병원 복도 소스(`...-source-v1`)를 대체하는 **마지막 구테마 유저 노출
  자산 교체**다.
- **판정**: 4후보를 실제 conform 경로(`build_main_menu_backdrop` — 480×270 축소·
  팔레트 잠금·NEAREST ×2)로 마감해 비교. C02 채택 근거: 옥상에서 내려다본 시점이
  가장 명확(전경 건물 프레임 + 협곡 원근)하고, 중앙·상단이 어두워 메뉴 타이틀/카피
  텍스트 자리가 살아 있다. C01·C03은 중앙이 광원 웅덩이로 붐비고, C04는 마젠타
  밴드가 플러드 수준(신호 분리 원칙 위배)이라 기각. 판독 가능 텍스트·인물 없음.
- **마감**: `Tools/ArtPipeline/process_ui_backdrops_v1.py` SOURCE를 v2로 갱신 후
  실행 — `Assets/_Project/Art/Runtime/ui-main-menu-backdrop.png` 960×540/PPU 64,
  off-palette 0. 실화면: `docs/captures/main-menu-backdrop-v2-live.png`
  (플레이 모드 UI Toolkit 캡처, 2560×1440).

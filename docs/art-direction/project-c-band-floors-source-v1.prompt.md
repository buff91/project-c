# 깊이 밴드 바닥 소스 시트 v1 — mid/deep/boss ×기본/raised (플랜 v2 배치 1-1)

- **채택**: 2026-07-30, art_runner job `ART-20260730-125829-c4b4de` 후보 **C04**
  (2라운드). 레시피 `environment-band-floors-v1`.
- **소스 보드**: 현행 승인 `env-floor`를 3×2 셀에 깔고 셀마다 밴드 오염 어휘의
  조악한 힌트를 스크립트로 미리 그린 합성본(`project-c-band-floors-source-board-v1.png`
  — stairs-source-v2의 "crude marker 교체" 방식). 열 = mid/deep/boss
  (subjects/env-floor-*.yaml 어휘), 행 = 기본/raised(얕은 전면 립).
- **1라운드 기각(denoise 0.55)**: 석재색이 웜 → 한색 세이지로 드리프트(§1-c 위반),
  boss 힌트 소실로 밴드 식별 실패. 2라운드는 힌트 확대 + denoise 0.5 + 프롬프트에
  "warm gray 유지·틸은 오른쪽 열만" 명시.
- **2라운드 판정**(밴드별 3×3 + 라이더 게이트, `docs/captures/band-floors-gate-v1.png`):
  C01 차점(boss 정체성이 앰버 반점 수준), C02 기각(deep 틸 플러드 — Hole 신호 충돌),
  C03 기각(밴드 구분 과소), **C04 채택**(mid 클린 / deep 균열+철근 / boss hazard 조각
  — 구분 최상). C04의 deep-raised 틸 웅덩이는 conform의 **틸 억제 패스**가 결정론적으로
  제거한다(아래).
- **마감**: `Tools/ArtPipeline/process_band_floors_v1.py` — 웜 가드(한색 드리프트 역보정),
  non-boss 틸 억제 + 잠금 후 틸 팔레트 금지 게이트, §1-c 명도 게이트(기본 바닥 대비
  ±0.08), despeckle. 산출 6종은 H32/V0.39~0.46으로 기본 바닥(H28/V0.40)과 같은 계열
  (`docs/captures/band-floors-conform-v1.png`).
- **연결**: `Art/Environment/env-floor-{mid,deep,boss}(-raised).png` 정식 파일명 →
  `ProjectCEnvironmentCatalog` 밴드 슬롯 6개에 할당(에디터 세션). 슬롯이 채워지며
  절차 `BandOverlayColor` 임시 대행은 자동 비활성(`BandFloorFallsBackToShared`).

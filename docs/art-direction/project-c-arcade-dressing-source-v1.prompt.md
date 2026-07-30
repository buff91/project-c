# 아케이드 드레싱 소스 시트 v1 — hospital* 9슬롯 재발주 (M5 잔여)

- **채택**: 2026-07-30, art_runner job `ART-20260730-052610-dd8f9e` 후보 **C03**
  (seed 2023961450). 레시피 `environment-neon-dressing-v1` —
  `environment-neon-style-v1`에서 분기해 소스를 병원 드레싱 보드
  (`project-c-hospital-dressing-source-v1.png`)로, 어휘를 아케이드 소품으로 교체.
  벽 모듈의 의미 교체(배관→자판기 등) 때문에 denoise 0.55→0.62 상향.
- **내용**: 3×2 보드 — 상단 바닥 3종(그레이트/균열 상가 타일+전단지/서비스 패널),
  하단 벽 3종(자판기·꺼진 홀로 패널·상태 패널). 출력 파일명 계약은 구판 유지
  (`env-floor-grate/cracked/service`, `env-wall-{pipes,window,cabinet}-rising-*`).
- **판정**: §1-d 게이트 — 후보 바닥 셀 3×3 위 라이더 스프라이트 합성
  (`docs/captures/arcade-dressing-gate-v1.png`). C03 채택 근거: 균열선+전단지
  어휘(플랜 v2 배치 1의 1F~5F 오염 어휘)와 플랫 클러스터 문법, 신호색 점 사용.
  C01 어두운 함몰 반복(Hole 오독), C02 벽 틸 프레임 넓음(신호색 충돌),
  C04 바닥 틸 얼룩(Hole=틸 예약 충돌)으로 기각.
- **전처리**: 생성 배경이 `#ff00ff` 평면으로 보존돼 별도 알파 전처리 불필요 —
  프로세서의 크로마키가 그대로 처리한다.
- **마감**: `Tools/ArtPipeline/process_hospital_dressing_v1.py` (팔레트 잠금 직후
  §1-d despeckle 패스 포함).
- **정합 패스(2026-07-30)**: 첫 마감본이 grey-* 램프(청회색)로 잠겨 기본 벽(tile-*
  웜 브라운)과 색온도가 갈라졌다. 프로세서에 저채도 벽 몸통 웜 시프트(게인
  1.18/1.00/0.78, 명도 대역 40~235·채도 ≤0.28)와 바닥 오버레이 명도 ×0.92를 추가해
  재마감 — 실화면 비교는 `arcade-dressing-live-v1.png`(전) → `-v2.png`(후).

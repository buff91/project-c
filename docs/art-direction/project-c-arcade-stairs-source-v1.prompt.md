# 아케이드 계단 소스 시트 v1 — 하행 계단 특수 소스 재발주 (M5 잔여)

- **채택**: 2026-07-30, art_runner job `ART-20260730-070942-0d5ac5` 후보 **C04**.
  레시피 `environment-neon-stairs-v1`(별도 세션이 커밋 2bf8bfb로 신설·큐 투입,
  이 세션이 후보 판정·채택) — 구판 `...collapsed-transit-stairs-source-v2.png`를
  소스로 denoise 0.45 스타일 트랜스퍼. v3 환경 채택 라운드에서 빠졌던 마지막
  병원판 소스가 이것으로 소거됐다.
- **판정**: 프로세서가 소비하는 좌상단 하행 셀을 최종 크기(128×80)로 드라이런
  conform해 비교(`docs/captures/arcade-stairs-conform-v1.png` — C03|C04|구판 순).
  C01은 피트 내부 틸 광(Hole=틸 신호 예약과 충돌), C02는 하행 단이 어둠에 묻혀
  판독 불가로 기각. C04는 하행 트레드 분리가 가장 명확하고 안전 계단=앰버 신호
  (§1-c)가 커브·단에 실려 채택. C03은 트레드가 어두워 차점.
- **전처리**: 생성 배경 불투명(알려진 버릇) → 승격 시 테두리 연결 플러드 알파
  (tol 14) + 1248→1254 리사이즈(SDXL 8배수 내림 복원).
- **마감**: `Tools/ArtPipeline/process_postapoc_environment_v2.py`의 `STAIRS_SOURCE`를
  이 파일로 갱신 후 재실행 — `env-stairs-down-rising-{right,left}.png` 2종만 변경되고
  나머지 14종은 바이트 동일(멱등 확인). 상행 셀은 스타일 참조용으로 소비하지 않는다.

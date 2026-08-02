# B2 프롭 패밀리 개념 보드 v1 — 생성 기록

- **생성 방식**: Codex 내장 ImageGen 스타일 보드 생성 후 `precise-object-edit` 교정.
- **산출물**: `project-c-b2-prop-family-concept-v1.png` (1536×1024).
- **용도**: 실루엣, 상대 스케일, 재료 면적, 상업시설 흔적의 시각 언어 기준.
- **판정**: 조건부 채택. 직접 자르거나 축소하는 생산 소스 시트로는 거절.

## 보드 계약

- 3셀 조용한 벽 master.
- 플레이어 대비 60~70% 높이의 이동 가능한 연료 셀.
- 독립 프롭이 아닌 벽 부착 호스 모듈.
- 더러운 아이보리 소비자 플라스틱의 결제·티켓 단말.
- 서로 분리된 낮은 안내판과 주차 범퍼.
- 한 군집으로 묶인 낮은 케이블·철판 잔해.

## 최종 교정 프롬프트 요약

```text
Preserve the isometric pixel-art prop board and correct only production semantics. Reduce the fuel cell
about 18%, remove permanent legs and attached hose, add a carry handle, bottom ring, asymmetric valve,
simple chevron and one tiny red light. Separate the hose reel as a flush wall module with a disconnected
quick coupler. Replace the upright sign with a nearly-flat fallen sign and keep the parking stop separate.
Make at least 60% of the wall broad quiet panels and concentrate detail in one maintenance spine.
Redesign the kiosk as a dead dirty-ivory arcade payment/ticket unit. Group cable debris and reduce noise.
Use hard pixel clusters, three values per material, no antialiasing, dithering, readable text or neon flood.
Update the silhouette row to match.
```

## 생산 시 재해석

- 연료 셀만 `prop-explosive-barrel` 128×128 독립 프롭으로 옮긴다.
- 호스는 서비스 벽 master에 포함한다.
- 단말은 벽 일체형으로 옮긴다. 독립형이면 실제 blocking/interactable 규칙이 먼저 필요하다.
- 안내판과 범퍼는 각자의 기존 128×64×4-view 바닥 슬롯으로 따로 그린다.
- 연속벽은 보드에서 크롭하지 않고 방향별 192×176 master에서 네이티브로 다시 그린다.
- 정식 파일은 hard alpha, 기준 팔레트, 피벗, Aseprite 원본과 deterministic export를 갖춰야 한다.

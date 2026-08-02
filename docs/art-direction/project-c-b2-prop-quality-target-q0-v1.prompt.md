# B2 프롭 품질 목표 q0 v1 — 생성 기록

- **생성 방식**: Codex 내장 ImageGen `precise-object-edit` 연속 교정.
- **산출물**: `project-c-b2-prop-quality-target-q0-v1.png` (1672×941).
- **기준**: 현행 B2 q0 실화면의 방 footprint, 플레이어 스케일, 중앙 직교 동선과 Field Deck HUD.
- **용도**: 프롭 질량·배치·재료 분배·국소 조명 방향판.
- **금지**: 런타임 크롭, HUD 복제, 생성 텍스트 사용, 지형·충돌·상호작용 규칙의 추론.

## 채택한 교정

1. 공용 벽은 넓은 무지 패널을 유지하고 설비 밀도를 한 서비스 스파인에 집중했다.
2. 연료 셀을 플레이어 키의 약 60~70%인 이동 가능한 단일 게임플레이 프롭으로 줄였다.
3. 호스·소켓은 벽에 고정하고 연료 셀과 분리했다.
4. 쓰러진 안내판과 범퍼를 분리하고 둘 다 통과 가능한 낮은 장식으로 눌렀다.
5. 아이보리 결제·티켓 단말을 바닥 점유 없는 벽 매립형으로 바꿨다.
6. 후면 장식에서 틸 통과 화살표를 제거하고 비활성 중성 패널로 만들었다.
7. 시안·앰버 광원은 설비 주변에만 약하게 남기고 마젠타는 작은 잔재로 제한했다.

## 최종 시맨틱 교정 프롬프트

```text
Use case: precise-object-edit. Preserve the exact canvas, camera, 6x5 room footprint, player,
central two-tile clear four-direction path, Field Deck HUD, floor geometry, all walls, restrained
lighting, palette, pixel cluster style, and every object position.

Remove the large cyan arrow from the rear wall and make that device a dead neutral maintenance panel.
Mount the dirty-ivory payment/ticket unit flush into the right wall; remove its floor base, feet and broad
contact shadow. Keep the movable fuel cell compact, clarify only its amber chevron and one tiny red warning
indicator, and do not attach a hose or legs. Flatten the fallen direction sign to 5–10 degrees above the
floor and keep the nearby parking stop separate.

Do not move the player, hose reel, cable coil, drain, scraps, marker, room boundaries, door, minimap,
health, log or controls. Add no props, text, arrows, neon, fog, bloom, gradients, painterly detail,
dither, antialiasing or new geometry. Keep crisp low-resolution hard pixel clusters.
```

## 승격 제한

- 이 이미지는 q0 구도만 승인한다.
- q1~q3는 ImageGen으로 다시 구성하지 않고 동일 물리 좌표와 방향형 원본에서 파생한다.
- 실제 실화면에서 세계 고정, 안내판 방향, 문 상태, HUD 가림을 통과해야 제작 기준이 완성된다.

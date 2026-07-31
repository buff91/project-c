# 적 액터 치비 레인 1라운드 소견 — 감시자(2026-07-31)

플랜 v2 배치 ② 적 액터 재생성의 첫 시도. **채택 없음**, 레인 자체는 도는 것을 확인했다.
레시피 3종은 리포에 남기고 프롬프트만 고쳐 다음 라운드를 돈다.

## 실행한 것

- 레시피 3종 신설(`actor-chibi-base-v1` 파생, 정체성 블록만 교체):
  - `actor-slinger-chibi-base-v1` — 익명 마스크·후드 + warning 렌즈 1곳 + 슬링/파우치(§1-b)
  - `actor-grave-warden-chibi-base-v1` — 보스 부피 + 센서 마스트 + 붉은 외눈 1개
  - `actor-arc-drone-chibi-base-v1` — 비인간형이라 openpose 없이 txt2img, 96×96 캔버스
- 감시자만 생성·판정: job `ART-20260731-020709-6c0615`(4방향 ×2후보).
  C02를 채택했다가 **아래 두 결손으로 되돌리고 기각**했다.

## 왜 되돌렸나 (다음 라운드에서 고칠 것)

1. **배경이 액자로 구워졌다.** "simple gray background"가 테두리 있는 **패널 플레이트**로
   해석돼, conform의 auto key-color가 지우지 못하고 96×128 스프라이트에 불투명 사각형이
   그대로 남았다(알파는 하드였지만 배경이 가시 픽셀에 포함).
   → 프롬프트에 `framed panel, border, background plate, picture frame`를 negative로
     넣고, positive는 `isolated character on flat empty background` 로 바꾼다.
     환경 레인에서 쓰는 `#ff00ff` 크로마 배경 지시를 액터 레인에도 넣는 편이 안전하다.
2. **신호색 계약 위반.** 채택 후보의 가슴 코어가 **크고 마젠타/핑크**였다 — 면적이
   "포인트 1곳"을 넘고(§1-a), 하필 플레이어 정체색(핑크 라이더)과 겹쳐 적아 구분을 해친다.
   → positive에서 코어를 빼거나 `one small dark red core`로 축소하고,
     negative에 `pink core, magenta glow, large glowing chest`를 넣는다.

## 판정 근거

- 2후보 ×4방향 보드: `docs/captures/grave-warden-chibi-review-v1.png`
- 게임 스케일 비교(기존 액터 3종 + 감시자 후보): `docs/captures/grave-warden-scale-compare-v1.png`
  — 부피/접지는 계약대로였다. 실패한 것은 배경과 색이지 비율이 아니다.

## 상태

`graveWarden`·`arcDrone` 카탈로그 슬롯은 **여전히 비어 있고**(절차 폴백 유지),
STATUS의 "절차 폴백 배율 결손" 경고 둘은 해소되지 않았다.

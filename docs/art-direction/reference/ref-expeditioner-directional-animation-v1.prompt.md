# 원정자 4방향 애니메이션 레퍼런스 v1

- 생성일: 2026-08-03
- 생성 도구: Codex ImageGen (`stylized-concept` 계열)
- 정체성 입력: `docs/art-direction/project-c-expeditioner-grounded-source-v1.png`
- 용도: **포즈·방향·실루엣 참고 전용**. 생성 이미지를 Unity에 직접 넣지 않는다.
- 최종 마감: `Tools/ArtPipeline/build_actor_knight_directional_v1.py`

## 입력 시트

| 파일 | 역할 | SHA-256 |
|---|---|---|
| `ref-expeditioner-directional-actions-v1.png` | 방향별 idle/단일 walk/attack release/hit 후보 | `889a3b9a68854db5bd65395141556bd179a0cf874fd55afbfa9d19a8a993e260` |
| `ref-expeditioner-directional-walk-v1.png` | north/east/south/west × contact A/pass/contact B | `5fd863b7f035c58bda7526478ddd2d6b6e8bfd9fcf5508c17156a65b6d7d8d67` |
| `ref-expeditioner-directional-attack-v1.png` | 방향별 windup/release/recovery 후보 | `0fa56123bc1b49f01f1a99c926f5e9150fc5e6b1a76521b306a978a417c05a9d` |
| `ref-expeditioner-directional-fall-death-v1.png` | 방향별 collapse/grounded knockout 후보 | `21577a73dd71c512ffe4d9a024c7f273528518d08c788254d07b475a69144064` |

## 생성 지시의 공통 계약

- 블론드+주황 팁 포니테일, 얼굴 노출, 패치된 오프화이트 메딕 재킷, 차콜 장비복,
  힙 의료 파우치, 숄더 사첼, 빈손을 모든 셀에서 유지한다.
- 2.5~3등신, 2:1 아이소메트릭 부감, 방향별 동일 해부와 크기, 심은 발의 동일 기준선을 요구한다.
- hard-edge 픽셀아트, chunky cluster, 웜 그레이/차콜/러스트/블론드 제한 팔레트를 요구한다.
- 무기·판타지 갑옷·네온 외곽선·바닥·그림자·UI·텍스트·추가 캐릭터를 금지한다.
- 행은 화면 방향 `north/east/south/west`다. walk와 attack 시트의 열은 각각
  `contact A/pass/contact B`, `windup/release/recovery`다.

## 채택과 보정

- canonical idle은 south에 승인 `actor-knight.png`, north/east에 broad action 시트의 방향별
  idle을 쓰고 west는 east를 정확히 픽셀 반전한다. idle의 호흡은 발을 고정한 세로 압축으로 만든다.
- walk는 전용 4×3 시트의 contact A/pass/contact B를 쓰며 west는 east 완성본을 픽셀 반전한다.
- attack은 canonical windup → broad action 시트의 방향별 release → canonical recovery다.
  release는 키를 58px 작업 높이에 고정하고, 너무 넓은 팔 뻗기만 가로 41px 안으로 접는다.
  attack 전용 시트는 north 오방향과 release 체형 축소가 있어 참고 이력으로만 보존한다.
- hit은 broad action 시트의 방향별 recoil → 압축 recovery → canonical idle이다. east/west는
  독립 생성하지 않고 완성된 east 프레임을 직접 반전한다.
- fall/death 생성 시트는 canonical과 해부가 달라 최종 프레임에 쓰지 않는다. 같은 방향 canonical을
  단계 회전하고, 후반 사체는 균일 축소 대신 가로를 접고 세로 부피를 보존한다. east의 fall/death를
  완성한 뒤 west를 직접 반전한다.
- 생성 배경은 가장자리 연결 neutral charcoal flood로 제거하고 가장 큰 연결 실루엣만 남긴다.
- 48×64 작업 격자에서 발 기준을 잠근 뒤 96×128로 nearest 2배 확대한다. 모든 프레임은
  하드 알파, 정확한 2×2 클러스터, 원정자용 24색 이하 팔레트만 허용한다.
- 기존 승인 정적 `actor-knight.png`는 Aseprite의 태그 밖 leading Frame 0으로 보존한다.
  Hub/카탈로그는 이 프레임을 쓰고, 던전 애니메이션은 별도 24방향 태그를 쓴다.

## 산출 계약

- 정식 태그: `idle/walk/attack/hit/fall/death × north/east/south/west` 24개.
- 프레임: 방향당 `idle 4 + walk 3 + attack 3 + hit 3 + fall 2 + death 5`, 태그 프레임 80개.
  태그 밖 승인 `Frame_0`까지 정식 Aseprite 총 81프레임이다.
- loop: idle/walk. once: attack/hit/fall/death.
- 검토 미리보기: `docs/captures/actor-knight-directional-conform-preview-v1.png`.
- PC 실화면: `docs/captures/actor-directional-{walk,attack,hit,fall}-runtime-pc-2026-08-03.png`.
  `fall` 캡처는 짧은 클립의 마지막 프레임이 월드 낙하 종료까지 유지되는 런타임 계약도 고정한다.

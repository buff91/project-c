# 메인 원정자 ComfyUI → Unity vertical slice

> 기준: 2026-07-28 `main` `fa6c90e`
>
> 범위: 단일 원정자 `actor-knight` 한 종의 생성 입력, 런타임 스프라이트,
> 6개 상태 태그, Unity 카탈로그 연결과 PC Game View 확인까지.
>
> **상태:** 아래는 초기 vertical slice의 역사 기록이다. 현재 런타임 기준은
> `project-c-expeditioner-grounded-source-v1.{png,prompt.md}`에서 마감한 `96×128` 단일
> `Frame_0`이며, 방향별 6태그를 수작업 승인하기 전까지 `SurvivorAnimationApproved=false`다.

## 화면에서 출발한 판단

비교 기준은 아래 셋이다.

- 게임 전: [`docs/captures/player-vertical-slice-before.png`](../captures/player-vertical-slice-before.png)
- 목표 화면: [`project-c-integrated-postapoc-gameplay-target-v2.png`](project-c-integrated-postapoc-gameplay-target-v2.png)
- 현재 액터 원본 시트:
  [`project-c-collapsed-transit-actors-source-v2.png`](project-c-collapsed-transit-actors-source-v2.png)

기존 `actor-knight.png`는 게임 화면에서 머리와 발 크기는 검증됐지만 한 손 봉과 큰 방패가
몸에 영구적으로 구워져 있다. 이는 "단일 원정자, 정체성은 장비가 진다"는 현재 규칙과 충돌한다.
새 실루엣은 후드·호흡기·낮은 무게중심·소형 의료팩만 캐릭터에 남기고 양손은 비운다.

## 재설계한 파이프라인

1. **정체성과 방법을 분리한다.** `subjects/actor-knight.yaml`은 후드·호흡기·의료팩·
   빈손 같은 캐릭터 검수 기준만 소유한다. `character-runtime-base-v2`와
   `character-action-keyframes-v6`는 모든 인간형이 공유할 캔버스·해부·피벗 검사만 소유한다.
   합성기는 두 `quality_gates`를 중복 없이 병합한다.
2. **현재 Game View를 production anchor로 쓴다.** 기존 96×128 런타임 PNG를 512×512
   정수배 guide로 만들고, 구 직업 장비 영역만 `--clear-box`로 magenta 처리한다. 원본 PNG는
   수정하지 않는다.
3. **기본 자세를 먼저 승인한다.** SD1.5 img2img + OpenPose, 512 생성, 96×128 conform,
   PPU 128, pivot `(0.5, 0.04)`, 마스터 GPL 팔레트가 `character-runtime-base-v2`의 계약이다.
   승인 스냅샷만 다음 단계의 identity 입력이 된다.
4. **동작은 표준 BODY_18로 제어한다.** 임의 색 스틱 이미지는 OpenPose가 아니다.
   가이드 생성기는 ControlNet 학습 규약과 같은 18개 관절 순서·좌우 색·17개 limb 연결을 쓴다.
   `character-action-keyframes-v6`는 동일 seed로 idle 2, walk 3, attack 3, hit/fall/death
   각 1개, 총 11개 키포즈를 만든다.
5. **AI 출력은 Aseprite에서 끝낸다.** 샷별 chroma 제거·팔레트 잠금 후 Lua가 동일
   96×128 캔버스에 `idle/walk/attack/hit/fall/death` 태그를 조립한다. Unity는 정식
   `actor-knight.aseprite`를 임포트해 첫 프레임과 태그별 sprite curve를 카탈로그에 굽는다.
6. **색 역할을 면적으로 검증한다.** conform 결과의 불투명 픽셀 중 teal은 4%,
   warning은 2%를 넘지 못한다. 공용 팔레트에 신호색이 있다는 이유로 의상 전체가 그 색에
   잠기는 문제를 생성 파이프라인에서 차단한다.

## 실생성 기록

| 작업 | 설정 | 판정 |
|---|---|---|
| `ART-20260728-081508-5806cb` | 기존 전체 장비 guide, base denoise 0.38 | 봉·방패가 그대로 남아 거절 |
| `ART-20260728-082345-fb06da` | SDXL 무장 없는 콘셉트 | 밝은 현대 구조대원으로 세계관 이탈, 중단 |
| `ART-20260728-083002-044ab1` C03 | 장비 영역 제거 guide, base denoise 0.58, seed 28072202 | 96×128에서 후드·호흡기·부츠가 읽혀 승인 |
| `ART-20260728-083528-65266e` | 구 스틱 가이드, action denoise 0.50 | identity는 보존했지만 모든 상태가 idle에 고정 |
| `ART-20260728-084228-8a4f4e` | 구 스틱 가이드, action denoise 0.72 | 자세보다 의상·소품이 먼저 드리프트해 거절 |
| `ART-20260728-084621-4ab8a4` | 구 스틱 가이드, action denoise 0.62 | 얼굴·복장이 바뀌어 거절 |
| `ART-20260728-090813-640d93` | 구 스틱 가이드, action denoise 0.56 | 비표준 guide 원인을 확정하고 중단 |
| `ART-20260728-091221-a13608` | 표준 BODY_18, action denoise 0.50, seed 28072700 | 좌우 관절은 읽었으나 동작 폭이 부족해 중단 |
| `ART-20260728-091550-2b0850` C01 | 표준 BODY_18, action denoise 0.56, seed 28072800 | 11포즈의 identity와 상태 실루엣이 96×128에서 유지돼 최종 승인 |

수치를 올려 포즈를 억지로 만드는 방식은 identity를 먼저 무너뜨렸다. 이 vertical slice의
핵심 결론은 **denoise 튜닝보다 ControlNet 입력 규약을 먼저 검증해야 한다**는 것이다.

## Unity 런타임 계약

- 정식 원본: `Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite`
- 캔버스/피벗: 96×128, PPU 128, Canvas Pivot `(0.5, 0.04)`
- 태그:
  - loop: `idle` 5 FPS, `walk` 10 FPS
  - once: `attack` 10 FPS, `hit` 12 FPS, `fall` 8 FPS, `death` 8 FPS
- 카탈로그: 기존 `knight` 슬롯을 단일 원정자 슬롯으로 유지한다.
- 검증 타깃: PC 가로 Game View만 사용한다.

## 실제 반영 결과

- 정식 원본은 13프레임이며 태그 범위는 `idle 0..2`, `walk 3..6`,
  `attack 7..9`, `hit 10`, `fall 11`, `death 12`다.
- Lua가 프레임 생성 도중 태그를 붙이면 Aseprite가 “현재 마지막 프레임” 태그를 뒤 프레임까지
  자동 확장하는 결함도 이 slice에서 발견했다. 전체 타임라인을 만든 뒤 태그를 붙이도록 고쳐,
  Unity 베이크의 태그별 distinct frame 수를 EditMode 계약으로 고정했다.
- `Project-C > Art > Aseprite > Validate Sources` 통과 후
  `ProjectCEnvironmentCatalog.knight`와 `actorAnimations[knight]`가 새 원본을 참조한다.
- 첫 PC Game View 결과는 플레이어 teal 면적과 장면 전체의 검은 비율이 커서 최종 승인하지 않았다.
  액터 팔레트를 차콜·웜 그레이·가죽 갈색 중심으로 재매핑한 뒤 teal 면적은 불투명 픽셀의
  약 0.83%로 줄었다.
- 장면 어둠의 직접 원인은 안개 배경의 `sortingOrder = -100000`이었다. SpriteRenderer에서
  실제 값이 `31072`로 되감겨 최하층 바닥 앞에 겹쳤다. `Dungeon Backdrop` Sorting Layer를
  `Default` 뒤에 추가하고, 콘크리트 램프와 휴대광 색조를 레퍼런스에 맞게 낮췄다.
- 최종 PC Game View:
  [`docs/captures/player-tone-correction-final.png`](../captures/player-tone-correction-final.png)
- 정렬 결함 진단 캡처:
  [`docs/captures/player-tone-correction-fog-order-diagnostic.png`](../captures/player-tone-correction-fog-order-diagnostic.png)

2026-07-28 검증 기록(`fa6c90e` 기반 작업 트리):

- ArtPipeline Python: 102 passed
- Core shim: 904 passed
- Unity EditMode: 1056 passed
- Unity PlayMode: 2 passed

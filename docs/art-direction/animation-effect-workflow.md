# 픽셀아트 애니메이션/이펙트 워크플로 (Project-C)

ComfyUI는 **키프레임 생성**에만 쓰고, 최종 애니메이션/이펙트는 Aseprite에서 마감한다. 핵심 규칙은 4개다.

1. ComfyUI는 포즈/구도 후보를 여러 장 뽑는다.
2. Aseprite에서 프레임 정렬, 좌우/상하 반전 규칙, 피벗과 캔버스를 맞춘다.
3. FrameTag를 붙여 루프를 결정한다.
4. Unity 반영은 태그 이름 기반 규칙(`idle`, `walk`, `attack`, `hit`, `fall`, `death`, `burst`, `idle-loop`)으로 수행한다.

## 1) 캐릭터 애니메이션 (권장)

- 현재 런타임은 방향별 클립이 아니라 `idle/walk/attack/hit/fall/death` 상태 태그를 사용한다.
- 기존 게임에 이미 정적 스프라이트가 있는 액터는 그 96×128 컷을 512 정수배로 확대한
  identity guide에서 시작한다. `character-runtime-base-v2`가 저 denoise(0.38)로 기본 자세를
  만들고, 승인본만 `character-action-keyframes-v6`의 img2img 입력이 된다. 이 경로는 현재
  Game View에서 검증된 머리·어깨·발 비율을 보존하기 위한 production bootstrap이다.
- 액터는 한 방향 기준으로 `idle`, walk contact/pass/contact, attack windup/release/recovery,
  `hit`, `fall`, `death` 키포즈만 ComfyUI에서 생성한다.
- 방향별 애니메이션은 런타임 계약이 확장될 때 별도 레시피 버전으로 추가한다.
- 원정자 vertical slice에서 처음 쓴 임의 색 스틱 가이드는 denoise 0.50에서 포즈를 거의
  바꾸지 못했고, 0.62부터는 포즈보다 의상·소품이 먼저 흔들렸다. 원인은 수치보다 ControlNet이
  입력을 BODY_18로 읽지 못한 데 있었다. 생성기는 표준 OpenPose 관절 순서·색을 사용하고,
  v6 기본값은 identity를 유지하면서 동작 폭을 확보한 0.56으로 둔다. 그래도 생성 컷은 pose/실루엣
  참고이며 검수 없이 정식 `.aseprite`로 자동 승격하지 않는다.
- 포즈 세트는 같은 base seed를 공유한다. shot별 seed 변경은 캐릭터 정체성 드리프트를 키운다.
- 공용 방법은 성별·직업·고정 무기를 말하지 않는다. 마스크·배낭·신호점처럼 캐릭터를
  구분하는 검수 항목은 `subjects/<actor>.yaml`의 `quality_gates`가 소유하고, 합성 시 공용
  방법의 해부/피벗 검사와 병합된다.
- Aseprite에서 프레임은 `동일 캔버스(96×128)` `고정 피벗`을 유지해야 한다.
- 추천 FPS
  - `idle` 4~6 FPS, `walk` 8~12 FPS, `attack` 8~12 FPS
- 추천 Tag
  - `idle` : 4~6프레임 루프
  - `walk` : 8프레임 루프
  - `attack` : 6프레임 once(또는 끝 프레임에서 hold)
  - `hit` : 3~4프레임 once
  - `death` : 8프레임 once

### ComfyUI에서 뽑아야 할 최소 키프레임

1. `idle`, `idle-breathe` 2컷
2. `walk-contact-a`, `walk-pass`, `walk-contact-b` 3컷
3. `attack-windup`, `attack-release`, `attack-recovery` 3컷
4. `hit` 1컷
5. `fall` 1컷
6. `death` 1컷

이렇게 모이면 Aseprite에서 총 8~18프레임으로 충분히 자연스럽다.

## 2) 이펙트 애니메이션 (권장)

이펙트는 절대 AI 한 장면을 프레임 단위로 무작위 생성하지 않는다.

- ComfyUI는 **중심 이펙트 원형 소스**(예: 임팩트 코어, 화염 링, 빙결 조각)을 1~4컷만 생성.
- SDXL 768 실생성은 큰 아이콘·완전한 링·배경 그림자를 만들기 쉬웠다. 24~32px 최종형으로
  자동 승격하지 않고 파편 방향과 색 참고로만 사용한 뒤 Aseprite에서 다시 그린다.
- Aseprite에서 다음 규칙으로 확대한다.
  - `burst` 계열: 4~6프레임, 커브형 알파감쇠
  - `idle-loop` 계열: 6~8프레임, 루프
  - 24×24 또는 32×32 캔버스에서 0.5px 단위 서브픽셀 이동 제한(픽셀 흔들림 방지)
  - 오브젝트 중심 오프셋(`pivot`)은 0.5,0.5 유지

기본 슬롯 제안은 `fx-impact-physical`, `fx-impact-fire`, `fx-impact-frost`,
`fx-impact-heavy`, `fx-status-burn`, `fx-status-freeze`이다.

## 3) 배경·소품 애니메이션

환경 애니메이션은 큰 배경 전체를 흔들지 않고 **독립 SpriteRenderer가 있는 국소 신호 자산**만
대상으로 한다. 현재 자동 연결 대상은 아래 4종이다.

- `prop-campfire`
- `prop-portal`
- `env-wall-torch-rising-right`
- `env-wall-torch-rising-left`

`environment-loop-concept-sdxl-v1`로 형태와 재료를 고른 뒤 승인 후보를
`environment-idle-keyframes-v1`에 넘긴다. 이 방법은 같은 seed와 identity 입력으로
`pulse-low/rise/high/fall` 네 키프레임을 만들고, Aseprite 초안에서는 `idle` 태그 6 FPS
루프로 조립한다. 불꽃·포탈 중심과 벽면 부착점은 모든 프레임에서 고정한다.

정식 `<slot>.aseprite`에 `idle` FrameTag가 있으면 `ProjectCAsepritePipeline`이 sprite
커브만 `EnvironmentAnimationSet`으로 굽는다. 런타임은 campfire·portal·좌우 벽 횃불의
SpriteRenderer에 `SpriteClipAnimator`를 붙여 자동 재생한다. 태그가 없으면 기존 첫 프레임
정적 스프라이트가 그대로 폴백이므로, 환경 애니메이션 도입이 기존 씬을 깨지 않는다.

## 4) ComfyUI → Aseprite 자동 인계

`art_runner.py`는 멀티샷 레시피를 실제로 해석한다.

- 액터 1후보 = OpenPose로 잠근 상태 기반 키포즈 11샷
- 환경 루프 1후보 = 동일 identity를 공유하는 `idle` 키프레임 4샷
- 이펙트 1후보 = physical/fire/frost/heavy/burn/freeze 6샷
- Slack에는 라벨 리뷰 시트 한 장을 올리고 원본은 샷별로 보존한다.
- `Aseprite 소스 세트`를 누르면 샷별 캔버스·알파·팔레트를 강제한
  `.aseprite` 파일과 `aseprite-handoff.json`을 만든다.
- `애니 초안`을 누르면 `aseprite_build_animation.lua`가 액터 상태 태그, 환경 `idle`,
  이펙트 `burst`/`idle-loop` 태그를 조립하고 1×/8× GIF를 만든다.
- Slack의 `빠르게/기본 속도/느리게` 버튼은 같은 프레임을 보존하고 duration만 다시 조립한다.

자동 산출물은 검수 초안이다. 발 기준선, 실루엣, 실제 보간 프레임과 최종 재생 타이밍은
게임플레이 판독과 연결되므로 Aseprite에서 사람이 확정한다.

Slack 버튼 없이도 같은 단계를 직접 실행할 수 있다.

```bash
python3 Tools/ArtPipeline/art_runner.py prepare <candidate-id>
python3 Tools/ArtPipeline/art_runner.py animation <candidate-id> \
  --timing-scale 1.0
python3 Tools/ArtPipeline/art_runner.py approve <candidate-id>
python3 Tools/ArtPipeline/art_runner.py apply-request <candidate-id>
```

Slack 명령으로는 각각 `/art prepare <candidate-id>`,
`/art animation <candidate-id> 1.0`, `/art approve <candidate-id>`,
`/art apply <candidate-id> confirm`이다. 승인은 스냅샷을 보관할 뿐 Unity 에셋을 바꾸지 않으며,
Spark 반영 요청이 실제 참조를 조사해 대상을 하나로 확정한 뒤에만 게임 파일을 갱신한다.
샷별 승인·거절·변형을 포함한 전체 대응표는
[`ART_REVIEW_AUTOMATION.md`](ART_REVIEW_AUTOMATION.md)의 「트리거·사용 가이드」가 소유한다.

## 5) Aseprite 진행 방식

### 자동 초안 이후 수동 마감

1. 후보 스레드에서 `Aseprite 소스 세트` → `애니 초안`을 순서대로 누른다.
2. `animation/*.aseprite`를 열고 foot baseline과 프레임 사이 실루엣을 맞춘다.
3. 필요한 인비트윈을 추가하고 기존 Frame Tag 범위를 갱신한다.
4. GIF와 실제 게임 속도를 비교해 duration을 최종 조절한다.
5. `project-c-torchstone.gpl` 팔레트와 고정 캔버스를 유지한 채 정식 원본으로 승격한다.

## 6) 레시피 연동

다음 템플릿을 사용한다.

- 메인 원정자:
  `character-runtime-base-v2 → character-action-keyframes-v6`
- 기존 투석 약탈자 샘플:
  `docs/art-direction/comfyui/recipes/actor-slinger-animation-v5.yaml`
- 이펙트: `docs/art-direction/comfyui/recipes/fx-impact-suite-v2.yaml`
- 환경 루프: `docs/art-direction/comfyui/methods/environment-idle-keyframes-v1.yaml`

레이블별로 사용 목적이 달라야 한다.

- `animation_scope: idle-loop`면 환경의 `idle` 승인만 Unity 슬롯 업로드 허용.
- `animation_scope: runtime-state-keyframes`면 Slack에서 `[walk]`, `[attack]`처럼
  태그를 붙여 보완 요청을 기록한다.

## 7) Slack 피드백 규칙(애니/이펙트)

채널에서 사용할 피드백 규칙을 아래로 둔다.

- `:thumbsup:`: 방향/루프가 읽힌다(approve)
- `:thumbsdown:`: 불량(Reject)
- `:arrows_counterclockwise:`: 프레임 보완(Variation)
- `:art:`: 팔레트 변경
- `:bone:`: 실루엣/비율 조정
- `:soap:`: 잡티 정리
- `:triangular_ruler:`: 피벗/크기/스케일

Thread 답글은 아래 형식으로 쓰면 자동 분류가 쉬워진다.

- `[walk] contact B에서 발 기준선이 2px 뜬다`
- `[attack] release를 1프레임 더 유지`
- `[fx-impact-fire] burst가 너무 원형이니 오른쪽 위 파편을 늘려줘`

## 8) 산출물 저장 가이드

- 검토 출력(채택 전): `docs/art-direction/comfyui/output/`로 고정.
- 샷 원본: `output/review/<job>/Cxx/shots/<shot-id>/raw.png`.
- Aseprite 인계: 같은 후보 폴더의 `aseprite-handoff.json`과 `aseprite/*.aseprite`.
- 애니 초안: 같은 후보 폴더의 `animation/*.aseprite`,
  `animation/previews/**/*.gif`, `animation-draft.json`.
- 승인 전용만 `Assets/_Project/Art/Source/Aseprite/`로 이동.
- 임시 출력은 `.gitignore` 대상.

# Project-C ComfyUI 워크플로

이 폴더의 `*.workflow.json`은 사람이 보는 캔버스 원본, `*.api.json`은 REST 실행본이다.
기존 6-셀 환경 시트를 **실루엣·2:1 투영을 보존한 채 재료만** 바꾸는 통제형 생성과
액터·아이템 생성 그래프를 함께 관리한다. 개념·근거는
`../comfyui-to-aseprite-pipeline.md`를 따른다.

> **성격**: 스톡 ComfyUI에서 바로 로드되도록 **코어 노드만**으로 짠 최소 그래프
> (img2img + ControlNet + LoRA 슬롯). IPAdapter와 LineArt 전처리기는 커스텀 노드라
> 코어에서 빼고 아래 "권장 추가"로 안내한다. 노드가 빨갛게 뜨면 설치 버전 차이 —
> 우클릭 → Add Node로 대체한다.
>
> **자동화 결정(2026-07-26)**: MCP 대신 **ComfyUI Desktop의 로컬 REST API**를 사용한다.
> 실행은 `Tools/ArtPipeline/comfy_batch.py`, 결정론적 마감은 Aseprite CLI/Lua
> (`aseprite_conform.sh` → `aseprite_conform.lua`)가 담당한다.
>
> **실제 운영 진입점**: 생성·수동 배치·큐·승인 보관·Aseprite 준비·Spark 게임 반영을
> Slack과 로컬 CLI에서 실행하는 전체 명령표는
> [`../ART_REVIEW_AUTOMATION.md`](../ART_REVIEW_AUTOMATION.md)의
> 「트리거·사용 가이드」를 따른다.

## 1. 준비물 (ComfyUI Manager로 설치)
- **SDXL 체크포인트** (`models/checkpoints`)
- **픽셀아트 LoRA(SDXL)** (`models/loras`)
- **ControlNet(SDXL) LineArt 또는 Canny** (`models/controlnet`)
- (권장) 커스텀 노드: **ComfyUI ControlNet Aux**(전처리기), **ComfyUI_IPAdapter_plus**

## 2. 로드 & 채우기
1. `.json`을 ComfyUI 캔버스에 **드래그**(또는 Load).
2. 플레이스홀더 위젯 3개를 실제 파일명으로 교체:
   - 노드 1 `PUT_SDXL_CHECKPOINT.safetensors`
   - 노드 2 `PUT_PIXELART_LORA.safetensors`
   - 노드 7 `PUT_CONTROLNET_SDXL_lineart.safetensors`
3. 노드 5 `LoadImage`에 **소스 시트**를 올린다(기존 6-셀 배치·`#ff00ff` 평면 배경 유지).

## 3. 권장 추가 (품질↑)
- **LineArt 전처리기**: `LoadImage(5)` → **AIO Aux Preprocessor(LineArt)** → 노드 8의 `image`.
  (지금은 원본을 직접 힌트로 넣음 — 동작하지만 전처리하면 실루엣 고정이 더 깨끗.)
- **IPAdapter**(스타일 앵커): `IPAdapter Unified Loader` + `IPAdapter Advanced`를 모델 경로에 끼우고
  레퍼런스(`../project-c-postapoc-ref-01/02/05`, `...target-v2`)를 물린다. weight ~0.6.

## 4. 파라미터 (노드 9 KSampler)
- **denoise 0.55**(0.45~0.65 — 낮을수록 원본 실루엣·배치 보존)
- ControlNet strength 0.8(노드 8) · seed 고정 · steps 28 · cfg 6.5 · dpmpp_2m/karras
- 각도 흔들리면 denoise↓ / ControlNet strength↑.

## 5. 마감으로 넘기기 (팔레트 잠금은 이미 배선됨)
1. 출력(노드 11 `SaveImage`, `ComfyUI/output`)을 **정확히 1536×1024**로 다듬어
   `docs/art-direction/project-c-collapsed-transit-environment-source-v2.png`에 저장.
2. `python3 Tools/ArtPipeline/process_postapoc_environment_v2.py` 실행 →
   셀 추출·리사이즈·**공용 `.gpl` 고정 양자화**·미러까지 자동. `Art/Environment/env-*.png` 갱신.
3. Unity 복귀 → `Validate Sources` → Play 캡처(PC 가로) → EditMode/PlayMode 회귀.

## 6. 하지 말 것
- **액터 애니메이션을 여기서 만들지 말 것.** idle 베이스 포즈 참고까지만 뽑고
  프레임은 Aseprite 손작업(발 고정·팔레트·실루엣 일관). 근거: 파이프라인 문서 §1·§4.
- 에셋 내부에 `#ff00ff` 사용 금지(후처리 크로마키가 지움).

## 7. REST 자동 실행

### 7-a. Desktop과 모델

ComfyUI Desktop을 실행하고 로컬 HTTP 서버를 `127.0.0.1:8188`에 둔다. 이 머신의 Desktop
모델 루트는 `~/Documents/ComfyUI/models`다.

- 체크포인트 → `models/checkpoints`
- LoRA → `models/loras`
- ControlNet → `models/controlnet`
- IPAdapter → `models/ipadapter`
- CLIP Vision → `models/clip_vision`

Civitai 모델을 사용해도 되지만 **base model을 섞지 않는다**. SD1.5 체크포인트에는 SD1.5
LoRA/ControlNet/IPAdapter, SDXL에는 SDXL용을 맞춘다. 채택 모델은 이름·버전·원문 URL·라이선스
조건을 생성 시트의 prompt 문서에 함께 기록한다.

연결과 실제 로더 선택지를 먼저 확인한다:

```bash
python3 Tools/ArtPipeline/comfy_batch.py status
python3 Tools/ArtPipeline/comfy_batch.py models
```

### 7-a-1. 발주 비용 (18GB 통합 메모리 기준)

이 머신은 M3 Pro / 18GB **통합** 메모리다. CPU와 GPU가 같은 풀을 쓰므로 "CPU가 논다"는
관측은 발주에 쓸 자원이 아니다 — `--cpu` 는 확산을 10~20배 느리게 만들면서 메모리는
그대로 먹는다.

발주가 느릴 때 **먼저 의심할 것은 체크포인트 재로드이지 메모리가 아니다.** 스왑이 가득
차 있으면 원인처럼 보이지만, 아래 실측에서 메모리를 비워도 장당 시간은 그대로였다.
추측하지 말고 `/history` 로 재본다 — 아래 표를 낸 방법이 그것이다.

2026-07-31 로컬 실측(SDXL `zavychromaxl_v100`, item-static 레인):

| 조건 | 장당 |
|---|---|
| 1024×1024 / 28스텝, 직전 잡과 같은 체크포인트 | 221초 (n=37) |
| 1024×1024 / 28스텝, 직전 잡과 **다른** 체크포인트 | 381초 (**+160초**, n=21) |
| 768×768 / 20스텝 | 99.7초 (**2.1배**) |
| 1024×1024 / 28스텝, 메모리 확보 후(스왑 6.3GB·여유 64%) | 243초 — **개선 없음** |
| 1024×1024 / 28스텝, `--lowvram` (같은 시점 짝 비교) | 282초 vs normal 233초 — **더 느림** |

**측정할 때 주의**: 같은 레시피의 장당 시간이 실행마다 229~358초로 흔들린다(배경 부하).
한두 장으로 비교하면 3배 차이도 만들어낼 수 있다 — 실제로 이 표를 만들며 한 번 그렇게
속았다. 반드시 **같은 시점에 짝지어** 재고, 첫 잡(체크포인트 콜드 로드)은 버린다.

- **체크포인트 재로드가 가장 비싸다.** 배치 드라이버는 서로를 모른 채 같은 큐에
  발주하므로, 체크포인트가 다른 두 배치가 교대하면 매 장마다 6.9GB를 다시 읽는다.
  `comfy_batch.execute_prompt` 가 `CheckpointLease` 로 큐를 체크포인트 단위(기본 4잡)
  로 점유해 이걸 막는다 — 드라이버는 따로 할 일이 없다. `COMFY_LEASE_CHUNK=0` 으로 끈다.
- **산출 해상도에 맞춰 발주한다.** 아이템은 64px 캔버스로 끝난다(`process_items_v3.py`).
  1024로 뽑아 64로 줄이면 선형 16배를 버린다 — `item-static-v2` 는 768/20으로 내려
  룩을 유지한 채 2.1배 빠르다. 승인된 자산과 룩이 갈리지 않도록 v1 을 제자리에서
  고치지 말고 **새 method 를 만든다.**
- **메모리 확보는 발주를 빠르게 하지 않는다 — 여기에 시간을 쓰지 마라.** 배치 중 스왑이
  18GB까지 차고 wired 가 11GB(ComfyUI 의 MPS 할당)로 잡히는 건 사실이고, Comfy Desktop
  (Electron 셸)과 Unity 에디터를 내리면 스왑 6.3GB·여유 64%까지 풀린다. 그런데 **같은
  레시피의 장당 시간은 그대로였다**(221초 → 243초, n=4). 스왑에 밀려 있던 건 대부분 다른
  앱의 콜드 페이지였고 MPS 워킹셋은 압박 속에서도 유지됐다. 머신은 쾌적해지지만 발주
  처리량은 그대로다 — 다른 작업을 위해 RAM 이 필요할 때만 정리한다.
  백엔드만 띄우는 스크립트: `./Tools/ArtPipeline/run_comfy_headless.sh`
- **`--lowvram` 도 쓰지 않는다.** 같은 시점 짝 비교에서 normal 233초 / lowvram 282초로
  오히려 느렸다. 메모리가 병목이 아니므로 모델을 쪼개 얻을 게 없고 전송 비용만 붙는다.
  스크립트에 `COMFY_LOWVRAM=1` 손잡이는 남겨 뒀지만 기본값은 끈 상태다.
- 후처리(`process_*.py`)는 장당 약 2초로 전체의 1% 다 — 여기를 병렬화해도 얻을 게 없다.

### 7-b. 캔버스/API 워크플로 쌍

Project-C의 모든 실행 그래프는 같은 basename의 두 파일을 함께 보존한다.

- `NAME.workflow.json`: ComfyUI 캔버스에서 여는 편집 SSOT. 노드 위치·그룹·위젯을 보존한다.
- `NAME.api.json`: 캔버스에서 **Save/Export (API Format)**으로 만든 실행 산출물.

캔버스 JSON을 `/prompt`에 그대로 보내지 않는다. 반대로 API JSON만 고치면 실제 실행과
캔버스가 달라지므로, 캔버스에서 수정하고 API 형식을 다시 Export한다. 실행 전 계약 검사는:

```bash
python3 Tools/ArtPipeline/comfy_batch.py validate \
  docs/art-direction/comfyui/environment-styletransfer.api.json
```

API 워크플로 실행 예:

```bash
python3 Tools/ArtPipeline/comfy_batch.py run \
  docs/art-direction/comfyui/environment-styletransfer.api.json \
  --upload 7.image=docs/art-direction/project-c-collapsed-transit-environment-source-v2.png \
  --set 9.seed=42 \
  --set 9.denoise=0.55 \
  --output-dir docs/art-direction/comfyui/output
```

- ComfyUI 왼쪽 **Workflows → Project-C** 목록에 캔버스 5개를 게시하려면
  `python3 Tools/ArtPipeline/comfy_batch.py sync-workflows`를 실행한다.
- `--set NODE.INPUT=VALUE`: API 그래프의 입력을 덮어쓴다. 숫자·bool·배열은 JSON으로 해석한다.
- `--upload NODE.INPUT=PATH`: `/upload/image`에 올리고 반환 파일명을 해당 입력에 넣는다.
- 기본값은 ComfyUI 캔버스 브리지를 먼저 찾고, 없으면 WebSocket으로
  `executing`/`progress`/프리뷰를 받은 뒤 `/history`와 `/view`로 결과를 내려받는다.
- 실행에 사용한 최종 API 그래프, 캔버스, 이벤트, 진행 상태는
  `OUTPUT_DIR/_runs/<prompt-id>/`에 보존한다.
- 캔버스 전체를 `extra_pnginfo.workflow`로 함께 보내므로 생성 PNG를 ComfyUI에 드롭하면
  실행 그래프를 다시 열 수 있다.
- `output/`은 검토용이며 gitignore 대상이다. 채택한 결과만 정식 `*-source-v3.png` 이름으로
  `docs/art-direction/`에 옮긴다.

### 7-c. ComfyUI에서 실제 노드 진행 보기

한 번만 브리지를 설치하고 ComfyUI Desktop을 재시작한다. 이미 장시간 실행 중인
`art_runner.py work` 서비스가 있으면 현재 작업이 끝난 뒤 그 워커도 재시작해 새 클라이언트를
로드한다.

```bash
Tools/ArtPipeline/install_comfy_live_bridge.sh
```

설치기는 실행 중인 ComfyUI의 실제 `custom_nodes` 경로를 자동으로 찾고, 캔버스 파일도
**Workflows → Project-C**에 함께 게시한다. 별도 설치 위치를 쓸 때만
`COMFYUI_CUSTOM_NODES_DIR=/path/to/custom_nodes`를 지정한다.

이후 ComfyUI 창이 열려 있으면 REST 작업을 제출하기 전에 대응하는
`*.workflow.json`을 자동으로 캔버스에 로드한다. 작업은 그 프런트엔드의 `client_id`로
제출되므로 ComfyUI 자체의 실행 노드 강조, KSampler 진행률, 프리뷰가 그대로 표시된다.
동시에 프런트엔드 이벤트가 워커로 돌아와 `_runs/<prompt-id>/events.ndjson`에도 기록된다.

브리지가 설치되지 않았거나 ComfyUI 창이 닫혀 있으면 배치는 멈추지 않고 독립 WebSocket
모니터로 전환한다. `websocket-client`는 아트 리뷰 requirements에 포함되어 있다.
의도적으로 캔버스 자동 로드를 끄려면 `comfy_batch.py run --no-frontend ...`를 사용한다.

## 8. Aseprite CLI/Lua 정적 마감

정확한 슬롯 크기로 추출된 PNG를 `.aseprite` SSOT로 승격한다:

```bash
Tools/ArtPipeline/aseprite_conform.sh \
  /path/to/actor-slinger.png \
  Assets/_Project/Art/Source/Aseprite/actor-slinger.aseprite \
  96 128 strict
```

이 명령은 다음을 강제한다.

- 캔버스 크기 검사(`strict`). 의도적인 최근접 크기 변경만 마지막 인자를 `nearest`로 지정한다.
- 알파 80 미만 완전 투명화, 나머지는 완전 불투명화
- `project-c-torchstone.gpl` 최근접색으로 무디더 매핑
- 단일 레이어 이름 `base`, 정식 `.aseprite` 저장

파일은 RGBA를 유지하면서 편집 팔레트를 Torchstone으로 고정한다. 팔레트 인덱스 0이 불투명
`pc-void`이므로 Indexed 투명 인덱스로 재사용하면 실제 void 픽셀이 사라지기 때문이다.
애니 액터는 이 명령으로 **idle 베이스만** 만든 뒤 Aseprite에서 공식 태그와 발 고정을 손작업한다.

## 9. 생성부터 슬롯 반영까지 한 명령

`art_asset.py`가 REST 실행과 Aseprite conform을 묶는다.

이미 생성한 PNG를 정식 슬롯에 반영:

```bash
python3 Tools/ArtPipeline/art_asset.py publish /path/to/slinger.png \
  --slot actor-slinger --width 96 --height 128 \
  --fit contain --anchor bottom --key-color ff00ff
```

ComfyUI API 워크플로부터 실행:

```bash
python3 Tools/ArtPipeline/art_asset.py generate \
  docs/art-direction/comfyui/actor-idle.api.json \
  --slot actor-slinger --width 96 --height 128 \
  --set 9.seed=42001 \
  --set 12.lora_name=project-c-pixelart-redmond-sdxl-v1-lite64.safetensors \
  --output-index 0 --fit contain --anchor bottom --key-color ff00ff
```

흐름은 `ComfyUI REST → raw PNG(output/, gitignore) → trim/contain → 96×128 prepared PNG →
Torchstone conform → Art/Source/Aseprite/actor-slinger.aseprite`다. 정식 `.aseprite`가 이미
있으면 실패하며, 검토 후 교체할 때만 `--force`를 쓴다.

환경 타일처럼 정확한 2:1 외곽이 중요한 입력은 먼저 카테고리 프로세서로 셀을 추출한 뒤
`publish --fit strict`를 사용한다. `contain`은 액터·소품·아이템 단일 컷아웃용이다.

`art_asset.py`는 정적 단일 컷용이다. 애니메이션/이펙트는 아래의
`art_runner.py` 멀티샷 레시피가 키포즈 세트를 만들고, 중간 프레임·타이밍은
Aseprite에서 발 기준선과 피벗을 고정해 마감한다.

## 10. Project-C용 Civitai SDXL LoRA

설치·SHA-256·트리거·생성물 이용 조건 스냅샷은
`civitai-model-manifest.json`이 기록한다. 현재 조합:

- PixelArtRedmond: 0.35~0.55 — 도트/하드 엣지
- IsoPixel_SDXL: 0.25~0.45 — 아이소 구도. **크레딧 필요**
- Envy Junkworld XL: 0.20~0.40 — 녹·고철·폐허 재료

세 LoRA를 모두 최대치로 겹치지 않는다. 환경은
`IsoPixel 0.3 + Junkworld 0.25 + PixelArt 0.4`, 액터는
`PixelArt 0.45`부터 시작한다. 최종 색은 LoRA가 아니라 Torchstone conform이 결정한다.

바로 실행 가능한 API 워크플로:

- `actor-idle.api.json` — 1024² 단일 액터 idle 베이스
- `actor-slinger-openpose.api.json` — SD1.5 OpenPose로 치켜든 팔을 고정한 투석 약탈자
- `environment-styletransfer.api.json` — 기존 6셀 시트 저 denoise img2img
- `item-static.api.json` — SDXL IsoPixel+Junkworld+PixelArtRedmond 정적 아이템 단일 소스.
  생성 모델이 지정 크로마키 대신 균일한 중성 플레이트를 낼 때는
  `process_items_v3.py`가 테두리 연결 영역만 허용 오차 28로 제거한다.

API 워크플로들은 이 머신에 설치된 `zavychromaxl_v100.safetensors`와 manifest의 정식 LoRA 파일명을
참조한다. ComfyUI Desktop을 켠 뒤 `models` 명령으로 인식 여부를 확인하고 실행한다.

## 11. 애니메이션/이펙트 키프레임 워크플로 (권장)

Project-C는 액터/이펙트의 최종 애니메이션을 AI로 한 번에 찍지 않고, ComfyUI에서
`키프레임 후보`만 뽑아 Aseprite에서 규칙화한다.

### 권장 순서

1. `art_runner.py`가 레시피의 `pipeline.shots`/`effect_variants.variants`를 각각
   독립된 ComfyUI REST 작업으로 제출한다.
2. 한 후보 안의 모든 결과는 라벨이 붙은 `raw.png` 리뷰 시트로 묶이고, 실제 원본은
   `shots/<shot-id>/raw.png`에 그대로 보존한다.
3. Slack의 `Aseprite 소스 세트` 또는 CLI `prepare`가 각 샷을 지정 캔버스로 정리하고
   Torchstone 팔레트의 개별 `.aseprite` 원본과 `aseprite-handoff.json`을 만든다.
4. Slack의 `애니 초안` 또는 CLI `animation`이 Lua 조립기를 실행해 Tag(`idle`, `walk`,
   `attack`, `hit`, `fall`, `death`, `burst`, `idle-loop`)와 GIF를 만든다.
5. Aseprite에서 발 기준선·실루엣·인비트윈·최종 duration을 마감한다.
6. Slack 평가를 거친 뒤 `Assets/_Project/Art/Source/Aseprite/` 슬롯에 반영한다.

### CLI 실행

```bash
python3 Tools/ArtPipeline/art_runner.py init
python3 Tools/ArtPipeline/art_runner.py submit \
  actor-slinger-animation-v5 --shot idle --count 1
python3 Tools/ArtPipeline/art_runner.py work --once

python3 Tools/ArtPipeline/art_runner.py submit \
  actor-slinger-animation-v5 --count 1 --requested-by local
python3 Tools/ArtPipeline/art_runner.py work --once

python3 Tools/ArtPipeline/art_runner.py submit \
  fx-impact-suite-v2 --count 1 --requested-by local
python3 Tools/ArtPipeline/art_runner.py work --once

# Slack의 Aseprite 소스 세트와 같은 준비 작업 후:
python3 Tools/ArtPipeline/art_runner.py prepare <candidate-id>
python3 Tools/ArtPipeline/art_runner.py work --once
python3 Tools/ArtPipeline/art_runner.py animation <candidate-id> \
  --timing-scale 1.0
python3 Tools/ArtPipeline/art_runner.py work --once
```

발주가 어디까지 왔는지는 `art_runner.py progress --watch`(또는 뷰어 상단 스트립)로 본다.
`submit` 은 이 표가 아니라 **완료된 job의 실측 중앙값**으로 예상 시간을 stderr에 낸다.

생성된 후보를 Slack 없이 판정하려면 `python3 Tools/ArtPipeline/art_runner.py review`로 로컬
뷰어를 띄운다. 후보 ID를 손으로 옮겨 적는 대신 `approve ^2`·`prepare`(선택기)처럼 별칭을 쓴다 —
둘 다 [`../ART_REVIEW_AUTOMATION.md`](../ART_REVIEW_AUTOMATION.md) §3-b-1·§3-b-2가 소유한다.

처음 파라미터를 검증할 때는 전체 액터 포즈 세트를 만들지 말고
`--shot idle` 또는 `--shot walk-contact-a`로 한 장만 실행한다.
같은 작업은 Slack에서
`/art shot actor-slinger-animation-v5 idle 1`로 실행할 수 있다. 백그라운드 서비스가
실행 중이면 CLI 예제의 `work --once`는 생략한다.
`--count 1`의 전체 실행은 레시피에 선언된 액터 10~11개 포즈 샷, 이펙트 6개 슬롯 샷을
생성한다. 후보 수는 서로 다른 전체 세트의 개수다. OpenPose 가이드를 바꾼 뒤에는
`python3 Tools/ArtPipeline/generate_openpose_guides.py`로 포즈 가이드를 다시 만든다.
가이드의 색과 연결 순서는 장식이 아니라 SD1.5 OpenPose ControlNet의 BODY_18 입력 계약이다.
임의 색 스틱 그림으로 바꾸면 denoise를 올려도 포즈보다 캐릭터 정체성이 먼저 흔들린다.
정식 액터 identity 가이드는
`python3 Tools/ArtPipeline/generate_actor_identity_guide.py`가 기본적으로
`actor-slinger.aseprite` 첫 프레임에서 버전 고정 512 입력을 생성한다. 이미 런타임에서
검증된 PNG를 production anchor로 쓸 때는 입력과 출력을 명시한다.

```bash
python3 Tools/ArtPipeline/generate_actor_identity_guide.py \
  --source Assets/_Project/Art/Runtime/actor-knight.png \
  --output docs/art-direction/comfyui/guides/actor-survivor-runtime-source-512-v1.png
```

정적 폴백에 이미 구 직업 장비가 구워져 있으면 final PNG를 지우지 않고 guide에서만 그
영역을 비운다. 원정자 vertical slice는 왼손 봉과 오른손 방패 영역을 아래처럼 비워
OpenPose가 양손을 다시 만들게 한다.

```bash
python3 Tools/ArtPipeline/generate_actor_identity_guide.py \
  --source Assets/_Project/Art/Runtime/actor-knight.png \
  --output docs/art-direction/comfyui/guides/actor-survivor-neutral-source-512-v1.png \
  --clear-box 70,225,105,220 \
  --clear-box 285,205,160,285
```

`--clear-box`는 production bootstrap 입력에만 쓴다. 최종 Aseprite 프레임을 지우는 도구가
아니며, 승인된 base 후보가 생기면 다음 단계는 그 후보 원본을 identity로 사용한다.

### 샘플 레시피

- `docs/art-direction/comfyui/recipes/actor-slinger-animation-v5.yaml`
- `docs/art-direction/comfyui/recipes/fx-impact-suite-v2.yaml`

### 추천 Slack 체크리스트

- `슬롯 일치`: slot 값과 실제 슬롯 매핑이 맞는가
- `캔버스/피벗`: 96×128, 24×24, 32×32 등 지정 크기 일치
- `루프`: 걷기/상태 이펙트의 Tag와 재생 속도가 의도대로인지
- `팔레트`: `project-c-torchstone.gpl`만 사용
- `정지 프레임`: idle/fall/hit가 어색하게 고정되지 않는가

더 자세한 운영 규칙은 `docs/art-direction/animation-effect-workflow.md`를 함께 본다.

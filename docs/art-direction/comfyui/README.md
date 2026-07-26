# ComfyUI 스타터 워크플로 — collapsed-transit 스타일 트랜스퍼

`collapsed-transit-styletransfer.workflow.json` — 기존 6-셀 환경 시트를 **실루엣·2:1 투영을
보존한 채 재료만 postapoc로** 바꾸는 통제형 생성 스캐폴드. 개념·근거는
`../comfyui-to-aseprite-pipeline.md`, 첫 실행 범위는 `../vertical-slice-01-collapsed-transit-env.md`.

> **성격**: 스톡 ComfyUI에서 바로 로드되도록 **코어 노드만**으로 짠 최소 그래프
> (img2img + ControlNet + LoRA 슬롯). IPAdapter와 LineArt 전처리기는 커스텀 노드라
> 코어에서 빼고 아래 "권장 추가"로 안내한다. 노드가 빨갛게 뜨면 설치 버전 차이 —
> 우클릭 → Add Node로 대체한다.
>
> **자동화 결정(2026-07-26)**: MCP 대신 **ComfyUI Desktop의 로컬 REST API**를 사용한다.
> 실행은 `Tools/ArtPipeline/comfy_batch.py`, 결정론적 마감은 Aseprite CLI/Lua
> (`aseprite_conform.sh` → `aseprite_conform.lua`)가 담당한다.

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

### 7-b. API 형식으로 저장

이 폴더의 `collapsed-transit-styletransfer.workflow.json`은 **ComfyUI 캔버스 편집 형식**이다.
Desktop에서 모델과 노드를 채운 뒤 **Save/Export (API Format)**으로 별도 저장해야 REST 실행이
가능하다. 캔버스 JSON을 `/prompt`에 그대로 보내지 않는다.

API 워크플로 실행 예:

```bash
python3 Tools/ArtPipeline/comfy_batch.py run \
  docs/art-direction/comfyui/collapsed-transit-styletransfer.api.json \
  --upload 5.image=docs/art-direction/project-c-collapsed-transit-environment-source-v2.png \
  --set 9.seed=42 \
  --set 9.denoise=0.55 \
  --output-dir docs/art-direction/comfyui/output
```

- `--set NODE.INPUT=VALUE`: API 그래프의 입력을 덮어쓴다. 숫자·bool·배열은 JSON으로 해석한다.
- `--upload NODE.INPUT=PATH`: `/upload/image`에 올리고 반환 파일명을 해당 입력에 넣는다.
- 기본값은 완료까지 기다린 뒤 `/history`와 `/view`로 결과를 내려받는 것이다.
- `output/`은 검토용이며 gitignore 대상이다. 채택한 결과만 정식 `*-source-v3.png` 이름으로
  `docs/art-direction/`에 옮긴다.

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

애니 액터는 이 도구가 만든 **idle 베이스에서 멈춘다**. walk/attack/hit/fall/death는
Aseprite에서 발 기준선을 고정해 손작업한다.

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

둘 다 이 머신에 설치된 `zavychromaxl_v100.safetensors`와 manifest의 정식 LoRA 파일명을
참조한다. ComfyUI Desktop을 켠 뒤 `models` 명령으로 인식 여부를 확인하고 실행한다.

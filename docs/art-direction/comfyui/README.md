# ComfyUI 스타터 워크플로 — collapsed-transit 스타일 트랜스퍼

`collapsed-transit-styletransfer.workflow.json` — 기존 6-셀 환경 시트를 **실루엣·2:1 투영을
보존한 채 재료만 postapoc로** 바꾸는 통제형 생성 스캐폴드. 개념·근거는
`../comfyui-to-aseprite-pipeline.md`, 첫 실행 범위는 `../vertical-slice-01-collapsed-transit-env.md`.

> **성격**: 스톡 ComfyUI에서 바로 로드되도록 **코어 노드만**으로 짠 최소 그래프
> (img2img + ControlNet + LoRA 슬롯). IPAdapter와 LineArt 전처리기는 커스텀 노드라
> 코어에서 빼고 아래 "권장 추가"로 안내한다. 노드가 빨갛게 뜨면 설치 버전 차이 —
> 우클릭 → Add Node로 대체한다.

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

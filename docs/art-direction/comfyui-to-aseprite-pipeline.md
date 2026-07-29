# ComfyUI → conform 파이프라인 (통제형 로컬 생성)

> **한 줄**: 범용 AI가 "자꾸 안 맞은" 이유는 *무통제* 생성이었다. ComfyUI는 노드로
> **실루엣·투영·팔레트를 강제**할 수 있어, 기존 `ImageGen 스타일 트랜스퍼` 단계를 그대로
> 대체·강화하는 통제형 생성 루트다. 마감(그리드·피벗·팔레트 잠금)은 여전히 결정론적 단계
> (Python 후처리 또는 Aseprite)가 맡는다.
>
> **자동화 결정(2026-07-26)**: 로컬 ComfyUI Desktop은 MCP가 아니라 `127.0.0.1:8188`
> REST API로 제어한다(`Tools/ArtPipeline/comfy_batch.py`). 정적 최종본 승격은
> Aseprite CLI/Lua(`aseprite_conform.sh`)를 사용한다. 실행 예시는 `comfyui/README.md`.
>
> 개념 워크플로 상위 문서: `ai-to-aseprite-workflow.md`. 규격은 `asset-spec-sheet.md`.
> 팔레트 SSOT: `Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl`.

## 0. 어디에 끼워 넣나 (기존 파이프라인과의 관계)

기존 collapsed-transit v2는 이미 이렇게 돈다:

```
레퍼런스(ref-01~05·target-v2)
  └─(개념)→ [ImageGen 스타일 트랜스퍼] → 고해상 시트(#ff00ff 배경, 고정 3×2 셀)
              └→ [Tools/ArtPipeline/process_postapoc_*_v2.py] → PNG(Art/Environment 등)
                    └→ Unity 자동 임포트(Point/PPU128, ui-*만 64/무압축)
```

이 문서는 **생성 단계 하나만** 교체한다:

```
              [ImageGen 스타일 트랜스퍼]  →  [ComfyUI 스타일 트랜스퍼]  (더 통제됨)
```

마감 단계(`process_postapoc_*_v2.py` 또는 Aseprite)와 규격·파일명 계약은 **그대로**다.

### 통제형이 되는 이유 (범용 생성과의 차이)

| 문제(무통제) | ComfyUI 통제 수단 |
|--------------|--------------------|
| 2:1 아이소 각도가 흔들림 | **img2img + ControlNet(LineArt/Canny/Depth)**를 기존 스프라이트/아이소 가이드에 물려 실루엣·투영 고정 |
| 스타일이 시트마다 다름 | **IPAdapter**로 레퍼런스(ref·target)를 스타일 앵커로 물림 |
| 색이 매 생성 드리프트 | 프리뷰 posterize + **마감에서 `.gpl` 잠금**(§3) |
| 결과 재현 불가 | **고정 seed + 배치** |

## 1. 원리 (비협상)

- **AI = 실루엣·재료·명암까지만.** 그리드 스냅·발 피벗·팔레트 잠금·**애니메이션**은 결정론적 마감이 한다.
- **정적(환경 타일·소품·아이템·디오라마·UI 아이콘 베이스)** → ComfyUI로 대량 생산 OK.
- **애니 액터(idle/walk/attack/hit/fall/death)** → ComfyUI는 **idle 베이스 포즈까지만**.
  프레임 간 발 고정·실루엣·팔레트 일관은 AI가 못 지킨다 → **Aseprite 손 애니**(§4).
- AI 모델은 96×128 저해상 도트를 native로 못 뽑는다. 항상 **고해상 생성 → 다운스케일 → 팔레트 잠금**.

## 2. ComfyUI 그래프 (환경 시트 스타일 트랜스퍼 예)

> 노드 팩 이름은 대표값 — 설치 버전에 맞춰 대체한다.
> (ControlNet Aux 전처리기, ComfyUI_IPAdapter_plus, 표준 KSampler/VAE Encode(img2img).)

**입력**: 기존 6-스프라이트 소스 시트(원본 실루엣이 이미 2:1·baseline 정합) 또는
신규 슬롯이면 손으로 만든 회색 아이소 가이드(다이아/박스를 캔버스 크기로 렌더).

```
[Load Checkpoint: SDXL] ─┐
[Load LoRA: pixel-art SDXL] ─┤→ MODEL/CLIP
[VAE Encode (img2img)] ← 소스 시트                → LATENT (denoise 0.45~0.65)
[ControlNet: LineArt/Canny] ← 소스 시트           → 실루엣·셀 위치 고정 (weight 0.7~1.0)
[IPAdapter] ← ref-01/02/05 + target-v2            → 스타일 앵커 (weight 0.5~0.8)
[Positive/Negative prompt]                        → 아래 프롬프트 키트
[KSampler: 고정 seed] → [VAE Decode] → [Save]     → 고해상 시트(#ff00ff 평면 배경 유지)
```

- **denoise를 낮게(0.45~0.65)** 두면 원본 실루엣·2:1·셀 분리·여백이 보존되고 재료만 바뀐다.
  (기존 ImageGen 프롬프트의 "preserve exact cell positions/scale/perspective/silhouettes" 규율과 동일.)
- 배경은 **평면 `#ff00ff`** 유지 → 기존 후처리의 크로마키 제거가 그대로 동작. 에셋 내부엔 `#ff00ff` 금지.
- 신규 슬롯(원본 없음)은 img2img 대신 **손 아이소 가이드를 Depth/LineArt ControlNet**에 물려 투영을 강제한다.

### 프롬프트 키트 (기존 env 프롬프트 어휘 계승)

- **Positive**: `polished chunky isometric pixel art, 2:1 dimetric, abandoned underground
  transit facility, cracked concrete, oxidized dark steel, restrained rust-orange wear,
  saturated amber only for emergency light, one very subtle desaturated teal service stripe,
  crisp hard edges, blue-black charcoal shadows`
- **Negative**: `fantasy masonry, wood planks, medieval iron straps, torches, arches, runes,
  cyberpunk neon overload, excessive dithering, white outline noise, photorealism,
  smooth 3D/vector look, text, watermark`

### 카테고리별 가이드

| 대상 | 생성 방식 | ControlNet 소스 | 후 마감 |
|------|-----------|-----------------|---------|
| 환경 6-시트(있음) | img2img 스타일 트랜스퍼 | 기존 스프라이트 시트(LineArt) | §3 Python |
| 신규 타일/소품 | txt2img + 아이소 가이드 | 손 회색 다이아/박스(Depth) | §3 Python 또는 Aseprite |
| 아이템 32×32 | txt2img → 강한 다운스케일 | (옵션) 단순 실루엣 | §3 |
| **액터 idle 베이스** | txt2img/img2img (포즈만) | (옵션) OpenPose | **§4 Aseprite 손 애니** |

## 3. 마감 A — Python 후처리 (정적·배치, `.gpl` 잠금)

기존 `Tools/ArtPipeline/process_postapoc_environment_v2.py`는 셀별로
`quantize(colors=32, MEDIANCUT)`를 썼다 — **시트마다 팔레트가 독립**이라 서로 안 붙는 원인이었다.
**공용 `.gpl`로 고정 양자화**하면 모든 시트가 한 팔레트로 잠긴다(=응집).
→ **구현됨**: `Tools/ArtPipeline/torchstone_palette.py`(`lock_to_palette`)로 뽑아 env 프로세서에 배선.

> ⚠️ **실측 교훈(중요)**: **UI 팔레트(26색)로 곧장 하드락하면 오히려 더 나빠진다.** UI 토큰엔
> 중립 계조가 부족해, 무디더 최근접 양자화가 콘크리트 중간톤을 teal/moss/xp 같은 채도색으로
> 스냅해 **노이즈**를 뿌린다. 그래서 `.gpl`은 **UI 토큰의 슈퍼셋**이어야 한다 — 실아트에서
> 추출한 **웜 중립 계조(인덱스 30~)**를 보강해야 콘크리트/강철이 깨끗하게 잠긴다.
> 즉 **UI 토큰 ⊂ 스프라이트 마스터 `.gpl`**. 새 카테고리를 잠글 땐 그 아트에서 부족 계조를
> 추출해 `.gpl`에 append하고 검증한다(env는 웜 중립 4색 보강으로 확인 완료).

```python
from PIL import Image

def load_gpl(gpl_path):
    """project-c-torchstone.gpl → [(r,g,b), ...] (<=256)."""
    colors = []
    for line in gpl_path.read_text(encoding="utf-8").splitlines():
        s = line.strip()
        if not s or not s[0].isdigit():   # 헤더/주석(GIMP/Name/Columns/#) 건너뜀
            continue
        r, g, b = (int(v) for v in s.split()[:3])
        colors.append((r, g, b))
    return colors

def palette_image(colors):
    pal = Image.new("P", (1, 1))
    flat = [c for rgb in colors for c in rgb]
    flat += [0] * (768 - len(flat))       # 256*3 패딩
    pal.putpalette(flat)
    return pal

# reduce_colors() 안에서 median-cut 대신:
locked = rgb.quantize(palette=palette_image(load_gpl(GPL_PATH)),
                      dither=Image.Dither.NONE).convert("RGBA")
```

- `dither=NONE` 유지(도트 경계 보존). 알파는 기존처럼 컷오프 80으로 하드닝.
- 이렇게 하면 환경·소품·아이템 시트가 **전부 같은 `.gpl` 인덱스**를 쓴다. UI 토큰은 그중
  의미 있는 **부분집합**(신호색·프레임)이라 화면과 월드의 신호색이 자동으로 일치한다.
- 규격(캔버스·미러·계단 fit)·파일명 계약은 기존 프로세서 그대로 둔다. 바꾸는 건 양자화 타깃뿐.

## 4. 마감 B — Aseprite (히어로·손터치·애니)

정적이라도 히어로 타일이나 손터치가 필요하면, 그리고 **모든 액터 애니**는 Aseprite로 간다.

1. 생성 베이스를 배경 레이어로 깔고 **Indexed 모드 → `.gpl` 로드 → Remap**(팔레트 이탈 차단).
2. **AA 제거·오프그리드 정렬**(반픽셀 어긋남), 밴딩 정리.
3. **캔버스·피벗을 `asset-spec-sheet.md` 규격으로** (액터 96×128, 피벗 (0.5,0.04) 등).
4. **애니**: 온니언스킨 + 발 기준선 고정, 태그 `idle/walk/attack/hit/fall/death`,
   비반복 태그 Repeat=1, Layer UUID 켜기. ComfyUI 출력은 **idle 디자인 참고만**, 프레임은 손으로.
5. **정식 파일명**으로 `Art/Source/Aseprite/<파일명>.aseprite` 저장 → 자동 임포트·카탈로그 연결.

## 5. 검증 (양쪽 마감 공통)

- `Project-C > Art > Aseprite > Validate Sources` — 파일명·규격·프레임 경고 0.
- **Unity MCP Play 캡처(현 우선순위: PC 가로만)** — FOV 3상태(Unknown/Explored/Visible)에서
  톤 유지, 발/피벗이 타일에 정확히 앉는지.
- 회귀: EditMode `ProjectC.Tests.EditMode` / PlayMode `ProjectC.Tests.PlayMode` **둘 다 재실행**.
  테스트 개수는 기준 커밋 없이 복제하지 않는다.

## 6. 흔한 실패 (ComfyUI 특화 — 상위 문서 표에 추가)

| 증상 | 원인 | 조치 |
|------|------|------|
| 아이소 각도 어긋남 | txt2img가 투영을 모름 | 실제 2:1 가이드를 LineArt/Depth ControlNet에 물림 |
| 실루엣 붕괴 | denoise 과다 | denoise↓(0.45~) · ControlNet weight↑ |
| 시트끼리 색 따로 놈 | median-cut 독립 양자화 | **`.gpl` 고정 양자화**(§3)로 전환 |
| 걷는데 미끄러짐 | AI 프레임 발 위치 제각각 | 애니는 Aseprite 손작업(§4) |
| 도트에 AA/노이즈 | 고해상 잔재 | 다운스케일 후 경계 단색화·Indexed remap |

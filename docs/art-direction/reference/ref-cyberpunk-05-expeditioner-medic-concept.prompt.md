# 원정자 정식 컨셉 후보 — 필드 메딕 생존자 (ref-cyberpunk-05)

- **역할**: 메인 원정자(`actor-knight` 슬롯) **정식 디자인 후보의 정체성 레퍼런스**.
  ref-cyberpunk-00~04(외부 스타일 레퍼런스)와 달리 **자체 파이프라인 출력의 채택 스냅샷**이다.
- **출처**: ComfyUI 배치 `comfyui/output/concept-final-v1/actor-idle_00062_.png`
  (output은 gitignore라 리포 보존용으로 여기 승격). 원본 PNG에 ComfyUI prompt 메타데이터가
  그대로 박혀 있어 아래 재현값은 PNG에서 재추출 가능하다.
- **채택**: 2026-07-30 사용자 확정. 게임 스케일 판정 근거(96×128 행 계약 crown 8/feet 122,
  `art_runner.game_scale_preview`):
  `docs/captures/expeditioner-medic-concept-game-preview-v1.png` — 포니테일 실루엣·얼굴·주황
  팁이 게임 배율에서 판독됨.

## 재현값 (v2 확정 스택 — 액터 계약 §4)

- checkpoint `animagine-xl-4.0-opt.safetensors` + LoRA
  `project-c-pixelart-redmond-sdxl-v1-lite64.safetensors` 0.45/0.45
- 1024², seed `57003`, steps 28, cfg 5.0, euler_ancestral/karras, denoise 1.0, batch 2
- 그래프: `comfyui/actor-idle.api.json` (txt2img — OpenPose 없음, 컨셉 단계라 의도된 것)

Positive:

```text
1girl, solo, chibi, pixel art, full body, standing, three-quarter view, field medic survivor,
blonde messy short ponytail with orange tint tips, determined kind eyes, reworked white coat
as patched short jacket with rolled sleeves, gray utility shirt, medical pouch on hip belt,
shoulder satchel, fingerless gloves, dark leggings, sturdy boots, one hand adjusting satchel
strap, isolated character, simple gray background, flat color, clean pixel clusters, safe,
masterpiece, high score, great score, absurdres
```

Negative:

```text
lowres, bad anatomy, bad hands, text, error, missing finger, extra digits, fewer digits,
cropped, worst quality, low quality, low score, bad score, average score, signature,
watermark, username, blurry, realistic proportions, tall body, small head, multiple views,
multiple characters, gradient background, scenery, photorealistic, 3d, neon overload,
fully glowing outfit, covered eyes, hidden eyes, mask over mouth, covered face, weapon,
baseball bat
```

## 승격 전 남은 게이트 (액터 계약 v2 기준)

1. **팔레트 append** — `.gpl`에 블론드 헤어 램프가 없다(§3에서 skin 3단·hair-pink 2단만
   append됨). `hair-blonde` 2단(base/light)을 같은 원칙으로 append해야 conform에서 머리가
   fabric/tan 진흙색으로 양자화되지 않는다.
2. **주황 팁 ↔ 앰버 광원 간섭**(§2.4) — 팁의 주황이 환경 앰버 광원과 혼동되지 않게
   채도·명도 분리를 conform/Aseprite 마감에서 확인한다. 흰 재킷의 잔여 주황 얼룩은
   소스 노이즈이므로 마감에서 제거.
3. **정면 컨셉은 게임 투입 불가**(§4-b) — 3/4 부감 + 4방향 기본 스프라이트를
   `actor-chibi-base-v1` 레인(OpenPose `guides/openpose/actor-chibi-*`, strength 0.9/end 0.8)
   으로 재생성하고, 이 이미지는 그 잡의 정체성 레퍼런스로만 쓴다.
4. 기본 슬롯 `allow_replace: false` — 4방향 승인 전 `actor-knight.aseprite`(치비 라이더
   초안)를 교체하지 않는다.

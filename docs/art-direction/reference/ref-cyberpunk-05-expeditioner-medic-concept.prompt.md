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

## 게이트 진행 기록 (2026-07-31 초안 승격까지)

1. ~~팔레트 append~~ — **완료.** `hair-blonde-1/2`(#E0B94F/#F5E288) append.
   기존 세트 재-conform 절도 검사에서 **블론드로 이동한 픽셀 0** 확인
   (같은 검사에서 드러난 skin 램프의 기존 잠재 절도 632px은 별개 이슈로 분리).
2. **4방향 기본 스프라이트** — `actor-knight-medic-base-v1` 잡
   `ART-20260730-054450-dc29b2` **C01 채택**(south/west/north 정체성·얼굴 판독 성립,
   C02는 방향 간 머리색 불일치로 거절). east 재롤(`ART-20260730-112626-6eb7bf`)은
   두 후보 모두 정면 뷰로 나와 **거절** — C01 east를 유지하고 헤어 드리프트는 마감 몫.
3. **액션 키포즈 9종** — `actor-knight-medic-anim-v1` 잡 `ART-20260730-113516-1f1d1c`
   C01 채택(seed 2130163433 고정으로 기본 스프라이트와 정체성 연결).
4. **초안 조립·승격** — conform 사본에 sig-ice→grey-6 일괄 재매핑(재킷 틸 오염 처방,
   §3 실측과 같은 계열) 후 `medic-anim/animation-manifest.json` + Lua 조립기로
   11프레임/6태그 `actor-knight-medic-draft.aseprite`를 만들어
   `Art/Source/Aseprite/actor-knight.aseprite`(라이더 초안 자리)에 승격했다.

## Aseprite 마감 잔여 항목 (사람 몫)

- east 방향 헤어 볼륨·올리브 톤 재매핑 (기본 4방향 소스 세트 한정 — 초안 타임라인은 south만 쓴다)
- walk-pass 포니테일 소실·fall 잔상 노이즈·death 핏자국 축소, 프레임 간 발 기준선 고정
- 주황 팁 ↔ 앰버 광원 채도 분리 확인(§2.4), north 발밑 바닥선 제거

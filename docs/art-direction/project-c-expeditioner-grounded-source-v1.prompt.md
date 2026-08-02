# 원정자 접지 정적 소스 v1

- 생성 방식: Codex 내장 ImageGen (`stylized-concept`)
- 생성일: 2026-08-02
- 최종 소스: `project-c-expeditioner-grounded-source-v1.png`
- SHA-256: `ed2ea1cb8aad7e04afe5ad1a29583c5d5ec8ad1506426e89d1c6ddfaf5ce02e7`

## 입력 이미지 역할

1. `reference/ref-cyberpunk-05-expeditioner-medic-concept.png` — 캐릭터 정체성 기준
2. `docs/captures/b2-floor-light-coherence-q0-live-v1.png` — 실제 카메라·바닥 투영·게임 배율 기준
3. 사용자 레퍼런스 `IMG_5325.JPG` — 픽셀 클러스터와 사이버펑크 재질 밀도 기준

## 최종 프롬프트

```text
Use case: stylized-concept
Asset type: production source for one 96x128 isometric pixel-art player sprite in Project-C
Input images: Image 1 is the character identity reference; Image 2 is the actual gameplay camera, scale, floor projection, and grounding reference; Image 3 is the target pixel-cluster discipline and cyberpunk material-density reference.
Primary request: redraw the same field-medic survivor from Image 1 as one clean full-body 3/4 isometric gameplay pose that visibly plants both boots on the floor plane used in Image 2.
Subject: same blonde messy short ponytail with restrained orange tips, visible determined face, patched short off-white medic jacket over charcoal utility clothing, hip medical pouch, compact shoulder satchel, fingerless gloves, sturdy dark boots, empty hands, no fixed weapon.
Pose: compact 2.5-to-3-head-tall silhouette, viewed from slightly above, torso and feet aligned to a 2:1 isometric floor, knees subtly bent, boots sharing one exact horizontal ground baseline, weight clearly resting through the legs; no front-facing fashion pose.
Style/medium: deliberate hand-authored pixel art, chunky coherent clusters, crisp 1-pixel stepped contours at final scale, limited 20-28 color palette, strong material separation, readable face and ponytail at gameplay size, restrained cyberpunk 2077 street-medic vocabulary without copying any trademarked design.
Composition/framing: exactly one isolated character, centered, full body, generous empty margin, no crop.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal.
Lighting/mood: one upper-left practical key light, cool charcoal shadows, restrained warm highlights; no glow flooding.
Constraints: preserve the identity and outfit language from Image 1; prioritize a broad stable foot silhouette and a clear contact zone; background must be uniform #ff00ff with no floor, shadow, gradient, texture, reflections, or lighting variation; no text, no UI, no health bar, no locator arrow, no watermark.
Avoid: smooth digital painting, 3D render, anti-aliased blur, high-frequency salt-and-pepper noise, long thin limbs, tiny feet, floating pose, tiptoe pose, perspective mismatch, front-facing pose, giant head, weapon, shield, gas mask worn on face, broad cyan clothing, broad red clothing, neon outline, cast shadow, extra props, multiple views, multiple characters.
```

## 마감 계약

- 생성 이미지는 직접 런타임에 넣지 않는다.
- 이 소스에는 의도한 마젠타가 없으므로 실루엣 사이에 갇힌 배경까지 키 제거하고 `96×128`, 발 기준선 `y=124`, 공용 Torchstone 팔레트로 결정론적으로 conform한다.
- 첫 승격은 애니메이션이 아닌 정적 `Frame_0` 품질 게이트다. 이동·공격 애니메이션 승인은 별도다.

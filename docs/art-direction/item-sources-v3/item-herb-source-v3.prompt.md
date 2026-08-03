# 지혈 패치 소스 v3 — ImageGen 프롬프트와 provenance

- 생성: 2026-08-03
- 도구: OpenAI ImageGen 신규 생성
- 상태: 채택 — `item-herb-source-v3.png`
- 런타임: `Tools/ArtPipeline/process_items_v3.py`로 마감한
  `Assets/_Project/Art/Runtime/item-herb.png`
- 호환 계약: `ItemKind.Herb = 9`, `item-herb` 슬롯과 파일명은 유지한다. 표시만
  `지혈 패치` / `PATCH`로 바꾼다.

## 채택 발주 요지

```text
Use case: stylized-concept
Asset type: Project-C single item sprite source, later reduced to a 64×64 runtime pixel-art prop

Create one compact post-apocalyptic cyberpunk hemostatic patch / field-dressing packet on a perfectly
flat solid #FF00FF chroma-key background. The object is a worn off-white foil pouch with one simple red
medical-cross symbol, a bold orange sealing band around its lower third, and a small dark-steel pull tab.
Use a clear 3/4 isometric view and one chunky, immediately readable silhouette. Render deliberate large
pixel clusters and hard alpha-like edges suitable for clean reduction; keep the packet centered and make it
fill most of the square frame.

No plants, fungus, mushrooms, mycelium, spores, herbs, leaves, roots, flowers, potion bottle, alchemy,
glow, magic, ground shadow, platform, background scene, extra objects, watermark, letters or words.
The simple medical-cross pictogram is the only allowed symbol. Do not use #FF00FF inside the object.
```

## 판독 계약

- HUD 32px에서도 흰 패킷, 적십자, 주황 밀봉띠의 세 덩어리가 먼저 읽힌다.
- 식물·버섯이 아니라 거점에서 조달한 현장 의료 소모품이어야 한다.
- 패킷 두 개를 묶어 `응급 키트` 1회분을 만드는 기존 조합 규칙은 바꾸지 않는다.

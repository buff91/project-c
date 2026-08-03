# 냉각 코일 소스 v3 — ImageGen 프롬프트와 provenance

- 생성: 2026-08-03
- 도구: OpenAI ImageGen 신규 생성
- 상태: 채택 — `item-frost-shard-source-v3.png`
- 런타임: `Tools/ArtPipeline/process_items_v3.py`로 마감한
  `Assets/_Project/Art/Runtime/item-frost-shard.png`
- 호환 계약: `ItemKind.FrostShard = 11`, `item-frost-shard` 슬롯과 파일명은 유지한다. 표시만
  `냉각 코일` / `COIL`로 바꾼다.

## 채택 발주 요지

```text
Use case: stylized-concept
Asset type: Project-C single item sprite source, later reduced to a 64×64 runtime pixel-art prop

Create one compact dark-steel microchannel cooling-coil cartridge / heat exchanger on a perfectly flat
solid #FF00FF chroma-key background. Show thick exposed copper loop tubing across the front, dark cooling
fins, one practical hose port, and only restrained cyan coolant or frost traces on the metal fittings. Use a
clear 3/4 isometric view and one chunky, immediately readable industrial silhouette. Render deliberate large
pixel clusters and hard alpha-like edges suitable for clean reduction; keep the cartridge centered and make
it fill most of the square frame.

No crystal, shard, icicle, ice sword, blade, gem, jewel, transparent glass, magic, rune, glow, ice pedestal,
ground shadow, platform, background scene, extra objects, text or watermark. Do not use #FF00FF inside the
object.
```

## 판독 계약

- HUD 32px에서도 검은 프레임과 굵은 구리 루프가 먼저 읽혀야 한다.
- 보석·얼음 조각이 아니라 수리 가능한 산업용 냉각 부품이어야 한다.
- 급조 폭발물에 장착해 `냉각재 수류탄`을 만드는 기존 조합 규칙은 바꾸지 않는다.

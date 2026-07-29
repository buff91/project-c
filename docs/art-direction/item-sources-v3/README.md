# Project-C item source v3

2026-07-29 품질 게이트에서 확정한 12종 아이템 소스다. 생성 결과를 그대로 런타임에 넣지 않고
`Tools/ArtPipeline/process_items_v3.py`가 64×64 캔버스, 하드 알파, Torchstone 공용 팔레트,
아이템별 바닥 피벗 여백으로 마감한다.

## 생성 방식

- 도구: Codex 내장 ImageGen
- 참조 1: `project-c-integrated-postapoc-gameplay-target-v2.png`
- 참조 2: `project-c-torchstone-ui-icons-source-v1.png`
- 배경: 단색 `#ff00ff` 크로마키
- 공통 스타일: premium hand-pixelled game sprite source, chunky deliberate pixel clusters,
  strong dark navy outline, 5–7 stepped tonal clusters, no anti-aliasing
- 공통 금지: text, numbers, extra items, cast/contact shadow, watermark, platform,
  background scene, vector/painterly/photorealistic rendering, gradients, soft edges
- 구도: 한 파일에 한 오브젝트만 배치하고, 정사각 캔버스의 약 70–76%를 채운다.

## 최종 프롬프트 세트

모든 항목은 위 공통 생성 방식과 아래 항목별 요청·대상·팔레트를 합쳐 사용했다.

| 출력 | Primary request / Subject | Palette |
|---|---|---|
| `item-potion` | derelict hospital survivor의 rugged emergency healing ampoule; squat red medical ampoule, dark metal cap, worn off-white medical cross plate, chipped enamel, wrapped rubber grip | dark navy, warm taupe, rust red, off-white, amber |
| `item-bomb` | improvised fragmentation bomb; squat segmented dark-steel grenade, wrapped fuse, amber fuse tip, chipped hazard-yellow band | dark navy, gunmetal, taupe, rust, muted yellow, amber |
| `item-frost-bomb` | scavenged cryogenic grenade; hexagonal cyan coolant canister, steel trigger, frosted rim, pressure valve, off-white radial cold plate | dark navy, gunmetal, cyan, ice blue, taupe, white |
| `item-oil-flask` | survivor oil flask; dented amber fuel bottle, rusted wire cage, black cap, cloth grip, hazard droplet plate | dark navy, rust, amber ochre, taupe, dirty off-white, orange |
| `item-throwing-knife` | heavy reusable throwing knife; broad clipped steel blade, three balance holes, rust scratches, rubber wrap, yellow cord | dark navy, gunmetal, pale steel, rust, taupe, yellow |
| `item-recall-scroll` | emergency recall/evacuation chart; dirty folded hospital map, metal clips, teal return-arrow mark, red tie | dark navy, dirty off-white, taupe, rust red, gunmetal, teal |
| `item-coin-pouch` | scavenged barter-token pouch; dark canvas bag, leather corners, red cord, three brass transit tokens | dark navy, soot black, taupe, rust red, tarnished brass |
| `item-gemstone` | anomaly gemstone from hospital machinery; angular teal crystal in a three-prong rusted specimen cage | dark navy, gunmetal, rust, brass, teal, cyan, pale blue |
| `item-relic` | pre-collapse hospital relic; biometric scanner core, cracked ceramic plate, teal lens, copper coils, red strap | dark navy, gunmetal, rust, off-white, copper, teal, red |
| `item-herb` | hardy medicinal herb bundle; serrated sage stems, seed heads, dirty roots, medical bandage and red thread | dark navy, sage, olive, brown, dirty off-white, rust red |
| `item-blast-powder` | sealed crafting powder tin; dented steel tin, orange seal, yellow burst mark, red pull tab | dark navy, gunmetal, rust, orange, yellow, red, taupe |
| `item-frost-shard` | broken coolant shard; long jagged cyan fragment, dark steel base clamp, frosted edge, copper wire | dark navy, gunmetal, teal, cyan, ice blue, copper |

각 생성에서 배경은 “perfectly flat solid #ff00ff, one uniform color only”로 명시했고,
오브젝트 내부에는 magenta를 쓰지 않도록 제한했다. 대각선형은 칼/허브/서리 조각만 허용했다.

## ComfyUI 전환 게이트

- 위 12종의 현재 소스 provenance는 그대로 ImageGen이며 ComfyUI 생성물로 소급 표기하지 않는다.
- `item-static-v1 + item-potion` 레시피로 포션 1종을 먼저 재생성했다.
- SDXL이 마젠타 대신 균일한 중성 플레이트를 내는 경우가 있어 프로세서는 테두리에 연결된
  유사색 배경을 추가로 제거한다. 오브젝트 내부의 같은 색까지 전역 삭제하지 않는다.
- 첫 모델 조합 후보는 배경·그림자 게이트에서 탈락했다. IsoPixel 0.30 +
  Junkworld 0.20 + PixelArtRedmond 0.55 조합의 두 번째 후보는 64×64, 하드 알파,
  가시 픽셀 1,673으로 기계 게이트를 통과했다.
- 비교본은 `docs/captures/item-potion-comfy-gate-v2.png`이며, 한 종 승인 후 다음 아이템을
  진행하는 규칙에 따라 현재 런타임 12종을 일괄 덮어쓰지 않는다.

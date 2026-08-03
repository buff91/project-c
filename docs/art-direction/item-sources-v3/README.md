# Project-C item source v3

12종 아이템 소스다. 생성 결과를 그대로 런타임에 넣지 않고
`Tools/ArtPipeline/process_items_v3.py`가 64×64 캔버스, 하드 알파, Torchstone 공용 팔레트,
아이템별 바닥 피벗 여백으로 마감한다.

> **provenance가 갈린다.** 10종은 ComfyUI `item-static-v1` 생성물(§ 아케이드 재발주)이고,
> `item-herb`·`item-frost-shard` 두 종은 2026-08-03 OpenAI ImageGen 신규 생성물이다.
> 어느 쪽도 상대 쪽으로 소급 표기하지 않는다.

## 세계관 용어 교정 (2026-08-03 — 채택)

첫 던전과 아이템 공통 배경에는 초자연이 없으므로, 유기·판타지 실루엣으로 남아 있던 두 조합
재료를 기술·생존 장비로 교체했다. **기존 세이브와 Unity 참조를 보존하기 위해** enum 이름과 숫자,
슬롯 ID, 파일명은 그대로 둔다.

| 영구 계약 | 새 표시 계약 | 채택 소스 | 런타임 판독 |
|---|---|---|---|
| `ItemKind.Herb = 9` / `item-herb` | 지혈 패치 / `PATCH` | `item-herb-source-v3.png` | 적십자·주황 밀봉띠가 있는 현장용 지혈 패치 |
| `ItemKind.FrostShard = 11` / `item-frost-shard` | 냉각 코일 / `COIL` | `item-frost-shard-source-v3.png` | 구리 루프가 드러난 소형 냉각 코일 카트리지 |

- 생성: 2026-08-03, OpenAI ImageGen 신규 생성. 채택 프롬프트 요지는
  `item-herb-source-v3.prompt.md`·`item-frost-shard-source-v3.prompt.md`가 보존한다.
- 마감: 위 소스를 `Tools/ArtPipeline/process_items_v3.py`에 통과시켜
  `Assets/_Project/Art/Runtime/item-herb.png`·`item-frost-shard.png`로 승격했다.
- 두 PNG는 **채택본**이다. 2026-07-31의 버섯 군락과 구 냉매 결정 보존 판단은 아래에 역사로만 남긴다.

## 생성 방식 — 초기 ImageGen 세트 (2026-07-29, 이력 보존)

- 도구: Codex 내장 ImageGen
- 참조 1: `project-c-integrated-postapoc-gameplay-target-v2.png`
- 참조 2: `project-c-torchstone-ui-icons-source-v1.png`
- 배경: 단색 `#ff00ff` 크로마키
- 공통 스타일: premium hand-pixelled game sprite source, chunky deliberate pixel clusters,
  strong dark navy outline, 5–7 stepped tonal clusters, no anti-aliasing
- 공통 금지: text, numbers, extra items, cast/contact shadow, watermark, platform,
  background scene, vector/painterly/photorealistic rendering, gradients, soft edges
- 구도: 한 파일에 한 오브젝트만 배치하고, 정사각 캔버스의 약 70–76%를 채운다.

## 초기 프롬프트 세트 (현재 채택 계약 아님)

모든 항목은 당시 위 공통 생성 방식과 아래 항목별 요청·대상·팔레트를 합쳐 사용했다.
현재 소스는 10종 ComfyUI와 위 2026-08-03 ImageGen 두 종으로 교체됐으며, 이 표는 재현 이력이다.

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

당시 각 생성에서 배경은 “perfectly flat solid #ff00ff, one uniform color only”로 명시했고,
오브젝트 내부에는 magenta를 쓰지 않도록 제한했다. 대각선형은 칼/허브/서리 조각만 허용했다.

## ComfyUI 전환 게이트

- 전환 시작 당시 12종 소스 provenance는 ImageGen이었으며, 이후 채택된 ComfyUI 결과만 출처를 바꿨다.
- `item-static-v1 + item-potion` 레시피로 포션 1종을 먼저 재생성했다.
- SDXL이 마젠타 대신 균일한 중성 플레이트를 내는 경우가 있어 프로세서는 테두리에 연결된
  유사색 배경을 추가로 제거한다. 오브젝트 내부의 같은 색까지 전역 삭제하지 않는다.
- 첫 모델 조합 후보는 배경·그림자 게이트에서 탈락했다. IsoPixel 0.30 +
  Junkworld 0.20 + PixelArtRedmond 0.55 조합의 두 번째 후보는 64×64, 하드 알파,
  가시 픽셀 1,673으로 기계 게이트를 통과했다.
- 비교본은 `docs/captures/item-potion-comfy-gate-v2.png`다. 한 종 승인 후 다음 아이템을
  진행하는 규칙에 따라 포션 승격 판단 전에는 나머지를 덮어쓰지 않았다.

## 아케이드 재발주 (2026-07-31 — 당시 11종 교체)

색감만 사이버펑크고 **오브젝트 자체는 병원/판타지 그대로**라는 지적에서 출발했다. 정체성은
리스킨 표 §4가 소유하고, 항목별 `docs/art-direction/comfyui/subjects/item-*.yaml`이 그것을
프롬프트로 옮긴다. 배치 러너는
`docs/art-direction/comfyui/batches/item-arcade-batch-v1.py`(`--revision`/`--subjects`로 일부만
재발주 — 출력 폴더·매니페스트가 리비전으로 갈린다).

| 출력 | 채택 | 시드 | 판독 |
|---|---|---|---|
| `item-potion` | gate-v2 `00002` | 73101 | 적색 응급 캐니스터 + 흰 십자 |
| `item-bomb` | v2 `00033` | 619200 | 노란 위험띠 두른 파이프 폭약 |
| `item-frost-bomb` | v4 `00063` | 345635 | 티일 냉각 밴드 압력 캐니스터 |
| `item-oil-flask` | v1 `00014` | 835705 | 제리캔 |
| `item-throwing-knife` | v2 `00046` | 599017 | 테이프 감은 육중한 투척 날 |
| `item-recall-scroll` | v1 `00019` | 292589 | 안테나 달린 귀환 비컨 |
| `item-coin-pouch` | v2 `00058` | 505965 | 입 벌린 자루 + 황동 토큰 |
| `item-gemstone` | v1 `00026` | 537886 | 발광 코어 큐브 |
| `item-relic` | v1 `00029` | 446640 | 시안 렌즈 미상 장치 |
| `item-herb` | v3 `00053` | 73621 | 구 채택: 창백한 버섯 군락 (2026-08-03 교체) |
| `item-blast-powder` | v1 `00045` | 121551 | 스텐실 찍힌 밀봉 화약통 |
| `item-frost-shard` | **교체 없음** | — | 구 채택 유지 판단 (2026-08-03 교체) |

신구 비교본은 `docs/captures/item-arcade-reskin-v1.png` — 12종을 쌍으로 세워 교체하지 않은
종은 같은 그림이 나란히 선다.

### 실측으로 배운 것 (다음 발주에 그대로 적용한다)

- **장소 구문은 오브젝트로 샌다.** subject에 넣은 "in a ruined arcade tower"가 첫 폭탄 후보를
  잔디 바닥의 미니어처 타워로 만들었다. 장소는 method가 배경으로만 쓰고, subject에는 쓰지
  않는다. 건축물 어휘는 공통 금지어로 내렸다.
- **용기 어휘는 주역을 뺏는다.** "opened tin sample can"에 담긴 균사는 세 후보 모두 드럼통이
  됐고, "steel clamp base"에 얹은 결정은 기계 받침이 주역이 됐다. 주체가 프레임을 차지한다고
  명시하고(`filling most of the frame`) 나머지는 크기를 못박는다(`far larger than`).
- **바닥 효과는 금지어 한 줄로 안 죽는다.** 냉각재 수류탄은 v1~v2에서 바닥 얼음판이 계속
  붙었다. `ice pool`·`frozen ground` 같은 구체 명사를 여러 개 넣고, 서리를 칠할 위치를
  **금속 껍데기 위로 한정**하고, "빈 공간에 홀로 선다"를 양성 프롬프트에 넣고서야 사라졌다.
- **반투명 묘사는 크로마키를 오브젝트 안으로 끌어들인다.** 프로세서는 테두리에 연결된 배경만
  지우므로, 결정을 비치게 그리면 내부에 마젠타가 남는다(`item-frost-shard` v4 실측).
- **기계 게이트는 계약 위반을 못 잡는다.** 가시 픽셀 수만 보는 게이트는 바닥 이펙트·분리 조각·
  극단 종횡비를 전부 통과시킨다. 선별에는 연결 성분 검사와 육안 콘택트 시트를 함께 썼다.

### 구 `item-frost-shard`를 남긴 이유 (2026-07-31 판단, 현재 폐기)

v1·v4·v5 세 리비전 15장을 뽑았지만 전부 현행보다 못 읽혔다 — 결정보다 기계 받침이 커지거나
(`v1/00051`, `v1/00057`), 마젠타가 새거나(`v4`), 불투명을 강조하자 결정 면이 뭉갰다(`v5`).
당시 스프라이트는 강철 칼라와 케이블이 달린 산업용 냉매 결정으로 읽힌다고 판단해
**더 나쁜 그림으로 교체하지 않는다**는 원칙에 따라 남겼다. 그러나 결정/조각 실루엣 자체가
초자연·판타지로 읽힌다는 2026-08-03 검토를 반영해, 현재는 구리 루프형 냉각 코일로 교체했다.

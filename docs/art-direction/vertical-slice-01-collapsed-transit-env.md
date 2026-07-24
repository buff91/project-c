# 버티컬 슬라이스 01 — Collapsed Transit 환경 세트 (제작 지시서)

> **목표**: `collapsed-transit`(무너진 환승역, 프로파일 A) **던전 한 방 화면**이 응집된 톤으로
> 렌더되는지 검증한다. 슬롯 전체를 갈지 않고 **환경 타일 1세트만** 완주해 톤을 확정한 뒤 확산한다
> (범위 원칙: GDD §3 · art-direction v1 §4). 액터·소품·아이템은 이 슬라이스에서 제외(후속).
>
> **레퍼런스**: `project-c-postapoc-ref-01·02·05`, `integrated-postapoc-gameplay-target-v2`,
> 기존 소스 `project-c-collapsed-transit-environment-source-v2`.
> **생성/마감**: `comfyui-to-aseprite-pipeline.md`. **규격 SSOT**: `asset-spec-sheet.md`.
> **팔레트**: `project-c-torchstone.gpl`(코어 + postapoc 재료색).

## 1. 왜 이 세트인가 (이미 배선돼 있음 = 검증 가능)

`Tools/ArtPipeline/process_postapoc_environment_v2.py`가 **이미 이 슬롯들을 출력**한다.
따라서 이 슬라이스는 가공의 목표가 아니라 **곧바로 Play로 검증되는 실범위**다.
할 일은 세 가지뿐: ① 소스 시트를 ComfyUI 통제형으로 재생성/정제,
② 프로세서 양자화를 `.gpl` 잠금으로 교체(파이프라인 문서 §3), ③ 검증.

## 2. 슬롯 표 (캔버스·피벗은 `asset-spec-sheet.md` = 코드 기준)

| 슬롯(파일명) | 캔버스 | 피벗(발 px) | 재료(프로파일 A) | 강조 팔레트 |
|--------------|--------|-------------|-------------------|-------------|
| `env-floor` | 64×32 | (0.5,0.5) 중앙 | 균열 콘크리트/아스팔트 플랫폼 바닥 | concrete/-dim, 미세 균열=inset, 얼룩=rust |
| `env-wall-rising-right/left` | 32×56 | (0.5,0.143) 8px | 손상 콘크리트 환승벽 패널 (씸·케이블 덕트·칩) | concrete/-dim, stone-lit(조명면), 소량 hazard |
| `env-wall-torch-rising-right/left` ★ | 32×56 | (0.5,0.143) 8px | 벽 **앰버 비상등**(작은 램프). `idle` 앰버 루프 | 벽=concrete, 광원=**torch/gold(앰버는 여기만)** |
| `env-door-closed-rising-right/left` | 64×80 | (0.5,0.2) 16px | 콘크리트/금속 프레임의 **중철 방폭문(닫힘)** + 표시등 | rust/-dark(강철), concrete(프레임), 표시등=torch |
| `env-door-open-rising-right/left` | 64×80 | (0.5,0.2) 16px | 같은 방폭문(열림), 내부 어둠 | 동일 + 개구부=void/inset |
| `env-stairs-rising-right/left` | 64×56 | (0.5,0.286) 16px | 마모 콘크리트 서비스 계단 + 강철 논싱 | concrete/-lit(디딤), rust(논싱) |
| `env-stairs-up-rising-right/left` | 64×56 | (0.5,0.286) 16px | 상행 계단(동일 재료) | 동일 |
| `env-stairs-down-rising-right/left` | 64×40 | (0.5,0.4) 16px | 하행 계단(별도 소스 fit) | 동일 |

> `-right`/`-left`는 프로세서가 **미러로 자동 생성**(현재 코드). 소스는 right 하나만 잘 뽑으면 된다.
> ★ 비상등 `idle` 루프는 애니이므로 **Aseprite 손작업**(파이프라인 문서 §4). 슬라이스 1차는
> 정적으로 톤만 확정하고, 루프는 톤 확정 후 붙여도 된다.

## 3. 톤 규칙 (레퍼런스에서 못 박은 것 — 어기면 "안 맞음"으로 회귀)

- **바탕은 청흑/차콜.** 웜 브라운 지배 금지(씬 온도 충돌).
- **탈색 저채도**가 기본: 콘크리트 회색 + 웜 토프 + 산화 강철 + **절제된** rust-orange.
- **고채도 앰버는 물리 광원(비상등·표시등)에만.** 벽·바닥을 앰버로 물들이지 않는다.
- **틸은 아주 옅은 서비스 스트라이프 한 줄** — 빛나는 마법 액센트로 쓰지 않는다.
- hazard(흑황)는 **소량 마킹**만. 바닥/벽 균열은 **드문드문**(과밀 디더링 금지).
- floor↔wall **명도 분리** 유지(가독성). 문/계단 논싱은 강철 rust로 재질 대비.

## 4. 제작 순서 (실행)

1. **생성**: `comfyui-to-aseprite-pipeline.md` §2로 6-셀 소스 시트를 img2img 스타일 트랜스퍼
   (기존 `-environment-source-v2` 레이아웃·실루엣 보존, denoise 0.45~0.65, LineArt ControlNet,
   IPAdapter=ref-01/02/05·target). 배경 평면 `#ff00ff` 유지. 계단은 별도 소스.
2. **마감(정적)**: 프로세서의 median-cut 32색을 **`.gpl` 고정 양자화로 교체**(파이프라인 §3) 후 실행
   → `Assets/_Project/Art/Environment/env-*.png` 갱신. 히어로 타일만 필요 시 Aseprite 손터치(§4).
3. **검증 게이트**:
   - `Project-C > Art > Aseprite > Validate Sources` 경고 0.
   - **Unity MCP Play 캡처(PC 가로)** — 한 방이 응집 톤으로 읽히는지, FOV 3상태에서 톤 유지,
     발/피벗이 타일에 앉는지, floor/wall 명도 분리.
   - 회귀: EditMode / PlayMode **둘 다 재실행**(스냅샷 673/1, 숫자 맹신 금지).

## 5. Definition of Done

- [ ] collapsed-transit **한 방 화면**이 target-v2 계열 톤으로 응집돼 렌더된다.
- [ ] 모든 env 스프라이트가 **동일 `.gpl` 인덱스**로 잠겨 UI 토큰과 같은 팔레트를 쓴다.
- [ ] 앰버는 비상등/표시등에만, 틸은 옅은 스트라이프에만 — 톤 규칙(§3) 위반 0.
- [ ] Validate Sources 경고 0 · Play 캡처 확인 · EditMode/PlayMode 회귀 통과.

## 6. 이 슬라이스에 없는 것 (후속 슬라이스로)

- **액터**(player/scavenger/sentry/ooze) — 애니 필요, Aseprite 손작업 슬라이스(별도).
- **소품**(연료 드럼·이상 균열·컨테이너) 및 **아이템 12종** — 정적, ComfyUI→§3로 후속.
- 몬스터/아이템 **표시 이름 리스킨**(문자열) — `postapoc-reskin-table-v1.md` §0-a (A) 경로, 로컬 코드 작업.

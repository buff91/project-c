# Project-C 아트 레시피·Slack 리뷰 자동화

> 목적: ComfyUI 후보 생성, Aseprite 마감, Slack 리뷰, Codex Scheduled 피드백 해석을
> 분리하면서 모든 결과를 재현 가능하게 만든다.

## 1. 구성

```text
styles/*.yaml + worlds/*.yaml + subjects/*.yaml + methods/*.yaml
     │
     ▼
art_runner.py ──────── ComfyUI REST :8188
     │                       │
     │                       ▼
     │                 후보 PNG
     ▼
art-review.sqlite3
     │
     ├──────── art_slack_bot.py ◀──── Slack Socket Mode
     │                 │                 버튼·이모지·스레드
     │                 ▼
     │            Aseprite CLI
     │
     ├──────── Codex Scheduled
     │            자연어 피드백 → 새 레시피 버전 → 새 job
     │
     └──────── apply_requests ───── Codex Spark Scheduled
                                  대상 분석 → 에셋 연결 → Unity 검증
```

- **화풍 YAML**: 어떻게 그리는지(픽셀 클러스터·에지·명도·렌더링 문법)의 SSOT.
- **세계관 YAML**: 어디에 속하는지(테마·재료·분위기·공통 배제)의 SSOT.
- **대상 YAML**: 무엇을 만드는지(슬롯·정체성·가이드·대상별 positive/negative)의 SSOT.
- **방법 YAML**: 어떻게 만드는지(모델·LoRA·워크플로·공통 프롬프트·생성값·승격 규칙)의 SSOT.
- **job recipe snapshot**: 화풍×세계관×대상×방법과 이번 입력을 합친 실제 실행값. 재현할 때는 원본 YAML이
  아니라 이 스냅샷을 본다.
- **배치 YAML**: 여러 용도의 레시피를 한 번에 넣는 수동/예약 실행 단위다.
- **SQLite**: batch run, job, 후보, 피드백, Spark 반영 요청과 Slack 연결을 영구 기록한다.
- **Slack**: 사람이 보는 리뷰 UI다. 기록의 SSOT는 아니다.
- **Codex Scheduled (Spark)**: 자연어 피드백과 승인된 후보의 실제 게임 반영만 처리한다.
- **Runner**: 승인·거절·변형·Aseprite 마감처럼 결정론적인 작업만 실행한다.

상태 DB와 토큰은 `Tools/ArtPipeline/.art-review/`에 있으며 git에서 제외된다. 생성 후보는
`docs/art-direction/comfyui/output/review/`에 저장되고 역시 git에서 제외된다.

## 2. 레시피

현재 시작 레시피:

- `actor-slinger-idle-v1`: SD1.5 img2img + OpenPose. 실제 게임용 96×128 액터.
- `actor-slinger-animation-walk-v1`: 방향 실험을 보존한 구 레시피. 현재 런타임에는 직접 쓰지 않는다.
- `actor-slinger-animation-v2`~`v4`: identity/pose 균형 실생성 기록을 보존한 실험 버전.
- `actor-slinger-animation-v5`: 현재 런타임 6태그, 동일 seed, 버전 고정 512 identity
  입력을 쓰는 키포즈 참고 시안 묶음. 최종 프레임으로 자동 승격하지 않는다.
- `actor-concept-sdxl-v1`: SDXL txt2img. 액터 콘셉트 탐색.
- `environment-hospital-style-v1`: SDXL img2img. 구 6셀 병원 환경 시트 스타일 트랜스퍼
  (DEPRECATED — 현행은 `environment-neon-style-v1`).
- `fx-impact-suite-v1`: 첫 실생성 비교를 보존한 원형/동심원 기준 레시피.
- `fx-impact-suite-v2`: UI 아이콘화를 억제하고 비대칭 파편·열린 상태 호를 강제한 6슬롯 묶음.

애니메이션·이펙트의 Aseprite 프레임/태그 마감 규칙은
`docs/art-direction/animation-effect-workflow.md`가 소유한다.

### 2-a. 에셋 타입 (`purpose.asset_type`)

레시피를 **고를 때** 쓰는 축이다. `category`(어느 슬롯 계열인가)와도, `use`(무슨 용도인가)와도
다르다 — 같은 `actor` 카테고리라도 콘셉트 탐색·런타임 스프라이트·애니 키포즈는 고르는 순간의
목적이 서로 다르기 때문이다. `/art recipes`와 생성 폼 드롭다운, `art_runner.py recipes`가
모두 이 순서로 묶어 보여준다.

| `asset_type` | 표시 | 무엇인가 |
|---|---|---|
| `concept` | 컨셉 | 방향 탐색용. 정식 슬롯으로 승격하지 않는다 |
| `environment` | 배경 | 환경 타일·소스시트 |
| `character` | 캐릭터 | 런타임에 바로 쓰는 액터 스프라이트 |
| `animation` | 애니메이션 | 상태별 키포즈 묶음. Aseprite 마감이 뒤따른다 |
| `effect` | 이펙트 | 전투 FX 키포즈 |
| `prop` | 소품·아이템 | 아이템·소품·마커 |
| `ui` | UI | UI 아이콘·프레임 |

`ASSET_TYPES`(`art_review.py`)가 목록과 순서의 SSOT다. 새 타입은 여기에 먼저 추가한다 —
레시피에 없는 타입을 쓰면 검증이 막는다. 필드를 빠뜨린 레시피는 `derive_asset_type()`이
`category`/`use`에서 파생하지만, **레시피는 명시하는 것이 규칙**이고 테스트가 이를 강제한다.

```bash
python3 Tools/ArtPipeline/art_runner.py recipes --asset-type animation
```

### 2-b. Unity 슬롯 (`purpose.slot`) — ID는 발급받는다

에셋 타입이 "무엇을 만드나"라면, 슬롯은 **"Unity 의 무엇을 채우나"**다. 같은 `캐릭터` 타입
안에서도 `actor-slinger`와 `actor-goblin`은 서로 다른 `IsoVisualCatalog` 필드를 채운다.

**슬롯 ID의 발급처는 `Assets/_Project/Editor/ArtPipeline/ProjectCAsepritePipeline.cs`의
`CatalogSlots`다.** 파이썬은 이 목록을 복제하지 않고 그대로 읽는다(`SlotCatalog`) — 복제하면
반드시 어긋난다. 여기 없는 슬롯 ID는 **존재하지 않는 것**이다.

```bash
python3 Tools/ArtPipeline/art_runner.py slots actor-       # 액터 슬롯과 Unity 필드
python3 Tools/ArtPipeline/art_runner.py slots --uncovered  # 아직 레시피가 없는 슬롯
```

#### 슬롯이 게임에서 무엇인지

`actor-slinger`만 보고는 그게 뭔지 알 수 없다. 몬스터 슬롯은 표시명과 한 줄 설명을
**`MonsterRoster`에서 읽어온다** — 파이프라인이 "투석 약탈자"를 다시 타이핑하면 게임과
갈리기 때문이다(`DungeonCatalog`가 보스 이름에 대해 지키는 규칙과 같다).

```
*대상*  캐릭터 · *투석 약탈자* · `actor-slinger`
*정체*  투석 약탈자(코드 ID Slinger): 유일한 원거리 교전 몬스터
```

이름을 아는 슬롯만 이름이 붙는다. `actor-player`·`env-floor`·`fx-*`처럼 `MonsterRoster`에
없는 슬롯은 **ID만 보여주고 설명을 지어내지 않는다.** 새 몬스터의 설명이 카드에 뜨게 하려면
`MonsterRoster`의 선언 **바로 위**에 `/// <summary>` 한 줄을 적으면 된다.

#### 승격하는 레시피는 등록된 슬롯만 겨눌 수 있다

`output.promotion`이 슬롯 요구를 가른다.

| `promotion` | 정식 슬롯에 쓰나 | 슬롯 등록 필요 |
|---|---|---|
| `aseprite` | ✓ `Art/Source/Aseprite/<slot>.aseprite` | **필수** |
| `animation-review-only` | ✗ 검수 초안까지 | 불필요 |
| `manual-processor` | ✗ 지정 processor 를 거친다 | 불필요 |
| `concept-only` | ✗ 방향 탐색 전용 | 불필요 |

슬롯 이름은 예전엔 정규식(`^(actor|env|item|marker|prop|fx)-...`)만 통과하면 됐다. 그래서
미등록 슬롯에 게시하면 `.aseprite` 파일이 생기고 **아무 일도 일어나지 않는데** 파이프라인은
"반영 완료"라고 말했다. 이제 레시피 검증과 게시 경로가 둘 다 발급 목록을 확인한다 — Spark 가
`--target-slot`으로 넘긴 값도 같은 관문을 지난다. 멀티샷은 샷이 슬롯을 갈아타므로 대표 슬롯이
아니라 **`target_slots` 전부**를 본다.

#### 새 캐릭터(또는 새 슬롯)를 추가하는 순서

1. `ProjectCAsepritePipeline.cs`의 `CatalogSlots`에 `{ "actor-<이름>", "<필드명>" }`을 더해
   **ID를 발급**한다. `IsoVisualCatalog`에 같은 이름의 Sprite 필드가 있어야 한다.
2. 필요하면 `ProjectCArtPivots.cs`에 피벗을 등록한다.
3. 그다음에 그 슬롯을 겨누는 레시피를 만든다. 순서를 뒤집으면 검증이 막는다 — **의도한 것이다.**
   Unity 가 읽을 자리가 없는데 그림부터 만들면, 승인·마감까지 다 하고 나서야 갈 곳이 없음을 안다.

#### 화풍·세계관·제작 대상을 추가하는 위치

세 축은 서로 섞지 않고 YAML 한 파일씩 추가한다. 기존 파일을 복사해 `id`와 계약만 바꾼 뒤
`python3 Tools/ArtPipeline/art_runner.py init`으로 모든 조합을 검증한다.

| 추가하려는 것 | 경로 | 그 파일이 소유하는 내용 |
|---|---|---|
| 화풍 | `comfyui/styles/<style-id>.yaml` | 픽셀 클러스터, 에지, 명도, 공통 positive/negative |
| 세계관 | `comfyui/worlds/<world-id>.yaml` | 재료·장소·분위기·세계관상 금지 요소 |
| 캐릭터/환경/VFX | `comfyui/subjects/<slot-id>.yaml` | Unity 슬롯, 정체성, 캔버스·피벗, 포즈/소스 가이드 |
| 여러 슬롯 묶음 | `comfyui/subject-sets/<set-id>.yaml` | 멤버 순서와 세트 목적 |
| 새 제작 단계 | `comfyui/methods/<method-id>.yaml` | ComfyUI 워크플로, 모델·LoRA, 생성값, 승인 소스 요구 |

현재 메인 캐릭터는 `subjects/actor-knight.yaml`이다. 이름은 원정자지만 직업·영웅 선택이
없으므로 컨셉 단계에서도 검·방패 같은 직업 장비를 정체성에 굽지 않는다. 기존 Game View에서
검증된 정적 원정자는 버전 고정 512 identity guide로 만들고
`character-runtime-base-v2 → 승인 → character-action-keyframes-v6`로 이어 간다.
신규 액터는 앞에 `concept-sdxl-v1 → 승인`을 붙인다. 기본 스프라이트와 액션 키프레임은
같은 승인 후보를 identity 입력으로 쓰며, 각 샷의 OpenPose만 달라진다. 해부/피벗 검사는
method, 마스크·팩·고정 무기 금지처럼 캐릭터 고유 검사는 subject가 소유하고 합성 시 병합된다.

정적 환경 대상은 mid/deep/boss 기본·raised 바닥, hole, weak-floor, ladder 9종이 등록돼 있다.
각 대상 YAML이 자신의 정확한 캔버스와 피벗을 소유한다. 환경 루프 대상은 campfire, portal,
좌·우 상승 벽 횃불 4종이며 `environment-idle-keyframes-v1`이 `pulse-low/rise/high/fall`
네 샷을 하나의 `idle` 루프로 인계한다.

### 2-c. 워크플로 타입 (`pipeline.type`)

`docs/art-direction/comfyui/workflow-types.yaml`이 목록과 **계약**을 소유한다. 타입은
"어떤 ComfyUI 워크플로 계열인가"를 말하고, 그 타입이 성립하려면 레시피가 무엇을 채워야
하는지를 함께 선언한다.

| `pipeline.type` | 표시 | 필수 업로드 | denoise | ControlNet |
|---|---|---|---|---|
| `sdxl-txt2img` | SDXL txt2img | 없음 | ✗ | ✗ |
| `sdxl-img2img` | SDXL img2img (스타일 트랜스퍼) | `7.image` | ✓ | ✗ |
| `sd15-img2img-openpose` | SD1.5 img2img + OpenPose | `5.image` · `6.image` | ✓ | ✓ |

`requires.bindings`에 적힌 논리 이름이 레시피 `pipeline.bindings`에 없으면 검증이 막는다 —
**타입 문자열만 맞고 바인딩이 비어 있으면 ComfyUI는 조용히 기본값으로 생성하고 seed
재현성이 무너지는데 아무도 모르기 때문이다.** `requires.uploads`는 레시피 전체
(`pipeline.uploads`)나 샷 하나(`shots[].uploads`) 어느 쪽에서 채워도 된다 — 포즈 가이드처럼
샷마다 다른 입력이 있다.

```bash
python3 Tools/ArtPipeline/art_runner.py workflow-types
python3 Tools/ArtPipeline/art_runner.py workflow-types sd15-img2img-openpose
```

새 워크플로 계열을 늘릴 때는 `.api.json`을 두고 이 파일에 타입을 먼저 추가한다. 레지스트리에
없는 타입을 레시피가 쓰면 `art_recipe_tool.py validate`와 `art_runner.py init`이 막는다.

### 2-d. 화풍 → 세계관 → 제작 대상 → 제작 방법 → 실행 내용

Slack 생성 폼은 고정 레시피 이름부터 고르지 않는다. 기본 화풍·세계관은 처음부터 선택돼
있으므로, 보통은 **제작 대상 → 이번 생성 내용 → 결과 다양성**만 고르면 된다.

1. **화풍**: `styles/*.yaml`에서 렌더링 문법을 고른다. 현재 기본값은
   `chunky-isometric-pixel-v1`이다.
2. **세계관**: `worlds/*.yaml`에서 테마와 재료 어휘를 고른다. 현재 기본값은
   `arcade-tower-v1`이다(구 `collapsed-hospital-v1`은 재현용 DEPRECATED).
3. **제작 대상**: `subjects/*.yaml` 또는 `subject-sets/*.yaml`에서 어떤 캐릭터·환경·VFX를
   만들지 고른다. 캐릭터는 `actor-slinger`(투석 약탈자)·`actor-grave-warden`(감시자)처럼
   실제 Unity 슬롯과 정체성을 함께 가진다. `컨셉`은 대상이 아니라 다음 단계의 제작 방법이다.
4. **제작 단계/방법**: `methods/*.yaml`에서 컨셉 탐색, 기본 스프라이트, 액션 키프레임,
   VFX 컨셉/정제처럼 그 대상에 맞는 방법만 고른다.
   아직 승인 가이드가 없는 감시자는 `SDXL 컨셉 탐색`만 보이고, 컨셉 승인 후 가이드를
   등록해야 기본 스프라이트·키프레임 단계가 열린다.
5. **캐릭터/대상 정의**: 대상 YAML의 기본 정체성과 판독 목표를 불러온다. 이번 job에서
   외형·역할을 바꾸고 싶으면 여기서 수정하며, 수정값은 positive와 job 메모에 함께 남는다.
6. **이번 생성 내용**: 이번 시안에서만 필요한 복장·동작·재질·방향을 입력한다. 이 문장은
   합성된 positive 뒤에 붙는다.
7. **실행값**: positive/negative 전체, checkpoint, base seed, Steps, CFG, denoise, 후보 수를
   확인하고 큐에 넣는다.

기본 폼은 처음 만드는 작업과 승인본 발전을 의도적으로 분리한다.

- `/art new`: 승인 소스 없이 바로 실행할 수 있는 **새 컨셉 탐색 방법만** 보여준다.
- 승인 후보 카드의 `➡️ 다음 단계 생성`: 같은 화풍·세계관·대상과 후보 ID를 자동으로 채우고
  **승인본 발전 방법만** 보여준다. 후보 ID를 복사해 입력하지 않는다.
- `고급 설정 열기`: 모델, 전체 positive/negative, seed, Steps, CFG, denoise를 직접 바꿀 때만
  연다. 기본 폼에서는 이 값들을 숨기되 job snapshot에는 전부 기록한다.
- `결과 다양성`: `빠르게 확인 / 균형 있게 / 넓게 탐색`으로 고른다. 단일 이미지는 각각
  2/4/6장이고, 멀티샷 키프레임은 한 후보가 전체 샷 세트이므로 1/1/2세트로 제한한다.

`style × world × subject × method` 합성 결과와 폼 조정값은 job의 `recipe_json`에 통째로 저장된다.
base seed는 비워도 job 생성 시 난수 하나가 배정되어 고정되고, 후보 `C01..`은 그 seed에서
순차 파생된다. 같은 그림을 다시 만들 때는 `/art job <job-id>`의 **후보 seed**를 새 폼의
base seed에 넣고 후보 수를 1로 둔다.

승인 후보 카드의 **다음 단계 생성**은 후보 ID를 폼에 넘긴다. img2img 제작 방법을 고르면
승인 시 보관한 `approvals/.../raw.png`가 실제 ComfyUI 입력 노드에 연결된다.

- 기존 메인 원정자: 현재 정적 identity guide → `character-runtime-base-v2` → 기본 스프라이트 승인 →
  `character-action-keyframes-v6` → Aseprite 애니 초안
- 신규 캐릭터: `concept-sdxl-v1 → 컨셉 후보 승인` 뒤에 위 기본/액션 단계를 연결
- VFX: `effect-concept-sdxl-v1 → effect-refine-img2img-v1 → Aseprite의 burst/idle-loop`
- 정적 환경 한 슬롯: `environment-concept-sdxl-v1 → 승인 →
  environment-static-refine-v1 → Aseprite/Unity`
- 기존 환경 시트: 소스시트 또는 승인 후보 → `environment-styletransfer-v1`
- 환경 루프: `environment-loop-concept-sdxl-v1 → 승인 →
  environment-idle-keyframes-v1 → Aseprite idle → Unity`

프롬프트만 복사하는 것은 계보가 아니다. 다음 단계는 반드시 승인 후보 ID를 가져야 하며,
`requires_source_candidate: true`인 방법은 승인 스냅샷 없이는 제출 자체가 막힌다.

Slack 없이 같은 작업을 넣는 예:

```bash
python3 Tools/ArtPipeline/art_runner.py compose-submit \
  actor-slinger character-idle-v1 \
  --style chunky-isometric-pixel-v1 \
  --world arcade-tower-v1 \
  --source-candidate ART-...-C01 \
  --target-definition "붉은 센서 눈과 짧은 슬링을 지닌 아케이드 타워 약탈자" \
  --positive-suffix "shorter sling, one compact oxygen tank" \
  --seed 1667020327 --steps 26 --cfg 6.0 --denoise 0.55 --count 2
```

경로는 `docs/art-direction/comfyui/recipes/`다. 모든 레시피는 다음 정보를 사람이 읽을 수
있게 기록한다.

- `purpose`: 카테고리·**에셋 타입**·정식 슬롯·게임/콘셉트/소스시트 용도·가독성 목표
- `output`: 최종 캔버스·피벗·크로마키·팔레트·승격 방식
- `pipeline`: ComfyUI API 워크플로·checkpoint·논리값→노드 입력 binding
- `loras`: 파일명·노드·base model·model/CLIP 강도·용도·출처
- `controlnets`: 모델·loader/apply 노드·강도·용도
- `generation`: 해상도·sampler·scheduler·steps·CFG·denoise·후보 수
- `prompt`: positive/negative 원문
- `quality_gates`: 사람이 확인할 실루엣과 자동 측정 기준
- `review`: Slack 채널 환경변수와 정식 승격 게이트

생성 모델이 seed마다 마젠타 색을 조금 바꾸므로 액터·효과 레시피는 `key_color: auto`를 쓴다.
테두리 중앙값으로 배경색을 찾는다. 액터는 `trim_detached: true`로 본체와 분리된 잔여
노이즈를 제거하지만, 이펙트는 분리된 파티클을 보존하려고 `false`로 둔다.
`quality_gates.color_area_limits`는 conform된 PNG의 투명 픽셀을 제외하고, 지정한 RGB 그룹이
차지하는 비율을 잰다. 예를 들어 메인 원정자는 teal 4%·warning 2% 상한을 두어 신호색 한 점은
허용하되 의상 전체가 청록/주황으로 드리프트한 후보는 자동 거절한다.

검사:

```bash
python3 Tools/ArtPipeline/art_recipe_tool.py validate
```

기존 레시피를 덮어쓰지 않고 새 버전 생성:

```bash
python3 Tools/ArtPipeline/art_recipe_tool.py clone \
  actor-slinger-idle-v1 actor-slinger-idle-v1-r2 \
  --name "투석 약탈자 — 짧은 슬링 r2" \
  --set generation.denoise=0.70 \
  --set generation.steps=28 \
  --set 'prompt.positive=full body hooded raider, shorter leather sling...'
```

## 3. 트리거·사용 가이드

Slack 버튼, Slack `/art` 명령, 로컬 CLI는 같은 SQLite 상태 DB와 작업 큐를 사용한다. 작업 중인 터미널이
없어도 백그라운드 서비스가 켜져 있으면 생성과 후처리를 자동으로 이어서 실행한다.

### 3-a. 생성과 조회

| 용도 | Slack | 로컬 CLI | 언제 쓰나 |
|---|---|---|---|
| 전체 도움말 | `/art help` | `art_runner.py --help` | 지원하는 명령 확인 |
| 빠른 생성 폼 | `/art new` 또는 전역 바로가기 **새 아트 생성** | `art_runner.py compose-submit <target> <method> --style <style> --world <world> ...` | 기본 화풍·세계관에서 대상→내용→다양성만 골라 새 컨셉 생성 |
| 승인본 다음 단계 | 후보 카드의 **다음 단계 생성** | `compose-submit ... --source-candidate <candidate-id>` | 대상·화풍·세계관·승인 ID를 자동 계승해 스프라이트·정제·키프레임 생성 |
| 레시피 목록 | `/art recipes` | `art_runner.py recipes [--asset-type <타입>]` | 에셋 타입별로 묶인 recipe ID 확인 |
| 레시피 상세 | `/art recipe <recipe-id>` | `art_runner.py recipes <recipe-id>` | 모델·LoRA·프롬프트·steps 확인 |
| 전체 세트 생성 | `/art run <recipe-id> [count]` | `art_runner.py submit <recipe-id> --count <n>` | 확정한 설정으로 후보 또는 멀티샷 세트 생성 |
| 한 샷 시험 | `/art shot <recipe-id> <shot-id> [count]` | `art_runner.py submit <recipe-id> --shot <shot-id> --count <n>` | 전체 세트를 만들기 전에 포즈·효과 한 장만 검증 |
| 배치 목록 | `/art batches` | `art_runner.py batches` | 등록된 다용도 배치 확인 |
| 배치 실행 | `/art batch style-sampler` | `art_runner.py batch-submit style-sampler` | 용도별 후보를 한꺼번에 큐에 등록 |
| 활성 큐 | `/art queue` | `art_runner.py queue` | 대기·실행·실패 job 확인 |
| 최근 작업 | `/art status` | `art_runner.py jobs` | 완료 항목을 포함한 최근 job 확인 |
| 대기 취소 | `/art cancel <job-id>` | `art_runner.py cancel <job-id>` | 아직 시작하지 않은 job만 취소 |
| 실패 재시도 | `/art retry <job-id>` | `art_runner.py retry <job-id>` | 실패한 job만 같은 설정으로 재큐잉 |
| 작업 상세 | 후보 카드의 스레드 첫 답글 (`/art job <job-id>`도 지원) | `art_runner.py job <job-id>` | 실제 positive/negative, 모델·LoRA, base/candidate seed, Steps·CFG·denoise 확인 |

#### 생성 폼의 이번 실행 조정

`/art new` 폼에서 대상과 제작 방법을 고르면 합성된 **현재 워크플로·모델(checkpoint)·긍정/제외
프롬프트·Steps·CFG·denoise가 채워진 채로** 나타나고, 그 자리에서 이번 실행만 고칠 수 있다.
`캐릭터/대상 정의`는 기본 subject 정체성 문장을 교체하고, `이번 생성 내용`은 positive 뒤에
추가된다. `메모`는 프롬프트에 들어가지 않는다. seed를 비우면 job 생성 시 난수로 고정된다.

조정값은 **이번 job 에만** 적용된다 — 레시피 YAML 은 그대로다. 대신 job 이 문서 전체를
`recipe_json` 으로 스냅샷하므로 조정본도 원본과 똑같이 재현 가능하고, 후보 카드에
`✏️ 이번 실행 조정  모델 · 긍정 프롬프트 · Steps · CFG` 로 무엇이 달라졌는지 표시된다. 결과가 좋아서
계속 쓸 설정이면 `art_recipe_tool.py clone` 으로 `-rN` 레시피를 만든다 — **조정만으로는
다음 실행에 남지 않는다.**

워크플로 타입을 바꾸면 워크플로 JSON 도 함께 바뀌므로, 레시피의 바인딩이 새 JSON 의 노드와
맞지 않으면 폼이 제출을 거부한다. 워커가 아니라 폼에서 막는다 — 6장 생성을 큐에 넣고 몇 분
기다린 뒤에 알 일이 아니다.

`count`는 후보 **세트 수**이며 1~12다. 멀티샷 레시피에서 `count 2`는 샷 두 장이 아니라
전체 샷 묶음 두 세트를 뜻한다. 비용이 큰 액터/이펙트는 반드시 한 샷 `count 1`로 먼저
검증한다.

기본 `style-sampler` 배치는 액터 콘셉트 1장, 실제 크기 액터 1장, 환경 1장, 이펙트 1장,
애니메이션 키포즈 1장을 만든다. 이펙트와 애니메이션은 매 실행마다 다음 shot으로 회전하므로
한 번에 비싼 전체 세트를 만들지 않는다. 나중에 Scheduled 생성 주기를 정해도 같은
`batch-submit style-sampler`를 호출한다.

### 3-b. 검수와 후처리

| 용도 | Slack 카드/명령 | 로컬 CLI | 결과 |
|---|---|---|---|
| 후보 채택 | **채택** 또는 `/art approve <candidate-id>` | `approve <candidate-id>` | 정식 승격 가능한 명시적 승인 기록 |
| 후보 거절 | **거절** 또는 `/art reject <candidate-id>` | `reject <candidate-id>` | 후보 전체 제외 |
| 전체 변형 | **변형 4장** 또는 `/art variation <candidate-id> [count]` | `variation <candidate-id> --count <n>` | 같은 레시피의 새 seed 배치 |
| 한 샷 채택 | **샷 채택** 또는 `/art shot-approve <candidate-id> <shot-id>` | `shot-approve <candidate-id> <shot-id>` | 멀티샷 중 해당 샷만 승인 |
| 한 샷 거절 | **샷 거절** 또는 `/art shot-reject <candidate-id> <shot-id>` | `shot-reject <candidate-id> <shot-id>` | 멀티샷 중 해당 샷만 거절 |
| 한 샷 변형 | **이 샷만 변형 2장** 또는 `/art shot-variation <candidate-id> <shot-id> [count]` | `shot-variation <candidate-id> <shot-id> --count <n>` | 해당 shot ID만 분리한 새 job |
| Aseprite 준비 | **Aseprite 마감/소스 세트** 또는 `/art prepare <candidate-id>` | `prepare <candidate-id>` | 크로마키·캔버스·팔레트 정리와 `.aseprite` 인계 |
| 애니 초안 | **애니 초안** 또는 `/art animation <candidate-id> [timing-scale]` | `animation <candidate-id> --timing-scale <n>` | 태그 타임라인과 검수 GIF 조립 |
| 게임 반영 요청 | **게임 반영 요청** 또는 `/art apply <candidate-id> confirm` | `apply-request <candidate-id>` | Spark가 실제 교체 대상을 분석하도록 큐에 등록 |
| 반영 상태 | `/art applies` | `apply-requests` | 분석·선택 필요·적용 완료 상태 확인 |

`timing-scale`은 0.5~2.0이며 1보다 작으면 빠르고 크면 느리다. Slack의 빠르게/기본
속도/느리게 버튼도 같은 작업이다. 채택 시 생성 원본·멀티샷·승인 메타데이터를
`approvals/APPROVAL-.../` 스냅샷으로 보관한다. 승인만으로 Unity 파일은 바뀌지 않는다. Slack의 `apply`는
실수 방지를 위해 마지막 `confirm`이 필수며, 실제 대상 선택과 반영은 Spark가 담당한다.
Spark가 `needs_input`을 남기면 후보 스레드에 답한다. 다음 Scheduled 실행이 답을 intent로
기록해 같은 요청을 다시 `queued`로 돌린다.

### 3-c. 자연어 피드백

- Slack: 후보 또는 샷 카드의 스레드에 `[shot-id] 수정 내용` 또는 `[walk] 수정 내용`으로
  답장한다. 이모지는 빠른 분류에 쓴다.
- 로컬:

```bash
python3 Tools/ArtPipeline/art_runner.py feedback \
  --candidate-id <candidate-id> \
  --label "shot:fx-impact-fire" \
  --text "오른쪽 위 파편을 늘려줘"
```

버튼과 명령의 승인·거절은 즉시 처리되는 결정론적 작업이다. 자연어 요청만 pending feedback으로
남고 Codex Scheduled가 다음 실행에서 레시피 수정 또는 재생성 여부를 해석한다. 스레드 답변을
받으면 봇이 즉시 `검토 대기`를 알리고, Scheduled가 가져갈 때 `확인 중`, 질문 답변이나 처리 판단이
끝나면 `검토 완료`와 실제 답변을 같은 스레드에 남긴다.

### 3-d. 권장 운영 순서

1. ComfyUI Desktop과 백그라운드 서비스를 켠다.
2. `/art recipes`로 recipe ID를 찾고 `/art recipe <id>`로 설정을 확인한다.
3. `/art shot <recipe-id> <shot-id> 1`로 가장 싼 단일 샷 시험을 실행한다.
4. 카드에서 샷을 평가하고 필요하면 해당 샷만 변형한다.
5. 설정이 읽힐 때만 `/art run <recipe-id> 1`로 전체 세트를 만든다.
6. `Aseprite 소스 세트` → `애니 초안` 순서로 만들고 Aseprite에서 인비트윈과 피벗을 마감한다.
7. 후보 전체를 채택해 스냅샷을 보관한다.
8. 실제 게임에 쓸 후보만 `게임 반영 요청`한다. Spark가 기존 에셋·카탈로그 참조를 조사해
   대상을 하나로 확정하거나, 모호하면 Slack 스레드에 선택지를 남긴다.

후보 ID는 카드 제목의 `ART-...-C01`, job ID는 `/art status`에서 찾는다. shot ID는 레시피
상세 카드, 샷 카드 제목 또는 CLI `recipes <recipe-id>`에서 확인한다.

## 4. 로컬 큐 상세

초기화 및 레시피 확인:

```bash
python3 Tools/ArtPipeline/art_runner.py init
python3 Tools/ArtPipeline/art_runner.py recipes
python3 Tools/ArtPipeline/art_runner.py recipes actor-slinger-idle-v1
```

생성 요청:

```bash
python3 Tools/ArtPipeline/art_runner.py submit \
  actor-slinger-idle-v1 --count 6 --notes "슬링 길이 비교"

python3 Tools/ArtPipeline/art_runner.py submit \
  actor-slinger-animation-v5 --shot idle --count 1

python3 Tools/ArtPipeline/art_runner.py batches
python3 Tools/ArtPipeline/art_runner.py batch-submit style-sampler
```

한 작업만 처리하거나 계속 감시:

```bash
python3 Tools/ArtPipeline/art_runner.py work --once
python3 Tools/ArtPipeline/art_runner.py work
```

상태:

```bash
python3 Tools/ArtPipeline/art_runner.py jobs
python3 Tools/ArtPipeline/art_runner.py queue
python3 Tools/ArtPipeline/art_runner.py batch-runs
python3 Tools/ArtPipeline/art_runner.py job ART-...
python3 Tools/ArtPipeline/art_runner.py feedback-context
```

대기 작업 취소와 실패 작업 재시도:

```bash
python3 Tools/ArtPipeline/art_runner.py cancel ART-...
python3 Tools/ArtPipeline/art_runner.py retry ART-...
```

`cancel`은 `queued` 상태에서만, `retry`는 `failed` 상태에서만 성공한다. 실행 중인 ComfyUI
요청을 강제로 끊지 않으므로 중간 파일과 DB가 어긋나지 않는다.

CLI 리뷰:

```bash
python3 Tools/ArtPipeline/art_runner.py approve ART-...-C01
python3 Tools/ArtPipeline/art_runner.py reject ART-...-C02
python3 Tools/ArtPipeline/art_runner.py variation ART-...-C01 --count 4
python3 Tools/ArtPipeline/art_runner.py shot-approve \
  ART-...-C01 walk-contact-a
python3 Tools/ArtPipeline/art_runner.py shot-reject \
  ART-...-C01 attack-release
python3 Tools/ArtPipeline/art_runner.py shot-variation \
  ART-...-C01 attack-release --count 2
python3 Tools/ArtPipeline/art_runner.py prepare ART-...-C01
python3 Tools/ArtPipeline/art_runner.py animation ART-...-C01 --timing-scale 1.0
python3 Tools/ArtPipeline/art_runner.py apply-request \
  ART-...-C01 --intent "투석 약탈자 런타임 교체"
python3 Tools/ArtPipeline/art_runner.py apply-requests
python3 Tools/ArtPipeline/art_runner.py work --once
```

백그라운드 서비스가 실행 중이면 마지막 `work --once`는 필요 없다. 서비스가 꺼진 복구·디버그
상황에서만 큐 입력 뒤 `work --once`를 실행한다. 별도의 장기 `work` 프로세스와 LaunchAgent를
동시에 운영하지 않는다.

`apply-request`는 승인/마감 후보에만 허용된다. Spark가 요청을 claim하고 실제 교체 대상이
확정된 뒤에만 내부 `publish --apply-request ... --target-slot ...`을 호출할 수 있다. 정식
`.aseprite`가 이미 있으면 레시피의 `output.allow_replace`가 `true`인 검토된 새 버전만
교체할 수 있다. 교체 전 원본은 해당 후보 출력 폴더에 백업된다.

## 5. Slack 앱 만들기

Slack Free의 Workflow Builder가 아니라 커스텀 앱 한 개를 사용한다.

1. Slack API의 **Create New App → From an app manifest**를 연다.
2. `Tools/ArtPipeline/slack-art-review-app-manifest.yaml` 내용을 붙여 넣는다.
3. 앱의 **Basic Information → App-Level Tokens**에서 `connections:write` 권한의
   `xapp-...` 토큰을 만든다.
4. **Install App**에서 워크스페이스에 설치하고 `xoxb-...` Bot Token을 복사한다.
5. **Basic Information → Display Information → App icon**에
   `Tools/ArtPipeline/assets/slack-art-forge-icon.png`를 업로드한다.
6. 리뷰 채널을 만들고 `/invite @project-c-art-forge`로 봇을 초대한다.
7. 채널 상세에서 Channel ID를 복사한다.

Socket Mode이므로 외부 공개 URL, 포트 포워딩, 터널이 필요 없다.

이미 앱을 설치했다면 앱 설정의 **App Manifest**에서
`Tools/ArtPipeline/slack-art-review-app-manifest.yaml`을 다시 적용하면 표시 이름이
`Project-C Art Forge` / `project-c-art-forge`로 갱신된다. 아이콘은 manifest가 아니라 위
Display Information에서 한 번 업로드한다. 이름·아이콘 변경에는 새 OAuth scope나 재설치가
필요하지 않다.

## 6. 토큰과 Python 환경

```bash
python3 -m venv .venv-art-review
.venv-art-review/bin/python -m pip install \
  -r Tools/ArtPipeline/requirements-art-review.txt

mkdir -p Tools/ArtPipeline/.art-review
cp Tools/ArtPipeline/art-review.env.example \
  Tools/ArtPipeline/.art-review/env
```

`Tools/ArtPipeline/.art-review/env`에 실제 값을 기록한다.

```dotenv
SLACK_APP_TOKEN=xapp-...
SLACK_BOT_TOKEN=xoxb-...
SLACK_ART_CHANNEL_ID=C...
SLACK_ART_ALLOWED_USERS=U...
COMFYUI_URL=http://127.0.0.1:8188
```

토큰 파일은 커밋하지 않는다. `SLACK_ART_ALLOWED_USERS`에는 정식 슬롯 승격 권한이 있는 사람만
쉼표로 구분해 넣는다. 이 변수는 필수다 — 비어 있으면 봇이 시작하지 않고, 목록에 없는
사용자의 상태 변경 요청(승인·준비·게시·생성)은 전부 거부된다.

## 7. Slack 리뷰 동작

서비스 실행:

```bash
Tools/ArtPipeline/run_art_review_service.sh
```

Slack 명령:

```text
/art help
/art new
/art recipes
/art recipe actor-slinger-idle-v1
/art job ART-...
/art shot actor-slinger-animation-v5 idle 1
/art run actor-slinger-idle-v1 6
/art status
/art batches
/art batch style-sampler
/art queue
/art cancel ART-...
/art retry ART-...

/art approve ART-...-C01
/art reject ART-...-C02
/art variation ART-...-C01 4
/art shot-approve ART-...-C01 walk-contact-a
/art shot-reject ART-...-C01 attack-release
/art shot-variation ART-...-C01 attack-release 2
/art prepare ART-...-C01
/art animation ART-...-C01 1.0
/art apply ART-...-C01 confirm
/art applies
```

메시지는 사람이 훑는 순서에 맞춰 구성한다.

- **제목**: 상태 아이콘 + `검토 대기/채택됨/준비 완료/반영 완료` + 자산 이름
- **본문**: 대상 종류·슬롯·후보 ID(묶음이면 `(2/3)`)와 지금 해야 할 한 가지 행동
- **하단 작은 글씨**: 작업 ID, recipe snapshot ID, steps, CFG, denoise 같은 재현 정보.
  전체 positive/negative와 seed 계보는 후보 스레드의 자동 `실행 상세`
  답글에서 바로 확인한다. `/art job <job-id>`는 과거 작업 재조회나
  채널 밖 디버깅용으로 계속 지원한다.
- **스레드**: 원본 이미지, 샷별 카드, Aseprite 미리보기, GIF, 오류와 후속 작업

자연어 답변의 상태도 같은 스레드에서 이어진다.

```text
👀 답변 확인 · Codex Agent 검토 대기
🔎 Codex Agent 확인 중
✅ Codex Agent 검토 완료 · 실제 답변/처리 결과
```

후보마다 독립된 채널 카드가 생기며 원본 이미지는 그 카드의 스레드에 업로드된다. **후보 카드가
곧 생성 완료 알림이다** — 앞에 별도의 요약 메시지를 세우지 않는다. 후보 1개짜리 job에서
같은 말이 두 번 나오고, 사람이 눌러야 할 버튼은 어차피 카드에만 있기 때문이다. 그래서 작업
ID와 묶음 내 위치는 카드가 직접 진다. 채널에는 후보 카드만 남기고 기술 로그와 수정 대화는
스레드에 모은다. 생성이 끝났는데 후보가 0개인 예외 상황에서만 경고 메시지 하나가 올라간다.

멀티샷 레시피는 후보 카드 아래에 샷별 원본과 카드도 함께 올린다. 각 샷 카드에는
**✅ 채택 / ❌ 제외 / 🔁 이 샷만 2개** 버튼이 있다. 마지막 버튼은 전체 묶음을 다시 만들지
않고 해당 `shot_id` 하나만 떼어 새 job으로 실행한다.

샷 카드에 이모지를 달면 `shot:<id>:<label>`로 기록된다. 일반 스레드 답글은 샷 ID를
대괄호로 붙인다.

```text
[fx-impact-fire] 완전한 링을 없애고 오른쪽 위로 튀는 불씨 4개만 남겨줘.
```

```text
[fx-status-burn] 캐릭터 발을 가리지 않도록 위쪽을 열어줘.
```

버튼:

- **✅ 채택**: 후보 상태를 승인으로 변경하고 원본·샷·승인 메타데이터 스냅샷을 보관
- **❌ 제외**: 후보 제외
- **🔁 비슷하게 4개**: 같은 레시피로 새 seed 배치 생성
- **🧹 Aseprite 준비/소스**: 크로마키·캔버스·Torchstone 팔레트 시험 결과 업로드
- **🎞 애니 초안**: 샷 세트를 `.aseprite` 타임라인으로 조립하고 8× GIF를 업로드
- **빠르게 / 기본 속도 / 느리게**: 프레임은 유지하고 duration만 재조립
- **🚀 게임 반영 요청**: 파일을 즉시 덮지 않고 Spark 큐에 등록. Spark가 교체 대상과 검증 경로를 결정

멀티샷 카드의 전체 **채택/거절/변형**은 세트 단위다. 특정 슬롯만 판단할 때는 반드시
스레드의 샷별 버튼을 사용한다.

이모지:

- 👍 스타일 적합
- 👎 거절 의견
- 🎨 팔레트/색 문제
- 🦴 해부·실루엣 문제
- 🧼 배경·클린업 문제
- 📐 크기·피벗 문제
- 🔁 이 방향으로 변형

후보 스레드의 일반 답글과 봇 멘션은 모두 피드백으로 기록된다.

```text
팔은 유지하고 슬링만 20% 짧게. 같은 방향으로 4장.
```

```text
@project-c-art-forge 붉은 포인트는 눈 한 곳에만 남겨줘.
```

Slack 문장은 직접 셸로 실행되지 않는다. SQLite의 pending feedback으로 들어가며 Codex
Scheduled가 다음 실행에서 해석한다.

`CERTIFICATE_VERIFY_FAILED`가 발생하면 의존성을 다시 설치한다.

```bash
.venv-art-review/bin/python -m pip install \
  -r Tools/ArtPipeline/requirements-art-review.txt
```

실행 스크립트는 `certifi` CA 번들을 `SSL_CERT_FILE`로 지정한다. 인증서 검증을 끄지 않는다.

## 8. 백그라운드 서비스

터미널을 닫아도 봇과 로컬 worker를 유지하려면:

```bash
Tools/ArtPipeline/install_art_review_service.sh install
Tools/ArtPipeline/install_art_review_service.sh status
```

중지:

```bash
Tools/ArtPipeline/install_art_review_service.sh uninstall
```

LaunchAgent는 Mac 로그인 후 봇을 실행한다. ComfyUI Desktop이 꺼져 있으면 생성 job은 실패
한다. LaunchAgent plist에는 설치 당시 저장소 절대 경로가 기록되므로 저장소를 옮기거나
다른 checkout으로 전환했다면 `uninstall` 후 새 위치에서 `install`을 다시 실행한다.
생성 실패 상태와 오류는 Slack에 남으며, Slack 리뷰 자체는 계속 동작한다.

worker가 죽어 `running`으로 남은 job/작업과 `sending`으로 남은 Slack 발송은 worker 재시작 시
자동으로 큐에 복구된다(기본 1시간 경과 기준, `PROJECTC_ART_STALE_RUNNING_SECONDS`로 조정).
Slack 발송은 최대 5회까지 재시도하고 그 뒤 실패로 기록된다.

## 9. Codex Scheduled

자동화 프롬프트 SSOT:

`Tools/ArtPipeline/codex-art-review-sweep.md`

기존 Codex 자동화 `Project-C 아트 리뷰·게임 반영`은 시간당 한 번 Spark low로 실행되며, 생성
스케줄이 아니라 **대기 중인 자연어 피드백과 게임 반영 요청만** 처리한다. 생성 배치 스케줄은
아직 활성화하지 않았다. 나중에 주기를 정하면 동일한 `batch-submit style-sampler` 명령을 별도
Scheduled 작업에서 호출한다.

Scheduled 실행은 다음 원칙을 따른다.

- pending 피드백이 없으면 아무것도 수정하지 않는다.
- 버튼 승인/거절은 다시 판단하지 않는다.
- 스레드 자연어만 이미지와 레시피를 함께 보고 해석한다.
- 기존 레시피를 덮어쓰지 않고 `-rN` 새 버전을 만든다.
- 명시적인 재생성 요구가 있을 때만 job을 만든다.
- 한 실행에서 Spark 반영 요청은 최대 한 건만 claim한다.
- 승인 스냅샷이 없으면 적용하지 않는다.
- 실제 Unity/Aseprite 참조가 하나로 확정될 때만 반영하고, 모호하면 `needs_input`으로 Slack에 묻는다.
- 대상·이유·검증 계획을 먼저 기록한 뒤 Aseprite 슬롯 또는 데이터 중심 카탈로그를 갱신한다.
- Unity MCP가 없으면 에디터 검증을 통과했다고 쓰지 않고 결과에 `pending`을 남긴다.

로컬 자동화는 Mac이 깨어 있고 Codex 앱과 ComfyUI가 실행 중일 때만 생성까지 이어진다. 자동화는
현재 자동화는 **ACTIVE**다. 빈 큐에서는 파일을 수정하지 않는다.

## 10. 운영 원칙

- Slack의 메시지 보존 기간과 무관하게 SQLite와 레시피 YAML이 기록을 소유한다.
- 모델/LoRA 파일을 교체하면 레시피도 새 버전으로 만든다.
- 후보 생성은 자동화해도 정식 슬롯 덮어쓰기는 사람의 승인과 별도 게임 반영 요청 없이는 하지 않는다.
- 액터 AI 생성은 상태별 키포즈에서 멈춘다. walk/attack의 인비트윈, 발 기준선,
  hit/fall/death의 실루엣과 최종 타이밍은 Aseprite 손작업이다.
- 환경 소스시트는 직접 Aseprite 슬롯에 게시하지 않고 지정 processor를 거친다.
- Slack 사용자 입력을 명령 문자열로 연결하거나 `shell=True`로 실행하지 않는다.

# Project-C 아트 레시피·Slack 리뷰 자동화

> 목적: ComfyUI 후보 생성, Aseprite 마감, Slack 리뷰, Codex Scheduled 피드백 해석을
> 분리하면서 모든 결과를 재현 가능하게 만든다.

## 1. 구성

```text
recipes/*.yaml
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
     └──────── Codex Scheduled
                  자연어 피드백 → 새 레시피 버전 → 새 job
```

- **레시피 YAML**: 모델·LoRA·워크플로·프롬프트·생성값·용도·승격 규칙의 SSOT.
- **SQLite**: job, 후보, 피드백, 버튼 작업, Slack 메시지 연결을 영구 기록한다.
- **Slack**: 사람이 보는 리뷰 UI다. 기록의 SSOT는 아니다.
- **Codex Scheduled**: 스레드 자연어처럼 판단이 필요한 피드백만 처리한다.
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
- `environment-hospital-style-v1`: SDXL img2img. 기존 6셀 병원 환경 시트 스타일 트랜스퍼.
- `fx-impact-suite-v1`: 첫 실생성 비교를 보존한 원형/동심원 기준 레시피.
- `fx-impact-suite-v2`: UI 아이콘화를 억제하고 비대칭 파편·열린 상태 호를 강제한 6슬롯 묶음.

애니메이션·이펙트의 Aseprite 프레임/태그 마감 규칙은
`docs/art-direction/animation-effect-workflow.md`가 소유한다.

경로는 `docs/art-direction/comfyui/recipes/`다. 모든 레시피는 다음 정보를 사람이 읽을 수
있게 기록한다.

- `purpose`: 카테고리·정식 슬롯·게임/콘셉트/소스시트 용도·가독성 목표
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
| 생성 폼 | `/art new` 또는 전역 바로가기 **새 아트 생성** | 해당 없음 | 레시피와 후보 수를 폼에서 선택 |
| 레시피 목록 | `/art recipes` | `art_runner.py recipes` | 사용할 recipe ID 확인 |
| 레시피 상세 | `/art recipe <recipe-id>` | `art_runner.py recipes <recipe-id>` | 모델·LoRA·프롬프트·steps 확인 |
| 전체 세트 생성 | `/art run <recipe-id> [count]` | `art_runner.py submit <recipe-id> --count <n>` | 확정한 설정으로 후보 또는 멀티샷 세트 생성 |
| 한 샷 시험 | `/art shot <recipe-id> <shot-id> [count]` | `art_runner.py submit <recipe-id> --shot <shot-id> --count <n>` | 전체 세트를 만들기 전에 포즈·효과 한 장만 검증 |
| 최근 작업 | `/art status` | `art_runner.py jobs` | job ID와 큐 상태 확인 |
| 작업 상세 | 카드와 스레드 | `art_runner.py job <job-id>` | candidate ID, seed, 출력 경로 확인 |

`count`는 후보 **세트 수**이며 1~12다. 멀티샷 레시피에서 `count 2`는 샷 두 장이 아니라
전체 샷 묶음 두 세트를 뜻한다. 비용이 큰 액터/이펙트는 반드시 한 샷 `count 1`로 먼저
검증한다.

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
| 정식 반영 | 확인창의 **Unity 반영** 또는 `/art publish <candidate-id> confirm` | `publish <candidate-id>` | 승인된 결과를 정식 Aseprite 슬롯에 저장 |

`timing-scale`은 0.5~2.0이며 1보다 작으면 빠르고 크면 느리다. Slack의 빠르게/기본
속도/느리게 버튼도 같은 작업이다. `publish`는 후보 전체의 명시적 승인과 레시피의 교체 허가를
모두 검사한다. Slack 명령은 실수 방지를 위해 마지막 `confirm`이 필수다.

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
남고 Codex Scheduled가 다음 실행에서 레시피 수정 또는 재생성 여부를 해석한다.

### 3-d. 권장 운영 순서

1. ComfyUI Desktop과 백그라운드 서비스를 켠다.
2. `/art recipes`로 recipe ID를 찾고 `/art recipe <id>`로 설정을 확인한다.
3. `/art shot <recipe-id> <shot-id> 1`로 가장 싼 단일 샷 시험을 실행한다.
4. 카드에서 샷을 평가하고 필요하면 해당 샷만 변형한다.
5. 설정이 읽힐 때만 `/art run <recipe-id> 1`로 전체 세트를 만든다.
6. `Aseprite 소스 세트` → `애니 초안` 순서로 만들고 Aseprite에서 인비트윈과 피벗을 마감한다.
7. 후보 전체를 채택한 뒤에만 `Unity 반영`으로 정식 슬롯에 게시한다.

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
```

한 작업만 처리하거나 계속 감시:

```bash
python3 Tools/ArtPipeline/art_runner.py work --once
python3 Tools/ArtPipeline/art_runner.py work
```

상태:

```bash
python3 Tools/ArtPipeline/art_runner.py jobs
python3 Tools/ArtPipeline/art_runner.py job ART-...
python3 Tools/ArtPipeline/art_runner.py feedback-context
```

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
python3 Tools/ArtPipeline/art_runner.py publish ART-...-C01
python3 Tools/ArtPipeline/art_runner.py work --once
```

백그라운드 서비스가 실행 중이면 마지막 `work --once`는 필요 없다. 서비스가 꺼진 복구·디버그
상황에서만 큐 입력 뒤 `work --once`를 실행한다. 별도의 장기 `work` 프로세스와 LaunchAgent를
동시에 운영하지 않는다.

`publish`는 승인/마감 후보에만 허용된다. 정식 `.aseprite`가 이미 있으면 레시피의
`output.allow_replace`가 `true`인 검토된 새 버전만 교체할 수 있다. 교체 전 원본은 해당 후보
출력 폴더에 백업된다.

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
/art shot actor-slinger-animation-v5 idle 1
/art run actor-slinger-idle-v1 6
/art status

/art approve ART-...-C01
/art reject ART-...-C02
/art variation ART-...-C01 4
/art shot-approve ART-...-C01 walk-contact-a
/art shot-reject ART-...-C01 attack-release
/art shot-variation ART-...-C01 attack-release 2
/art prepare ART-...-C01
/art animation ART-...-C01 1.0
/art publish ART-...-C01 confirm
```

메시지는 사람이 훑는 순서에 맞춰 구성한다.

- **제목**: 상태 아이콘 + `검토 대기/채택됨/준비 완료/반영 완료` + 자산 이름
- **본문**: 대상 종류·슬롯·후보 ID와 지금 해야 할 한 가지 행동
- **하단 작은 글씨**: recipe ID, steps, CFG, denoise 같은 재현 정보
- **스레드**: 원본 이미지, 샷별 카드, Aseprite 미리보기, GIF, 오류와 후속 작업

후보마다 독립된 채널 카드가 생기며 원본 이미지는 그 카드의 스레드에 업로드된다. 채널에는
완료 요약과 후보 카드만 남기고 기술 로그와 수정 대화는 스레드에 모은다.

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

- **✅ 채택**: 후보 상태를 승인으로 변경
- **❌ 제외**: 후보 제외
- **🔁 비슷하게 4개**: 같은 레시피로 새 seed 배치 생성
- **🧹 Aseprite 준비/소스**: 크로마키·캔버스·Torchstone 팔레트 시험 결과 업로드
- **🎞 애니 초안**: 샷 세트를 `.aseprite` 타임라인으로 조립하고 8× GIF를 업로드
- **빠르게 / 기본 속도 / 느리게**: 프레임은 유지하고 duration만 재조립
- **🚀 Unity 반영**: 확인창 후 정식 Aseprite 소스에 저장. 기존 슬롯 교체는 별도 레시피 허가 필요

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
상태와 오류를 Slack에 남기며, Slack 리뷰 자체는 계속 동작한다.

## 9. Codex Scheduled

자동화 프롬프트 SSOT:

`Tools/ArtPipeline/codex-art-review-sweep.md`

Scheduled 실행은 다음 원칙을 따른다.

- pending 피드백이 없으면 아무것도 수정하지 않는다.
- 버튼 승인/거절은 다시 판단하지 않는다.
- 스레드 자연어만 이미지와 레시피를 함께 보고 해석한다.
- 기존 레시피를 덮어쓰지 않고 `-rN` 새 버전을 만든다.
- 명시적인 재생성 요구가 있을 때만 job을 만든다.
- 정식 슬롯 교체와 Unity 반영은 자동으로 하지 않는다.

로컬 자동화는 Mac이 깨어 있고 Codex 앱과 ComfyUI가 실행 중일 때만 생성까지 이어진다. 자동화는
처음에는 **PAUSED** 상태로 만들어 토큰·채널 연결과 수동 배치 1회를 확인한 뒤 활성화한다.

## 10. 운영 원칙

- Slack의 메시지 보존 기간과 무관하게 SQLite와 레시피 YAML이 기록을 소유한다.
- 모델/LoRA 파일을 교체하면 레시피도 새 버전으로 만든다.
- 후보 생성은 자동화해도 정식 슬롯 덮어쓰기는 사람의 명시적 승인 없이는 하지 않는다.
- 액터 AI 생성은 상태별 키포즈에서 멈춘다. walk/attack의 인비트윈, 발 기준선,
  hit/fall/death의 실루엣과 최종 타이밍은 Aseprite 손작업이다.
- 환경 소스시트는 직접 Aseprite 슬롯에 게시하지 않고 지정 processor를 거친다.
- Slack 사용자 입력을 명령 문자열로 연결하거나 `shell=True`로 실행하지 않는다.

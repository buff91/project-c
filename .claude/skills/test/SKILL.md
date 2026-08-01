---
name: test
description: Project-C의 테스트를 올바른 경로로 실행하고 실패만 요약한다. C# Core shim · Python 회귀(ArtPipeline/Telemetry) · Unity 에디터 회귀 세 경로를 구분해서 돌리고, 못 돌린 경로를 분명히 말한다. 사용자가 "테스트 돌려줘", "회귀 확인", "통과하는지 봐줘"라고 하거나 코드 변경 후 검증이 필요할 때 사용한다.
---

# 테스트 실행

Project-C에는 검증 경로가 **세 개**다. 무엇을 건드렸는지에 따라 돌릴 것이 정해지고,
환경(Unity 에디터 유무)에 따라 돌릴 수 있는 것이 갈린다.
**셋을 뭉개서 "테스트 통과"라고 보고하지 않는다** — 무엇을 돌렸고 무엇을 못 돌렸는지 항상 밝힌다.

| 건드린 것 | 돌릴 것 |
|---|---|
| `Scripts/Core`, `Tests/EditMode` | 1단계(항상) |
| `Tools/ArtPipeline/**`, `Tools/Telemetry/**` (`.py`) | 1단계 + **2단계** |
| 씬·프리팹·스프라이트·UXML/USS·`Scripts/Gameplay` | 1단계 + **3단계**(가능하면) |

## 1단계 — 항상 먼저: Core 규칙 shim (Unity 불필요)

```bash
./Tools/CoreTests/run-core-tests.sh              # 전체
./Tools/CoreTests/run-core-tests.sh SightRules   # 이름 필터
```

- 없으면 .NET 8 SDK를 `$HOME/.dotnet`에 자동 설치한다(첫 실행만 수 분).
- 커버: `Scripts/Core` 규칙 + `Tests/EditMode` 중 UnityEngine 무의존 파일.
- **커버하지 않음**: 씬, 스프라이트/정렬, HUD·UI Toolkit 계약, PlayMode 흐름, `IsoGrid`.

## 2단계 — 파이썬을 건드렸으면: ArtPipeline · Telemetry 회귀

아트 후처리·발주·리포트 분석은 전부 파이썬이고, **여기엔 훅이 없다**(아래 「훅」 참조).
`Tools/` 아래 `.py`를 고쳤으면 이 단계를 사람이 직접 돌려야 한다.

```bash
COMFY_LEASE_CHUNK=0 python3 -m unittest discover -s Tools/ArtPipeline/tests -t Tools/ArtPipeline/tests
python3 -m unittest discover -s Tools/Telemetry/tests -t Tools/Telemetry/tests
```

- `-t`를 시작 디렉터리와 **같게** 준다. `tests/`에 `__init__.py`가 없어서
  `-t .`로 주면 `ImportError: Start directory is not importable`로 죽는다
  (테스트 파일이 각자 `sys.path`를 세우므로 이 형태로 충분하다).
- `COMFY_LEASE_CHUNK=0`은 GPU 리스를 끈다. 켜져 있으면 **실제 발주가 쥔 락에 걸려** 멈춘다.
- pytest는 이 환경에 없다. `unittest`로 돈다.
- ArtPipeline 쪽은 20초 넘게 걸린다(이미지 합성 실경로). 타임아웃을 넉넉히 준다.

## 3단계 — Unity MCP가 연결돼 있으면: 전체 회귀

MCP for Unity가 붙어 있는지 먼저 확인한다. 붙어 있으면:

1. EditMode `ProjectC.Tests.EditMode` 실행
2. PlayMode `ProjectC.Tests.PlayMode` 실행
3. `read_console`로 컴파일 에러/경고 확인

MCP가 없으면(웹·원격 세션이 대표적) **3단계는 불가능하다.** 그 경우 보고에
"에디터 회귀는 실행하지 못했다 — 로컬에서 확인 필요"를 반드시 포함한다.
숫자를 추측하거나 과거 기록(`docs/STATUS.md`의 수치)을 실행 결과처럼 인용하지 않는다.

## 훅 — 자동으로 걸리는 것과 안 걸리는 것

CI는 `release/**` 한정이라 작업 브랜치의 방어선은 로컬 훅뿐이다(`.claude/settings.json`).
**훅을 끄고 작업하지 않는다.** 훅이 막으면 우회하지 말고 원인을 고친다.

| 훅 | 시점 | 잡는 것 |
|---|---|---|
| `check-cs-edit.sh` | `.cs` 편집 직후 | `Scripts/Core`의 UnityEngine 의존(`IsoGrid.cs`만 예외) · `Assets` 아래 `.meta` 누락 · Unity 타입을 쓰는 EditMode 테스트의 shim 제외 목록 누락 |
| `check-comfy-workflow.sh` | `docs/art-direction/comfyui/**`의 `*.workflow.json`/`*.api.json` 편집 직후 | 캔버스↔API 쌍이 다른 그래프가 되는 것 |
| `verify-core-tests.sh` | 세션 종료 시(`.cs`가 수정돼 있을 때만) | Core 테스트가 실패한 상태로 세션이 끝나는 것 |

대처:

- **`.meta` 누락** — 에디터 없는 세션에서는 생성되지 않는다. 만들어 낼 수 없으면
  커밋 전에 에디터를 한 번 열어야 한다고 **사용자에게 알린다.**
- **Core 순수성** — UnityEngine이 꼭 필요하면 그 로직은 `Scripts/Gameplay`에 속한다.
  `csproj`의 `Compile Remove`로 빼는 건 shim 커버리지를 깎는 마지막 수단이다.
- **워크플로 쌍 어긋남** — API JSON을 손으로 고치지 말고 ComfyUI 캔버스에서 고쳐
  Save/Export (API Format)으로 다시 내보낸다. 캔버스에서 직접 고친 뒤 Export를 잊은 경우는
  훅이 못 잡으므로 전체 스윕: `python3 Tools/ArtPipeline/comfy_batch.py validate`
- **`Stop` 훅은 `.cs`만 본다.** 파이썬·UXML·에셋만 고친 세션은 아무것도 검증되지 않은 채
  끝날 수 있다 — 2단계를 스스로 돌린다.

## 실패를 다룰 때

1. **실패 출력을 그대로 인용한다** — "몇 개 실패"만 쓰지 않는다.
2. 원인을 **코드 버그 / 낡은 테스트** 중 어느 쪽인지 판정한다.
   낡은 테스트로 판정했다면 근거를 `GDD.md` · `docs/SYSTEMS.md` · `CLAUDE.md`에서 인용한다.
   설계 SSOT가 코드와 테스트 중 어느 쪽을 지지하는지가 판단 기준이다.
3. **SSOT가 애매하면 고치지 말고 사용자에게 묻는다.** 테스트를 통과시키려고
   게임 규칙을 조용히 바꾸는 것이 가장 나쁜 실패다.
4. 시드 기반 생성 테스트(`ProceduralDungeonTests`, `SecretRoomRulesTests` 등)가 실패하면
   도달성 검사의 전제를 확인한다 — **문을 다 연 상태**로 경로를 찾는 것이 이 리포의 관례다
   (`GridPathfinder.FindPath`는 기본적으로 닫힌 문을 막는다).
5. 밸런스 수치를 바꿔서 테스트가 깨진 것이라면 값의 근거를 먼저 본다 — `playtest` 스킬이
   그 수치의 출처(실플레이 리포트)를 다룬다. 리포트 없이 값을 흔들지 않는다.

## 보고 형식

돌린 경로만 쓴다. 실행하지 않은 줄은 **적지 않거나** "실행 못 함"을 이유와 함께 쓴다.
수치는 **그 실행에서 실제로 나온 값**을 옮긴다 — 이 문서의 예시나 `docs/STATUS.md`의
기록을 베끼지 않는다.

```
Core shim: <통과>/<전체> 통과
Python 회귀: ArtPipeline <통과>/<전체> · Telemetry <통과>/<전체>
에디터 회귀: 실행 못 함 (MCP 미연결) — 로컬 확인 필요
```

실패가 있으면 그 아래에 실패 테스트명 + 실제 출력 + 원인 판정을 붙인다.

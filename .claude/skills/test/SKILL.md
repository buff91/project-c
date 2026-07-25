---
name: test
description: Project-C의 테스트를 올바른 경로로 실행하고 실패만 요약한다. Unity 에디터가 있으면 EditMode/PlayMode 전체 회귀까지, 없으면 dotnet Core shim만 돌리고 그 한계를 분명히 말한다. 사용자가 "테스트 돌려줘", "회귀 확인", "통과하는지 봐줘"라고 하거나 코드 변경 후 검증이 필요할 때 사용한다.
---

# 테스트 실행

Project-C에는 검증 경로가 **두 개**이고, 환경에 따라 쓸 수 있는 것이 다르다.
**둘을 뭉개서 "테스트 통과"라고 보고하지 않는다** — 무엇을 돌렸고 무엇을 못 돌렸는지 항상 밝힌다.

## 1단계 — 항상 먼저: Core 규칙 shim (Unity 불필요)

```bash
./Tools/CoreTests/run-core-tests.sh              # 전체
./Tools/CoreTests/run-core-tests.sh SightRules   # 이름 필터
```

- 없으면 .NET 8 SDK를 `$HOME/.dotnet`에 자동 설치한다(첫 실행만 수 분).
- 커버: `Scripts/Core` 규칙 + `Tests/EditMode` 중 UnityEngine 무의존 파일.
- **커버하지 않음**: 씬, 스프라이트/정렬, HUD·UI Toolkit 계약, PlayMode 흐름, `IsoGrid`.

## 2단계 — Unity MCP가 연결돼 있으면: 전체 회귀

MCP for Unity가 붙어 있는지 먼저 확인한다. 붙어 있으면:

1. EditMode `ProjectC.Tests.EditMode` 실행
2. PlayMode `ProjectC.Tests.PlayMode` 실행
3. `read_console`로 컴파일 에러/경고 확인

MCP가 없으면(웹·원격 세션이 대표적) **2단계는 불가능하다.** 그 경우 보고에
"에디터 회귀는 실행하지 못했다 — 로컬에서 확인 필요"를 반드시 포함한다.
숫자를 추측하거나 과거 기록(`docs/STATUS.md`의 수치)을 실행 결과처럼 인용하지 않는다.

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

## 보고 형식

```
Core shim: 735/735 통과
에디터 회귀: 실행 못 함 (MCP 미연결) — 로컬 확인 필요
```

실패가 있으면 그 아래에 실패 테스트명 + 실제 출력 + 원인 판정을 붙인다.

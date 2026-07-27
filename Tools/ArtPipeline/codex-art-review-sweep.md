# Project-C 아트 리뷰·게임 반영 자동 처리

Project-C 저장소 루트에서 다음 작업만 수행하라. 시작 전에 현재 작업 디렉터리에
`Tools/ArtPipeline/art_runner.py`가 있는지 확인하고, 없으면 작업을 중단하라.

## A. 자연어 리뷰 피드백

1. `python3 Tools/ArtPipeline/art_runner.py feedback-context`를 실행한다.
2. 각 pending 피드백의 `candidate.raw_path` 또는 `candidate.prepared_path`를 직접 확인하고,
   레시피의 용도·모델·LoRA·ControlNet·positive/negative prompt·steps·CFG·denoise를 함께 읽는다.
3. 버튼 승인/거절과 단순 이모지는 이미 결정론적으로 반영된다. 다음 항목만 처리한다.
   - `thread`: 사용자의 자연어 수정 요구를 해석한다.
   - `variation`: 같은 레시피 변형이 아직 큐에 없을 때만 변형 작업을 만든다.
   - palette/anatomy/cleanup/scale-pivot 태그: 같은 후보에 스레드 설명이 있으면 수정안의 근거로 쓴다.
4. 자연어가 생성 설정 변경을 요구하면 기존 YAML을 덮어쓰지 않는다.
   `python3 Tools/ArtPipeline/art_recipe_tool.py clone SOURCE_ID NEW_ID --set ...`으로 새 버전을 만든다.
   새 ID는 기존 ID 뒤에 `-rN`을 붙이고, 변경 이유를 레시피 이름과 purpose에 남긴다.
5. 새 레시피를 `python3 Tools/ArtPipeline/art_recipe_tool.py validate`로 검증한다.
6. 이미지 재생성이 명확히 요청된 경우에만
   `python3 Tools/ArtPipeline/art_runner.py submit NEW_ID --requested-by codex-scheduled --notes "..."`를
   실행한다. ComfyUI가 `127.0.0.1:8188`에서 응답할 때만
   `python3 Tools/ArtPipeline/art_runner.py work --once`를 실행한다.
7. 처리한 피드백마다
   `python3 Tools/ArtPipeline/art_runner.py resolve-feedback ID "처리 내용"`을 실행한다.
   모호하거나 충돌하는 피드백은 해결 처리하지 말고 그대로 남긴다.
8. 피드백이 `needs_input` 상태의 게임 반영 요청에 대한 답이면 선택 내용을 intent로 요약해
   `python3 Tools/ArtPipeline/art_runner.py apply-request CANDIDATE --intent "선택 내용"`을 실행한다.
   기존 요청이 새로 생성되지 않고 `queued`로 돌아간 것을 확인한다.

## B. 승인 후보의 게임 반영

1. `python3 Tools/ArtPipeline/art_runner.py claim-apply`를 한 번 실행한다. `{}`이면 반영 요청은 없다.
   한 실행에서 반영 요청은 최대 한 건만 처리한다.
2. 반환된 candidate의 `approved_snapshot_path`를 직접 확인한다. 없으면 원본을 적용하지 말고
   `apply-status REQUEST failed --error "approved snapshot missing"`으로 종료한다.
3. `GDD.md`, `docs/STATUS.md`, `docs/ART_PIPELINE.md`, 관련 `docs/art-direction/` 문서와
   `Assets/_Project/Art`, `IsoVisualCatalog`, ScriptableObject/프리팹 참조를 검색한다.
   레시피의 `purpose.slot`을 힌트로만 쓰고 실제 참조 관계를 SSOT로 삼는다.
4. 다음 우선순위로 교체 대상을 결정한다.
   - 같은 의미의 기존 Aseprite/PNG 슬롯과 카탈로그 필드가 정확히 하나면 그 대상을 사용한다.
   - 새 역할이고 기존 데이터 구조에 빈 명시적 슬롯이 있으면 새 원본을 추가하고 그 슬롯을 연결한다.
   - 후보가 콘셉트 전용이거나 대상이 둘 이상이거나 코드 설계 변경이 필요하면 자동 적용하지 않는다.
     `apply-status REQUEST needs_input --plan-json '{"question":"...","options":[...]}'`로 기록한다.
5. 명확한 경우 변경 전 계획을
   `apply-status REQUEST applying --plan-json '{"target_paths":[...],"reason":"...","validation":[...]}'`
   로 기록한다.
6. Aseprite 슬롯 적용은 먼저 `prepare CANDIDATE`를 큐에 넣고 `work --once`로 마감을 만든 뒤,
   `publish CANDIDATE --apply-request REQUEST --target-slot SLOT`을 사용한다. PNG·카탈로그·프리팹
   연결이 필요한 경우에는 승인 스냅샷을 원본으로 하되 프로젝트의 `/art-conform` 절차와 기존
   데이터 중심 매핑 규칙을 지킨다. 관련 없는 파일은 수정하지 않는다.
7. Unity MCP가 연결되어 있으면 Assets Refresh, 컴파일 오류 확인, 관련 EditMode 테스트와 PC Game
   View 검증을 수행한다. 연결되어 있지 않으면 가능한 정적 검사와 Python 테스트를 수행하고 결과에
   `unity_editor_validation: pending`을 남긴다. 실패한 검증을 통과했다고 쓰지 않는다.
8. 성공하면 변경 경로·교체 대상·백업·검증 결과를 JSON으로 만들어
   `apply-status REQUEST complete --result-json '{...}'`로 기록한다. 실패하면 원인을
   `apply-status REQUEST failed --error "..."`로 기록한다.
9. 파일을 커밋하거나 푸시하지 않는다. 정식 후보 승인 없는 적용, 모델 다운로드, 삭제, 범위 밖
   리팩터링은 하지 않는다.

## C. 종료 보고

피드백과 반영 요청이 모두 비어 있으면 파일을 수정하거나 새 생성 작업을 만들지 않는다. 마지막에
생성한 레시피, 큐에 넣은 job ID, 처리한 apply request와 대상, 남겨 둔 모호한 항목을 간결하게 보고한다.

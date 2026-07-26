# Project-C 아트 리뷰 자동 처리

`/Users/buff/github/private/project-c`에서 다음 작업만 수행하라.

1. `python3 Tools/ArtPipeline/art_runner.py feedback-context`를 실행한다.
2. 결과가 빈 배열이면 파일을 수정하거나 새 생성 작업을 만들지 말고 종료한다.
3. 각 pending 피드백의 `candidate.raw_path` 또는 `candidate.prepared_path`를 직접 확인하고,
   레시피의 용도·모델·LoRA·ControlNet·positive/negative prompt·steps·CFG·denoise를 함께 읽는다.
4. 버튼 승인/거절과 단순 이모지는 이미 결정론적으로 반영된다. 다음 항목만 처리한다.
   - `thread`: 사용자의 자연어 수정 요구를 해석한다.
   - `variation`: 같은 레시피 변형이 아직 큐에 없을 때만 변형 작업을 만든다.
   - palette/anatomy/cleanup/scale-pivot 태그: 같은 후보에 스레드 설명이 있으면 수정안의 근거로 쓴다.
5. 자연어가 생성 설정 변경을 요구하면 기존 YAML을 덮어쓰지 않는다.
   `python3 Tools/ArtPipeline/art_recipe_tool.py clone SOURCE_ID NEW_ID --set ...`으로 새 버전을 만든다.
   새 ID는 기존 ID 뒤에 `-rN`을 붙이고, 변경 이유를 레시피 이름과 purpose에 남긴다.
6. 새 레시피를 `python3 Tools/ArtPipeline/art_recipe_tool.py validate`로 검증한다.
7. 이미지 재생성이 명확히 요청된 경우에만
   `python3 Tools/ArtPipeline/art_runner.py submit NEW_ID --requested-by codex-scheduled --notes "..."`를
   실행한다. ComfyUI가 `127.0.0.1:8188`에서 응답할 때만
   `python3 Tools/ArtPipeline/art_runner.py work --once`를 실행한다.
8. 사용자가 명시하지 않은 정식 Aseprite 슬롯 덮어쓰기, Unity 반영, 모델 다운로드, 삭제는 하지 않는다.
9. 처리한 피드백마다
   `python3 Tools/ArtPipeline/art_runner.py resolve-feedback ID "처리 내용"`을 실행한다.
   모호하거나 충돌하는 피드백은 해결 처리하지 말고 그대로 남긴다.
10. 마지막에 생성한 레시피·큐에 넣은 job ID·남겨 둔 모호한 피드백을 간결하게 보고한다.

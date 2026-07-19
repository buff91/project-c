# M0 셋업 노트 (씬 연결)

M0 로직/스크립트는 작성 완료. Unity 프로젝트를 열면 아래 순서로 씬에 붙이면 바로 확인 가능.

## 폴더 구조
```
Assets/_Project/
  Scripts/Core/       ← 순수 로직 (Unity 무관, 테스트 대상)
    GridPos.cs          다층 격자 좌표 (x, y, elevation)
    TileData.cs         타일 종류 + 통행/바닥/시야 판정
    GridMap.cs          희소 격자 저장소 + 낙하 착지 탐색
    IsoGrid.cs          아이소 변환(정/역) + 정렬 규칙
  Scripts/Gameplay/   ← Unity 연결부 (MonoBehaviour)
    GridManager.cs      데이터+변환 소유, 데모 층 생성
    IsoGridGizmo.cs     에셋 없이 Scene 뷰에서 격자 확인
    IsoTapInput.cs      탭/클릭 → 격자 역변환 로그
    GridSortingObject.cs 실제 스프라이트 위치+정렬
  Tests/EditMode/     ← NUnit 테스트 (변환 왕복/정렬/낙하)
```

## 씬 연결 (2분)
1. 새 씬 생성. 빈 GameObject 하나 만들고 이름 `Grid`.
2. `Grid` 에 **GridManager** 컴포넌트 추가 → 자동으로 **IsoGridGizmo** 도 붙음(RequireComponent).
3. **IsoTapInput** 컴포넌트도 `Grid` 에 추가.
4. Main Camera 를 **Orthographic** 로. (Projection = Orthographic, Size 5 정도)
5. Scene 뷰에서 바로 다이아몬드 격자가 보임 (파랑=바닥, 노랑=높은 바닥, 초록=계단, 빨강=구멍).
6. Play → Console 에 데모 층 생성 로그. 화면 클릭하면 `[Tap] ... → 격자 (x,y,e0)` 로그로 역변환 확인.

## 테스트 실행
Window > General > **Test Runner** > EditMode > Run All.
(변환 정/역 왕복, 정렬 순서, 낙하 착지 탐색 검증)

## 다음 (M1 준비)
- 프로토타입 A* 이동을 실제 Actor/Turn 상태에 연결
- 이동 불가·적·오브젝트 탭의 문맥 액션 분기
- 턴 매니저 + 기본 전투

## 아이소메트릭 프로토타입 룸

`Project-C > Build Isometric Prototype` 메뉴를 실행하면 다음을 자동 생성한다.

- `Assets/_Project/Scenes/IsoPrototype.unity`
- 64×32 런타임 픽셀 타일로 만든 11×11 테스트 층
- 각 층의 방 3개, 1칸 복도 2개, 닫힌 문 2개
- 한 단계 높은 테라스와 계단
- 구멍, 약한 바닥과 구멍 아래 하층 일부
- 플레이어, 고블린, 폭발 배럴
- 탭한 타일까지 A* 4방향 이동
- 화면 우상단 UI Toolkit 회전 HUD (`VIEW 1/4`)
- HUD 층 표시 (`▲ B1 [B2] ▼ B3`)
- HUD `MODE: PLAY FOV / MODE: DEBUG ALL` 버튼으로 표시 모드 전환
- HUD `ATTACK: MELEE / RANGED` 버튼으로 자동 접근 근접과 사거리 6 원거리를 전환
- HUD 현재 위치는 `던전 층 · 층 내부 높이 · 격자 좌표`로 표시하고, 월드에는 청록 발판과 머리 위 화살표를 유지
- PLAY: 닫힌 문 뒤와 미발견 방 숨김, 발견한 Hole/계단 주변 인접층만 표시
- `VERTICAL ROUTES` HUD에 현재 보이는 계단/Hole의 목적지 층과 탭 행동 표시
- DEBUG: B1/B2/B3 전체를 가까운 간격으로 표시해 생성·정렬 검사
- `Q`/`E` 또는 HUD 좌우 버튼으로 4방향 아이소 시점 회전
- 시점에 따라 후면 두 변의 석벽·횃불을 다시 만들고, 타일/액터 정렬과 탭 역변환을 갱신
- `docs/art-direction/iso-prototype-room.png` 세로 캡처

씬을 연 뒤 Play하고 이동 가능한 타일을 클릭한다. 기본은 PLAY FOV다. 닫힌 문을 탭하면 플레이어가 인접 칸까지 이동한 뒤 문을 열고, 열린 문을 다시 탭하면 닫는다. 문은 통로와 시점 방향에 맞는 아이소 사선 평면으로 표시된다. 두 행동 모두 한 턴을 소비하고 문 너머 시야를 즉시 다시 계산한다. `ATTACK` 버튼에서 근접/원거리를 바꾼 뒤 적을 탭한다. 원거리는 같은 높이에서 사거리와 시야선이 확보돼야 하며 닫힌 문과 벽에 막힌다. 발견한 Hole/계단에는 색이 다른 시작·도착 발판과 샤프트가 나타나고, 하단 `VERTICAL ROUTES`가 목적지 층을 알려준다. 주황 계단을 탭하면 연결층으로 이동하고, 청록 Hole을 탭하면 적이 없는 인접 칸으로 접근한 뒤 아래층에 착지한다. 전체 구조가 필요할 때만 HUD의 `MODE: PLAY FOV` 버튼을 눌러 DEBUG ALL로 전환한다. 높은 테라스에는 계단을 통해서만 올라가며, 외곽 벽에 가린 부분은 `Q`/`E`로 돌려 확인한다. 런타임 타일은 규격·팔레트·레이어 검증용이므로 최종 아트는 `docs/ART_PIPELINE.md`의 절차로 교체한다.

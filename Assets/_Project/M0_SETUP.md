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
- GridManager 의 `OnTileTapped` 훅에 이동 라우팅 연결
- A* 경로탐색 (GridMap.IsWalkable 이미 준비됨)
- 턴 매니저 + 기본 전투

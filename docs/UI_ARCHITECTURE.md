# UI 아키텍처 & Claude 디자인 워크플로

> UI를 **UI Toolkit**과 **UGUI**로 이원화한다. Claude가 HTML/CSS 디자인 시안을 뽑고, 그중 화면공간 평면 UI는 UI Toolkit(UXML/USS)로 이식한다.
> 상세 근거는 `GDD.md` §4.2·§12, `docs/SYSTEMS.md` §12 참조. 이 문서는 UI 판단의 SSOT.
> **시각 언어(색·타이포·컴포넌트)는 `docs/UI_DESIGN_SYSTEM.md` + `Assets/_Project/UI/DesignSystem.uss`가 SSOT.**

## 한 줄 판단 기준

> **월드 좌표를 따라다니거나 엔티티/타일에 앵커되면 → UGUI. 화면공간 평면 패널이면 → UI Toolkit.**

## 분담 규칙

| 구분 | UI Toolkit | UGUI |
|------|-----------|------|
| 성격 | 화면공간 평면 HUD·메뉴·패널 | 월드 앵커·엔티티/타일 추종 |
| 데이터 | 데이터 바인딩 유리 | 개별 인스턴스 추종 |
| 애니 | USS 트랜지션 | DOTween UGUI 모듈(`DOTweenModuleUI.cs`) |
| 이식 | Claude HTML 시안 → UXML/USS 거의 1:1 | 시안은 참고용, 씬에서 직접 배치 |

### 화면별 배치

| 화면 | 시스템 | 비고 |
|------|--------|------|
| 인게임 HUD (HP/자원/턴/미니맵/층·높이) | **UI Toolkit** | 상시 오버레이 |
| 인벤토리 + 조합 | **UI Toolkit** | 아이템 그리드·레시피 |
| 메인메뉴 / 설정 | **UI Toolkit** | |
| 메타 프로그레션 해금 | **UI Toolkit** | 레시피/시작장비/직업 |
| 결과 / 게임오버 | **UI Toolkit** | |
| 오브젝트 상호작용 팝업 (밀기/부수기/열기/줍기) | **UGUI** | 탭한 오브젝트에 앵커 (GDD §4.2) |
| 조준·타겟·높이 마커 | **UGUI** | 타일/월드 좌표 추종 |
| 플로팅 데미지 숫자, 월드 툴팁 | **UGUI** | 엔티티 추종 |

> 상호작용 UI의 입력 소비 지점은 `Assets/_Project/Scripts/Gameplay/IsoTapInput.cs`의 `OnTileTapped`(현재 `TODO(M1)`). 게임 로직은 어떤 UI 시스템인지 몰라도 되게 유지.

## Claude 디자인 워크플로

1. **시안 생성** — Claude가 `artifact-design` 스킬로 HTML/CSS 디자인 시안(아티팩트) 생성. 정통 판타지 던전 + 픽셀아트 톤, 라이트/다크 대응, 모바일 세로 기준(+PC 와이드 확장).
2. **리뷰** — 사용자가 아티팩트로 화면 비교·피드백.
3. **이식** — 확정된 화면 중 UI Toolkit 대상만 UXML(구조)/USS(스타일)로 이식.

### HTML → UXML/USS 매핑 메모

| HTML/CSS | UI Toolkit |
|----------|-----------|
| `<div>` | `<ui:VisualElement>` |
| `class` / CSS 셀렉터 | USS 셀렉터 (`.class`, `#name`) |
| flexbox (`display:flex`) | UI Toolkit 기본 레이아웃(=flex) 그대로 |
| `px` 단위 | 그대로 사용 가능 |
| `<img>` / `background-image` | `background-image: url()` (Sprite) |
| `<button>` | `<ui:Button>` |
| `<label>` / 텍스트 | `<ui:Label>` |

> UGUI 대상 화면 시안은 **레이아웃/톤 참고용**으로만 쓰고, 씬에서 Canvas·RectTransform으로 직접 구성한다.

## 크로스플랫폼 제약 (SYSTEMS.md §12 연동)

- **해상도**: UI Toolkit `PanelSettings`의 Scale Mode, UGUI `Canvas Scaler` — 모바일 세로 우선 + PC 와이드 대응. 노치 **안전영역** 처리.
- **호버 부재**: 탭=선택 / 재탭=실행 2단계, 롱프레스=정보. 모든 시스템 공통.
- **조준**: 타일 단위 스냅 + 확인 단계 (마우스·터치 모두 커버).
- **픽셀아트**: 폰트·9-slice·Point filter 프리셋 통일.
- **성능**: 모바일 하한선 — UI 갱신도 컬링 원칙 준수.

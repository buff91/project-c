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
| 인벤토리 + 조합 | **UI Toolkit** | 6×4 멀티슬롯 백팩(`BackpackLayout`)·레시피 |
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

- **프레젠테이션 분리**: 던전 HUD는 `PrototypeHUD.Mobile.uxml`과 `PrototypeHUD.Desktop.uxml`을 분리하되 동일한 요소 이름 계약과 `PrototypeHudController`를 공유한다. `Auto`는 플랫폼 기준으로 터치 우선/포인터 우선 View를 고르고, 개발 검증에서는 강제로 지정할 수 있다.
- **공용 설정**: `DisplaySettings.uxml`과 `DisplaySettingsPanelController`를 타이틀/허브/던전이 함께 사용한다. 가독성 값은 `DisplaySettingsStore`에 저장해 씬 전환 뒤에도 유지하며, 톱니바퀴는 설정·햄버거는 현재 씬 메뉴로 역할을 고정한다.
- **시작/원정지 선택**: 시작 화면은 독립 `MainMenu` 씬, 캠프 월드는 `Hub` 씬으로 분리한다. 프롤로그·세계관 연출은 두 씬 사이에 별도 씬으로 삽입한다. 원정지 선택은 허브 포탈 도착 시 UI Toolkit 모달로 열며 실제 던전 씬 전환은 `던전 진입` 확인 버튼에서만 수행한다.
- **월드 기능 발견 카드**: 수직 이동 수단처럼 공간에서 처음 알아차려야 하는 기능은 상시 범례를 늘리지 않고 `VerticalRouteDiscovered` 이벤트로 1회성 비차단 카드를 띄운다. PC는 상단 중앙, 모바일은 상단 안전 영역 아래에 두고 7초 뒤 자동으로 닫는다.
- **개발 화면 테스트**: 에디터/개발 빌드의 공용 설정 하단에서 `AUTO/MOBILE/PC` UI와 Game View 해상도 프리셋을 즉시 바꾼다. 릴리스 빌드에서는 이 섹션과 개발 오버라이드를 숨기고 무시한다.
- **해상도**: UI Toolkit `PanelSettings`는 540×960 `Scale With Screen Size`를 기준으로 한다. View 선택 뒤 `ResponsiveUiLayout`이 패널 논리 크기에 따라 `is-narrow`(<520), `is-short`(<700), `is-landscape`, `is-expanded`(짧은 축 ≥590), `is-tall`(세로 비율 ≥2:1), `is-ultrawide`(가로 비율 ≥2:1) 클래스를 HUD 루트에 적용한다. 모바일/PC 전용 USS가 같은 프로필도 입력 방식에 맞게 다르게 재배치한다. 실제 기기의 노치·홈 인디케이터는 `Screen.safeArea`를 패널 좌표로 환산해 루트 inset으로 처리한다.
- **해상도 정규화**: 1280×720, 1366×768, 1920×1080처럼 종횡비가 같은 화면은 거의 같은 논리 크기로 환산되므로 같은 배치를 공유한다. 픽셀 해상도별 하드코딩 대신 종횡비와 사용 가능한 논리 공간이 달라지는 지점에서만 프로필을 전환한다.
- **회귀 기준**: 모바일은 360×640, 390×844, 768×1024, 844×390을, PC는 960×540, 1366×768, 1280×1024, 2560×1080을 대표값으로 삼아 타이틀·허브·인게임 HUD·설정·인벤토리 모달의 잘림/겹침을 렌더 검증한다.
- **호버 부재**: 탭=선택 / 재탭=실행 2단계, 롱프레스=정보. 모든 시스템 공통.
- **조준**: 타일 단위 스냅 + 확인 단계 (마우스·터치 모두 커버).
- **픽셀아트**: 폰트·9-slice·Point filter 프리셋 통일.
- **성능**: 모바일 하한선 — UI 갱신도 컬링 원칙 준수.

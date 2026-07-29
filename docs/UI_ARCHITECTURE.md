# UI 아키텍처 & Claude 디자인 워크플로

> UI를 **UI Toolkit**과 **UGUI**로 이원화한다. Claude가 HTML/CSS 디자인 시안을 뽑고, 그중 화면공간 평면 UI는 UI Toolkit(UXML/USS)로 이식한다.
> 상세 근거는 `GDD.md` §4.2·§12, `docs/SYSTEMS.md` §12 참조. 이 문서는 UI 판단의 SSOT.
> **시각 언어(색·타이포·컴포넌트)는 `docs/UI_DESIGN_SYSTEM.md` + `Assets/_Project/UI/DesignSystem.uss`가 SSOT.**

> **⚠ 현재 개발 우선순위(임시): 모바일 보류 · PC 우선.** 아래 크로스플랫폼·모바일 배치 규칙은 코드로
> 계속 유지되지만, 당분간 **새 작업의 검증·패리티 대상이 아니다**(PC 가로만 검증). 상세: `CLAUDE.md` "현재 개발 우선순위".

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
| 메인메뉴 / 설정 | **UI Toolkit** | 중앙 저정보 영역을 가진 `ui-main-menu-backdrop` 위에 패널 배치 |
| 메타 프로그레션 해금 | **UI Toolkit** | 레시피/도구/시작 장비/시설 (직업 없음) |
| 결과 / 게임오버 | **UI Toolkit** | |
| 오브젝트 상호작용 팝업 (밀기/부수기/열기/줍기) | **UGUI** | 탭한 오브젝트에 앵커 (GDD §4.2) |
| 조준·타겟·높이 마커 | **UGUI** | 타일/월드 좌표 추종 |
| 플로팅 데미지 숫자, 월드 툴팁 | **UGUI** | 엔티티 추종 |

> 상호작용 입력은 `IsoTapInput`이 타일/액터 선택으로 추상화하고 `IsoPrototypeDemo`의 문맥 행동 경로로
> 전달한다. 게임 로직은 어떤 UI 시스템이나 입력 장치에서 왔는지 몰라도 되게 유지한다.

현재 `PrototypeHUD`의 `action-wheel`은 플레이어 화면 좌표를 따라가는 UI Toolkit
프로토타입이다. v1.5에서는 창 형태의 2×3 팔레트를 쓰지 않고, 플레이어 중심을 비운
6방향 육각 아이콘+짧은 동사 링으로 표시한다. 문맥에 없는 행동은 해당 방향을 비운다.
M4의 오브젝트별 문맥 팝업을 구현할 때는 동일한 데이터 계약과 육각 셀 시각 슬롯을
UGUI View로 옮긴다.
화면 고정 하단 `interact-button`은 키보드/포인터용 현재 행동 단축 도크로 유지한다.

## UI Toolkit 상태 클래스 계약

> **상태는 USS 클래스 토글로 표현한다.** 컨트롤러는 `AddToClassList`/`EnableInClassList`로
> 클래스만 붙이고, 보이기·색·크기는 USS가 정한다. 예외는 클래스를 새로 만들 가치가 없는 단발
> 노출 토글(개발 전용 섹션·타이틀 재개 버튼·사망 원인 라벨)의 `style.display`뿐이다.
> 클래스 이름이 여러 컨트롤러에 문자열 리터럴로 흩어져 있어 이 표가 유일한 계약서다. USS 규칙이 없는
> 이름을 붙이면 **아무 일도 일어나지 않고 조용히 실패한다** — 새 상태를 만들 땐 여기 먼저 적는다.

| 클래스 | 의미 | 붙는 요소 | USS 계약 |
|--------|------|-----------|----------|
| `is-open` | 모달·오버레이·패널 열림 | 모든 모달(허브 7종 / 인벤토리 / 게임메뉴 / 종료 / 결과 / 공용 설정)과 `action-wheel`·`boss-panel`·`vertical-route-discovery`·`debug-panel` | 기본형이 `display: none`, `.X.is-open`이 `display: flex`. **닫힘이 기본값** |
| `is-available` | 지금 실행 가능 | `hub-continue`(세이브 있음), `interact-button`(문맥 행동 있음) | 없으면 숨김/비활성 표현 |
| `is-empty` | 채워지지 않은 칸 | `pc-heart`(HP 빈칸), `inventory-detail-icon` | 빈 칸 표현 |
| `is-warning` | 경고 임계 | `hunger-label` | 경고색 |
| `is-victory` | 결과가 생존 | `gameover-overlay` | 제목 색/문구 분기 |

모달은 **`settings-modal` 한 껍데기를 공유한다.** 허브·던전·공용 설정의 모달 전부가 UXML에서
`class="settings-modal"`을 달고, `display` 규칙은 `.settings-modal` / `.settings-modal.is-open`
한 쌍에만 있다. 그래서 모달을 새로 만들 때 개별 USS 규칙을 쓸 필요가 없고, 반대로 그 한 쌍을
건드리면 모든 모달이 같이 움직인다. `PrototypeHUD.Mobile/Desktop.uxml`은 공용 `PrototypeHUD.uxml`을
`<ui:Instance>`로 감싸기만 하므로 이 계약은 두 View에서 자동으로 같다.

레이아웃 프로필(`is-narrow`·`is-short`·`is-landscape`·`is-expanded`·`is-tall`·`is-ultrawide`)과
입력 프로필(`ui-touch`·`ui-pointer`)은 `ResponsiveUiLayout`이 **HUD 루트에만** 부여한다.
정의와 임계값은 아래 「크로스플랫폼 제약」의 해상도·입력 프로필 항목에 있다.

접두사 없는 상태 클래스도 남아 있다 — 초기 코드의 잔재이므로 **새로 만들 땐 `is-` 를 쓴다.**

| 클래스 | 의미 | 붙는 요소 |
|--------|------|-----------|
| `selected` | 선택된 슬롯/옵션 | 인벤토리·백팩 슬롯, 허브 원정지 옵션, 출정 준비 슬롯, 상점 탭 |
| `locked` | 잠긴 선택지 | 허브 원정지 옵션 |
| `drop-valid` / `drop-invalid` | 드래그 목적지 판정 | 출정 준비의 적재/창고 패널 |
| `aiming` | 조준 중인 투척 아이템 | 폭탄·서리폭탄 버튼 |
| `ranged` | 현재 전투 행동이 원거리 | `combat-button` |
| `uncraftable` | 재료 부족 | 조합 행 |

> **알려진 어긋남**: `HubHudController.Vendors.cs`가 의뢰·기록 행에 `is-locked`를 토글하지만
> USS 어디에도 `is-locked` 규칙이 없다 — 붙어도 화면이 변하지 않는다. 잠금 표현이 `locked`와
> `is-locked` 두 벌로 갈린 결과다. 고칠 때는 `locked` 쪽으로 통일하는 편이 싸다(그쪽에만 규칙이 있다).

## Claude 디자인 워크플로

1. **시안 생성** — Claude가 `artifact-design` 스킬로 HTML/CSS 디자인 시안(아티팩트) 생성.
   톤은 **포스트 아포칼립스/이상 미궁** 픽셀아트 — 차가운 청흑 바탕 + 국소 앰버(호박) 광원 +
   신호색(틸) 하나. 예전엔 "정통 판타지 던전"이라고 적혀 있었는데 GDD §10이 테마 전환을 확정해서
   바꿨다. 팔레트 공식 자체는 테마와 무관하게 유지되고 **재료 어휘만** 바뀐다(횃불→비상등,
   석재→콘크리트·녹·균열). 방향 SSOT는 `docs/art-direction/project-c-postapoc-art-direction-v1.md`,
   토큰 값은 `docs/UI_DESIGN_SYSTEM.md`. 시안 자체는 라이트/다크 대응하되, **기준 화면은 PC 가로**다 —
   모바일 세로를 기준으로 잡고 PC 와이드를 확장으로 두던 순서는 문서 상단 우선순위 주의에서 뒤집혔다.
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
- **시작/원정지 선택**: 시작 화면은 독립 `MainMenu` 씬, 캠프 월드는 `Hub` 씬으로 분리한다.
  메인 메뉴 배경은 `ui-main-menu-backdrop` 한 슬롯이며 `scale-and-crop`으로만 배치한다. 배경의
  중앙 55%는 제목/행동 패널을 위해 저정보 영역으로 유지하고, 테마 서사는 가장자리 건축과
  국소 호박색/청록 신호로만 전달한다. 프롤로그·세계관 연출은 두 씬 사이에 별도 씬으로 삽입한다.
  원정지 선택은 허브 포탈 도착 시 UI Toolkit 모달로 열며 실제 던전 씬 전환은 `던전 진입`
  확인 버튼에서만 수행한다.
- **월드 기능 발견 카드**: 수직 이동 수단처럼 공간에서 처음 알아차려야 하는 기능은 상시 범례를 늘리지 않고 `VerticalRouteDiscovered` 이벤트로 1회성 비차단 카드를 띄운다. PC는 상단 중앙, 모바일은 상단 안전 영역 아래에 두고 7초 뒤 자동으로 닫는다.
- **개발 화면 테스트**: 에디터/개발 빌드의 공용 설정 하단에서 `AUTO/MOBILE/PC` UI와 Game View 해상도 프리셋을 즉시 바꾼다. 릴리스 빌드에서는 이 섹션과 개발 오버라이드를 숨기고 무시한다.
- **개발 저장 테스트**: 같은 공용 설정에서 실제 창고·체크포인트와 격리된 임시 프로필을 선택하고 초기화한다. 프로필 전환은 타이틀/허브에서만 허용하고, 던전 진행 중에는 잠근다.
- **해상도**: UI Toolkit `PanelSettings`는 540×960 `Scale With Screen Size`를 기준으로 한다. View 선택 뒤 `ResponsiveUiLayout`이 패널 논리 크기에 따라 `is-narrow`(<520), `is-short`(<700), `is-landscape`, `is-expanded`(짧은 축 ≥590), `is-tall`(세로 비율 ≥2:1), `is-ultrawide`(가로 비율 ≥2:1) 클래스를 HUD 루트에 적용한다. 모바일/PC 전용 USS가 같은 프로필도 입력 방식에 맞게 다르게 재배치한다. 실제 기기의 노치·홈 인디케이터는 `Screen.safeArea`를 패널 좌표로 환산해 루트 inset으로 처리한다.
- **씬 간 월드 배율**: 메인·허브·던전은 같은 `PrototypePanelSettings`와 현재 Screen/Game View 해상도를 공유한다. PC 허브와 던전 플레이는 직교 카메라 `playCameraSize` 2.3을 그대로 쓰고 플레이어를 추종한다. 허브 전체 맵을 담기 위한 auto-fit은 금지한다 — 로비의 크기가 씬 전환 전후 타일·액터 배율을 바꾸면 안 된다.
- **입력 프로필**: `ResponsiveUiLayout`은 타이틀·허브·던전 모든 루트에 `ui-touch` 또는 `ui-pointer`를 부여한다. 터치 표준 컨트롤(버튼·토글·드롭다운·슬라이더)은 논리 56px, 밀집 아이템 슬롯은 최소 44px, 6×4 백팩 셀의 실제 누름 영역은 52px을 보존한다. 아이콘 그림은 작아도 picking 영역은 줄이지 않는다.
- **짧은 화면**: 세로 공간이 부족하면 컨트롤을 축소하지 않고 본문만 ScrollView로 전환한다. 설정·인벤토리는 헤더/주요 완료 버튼을 고정하고, 모바일 가로 타이틀은 주 행동을 2열로 배치한다.
- **해상도 정규화**: 1280×720, 1366×768, 1920×1080처럼 종횡비가 같은 화면은 거의 같은 논리 크기로 환산되므로 같은 배치를 공유한다. 픽셀 해상도별 하드코딩 대신 종횡비와 사용 가능한 논리 공간이 달라지는 지점에서만 프로필을 전환한다.
- **회귀 기준**: 모바일은 360×640, 390×844, 768×1024, 844×390을, PC는 960×540, 1366×768, 1280×1024, 2560×1080을 대표값으로 삼아 타이틀·허브·인게임 HUD·설정·인벤토리 모달의 잘림/겹침을 렌더 검증한다.
- **호버 부재**: 탭=선택 / 재탭=실행 2단계, 롱프레스=정보. 모든 시스템 공통.
- **조준**: 타일 단위 스냅 + 확인 단계 (마우스·터치 모두 커버).
- **픽셀아트**: 폰트·9-slice·Point filter 프리셋 통일.
- **성능**: UI 갱신도 컬링 원칙을 따른다. "모바일이 하한선"이라는 전제 쪽은 문서 상단 우선순위 주의가 임시로 완화한 상태라 여기서 되풀이하지 않는다.

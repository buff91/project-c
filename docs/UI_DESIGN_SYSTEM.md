# UI 디자인 시스템 "Torchstone" v1

> 화면공간 UI(UI Toolkit)의 시각 언어 SSOT. 아키텍처 판단(UI Toolkit vs UGUI)은 `docs/UI_ARCHITECTURE.md`, 톤의 근거는 `docs/art-direction/project-c-artstyle-concept-v1.png`.
> 실체 파일: `Assets/_Project/UI/DesignSystem.uss` (토큰 + 공통 컴포넌트 클래스).

## 콘셉트

**토치스톤(Torchstone)** — 던전의 두 재료인 *돌*과 *횃불빛*.
어두운 청록빛 돌 바탕 위에, 정보 위계를 색 온도로 나눈다:

- **골드(따뜻함)** = 제목·강조·현재 위치 → 횃불이 비추는 곳
- **민트(차가움)** = 시스템 정보·힌트·마법 → 던전의 물/얼음/마력
- **엠버(뜨거움)** = 행동(공격·확정) → 불씨
- **양피지** = 기본 텍스트

시그니처는 **2px 픽셀 베벨**: 밝은색 상·좌 / 어두운색 하·우. 라운딩 금지(픽셀아트 톤), 그림자 대신 베벨로 깊이 표현.

## 컬러 토큰

| 토큰 | 값 | 용도 |
|------|-----|------|
| `--pc-void` | `#0A1215` | 던전 배경(풀스크린 화면 바탕) |
| `--pc-panel` | `rgba(13,20,24,0.92)` | 패널 바탕 (게임 위 오버레이라 반투명) |
| `--pc-inset` | `#181F22` | 패널 안 음각 스트립(수치 리드아웃) |
| `--pc-bevel-hi` / `--pc-bevel-lo` | `#5C6C6D` / `#273135` | 스톤 베벨 밝은쪽/어두운쪽 |
| `--pc-text` | `#EEE6CC` | 양피지 — 기본 텍스트 |
| `--pc-text-title` | `#D1B980` | 토치골드 — 패널 제목·강조 |
| `--pc-text-dim` | `#969E94` | 보조 텍스트 |
| `--pc-mint` | `#77D3C3` | 시스템/정보/힌트 |
| `--pc-ember` / `--pc-ember-bg` | `#FFD375` / `#4B3122` | 액션(공격) 텍스트/바탕 |
| `--pc-ice` / `--pc-ice-bg` | `#B4FCEE` / `#18484A` | 원거리/빙결/마법 |
| `--pc-hp` | `#E0524A` | HP·화상 |
| `--pc-poison` | `#8BC34A` | 중독 |
| `--pc-gold-marker` | `#DDB25C` | 현재 층 마커·선택 표시·재화 |

> 값의 출처: 프로토타입 HUD(`PrototypeHUD.uss`)에서 실사용 중인 색 + 아트 컨셉 이미지. 새 화면은 **리터럴 금지, 토큰만** 사용.

## 타이포그래피

| 토큰 | 크기 | 용도 |
|------|------|------|
| `--pc-fs-hint` | 9px | 힌트·캡션·칩 |
| `--pc-fs-label` | 10px | 패널 제목·버튼·리드아웃 |
| `--pc-fs-body` | 12px | 본문·일반 수치 |
| `--pc-fs-strong` | 14px | 강조 수치 |
| `--pc-fs-display` | 18px | 큰 수치·아이콘 글리프 |

- 제목/레이블은 **대문자 + `letter-spacing: 1px` + bold** (영문 기준. 한국어는 자간 없이 bold만).
- 폰트 에셋: 픽셀 폰트로 통일 예정 — 한국어 지원 픽셀 폰트(예: Galmuri, DungGeunMo) 후보. Point filter·정수 배율 프리셋은 `docs/SYSTEMS.md` §12 크로스플랫폼 규칙을 따른다. **디자인 시안(HTML)에서는 모노스페이스로 대체 표현.**

## 간격 & 형태

- **4px 그리드**: `--pc-space-1(4)` `-2(8)` `-3(12)` `-4(16)` `-6(24)`.
- **베벨**: 기본 `2px`(`--pc-bevel-w`), 보조 요소 `1px`. 볼록(패널·버튼) = hi 상·좌 / 오목(리드아웃·슬롯) = lo 상·좌 — 빛은 항상 좌상단에서.
- **라운딩·그림자 금지.** 깊이는 베벨과 음각색으로만.
- **터치 타깃 최소 44px** (모바일 하한선).

## 컴포넌트 클래스 (DesignSystem.uss)

| 클래스 | 요소 | 설명 |
|--------|------|------|
| `.pc-panel` (+`--system`) | VisualElement | 스톤 베벨 패널. system 변형은 민트 베벨 |
| `.pc-title` | Label | 패널 제목 (토치골드) |
| `.pc-readout` | Label | 음각 수치 스트립 |
| `.pc-btn` | Button | 기본 스톤 버튼 |
| `.pc-btn--action` | Button | 공격/확정 (엠버) |
| `.pc-btn--ranged` | Button | 원거리/마법 (아이스) |
| `.pc-btn--system` | Button | 모드 토글 등 (민트, 얇은 보더) |
| `.pc-hint` / `.pc-body` / `.pc-dim` | Label | 텍스트 유틸 |
| `.pc-bar-track` + `.pc-bar-fill`(`--mana`) | VisualElement | HP/자원 게이지 (fill 너비는 코드에서 %) |
| `.pc-chip--burn/freeze/poison` | VisualElement | 상태이상 칩 |
| `.pc-slot` (+`--selected`) | VisualElement | 인벤토리/조합 슬롯. 선택 = 골드 보더 |

## 사용 규칙

1. UXML에서 `DesignSystem.uss`를 **화면별 USS보다 먼저** 로드:
   ```xml
   <ui:Style src="DesignSystem.uss" />
   <ui:Style src="InventoryScreen.uss" />
   ```
2. 화면별 USS는 **레이아웃(배치·크기)만** 담당. 색·폰트·베벨은 `pc-*` 클래스와 `var(--pc-*)` 토큰으로.
3. 새 색이 필요하면 이 문서와 `DesignSystem.uss`에 토큰부터 추가 — 화면 USS에 리터럴을 넣지 않는다.
4. UGUI(월드 앵커 UI: 상호작용 팝업, 데미지 숫자, 타겟 마커)도 **같은 팔레트/베벨 규칙**을 따르되, 구현은 씬에서 직접. 시안은 참고용.
5. `PrototypeHUD.uss`는 이 시스템보다 먼저 작성됨 — 값은 이미 일치하며, 다음 UI 작업 때 `pc-*` 클래스로 점진 이관.

## 디자인 시안 워크플로 (UI_ARCHITECTURE.md §Claude 디자인 워크플로)

Claude가 만드는 HTML 시안은 이 토큰을 CSS 변수로 그대로 사용한다(이름 동일). 확정 후 UXML/USS 이식 시 매핑이 1:1이 되도록 유지한다.

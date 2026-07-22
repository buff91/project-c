# UI 디자인 시스템 "Torchstone" v1.3

> 화면공간 UI(UI Toolkit)의 시각 언어 SSOT. 아키텍처 판단(UI Toolkit vs UGUI)은 `docs/UI_ARCHITECTURE.md`.
> 근거: `docs/art-direction/project-c-artstyle-concept-v1.png` **픽셀 실측**.
> 실체 파일: `Assets/_Project/UI/DesignSystem.uss` (토큰+컴포넌트) · `DesignSystemGallery.uxml` (검증 갤러리) · `UI/Fonts/Galmuri9.ttf` (OFL).

## 콘셉트

**토치스톤(Torchstone)** — 씬의 공식을 UI에 그대로 적용한다:

> **차가운 청흑 바탕 + 횃불에 데워진 돌 프레임 + 토치 골드 포인트.**

- 바탕은 씬의 어둠과 같은 **청흑**(웜 브라운 금지 — 씬과 온도가 충돌한다)
- 프레임·구조물은 **횃불빛 받은 돌**(웜 그레이·토프)
- **골드**는 "현재·선택" 단 한 곳에만 (현재 층, 선택 슬롯, 선택 액션)
- **틸/아이스**는 시스템·물·얼음·마법, **하트 레드**는 HP

### HUD 이원화 (v1.2의 핵심 규칙)

| 구분 | 스타일 | 대상 |
|------|--------|------|
| **인게임 상시 HUD** | 프레임 없는 플로팅 (하드 섀도로만 분리) | 하트 HP, 다이아 층 레일, 다이아 액션 버튼, 위치/힌트 텍스트 |
| **메뉴·팝업** | 창 크롬 (`pc-window`) | 인벤토리, 조합, 설정, 상호작용 팝업, 결과 화면 |

컨셉 아트가 이미 그려둔 UI 어휘(하트·다이아)를 시스템으로 승격한 것. **씬이 주인공** — 인게임에 창을 띄우지 않는다.

## 컬러 토큰 (씬 실측)

| 토큰 | 값 | 용도 | 출처 |
|------|-----|------|------|
| `--pc-void` | `#05070C` | 던전 배경 (청흑) | 씬 어둠 #000206~#11141A |
| `--pc-panel` | `#0A0D13` 92% | 창 바탕 | 〃 |
| `--pc-inset` | `#07090E` | 음각·슬롯 바닥 | 〃 |
| `--pc-stone` / `-lit` / `-dim` | `#98866F` / `#CFC0AE` / `#4A4038` | 돌 프레임 | 다이아 버튼 #B0A59A |
| `--pc-gold` | `#FFD554` | 현재 층·선택 | 층 마커 #FFEA5C |
| `--pc-torch` | `#FFBD41` | 횃불·화상 | 횃불 실측 |
| `--pc-gold-deep` | `#9A6B22` | 액션 버튼 프레임 | 파생 |
| `--pc-text` / `--pc-dim` | `#EADFC8` / `#97907E` | 본문 / 보조 | 파생 |
| `--pc-hp` / `-empty` | `#D8452A` / `#45100B` | 하트·HP 게이지 | 하트 실측 |
| `--pc-teal` / `--pc-ice` / `--pc-teal-bg` | `#4FA7A0` / `#9ADFE8` / `#14343A` | 시스템·물·얼음 | 물 #1C4347 |
| `--pc-xp` | `#7FB241` | 경험치·중독 | 파생 |
| `--pc-btn-bg`(-hover), `--pc-action-bg`(-hover) | USS 참조 | 버튼 바탕 | 파생 |

새 색이 필요하면 **씬에서 실측 → 토큰 추가 → 사용**. 화면 USS에 리터럴 금지.

## 타이포그래피 — Galmuri9, 정수 배율

- 폰트: **Galmuri9** (`UI/Fonts/Galmuri9.ttf`, SIL OFL 1.1 — 라이선스 동봉). `DesignSystem.uss`의 `:root`에서 전역 지정.
- **크기는 9의 정수 배율만**: `--pc-fs-1`(9) 캡션·레이블·버튼 / `--pc-fs-2`(18) 주요 수치 / `--pc-fs-3`(27) 데미지·타이틀 / `--pc-fs-6`(54) 층 전환 연출. 배율이 어긋나면 도트가 깨진다.
- PanelSettings 기준 해상도 540×960(Scale With Screen Size) 전제 — 9px ≈ 기기 18px.
- 선명도가 부족하면 에디터에서 Galmuri9로 Font Asset을 만들어(Raster 모드) `-unity-font-definition`에 연결한다.

## 컴포넌트 클래스 (DesignSystem.uss)

| 클래스 | 용도 |
|--------|------|
| `.pc-window` (+`--teal`), `.pc-window-title` | 창 크롬 — 메뉴·팝업 전용 |
| `.pc-float-text` (+`--hint`) | 플로팅 HUD 텍스트 (하드 섀도) |
| `.pc-dia` (+`--current`), `.pc-dia-label`, `.pc-rail-link` | 다이아 층 레일 — 현재 층 골드 |
| `.pc-dbtn` (+`--selected`) | 다이아 액션 버튼 — 탭=선택(골드)/재탭=실행 |
| `.pc-heart` | HP 하트 자리 (도트 스프라이트 지정) |
| `.pc-btn` (+`--action` 골드 / `--ranged` 아이스 / `--system` 틸) | 창 안 사각 버튼 |
| `.pc-readout` | 음각 수치 스트립 |
| `.pc-bar-track`/`.pc-bar-fill` (+`--xp`) | 게이지 (fill 너비는 코드에서 %) |
| `.pc-chip--burn/freeze/poison` + `.pc-chip-swatch` | 상태이상 칩 |
| `.pc-slot` (+`--selected`), `.pc-slot-qty` | 아이템 슬롯 |

검증: `DesignSystemGallery.uxml`을 UIDocument에 연결하면 전체 컴포넌트를 에디터에서 바로 확인할 수 있다.

## 사용 규칙

1. UXML에서 `DesignSystem.uss`를 **화면별 USS보다 먼저** 로드.
2. 화면별 USS는 **레이아웃만**. 색·폰트·프레임은 `pc-*` 클래스와 `var(--pc-*)`.
3. **골드 글로우/강조는 화면에 한 곳만** (현재 층 또는 선택 대상).
4. UGUI(월드 앵커: 적 HP바, 데미지 숫자, 상호작용 팝업)도 같은 팔레트. 데미지 숫자는 `--pc-fs-3` + 하드 섀도.
5. `PrototypeHUD.uss`는 v1.2 토큰으로 이관 완료 (레이아웃 유지, 색만 교체).

## 스프라이트 적용 현황과 남은 승격 (USS 한계)

USS에는 box-shadow가 없어 아래는 임시로 단순 보더이며, 아트 파이프라인(GDD §6)에서 도트 스프라이트로 승격한다:

- 창 크롬의 **깎인 모서리** → 9-slice 스프라이트 1장
- 다이아 현재층/선택의 **골드 글로우** → 스프라이트
- 게이지 **세그먼트 눈금** → 오버레이 스프라이트
- 하트·물약·폭탄·냉기 폭탄 아이콘은 `Art/Runtime` 도트 스프라이트 적용 완료. 검·장화 등 추가 액션 아이콘은 같은 세트에서 확장한다.
- 다이아는 최종적으로 `rotate` 대신 스프라이트로 (픽셀 그리드 보존)

v1.3부터 액션 휠도 원형 알약 버튼 대신 다이아 버튼을 사용한다. HUD와 허브의 화면별 USS는 `--pc-*` 토큰을 덮어쓰지 않으며, 양쪽 모두 같은 청흑 패널·돌 프레임·골드 선택·틸 시스템 규칙을 따른다.

## 디자인 시안 워크플로

Claude의 HTML 시안(아티팩트)은 이 토큰과 같은 이름을 쓴다. 시안 확정 → USS/UXML 이식 → `DesignSystemGallery.uxml`로 대조.

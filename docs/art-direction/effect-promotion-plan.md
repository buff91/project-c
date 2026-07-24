# 이펙트 승격 설계: 절차적 FX → 스프라이트 시트

> 목표: 지금 **코드가 런타임에 픽셀을 찍어 만드는** 전투/상태 이펙트를,
> 아트가 그린 **Aseprite 프레임 애니**로 승격한다. 파이프라인·정렬·타이밍은 유지하고
> **스프라이트 소스만 교체**한다. 폴백(에셋 없을 때 절차 생성)은 남겨 하위 호환.

## 현재 구조 (승격 전)

| 요소 | 현재 코드 | 방식 |
|------|-----------|------|
| 타격 버스트 | `IsoPrototypeDemo.CombatFx.cs → GetCombatImpactSprite()` | 24×24 텍스처를 `SetPixel` 광선 루프로 생성 |
| 상태 고리(화상/빙결) | `GetCombatStatusSprite()` | 32×24 텍스처 절차 생성 |
| 버스트 모션 | `AnimateImpactBurst()` | 코루틴: scale 0.36→1.28 + 회전 + 알파 페이드 |
| 상태 아이콘 모션 | `CombatStatusFxAnimator.Update()` | 위치·크기·알파 흔들림 |
| 색 | `ImpactPalette()`, `GetCombatStatusSprite()` 내부 리터럴 | 코드 하드코딩 |
| 색 분류 | `Core/CombatPresentationRules.ImpactForSource()` | source 문자열 → `CombatImpactKind` |

> 액터 **타격 모션**(`AnimateMeleeLunge`, `PlayCombatImpact`의 스쿼시/플래시, `ShakeCamera`)은
> 그대로 둔다. 이건 FX 에셋이 아니라 캐릭터/카메라 코드 모션이다.

## 승격 후 목표

- FX 프레임 애니를 아트로 그리고, 코루틴은 **프레임 재생 + 배치/정렬**만 담당.
- 색이 **아트로 이동** → 코드의 `ImpactPalette`/텍스처 리터럴 의존 축소.
- 에셋이 비면 **기존 절차 생성으로 자동 폴백** (현행 "외부 VFX 없어도 읽힘" 철학 유지).

---

## 신규 슬롯 (6개)

| 파일명 | 슬롯(제안) | 캔버스 | 피벗 | 태그 | 대응 |
|--------|-----------|--------|------|------|------|
| `fx-impact-physical` | fxImpactPhysical | 24×24 | (0.5, 0.5) | `burst`(Repeat=1) | Physical |
| `fx-impact-fire` | fxImpactFire | 24×24 | (0.5, 0.5) | `burst`(Repeat=1) | Fire |
| `fx-impact-frost` | fxImpactFrost | 24×24 | (0.5, 0.5) | `burst`(Repeat=1) | Frost |
| `fx-impact-heavy` | fxImpactHeavy | 32×32 | (0.5, 0.5) | `burst`(Repeat=1) | Heavy(더 큼) |
| `fx-status-burn` | fxStatusBurn | 32×24 | (0.5, 0.5) | `idle`(루프) | 화상 고리 |
| `fx-status-freeze` | fxStatusFreeze | 32×24 | (0.5, 0.5) | `idle`(루프) | 빙결 고리 |

- 프레임 수 권장: 임팩트 `burst` 4~6프레임(≈0.18~0.24s에 맞춤), 상태 `idle` 4~8프레임 루프.
- 캔버스는 현재 절차 텍스처 크기를 그대로 승계(24×24 / 32×24). Heavy만 32×32로 키워 차별화.

## 코드 변경 (4파일)

### 1) `Scripts/Gameplay/IsoVisualCatalog.cs` — 슬롯 필드 추가
```csharp
[Header("전투 이펙트")]
public Sprite fxImpactPhysical;
public Sprite fxImpactFire;
public Sprite fxImpactFrost;
public Sprite fxImpactHeavy;
public Sprite fxStatusBurn;
public Sprite fxStatusFreeze;

public Sprite ImpactFx(CombatImpactKind kind) => kind switch
{
    CombatImpactKind.Fire  => fxImpactFire,
    CombatImpactKind.Frost => fxImpactFrost,
    CombatImpactKind.Heavy => fxImpactHeavy,
    _ => fxImpactPhysical,
};
public Sprite StatusFx(StatusKind kind) =>
    kind == StatusKind.Burn ? fxStatusBurn : fxStatusFreeze;
```

### 2) `Editor/ArtPipeline/ProjectCAsepritePipeline.cs` — 계약 등록
- `CatalogSlots`에 6줄 추가: `{ "fx-impact-physical", "fxImpactPhysical" }` … `{ "fx-status-freeze", "fxStatusFreeze" }`.
- `CustomPivots`에 6줄 추가: 전부 `new Vector2(0.5f, 0.5f)`.
- (다중 프레임이면 `SelectFirstFrame`이 첫 프레임만 슬롯에 꽂으므로, 재생은 아래 3)에서 AnimationClip/프레임 배열로 처리.)

### 3) `Scripts/Gameplay/IsoPrototypeDemo.CombatFx.cs` — 소스 교체 + 폴백
- `GetCombatImpactSprite(kind)`:
  ```csharp
  Sprite art = _catalog != null ? _catalog.ImpactFx(kind) : null;
  if (art != null) return art;          // 승격된 아트 우선
  // …이하 기존 절차 생성(폴백 유지)…
  ```
- `GetCombatStatusSprite(kind)` 동일 패턴으로 `_catalog.StatusFx(kind)` 우선.
- `AnimateImpactBurst()`: 아트가 **자체 프레임 애니**를 갖는 경우 scale/rotate 과장을 줄이고
  알파 페이드만 유지(또는 프레임 재생으로 대체). 절차 폴백일 때는 현행 코루틴 그대로.
- 멀티프레임 재생이 필요하면 경량 프레임 드라이버(코루틴에서 `renderer.sprite = frames[i]`)를
  추가하거나, `.aseprite`가 만든 AnimationClip을 `Animator` 없이 `SimpleSpriteAnimator`로 재생.

### 4) 색 하드코딩 정리 (선택)
- 아트로 색이 이동하면 `ImpactPalette()`와 `GetCombatStatusSprite()` 내부 색 리터럴은
  **폴백 전용**으로 격하. 지우지 말고 "폴백 팔레트"로 주석 명시(에셋 누락 시 가독성 보장).
- 액터 틴트(`CombatantTint`)·플래시(`ImpactHighlight`)는 **유지**(FX 아님).

## 테스트

- `Tests/EditMode/IsoVisualCatalogTests.cs`에 신규 슬롯 매핑 검증 추가
  (`ImpactFx(Fire)==fxImpactFire` 등, null 폴백 경로 포함).
- `CombatPresentationRulesTests`는 분류 로직이라 변경 불필요(그대로 통과 확인).
- 회귀: EditMode 673 / PlayMode 1.

## 단계별 착수 순서 (안전)

1. **슬롯+매핑+파이프라인 등록**(코드만, 아트 0장) → 폴백으로 현행 유지, 테스트 그린 확인.
2. `fx-impact-fire` 1장만 그려 꽂고 Play 캡처로 톤 확인(승격 파이프 검증).
3. 나머지 5개 순차 제작 → 각 캡처.
4. 아트 정착 후 `AnimateImpactBurst` 과장 축소·색 리터럴 폴백 격하.

> 핵심 원칙: **한 번에 하나씩, 폴백을 항상 남긴다.** 슬롯을 먼저 계약으로 박고
> 아트는 나중에 채워도 게임이 깨지지 않게 한다(현행 설계와 동일 철학).

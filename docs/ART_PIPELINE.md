# Project-C 아트 제작 파이프라인

> 목표: AI 시안을 그대로 게임에 넣는 것이 아니라, 시안에서 방향을 고른 뒤 Aseprite에서 일관된 모듈형 픽셀아트로 재제작한다.

## 1. 고정 제작 규격

| 항목 | 프로토타입 기준 |
|---|---:|
| 아이소 바닥 타일 | 64×32 px (2:1) |
| Pixels Per Unit | 64 |
| elevation 1단 | 화면상 16 px |
| 캐릭터 작업 캔버스 | 32×48 또는 48×64 px |
| 캐릭터 Pivot | 발 중앙 |
| 기본 방향 수 | 4방향 |
| 애니메이션 | 8~12 FPS |
| Texture Filter | Point |
| Compression | None |
| Mip Maps | Off |

`IsoGrid(tileWidth=1, tileHeight=0.5, elevationStep=0.25)`가 이 비율을 사용한다. 아트 규격을 바꾸려면 에셋을 그리기 전에 세 값을 함께 검증한다.

## 2. AI를 사용하는 범위

AI 이미지 생성에 맡긴다:

- 전체 분위기와 팔레트 탐색
- 방 구성과 소품 실루엣 아이디어
- 벽돌·금속·나무의 재질 밀도 비교
- 캐릭터 비율과 장비 형태 후보

Aseprite에서 직접 만든다:

- 반복 가능한 64×32 바닥 타일
- 좌·우 벽면과 안/밖 모서리
- 방향별 계단과 문
- 캐릭터 방향 및 애니메이션 프레임
- 정확한 Pivot, 타일 경계, 충돌 기준이 필요한 모든 에셋

AI 결과를 그대로 잘라 쓰면 투영각, 광원, 픽셀 크기와 타일 경계가 서로 달라진다. AI 결과는 원화로만 사용한다.

## 3. 첫 번째 실제 제작 묶음

한 번에 전체 던전을 만들지 않는다. 아래 묶음으로 8×8 테스트 룸을 먼저 완성한다.

1. 바닥 3종: 기본, 금 간 바닥, 이끼
2. 벽: 좌면, 우면, 바깥 모서리, 안쪽 모서리
3. 높이: 16 px 단차 블록, 4방향 계단
4. 특수 타일: Hole, WeakFloor, StairsUp(청색 표식), StairsDown(주황색 표식)
5. 소품: DoorClosed, DoorOpen, Crate, ExplosiveBarrel, Torch
6. 캐릭터: Player와 Goblin의 Idle 4방향

이 묶음이 Unity에서 정상적으로 정렬되고 반복된 뒤 Walk/Attack/Hit/Fall 애니메이션을 추가한다.

## 4. Aseprite 작업 순서

1. 64×32 다이아몬드를 템플릿 레이어로 만든다.
2. 모든 에셋에 같은 좌상단 광원을 사용한다.
3. 16~24색 마스터 팔레트에서만 색을 고른다.
4. 타일 경계 픽셀을 복사해 이웃 타일과 맞춘다.
5. 캐릭터 발 중앙을 동일한 좌표에 둔다.
6. `.aseprite` 원본과 PNG export를 함께 보관한다.
7. Unity 테스트 룸에서 100%와 실제 모바일 크기로 확인한다.

권장 폴더:

```text
Assets/_Project/Art/
  Source/Aseprite/
  Sprites/Tiles/
  Sprites/Characters/
  Sprites/Props/
  Palettes/
```

## 5. AI 시안용 프롬프트 템플릿

```text
Create a clean modular isometric pixel-art asset reference board for a dark fantasy mobile roguelike.
Use an exact-looking 2:1 diamond tile projection and one consistent upper-left light source.
Show isolated floor, left/right wall faces, corner, stairs, hole, weak floor, door, crate,
barrel, player and goblin on a flat dark background with generous spacing.
Use chunky deliberate pixel clusters and a limited 16-24 color palette.
No text, labels, UI, watermark, smooth painting, 3D rendering or overlapping assets.
This is an art-direction reference for rebuilding precise 64×32 assets in Aseprite,
not a ready-to-slice sprite sheet.
```

한 번에 여러 애니메이션 프레임을 요구하지 않는다. 캐릭터 디자인을 확정한 뒤 `Idle 4방향`, `Walk 4방향`처럼 묶음을 나눠 생성하고 Aseprite에서 다시 맞춘다.

## 6. Unity 교체 원칙

- 논리 데이터는 `TileKind`만 보유한다.
- `TileKind → Sprite` 매핑은 `IsoVisualCatalog` ScriptableObject가 담당한다.
- 바닥, 벽면, 오브젝트와 캐릭터는 같은 타일에서도 별도 렌더러로 둔다.
- 캐릭터와 높은 소품은 발 중앙 Pivot으로 정렬한다.
- 시안 교체 시 Core 로직과 `IsoGrid.SortingOrder`는 수정하지 않는다.
- 문은 닫힘/열림 두 스프라이트를 `IsoVisualCatalog`에 별도 연결한다. 상태 판정은 Core의 `TileKind`가 담당한다.
- 닫힌 문은 바닥 데칼이 아니라 발 중앙 기준의 세워진 문짝·문틀 실루엣이어야 한다. 열린 문은 중앙 통로를 비우고 문짝을 측면에 표시한다.
- 아이소 문은 정면 직사각형 한 장으로 만들지 않는다. 통로 축에 수직인 `↗ / ↖` 두 사선 평면의 닫힘·열림 세트를 제작하고, 시점 회전 시 대응 방향을 선택한다.
- 플레이어 고정 표식은 청록 발판+머리 위 화살표, 선택/공격 대상 표식은 주황 링으로 색 역할을 분리한다.
- 수직 연결은 선만 그리지 않는다. 시작/도착 발판, 방향 화살표, 두 층을 잇는 점선 리본을 한 세트로 제작한다. Hole은 청록, 안전 계단은 주황을 사용한다.
- 인접층 미리보기용 바닥/벽은 현재층보다 명도와 알파를 낮추되 최소 5×5 타일 조각이 읽혀야 한다. 전체 층을 축소 표시하지 않는다.

Unity Project 창에서 `Create > Project-C > Isometric Visual Catalog`를 선택하고 완성한 스프라이트를 슬롯에 넣은 뒤, `IsoPrototypeDemo.visualCatalog`에 연결한다. 일부 슬롯만 연결해도 나머지는 임시 아트로 표시되므로 한 에셋씩 교체하며 비교할 수 있다.

## 7. 현재 레퍼런스

- 전체 게임 화면 방향: `docs/art-direction/project-c-artstyle-concept-v1.png`
- 첫 모듈형 아트 키트 방향: `docs/art-direction/project-c-starter-art-kit-v1.png`
- 회전 가능한 던전의 최종 밀도 타깃: `docs/art-direction/project-c-rotatable-dungeon-target-v1.png`
- 현재 Unity 절차식 구현 캡처: `docs/art-direction/iso-prototype-room.png`

현재 런타임 임시 아트에도 석재 타일 변형, 16 px 단차 측면, 연속 후면 벽, 횃불, 상·하행 계단 색, Hole 강조, 기사·고블린 실루엣이 적용되어 있다. 목적은 최종 그림을 절차 생성하는 것이 아니라 다음을 먼저 검증하는 것이다.

- 어떤 시점에서도 벽·단차·캐릭터 정렬이 깨지지 않는가
- Hole과 계단이 작은 모바일 화면에서도 즉시 구분되는가
- 실제 Aseprite 스프라이트를 `IsoVisualCatalog`에 한 칸씩 넣어도 Core 로직을 수정하지 않아도 되는가

AI 타깃 이미지는 한 장의 완성 장면이므로 직접 슬라이스하지 않는다. `project-c-starter-art-kit-v1.png`를 형태 참고로 삼아 바닥/벽/코너/계단/캐릭터를 각각 64×32 규격에 다시 그린 뒤 Catalog 슬롯으로 점진 교체한다.

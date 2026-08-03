# Project-C 액터 어필 리스타일 — 캐릭터 언어 계약 (v2)

> **v1 (2026-07-29)**: 기존 액터 8종이 "매력 없다"는 문제의 원인을 **캐릭터 언어**(두신 비율 ·
> 얼굴 부재 · 환경 위장색 · 차렷 포즈)로 진단하고, 액터 화풍 계약을 치비로 전환.
> **v2 (2026-07-30)**: 세계관의 사이버펑크 승격(GDD v0.3.3)과 사용자 레퍼런스
> `reference/ref-cyberpunk-00-actor-lineup.jpg`(스트리트웨어 라인업 — **액터 스타일 SSOT**)에 맞춰
> 비율·복장·렌더링 문법을 개정. v1의 뭉툭한 치비(2등신·클러스터 손발)는 폐기하고
> **2.5~3등신 + 섬세 렌더링**으로 상향한다. 얼굴 정책(아군 노출/적 익명)은 유지.
> 이 문서는 **액터 한정**이다 — 환경·아이템·UI의 톤 원칙(`project-c-postapoc-art-direction-v1.md`)과
> 가독성 규칙(`project-c-art-improvement-plan-v2.md` §1)은 그대로 유효하며, 이 계약은 그 위에 얹힌다.

## 1. 확정 결정 (v2)

| 항목 | 결정 | 근거 |
|------|------|------|
| 두신 비율 | **2.5~3등신** (머리가 전체 높이의 33~40%) | 라인업 레퍼런스 기준. 표정 판독과 복장·소품 연기의 균형점 |
| 얼굴 정책 | **아군만 얼굴 노출, 적은 익명 유지** | 감정이입 + "얼굴 있는 쪽이 내 편"이라는 판독 규칙을 겸함 (v1 유지) |
| 복장 언어 | **캐주얼 스트리트웨어 + 생존 장비** — 후디·패딩·안경·배낭에 고글/마스크는 소품으로 | "특별한 전사"가 아니라 "붕괴 도시의 보통 사람"이 원정자라는 서사와 정합 |
| 아이덴티티 색 | **컬러 헤어가 캐릭터 아이덴티티** (핑크·틸그린·블론드 등 1인 1색) | 라인업 레퍼런스의 매력 핵심. 진영 신호색과 별도 축 |
| 무기 소품 | 배트·파이프·스틱류 즉석 무기 + **빔 계열**(빔 랜스·아크 캐스터, 리스킨 표 §4-b) | 캐주얼 복장 × SF 무기의 대비가 세계관 그 자체 |
| 테마 | **아포칼립스 + 사이버펑크** (GDD v0.3.3) | v1의 "폐병원 유지" 항목은 세계관 개정으로 대체됨 |

- **아군**(원정자·허브 NPC): 눈·피부·머리카락 노출. 마스크/고글은 쓰지 않고 **목/가슴에 건 소품**으로 강등.
- **적**(점거군·기업 병사·산업/진압 로봇): 마스크/렌즈/바이저로 익명. 신호는 `sig-warning` 네온 포인트 1곳(§1-a 유지).
- 실루엣 계약(개선 플랜 §1-b)은 이 비율 안에서 그대로 적용한다 — 웅크린 어깨+배낭,
  치켜든 팔+투척끈 등 고유 외곽 1요소는 유지해야 한다.

## 2. 렌더링 문법 (액터 화풍 계약 v4)

1. **비율**: 2.5~3등신. 머리는 크되 v1처럼 몸통을 삼키지 않는다. 손발은 단순화하되
   손가락 없는 뭉치로 뭉개지 않는다(라인업 레퍼런스의 소품 쥔 손 참조).
2. **얼굴(아군)**: 눈은 2~4px 세로 클러스터 + 하이라이트 1px. 눈썹으로 성격을 만든다.
   피부 명암은 2단 이내.
3. **포즈**: 정면 차렷 금지. 3/4 뷰, 무게 실린 다리, 소품을 "들고 있는 연기" 1개
   (어깨에 걸친 배트, 늘어뜨린 파이프, 안은 상자 등).
4. **색**: 몸통 베이스는 Torchstone 재료 램프(grey/fabric/rust) + 의상 1~2색, 진영 신호색 포인트
   1곳은 개선 플랜 §1-a를 따른다(아군 틸 · 중립 앰버 · 적 warning). **컬러 헤어**는 신호색
   면적 제한과 별개 축이지만, 환경 신호색(앰버 광원·틸 마커·warning)과 혼동되는 색·면적은 금지
   (예: 주황 머리는 앰버 광원과, 적색 머리는 warning과 간섭 — 채도나 명도로 분리).
5. **광원**: 공용 좌상단 광원 + `Outline → Shadow → Base → Light` 4단 구조 유지.
   라인업 레퍼런스처럼 **웜 림라이트 1px**을 실루엣 우측(광원 반대편 강조)에 허용한다.
6. **아웃라인**: v1의 두꺼운 클러스터 대신 **1px 다크 아웃라인**(순검정 아님 — `dark-cool`/`dark-warm`).

## 2-b. 비율 일관성의 3층 책임 (2026-07-30 실측 확정)

비율 일관성은 한 단계가 아니라 세 층이 나눠 진다. 어느 층의 문제인지 구분해서 고친다:

1. **관절 골격 = OpenPose ControlNet** — `xinsir controlnet-openpose-sdxl-1.0`(설치됨) +
   `guides/openpose/actor-chibi-*.png`. 서로 다른 정체성도 같은 관절 위치에 정렬된다.
   실측: strength 0.9 / end 0.8. **마네킹 img2img 방식은 두 번 실패로 폐기** —
   평평한 실루엣은 denoise를 낮추면 색이 새고 올리면 구도가 무너진다.
2. **체감 비율(부피) = 96×128 캔버스 행 계약** — 관절이 같아도 머리카락·모자 부피가
   체감 비율을 흔든다. conform 정규화에서 **crown row 8 · feet row 122** 고정(발 중앙 피벗과
   정합), 얼굴은 eye-zone row ~40 목표. 어떤 소스가 와도 게임 안에서는 같은 비율로 읽힌다.
3. **잔여 편차 = Aseprite 마감** — 머리카락 실루엣 다듬기·프레임 간 발 고정은 손작업 몫
   (파이프라인의 원래 분업: AI 결과는 원화다).

## 3. 팔레트 영향 (선행 작업)

`project-c-torchstone.gpl`에는 **피부·머리카락 램프가 없다.** 얼굴 노출 액터를 현행 conform에
넣으면 피부가 fabric/tile 진흙색으로 양자화된다. 첫 채택 후보를 conform하기 전에:

- `skin-1/2/3`(암부는 기존 저채도 공유 원칙 준수)을 **append**한다
  (코어 18색 순서 불변, .gpl 설계 규칙 주석 준수 — 겹치는 명도 구간의 램프 분할 금지).
- **(v2 추가)** 컬러 헤어 아이덴티티 색과 쿨 네온 광원(마젠타/시안)도 같은 원칙으로 append한다 —
  헤어는 캐릭터당 2단(base/light)이면 충분하고 램프를 늘리지 않는다. GDD §6 「사이버펑크 전환」 참조.
- 양자화가 기존 재료 램프의 픽셀을 뺏지 않는지 기존 Runtime 세트 재-conform으로 확인한다.
- **(게이트 실측 2026-07-30) 값 조정으로는 통과 불가 → 구조로 마감.** 재-conform 실측에서
  items/actors 16장 629px 절도(93%가 skin-1, fabric-2·rust-3·stone-mid 암부에서) —
  전 격자 탐색 결과 "절도 0인 갈색 피부톤"은 존재하지 않는다(유일해는 #B87474 로즈, 피부
  램프로 부적격). 처방: `torchstone_palette`가 `skin-*`/`hair-*`를 **기본 잠금에서 제외**하고
  얼굴 노출 자산(현재 백드롭, 이후 라이더 conform 레인)만 `include_identity=True`로 연다.
  덕분에 이후 `hair-blonde` 등 아이덴티티 append도 절도 걱정 없이 값만 보고 정하면 된다.
  근거 캡처: `docs/captures/palette-skin1-theft-audit-v1.png`(기각분) ·
  `palette-identity-lock-cleanup-v1.png`(env/props 잠복 절도 청산 + gemstone 네온 4px 승인).
- **(실측 2026-07-30)** skin 3단 + hair-pink 2단 + sig-neon 2색 append 완료. 단, **팔레트에
  네이비 램프가 없어 어두운 청색 의상이 틸 계열(anomaly/ui-teal)로 양자화된다** — 라이더
  4방향 conform에서 재킷이 틸로 끌려가 신호색 판독과 충돌했다. 처방: ① 액터 의상
  프롬프트에서 navy/blue-black 대신 **charcoal gray** 어휘를 쓴다(grey 램프가 받는다),
  ② 이미 생성된 소스는 Aseprite 마감에서 재킷을 grey/dark 램프로 재매핑한다. 네이비 램프
  신설은 grey 램프와 명도 구간이 겹쳐 얼룩 위험(.gpl 설계 규칙)이라 채택하지 않는다.

## 4. 생성 레시피 스냅샷 (v1 검증분, 2026-07-29 — **스타일 기준으로는 구식**)

> **v2 주의**: 아래 스냅샷은 v1의 뭉툭한 2등신 치비를 검증한 기록이다. 체크포인트·LoRA·
> 파라미터·실측 교훈(마젠타 배경 누출, 색이름 이중 표기)은 계속 유효하지만, **프롬프트의
> 비율·복장 어휘는 v2 계약(2.5~3등신·스트리트웨어·컬러 헤어)으로 재작성해야 한다.**

### v2 확정 스택 (2026-07-30 검증 — 이후 액터 발주는 전부 이 레인)

- **레시피 SSOT**: `comfyui/recipes/actor-chibi-base-v1.yaml`
  (워크플로 타입 `sdxl-txt2img-openpose`, 4방향 샷). 손실행은
  `comfyui/actor-chibi-openpose-sdxl.api.json` + `comfy_batch.py`.
- 체크포인트 `animagine-xl-4.0-opt.safetensors`(부루 태그 문법 — `1boy/1girl, chibi, ...` +
  `masterpiece, high score, great score, absurdres` 품질 꼬리) + PixelArtRedmond 0.45.
  euler_ancestral / cfg 5.0 / steps 28 / 1024².
- **비율·포즈**: xinsir SDXL OpenPose ControlNet(strength 0.9, end 0.8) +
  `guides/openpose/actor-chibi-*.png` 4방향 골격. IPAdapter는 미설치라 v2에서 미사용 —
  스타일은 체크포인트+태그가 진다.
- **판정**: 리뷰 시트에 96×128 행 계약(crown 8/feet 122) 게임 프리뷰가 자동 동봉된다.
  원본 1024로 비율·톤을 판정하지 않는다(§2-b·§4-b).
- 실측 교훈(v2 추가): 정체성 색을 넓게 쓰면(시안 트림 과다) 신호색 규칙과 충돌한다 —
  네거티브에 `cyan outfit, glowing weapon` 계열을 유지할 것.

`actor-idle.api.json` 그래프 그대로, 프롬프트만 교체. 재현값:

- checkpoint `zavychromaxl_v100.safetensors` + LoRA
  `project-c-pixelart-redmond-sdxl-v1-lite64.safetensors` 0.45/0.45
- 1024², steps 28, cfg 6.0, dpmpp_2m/karras, denoise 1.0
- 아군 검증 배치: seed `52101` batch 4 → `output/chibi-explorer-v2/` (4/4 합격)
- 적 검증 배치: seed `52201` batch 2 → `output/chibi-raider-v1/`

핵심 프롬프트 어휘(아군): `chibi, super-deformed proportions, two heads tall, oversized head,
visible friendly face with expressive eyes, hood down, gas mask hanging on chest strap as
accessory, teal turquoise scarf, plain flat uniform light gray studio background`

핵심 네거티브: `realistic proportions, tall slender body, small head, gas mask worn on face,
covered face, hood up, pink scarf, magenta clothing`

주의(실측):

- 프롬프트에 "magenta background"를 쓰면 **마젠타가 의상으로 샌다.** 배경은 중성 회색 단색으로
  받고 conform의 `key_color: auto`(테두리 연결 영역 키잉)로 제거한다.
- 진영색은 색이름을 이중으로 박는다(`teal turquoise scarf`) — 단독 색이름은 자주 무시된다.

## 4-b. 컨셉 뷰 ≠ 게임 뷰 (2026-07-30 실측)

정면 스탠딩 컨셉을 실제 던전 캡처에 실측 배율로 합성해 확인한 결과:

- **성립**: 치비 머리 비중·실루엣·크기는 게임 배율(카메라 0.72)에서도 얼굴이 살아 판독된다.
- **불성립 1 — 뷰**: 정면 뷰는 아이소 타일에 "붙지" 않는다. 게임 투입본은 **3/4 부감 + 4방향**이
  필수이며, 이는 컨셉 승인 뒤 방향별 키프레임 단계(기존 파이프라인 계보)가 진다.
  치비 비율 골격은 `guides/openpose/actor-chibi-*.png`(BODY_18, `generate_openpose_guides.py`의
  `actor-chibi` 프로파일)를 쓴다 — 기존 리얼 비율 가이드를 쓰면 비율이 회귀한다(실측: denoise
  0.88에서 골격이 img2img 스타일 소스보다 우선한다. 0.7대에서는 소스 비율이 샌다).
- **불성립 2 — 톤**: 원본 셀 렌더는 환경 대비 너무 밝고 차갑다. Torchstone conform(다운스케일+
  팔레트 잠금)이 흡수할 몫이므로, **컨셉 원본으로 톤을 판정하지 않는다** — 판정은 conform 후
  게임 캡처에서 한다.

## 4-c. 원정자 정식 컨셉 후보 채택 (2026-07-30) → 초안 승격 (2026-07-31)

`concept-final-v1` 배치의 **필드 메딕 생존자**(블론드+주황 팁 포니테일)를 원정자 정식 디자인
후보로 채택했다. 정체성 레퍼런스·재현값·**게이트 진행 기록**(팔레트 append 실측, 4방향 C01
채택, 키포즈 채택, 초안 조립)은
`reference/ref-cyberpunk-05-expeditioner-medic-concept.{png,prompt.md}`가 소유하고,
게임 스케일 판정 근거는 `docs/captures/expeditioner-medic-concept-game-preview-v1.png`다.
`actor-knight.aseprite`는 라이더 초안 → **메딕 자동 조립 초안**(11프레임/6태그)으로
교체됐다. 최종 마감(보간·발 기준선·실루엣)은 여전히 Aseprite 손작업 몫이다.
팔레트에는 `hair-blonde-1/2`가 추가됐다(절도 검사 통과 — 근거는 위 prompt.md).

위 문단은 2026-07-31의 역사 기록이다. 2026-08-02에는 프레임 사이 해부가 깨진 자동 조립 초안을
`project-c-expeditioner-grounded-source-v1.{png,prompt.md}` 기반의 접지 정적본으로 교체했다.
`process_actor_knight_grounded_v1.py`가 하드 알파·24색 역할 팔레트·2×2 클러스터·한 발 기준선을
적용했다. 2026-08-03에는 이 승인 `Frame_0`을 태그 밖 첫 프레임으로 보존한 채 방향별 6상태
24태그를 정식 `actor-knight.aseprite`에 승격했다. 전수 프레임과 PC Game View 승인이 끝나
`SurvivorAnimationApproved=true`이며, 세부 계보는
`reference/ref-expeditioner-directional-animation-v1.prompt.md`가 소유한다.

## 5. 적용 순서

1. 원정자(`actor-player`/`actor-knight` 슬롯) 후보 확정 → 팔레트 append → conform →
   `Art/Source/Aseprite/` 승격. `player-character-vertical-slice.md`의 장비 중립 원칙 유지.
2. 개선 플랜 §배치 2의 액터 재생성(기존 8종 + slinger/grave-warden)은 **전부 이 계약으로** 뽑는다.
   기존 리얼 비율 프롬프트(`actor-concept-sdxl-v1.yaml` 등)는 이 계약과 충돌 — 재사용 금지.
3. 애니메이션 레시피(OpenPose 가이드)는 치비 비율에 맞는 가이드 재작성이 필요하다 —
   기존 BODY_18 가이드는 리얼 비율 기준이므로 그대로 쓰면 비율이 되돌아간다.

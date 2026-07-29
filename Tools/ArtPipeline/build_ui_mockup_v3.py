#!/usr/bin/env python3
"""Generate the v3 HUD redesign mockup (self-contained HTML).

v2 illustrated a *fake* scene with CSS gradients at an arbitrary scale. v3 is a
pixel-exact preview instead: every board is the 640x360 logical canvas rendered
at the integer scale 2, i.e. exactly what the 1280x720 build will show. The
backdrop is a real HUD-free engine capture, so legibility is judged over actual
busy pixel art rather than flat black.

Rules the boards obey (these are the point of the redesign):
  - font sizes only from {9, 18, 27, 36, 54} logical  -> {18, 36, 54, 72, 108} at z=2
  - no synthetic bold, no fractional letter-spacing
  - exactly two chrome idioms: plate (HUD) and window (modals)
  - one gold element per screen

Output is body-content HTML (inline <style>), valid both as a repo file and as
a published Artifact page.
"""
import base64
import io
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / "Assets/_Project/Art/Runtime"
CAP = ROOT / "docs/captures"
FONT = ROOT / "Assets/_Project/UI/Fonts/Galmuri9.ttf"
OUT = ROOT / "docs/art-direction/project-c-ui-mockups-v3.html"

Z = 2  # integer panel scale being proposed for 1280x720


def z(n):
    """logical px -> board px"""
    return int(n * Z)


SPRITES = {
    "heart-full": RT / "ui-heart-full.png",
    "heart-empty": RT / "ui-heart-empty.png",
    "potion": RT / "item-potion.png",
    "bomb": RT / "item-bomb.png",
    "frost": RT / "item-frost-bomb.png",
    "herb": RT / "item-herb.png",
    "knife": RT / "item-throwing-knife.png",
    "relic": RT / "item-relic.png",
    "backpack": RT / "ui-backpack.png",
    "menu": RT / "ui-menu.png",
    "settings": RT / "ui-settings.png",
    "melee": RT / "ui-melee.png",
    "wait": RT / "ui-wait.png",
    "interact": RT / "ui-interact.png",
    "rot-l": RT / "ui-rotate-left.png",
    "rot-r": RT / "ui-rotate-right.png",
    "window": RT / "ui-window-frame.png",
}
PLATES = {
    "scene": CAP / "dungeon-common-tone-b7-world.png",      # HUD-free dungeon
    "before": CAP / "lobby-game-scale-dungeon-1280x720.png",  # current build
    "hub": CAP / "art-v2-hub-world.png",
}


def datauri(path: Path) -> str:
    return "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode()


SPR = {k: datauri(v) for k, v in SPRITES.items() if v.exists()}
IMG = {k: datauri(v) for k, v in PLATES.items() if v.exists()}
for k, v in list(SPRITES.items()) + list(PLATES.items()):
    if not v.exists():
        print(f"  ! missing asset: {v}")


def sp(name, cls="", w=None, h=None):
    if name not in SPR:
        return f'<span class="px {cls}"></span>'
    st = ""
    if w:
        st = f' style="width:{z(w)}px;height:{z(h or w)}px"'
    return f'<img class="px {cls}" src="{SPR[name]}" alt=""{st}>'


def hearts(full=5, total=5):
    out = []
    for i in range(total):
        out.append(sp("heart-full" if i < full else "heart-empty", "heart", 12, 11))
    return "".join(out)


def floor_stack(floors, current, explored_to, up=True, down=False):
    """Zone D - spatial verticality rail. floors listed top (highest) first.

    The up/down caps mean "a route exists from where you stand", so they sit
    against the current floor, not at the ends of the whole rail.
    """
    rows = []
    ci = floors.index(current)
    for idx, name in enumerate(floors):
        cur = idx == ci
        state = "cur" if cur else ("seen" if idx >= floors.index(explored_to) else "unseen")
        if cur and up:
            rows.append('<div class="fs-cap">▲</div>')
        label = f'<i class="fs-name">{name}</i>' if cur else ""
        rows.append(f'<div class="fs-row fs-{state}"><b class="fs-rung"></b>{label}</div>')
        if cur and down:
            rows.append('<div class="fs-cap">▼</div>')
    return "".join(rows)


def log_lines(lines):
    out = []
    n = len(lines)
    for i, t in enumerate(lines):
        cls = "ml-new" if i == n - 1 else "ml-old"
        out.append(f'<div class="ml {cls}">{t}</div>')
    return "".join(out)


def slot(icon, label, qty, key, dim=False):
    cls = "qs qs-dim" if dim else "qs"
    return (f'<div class="{cls}"><i class="qs-key">{key}</i>{sp(icon, "qs-ic", 14, 14)}'
            f'<span class="qs-qty">{qty}</span><span class="qs-lb">{label}</span></div>')


def grid_cells(items):
    """6x4 backpack at pitch 40 logical."""
    cells = []
    for r in range(4):
        for c in range(6):
            cells.append(f'<div class="bp-cell" style="left:{z(c*40)}px;top:{z(r*40)}px"></div>')
    for it in items:
        icon, c, r, w, h, sel = it
        cls = "bp-item" + (" bp-sel" if sel else "")
        cells.append(
            f'<div class="{cls}" style="left:{z(c*40)}px;top:{z(r*40)}px;'
            f'width:{z(w*40)}px;height:{z(h*40)}px">{sp(icon, "bp-ic")}</div>')
    return "".join(cells)


# --------------------------------------------------------------------------
# Board fragments
# --------------------------------------------------------------------------

def zone_vitals(chips=""):
    return f"""
    <div class="zA plate">
      <div class="hp-row">{hearts(4)}<span class="hp-val">8/10</span></div>
    </div>
    {chips}"""


CHIPS = """
    <div class="zA2">
      <div class="chip chip-burn"><b></b>화상 2</div>
      <div class="chip chip-poison"><b></b>중독 3</div>
      <div class="chip chip-warn"><b></b>허기</div>
    </div>"""


def zone_instrument(current="B2", up=True, down=False):
    """Zone C+D as ONE plate.

    UI_DESIGN_SYSTEM.md:128 already asks for "층 정보와 미니맵은 하나의 우상단
    계기 묶음". Leaving the floor rail floating bare beside the map would
    reintroduce exactly the orphan-chrome problem this redesign removes.
    """
    fl = ["8F", "7F", "6F", "5F", "4F", "3F", "2F", "1F", "B1", "B2"]
    return f"""
    <div class="zC plate">
      <div class="tools">
        <button class="tl tl-on">FOV</button>
        <button class="tl">{sp("settings", "", 10, 10)}</button>
        <button class="tl">{sp("menu", "", 10, 10)}</button>
      </div>
      <div class="inst-body">
        <div class="zD">{floor_stack(fl, current, "B1", up, down)}</div>
        <div class="minimap"><i class="mm-room"></i><i class="mm-room mm-r2"></i><i class="mm-you"></i></div>
      </div>
      <div class="viewrow">
        <button class="vb">{sp("rot-l", "", 10, 10)}</button>
        <span class="vlabel">VIEW 1/4</span>
        <button class="vb">{sp("rot-r", "", 10, 10)}</button>
      </div>
    </div>"""


def zone_log(lines):
    return f'<div class="zE plate">{log_lines(lines)}</div>'


def zone_rail(interact=True):
    inter = (f'<button class="ab">{sp("interact", "ab-ic", 12, 12)}<span>오르기</span>'
             f'<kbd>Space</kbd></button>') if interact else ""
    return f"""
    <div class="zF">
      <div class="quick plate">
        {slot("potion", "물약", "3", "1")}
        {slot("bomb", "폭탄", "1", "2")}
        {slot("frost", "냉기", "0", "3", dim=True)}
        {slot("backpack", "가방", "", "I")}
      </div>
      <div class="dock plate">
        <button class="ab ab-mode">{sp("melee", "ab-ic", 12, 12)}<span>근접</span></button>
        <button class="ab">{sp("wait", "ab-ic", 12, 12)}<span>대기</span><kbd>X</kbd></button>
        {inter}
        <span class="turn">내 턴</span>
      </div>
    </div>"""


BOSS = """
    <div class="zB plate boss">
      <div class="boss-k">최상층 · 봉인된 구역</div>
      <div class="boss-n">감시자</div>
      <div class="boss-track"><i style="width:62%"></i></div>
      <div class="boss-o">출구는 이것이 쓰러져야 열린다</div>
    </div>"""

DISCOVERY = """
    <div class="zB plate disc">
      <div class="disc-k">수직 경로 발견</div>
      <div class="disc-t">사다리</div>
      <div class="disc-d">위층으로 오른다 · 행동 1회</div>
    </div>"""


def board(inner, scene=True, cls=""):
    bg = f'<img class="scene px" src="{IMG["scene"]}" alt="">' if scene and "scene" in IMG else ""
    vig = '<i class="vignette"></i><i class="amber amber-a"></i><i class="amber amber-b"></i>' if scene else ""
    return f'<div class="board {cls}">{bg}{vig}<div class="hud">{inner}</div></div>'


# --------------------------------------------------------------------------
# Inventory board
# --------------------------------------------------------------------------
INV_ITEMS = [
    ("potion", 0, 0, 1, 1, True), ("herb", 1, 0, 1, 1, False),
    ("bomb", 2, 0, 1, 1, False), ("knife", 0, 1, 1, 1, False),
    ("relic", 3, 1, 1, 2, False), ("frost", 1, 1, 1, 1, False),
]

INVENTORY = f"""
    <div class="scrim"></div>
    <div class="win inv">
      <div class="win-hd"><span class="win-t">소지품</span>
        <span class="win-cap">14 / 24 칸</span>
        <button class="win-x">×</button></div>
      <div class="inv-body">
        <div class="inv-grid">{grid_cells(INV_ITEMS)}</div>
        <div class="inv-detail">
          <div class="det-hd">{sp("potion", "det-ic", 16, 16)}<span>회복 물약</span></div>
          <div class="det-sz">1×1 칸 · 3개 보유</div>
          <div class="det-desc">HP를 회복한다. 마시는 데 행동 1회를 소비한다.</div>
          <div class="det-stat"><span>회복</span><b>+4 HP</b></div>
          <div class="det-stat"><span>무게</span><b>0.5</b></div>
          <div class="det-gauge"><span>남은 충전</span><div class="bar"><i style="width:60%"></i></div></div>
          <button class="det-act">마시기</button>
        </div>
      </div>
      <div class="inv-craft">
        <div class="cr-hd">현장 조합 · 행동 1회</div>
        <div class="cr-row"><span>약초 ×2 → 회복 물약</span><button class="cr-b">조합</button></div>
        <div class="cr-row cr-no"><span>폭발 가루 ×2 → 폭탄</span><button class="cr-b cr-bd">재료 부족</button></div>
      </div>
    </div>"""

HUB = f"""
    <div class="zA plate hub-cur"><span class="hub-lb">캠프</span><span class="hub-v">$1,240</span></div>
    <div class="zC plate hub-tools">
      <button class="tl">{sp("settings", "", 10, 10)}</button>
      <button class="tl">{sp("menu", "", 10, 10)}</button>
    </div>
    <div class="zE plate">
      <div class="ml ml-new">대장간에서 단검을 벼렸다</div>
      <div class="ml ml-old">의뢰 2건이 남았다</div>
    </div>
    <div class="zF">
      <div class="quick plate">
        {slot("backpack", "적재", "", "I")}
        {slot("relic", "창고", "", "S")}
      </div>
      <div class="dock plate">
        <button class="ab ab-mode"><span>포탈로 출정</span><kbd>Enter</kbd></button>
      </div>
    </div>"""

MAINMENU = """
    <div class="mm-card win">
      <div class="mm-t">이상 미궁</div>
      <div class="mm-s">폐병원 · 상승</div>
      <div class="mm-btns">
        <button class="mm-b mm-p">이어하기</button>
        <button class="mm-b">새 원정</button>
        <button class="mm-b">설정</button>
      </div>
    </div>"""

LOG4 = ["약탈자의 공격 · 2 피해", "약탈자를 쳤다 · 3 피해 ×2",
        "화상 · 1 피해", "사다리를 찾았다"]

# --------------------------------------------------------------------------
BODY = f"""
<div class="wrap">

  <header class="lead">
    <p class="eyebrow">Torchstone · v3 · 시인성 재설계</p>
    <h1>HUD 레이아웃 시안</h1>
    <p class="sub">논리 캔버스 <b>640×360</b>을 정수 배율 <b>2</b>로 렌더한 보드다 —
    즉 아래 화면은 1280×720 빌드의 <b>픽셀 1:1 프리뷰</b>다. 1920×1080은 배율 3,
    2560×1440은 배율 4로 같은 배치가 그대로 확대된다. 배경은 HUD를 뺀 실제 엔진 캡처라
    시인성을 진짜 씬 위에서 판단할 수 있다.</p>
  </header>

  <section class="sec">
    <div class="sec-hd"><h2>1 · 던전 HUD — 평상시</h2>
      <p>앵커 6개를 4구역으로 접었다. 크롬 어휘는 <b>플레이트</b> 하나뿐이고,
      골드는 층 스택의 현재 층 한 곳에만 쓴다.</p></div>
    {board(zone_vitals() + zone_instrument() + zone_log(LOG4[1:]) + zone_rail())}
    <div class="legend">
      <span class="lg"><i class="lg-a"></i>A 바이탈 — 하트 + 상태이상 칩</span>
      <span class="lg"><i class="lg-b"></i>B 과도 밴드 — 보스 / 발견 카드</span>
      <span class="lg"><i class="lg-c"></i>C 계기 묶음 — 도구 · 층 레일 · 미니맵 · 시점</span>
      <span class="lg"><i class="lg-d"></i>D 층 레일 — 공간적 수직성 (C 안에 든다)</span>
      <span class="lg"><i class="lg-e"></i>E 메시지 로그 — 신규</span>
      <span class="lg"><i class="lg-f"></i>F 하단 레일 — 퀵바 + 행동 도크</span>
    </div>
  </section>

  <section class="sec">
    <div class="sec-hd"><h2>2 · 던전 HUD — 보스 · 상태이상 · 로그 4줄</h2>
      <p>정보가 가장 많이 뜬 최악의 경우다. 상태이상 칩(기둥 ②)과 메시지 로그가
      동시에 살아 있고, 과도 밴드는 보스가 점유한다.</p></div>
    {board(zone_vitals(CHIPS) + BOSS + zone_instrument("7F", up=True, down=True)
           + zone_log(LOG4) + zone_rail())}
  </section>

  <section class="sec">
    <div class="sec-hd"><h2>3 · 인벤토리 모달</h2>
      <p>모달만 <b>창</b> 크롬(<code>ui-window-frame.png</code> 9-slice)을 쓴다.
      격자 피치는 56 → 40으로 줄여 640 캔버스에 상세 페인까지 들어간다.
      골드 슬래브였던 사용 버튼은 어두운 바탕 + 골드 글자로 내렸다.</p></div>
    {board(INVENTORY)}
  </section>

  <section class="sec">
    <div class="sec-hd"><h2>4 · 허브 · 메인 메뉴</h2>
      <p>허브가 던전과 같은 구역·어휘를 쓴다 — 좌상 바이탈 자리엔 소지금,
      우상 계기 자리엔 도구, 하단 레일은 그대로다.</p></div>
    <div class="pair">
      {board(HUB, cls="b-hub")}
      {board(MAINMENU, scene=False, cls="b-mm")}
    </div>
  </section>

  <section class="sec">
    <div class="sec-hd"><h2>5 · 지금 / 제안</h2>
      <p>위는 현재 빌드 캡처, 아래는 같은 1280×720에서의 제안이다. 픽셀 대 픽셀로
      겹쳐 보라고 같은 폭으로 세로로 쌓았다. 바뀐 것은 크기만이 아니라
      <b>어휘의 수</b>다 — 여섯 갈래 크롬이 하나로 줄었다.</p></div>
    <div class="pair">
      <figure class="cmp"><img class="px" src="{IMG.get('before','')}" alt="현재 빌드">
        <figcaption>지금 — 크롬 6종, 깊이 정보 4줄, 로그 없음</figcaption></figure>
      <figure class="cmp">{board(zone_vitals(CHIPS) + zone_instrument()
                                 + zone_log(LOG4) + zone_rail())}
        <figcaption>제안 — 플레이트 1종, 층 스택, 로그 4줄, 상태이상 노출</figcaption></figure>
    </div>
  </section>

  <section class="notes">
    <h2>이 시안이 지키는 규칙</h2>
    <div class="tbl">
      <div class="tr th"><span>항목</span><span>규칙</span><span>이유</span></div>
      <div class="tr"><span>논리 캔버스</span><span>640×360 (배율 2 / 3 / 4)</span>
        <span>에디터 실측: 같은 2560×1440에서 HUD 잉크 폭이 361px → 465px, <b>+29%</b>.
        시인성은 여기서 나온다</span></div>
      <div class="tr"><span>폰트 크기</span><span>9 · 18 · 27 · 36 · 54 만</span>
        <span>Galmuri9은 9px 페이스다. 스케일을 하나로 묶어 위계를 만든다</span></div>
      <div class="tr"><span>굵기</span><span>합성 볼드 금지</span>
        <span>Galmuri9에 볼드 페이스가 없어 Unity가 래스터를 번지게 합성한다</span></div>
      <div class="tr"><span>자간</span><span>소수점 letter-spacing 금지</span>
        <span>어떤 배율에서도 도트 격자에 안 떨어진다</span></div>
      <div class="tr"><span>크롬</span><span>플레이트 / 창, 딱 둘</span>
        <span>맨 스프라이트·맨 텍스트·좌측 보더 칩이 섞여 잔해처럼 읽혔다</span></div>
      <div class="tr"><span>골드</span><span>화면당 하나</span>
        <span>지금은 층·전투 버튼·턴 배지가 동시에 금색이라 초점이 없다</span></div>
    </div>
    <p class="foot"><b>실측으로 기각된 가설</b>: 정수 패널 배율이 도트를 살린다(배율 3에서
    같은 글자 폭이 6px·5px로 오히려 갈렸다) · <code>fontRenderingMode</code>가 도트를
    살린다(캡처 바이트 동일). UI Toolkit은 글자마다 제 서브픽셀 위상에서 따로 래스터화하므로
    배율로는 못 고친다. 그래서 이 시안이 파는 것은 <b>선명도가 아니라 상대 크기·대비·그룹핑</b>이다.
    증거: <code>docs/captures/spike-*.png</code>.</p>
    <p class="foot">스프라이트·폰트는 실제 런타임 에셋(Galmuri9 사용 글자 서브셋 인라인).
    비네트와 앰버 광원 웅덩이는 Phase 5의 제안을 CSS로 근사한 것이다 —
    엔진에서는 9-slice 스프라이트와 기존 <code>GridLighting</code>으로 구현한다.</p>
  </section>
</div>
"""

# ---- collect charset & subset the font ------------------------------------
text_only = re.sub(r"<[^>]+>", " ", BODY)
charset = "".join(sorted(set(text_only))) + "0123456789/×·▲▼()•—+"
font_face = ""
try:
    from fontTools import subset as ftsub
    opts = ftsub.Options()
    opts.set(layout_features="*", glyph_names=False, notdef_outline=True,
             recalc_bounds=True, drop_tables=[])
    f = ftsub.load_font(str(FONT), opts)
    ss = ftsub.Subsetter(options=opts)
    ss.populate(text=charset)
    ss.subset(f)
    buf = io.BytesIO()
    f.save(buf)
    f.close()
    font_face = ("@font-face{font-family:'Galmuri9';font-display:block;"
                 f"src:url(data:font/ttf;base64,{base64.b64encode(buf.getvalue()).decode()})"
                 " format('truetype');}")
    print(f"font subset: {len(charset)} chars -> {len(buf.getvalue())//1024} KB")
except Exception as e:  # pragma: no cover
    print("font subset FAILED, falling back to monospace:", e)

# ---- CSS ------------------------------------------------------------------
CSS = font_face + f"""
*{{box-sizing:border-box;margin:0;padding:0}}
:root{{
  --void:#05070C; --panel:#0A0D13; --inset:#07090E;
  --stone:#98866F; --stone-lit:#CFC0AE; --stone-dim:#4A4038;
  --gold:#FFD554; --torch:#FFBD41; --gold-deep:#9A6B22;
  --teal:#4FA7A0; --ice:#9ADFE8; --teal-bg:#14343A;
  --hp:#D8452A; --xp:#7FB241; --warning:#F0492A; --hazard:#E0A62B;
  --text:#EADFC8; --dim:#97907E;
  --page:#0A0D13; --page2:#070910; --rule:#1B212A; --label:#7E8894;
  --pf:'Galmuri9','Courier New',monospace;
  --z:{Z};
}}
html{{background:#06080d}}
body{{font-family:var(--pf);color:var(--text);background:var(--page2);
  -webkit-font-smoothing:none;font-smooth:never;line-height:1.55;
  padding:36px 20px 64px}}
img.px,.px{{image-rendering:pixelated;display:block}}
.wrap{{max-width:1320px;margin:0 auto;display:flex;flex-direction:column;gap:44px}}

/* ---- page chrome (deliberately quiet: the boards are the subject) ---- */
.lead{{display:flex;flex-direction:column;gap:8px;
  border-left:2px solid var(--gold-deep);padding-left:16px}}
.eyebrow{{font-size:12px;letter-spacing:.24em;color:var(--torch)}}
.lead h1{{font-size:27px;font-weight:400;color:var(--stone-lit);text-wrap:balance}}
.sub{{font-size:14px;color:var(--dim);max-width:70ch;line-height:1.7}}
.sub b,.sec-hd b{{color:var(--text);font-weight:400}}
.sec{{display:flex;flex-direction:column;gap:14px}}
.sec-hd h2{{font-size:18px;font-weight:400;color:var(--stone-lit);margin-bottom:4px}}
.sec-hd p{{font-size:13px;color:var(--dim);max-width:78ch;line-height:1.7}}
code{{font-family:var(--pf);color:var(--ice)}}
.notes{{border:1px solid var(--rule);background:var(--page);padding:24px;
  display:flex;flex-direction:column;gap:14px}}
.notes h2{{font-size:18px;font-weight:400;color:var(--stone-lit)}}
.tbl{{display:flex;flex-direction:column;border:1px solid var(--rule);
  overflow-x:auto}}
.tr{{display:grid;grid-template-columns:104px 190px 1fr;gap:14px;padding:9px 14px;
  border-top:1px solid #12161d;font-size:13px;min-width:640px}}
.tr:first-child{{border-top:none}}
.tr.th{{background:#0C1017;color:var(--label);font-size:12px;letter-spacing:.05em}}
.tr span:first-child{{color:var(--text)}}
.tr span:nth-child(2){{color:var(--ice)}}
.tr span:last-child{{color:var(--dim)}}
.foot{{font-size:12px;color:var(--label);line-height:1.7;max-width:80ch}}
.legend{{display:flex;flex-wrap:wrap;gap:6px 18px;font-size:12px;color:var(--label)}}
.lg{{display:flex;align-items:center;gap:6px}}
.lg i{{width:9px;height:9px;display:block;border:1px solid #000}}
.lg-a{{background:var(--hp)}} .lg-b{{background:var(--warning)}}
.lg-c{{background:var(--teal)}} .lg-d{{background:var(--gold)}}
.lg-e{{background:var(--stone)}} .lg-f{{background:var(--stone-dim)}}
/* Boards are pixel-exact, so they are never fluid — a "pair" stacks instead of
   squeezing, which would leave the px-sized children overflowing the frame. */
.pair{{display:flex;flex-direction:column;gap:22px;align-items:flex-start}}
.cmp{{display:flex;flex-direction:column;gap:8px}}
.cmp img{{width:{z(640)}px;max-width:100%;height:auto;border:1px solid var(--rule)}}
.cmp figcaption{{font-size:12px;color:var(--label)}}

/* ---- the board: 640x360 logical at scale {Z} ---- */
.board{{position:relative;width:{z(640)}px;height:{z(360)}px;flex:none;
  background:var(--void);border:1px solid var(--rule);overflow:hidden;
  font-size:{z(9)}px}}
.scene{{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);
  width:{z(464)}px;height:auto}}
.vignette{{position:absolute;inset:0;pointer-events:none;
  background:radial-gradient(124% 104% at 50% 54%,transparent 46%,rgba(5,7,12,.34) 78%,rgba(5,7,12,.72) 100%)}}
/* lamp pools sit on the wall sconces of this plate, not floating in the room */
.amber{{position:absolute;pointer-events:none;border-radius:50%;
  background:radial-gradient(circle,rgba(255,189,65,.26),rgba(255,150,40,.07) 38%,transparent 66%)}}
.amber-a{{left:41.5%;top:36%;width:9%;height:13%}}
.amber-b{{left:50.5%;top:33%;width:8%;height:11%}}

.hud{{position:absolute;inset:0;pointer-events:none}}
.hud>*{{position:absolute}}
.hud button{{pointer-events:auto;font-family:var(--pf);color:var(--text);
  cursor:pointer;background:none;border:none;font-size:{z(9)}px;line-height:1}}
.plate{{background:rgba(5,7,12,.78);border:1px solid var(--stone-dim)}}

/* A - vitals */
.zA{{left:{z(8)}px;top:{z(8)}px;padding:{z(3)}px {z(4)}px}}
.hp-row{{display:flex;align-items:center;gap:{z(2)}px}}
.hp-val{{margin-left:{z(4)}px;color:var(--dim);font-variant-numeric:tabular-nums}}
.zA2{{left:{z(8)}px;top:{z(31)}px;display:flex;gap:{z(3)}px}}
.chip{{display:flex;align-items:center;gap:{z(3)}px;padding:{z(2)}px {z(4)}px;
  background:rgba(5,7,12,.78);border:1px solid var(--stone-dim);color:var(--dim)}}
.chip b{{width:{z(4)}px;height:{z(4)}px;display:block}}
.chip-burn b{{background:var(--torch)}} .chip-burn{{color:var(--torch)}}
.chip-poison b{{background:var(--xp)}} .chip-poison{{color:var(--xp)}}
.chip-warn b{{background:var(--warning)}} .chip-warn{{color:var(--warning)}}

/* B - transient band */
.zB{{left:50%;transform:translateX(-50%);top:{z(8)}px;width:{z(288)}px;
  padding:{z(5)}px {z(8)}px}}
.boss-k,.disc-k{{color:var(--dim);font-size:{z(9)}px}}
.boss-n{{color:var(--warning);font-size:{z(18)}px;margin:{z(2)}px 0}}
.boss-track{{height:{z(4)}px;background:#2F0D11;margin:{z(3)}px 0}}
.boss-track i{{display:block;height:100%;background:var(--hp)}}
.boss-o{{color:var(--stone);font-size:{z(9)}px}}
.disc{{border-left:{z(3)}px solid var(--teal)}}
.disc-t{{color:var(--ice);font-size:{z(18)}px;margin:{z(2)}px 0}}
.disc-d{{color:var(--dim)}}

/* C+D - instrument cluster (tools / floor rail + minimap / view) */
.zC{{right:{z(8)}px;top:{z(8)}px;width:{z(132)}px;padding:{z(4)}px;
  display:flex;flex-direction:column;gap:{z(4)}px}}
.tools{{display:flex;gap:{z(4)}px}}
.tl{{flex:1;height:{z(14)}px;display:grid;place-items:center;
  background:var(--teal-bg);border:1px solid #0C2126;color:var(--teal)}}
.tl-on{{color:var(--ice)}}
.inst-body{{display:flex;gap:{z(4)}px;align-items:center}}
.minimap{{position:relative;flex:1;height:{z(96)}px;background:var(--inset);
  border:1px solid #0C1216}}
.mm-room{{position:absolute;left:24%;top:30%;width:34%;height:30%;
  background:#2B2A25;display:block}}
.mm-r2{{left:52%;top:52%;width:22%;height:20%;background:#1E1E1B}}
.mm-you{{position:absolute;left:38%;top:40%;width:{z(3)}px;height:{z(3)}px;
  background:var(--ice);display:block}}
.viewrow{{display:flex;align-items:center;justify-content:space-between}}
.vb{{width:{z(14)}px;height:{z(12)}px;display:grid;place-items:center;
  background:var(--teal-bg);border:1px solid #0C2126}}
.vlabel{{color:var(--dim);font-variant-numeric:tabular-nums}}

/* D - floor rail, inside the C plate. The current rung is the ONE gold element. */
.zD{{position:relative;width:{z(24)}px;flex:none;
  display:flex;flex-direction:column;align-items:flex-end;gap:{z(2)}px}}
.fs-row{{display:flex;align-items:center;justify-content:flex-end;
  height:{z(3)}px}}
/* unreached rungs must still read as a scale against the void, so they sit a
   step above --pc-inset rather than vanishing into it */
.fs-rung{{display:block;width:{z(10)}px;height:{z(3)}px;background:#1A1F27}}
.fs-seen .fs-rung{{background:var(--stone-dim)}}
.fs-cur .fs-rung{{width:{z(16)}px;background:var(--gold)}}
.fs-name{{font-style:normal;color:var(--gold);font-size:{z(9)}px;line-height:1;
  position:absolute;right:{z(18)}px;white-space:nowrap}}
.fs-cur{{position:relative;height:{z(5)}px}}
.fs-cap{{color:var(--stone-lit);font-size:{z(9)}px;line-height:1;
  height:{z(7)}px;align-self:flex-end;margin-right:{z(1)}px}}

/* E - message log */
.zE{{left:{z(8)}px;top:{z(272)}px;width:{z(208)}px;padding:{z(4)}px {z(5)}px;
  display:flex;flex-direction:column;gap:{z(2)}px}}
.ml{{font-size:{z(9)}px;line-height:1.2;white-space:nowrap;overflow:hidden;
  text-overflow:ellipsis}}
.ml-old{{color:var(--dim)}} .ml-new{{color:var(--text)}}

/* F - bottom rail */
.zF{{left:{z(8)}px;right:{z(8)}px;bottom:{z(8)}px;height:{z(24)}px;
  display:flex;justify-content:space-between;align-items:stretch}}
.quick{{display:flex}}
.qs{{position:relative;width:{z(44)}px;display:flex;align-items:center;
  justify-content:center;gap:{z(3)}px;border-right:1px solid #171C24}}
.qs:last-child{{border-right:none}}
.qs-dim{{opacity:.42}}
.qs-key{{position:absolute;left:{z(3)}px;top:{z(2)}px;font-style:normal;
  font-size:{z(9)}px;color:var(--gold-deep);line-height:1}}
.qs-qty{{color:var(--dim);font-variant-numeric:tabular-nums}}
.qs-lb{{display:none}}
.dock{{display:flex;align-items:stretch}}
.ab{{display:flex;align-items:center;gap:{z(4)}px;padding:0 {z(7)}px;
  border-right:1px solid #171C24}}
.ab:last-of-type{{border-right:none}}
.ab-mode{{color:var(--ice)}}
.ab kbd{{font-family:var(--pf);font-size:{z(9)}px;color:var(--stone-dim)}}
.turn{{display:flex;align-items:center;padding:0 {z(7)}px;color:var(--dim);
  border-left:1px solid #171C24}}

/* ---- window chrome (modals only) ---- */
.scrim{{position:absolute;inset:0;background:rgba(5,7,12,.82)}}
.win{{background:#0D1016;border:{z(2)}px solid var(--stone-dim);
  outline:1px solid #000;outline-offset:{z(1)}px}}
.inv{{left:50%;top:50%;transform:translate(-50%,-50%);width:{z(528)}px;
  padding:{z(8)}px;display:flex;flex-direction:column;gap:{z(6)}px}}
.win-hd{{display:flex;align-items:center;gap:{z(8)}px;
  border-bottom:1px solid var(--stone-dim);padding-bottom:{z(5)}px}}
.win-t{{font-size:{z(18)}px;color:var(--stone-lit)}}
.win-cap{{color:var(--dim);margin-left:auto;font-variant-numeric:tabular-nums}}
.win-x{{width:{z(14)}px;height:{z(14)}px;display:grid;place-items:center;
  border:1px solid var(--stone-dim);color:var(--dim);font-size:{z(9)}px}}
.inv-body{{display:flex;gap:{z(8)}px}}
.inv-grid{{position:relative;width:{z(240)}px;height:{z(160)}px;flex:none}}
.bp-cell{{position:absolute;width:{z(40)}px;height:{z(40)}px;
  border:1px solid #1A1F27;background:var(--inset)}}
.bp-item{{position:absolute;display:grid;place-items:center;
  border:1px solid var(--stone-dim);background:#12161D}}
.bp-sel{{border-color:var(--gold);background:#1F1710}}
.bp-ic{{width:{z(22)}px;height:{z(22)}px}}
.inv-detail{{flex:1;min-width:0;display:flex;flex-direction:column;gap:{z(4)}px}}
.det-hd{{display:flex;align-items:center;gap:{z(5)}px;font-size:{z(18)}px;
  color:var(--stone-lit)}}
.det-sz,.det-desc{{color:var(--dim);font-size:{z(9)}px;line-height:1.5}}
.det-stat{{display:flex;justify-content:space-between;font-size:{z(9)}px;
  color:var(--dim);border-top:1px solid #161B22;padding-top:{z(3)}px}}
.det-stat b{{color:var(--text);font-weight:400}}
.det-gauge{{display:flex;align-items:center;gap:{z(5)}px;font-size:{z(9)}px;
  color:var(--dim);margin-top:{z(2)}px}}
.bar{{flex:1;height:{z(5)}px;background:var(--inset);border:1px solid #1A1F27}}
.bar i{{display:block;height:100%;background:var(--teal)}}
.det-act{{margin-top:auto;height:{z(18)}px;background:#1F1710;
  border:1px solid var(--gold-deep);color:var(--gold);font-size:{z(9)}px}}
.inv-craft{{border-top:1px solid var(--stone-dim);padding-top:{z(5)}px;
  display:flex;flex-direction:column;gap:{z(3)}px}}
.cr-hd{{color:var(--dim);font-size:{z(9)}px}}
.cr-row{{display:flex;align-items:center;justify-content:space-between;
  background:var(--inset);border:1px solid #171C24;padding:{z(3)}px {z(5)}px;
  font-size:{z(9)}px}}
.cr-no{{opacity:.5}}
.cr-b{{border:1px solid var(--stone-dim);padding:{z(1)}px {z(5)}px;
  color:var(--text);font-size:{z(9)}px;background:none;font-family:var(--pf)}}
.cr-bd{{color:var(--stone-dim)}}

/* hub */
.hub-cur{{display:flex;align-items:center;gap:{z(6)}px;padding:{z(4)}px {z(6)}px}}
.hub-lb{{color:var(--dim)}}
.hub-v{{color:var(--gold);font-size:{z(18)}px;font-variant-numeric:tabular-nums}}
.hub-tools{{width:auto;flex-direction:row}}

/* main menu */
.b-mm{{background:linear-gradient(#0A0D13,#05070C)}}
.mm-card{{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);
  width:{z(400)}px;padding:{z(16)}px;display:flex;flex-direction:column;
  align-items:center;gap:{z(4)}px}}
.mm-t{{font-size:{z(36)}px;color:var(--stone-lit);letter-spacing:{z(4)}px}}
.mm-s{{font-size:{z(9)}px;color:var(--dim);margin-bottom:{z(8)}px}}
.mm-btns{{display:flex;flex-direction:column;gap:{z(4)}px;width:{z(176)}px}}
.mm-b{{height:{z(24)}px;background:#12161D;border:1px solid var(--stone-dim);
  color:var(--text);font-family:var(--pf);font-size:{z(9)}px;cursor:pointer}}
.mm-p{{background:#1F1710;border-color:var(--gold-deep);color:var(--gold)}}

/* Narrow viewports drop to the raw 640x360 logical canvas — still an integer
   step, so the pixel font stays crisp instead of resampling to a fractional one. */
@media (max-width:1340px){{
  .board{{zoom:.5}}
  .cmp img{{width:{z(320)}px}}
}}
@media (prefers-reduced-motion:reduce){{*{{animation:none!important;transition:none!important}}}}
"""

HTML = f"<style>{CSS}</style>\n{BODY}"
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text(HTML, encoding="utf-8")
print(f"wrote {OUT.relative_to(ROOT)}  ({len(HTML)//1024} KB)")

#!/usr/bin/env python3
"""Generate the Torchstone postapoc HUD design mockup (self-contained HTML).

Inlines the real game UI sprites + a subset of Galmuri9 so the mockup renders
in the exact engine pixels. Output is body-content HTML (inline <style>), valid
both as a standalone repo file and as a published Artifact page.
"""
import base64, io, re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / "Assets/_Project/Art/Runtime"
ENV = ROOT / "Assets/_Project/Art/Environment"
FONT = ROOT / "Assets/_Project/UI/Fonts/Galmuri9.ttf"
OUT = ROOT / "docs/art-direction/project-c-ui-mockups-v2.html"

SPRITES = {
    "heart-full": RT / "ui-heart-full.png",
    "heart-empty": RT / "ui-heart-empty.png",
    "potion": RT / "item-potion.png",
    "bomb": RT / "item-bomb.png",
    "frost": RT / "item-frost-bomb.png",
    "backpack": RT / "ui-backpack.png",
    "menu": RT / "ui-menu.png",
    "settings": RT / "ui-settings.png",
    "melee": RT / "ui-melee.png",
    "wait": RT / "ui-wait.png",
    "rot-l": RT / "ui-rotate-left.png",
    "rot-r": RT / "ui-rotate-right.png",
    "floor": ENV / "env-floor.png",
    "lampwall": ENV / "env-wall-torch-rising-right.png",
    "wall": ENV / "env-wall-rising-right.png",
    "player": RT / "actor-player.png",
}


def datauri(path: Path) -> str:
    b = path.read_bytes()
    return "data:image/png;base64," + base64.b64encode(b).decode()


def img(name, cls=""):
    return f'<img class="px {cls}" src="{SPR[name]}" alt="">'


SPR = {k: datauri(v) for k, v in SPRITES.items()}

# ---- BODY -----------------------------------------------------------------
BODY = f"""
<div class="wrap">
  <header class="lead">
    <p class="eyebrow">Torchstone · v1.6 · POSTAPOC</p>
    <h1>인게임 HUD 시안</h1>
    <p class="sub">collapsed-transit 던전 오버레이 — 실제 엔진 스프라이트 + Galmuri9로 렌더한
    UI Toolkit 이식용 시안. PC 가로(16:9). 씬이 주인공, HUD는 프레임 없는 플로팅.</p>
  </header>

  <div class="frame" role="img" aria-label="collapsed-transit 던전 HUD 시안">
    <!-- SCENE -->
    <div class="scene">
      <div class="iso-grid"></div>
      <div class="glow"></div>
      <div class="cluster">
        {img('lampwall','sc sc-lamp')}
        {img('wall','sc sc-wall')}
        {img('floor','sc sc-f1')}
        {img('floor','sc sc-f2')}
        {img('floor','sc sc-f3')}
        {img('player','sc sc-player')}
      </div>
    </div>

    <!-- HUD -->
    <div class="hud">
      <!-- top-left: HP -->
      <div class="hp">
        {''.join(img('heart-full','heart') for _ in range(4))}{img('heart-empty','heart')}
        <span class="hp-read">4 / 5</span>
      </div>

      <!-- top-right: gauge cluster -->
      <div class="gauge">
        <div class="floor-tag">
          <div class="dia dia--cur"><span>B2</span></div>
          <div class="floor-meta">
            <span class="run">연전 1 / 3</span>
            <span class="hgt">HEIGHT 0 · (0,0)</span>
            <span class="near"><i class="tri up"></i>B1&nbsp;&nbsp;<i class="tri dn"></i>B3</span>
          </div>
        </div>
        <div class="panel panel--bevel map">
          <div class="map-head">
            <span>던전 지도</span>
            <span class="tools">
              <button class="ic" aria-label="설정">{img('settings')}</button>
              <button class="ic" aria-label="메뉴">{img('menu')}</button>
            </span>
          </div>
          <div class="minimap">
            <span class="you"></span>
          </div>
          <div class="view">
            <button class="ic" aria-label="시점 왼쪽">{img('rot-l')}</button>
            <span>VIEW 1 / 4</span>
            <button class="ic" aria-label="시점 오른쪽">{img('rot-r')}</button>
          </div>
        </div>
      </div>

      <!-- center-low: status -->
      <div class="status">
        <span class="s-ready"><i class="dot"></i>READY</span>
        <span class="s-hint"><i class="dot dot--teal"></i>탐색해 수직 경로를 찾아라</span>
      </div>

      <!-- bottom-left: quickbar -->
      <div class="quickbar">
        <button class="slot"><span class="key">1</span>{img('potion','it')}<span class="lbl">POTION</span><span class="qty">×0</span></button>
        <button class="slot"><span class="key">2</span>{img('bomb','it')}<span class="lbl">BOMB</span><span class="qty">×0</span></button>
        <button class="slot"><span class="key">3</span>{img('frost','it')}<span class="lbl">FROST</span><span class="qty">×0</span></button>
        <button class="slot"><span class="key">4</span>{img('backpack','it')}<span class="lbl">BAG</span></button>
      </div>

      <!-- bottom-right: action dock -->
      <div class="dock panel--bevel">
        <div class="drow">
          <span class="mode"><b>MODE</b> PLAY FOV</span>
          <span class="atk">{img('melee','di')}<b>ATTACK</b> MELEE</span>
        </div>
        <div class="drow">
          <button class="act">{img('wait','di')}대기 <kbd>X</kbd></button>
          <button class="act act--cur"><i class="dot"></i>내 턴</button>
        </div>
      </div>
    </div>
  </div>

  <!-- PORT NOTES -->
  <section class="notes">
    <h2>UI Toolkit 이식 노트</h2>
    <p class="notes-sub">이 시안의 영역 → 기존 <code>DesignSystem.uss</code> 클래스·요소 계약. 색은 전부
    <code>var(--pc-*)</code> 토큰, 리터럴 없음. 확정 시 UXML(구조)/USS(레이아웃)로 거의 1:1 이식.</p>
    <div class="swatches">{''.join(
        f'<span class="sw"><i style="background:{c}"></i>{n}</span>'
        for n, c in [
            ('void','#05070C'),('concrete','#6B7178'),('stone','#98866F'),('stone-lit','#CFC0AE'),
            ('gold','#FFD554'),('torch','#FFBD41'),('teal','#4FA7A0'),('ice','#9ADFE8'),
            ('hp','#D8452A'),('hazard','#E0A62B'),('warning','#F0492A'),
        ])}</div>
    <div class="tbl">
      <div class="tr th"><span>영역</span><span>클래스 / 요소</span><span>규칙</span></div>
      <div class="tr"><span>HP 하트</span><span class="m">.pc-heart · ui-heart-full/empty</span><span>플로팅, 하드 섀도. HP 레드 고정</span></div>
      <div class="tr"><span>현재 층 B2</span><span class="m">.pc-dia--current</span><span>골드는 "현재·선택" 한 곳만</span></div>
      <div class="tr"><span>던전 지도</span><span class="m">.panel--bevel (승격: 9-slice)</span><span>계기 묶음. 틸 시스템 프레임</span></div>
      <div class="tr"><span>소모품 퀵바</span><span class="m">.pc-slot · .pc-slot-qty</span><span>아이콘+수량+숫자키. 0은 흐리게</span></div>
      <div class="tr"><span>행동 도크</span><span class="m">.pc-btn--action / --system</span><span>내 턴=골드, MODE=틸 시스템</span></div>
      <div class="tr"><span>발견 카드</span><span class="m">.pc-float-text--hint</span><span>1회성·비차단·자동 닫힘</span></div>
    </div>
    <p class="foot">깎인 모서리·골드 글로우·게이지 눈금은 USS box-shadow 부재로 9-slice 스프라이트 승격 예정
    (현재는 하드 보더로 근사). 폰트 Galmuri9(사용 글자 서브셋 인라인) · 스프라이트는 실제 런타임 에셋.</p>
  </section>
</div>
"""

# ---- collect charset from rendered text & subset the font -----------------
text_only = re.sub(r"<[^>]+>", " ", BODY)
charset = "".join(sorted(set(text_only))) + "0123456789/×·()•"
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
    b64 = base64.b64encode(buf.getvalue()).decode()
    font_face = ("@font-face{font-family:'Galmuri9';font-display:block;"
                 f"src:url(data:font/ttf;base64,{b64}) format('truetype');}}")
    print(f"font subset: {len(charset)} chars -> {len(buf.getvalue())//1024} KB")
except Exception as e:  # pragma: no cover
    print("font subset FAILED, falling back to monospace:", e)

# ---- CSS ------------------------------------------------------------------
CSS = font_face + r"""
*{box-sizing:border-box;margin:0;padding:0}
:root{
  --void:#05070C; --void2:#07090E; --panel:#0A0D13; --panel2:#12161d;
  --concrete:#6B7178; --concrete-dim:#3B3F45; --stone:#98866F; --stone-lit:#CFC0AE; --stone-dim:#4A4038;
  --gold:#FFD554; --torch:#FFBD41; --gold-deep:#9A6B22;
  --teal:#4FA7A0; --ice:#9ADFE8; --teal-bg:#14343A;
  --hp:#D8452A; --hp-empty:#45100B; --hazard:#E0A62B; --warning:#F0492A;
  --text:#EADFC8; --dim:#97907E;
  --ink:#0b0e14; --paper:#0e1219;
  --pf:'Galmuri9','Courier New',monospace;
}
html{background:#04050a}
body{font-family:var(--pf);color:var(--text);
  background:radial-gradient(120% 80% at 50% -10%,#0b0f17 0%,#05070c 60%,#04050a 100%);
  -webkit-font-smoothing:none;font-smooth:never;line-height:1.5;padding:32px 20px 56px}
img.px{image-rendering:pixelated;image-rendering:crisp-edges;display:block}
.wrap{max-width:1120px;margin:0 auto;display:flex;flex-direction:column;gap:28px}

/* lead */
.lead{display:flex;flex-direction:column;gap:6px;border-left:2px solid var(--gold-deep);padding-left:14px}
.eyebrow{font-size:12px;letter-spacing:.28em;color:var(--torch);text-transform:uppercase}
.lead h1{font-size:28px;font-weight:400;color:var(--stone-lit);text-wrap:balance}
.sub{font-size:14px;color:var(--dim);max-width:64ch}

/* frame */
.frame{position:relative;aspect-ratio:16/9;width:100%;overflow:hidden;
  background:#05070c;border:1px solid #1b2129;
  outline:1px solid #000;outline-offset:-2px}

/* scene */
.scene{position:absolute;inset:0}
.iso-grid{position:absolute;inset:0;opacity:.14;
  background-image:
   repeating-linear-gradient(26.57deg,transparent 0 30px,#2a3742 30px 31px),
   repeating-linear-gradient(-26.57deg,transparent 0 30px,#2a3742 30px 31px);
  -webkit-mask-image:radial-gradient(90% 80% at 46% 62%,#000 30%,transparent 85%);
          mask-image:radial-gradient(90% 80% at 46% 62%,#000 30%,transparent 85%)}
.glow{position:absolute;left:38%;top:32%;width:34%;height:44%;
  background:radial-gradient(circle,rgba(255,189,65,.34),rgba(255,150,40,.10) 40%,transparent 70%);
  filter:blur(2px);animation:flick 3.6s ease-in-out infinite}
.cluster{position:absolute;left:0;top:0;right:0;bottom:0}
.sc{position:absolute;image-rendering:pixelated}
.sc-lamp{width:150px;left:39%;top:30%}
.sc-wall{width:150px;left:53%;top:30%}
.sc-f1{width:200px;left:37%;top:53%}
.sc-f2{width:200px;left:47.5%;top:59%}
.sc-f3{width:200px;left:26.5%;top:59%}
.sc-player{width:120px;left:45%;top:44%;filter:drop-shadow(0 6px 0 rgba(0,0,0,.5))}

/* hud shell */
.hud{position:absolute;inset:0;pointer-events:none;font-size:18px}
.hud button{pointer-events:auto;font-family:var(--pf);color:var(--text);cursor:pointer;background:none;border:none}
.hud>*{position:absolute}
.hp{left:20px;top:16px;display:flex;align-items:center;gap:5px}
.heart{width:26px;height:23px}
.hp-read{font-size:18px;color:var(--dim);margin-left:6px;text-shadow:1px 1px 0 #000;
  font-variant-numeric:tabular-nums}

/* gauge cluster (top-right) */
.gauge{right:18px;top:14px;display:flex;flex-direction:column;align-items:flex-end;gap:8px}
.floor-tag{display:flex;align-items:center;gap:10px}
.dia{width:44px;height:44px;transform:rotate(45deg);background:#15110d;
  border:3px solid var(--stone-dim);display:grid;place-items:center}
.dia span{transform:rotate(-45deg);font-size:18px;color:var(--dim);font-variant-numeric:tabular-nums}
.dia--cur{width:52px;height:52px;border-color:var(--gold);
  box-shadow:0 0 10px rgba(255,213,84,.35)}
.dia--cur span{color:var(--gold);font-size:22px}
.floor-meta{display:flex;flex-direction:column;gap:2px;text-align:right;text-shadow:1px 1px 0 #000}
.floor-meta .run{color:var(--text)}
.floor-meta .hgt{font-size:18px;color:var(--dim);font-variant-numeric:tabular-nums}
.floor-meta .near{font-size:18px;color:var(--stone)}
.tri{display:inline-block;width:0;height:0;border-inline:5px solid transparent;vertical-align:middle;margin-right:3px}
.tri.up{border-bottom:7px solid var(--stone-lit)}
.tri.dn{border-top:7px solid var(--stone-lit)}

.panel{background:rgba(10,13,19,.92);border:2px solid var(--teal-bg)}
.panel--bevel{clip-path:polygon(0 8px,8px 0,100% 0,100% calc(100% - 8px),calc(100% - 8px) 100%,0 100%)}
.map{width:250px;padding:8px}
.map-head{display:flex;justify-content:space-between;align-items:center;color:var(--teal);font-size:18px}
.tools{display:flex;gap:4px}
.ic{width:30px;height:30px;display:grid;place-items:center;border:1px solid var(--stone-dim);background:#12161d}
.ic:hover{border-color:var(--teal)}
.ic img{width:20px;height:20px}
.minimap{height:96px;margin:6px 0;position:relative;background:
   linear-gradient(#0c1a1d,#081215);
  background-image:repeating-linear-gradient(#14343a55 0 23px,transparent 23px 24px),
                   repeating-linear-gradient(90deg,#14343a55 0 23px,transparent 23px 24px);
  border:1px solid #0a1416}
.you{position:absolute;left:52%;top:46%;width:8px;height:8px;background:var(--ice);
  box-shadow:0 0 8px var(--ice)}
.view{display:flex;justify-content:space-between;align-items:center;color:var(--dim);font-size:18px}
.view span{font-variant-numeric:tabular-nums}

/* status */
.status{left:50%;top:66%;transform:translateX(-50%);display:flex;flex-direction:column;
  align-items:center;gap:6px;text-shadow:1px 1px 0 #000;font-size:18px}
.s-ready{color:var(--text)} .s-hint{color:var(--ice)}
.dot{display:inline-block;width:7px;height:7px;background:var(--gold);margin-right:7px;vertical-align:middle}
.dot--teal{background:var(--teal)}

/* quickbar */
.quickbar{left:18px;bottom:16px;display:flex;gap:8px}
.slot{position:relative;width:74px;height:56px;background:#0b0f16;border:2px solid var(--stone-dim);
  display:flex;flex-direction:column;align-items:center;justify-content:center;gap:2px;
  pointer-events:auto;font-family:var(--pf)}
.slot:hover{border-color:var(--stone)}
.slot .it{width:26px;height:26px}
.slot .lbl{font-size:12px;letter-spacing:.06em;color:var(--dim)}
.slot .key{position:absolute;left:5px;top:3px;font-size:12px;color:var(--gold-deep)}
.slot .qty{position:absolute;right:5px;bottom:3px;font-size:12px;color:var(--dim);
  font-variant-numeric:tabular-nums}

/* dock */
.dock{right:18px;bottom:16px;width:328px;background:rgba(10,13,19,.94);
  border:2px solid var(--stone-dim);display:flex;flex-direction:column;padding:8px;gap:7px}
.drow{display:flex;gap:8px}
.drow>*{flex:1;display:flex;align-items:center;gap:7px;font-size:18px}
.mode,.atk{padding:6px 8px;background:#0b0f16;border:1px solid #1c232c}
.mode{color:var(--teal)} .mode b,.atk b{color:var(--dim);font-weight:400;font-size:12px;letter-spacing:.1em}
.atk{color:var(--text)} .di{width:20px;height:20px}
.act{padding:8px;background:#161b23;border:2px solid var(--stone-dim);
  display:flex;align-items:center;justify-content:center;gap:7px;font-size:18px;color:var(--text)}
.act kbd{font-family:var(--pf);font-size:12px;border:1px solid var(--stone-dim);padding:0 4px;color:var(--dim)}
.act--cur{color:var(--gold);background:#1f1710;border-color:var(--gold-deep);
  box-shadow:inset 0 0 0 1px rgba(255,213,84,.2)}
.act--cur .dot{background:var(--gold);box-shadow:0 0 8px var(--gold)}
.act:hover{border-color:var(--stone)}

/* notes */
.notes{border:1px solid #171c24;background:linear-gradient(#0a0d13,#080a10);padding:22px}
.notes h2{font-size:20px;font-weight:400;color:var(--stone-lit);margin-bottom:6px}
.notes-sub{font-size:14px;color:var(--dim);max-width:74ch;margin-bottom:16px}
.notes code{font-family:var(--pf);color:var(--ice);font-size:.92em}
.swatches{display:flex;flex-wrap:wrap;gap:8px 14px;margin-bottom:18px}
.sw{display:flex;align-items:center;gap:6px;font-size:12px;color:var(--dim);letter-spacing:.04em}
.sw i{width:16px;height:16px;border:1px solid #000;outline:1px solid #ffffff14;outline-offset:-1px}
.tbl{display:flex;flex-direction:column;border:1px solid #171c24;font-size:13px}
.tr{display:grid;grid-template-columns:120px 1fr 1fr;gap:10px;padding:8px 12px;border-top:1px solid #12161d}
.tr:first-child{border-top:none}
.tr.th{background:#0c1017;color:var(--dim);letter-spacing:.06em;font-size:12px}
.tr span:first-child{color:var(--text)}
.tr .m{color:var(--teal)}
.tr:not(.th) span:last-child{color:var(--dim)}
.foot{font-size:12px;color:#6b6656;margin-top:14px;max-width:80ch;line-height:1.6}

@keyframes flick{0%,100%{opacity:.9}45%{opacity:.66}70%{opacity:1}}
@media (prefers-reduced-motion:reduce){.glow{animation:none}}
@media (max-width:640px){
  .hud{font-size:12px}.dock{width:min(60%,300px)}.map{width:min(46%,250px)}
  .lead h1{font-size:22px}
}
"""

html = f"<style>{CSS}</style>\n{BODY}"
OUT.write_text(html, encoding="utf-8")
print("wrote", OUT, f"{len(html)//1024} KB")

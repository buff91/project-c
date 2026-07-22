#!/usr/bin/env python3
"""Generate Project-C's cohesive Torchstone runtime pixel-art set.

The generated PNGs are deliberately hand-authored at final pixel resolution.  The
AI board in docs/art-direction/project-c-runtime-asset-board-v2.png is a design
reference only; no pixels are sliced from it.  Re-running this script is stable.
"""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
T = (0, 0, 0, 0)

P = {
    "outline": (10, 13, 18, 255),
    "void": (5, 7, 12, 255),
    "shadow": (24, 27, 31, 255),
    "steel_dark": (54, 58, 62, 255),
    "steel": (112, 113, 110, 255),
    "steel_lit": (205, 190, 170, 255),
    "skin": (190, 142, 96, 255),
    "skin_lit": (226, 184, 132, 255),
    "brown_dark": (53, 34, 25, 255),
    "brown": (96, 58, 34, 255),
    "brown_lit": (157, 94, 43, 255),
    "blue_dark": (24, 42, 57, 255),
    "blue": (39, 70, 96, 255),
    "blue_lit": (66, 108, 137, 255),
    "goblin_dark": (43, 61, 31, 255),
    "goblin": (82, 105, 46, 255),
    "goblin_lit": (132, 142, 62, 255),
    "bone_dark": (91, 83, 69, 255),
    "bone": (174, 157, 126, 255),
    "bone_lit": (218, 203, 170, 255),
    "green_dark": (37, 53, 34, 255),
    "green": (62, 83, 45, 255),
    "green_lit": (105, 123, 62, 255),
    "red_dark": (83, 31, 25, 255),
    "red": (164, 49, 34, 255),
    "red_lit": (222, 81, 45, 255),
    "gold_dark": (116, 71, 26, 255),
    "gold": (229, 148, 43, 255),
    "gold_lit": (255, 213, 84, 255),
    "teal_dark": (18, 55, 63, 255),
    "teal": (56, 153, 166, 255),
    "teal_lit": (154, 223, 232, 255),
    "ice": (198, 244, 247, 255),
}


def canvas(size=(48, 64)):
    image = Image.new("RGBA", size, T)
    return image, ImageDraw.Draw(image)


def save(image: Image.Image, name: str) -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT / f"{name}.png", optimize=True)


def px(draw, points, color):
    for x, y in points:
        draw.point((x, y), fill=color)


def knight(name="actor-knight"):
    im, d = canvas()
    # Sword and shield define the silhouette before the body.
    d.polygon([(5, 35), (8, 33), (24, 53), (21, 57)], fill=P["outline"])
    d.polygon([(7, 35), (9, 35), (23, 53), (21, 54)], fill=P["steel_lit"])
    d.rectangle((5, 31, 12, 35), fill=P["outline"])
    d.rectangle((6, 32, 11, 33), fill=P["gold"])
    d.polygon([(34, 29), (43, 33), (42, 48), (36, 54), (31, 47), (31, 34)], fill=P["outline"])
    d.polygon([(35, 31), (41, 34), (40, 46), (36, 51), (33, 46), (33, 35)], fill=P["brown_dark"])
    d.line([(35, 34), (39, 36), (39, 44), (36, 48)], fill=P["steel"], width=1)
    d.rectangle((35, 39, 39, 42), fill=P["steel_lit"])
    # Boots, legs, tabard and cuirass.
    d.polygon([(17, 49), (24, 49), (23, 59), (14, 59), (14, 55)], fill=P["outline"])
    d.polygon([(25, 48), (32, 49), (34, 58), (25, 60), (23, 55)], fill=P["outline"])
    d.rectangle((16, 51, 22, 57), fill=P["brown"])
    d.polygon([(26, 50), (31, 51), (31, 56), (26, 58)], fill=P["brown_dark"])
    d.polygon([(14, 28), (20, 23), (31, 23), (36, 30), (33, 49), (18, 51), (13, 43)], fill=P["outline"])
    d.polygon([(16, 29), (21, 25), (29, 25), (34, 30), (31, 47), (19, 48), (15, 41)], fill=P["blue_dark"])
    d.polygon([(18, 29), (22, 26), (24, 45), (19, 45)], fill=P["blue_lit"])
    d.polygon([(25, 26), (31, 27), (32, 35), (26, 39)], fill=P["steel_dark"])
    d.line([(17, 37), (32, 39)], fill=P["gold"], width=2)
    # Armoured shoulder and gauntlet.
    d.ellipse((10, 25, 22, 35), fill=P["outline"])
    d.polygon([(12, 28), (17, 25), (20, 28), (18, 33), (12, 32)], fill=P["steel"])
    d.line([(13, 28), (18, 27)], fill=P["steel_lit"])
    d.rectangle((10, 34, 15, 43), fill=P["outline"])
    d.rectangle((11, 35, 14, 41), fill=P["steel_dark"])
    # Helmet, face slit and torch-side highlights.
    d.ellipse((16, 5, 34, 27), fill=P["outline"])
    d.polygon([(18, 10), (22, 6), (30, 8), (33, 14), (31, 23), (20, 24), (17, 18)], fill=P["steel"])
    d.polygon([(19, 10), (23, 7), (25, 8), (21, 17), (19, 18)], fill=P["steel_lit"])
    d.polygon([(19, 16), (32, 14), (31, 20), (20, 21)], fill=P["outline"])
    d.rectangle((22, 18, 29, 20), fill=P["skin"])
    d.point((23, 18), fill=P["skin_lit"])
    d.line([(18, 13), (31, 11)], fill=P["steel_lit"])
    px(d, [(20, 29), (17, 34), (29, 28), (22, 47), (17, 54)], P["steel_lit"])
    save(im, name)


def ranger():
    im, d = canvas()
    # Bow behind the figure.
    d.arc((29, 11, 47, 56), 250, 105, fill=P["outline"], width=3)
    d.arc((30, 12, 46, 55), 250, 105, fill=P["brown_lit"], width=1)
    d.line([(38, 18), (38, 50)], fill=P["steel_lit"], width=1)
    d.polygon([(14, 48), (22, 48), (21, 59), (13, 59)], fill=P["outline"])
    d.polygon([(25, 48), (31, 49), (34, 58), (25, 60)], fill=P["outline"])
    d.rectangle((15, 50, 20, 57), fill=P["brown"])
    d.polygon([(26, 50), (30, 51), (31, 56), (26, 58)], fill=P["brown_dark"])
    d.polygon([(12, 28), (19, 22), (30, 24), (35, 34), (31, 49), (16, 50), (11, 42)], fill=P["outline"])
    d.polygon([(14, 29), (20, 24), (28, 25), (33, 34), (29, 47), (17, 48), (13, 41)], fill=P["green_dark"])
    d.polygon([(15, 29), (21, 25), (20, 45), (16, 46)], fill=P["green_lit"])
    d.polygon([(18, 35), (32, 30), (34, 35), (21, 41)], fill=P["brown"])
    d.rectangle((15, 41, 31, 44), fill=P["brown_dark"])
    d.rectangle((22, 40, 25, 45), fill=P["gold"])
    # Hood and face.
    d.polygon([(13, 17), (18, 7), (30, 6), (36, 17), (33, 28), (18, 30), (12, 24)], fill=P["outline"])
    d.polygon([(15, 18), (19, 9), (28, 8), (34, 17), (31, 26), (19, 28), (14, 23)], fill=P["green"])
    d.polygon([(16, 17), (20, 10), (24, 9), (20, 26), (16, 23)], fill=P["green_lit"])
    d.polygon([(21, 14), (30, 15), (31, 23), (26, 27), (20, 23)], fill=P["skin"])
    d.polygon([(21, 15), (24, 14), (23, 23), (21, 22)], fill=P["skin_lit"])
    d.rectangle((27, 19, 29, 20), fill=P["outline"])
    # Quiver and arrow fletching.
    d.polygon([(8, 19), (13, 17), (17, 39), (12, 41)], fill=P["outline"])
    d.polygon([(10, 20), (12, 19), (15, 37), (13, 38)], fill=P["brown"])
    d.line([(9, 16), (13, 30)], fill=P["steel_lit"])
    d.line([(12, 15), (14, 29)], fill=P["steel_lit"])
    save(im, "actor-ranger")


def alchemist():
    im, d = canvas()
    d.polygon([(14, 50), (22, 49), (22, 59), (13, 59)], fill=P["outline"])
    d.polygon([(25, 49), (32, 50), (34, 58), (25, 60)], fill=P["outline"])
    d.rectangle((15, 51, 20, 57), fill=P["brown"])
    d.polygon([(26, 51), (31, 52), (31, 57), (26, 58)], fill=P["brown_dark"])
    d.polygon([(12, 27), (19, 22), (31, 23), (37, 34), (32, 52), (16, 52), (10, 42)], fill=P["outline"])
    d.polygon([(14, 29), (20, 24), (29, 25), (35, 35), (30, 49), (18, 50), (12, 41)], fill=P["blue_dark"])
    d.polygon([(15, 30), (21, 25), (20, 47), (17, 48), (13, 40)], fill=P["blue_lit"])
    d.line([(16, 40), (32, 39)], fill=P["gold_dark"], width=2)
    d.rectangle((22, 38, 26, 43), fill=P["gold"])
    # Head, hair and mantle.
    d.ellipse((16, 6, 34, 27), fill=P["outline"])
    d.polygon([(18, 10), (22, 7), (30, 9), (33, 16), (31, 24), (22, 27), (17, 21)], fill=P["skin"])
    d.polygon([(18, 10), (22, 7), (30, 8), (32, 13), (25, 12), (21, 18), (17, 17)], fill=P["brown_dark"])
    d.polygon([(19, 10), (22, 8), (25, 9), (21, 14), (18, 15)], fill=P["brown_lit"])
    d.rectangle((27, 17, 29, 18), fill=P["outline"])
    d.polygon([(12, 24), (19, 20), (27, 25), (22, 31), (13, 30)], fill=P["steel_dark"])
    # Potion hand and cold glow clusters.
    d.rectangle((34, 29, 40, 39), fill=P["outline"])
    d.rectangle((36, 27, 38, 31), fill=P["steel_lit"])
    d.polygon([(35, 32), (39, 32), (41, 37), (39, 41), (34, 40), (33, 36)], fill=P["teal"])
    d.rectangle((35, 34, 37, 38), fill=P["ice"])
    px(d, [(42, 31), (43, 36), (40, 27), (31, 34)], P["teal_lit"])
    # Satchel bottles.
    d.polygon([(7, 34), (14, 31), (17, 45), (10, 48), (6, 42)], fill=P["outline"])
    d.polygon([(9, 35), (13, 33), (15, 43), (10, 46), (8, 41)], fill=P["brown"])
    d.rectangle((9, 38, 11, 42), fill=P["red"])
    save(im, "actor-alchemist")


def goblin():
    im, d = canvas()
    # Dagger and buckler.
    d.polygon([(5, 36), (8, 34), (17, 45), (14, 48)], fill=P["outline"])
    d.polygon([(7, 36), (8, 36), (16, 45), (14, 46)], fill=P["steel_lit"])
    d.ellipse((32, 31, 45, 47), fill=P["outline"])
    d.ellipse((34, 33, 43, 45), fill=P["steel_dark"])
    d.rectangle((38, 34, 40, 43), fill=P["steel"])
    # Feet and wiry body.
    d.polygon([(15, 48), (21, 49), (20, 58), (11, 58), (12, 54)], fill=P["outline"])
    d.polygon([(25, 48), (31, 49), (36, 57), (27, 59), (24, 54)], fill=P["outline"])
    d.rectangle((15, 50, 20, 56), fill=P["goblin_dark"])
    d.polygon([(27, 50), (31, 51), (33, 55), (28, 57)], fill=P["goblin"])
    d.polygon([(13, 29), (20, 25), (31, 27), (36, 37), (31, 50), (17, 49), (11, 40)], fill=P["outline"])
    d.polygon([(15, 30), (21, 27), (29, 29), (34, 37), (29, 47), (18, 47), (13, 39)], fill=P["brown_dark"])
    d.polygon([(16, 32), (22, 28), (21, 45), (17, 45), (14, 39)], fill=P["brown_lit"])
    d.line([(15, 39), (32, 39)], fill=P["steel_dark"], width=2)
    # Head with asymmetric ears and brow.
    d.polygon([(5, 13), (16, 17), (20, 8), (31, 9), (35, 16), (45, 11), (40, 22), (34, 28), (18, 29), (10, 23)], fill=P["outline"])
    d.polygon([(8, 15), (17, 19), (21, 10), (29, 11), (34, 19), (41, 14), (38, 21), (32, 26), (19, 27), (12, 22)], fill=P["goblin"])
    d.polygon([(10, 16), (18, 20), (22, 11), (25, 11), (20, 25), (14, 22)], fill=P["goblin_lit"])
    d.line([(18, 18), (23, 17)], fill=P["outline"], width=2)
    d.line([(29, 17), (34, 19)], fill=P["outline"], width=2)
    d.point((21, 19), fill=P["red_lit"])
    d.point((31, 20), fill=P["red_lit"])
    d.polygon([(23, 23), (30, 23), (27, 26)], fill=P["outline"])
    d.point((25, 24), fill=P["bone_lit"])
    save(im, "actor-goblin")


def skeleton():
    im, d = canvas()
    d.polygon([(5, 35), (8, 33), (21, 51), (18, 54)], fill=P["outline"])
    d.polygon([(7, 35), (8, 35), (20, 51), (18, 52)], fill=P["steel_lit"])
    d.ellipse((33, 30, 46, 47), fill=P["outline"])
    d.ellipse((35, 32, 44, 45), fill=P["steel_dark"])
    d.line([(36, 37), (43, 39)], fill=P["steel"])
    # Legs, pelvis and rib cage.
    d.line([(20, 44), (16, 58)], fill=P["outline"], width=5)
    d.line([(28, 44), (32, 58)], fill=P["outline"], width=5)
    d.line([(20, 45), (17, 57)], fill=P["bone"] , width=2)
    d.line([(27, 45), (31, 57)], fill=P["bone_dark"], width=2)
    d.polygon([(17, 39), (23, 36), (31, 39), (28, 47), (20, 47)], fill=P["outline"])
    d.polygon([(19, 40), (23, 38), (29, 40), (27, 45), (21, 45)], fill=P["bone"])
    d.line([(24, 24), (24, 40)], fill=P["bone"], width=3)
    for y, span in [(27, 8), (31, 9), (35, 7)]:
        d.arc((24 - span, y - 4, 24 + span, y + 4), 15, 165, fill=P["bone"], width=2)
    d.line([(17, 27), (11, 41)], fill=P["bone"], width=2)
    d.line([(31, 27), (37, 38)], fill=P["bone_dark"], width=2)
    # Skull.
    d.ellipse((15, 5, 34, 24), fill=P["outline"])
    d.polygon([(18, 7), (29, 7), (33, 12), (30, 21), (26, 24), (18, 20), (16, 12)], fill=P["bone"])
    d.polygon([(18, 8), (23, 7), (21, 18), (18, 18), (16, 13)], fill=P["bone_lit"])
    d.rectangle((19, 13, 22, 16), fill=P["outline"])
    d.rectangle((27, 13, 30, 16), fill=P["outline"])
    d.rectangle((23, 17, 26, 20), fill=P["bone_dark"])
    d.line([(20, 21), (29, 21)], fill=P["outline"])
    px(d, [(20, 9), (18, 11), (20, 29), (20, 33)], P["bone_lit"])
    save(im, "actor-skeleton")


def slime():
    im, d = canvas()
    d.polygon([(7, 50), (10, 38), (17, 31), (22, 24), (28, 27), (32, 32), (38, 38), (42, 50), (38, 57), (11, 57)], fill=P["outline"])
    d.polygon([(9, 49), (12, 39), (18, 33), (22, 27), (27, 29), (31, 35), (36, 39), (40, 50), (36, 55), (12, 55)], fill=P["teal_dark"])
    d.polygon([(12, 45), (16, 36), (20, 32), (22, 42), (18, 51), (12, 52)], fill=P["teal"])
    d.polygon([(15, 39), (18, 34), (21, 32), (20, 38), (17, 42)], fill=P["teal_lit"])
    d.rectangle((18, 43, 21, 46), fill=P["outline"])
    d.rectangle((30, 43, 33, 46), fill=P["outline"])
    d.line([(22, 50), (29, 50)], fill=P["outline"], width=2)
    px(d, [(13, 49), (17, 53), (27, 32), (35, 49), (38, 52)], P["teal"])
    save(im, "actor-slime")


def merchant():
    im, d = canvas()
    # Large pack establishes a distinct, non-combat silhouette.
    d.polygon([(4, 20), (13, 13), (20, 18), (19, 46), (7, 51), (3, 42)], fill=P["outline"])
    d.polygon([(6, 21), (13, 16), (18, 20), (17, 43), (8, 48), (5, 40)], fill=P["brown"])
    d.line([(7, 29), (17, 26)], fill=P["brown_lit"], width=2)
    d.rectangle((6, 35, 17, 38), fill=P["steel_dark"])
    d.rectangle((8, 39, 11, 44), fill=P["red"])
    d.rectangle((13, 37, 16, 42), fill=P["teal"])
    d.polygon([(18, 49), (25, 49), (24, 59), (16, 59)], fill=P["outline"])
    d.polygon([(28, 48), (34, 49), (37, 58), (28, 60)], fill=P["outline"])
    d.rectangle((19, 51, 23, 57), fill=P["brown"])
    d.polygon([(29, 50), (33, 51), (34, 56), (29, 58)], fill=P["brown_dark"])
    d.polygon([(13, 28), (21, 23), (33, 25), (39, 36), (34, 51), (19, 51), (12, 43)], fill=P["outline"])
    d.polygon([(15, 30), (22, 25), (31, 27), (37, 36), (32, 49), (20, 49), (14, 42)], fill=P["brown_dark"])
    d.polygon([(16, 31), (22, 26), (21, 46), (17, 47), (14, 41)], fill=P["brown_lit"])
    d.polygon([(13, 17), (18, 7), (31, 6), (38, 17), (34, 29), (18, 30), (12, 24)], fill=P["outline"])
    d.polygon([(15, 18), (19, 9), (29, 8), (36, 17), (32, 27), (19, 28), (14, 23)], fill=P["brown_dark"])
    d.polygon([(17, 18), (20, 11), (25, 10), (20, 26), (16, 23)], fill=P["brown_lit"])
    d.polygon([(21, 14), (30, 15), (32, 23), (27, 27), (20, 23)], fill=P["goblin"])
    d.rectangle((27, 19, 29, 20), fill=P["outline"])
    d.point((28, 19), fill=P["gold_lit"])
    # Potion offered in hand.
    d.rectangle((37, 30, 41, 36), fill=P["outline"])
    d.rectangle((38, 27, 40, 31), fill=P["gold"])
    d.polygon([(37, 34), (41, 34), (43, 40), (40, 44), (36, 42), (35, 38)], fill=P["outline"])
    d.polygon([(38, 35), (40, 35), (41, 40), (39, 42), (37, 40)], fill=P["red_lit"])
    save(im, "actor-merchant")


def prop_chest():
    im, d = canvas((64, 64))
    d.polygon([(10, 28), (18, 20), (47, 20), (55, 28), (52, 52), (15, 52)], fill=P["outline"])
    d.polygon([(13, 29), (20, 23), (45, 23), (52, 29), (49, 49), (17, 49)], fill=P["brown_dark"])
    d.polygon([(16, 29), (21, 24), (44, 24), (49, 29), (47, 35), (17, 35)], fill=P["brown_lit"])
    d.line([(17, 35), (49, 35)], fill=P["steel_dark"], width=3)
    d.line([(20, 24), (18, 49)], fill=P["steel"], width=2)
    d.line([(45, 24), (48, 49)], fill=P["steel_dark"], width=2)
    d.rectangle((28, 32, 37, 43), fill=P["outline"])
    d.rectangle((30, 34, 35, 40), fill=P["gold_dark"])
    d.rectangle((31, 35, 34, 38), fill=P["gold_lit"])
    px(d, [(19, 27), (23, 24), (18, 39), (22, 45), (43, 28)], P["brown_lit"])
    save(im, "prop-stash")


def prop_barrel():
    im, d = canvas((64, 64))
    d.ellipse((14, 11, 50, 27), fill=P["outline"])
    d.rectangle((13, 19, 51, 52), fill=P["outline"])
    d.ellipse((13, 43, 51, 58), fill=P["outline"])
    d.rectangle((16, 20, 48, 49), fill=P["red_dark"])
    d.ellipse((16, 14, 48, 26), fill=P["red"])
    d.ellipse((19, 16, 45, 23), fill=P["brown_lit"])
    d.rectangle((17, 24, 47, 28), fill=P["steel_dark"])
    d.rectangle((17, 44, 47, 48), fill=P["steel_dark"])
    d.line([(20, 20), (20, 50)], fill=P["red_lit"], width=2)
    d.line([(43, 22), (44, 49)], fill=P["brown_dark"], width=2)
    d.rectangle((25, 31, 39, 42), fill=P["bone"])
    d.polygon([(27, 33), (31, 30), (36, 32), (38, 36), (35, 40), (29, 39)], fill=P["bone_lit"])
    d.rectangle((29, 36, 31, 39), fill=P["red_dark"])
    d.rectangle((34, 36, 36, 39), fill=P["red_dark"])
    save(im, "prop-explosive-barrel")


def prop_campfire():
    im, d = canvas((64, 64))
    d.ellipse((8, 43, 56, 57), fill=P["outline"])
    for box in [(10, 44, 23, 51), (24, 48, 38, 55), (40, 43, 54, 51), (17, 40, 31, 47), (33, 40, 47, 47)]:
        d.ellipse(box, fill=P["steel_dark"])
    d.line([(18, 49), (45, 39)], fill=P["brown"], width=5)
    d.line([(19, 39), (45, 50)], fill=P["brown_lit"], width=5)
    d.polygon([(31, 46), (22, 37), (27, 26), (31, 31), (35, 15), (40, 31), (45, 37), (39, 47)], fill=P["red_lit"])
    d.polygon([(31, 45), (27, 37), (32, 29), (35, 23), (38, 36), (41, 39), (37, 46)], fill=P["gold"])
    d.polygon([(32, 43), (31, 37), (35, 30), (37, 39), (35, 44)], fill=P["gold_lit"])
    px(d, [(21, 26), (45, 29), (28, 18), (42, 20), (48, 34)], P["gold"])
    save(im, "prop-campfire")


def prop_portal():
    im, d = canvas((64, 80))
    # Stone arch.
    d.polygon([(8, 69), (8, 30), (15, 13), (26, 6), (40, 6), (52, 15), (58, 31), (58, 69)], fill=P["outline"])
    d.polygon([(11, 66), (11, 31), (18, 16), (27, 10), (39, 10), (49, 18), (55, 32), (55, 66), (47, 66), (47, 31), (42, 21), (36, 17), (29, 17), (23, 22), (19, 32), (19, 66)], fill=P["steel_dark"])
    d.line([(13, 28), (19, 31)], fill=P["steel"], width=2)
    d.line([(20, 17), (25, 22)], fill=P["steel"], width=2)
    d.line([(28, 10), (29, 18)], fill=P["steel"], width=2)
    d.line([(42, 12), (39, 19)], fill=P["shadow"], width=2)
    d.line([(51, 21), (44, 24)], fill=P["shadow"], width=2)
    # Portal surface and clustered energy.
    d.polygon([(21, 63), (21, 33), (26, 23), (32, 19), (39, 22), (45, 32), (45, 63), (39, 69), (28, 69)], fill=P["teal_dark"])
    d.polygon([(24, 61), (24, 34), (28, 26), (33, 22), (38, 25), (42, 34), (42, 60), (37, 65), (29, 65)], fill=(24, 107, 122, 255))
    d.polygon([(27, 59), (27, 36), (31, 28), (35, 27), (39, 35), (39, 57), (35, 62), (31, 61)], fill=P["teal"])
    d.line([(25, 35), (25, 58)], fill=P["teal_lit"], width=2)
    px(d, [(7, 19), (11, 11), (55, 13), (60, 28), (4, 43), (58, 53), (14, 72), (51, 73)], P["teal_lit"])
    save(im, "prop-portal")


def item(name, kind):
    im, d = canvas((32, 32))
    if kind in ("potion", "bomb", "frost"):
        if kind == "potion":
            body, light = P["red"], P["red_lit"]
        elif kind == "frost":
            body, light = P["teal"], P["ice"]
        else:
            body, light = P["shadow"], P["steel"]
        d.rectangle((13, 4, 19, 9), fill=P["outline"])
        d.rectangle((14, 3, 18, 6), fill=P["gold_dark"])
        d.polygon([(9, 10), (13, 7), (19, 7), (23, 11), (25, 22), (21, 27), (11, 27), (7, 22)], fill=P["outline"])
        d.polygon([(11, 11), (14, 9), (18, 9), (21, 12), (23, 21), (20, 25), (12, 25), (9, 21)], fill=body)
        d.polygon([(11, 12), (14, 10), (14, 22), (12, 22), (10, 19)], fill=light)
        if kind != "potion":
            d.line([(18, 7), (23, 3)], fill=P["brown_lit"], width=2)
            d.point((25, 2), fill=P["gold_lit"] if kind == "bomb" else P["ice"])
    elif kind == "oil":
        d.rectangle((13, 4, 19, 9), fill=P["outline"])
        d.rectangle((14, 3, 18, 6), fill=P["brown_lit"])
        d.polygon([(9, 10), (13, 7), (19, 7), (23, 11), (25, 22), (21, 27), (11, 27), (7, 22)], fill=P["outline"])
        d.polygon([(11, 11), (14, 9), (18, 9), (21, 12), (23, 21), (20, 25), (12, 25), (9, 21)], fill=P["gold_dark"])
        d.rectangle((11, 18, 21, 23), fill=P["brown_dark"])
        d.rectangle((12, 17, 14, 21), fill=P["gold"])
    elif kind == "knife":
        d.polygon([(6, 24), (10, 19), (24, 4), (27, 5), (23, 13), (12, 25)], fill=P["outline"])
        d.polygon([(10, 20), (23, 6), (25, 6), (21, 12), (11, 23)], fill=P["steel_lit"])
        d.rectangle((7, 22, 14, 25), fill=P["gold_dark"])
        d.polygon([(4, 27), (8, 22), (12, 25), (8, 29)], fill=P["brown"])
    elif kind == "scroll":
        d.polygon([(7, 7), (12, 4), (24, 6), (25, 25), (20, 28), (7, 25)], fill=P["outline"])
        d.polygon([(9, 8), (13, 6), (22, 8), (23, 23), (19, 26), (9, 23)], fill=P["bone"])
        d.line([(10, 12), (21, 14)], fill=P["gold_dark"], width=2)
        d.line([(10, 18), (21, 20)], fill=P["brown"])
        d.rectangle((14, 15, 17, 18), fill=P["teal"])
    elif kind == "coin":
        d.polygon([(8, 11), (12, 6), (21, 6), (25, 12), (24, 25), (9, 25)], fill=P["outline"])
        d.polygon([(10, 12), (13, 8), (20, 8), (23, 13), (22, 23), (11, 23)], fill=P["brown"])
        d.line([(11, 13), (22, 13)], fill=P["brown_lit"], width=2)
        d.ellipse((13, 15, 20, 22), fill=P["gold_dark"])
        d.ellipse((15, 16, 20, 20), fill=P["gold_lit"])
    elif kind == "gem":
        d.polygon([(16, 3), (25, 10), (22, 23), (16, 29), (8, 22), (6, 11)], fill=P["outline"])
        d.polygon([(16, 5), (23, 11), (20, 22), (16, 26), (10, 21), (8, 12)], fill=P["teal"])
        d.polygon([(16, 5), (16, 25), (10, 20), (9, 12)], fill=P["teal_lit"])
        d.polygon([(17, 7), (21, 11), (18, 15), (16, 13)], fill=P["ice"])
    elif kind == "relic":
        d.rectangle((7, 24, 25, 28), fill=P["outline"])
        d.rectangle((9, 22, 23, 25), fill=P["gold_dark"])
        d.polygon([(11, 22), (10, 11), (14, 5), (18, 4), (23, 10), (22, 22)], fill=P["outline"])
        d.polygon([(13, 21), (12, 12), (15, 7), (18, 6), (21, 11), (20, 21)], fill=P["gold"])
        d.rectangle((15, 12, 18, 15), fill=P["teal_lit"])
        d.point((16, 12), fill=P["ice"])
    elif kind == "herb":
        d.line([(16, 26), (16, 8)], fill=P["green_lit"], width=2)
        for poly in [[(15, 12), (7, 8), (9, 17)], [(17, 15), (26, 10), (22, 20)], [(15, 20), (7, 18), (12, 25)]]:
            d.polygon(poly, fill=P["outline"])
            inner = [(x + (1 if x < 16 else -1), y + 1) for x, y in poly]
            d.polygon(inner, fill=P["green"])
    elif kind == "powder":
        d.polygon([(8, 10), (13, 6), (20, 7), (24, 12), (23, 26), (9, 26)], fill=P["outline"])
        d.polygon([(10, 11), (14, 8), (19, 9), (22, 13), (21, 24), (11, 24)], fill=P["brown"])
        d.line([(11, 14), (21, 14)], fill=P["gold_dark"], width=2)
        d.point((16, 19), fill=P["gold_lit"])
    else:
        d.polygon([(16, 3), (23, 10), (19, 27), (9, 21), (11, 10)], fill=P["outline"])
        d.polygon([(16, 5), (21, 11), (18, 24), (11, 20), (13, 11)], fill=P["teal"])
        d.polygon([(15, 7), (17, 8), (14, 20), (12, 19)], fill=P["ice"])
    save(im, name)


def marker(name, color, lit):
    im, d = canvas((64, 32))
    outer = [(32, 1), (62, 16), (32, 31), (2, 16)]
    inner = [(32, 5), (56, 16), (32, 27), (8, 16)]
    d.line(outer + [outer[0]], fill=P["outline"], width=3)
    d.line(inner + [inner[0]], fill=color, width=2)
    d.line([(32, 5), (56, 16)], fill=lit, width=1)
    for x, y in [(32, 4), (56, 16), (32, 27), (8, 16)]:
        d.rectangle((x - 1, y - 1, x + 1, y + 1), fill=lit)
    save(im, name)


def heart(name, filled):
    im, d = canvas((24, 21))
    shape = [(2, 5), (5, 2), (10, 2), (12, 5), (14, 2), (19, 2), (22, 5),
             (22, 11), (12, 20), (2, 11)]
    d.polygon(shape, fill=P["outline"])
    inner = [(4, 6), (6, 4), (9, 4), (12, 8), (15, 4), (18, 4), (20, 6),
             (20, 10), (12, 17), (4, 10)]
    d.polygon(inner, fill=P["red"] if filled else P["red_dark"])
    if filled:
        d.polygon([(5, 6), (7, 4), (10, 5), (10, 8), (7, 7), (5, 9)], fill=P["red_lit"])
        d.rectangle((6, 5, 8, 6), fill=(255, 143, 105, 255))
    else:
        d.line([(6, 6), (17, 15)], fill=P["shadow"], width=2)
    save(im, name)


def main():
    knight("actor-player")
    knight("actor-knight")
    ranger()
    alchemist()
    goblin()
    skeleton()
    slime()
    merchant()
    prop_chest()
    prop_barrel()
    prop_campfire()
    prop_portal()
    item("item-potion", "potion")
    item("item-bomb", "bomb")
    item("item-frost-bomb", "frost")
    item("item-oil-flask", "oil")
    item("item-throwing-knife", "knife")
    item("item-recall-scroll", "scroll")
    item("item-coin-pouch", "coin")
    item("item-gemstone", "gem")
    item("item-relic", "relic")
    item("item-herb", "herb")
    item("item-blast-powder", "powder")
    item("item-frost-shard", "shard")
    marker("marker-player", P["teal"], P["teal_lit"])
    marker("marker-target", P["gold"], P["gold_lit"])
    heart("ui-heart-full", True)
    heart("ui-heart-empty", False)
    print(f"wrote 28 cohesive runtime sprites to {OUTPUT}")


if __name__ == "__main__":
    main()

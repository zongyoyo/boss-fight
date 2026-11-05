"""
傳送門 SM_Portal（ART_STYLE「休息區（Phase 6）」）。

兩根石塊一塊塊疊起來的柱子（每塊大小、角度略有不同）、較寬的柱礎、三塊石板拼的門楣加中間的拱心石，
柱子正反兩面各有一排內縮的符文（子物件 SM_Portal_Runes，獨立材質 Hub_PortalRune：跟中間紫色光面同一組顏色，
光面本身仍是 HubBuilder 的旋轉圓盤，不在這個模型裡）；門前後地上幾塊扁平的鋪石（貼地 3 cm，玩家直接走過）。
柱子與門楣對齊 HubBuilder 的碰撞（柱中心 x ±2、寬 0.8、深 0.9、高 4.2；門楣 4.2–4.9 m、寬 5.2）。
座標照 Unity 站點本地想（hub.U），軸心在地面中心，正面（Unity +Z）是玩家走過來的那一邊。
"""
import importlib
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (os.path.join(_HERE, "..", "..", "lib"), _HERE):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import lowpoly_kit as kit  # noqa: E402
import hub_common as hub  # noqa: E402

importlib.reload(kit)
importlib.reload(hub)

U = hub.U
NAME = "SM_Portal"
SEED = 631
PILLAR_X = 2.0
PILLAR_TOP = 4.2
LINTEL = (4.2, 4.9)
RUNE_MATERIAL = "Hub_PortalRune"
# 符文：在 0.3 × 0.4 的格子裡的線段 (u0, v0, u1, v1)，u 往右、v 往上
GLYPHS = [
    [(0.0, -0.2, 0.0, 0.2), (0.0, 0.1, 0.12, 0.2), (0.0, 0.0, 0.12, 0.1)],
    [(0.0, -0.2, 0.0, 0.2), (-0.12, 0.05, 0.12, -0.08)],
    [(-0.1, -0.2, 0.1, 0.2), (0.1, -0.2, -0.1, 0.2)],
    [(0.0, -0.2, 0.0, 0.2), (0.0, 0.2, 0.12, 0.05), (0.0, -0.2, -0.12, -0.05)],
    [(-0.1, 0.2, 0.1, 0.2), (0.0, 0.2, 0.0, -0.2), (-0.1, -0.2, 0.1, -0.2)],
]
BUDGET = (400, 1000)


def pillar(bm, rng, x):
    """柱礎 + 一塊塊疊上去的石塊，回傳每塊的 (中心高, 高)（放符文用）。"""
    hub.box(bm, (1.15, 0.35, 1.25), (x, 0, 0), cell="stone.d", base=True, rng=rng, jitter=0.04)
    blocks = []
    y = 0.35
    while y < PILLAR_TOP - 0.2:
        h = min(rng.uniform(0.55, 0.75), PILLAR_TOP - y)
        w = rng.uniform(0.82, 0.92)
        hub.box(bm, (w, h * 0.98, rng.uniform(0.9, 0.98)), (x + rng.uniform(-0.03, 0.03), y, 0),
                    rotation=(0, 0, rng.uniform(-4, 4)), cell="stone." + rng.choice("lmmd"), base=True, rng=rng, jitter=0.04)
        blocks.append((y + h / 2, h))
        y += h
    return blocks


def lintel(bm, rng):
    y0, y1 = LINTEL
    for x, w in ((-1.75, 1.9), (1.75, 1.9)):
        hub.box(bm, (w, y1 - y0, 1.1), (x, y0, 0), rotation=(0, rng.uniform(-1.5, 1.5), 0),
                    cell="stone." + rng.choice("lm"), base=True, rng=rng, jitter=0.03)
    # 拱心石：中間一塊上寬下窄、比兩邊高一點
    kit.add_prism(bm, 4, 0.62, (y1 - y0) + 0.25, location=U(0, y0 - 0.1, 0), rotation=(0, 0, 45), radius_top=0.78,
                  cell="stone.l", rng=rng, jitter=0.03)
    # 頂上的壓頂石
    hub.box(bm, (5.5, 0.18, 1.25), (0, y1, 0), cell="stone_dark.m", base=True, rng=rng, jitter=0.02)


def runes(bm, rng, x, blocks):
    """這根柱子正反兩面、每塊石頭中央一個符文（內縮在石面裡，只露出薄薄一層）。"""
    for face in (1, -1):
        z = face * 0.47
        for i, (yc, h) in enumerate(blocks[1:-1]):
            glyph = GLYPHS[(i + (0 if x < 0 else 2) + (0 if face > 0 else 1)) % len(GLYPHS)]
            for u0, v0, u1, v1 in glyph:
                cu, cv = (u0 + u1) / 2, (v0 + v1) / 2
                length = math.hypot(u1 - u0, v1 - v0) + 0.05
                angle = math.degrees(math.atan2(v1 - v0, u1 - u0))
                # 石面在 Blender XZ 平面：繞 Blender Y 轉；Unity 的 u（右）在 Blender 是 −X
                kit.add_box(bm, (length, 0.04, 0.05), U(x + cu * face, yc + cv, z),
                            rotation=(0, angle * face, 0), cell="rune.m")


def paving(bm, rng):
    for z in (-1.4, 1.4, -2.3, 2.3):
        for x in (-0.9, 0.0, 0.9):
            if abs(z) > 2 and x == 0.0:
                continue
            verts, faces = kit.add_box(bm, (rng.uniform(0.75, 0.9), rng.uniform(0.75, 0.9), 0.06),
                                       U(x + rng.uniform(-0.08, 0.08), -0.03, z + rng.uniform(-0.08, 0.08)),
                                       rotation=(0, 0, rng.uniform(-12, 12)), cell="stone." + rng.choice("lmd"),
                                       base=True, rng=rng, jitter=0.08)


def build():
    rng = hub.rng_for(SEED)
    col = kit.begin(NAME)
    bm = kit.new_bmesh()
    rune_bm = kit.new_bmesh()
    for x in (-PILLAR_X, PILLAR_X):
        blocks = pillar(bm, rng, x)
        runes(rune_bm, rng, x, blocks)
    lintel(bm, rng)
    paving(bm, rng)
    root = hub.make(NAME, bm, col)
    rune_obj = hub.make(f"{NAME}_Runes", rune_bm, col, material=RUNE_MATERIAL)
    rune_obj.parent = root
    objs = [root, rune_obj]
    report = kit.check(objs, budget=BUDGET, pivots={NAME: (0, 0, 0), f"{NAME}_Runes": (0, 0, 0)})
    report["sheet"] = kit.contact_sheet(objs, NAME, target_bbox=((-2.6, -0.55, 0), (2.6, 0.55, 4.9)))
    report["fbx"] = kit.export_fbx(objs, f"Hub/{NAME}.fbx")
    report["blend"] = kit.save_blend(NAME)
    return report


result = build()

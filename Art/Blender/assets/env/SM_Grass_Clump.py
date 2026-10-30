"""
草叢 SM_Grass_Clump_<變體>（ART_STYLE 第 8 節「共用環境套件」：矮、中、高、帶花）。

照 How to Fish 的草（Art/參考圖/How to fish，不進版控）：一大叢細長的扁葉片，頂端是斜切的鈍頭，不是尖刺；
顏色是均勻的鮮綠，每片隨機取亮／中／暗一階，明暗主要交給投影。
每片葉是一個四邊形（2 個三角面）的單片網格：材質用雙面的 M_Palette_Foliage，法線全部朝上（光照跟地面一致）。
軸心在底部中心。變體寫在 VARIANTS：A 中、B 矮（鋪草毯用）、C 高、D 帶花（幾根細莖頂著五角形小花，花色 flower_white／flower_yellow）。
"""
import importlib
import math
import os
import random
import sys

from mathutils import Vector

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)

VARIANTS = {
    # height：中心葉片的高度範圍，外圈依 edge_height 比例變矮；lean：往外倒的比例（水平位移 / 高度）
    "A": dict(seed=41, blades=20, height=(0.42, 0.62), edge_height=0.6, spread=0.17,
              width=(0.06, 0.09), tip_width=0.65, slant=0.12, lean=(0.12, 0.45),
              tiers=(("m", 0.5), ("l", 0.3), ("d", 0.2)), group="grass"),
    "B": dict(seed=42, blades=16, height=(0.2, 0.3), edge_height=0.65, spread=0.22,
              width=(0.07, 0.1), tip_width=0.7, slant=0.12, lean=(0.2, 0.6),
              tiers=(("m", 0.5), ("l", 0.3), ("d", 0.2)), group="grass"),
    "C": dict(seed=43, blades=26, height=(0.75, 1.0), edge_height=0.55, spread=0.2,
              width=(0.06, 0.09), tip_width=0.6, slant=0.1, lean=(0.1, 0.4),
              tiers=(("m", 0.45), ("l", 0.25), ("d", 0.3)), group="grass"),
    "D": dict(seed=44, blades=14, height=(0.36, 0.5), edge_height=0.6, spread=0.17,
              width=(0.06, 0.09), tip_width=0.65, slant=0.12, lean=(0.12, 0.45),
              tiers=(("m", 0.5), ("l", 0.3), ("d", 0.2)), group="grass",
              # 花：(高度, 花瓣半徑, 色格)
              flowers=((0.62, 0.07, "flower_white.m"), (0.52, 0.06, "flower_yellow.m"), (0.56, 0.065, "flower_white.l"))),
}


def add_blade(bm, rng, base, outward, height, width, tip_width, slant, lean, cell):
    """一片扁葉：底邊 width、頂邊 width × tip_width，頂邊斜切（一角高 slant × height），往 outward 倒。"""
    up = Vector((0, 0, 1))
    # 葉面大致朝外，再隨機扭一點，從各個方向看都有葉子的寬面
    twist = rng.uniform(-0.6, 0.6)
    face = Vector((math.cos(twist) * outward.x - math.sin(twist) * outward.y,
                   math.sin(twist) * outward.x + math.cos(twist) * outward.y, 0)).normalized()
    side = Vector((-face.y, face.x, 0))
    base = Vector(base)
    top = base + up * height + outward * lean * height
    hw, tw = width / 2, width * tip_width / 2
    raise_left = rng.random() < 0.5
    tl = top - side * tw + up * (slant * height if raise_left else 0)
    tr = top + side * tw + up * (0 if raise_left else slant * height)
    verts = [bm.verts.new(base - side * hw), bm.verts.new(base + side * hw), bm.verts.new(tr), bm.verts.new(tl)]
    f = bm.faces.new(verts)
    kit._paint_faces(bm, [f], cell)


def add_flower(bm, rng, angle, height, radius, cell):
    """一朵花：細莖（一片很窄的葉）頂著一個水平的五角形花面（3 個三角面）。"""
    out = Vector((math.cos(angle), math.sin(angle), 0))
    base = out * 0.05
    add_blade(bm, rng, base, out, height, 0.025, 1.0, 0.0, 0.15, "stem.m")
    top = Vector(base) + Vector((0, 0, height)) + out * 0.15 * height
    spin = rng.uniform(0, 2 * math.pi)
    ring = [bm.verts.new(top + Vector((math.cos(spin + k * 2 * math.pi / 5) * radius,
                                       math.sin(spin + k * 2 * math.pi / 5) * radius, 0))) for k in range(5)]
    kit._paint_faces(bm, [bm.faces.new(ring)], cell)


def pick_tier(rng, tiers):
    x = rng.random()
    for tier, weight in tiers:
        if x < weight:
            return tier
        x -= weight
    return tiers[-1][0]


def build(variant, p):
    name = f"SM_Grass_Clump_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    n = p["blades"]
    for i in range(n):
        angle = 2 * math.pi * (i + rng.uniform(-0.4, 0.4)) / n
        d = math.sqrt(rng.random())  # 0 = 中心、1 = 外圈；開根號讓葉片平均鋪在圓盤上
        outward = Vector((math.cos(angle), math.sin(angle), 0))
        base = outward * p["spread"] * d
        height = rng.uniform(*p["height"]) * (1 - (1 - p["edge_height"]) * d)
        lean = p["lean"][0] + (p["lean"][1] - p["lean"][0]) * d * rng.uniform(0.7, 1.0)
        add_blade(bm, rng, base, outward, height, rng.uniform(*p["width"]), p["tip_width"], p["slant"], lean,
                  f'{p["group"]}.{pick_tier(rng, p["tiers"])}')
    for k, (h, r, cell) in enumerate(p.get("flowers", ())):
        add_flower(bm, rng, 2 * math.pi * k / len(p["flowers"]) + rng.uniform(-0.4, 0.4), h, r, cell)
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj, base_material=kit.MAT_FOLIAGE)
    kit.normals_up(obj)
    report = kit.check([obj], budget=(20, 60), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

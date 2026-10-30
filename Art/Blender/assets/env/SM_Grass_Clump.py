"""
草叢 SM_Grass_Clump_<變體>（ART_STYLE 第 8 節「共用環境套件」：矮、中、高、帶花）。

每片葉是扁三角斷面的細長錐，中段往外折一下；葉根暗、葉尖亮。葉片是封閉的實體（Unity 背面剔除也看得到）。
軸心在底部中心。變體寫在 VARIANTS；Phase 1 只有中高的 A。
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
    "A": dict(seed=41, blades=6, height=(0.38, 0.62), width=0.036, thickness=0.012, spread=0.13,
              lean=(0.25, 0.6), kink=0.3, group="grass"),
}


def add_blade(bm, base, angle, height, width, thickness, lean, kink, group):
    """一片葉：底面（扁三角）→ 折點（窄 40%、往外偏）→ 尖。每片 10 個三角面。"""
    d = Vector((math.cos(angle), math.sin(angle), 0))
    s = Vector((-math.sin(angle), math.cos(angle), 0))
    up = Vector((0, 0, 1))

    def section(center, scale):
        # 逆時針（從上看）：−s → +d → +s
        return [bm.verts.new(center - s * width * scale), bm.verts.new(center + d * thickness * scale),
                bm.verts.new(center + s * width * scale)]

    base = Vector(base)
    low = section(base, 1.0)
    mid_center = base + up * height * kink + d * lean * height * kink * 0.6
    mid = section(mid_center, 0.6)
    tip = bm.verts.new(base + up * height + d * lean * height)
    lower, upper = [], []
    for i in range(3):
        j = (i + 1) % 3
        lower.append(bm.faces.new((low[i], low[j], mid[j], mid[i])))
        upper.append(bm.faces.new((mid[i], mid[j], tip)))
    lower.append(bm.faces.new(list(reversed(low))))
    kit._paint_faces(bm, lower, f"{group}.d")
    kit._paint_faces(bm, upper, f"{group}.l")


def build(variant, p):
    name = f"SM_Grass_Clump_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    n = p["blades"]
    for i in range(n):
        angle = 2 * math.pi * (i + rng.uniform(-0.3, 0.3)) / n
        r = p["spread"] * rng.uniform(0.2, 1.0)
        base = (math.cos(angle) * r, math.sin(angle) * r, 0)
        add_blade(bm, base, angle, rng.uniform(*p["height"]), p["width"] * rng.uniform(0.85, 1.15), p["thickness"],
                  rng.uniform(*p["lean"]), p["kink"], p["group"])
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=(20, 60), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

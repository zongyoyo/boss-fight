"""
針葉樹 SM_Tree_Pine_<變體>（ART_STYLE 第 8 節「共用環境套件」）。

6 邊樹幹，一層層往下垂的鋸齒狀枝片（下層大、上層小），枝片上亮下暗。
軸心在樹幹底部中心；高度對照序章佔位樹（PrologueLayoutData.treeHeight 6–9 m、樹冠寬 3.8 m）。
樹幹碰撞由 Unity 端加（半徑 PrologueLayoutData.trunkRadius），模型不帶碰撞。
變體寫在 VARIANTS；Phase 1 只有 A，Phase 2 再加 B–D。
"""
import importlib
import math
import os
import random
import sys

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)

VARIANTS = {
    "A": dict(seed=11, height=8.0, trunk_radius=0.28, tiers=6, base_radius=2.0, top_radius=0.65,
              first_tier=1.5, points=8, droop=0.55, group="pine"),
}


def add_skirt(bm, rng, z_top, z_bottom, radius, points, droop, spin, group, sunny):
    """一層枝片：尖頂往外下垂的鋸齒傘。外圈 2×points 個點，長尖較低、短尖較高；
    中圈拱起讓邊緣下垂；底下收回樹幹裡（暗階）。"""
    outer, middle = [], []
    n = points * 2
    for i in range(n):
        a = 2 * math.pi * i / n + spin
        long = i % 2 == 0
        r = radius * (1.0 if long else 0.7) * rng.uniform(0.92, 1.05)
        z = z_bottom - (0.0 if long else -0.18 * radius * droop) + rng.uniform(-0.05, 0.05) * radius
        outer.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)))
        rm = r * 0.5
        zm = z_bottom + (z_top - z_bottom) * (0.5 + 0.25 * droop)
        middle.append(bm.verts.new((math.cos(a) * rm, math.sin(a) * rm, zm)))
    apex = bm.verts.new((0, 0, z_top))
    under = bm.verts.new((0, 0, z_bottom + (z_top - z_bottom) * 0.3))
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append(bm.faces.new((middle[i], middle[j], apex)))
        faces.append(bm.faces.new((outer[i], outer[j], middle[j], middle[i])))
        faces.append(bm.faces.new((outer[j], outer[i], under)))
    # 上亮下暗；sunny（上層受光多）時斜面也算亮階
    kit.shade_faces(bm, faces, group, up=0.1 if sunny else 0.5, down=-0.2)
    return faces


def build(variant, p):
    name = f"SM_Tree_Pine_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    h = p["height"]
    # 樹幹：底部略為外張，往上收細，伸進最上層枝片
    kit.add_tube(bm, [(0, 0, 0), (0, 0, 0.35), (0, 0, h * 0.55), (0, 0, h * 0.9)],
                 [p["trunk_radius"] * 1.35, p["trunk_radius"], p["trunk_radius"] * 0.6, p["trunk_radius"] * 0.25],
                 sides=6, cell="bark.m", jitter=0.06, rng=rng)
    tiers = p["tiers"]
    span = h - p["first_tier"]
    for t in range(tiers):
        f = t / (tiers - 1)
        radius = p["base_radius"] + (p["top_radius"] - p["base_radius"]) * f
        z_bottom = p["first_tier"] + span * (f ** 0.9) * 0.76
        tier_h = radius * 1.1 if t < tiers - 1 else h - z_bottom
        add_skirt(bm, rng, min(z_bottom + tier_h, h), z_bottom, radius, p["points"], p["droop"],
                  rng.uniform(0, math.pi), p["group"], sunny=f > 0.7)
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    width = p["base_radius"] * 2
    report = kit.check([obj], budget=(300, 700), target_size=(width, width, h), size_tol=0.1, pivots={name: (0, 0, 0)})
    # 藍框：序章佔位樹（樹冠球寬 3.8 m、頂端約 1.125 × 樹高）
    report["sheet"] = kit.contact_sheet([obj], name, target_bbox=((-1.9, -1.9, 0), (1.9, 1.9, h * 1.05)))
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

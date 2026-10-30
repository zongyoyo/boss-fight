"""
遠山 SM_Mountain_<變體>（ART_STYLE「序章」：MountainCone 換成 3 種有稜線的低面數山形，放在霧裡）。

一圈圈往上收的不規則環（每圈半徑與高度隨機偏移，做出稜線與山肩），頂上收成峰頂；幾座峰疊在一起成山脈。
朝上的面亮、陡坡中階、背光的暗；山尖（每座峰高度 SNOW_LINE 以上，邊界每個面隨機上下）是雪，也照朝向分亮暗。
實際尺寸約 36 m 寬、20 m 高，Unity 端再依位置縮放。
軸心在底部中心，底面封起來（埋在地平線下）。變體：A 單峰、B 雙峰、C 三峰長稜線。
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
    # peaks：(x, y, 底半徑 m, 高 m)
    "A": dict(seed=101, sides=11, peaks=[(0, 0, 18, 20)]),
    "B": dict(seed=102, sides=10, peaks=[(-6, 0, 14, 19), (8, 2, 12, 14)]),
    "C": dict(seed=103, sides=9, peaks=[(-12, 0, 11, 13), (0, 1, 13, 17), (11, -1, 10, 11)]),
}
# 每圈的 (高度比例, 半徑比例)，最上面收成峰頂
RINGS = [(0.0, 1.0), (0.28, 0.72), (0.55, 0.45), (0.8, 0.2)]
SNOW_LINE = 0.6      # 面的中心高於峰高的這個比例就是雪
SNOW_JITTER = 0.08   # 雪線每個面隨機上下的比例，邊界才是鋸齒狀


def add_peak(bm, rng, cx, cy, radius, height, sides):
    rings = []
    for f, rf in RINGS:
        ring = []
        for k in range(sides):
            a = 2 * math.pi * (k + rng.uniform(-0.25, 0.25)) / sides
            r = radius * rf * rng.uniform(0.8, 1.15)
            z = height * f + (height * rng.uniform(-0.05, 0.06) if f > 0 else 0)
            ring.append(bm.verts.new((cx + math.cos(a) * r, cy + math.sin(a) * r, z)))
        rings.append(ring)
    top = bm.verts.new((cx + rng.uniform(-0.05, 0.05) * radius, cy + rng.uniform(-0.05, 0.05) * radius, height))
    faces = []
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            j = (k + 1) % sides
            faces.append(bm.faces.new((a[k], a[j], b[j], b[k])))
    for k in range(sides):
        faces.append(bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], top)))
    faces.append(bm.faces.new(list(reversed(rings[0]))))
    snow, rock = [], []
    for f in faces:
        z = f.calc_center_median().z
        (snow if z > height * (SNOW_LINE + rng.uniform(-SNOW_JITTER, SNOW_JITTER)) else rock).append(f)
    kit.shade_faces(bm, rock, "mountain", up=0.62, down=0.2)
    kit.shade_faces(bm, snow, "snow", up=0.62, down=0.2)


def build(variant, p):
    name = f"SM_Mountain_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    for cx, cy, r, h in p["peaks"]:
        add_peak(bm, rng, cx, cy, r, h, p["sides"])
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=(50, 300), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

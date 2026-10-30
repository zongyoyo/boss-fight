"""
樹樁 SM_Stump_<變體>（ART_STYLE 第 8 節「共用環境套件」：樹樁 2、倒木 SM_Log 2 另一支腳本）。

7 邊樹身、底部外張，3 條往外下插的根；截面是年輪：外圈樹皮、淺色環、中間色環、淺色心。
軸心在底部中心。變體寫在 VARIANTS；Phase 1 只有 A。
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
    "A": dict(seed=51, radius=0.36, height=0.55, sides=7, roots=3, root_reach=0.62,
              # 截面的環：(半徑比例, 下沉 m, 色格)，由外往內
              rings=[(0.82, 0.0, "bark.m"), (0.55, 0.012, "wood_cut.l"), (0.32, 0.02, "wood_cut.m")],
              heart="wood_cut.l"),
}


def build(variant, p):
    name = f"SM_Stump_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    r, h, n = p["radius"], p["height"], p["sides"]
    verts, _ = kit.add_tube(bm, [(0, 0, 0), (0, 0, 0.15), (0, 0, h)], [r * 1.35, r, r * 0.95], sides=n,
                            cell="bark.m", jitter=0.07, rng=rng, caps=False)
    # 底蓋（埋在地裡，封起來才是封閉實體）
    bottom, outer = verts[:n], verts[-n:]
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    bm.faces.new(list(reversed(bottom)))[layer] = kit.cell_index("bark.d")
    # 截面的年輪：一圈圈往內、微微下沉
    prev = outer
    for scale, sink, cell in p["rings"]:
        ring = []
        for v in outer:
            co = v.co.copy()
            co.x *= scale
            co.y *= scale
            co.z = h - sink + rng.uniform(-0.01, 0.01)
            ring.append(bm.verts.new(co))
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((prev[i], prev[j], ring[j], ring[i]))[layer] = kit.cell_index(cell)
        prev = ring
    heart = bm.verts.new((0, 0, h - 0.03))
    for i in range(n):
        bm.faces.new((prev[i], prev[(i + 1) % n], heart))[layer] = kit.cell_index(p["heart"])
    # 根：從樹身下段往外下插的 4 邊粗根（末端鈍頭）
    for k in range(p["roots"]):
        a = 2 * math.pi * (k + rng.uniform(-0.15, 0.15)) / p["roots"] + 0.4
        reach = p["root_reach"] * rng.uniform(0.85, 1.1)
        start = (math.cos(a) * r * 0.75, math.sin(a) * r * 0.75, 0.24)
        mid = (math.cos(a) * reach * 0.7, math.sin(a) * reach * 0.7, 0.08)
        end = (math.cos(a) * reach, math.sin(a) * reach, 0.0)
        _, faces = kit.add_tube(bm, [start, mid, end], [0.17, 0.12, 0.05], sides=4, cell="bark.m", jitter=0.08, rng=rng)
        kit.shade_faces(bm, faces, "bark", up=0.6, down=-0.3)
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=(80, 160), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

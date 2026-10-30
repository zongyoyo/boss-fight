"""
倒木 SM_Log_<變體>（ART_STYLE 第 8 節「共用環境套件」：樹樁 2＋倒木 2；營火座椅之後也用這個）。

橫躺的 7 邊樹幹（沿 X），兩端是年輪截面（外圈淺色環、中間色心），中段帶一根斷掉的短枝。
樹皮朝上的面亮、朝下暗。軸心在底部中心（躺在地上，最低點 z = 0）。
"""
import importlib
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
    # stub：斷枝的 (x 位置, 半徑, 長度)
    "A": dict(seed=81, length=2.4, radius=0.24, sides=7, sag=0.04, stub=(0.35, 0.09, 0.32)),
    "B": dict(seed=82, length=1.5, radius=0.19, sides=7, sag=0.02, stub=(-0.2, 0.07, 0.22)),
}


def end_cap(bm, rng, ring, center, cells):
    """把管子一端的開口封成年輪：一圈淺色環往內，中間收成色心。ring 的繞序要朝外。"""
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    inner = []
    for v in ring:
        co = center + (v.co - center) * rng.uniform(0.58, 0.66)
        inner.append(bm.verts.new(co))
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ring[i], ring[j], inner[j], inner[i]))[layer] = kit.cell_index(cells[0])
    heart = bm.verts.new(center)
    for i in range(n):
        bm.faces.new((inner[i], inner[(i + 1) % n], heart))[layer] = kit.cell_index(cells[1])


def build(variant, p):
    name = f"SM_Log_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    half, r, n = p["length"] / 2, p["radius"], p["sides"]
    pts = [(-half, 0, r), (-half * 0.35, 0.03, r + p["sag"]), (half * 0.35, -0.03, r + p["sag"]), (half, 0, r)]
    verts, faces = kit.add_tube(bm, pts, [r * 1.05, r, r * 0.97, r * 0.9], sides=n, cell="bark.m",
                                jitter=0.06, rng=rng, caps=False)
    kit.shade_faces(bm, faces, "bark", up=0.55, down=-0.3)
    # 兩端的開口：起點那圈照管子方向繞（朝 +X），反過來才朝外
    end_cap(bm, rng, list(reversed(verts[:n])), Vector(pts[0]), ("wood_cut.l", "wood_cut.m"))
    end_cap(bm, rng, verts[-n:], Vector(pts[-1]), ("wood_cut.l", "wood_cut.m"))
    # 斷掉的短枝：從樹幹上方往上斜
    x, sr, length = p["stub"]
    base = (x, 0.02, r * 1.6)
    tip = (x + length * 0.4, -length * 0.3, r * 1.6 + length)
    _, faces = kit.add_tube(bm, [base, tip], [sr, sr * 0.75], sides=5, cell="bark.m", jitter=0.05, rng=rng)
    kit.shade_faces(bm, faces, "bark", up=0.55, down=-0.3)
    # 最低點拉回 z = 0
    z0 = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= z0
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=(60, 200), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

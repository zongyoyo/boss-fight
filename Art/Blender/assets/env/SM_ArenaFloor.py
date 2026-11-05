"""
競技場地板上的小件（ART_STYLE「競技場：風雨交加的黑暗森林」的地板 Arena_Floor）：
- SM_Arena_Slab_<變體>：中央半埋的古老石板，Builder 在魔法陣外圈排成一圈（魔法陣就有了一個合理的位置）。
- SM_Arena_RootFlat_<變體>：貼地的樹根，從場地邊緣往內爬一小段。
- SM_Arena_Puddle_<變體>：水窪，不規則的扁平多邊形。
地板碰撞是平面（y = 0），玩家直接走過這些東西，所以全部貼地：露出地面最多約 3 cm（ART_STYLE「視覺起伏 2 cm 內」，
石板邊緣多 1 cm 才看得出輪廓），其餘埋在地下。軸心在底部中心（地面高度）。沒有碰撞。
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

SLABS = {
    # sides：輪廓邊數；size：(長, 寬)；rune：中間刻一條暗色凹紋
    "A": dict(seed=601, sides=6, size=(1.1, 0.75), rune=True),
    "B": dict(seed=602, sides=7, size=(0.85, 0.7), rune=False),
    "C": dict(seed=603, sides=5, size=(1.3, 0.6), rune=True),
}
ROOTS = {
    # 主根長度、分叉數
    "A": dict(seed=611, length=3.2, forks=2, radius=0.16),
    "B": dict(seed=612, length=2.2, forks=1, radius=0.12),
}
PUDDLES = {
    "A": dict(seed=621, size=(1.6, 1.0), sides=9),
    "B": dict(seed=622, size=(0.9, 0.7), sides=7),
}
SLAB_TOP = 0.03      # 石板頂面離地
SLAB_DEPTH = 0.12    # 往地下埋多深（從斜的角度看不會露出底）
ROOT_TOP = 0.03      # 樹根最高點離地
ROOT_FLAT = 0.3      # 樹根上下壓扁的比例（斷面寬、矮）
PUDDLE_Z = 0.008     # 水窪離地（蓋過地板、不會閃爍）


def outline(rng, sides, size):
    pts = []
    for i in range(sides):
        a = 2 * math.pi * (i + rng.uniform(-0.25, 0.25)) / sides
        r = rng.uniform(0.82, 1.0)
        pts.append(Vector((math.cos(a) * size[0] / 2 * r, math.sin(a) * size[1] / 2 * r, 0)))
    return pts


def finish(name, bm, col, budget):
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=budget, pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


def build_slab(variant, p):
    name = f"SM_Arena_Slab_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    ring = outline(rng, p["sides"], p["size"])
    # 頂面中心微微隆起（中間 3 cm、邊緣 2 cm），平面著色下才有一點切面
    top = [bm.verts.new(v + Vector((0, 0, SLAB_TOP - 0.01))) for v in ring]
    bottom = [bm.verts.new(v * 1.08 + Vector((0, 0, -SLAB_DEPTH))) for v in ring]
    center = bm.verts.new((rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), SLAB_TOP))
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((top[i], top[j], center))
        # 暗林的地上：石板頂面以暗階為主，不能比泥地亮太多（戰鬥預兆要在它上面看得清楚）
        f[layer] = kit.cell_index("stone_dark.m" if rng.random() < 0.55 else "stone.d")
        f = bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
        f[layer] = kit.cell_index("stone_dark.m")
    bm.faces.new(list(reversed(bottom)))[layer] = kit.cell_index("stone_dark.m")
    if p["rune"]:
        # 刻紋：一條穿過中心的暗色細長菱形，貼在頂面上 2 mm
        a = rng.uniform(0, math.pi)
        d = Vector((math.cos(a), math.sin(a), 0))
        w = Vector((-d.y, d.x, 0))
        L = min(p["size"]) * 0.32
        z = Vector((0, 0, SLAB_TOP + 0.002))
        q = [bm.verts.new(z + d * L), bm.verts.new(z + w * 0.05), bm.verts.new(z - d * L), bm.verts.new(z - w * 0.05)]
        bm.faces.new(q)[layer] = kit.cell_index("stone_dark.m")
    return finish(name, bm, col, (12, 60))


def build_root(variant, p):
    name = f"SM_Arena_RootFlat_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    r = p["radius"]
    L = p["length"]
    # 主根沿 +X 蜿蜒（Builder 讓 +X 指向場地中央），末端收尖
    pts = [Vector((L * t, math.sin(t * 5.5 + rng.uniform(0, 1)) * 0.25, 0)) for t in (0, 0.25, 0.5, 0.75, 1.0)]
    branches = [(pts, [r, r * 0.85, r * 0.65, r * 0.4, 0.0])]
    for k in range(p["forks"]):
        s = pts[1 + k]
        side = 1 if k % 2 == 0 else -1
        a = math.radians(side * rng.uniform(30, 55))
        d = Vector((math.cos(a), math.sin(a), 0))
        fl = L * rng.uniform(0.3, 0.45)
        branches.append(([s, s + d * fl * 0.5 + Vector((0, side * 0.08, 0)), s + d * fl], [r * 0.6, r * 0.4, 0.0]))
    for bp, br in branches:
        verts, faces = kit.add_tube(bm, bp, br, sides=5, cell="bark_dark.m", jitter=0.08, rng=rng, caps=False)
        for v in verts:
            v.co.z *= ROOT_FLAT
        kit.shade_faces(bm, faces, "bark_dark", up=0.7, down=-0.3)
    # 壓扁後最高點放到離地 ROOT_TOP，其餘埋在地下
    z1 = max(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z += ROOT_TOP - z1
    return finish(name, bm, col, (20, 120))


def build_puddle(variant, p):
    name = f"SM_Arena_Puddle_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    ring = outline(rng, p["sides"], p["size"])
    # 外圈一圈濕泥（暗），中間是水
    outer = [bm.verts.new(v * 1.18 + Vector((0, 0, PUDDLE_Z - 0.002))) for v in ring]
    inner = [bm.verts.new(v + Vector((0, 0, PUDDLE_Z))) for v in ring]
    c = bm.verts.new((0, 0, PUDDLE_Z))
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((outer[i], outer[j], inner[j], inner[i]))[layer] = kit.cell_index("mud.d")
        bm.faces.new((inner[i], inner[j], c))[layer] = kit.cell_index("water.d" if rng.random() < 0.8 else "mountain.d")
    return finish(name, bm, col, (10, 60))


result = {f"Slab_{v}": build_slab(v, p) for v, p in SLABS.items()}
result.update({f"RootFlat_{v}": build_root(v, p) for v, p in ROOTS.items()})
result.update({f"Puddle_{v}": build_puddle(v, p) for v, p in PUDDLES.items()})

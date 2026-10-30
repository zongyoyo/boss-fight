"""
枯樹 SM_Tree_Dead_<變體>、枯枝 SM_Twig_<變體>（ART_STYLE 第 8 節「共用環境套件」：枯樹 2＋枯枝 3）。

How to Fish 前景那種細黑的枯枝：細 5 邊主幹、幾根往上斜的分枝，末端收尖；全部 dead_wood 色，
朝上的面亮、朝下的面暗。枯樹站著（軸心在底部中心），枯枝是地上的一小叢斜插的細枝（軸心在底部中心）。
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

# 每根枝：折線點與每點半徑（最後一點 0 = 收尖）
TREES = {
    "A": dict(seed=61, sides=5, trunk=([(0, 0, 0), (0.05, 0, 1.4), (-0.1, 0.05, 2.9), (0.05, -0.05, 4.4)],
                                       [0.16, 0.12, 0.07, 0.0]),
              branches=[([(0.03, 0, 1.6), (0.6, 0.1, 2.3), (1.0, -0.05, 3.1)], [0.06, 0.04, 0.0]),
                        ([(-0.05, 0.03, 2.4), (-0.6, -0.1, 3.0), (-0.85, 0.15, 3.6)], [0.05, 0.03, 0.0]),
                        ([(-0.02, 0.04, 3.2), (0.1, 0.6, 3.8), (0.0, 0.85, 4.2)], [0.04, 0.025, 0.0])]),
    "B": dict(seed=62, sides=5, trunk=([(0, 0, 0), (0.15, 0.05, 1.0), (0.05, 0.0, 2.0), (0.25, 0.1, 2.7)],
                                       [0.14, 0.11, 0.08, 0.05]),
              branches=[([(0.12, 0.04, 1.2), (-0.45, 0.2, 1.9), (-0.7, 0.05, 2.5)], [0.05, 0.035, 0.0]),
                        ([(0.08, 0.02, 1.8), (0.7, -0.25, 2.4)], [0.04, 0.0])]),
}
TWIGS = {
    # 一叢 stems 根斜插的細枝，各帶 0–1 根小分叉
    "A": dict(seed=71, stems=3, length=(0.5, 0.8), lean=(0.3, 0.7), radius=0.025, forks=2),
    "B": dict(seed=72, stems=4, length=(0.7, 1.15), lean=(0.2, 0.55), radius=0.03, forks=3),
    "C": dict(seed=73, stems=2, length=(0.35, 0.55), lean=(0.5, 0.9), radius=0.02, forks=1),
}


def finish(name, bm, col, budget):
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=budget, pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=True)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


def build_tree(variant, p):
    name = f"SM_Tree_Dead_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    for points, radii in [p["trunk"]] + p["branches"]:
        _, faces = kit.add_tube(bm, points, radii, sides=p["sides"], cell="dead_wood.m", jitter=0.08, rng=rng)
        kit.shade_faces(bm, faces, "dead_wood", up=0.5, down=-0.3)
    return finish(name, bm, col, (50, 200))


def build_twig(variant, p):
    name = f"SM_Twig_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    forks = p["forks"]
    for i in range(p["stems"]):
        a = 2 * math.pi * (i + rng.uniform(-0.3, 0.3)) / p["stems"]
        d = (math.cos(a), math.sin(a))
        length = rng.uniform(*p["length"])
        lean = rng.uniform(*p["lean"])
        base = (d[0] * 0.04, d[1] * 0.04, -0.03)  # 稍微插進土裡
        mid = (d[0] * length * lean * 0.45, d[1] * length * lean * 0.45, length * 0.5 * (1 - lean * 0.3))
        tip = (d[0] * length * lean, d[1] * length * lean, length * (1 - lean * 0.4))
        r = p["radius"]
        _, faces = kit.add_tube(bm, [base, mid, tip], [r, r * 0.7, 0.0], sides=4, cell="dead_wood.m", jitter=0.1, rng=rng)
        kit.shade_faces(bm, faces, "dead_wood", up=0.5, down=-0.3)
        if forks > 0:
            forks -= 1
            side = a + rng.choice((-1, 1)) * rng.uniform(0.6, 1.0)
            end = (mid[0] + math.cos(side) * length * 0.3, mid[1] + math.sin(side) * length * 0.3, mid[2] + length * 0.25)
            _, faces = kit.add_tube(bm, [mid, end], [r * 0.6, 0.0], sides=3, cell="dead_wood.m", rng=rng)
            kit.shade_faces(bm, faces, "dead_wood", up=0.5, down=-0.3)
    # 軸心在底部中心：把最低點拉回 z = 0
    z0 = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= z0
    return finish(name, bm, col, (16, 200))


result = {f"Dead_{v}": build_tree(v, p) for v, p in TREES.items()}
result.update({f"Twig_{v}": build_twig(v, p) for v, p in TWIGS.items()})

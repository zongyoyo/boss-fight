"""
闊葉樹 SM_Tree_Broad_<變體>（ART_STYLE 第 8 節「共用環境套件」）。

微彎的 6 邊樹幹分出兩根枝，頂上 3 團多面體葉叢（變形 icosphere），葉叢上亮下暗。
軸心在樹幹底部中心；樹幹碰撞由 Unity 端加。變體寫在 VARIANTS；Phase 1 只有 A。
"""
import importlib
import os
import random
import sys

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)

VARIANTS = {
    "A": dict(seed=21, height=6.5, trunk_radius=0.26,
              # 樹幹折線（x, y, z）與每點半徑比例
              trunk=[(0, 0, 0), (0.04, 0, 0.4), (0.22, 0.08, 2.1), (0.12, 0.16, 3.5), (-0.08, 0.14, 4.4)],
              trunk_scale=[1.35, 1.0, 0.82, 0.6, 0.38],
              # 枝：起點、終點、起點半徑
              branches=[((0.2, 0.1, 2.5), (1.15, -0.3, 3.6), 0.11), ((0.06, 0.18, 3.3), (-0.95, 0.5, 4.2), 0.09)],
              # 葉叢：中心、半徑、壓扁比例
              clumps=[((-0.05, 0.12, 5.35), 1.45, 0.78), ((-1.05, 0.5, 4.45), 1.05, 0.8), ((1.3, -0.3, 4.05), 1.15, 0.8)]),
}


def build(variant, p):
    name = f"SM_Tree_Broad_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    r = p["trunk_radius"]
    kit.add_tube(bm, p["trunk"], [r * s for s in p["trunk_scale"]], sides=6, cell="bark.m", jitter=0.08, rng=rng)
    for start, end, br in p["branches"]:
        mid = tuple(a + (b - a) * 0.5 for a, b in zip(start, end))
        mid = (mid[0], mid[1], mid[2] + 0.15)  # 枝微微上拱
        kit.add_tube(bm, [start, mid, end], [br, br * 0.7, br * 0.4], sides=5, cell="bark.m", jitter=0.08, rng=rng)
    for center, radius, squash in p["clumps"]:
        _, faces = kit.add_icosphere(bm, radius, 1, location=center, scale=(1, 1, squash),
                                     rotation=(0, 0, rng.uniform(0, 360)), cell="leaf.m", jitter=0.14, rng=rng)
        kit.shade_faces(bm, faces, "leaf", up=0.35, down=-0.3)
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=(250, 500), target_size=(0, 0, p["height"]), size_tol=0.06, pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, target_bbox=((-1.9, -1.9, 0), (1.9, 1.9, p["height"] * 1.05)))
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

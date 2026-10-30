"""
灌木 SM_Bush_<變體>（ART_STYLE 第 8 節「共用環境套件」）。

幾團壓扁的 20 面體擠在一起的低矮葉團，填樹和石頭之間的空隙；上亮下暗，底部埋進地裡一點。
軸心在底部中心。變體寫在 VARIANTS：A 中、B 小、C 寬扁（苔綠）。
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
    # 葉團：(中心 x, y, z, 半徑)；group 是色票組
    "A": dict(seed=91, squash=0.75, group="leaf",
              clumps=[(0, 0, 0.45, 0.55), (0.5, 0.15, 0.32, 0.4), (-0.45, -0.1, 0.3, 0.42), (0.05, 0.42, 0.3, 0.38)]),
    "B": dict(seed=92, squash=0.8, group="leaf",
              clumps=[(0, 0, 0.3, 0.38), (0.3, 0.12, 0.2, 0.28), (-0.22, 0.15, 0.2, 0.26)]),
    "C": dict(seed=93, squash=0.6, group="moss",
              clumps=[(0, 0, 0.35, 0.6), (0.7, 0.1, 0.28, 0.48), (-0.7, -0.05, 0.26, 0.5), (0.25, -0.5, 0.22, 0.4),
                      (-0.3, 0.5, 0.24, 0.42)]),
}


def build(variant, p):
    name = f"SM_Bush_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    for x, y, z, r in p["clumps"]:
        _, faces = kit.add_icosphere(bm, r, 0, location=(x, y, z), scale=(1, 1, p["squash"]),
                                     rotation=(0, 0, rng.uniform(0, 360)), jitter=0.12, rng=rng)
        kit.shade_faces(bm, faces, p["group"], up=0.6, down=-0.2)
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=(60, 150), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

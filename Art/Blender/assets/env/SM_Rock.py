"""
石頭 SM_Rock_<變體>（ART_STYLE 第 8 節「共用環境套件」：碎石、中石、大岩各 2）。

變形 icosphere 加幾刀平切面、底部壓平；朝上的面亮、朝下暗。軸心在底部中心。
變體寫在 VARIANTS；Phase 1 只有中石 A。
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
    # 中石：約 1.2 m 寬、0.7 m 高
    "A": dict(seed=31, radius=0.62, squash=0.72, cuts=3, budget=(60, 150)),
}


def build(variant, p):
    name = f"SM_Rock_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    _, faces = kit.add_rock(bm, p["radius"], rng, squash=p["squash"], cuts=p["cuts"])
    kit.shade_faces(bm, faces, "stone", up=0.55, down=-0.25)
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=p["budget"], pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

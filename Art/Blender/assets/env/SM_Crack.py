"""
地裂 SM_Crack_<變體>（ART_STYLE「序章」的地裂類：鋸齒狀薄片貼在地面，裂縫內暗、邊緣發光）。

一條鋸齒折線往兩端收尖，橫切成五條帶：外側泥土唇（mud）→ 暗紅發光（ember 暗階，走 M_Palette_Emissive）→ 中間黑縫（dark），
另一邊對稱；可以有一兩條細分岔。整片是平的（法線朝上），高出地面 LIFT 避免和地面打架。
沿 Blender Y 軸（Unity 的 Z 軸）置中、軸心在中心，Builder 只要擺位置與朝向。
序章用在守衛破土處的伏筆（ForeshadowCracks）；Phase 5 的 GroundCracks、Fissure 之後也從這裡取。
"""
import importlib
import math
import os
import random
import sys

import bmesh
from mathutils import Vector

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)

LIFT = 0.012  # 高出地面（m）
# 由外往內的帶：(到中心線的距離比例, 色格)；最外側比例 1 = 半寬
BANDS = [(1.0, "mud.m"), (0.62, "ember.d"), (0.3, "dark.m")]

VARIANTS = {
    # length：長度；width：最寬處的半寬；kinks：折點數；zig：折線左右擺動（m）；branches：(從第幾段分出, 長度比例, 角度 度)
    "A": dict(seed=111, length=2.4, width=0.17, kinks=6, zig=0.09, branches=[(3, 0.3, 40)]),
    "B": dict(seed=112, length=1.9, width=0.14, kinks=5, zig=0.08, branches=[(2, 0.28, -35), (3, 0.22, 30)]),
    "C": dict(seed=113, length=2.8, width=0.19, kinks=7, zig=0.1, branches=[]),
}


def centerline(rng, start, direction, length, kinks, zig):
    """不規則的折線：折點間距與左右偏移都隨機，偏移是隨機漫步（不是左右交錯，交錯的話像閃電符號）。"""
    side = Vector((-direction.y, direction.x, 0))
    cuts = sorted(rng.uniform(0.08, 0.92) for _ in range(kinks - 1))
    pts, offset = [start], 0.0
    for f in cuts:
        offset = max(-zig * 1.5, min(zig * 1.5, offset + rng.uniform(-zig, zig)))
        pts.append(start + direction * (length * f) + side * offset)
    pts.append(start + direction * length + side * (offset * 0.5))
    return pts


def add_crack(bm, pts, width):
    """沿折線鋪五條帶：每個點橫切出 2 × len(BANDS) 個點（左右各一組），寬度兩端收成 0。"""
    n = len(pts)
    rows = []
    for i, p in enumerate(pts):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, n - 1)]
        t = (b - a).normalized()
        side = Vector((-t.y, t.x, 0))
        w = width * math.sin(math.pi * i / (n - 1)) ** 0.7  # 兩端收尖、中段最寬
        left = [p + side * (w * r) for r, _ in BANDS]                # 外 → 內
        right = [p - side * (w * r) for r, _ in reversed(BANDS)]     # 內 → 外
        rows.append([bm.verts.new((v.x, v.y, LIFT)) for v in left + right])
    cells = [c for _, c in BANDS] + [c for _, c in reversed(BANDS[:-1])]
    for r0, r1 in zip(rows, rows[1:]):
        for k, cell in enumerate(cells):
            if k + 1 >= len(r0):
                break
            f = bm.faces.new((r0[k], r0[k + 1], r1[k + 1], r1[k]))
            kit._paint_faces(bm, [f], cell)


def build(variant, p):
    name = f"SM_Crack_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    length = p["length"]
    main = centerline(rng, Vector((0, -length / 2, 0)), Vector((0, 1, 0)), length, p["kinks"], p["zig"])
    add_crack(bm, main, p["width"])
    for seg, share, angle in p["branches"]:
        a = math.radians(angle)
        d = Vector((math.sin(a), math.cos(a), 0))
        branch = centerline(rng, main[seg], d, length * share, 3, p["zig"] * 0.5)
        add_crack(bm, branch, p["width"] * 0.55)
    # 中心線兩端收成 0 寬會有退化的面：合併重疊的點
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    degenerate = [f for f in bm.faces if f.calc_area() < 1e-9]
    if degenerate:
        bmesh.ops.delete(bm, geom=degenerate, context="FACES")
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    # 平的開放網格「朝外」沒有定義，recalc 可能整片朝下：朝下的面翻過來（材質是單面的）
    fix = bmesh.new()
    fix.from_mesh(obj.data)
    for f in fix.faces:
        if f.normal.z < 0:
            f.normal_flip()
    fix.to_mesh(obj.data)
    fix.free()
    kit.normals_up(obj)
    report = kit.check([obj], budget=(20, 200))
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

"""
營火 SM_Campfire 與座椅圓木 SM_SeatLog（ART_STYLE「休息區（Phase 6）」）。

營火：9 顆石頭圍一圈（半徑 0.75 m，對齊 HubBuilder 的佔位石頭與走不進火堆的碰撞 0.85 m）、圈內灰燼與炭火、
交叉成錐形的柴堆、3 層低面數火焰片（外層橘紅、中層橘、內層黃，發光色格）。
火焰片是子物件 Flame_0–2（軸心在火焰底部），Unity 的 FlameFlicker 各自縮放閃爍。
座椅圓木：沿 X 的 8 邊圓木（長 1.6、直徑 0.49），頂面削平到 0.44 m 好坐，兩端年輪截面；對齊佔位的 SeatLog（中心在 0.22 m 高）。
座標照 Unity 站點本地想（hub.U），軸心在地面中心。
"""
import importlib
import math
import os
import sys

from mathutils import Vector

_HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (os.path.join(_HERE, "..", "..", "lib"), _HERE):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import lowpoly_kit as kit  # noqa: E402
import hub_common as hub  # noqa: E402

importlib.reload(kit)
importlib.reload(hub)

U = hub.U
SEED = 621
STONES = 9
RING = 0.75
# 每層火焰：(名稱, 舌數, 底半徑, 高, 色格)
FLAMES = [
    ("Flame_0", 4, 0.24, 0.5, "ember.m"),
    ("Flame_1", 3, 0.16, 0.95, "fire.m"),
    ("Flame_2", 3, 0.13, 0.65, "lantern.m"),
]
FIRE_BUDGET = (300, 600)
SEAT = dict(length=1.6, radius=0.245, sides=8, flat=0.05)  # 頂面 0.44 m = 佔位圓木的頂（刀盾狗坐姿的高度）


def stones(bm, rng):
    for i in range(STONES):
        a = i * 2 * math.pi / STONES + rng.uniform(-0.08, 0.08)
        r = RING + rng.uniform(-0.03, 0.05)
        verts, faces = kit.add_rock(bm, rng.uniform(0.17, 0.22), rng, location=U(math.sin(a) * r, 0, math.cos(a) * r),
                                    squash=0.65, cuts=2, cell="stone.m", jitter=0.12, subdivisions=0)
        kit.shade_faces(bm, faces, "stone", up=0.5, down=-0.2)


def bed(bm, rng):
    # 灰燼盤與幾塊炭火
    kit.add_prism(bm, 9, 0.62, 0.04, location=U(0, 0, 0), cell="stone_dark.m", rng=rng, jitter=0.06)
    for i in range(5):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.1, 0.4)
        kit.add_icosphere(bm, rng.uniform(0.06, 0.09), 0, location=U(math.sin(a) * r, 0.05, math.cos(a) * r),
                          scale=(1, 1, 0.5), cell="ember.m", rng=rng, jitter=0.15)


def firewood(bm, rng):
    """6 根柴斜靠成錐形，上端在火焰中段交會。"""
    n = 6
    for i in range(n):
        a = i * 2 * math.pi / n + 0.3
        foot = Vector(U(math.sin(a) * 0.5, 0.04, math.cos(a) * 0.5))
        top = Vector(U(math.sin(a) * 0.08, 0.5 + rng.uniform(-0.04, 0.04), math.cos(a) * 0.08))
        verts, faces = kit.add_tube(bm, [foot, top], [0.06, 0.05], sides=5, cell="bark.m", jitter=0.05, rng=rng)
        kit.shade_faces(bm, faces, "bark", up=0.5, down=-0.2)
        # 上端燒黑
        layer = bm.faces.layers.int.get(kit.CELL_ATTR)
        for f in faces:
            if f.calc_center_median().z > 0.35:
                f[layer] = kit.cell_index("dark.m")


def flame(name, col, rng, tongues, radius, height, cell):
    """一層火焰：幾支扭轉的尖錐，一支在中間最高，其餘往外斜。"""
    bm = kit.new_bmesh()
    for k in range(tongues):
        if k == 0:
            off, h, r, lean = Vector((0, 0, 0)), height, radius, Vector((0, 0, 0))
        else:
            a = (k - 1) * 2 * math.pi / max(tongues - 1, 1) + rng.uniform(-0.3, 0.3)
            d = Vector((math.cos(a), math.sin(a), 0))
            off, h, r, lean = d * radius * 0.7, height * rng.uniform(0.55, 0.75), radius * 0.75, d * radius * 0.6
        base = off + Vector((0, 0, 0.12))
        mid = base + lean * 0.5 + Vector((0, 0, h * 0.45))
        tip = base + lean + Vector((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), h))
        kit.add_tube(bm, [base, mid, tip], [r * 0.8, r, 0.0], sides=5, cell=cell, jitter=0.08, rng=rng)
    return hub.make(name, bm, col)


def build_fire():
    name = "SM_Campfire"
    rng = hub.rng_for(SEED)
    col = kit.begin(name)
    bm = kit.new_bmesh()
    stones(bm, rng)
    bed(bm, rng)
    firewood(bm, rng)
    root = hub.make(name, bm, col)
    objs = [root]
    for fname, tongues, radius, height, cell in FLAMES:
        f = flame(fname, col, rng, tongues, radius, height, cell)
        f.parent = root
        objs.append(f)
    report = kit.check(objs, budget=FIRE_BUDGET, pivots={name: (0, 0, 0), **{f[0]: (0, 0, 0) for f in FLAMES}})
    report["sheet"] = kit.contact_sheet(objs, name, scale_ref=False)
    report["fbx"] = kit.export_fbx(objs, f"Hub/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


def end_cap(bm, ring, center):
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    inner = [bm.verts.new(center + (v.co - center) * 0.62) for v in ring]
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ring[i], ring[j], inner[j], inner[i]))[layer] = kit.cell_index("wood_cut.l")
    heart = bm.verts.new(center)
    for i in range(n):
        bm.faces.new((inner[i], inner[(i + 1) % n], heart))[layer] = kit.cell_index("wood_cut.m")


def build_seat():
    name = "SM_SeatLog"
    rng = hub.rng_for(SEED + 1)
    col = kit.begin(name)
    bm = kit.new_bmesh()
    half, r, n = SEAT["length"] / 2, SEAT["radius"], SEAT["sides"]
    pts = [(-half, 0, r), (-half * 0.3, 0, r * 1.02), (half * 0.3, 0, r * 1.02), (half, 0, r)]
    verts, faces = kit.add_tube(bm, pts, [r * 1.04, r, r, r * 0.96], sides=n, cell="bark.m", jitter=0.04, rng=rng, caps=False)
    # 削平頂面：坐的地方
    top = 2 * r - SEAT["flat"]
    for v in verts:
        v.co.z = min(v.co.z, top)
    kit.shade_faces(bm, faces, "bark", up=0.55, down=-0.3)
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        f.normal_update()
        if f.normal.z > 0.95:
            f[layer] = kit.cell_index("wood_cut.d")
    end_cap(bm, list(reversed(verts[:n])), Vector(pts[0]))
    end_cap(bm, verts[-n:], Vector(pts[-1]))
    z0 = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= z0
    obj = hub.make(name, bm, col)
    report = kit.check([obj], budget=(60, 200), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Hub/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {"fire": build_fire(), "seat": build_seat()}

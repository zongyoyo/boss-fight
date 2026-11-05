"""
競技場入口 SM_Gate_Thorns（ART_STYLE「競技場：風雨交加的黑暗森林」的入口 PlayerGate）。

兩棵巨樹之間的缺口，缺口被一道荊棘樹根封住：
- 兩側巨樹（取代門柱 Pillar_L／R）：粗樹幹往中間微傾，上段的粗枝往對面拱、在缺口上方交會成拱門；板根只往兩側與後方長，不伸進場地。
- 荊棘（取代門 Door）：幾條扭曲的粗根從一側地面斜爬到另一側高處、彼此交錯，沿路長出尖刺。
碰撞仍是 Builder 的門柱與門（ArenaWall 層，位置、大小不變），這個模型只是外觀、沒有碰撞。
座標跟 Builder 的入口物件一樣：軸心在牆內緣線上的缺口中心（地面），內面朝場地（Blender −Y，Unity +Z），
門在後方 y ≈ 0.3–1.5、門柱中心在 x = ±(門寬 / 2 + 0.6)、y = 牆厚 / 2。
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

NAME = "SM_Gate_Thorns"
SEED = 501
GATE_W = 3.5        # ArenaData.gateWidth
WALL_T = 1.5        # ArenaData.wallThickness
WALL_H = 4.0        # ArenaData.wallHeight
PILLAR_X = GATE_W / 2 + 0.6
TRUNK_R = 0.95
TRUNK_H = 6.2
ARCH_Z = 5.6        # 兩側粗枝在缺口上方交會的高度
INNER_LIMIT = -0.35  # 內面最多伸進場地這麼多（跟門柱碰撞一樣，門柱本來就比牆內緣多 0.4 m）
THORN_LEN = (0.28, 0.5)
BUDGET = (400, 1200)  # 兩棵巨樹加荊棘，比 ART_STYLE 原本估的 400–900 多（實際面數記在 ART_STYLE）


def dir2(deg):
    a = math.radians(deg)
    return Vector((math.cos(a), math.sin(a), 0.0))


def shade_mossy(bm, faces, rng, chance=0.6):
    kit.shade_faces(bm, faces, "bark_dark", up=0.55, down=-0.3)
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        if f.normal.z >= 0.6 and rng.random() < chance:
            f[layer] = kit.cell_index("moss." + ("m" if rng.random() < 0.7 else "d"))


def tube(bm, rng, pts, radii, sides, moss=0.6, jitter=0.1):
    verts, faces = kit.add_tube(bm, pts, radii, sides=sides, cell="bark_dark.m", jitter=jitter, rng=rng)
    shade_mossy(bm, faces, rng, moss)
    return verts, faces


def trunk(bm, rng, side):
    """一側的巨樹：從門柱位置長出，往缺口傾，頂上收成斷梢。side = -1 左、+1 右。"""
    base = Vector((side * PILLAR_X, WALL_T / 2, 0))
    lean = Vector((-side * 0.55, 0.15, 0))
    pts, radii = [], []
    for i in range(6):
        t = i / 5
        wobble = Vector((math.sin(t * 5 + side) * 0.18, math.cos(t * 4) * 0.12, 0))
        pts.append(base + lean * (t ** 1.4) + wobble + Vector((0, 0, TRUNK_H * t)))
        radii.append(TRUNK_R * (1.4 if i == 0 else 1.0 - 0.55 * t))
    # 頂端收成斷梢（不要平切的樹樁頭）
    pts.append(pts[-1] + Vector((-side * 0.2, 0.1, 1.3)))
    radii.append(0.0)
    tube(bm, rng, pts, radii, 9)
    # 板根：只往外側與後方（角度以 +X 為 0，往後是 +Y = 90）
    angles = (0, 50, 105) if side > 0 else (180, 130, 75)
    for a in angles:
        a += rng.uniform(-10, 10)
        d = dir2(a)
        reach = TRUNK_R * rng.uniform(2.3, 2.9)
        rp = [base + d * TRUNK_R * 0.3 + Vector((0, 0, TRUNK_R * 2.0)),
              base + d * TRUNK_R * 1.1 + Vector((0, 0, TRUNK_R * 0.9)),
              base + d * reach + Vector((0, 0, 0.08)),
              base + d * reach * 1.3 + Vector((0, 0, -0.15))]
        verts, _ = tube(bm, rng, rp, [TRUNK_R * 0.55, TRUNK_R * 0.4, TRUNK_R * 0.16, 0.0], 5)
        perp = dir2(a + 90)
        for v in verts:
            rel = v.co - base
            v.co -= perp * rel.dot(perp) * 0.5
    return pts


def arch_branch(bm, rng, start, side, height):
    """往對面拱的粗枝：從樹幹上段往缺口中央上方伸，末端越過中線往下垂、收尖。"""
    mid_x = -side * 0.2
    pts = [start,
           Vector((start.x * 0.55, start.y + 0.1, height - 0.1)),
           Vector((mid_x, start.y + 0.25, height + 0.25)),
           Vector((-side * PILLAR_X * 0.55, start.y + 0.2, height - 0.35)),
           Vector((-side * PILLAR_X * 0.8, start.y + 0.35, height - 1.3))]
    tube(bm, rng, pts, [0.42, 0.33, 0.24, 0.12, 0.0], 6, moss=0.7)
    for q in pts[1:4]:
        if rng.random() < 0.7:
            moss_drape(bm, rng, q)


def moss_drape(bm, rng, top):
    length = rng.uniform(0.6, 1.3)
    sway = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.1
    pts = [top, top + sway - Vector((0, 0, length * 0.5)), top + sway * 1.6 - Vector((0, 0, length))]
    _, faces = kit.add_tube(bm, pts, [0.08, 0.05, 0.0], sides=3, cell="moss.d", jitter=0.1, rng=rng)


def thorn_vine(bm, rng, pts, radius):
    """荊棘根：扭曲的粗根，沿路每隔一段長一根往外的尖刺。"""
    verts, faces = tube(bm, rng, pts, [radius * (1.0 - 0.45 * i / (len(pts) - 1)) for i in range(len(pts))], 6,
                        moss=0.25, jitter=0.12)
    for a, b in zip(pts, pts[1:]):
        seg = b - a
        for k in range(2):
            p = a.lerp(b, rng.uniform(0.15, 0.85))
            axis = seg.normalized()
            # 跟路徑垂直的隨機方向，偏向前方（場地那一側）與上方，玩家看得到
            rnd = Vector((rng.uniform(-1, 1), rng.uniform(-1, -0.2), rng.uniform(-0.3, 1)))
            out = (rnd - axis * rnd.dot(axis)).normalized()
            length = rng.uniform(*THORN_LEN)
            _, tf = kit.add_tube(bm, [p, p + out * length], [radius * 0.32, 0.0], sides=3, cell="dead_wood.d",
                                 rng=rng)


def build():
    rng = random.Random(SEED)
    col = kit.begin(NAME)
    bm = kit.new_bmesh()
    for side in (-1, 1):
        pts = trunk(bm, rng, side)
        arch_branch(bm, rng, pts[3], side, ARCH_Z + rng.uniform(-0.2, 0.2))
        # 往外側的另一根短枝，讓樹冠不只往內長
        s = pts[4]
        d = Vector((side, 0.3, 0)).normalized()
        tube(bm, rng, [s, s + d * 1.2 + Vector((0, 0, 0.7)), s + d * 2.2 + Vector((0, 0, 0.2))], [0.3, 0.18, 0.0], 5)
        moss_drape(bm, rng, s + d * 1.2 + Vector((0, 0, 0.6)))
        # 稀疏的暗綠葉團：外側短枝末端與樹頂
        for c, r in ((s + d * 2.0 + Vector((0, 0, 0.55)), 0.9), (pts[5] + Vector((-side * 0.4, 0.2, 0.6)), 1.1)):
            _, faces = kit.add_icosphere(bm, r, 0, location=c, scale=(1.25, 1.1, 0.6),
                                         rotation=(0, 0, rng.uniform(0, 360)), cell="pine.m", jitter=0.18, rng=rng)
            kit.shade_faces(bm, faces, "pine", up=0.72, down=-0.1)
    # 荊棘：交錯的斜根，兩端扎進兩側樹幹與地面
    half = GATE_W / 2
    vines = [
        [(-half - 0.3, 0.9, -0.2), (-0.9, 0.7, 1.1), (0.4, 0.95, 2.4), (half + 0.2, 0.8, 3.7)],
        [(half + 0.3, 0.6, -0.2), (0.8, 0.5, 1.4), (-0.6, 0.75, 2.7), (-half - 0.2, 0.9, 3.9)],
        [(-half - 0.2, 1.1, 1.6), (-0.3, 0.85, 1.85), (0.6, 1.0, 1.5), (half + 0.2, 1.15, 2.0)],
        [(-0.8, 0.9, -0.2), (-0.4, 0.6, 0.8), (0.3, 0.75, 1.6), (0.5, 0.9, 3.0), (0.1, 1.0, 4.1)],
        [(0.9, 1.2, -0.2), (0.5, 1.0, 0.9), (-0.2, 1.2, 2.0), (-1.1, 1.0, 3.1)],
    ]
    for i, v in enumerate(vines):
        thorn_vine(bm, rng, [Vector(p) for p in v], 0.24 if i < 2 else 0.18)
    # 荊棘腳下的土堆，把根扎進去的地方蓋住
    verts, faces = kit.add_rock(bm, 1.6, rng, location=(0, 1.0, -0.12), squash=0.32, cuts=1, cell="mud.m", jitter=0.15)
    kit.shade_faces(bm, faces, "mud", up=0.75, down=-0.3)
    for v in bm.verts:
        if v.co.y < INNER_LIMIT:
            v.co.y = INNER_LIMIT
    obj = kit.make_object(NAME, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=BUDGET, pivots={NAME: (0, 0, 0)})
    # 藍框：Builder 原本的門柱與門的範圍
    report["sheet"] = kit.contact_sheet([obj], NAME, target_bbox=((-PILLAR_X - 0.6, -0.4, 0), (PILLAR_X + 0.6, WALL_T + 0.4, WALL_H + 1.2)))
    report["fbx"] = kit.export_fbx([obj], f"Env/{NAME}.fbx")
    report["blend"] = kit.save_blend(NAME)
    return report


result = {NAME: build()}

"""
村長小屋 SM_Hut（ART_STYLE「休息區（Phase 6）」）。

附圖（How to Fish）最直接的參考：一片片獨立的橫向木板牆（下緣往外翹一點，看得出搭接）、四角立柱、
4 格窗（玻璃是暖色發光，傍晚屋裡亮燈）、拼板門加 Z 形斜撐、出簷的兩坡屋頂（屋脊前後向，山牆對著門口）、
屋脊板與封簷板、石塊地基、門前木平台與台階、門邊一盞掛燈、屋頂後方一支石砌煙囪。

大小對齊 HubBuilder 的碰撞盒（寬 6、深 5、高 3 m；Unity 本地 +Z 是門口）：牆外面剛好在碰撞盒上，
屋簷、門前平台與台階伸出碰撞盒（玩家走得到平台下方，村長站在平台前 0.5 m）。軸心在地面中心。
"""
import importlib
import math
import os
import sys

from mathutils import Matrix, Vector

_HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (os.path.join(_HERE, "..", "..", "lib"), _HERE):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import lowpoly_kit as kit  # noqa: E402
import hub_common as hub  # noqa: E402

importlib.reload(kit)
importlib.reload(hub)

NAME = "SM_Hut"
SEED = 601
W, D = 3.0, 2.5           # 牆外面的半寬（Blender X）、半深（Blender Y），= 碰撞盒
FOOT = 0.32               # 石塊地基高（地板高）
WALL_TOP = 3.0
ROWS = 8                  # 每面牆幾排橫板
PLANK_T = 0.08
LAP = 5.0                 # 橫板下緣往外翹的角度
PITCH = 38.0              # 屋頂坡度
EAVE = 0.45               # 屋簷伸出牆外（左右）
GABLE_OVER = 0.45         # 屋頂伸出山牆（前後）
DOOR = (-0.6, 0.6, FOOT, 2.4)                       # 門洞 (u0, u1, z0, z1)
FRONT_WINDOWS = [(-1.9, 1.8), (1.9, 1.8)]           # (u, z 中心)
SIDE_WINDOWS = [(0.0, 1.8)]
WIN_W, WIN_H = 0.9, 0.8
BUDGET = (1200, 2500)


def wall_matrix(side):
    """牆的本地框架：外面在本地 y = 0、法線 −Y、沿本地 X 長；side 是 front／back／right（Blender +X）／left。"""
    if side == "front":
        return Matrix.Translation((0, -D, 0))
    if side == "back":
        return Matrix.Translation((0, D, 0)) @ Matrix.Rotation(math.pi, 4, "Z")
    if side == "right":
        return Matrix.Translation((W, 0, 0)) @ Matrix.Rotation(math.pi / 2, 4, "Z")
    return Matrix.Translation((-W, 0, 0)) @ Matrix.Rotation(-math.pi / 2, 4, "Z")


def placed(bm, matrix, part):
    verts, faces = part
    bmesh_transform(bm, matrix, verts)
    return verts, faces


def bmesh_transform(bm, matrix, verts):
    import bmesh
    bmesh.ops.transform(bm, matrix=matrix, verts=verts)


def wall(bm, rng, side, length, holes):
    """一面橫板牆（含山牆三角的橫板）：holes = [(u0, u1, z0, z1)] 是門窗洞。"""
    m = wall_matrix(side)
    half = length / 2 - 0.2  # 兩端藏進角柱
    row_h = (WALL_TOP - FOOT) / ROWS
    gable = side in ("front", "back")
    tan = math.tan(math.radians(PITCH))
    z = FOOT
    while True:
        zc = z + row_h / 2
        if zc < WALL_TOP:
            lo, hi = -half, half
        elif gable:
            reach = W - 0.3 - (z + row_h - WALL_TOP) / tan  # 這排板的上緣要藏在屋頂底下（含封簷板的厚度）
            if reach < 0.25:
                break
            lo, hi = -reach, reach
        else:
            break
        cut = [(a, b) for a, b, z0, z1 in holes if z0 < zc < z1]
        for s, e in hub.segments(lo, hi, cut, rng):
            part = kit.add_plank(bm, e - s, PLANK_T, row_h * 1.1, location=((s + e) / 2, PLANK_T / 2, zc),
                                 rotation=(-LAP, 0, 0), cell=hub.tone(rng, "plank"), rng=rng)
            placed(bm, m, part)
        z += row_h


def window(bm, rng, side, u, zc):
    m = wall_matrix(side)
    w, h, f = WIN_W, WIN_H, 0.1
    parts = [
        kit.add_box(bm, (w + 2 * f, 0.12, f), (u, -0.02, zc + h / 2 + f / 2), cell="plank_dark.m"),
        kit.add_box(bm, (w + 2 * f, 0.12, f), (u, -0.02, zc - h / 2 - f / 2), cell="plank_dark.m"),
        kit.add_box(bm, (f, 0.12, h), (u - w / 2 - f / 2, -0.02, zc), cell="plank_dark.m"),
        kit.add_box(bm, (f, 0.12, h), (u + w / 2 + f / 2, -0.02, zc), cell="plank_dark.m"),
        kit.add_box(bm, (0.05, 0.06, h), (u, 0.0, zc), cell="plank_dark.d"),
        kit.add_box(bm, (w, 0.06, 0.05), (u, 0.0, zc), cell="plank_dark.d"),
        kit.add_box(bm, (w, 0.04, h), (u, 0.05, zc), cell="lantern.m"),               # 發光玻璃
        kit.add_box(bm, (w + 0.34, 0.2, 0.06), (u, -0.08, zc - h / 2 - f - 0.03), cell="plank.l", rng=rng, jitter=0.02),
    ]
    for p in parts:
        placed(bm, m, p)


def door(bm, rng):
    m = wall_matrix("front")
    u0, u1, z0, z1 = DOOR
    boards = 5
    bw = (u1 - u0) / boards
    for i in range(boards):
        placed(bm, m, kit.add_plank(bm, bw * 1.02, 0.06, z1 - z0, location=(u0 + bw * (i + 0.5), 0.06, (z0 + z1) / 2),
                                    rotation=(0, 0, 0), cell=hub.tone(rng, "plank_dark"), rng=rng))
    for zc in (z0 + 0.35, z1 - 0.3):
        placed(bm, m, kit.add_box(bm, (u1 - u0 - 0.08, 0.05, 0.14), (0, 0.01, zc), cell="plank.d"))
    brace = math.degrees(math.atan2(z1 - z0 - 0.65, u1 - u0 - 0.1))
    placed(bm, m, kit.add_box(bm, (math.hypot(z1 - z0 - 0.65, u1 - u0 - 0.1), 0.05, 0.12), (0, 0.01, (z0 + z1) / 2 + 0.02),
                              rotation=(0, -brace, 0), cell="plank.d"))
    placed(bm, m, kit.add_box(bm, (0.06, 0.08, 0.16), (u1 - 0.14, -0.03, 1.2), cell="iron.m"))
    # 門框
    for u in (u0 - 0.06, u1 + 0.06):
        placed(bm, m, kit.add_box(bm, (0.12, 0.12, z1 - z0 + 0.12), (u, -0.02, (z0 + z1 + 0.12) / 2), cell="plank_dark.d"))
    placed(bm, m, kit.add_box(bm, (u1 - u0 + 0.4, 0.14, 0.14), (0, -0.03, z1 + 0.1), cell="plank_dark.d"))


def roof(bm, rng):
    tan = math.tan(math.radians(PITCH))
    ridge = WALL_TOP + W * tan
    p = math.radians(PITCH)
    span = (W + EAVE) / math.cos(p)
    depth = 2 * (D + GABLE_OVER)
    strips = 6
    for sgn in (1, -1):
        normal = Vector((math.sin(p) * sgn, 0, math.cos(p)))
        for i in range(strips):
            s = (i + 0.5) * span / strips
            c = Vector((sgn * s * math.cos(p), 0, ridge - s * math.sin(p))) + normal * (0.06 + 0.03 * (i % 2))
            kit.add_box(bm, (span / strips * 1.18, depth * rng.uniform(0.99, 1.01), 0.09), c,
                                       rotation=(0, sgn * (PITCH + 3), 0), cell=hub.tone(rng, "roof", (0.15, 0.65, 0.2)),
                                       rng=rng, jitter=0.01)
        # 封簷板（前後山牆的斜邊）
        for y in (-(D + GABLE_OVER), D + GABLE_OVER):
            c = Vector((sgn * span / 2 * math.cos(p), y, ridge - span / 2 * math.sin(p))) + normal * 0.02
            kit.add_box(bm, (span + 0.05, 0.08, 0.24), c, rotation=(0, sgn * PITCH, 0), cell="plank_dark.m")
    kit.add_box(bm, (0.26, depth + 0.12, 0.2), (0, 0, ridge + 0.14), cell="plank_dark.d")


def posts_and_footing(bm, rng):
    for x in (-W + 0.12, W - 0.12):
        for y in (-D + 0.12, D - 0.12):
            kit.add_box(bm, (0.3, 0.3, WALL_TOP + 0.1 - FOOT), (x, y, FOOT), cell="plank_dark.m", base=True, rng=rng, jitter=0.02)
    # 石塊地基：沿四邊一塊塊排，略突出牆外
    for side, length in (("front", 2 * W), ("back", 2 * W), ("right", 2 * D), ("left", 2 * D)):
        m = wall_matrix(side)
        n = max(2, round(length / 1.1))
        step = (length + 0.3) / n
        for i in range(n):
            u = -length / 2 - 0.15 + step * (i + 0.5)
            cell = "stone." + rng.choice("lmmd")
            placed(bm, m, kit.add_box(bm, (step * 0.96, 0.4, FOOT + 0.04), (u, 0.1, 0), cell=cell, base=True, rng=rng, jitter=0.06))


def porch(bm, rng):
    """門前木平台（4 片前後向的木板）與一階台階。"""
    deep, wide = 0.7, 2.2
    for i in range(4):
        x = -wide / 2 + wide / 4 * (i + 0.5)
        kit.add_plank(bm, wide / 4 * 0.96, deep, 0.07, location=(x, -D - deep / 2, FOOT - 0.035),
                      cell=hub.tone(rng, "plank"), rng=rng)
    kit.add_box(bm, (wide - 0.1, deep - 0.05, FOOT - 0.07), (0, -D - deep / 2, 0), cell="plank_dark.d", base=True)
    kit.add_box(bm, (1.5, 0.3, FOOT * 0.5), (0, -D - deep - 0.15, 0), cell="plank.m", base=True, rng=rng, jitter=0.02)


def lantern(bm):
    """門左邊（Unity 本地 +X，模型的右手邊 = Blender −X）牆上的掛燈。"""
    x, z = -1.0, 2.15
    y = -D
    kit.add_box(bm, (0.05, 0.4, 0.05), (x, y - 0.2, z + 0.32), cell="iron.d")
    kit.add_box(bm, (0.05, 0.05, 0.12), (x, y - 0.38, z + 0.24), cell="iron.d")
    kit.add_box(bm, (0.17, 0.17, 0.22), (x, y - 0.38, z + 0.06), cell="lantern.m")
    kit.add_prism(bm, 4, 0.15, 0.1, location=(x, y - 0.38, z + 0.17), rotation=(0, 0, 45), radius_top=0.0, cell="iron.m")
    kit.add_box(bm, (0.2, 0.2, 0.03), (x, y - 0.38, z - 0.06), cell="iron.d")


def chimney(bm, rng):
    """屋頂後方偏右（模型的左手邊）的石砌煙囪：只露出屋頂以上的部分。"""
    x, y = 1.3, 1.0
    tan = math.tan(math.radians(PITCH))
    z = WALL_TOP + (W - x) * tan - 0.3
    top = WALL_TOP + W * tan + 0.8
    while z < top:
        h = rng.uniform(0.3, 0.38)
        kit.add_box(bm, (0.72, 0.72, h), (x, y, z), rotation=(0, 0, rng.uniform(-6, 6)),
                    cell="stone." + rng.choice("lmd"), base=True, rng=rng, jitter=0.05)
        z += h * 0.95
    kit.add_box(bm, (0.86, 0.86, 0.12), (x, y, z), cell="stone_dark.m", base=True)


def build():
    rng = hub.rng_for(SEED)
    col = kit.begin(NAME)
    bm = kit.new_bmesh()
    posts_and_footing(bm, rng)
    front_holes = [DOOR] + [(u - WIN_W / 2 - 0.1, u + WIN_W / 2 + 0.1, z - WIN_H / 2 - 0.1, z + WIN_H / 2 + 0.1) for u, z in FRONT_WINDOWS]
    side_holes = [(u - WIN_W / 2 - 0.1, u + WIN_W / 2 + 0.1, z - WIN_H / 2 - 0.1, z + WIN_H / 2 + 0.1) for u, z in SIDE_WINDOWS]
    wall(bm, rng, "front", 2 * W, front_holes)
    wall(bm, rng, "back", 2 * W, [])
    wall(bm, rng, "right", 2 * D, side_holes)
    wall(bm, rng, "left", 2 * D, side_holes)
    for u, z in FRONT_WINDOWS:
        window(bm, rng, "front", u, z)
    for side in ("right", "left"):
        for u, z in SIDE_WINDOWS:
            window(bm, rng, side, u, z)
    door(bm, rng)
    roof(bm, rng)
    porch(bm, rng)
    lantern(bm)
    chimney(bm, rng)
    obj = hub.make(NAME, bm, col)
    report = kit.check([obj], budget=BUDGET, pivots={NAME: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], NAME, target_bbox=((-3, -2.5, 0), (3, 2.5, 3)))
    report["fbx"] = kit.export_fbx([obj], f"Hub/{NAME}.fbx")
    report["blend"] = kit.save_blend(NAME)
    return report


result = build()

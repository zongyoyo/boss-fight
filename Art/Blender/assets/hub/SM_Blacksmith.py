"""
鐵匠鋪 SM_Blacksmith 與鐵砧 SM_Anvil（ART_STYLE「休息區（Phase 6）」）。

棚子：4 根柱（位置對齊 HubBuilder 的柱子碰撞：Unity 本地 x ±2.3、z 0.9／−3.5）、往後低的單坡木板棚頂、
背牆橫板；左後方是石塊砌的爐子（對齊爐子的碰撞盒 1.4×1.1×1.2），爐口與爐床用發光色格，
上方石砌爐罩一路穿出棚頂；爐邊皮革風箱、右後方水槽；背牆上一塊架板掛鐵鎚、鉗子、馬蹄鐵。
鐵砧另外一個模型（站點的互動點，Unity 本地原點）：樹墩底座加經典砧形（底座、腰、砧面、往 +X 的砧角、砧尾）。
座標照 Unity 站點本地想（hub.U），軸心都在站點原點的地面。
"""
import importlib
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (os.path.join(_HERE, "..", "..", "lib"), _HERE):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import lowpoly_kit as kit  # noqa: E402
import hub_common as hub  # noqa: E402

importlib.reload(kit)
importlib.reload(hub)

U = hub.U
SEED = 611
POST_X = 2.3
FRONT_Z, BACK_Z = 0.9, -3.5
FRONT_H, BACK_H = 3.35, 2.75          # 柱頂高（棚頂往後低）
FORGE = (-1.4, -2.6)                   # 爐子中心 (x, z)，碰撞 1.4×1.1×1.2
SHED_BUDGET = (800, 1500)
ANVIL_BUDGET = (100, 260)


def roof_y(z):
    """棚頂下緣在 z 處的高度（前高後低）。"""
    t = (z - BACK_Z) / (FRONT_Z - BACK_Z)
    return BACK_H + (FRONT_H - BACK_H) * t


def shed(bm, rng):
    slope = math.degrees(math.atan2(FRONT_H - BACK_H, FRONT_Z - BACK_Z))
    for x in (-POST_X, POST_X):
        for z, h in ((FRONT_Z, FRONT_H), (BACK_Z, BACK_H)):
            kit.add_prism(bm, 6, 0.14, h, location=U(x, 0, z), cell=hub.tone(rng, "bark", (0.2, 0.6, 0.2)), rng=rng, jitter=0.05)
    # 柱頂的橫樑（左右向）與側樑（前後向、跟著坡度）
    for z, h in ((FRONT_Z, FRONT_H), (BACK_Z, BACK_H)):
        hub.box(bm, (2 * POST_X + 0.6, 0.22, 0.2), (0, h - 0.05, z), cell="plank_dark.d")
    length = math.hypot(FRONT_Z - BACK_Z, FRONT_H - BACK_H) + 0.5
    for x in (-POST_X, POST_X):
        hub.box(bm, (0.18, 0.2, length), (x, (FRONT_H + BACK_H) / 2 + 0.12, (FRONT_Z + BACK_Z) / 2),
                    rotation=(-slope, 0, 0), cell="plank_dark.m")
    # 棚頂：前後向的木板一片片並排，左右各伸出 0.4、前伸 0.6、後伸 0.5
    boards = 11
    width = 2 * POST_X + 0.8
    z0, z1 = BACK_Z - 0.5, FRONT_Z + 0.6
    run = math.hypot(z1 - z0, roof_y(z1) - roof_y(z0))
    for i in range(boards):
        x = -width / 2 + width / boards * (i + 0.5)
        zc = (z0 + z1) / 2
        kit.add_box(bm, (width / boards * 1.04, run * rng.uniform(0.97, 1.02), 0.07),
                      location=U(x, roof_y(zc) + 0.28 + 0.02 * (i % 2), zc),
                      rotation=(-slope, 0, 0), cell=hub.tone(rng, "plank_dark", (0.3, 0.45, 0.25)), rng=rng)
    # 背牆：柱子後面的橫板，上緣到棚頂
    rows = 7
    row_h = (BACK_H + 0.1) / rows
    for r in range(rows):
        y = row_h * (r + 0.5)
        for s, e in hub.segments(-POST_X - 0.1, POST_X + 0.1, [], rng, split=2.4):
            kit.add_plank(bm, e - s, 0.07, row_h * 1.1, location=U((s + e) / 2, y, BACK_Z - 0.18),
                          rotation=(5, 0, 0), cell=hub.tone(rng, "plank"), rng=rng)


def forge(bm, rng):
    fx, fz = FORGE
    # 兩層石塊，四周略突出碰撞盒
    for layer, (y0, h) in enumerate(((0.0, 0.55), (0.55, 0.5))):
        for dx in (-0.38, 0.38):
            for dz in (-0.33, 0.33):
                hub.box(bm, (0.74, h, 0.64), (fx + dx + (0.04 if layer else 0), y0, fz + dz),
                            rotation=(0, 0, rng.uniform(-3, 3)), cell="stone." + rng.choice("lmmd"), base=True, rng=rng, jitter=0.05)
    # 爐床（頂面）與爐口（正面的開口）
    hub.box(bm, (1.5, 0.12, 1.3), (fx, 1.05, fz), cell="stone_dark.m", base=True)
    hub.box(bm, (1.0, 0.05, 0.8), (fx, 1.16, fz), cell="ember.m", base=True)
    hub.box(bm, (0.6, 0.4, 0.06), (fx, 0.35, fz + 0.66), cell="dark.m", base=True)
    hub.box(bm, (0.5, 0.28, 0.05), (fx, 0.38, fz + 0.68), cell="fire.m", base=True)
    hub.box(bm, (0.8, 0.1, 0.12), (fx, 0.75, fz + 0.66), cell="stone_dark.m", base=True)
    # 爐罩：四角錐台，接一支穿出棚頂的石煙囪
    kit.add_prism(bm, 4, 0.95, 0.75, location=U(fx, 1.65, fz - 0.15), rotation=(0, 0, 45), radius_top=0.42,
                  cell="stone.d", rng=rng, jitter=0.04)
    y = 2.4
    top = roof_y(fz) + 1.2
    while y < top:
        h = rng.uniform(0.32, 0.4)
        hub.box(bm, (0.6, h, 0.6), (fx, y, fz - 0.15), rotation=(0, 0, rng.uniform(-6, 6)),
                    cell="stone." + rng.choice("lmd"), base=True, rng=rng, jitter=0.05)
        y += h * 0.95
    hub.box(bm, (0.74, 0.1, 0.74), (fx, y, fz - 0.15), cell="stone_dark.m", base=True)


def workshop(bm, rng):
    fx, fz = FORGE
    # 風箱：爐子右邊，兩片木板夾一塊皮
    bx, bz = fx + 1.1, fz + 0.1
    hub.box(bm, (0.5, 0.06, 0.7), (bx, 0.62, bz), rotation=(0, 0, 0), cell="plank_dark.m")
    hub.box(bm, (0.5, 0.06, 0.7), (bx, 0.92, bz), rotation=(8, 0, 0), cell="plank_dark.m")
    hub.box(bm, (0.44, 0.24, 0.6), (bx, 0.77, bz), cell="leather.m")
    hub.box(bm, (0.06, 0.06, 0.5), (bx, 0.95, bz + 0.5), cell="plank.d")
    hub.box(bm, (0.12, 0.62, 0.12), (bx, 0.0, bz), cell="plank_dark.d", base=True)
    # 水槽：右後方
    tx, tz = 1.3, -2.8
    hub.box(bm, (1.1, 0.5, 0.6), (tx, 0, tz), cell="plank.d", base=True, rng=rng, jitter=0.02)
    hub.box(bm, (0.96, 0.04, 0.46), (tx, 0.44, tz), cell="water.d", base=True)
    # 背牆的架板與掛著的工具
    wall_z = BACK_Z - 0.1
    hub.box(bm, (1.8, 0.06, 0.28), (0.6, 1.95, wall_z + 0.14), cell="plank.l")
    for x in (0.0, 1.2):
        hub.box(bm, (0.05, 0.22, 0.24), (x, 1.82, wall_z + 0.12), cell="plank_dark.d")
    for i, x in enumerate((0.15, 0.45, 0.9)):
        hub.box(bm, (0.24, 0.08, 0.1), (x, 2.02, wall_z + 0.14), cell="iron." + "lmd"[i])  # 鐵錠
    # 鐵鎚：柄朝下掛
    hub.box(bm, (0.05, 0.5, 0.05), (-0.3, 1.35, wall_z + 0.04), cell="plank.m")
    hub.box(bm, (0.24, 0.1, 0.1), (-0.3, 1.62, wall_z + 0.05), cell="iron.m")
    # 鉗子：兩根交叉的細鐵條
    for a in (-8, 8):
        hub.box(bm, (0.035, 0.6, 0.03), (0.35, 1.25, wall_z + 0.04), rotation=(0, a, 0), cell="iron.d")
    # 馬蹄鐵：半個 6 邊環
    hx, hy = 1.0, 1.3
    for k in range(4):
        a = math.radians(-90 + k * 60)
        b = math.radians(-90 + (k + 1) * 60)
        mid = (a + b) / 2
        hub.box(bm, (0.13, 0.05, 0.035), (hx + math.cos(mid) * 0.1, hy + math.sin(mid) * 0.1, wall_z + 0.04),
                    rotation=(0, math.degrees(mid) - 90, 0), cell="iron.m")
    # 釘子
    for x in (-0.3, 0.35, 1.0):
        hub.box(bm, (0.03, 0.03, 0.08), (x, 1.66 if x < 0 else 1.55, wall_z + 0.04), cell="iron.d")


def anvil(bm, rng):
    # 樹墩底座
    stump, faces = kit.add_prism(bm, 8, 0.32, 0.42, location=U(0, 0, 0), radius_top=0.28, cell="bark.m", rng=rng, jitter=0.05)
    kit.shade_faces(bm, faces, "bark", up=0.5, down=-0.3)
    kit.add_prism(bm, 8, 0.27, 0.02, location=U(0, 0.42, 0), cell="wood_cut.l")
    # 砧形：底座、腰、砧面、砧角（往 Unity +X）、砧尾
    hub.box(bm, (0.5, 0.1, 0.38), (0, 0.44, 0), cell="iron.d", base=True)
    hub.box(bm, (0.3, 0.18, 0.22), (0, 0.54, 0), cell="iron.d", base=True)
    hub.box(bm, (0.62, 0.14, 0.3), (-0.04, 0.72, 0), cell="iron.m", base=True)
    hub.box(bm, (0.58, 0.015, 0.26), (-0.04, 0.86, 0), cell="steel.m", base=True)
    kit.add_prism(bm, 4, 0.11, 0.38, location=U(0.27, 0.79, 0), rotation=(0, -90, 0), radius_top=0.0, cell="iron.m")
    hub.box(bm, (0.14, 0.1, 0.2), (-0.39, 0.74, 0), cell="iron.m", base=True)
    # 擱在砧面上的鐵鎚
    hub.box(bm, (0.05, 0.05, 0.42), (-0.12, 0.89, 0.12), rotation=(0, 0, 70), cell="plank.m")
    hub.box(bm, (0.2, 0.1, 0.1), (0.06, 0.88, 0.07), rotation=(0, 0, 70), cell="iron.m", base=True)


def build_shed():
    name = "SM_Blacksmith"
    rng = hub.rng_for(SEED)
    col = kit.begin(name)
    bm = kit.new_bmesh()
    shed(bm, rng)
    forge(bm, rng)
    workshop(bm, rng)
    obj = hub.make(name, bm, col)
    report = kit.check([obj], budget=SHED_BUDGET, pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name)
    report["fbx"] = kit.export_fbx([obj], f"Hub/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


def build_anvil():
    name = "SM_Anvil"
    rng = hub.rng_for(SEED + 1)
    col = kit.begin(name)
    bm = kit.new_bmesh()
    anvil(bm, rng)
    obj = hub.make(name, bm, col)
    report = kit.check([obj], budget=ANVIL_BUDGET, pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Hub/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {"shed": build_shed(), "anvil": build_anvil()}

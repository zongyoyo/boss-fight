"""
競技場撞牆圈的模組 SM_Rim_<種類>_<變體>（ART_STYLE「競技場：風雨交加的黑暗森林」的撞牆圈 Arena_RimWall）。

場地外緣（半徑 12 m）的外觀：拱出地面的巨根、疊起來的倒木、長苔的岩塊，Builder 沿半徑排成一圈、彼此稍微重疊。
碰撞仍是 Builder 的隱形外牆（ArenaWall 層），模組本身沒有碰撞，所以：
- 模組的「內面」對著場地中央（Blender −Y，Unity +Z），內面不超過 y = 0（玩家可以走到牆邊，模型不能伸進場地）。
- 本體往後（+Y）長，深約 2–3 m；寬約 4–5 m（沿圓周）；高約 3–4 m，跟原本的外牆（4 m）差不多。
- 軸心在內面底邊的中點（Builder 直接放在半徑 R 上、+Z 朝中心）。
暗林色：樹皮 bark_dark、土 mud、石頭 stone 偏暗階，朝上的面長苔（moss）。
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

VARIANTS = {
    "Root_A": dict(seed=401, kind="root", width=4.6, height=3.6, arches=3),
    "Root_B": dict(seed=402, kind="root", width=4.2, height=3.2, arches=2),
    "Log_A": dict(seed=411, kind="log", width=5.0, height=3.0, logs=2),
    "Log_B": dict(seed=412, kind="log", width=4.6, height=2.4, logs=1),
    "Rock_A": dict(seed=421, kind="rock", width=4.4, height=3.6, rocks=3),
    "Rock_B": dict(seed=422, kind="rock", width=4.0, height=2.8, rocks=4),
}
BUDGET = (150, 700)
MOSS_UP = 0.6      # 面朝上到這個程度就長苔
MOSS_CHANCE = 0.75


def paint(bm, faces, cell):
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        f[layer] = kit.cell_index(cell)


def mossy(bm, faces, group, rng, up=MOSS_UP, chance=MOSS_CHANCE):
    """先依朝向分亮中暗，再把朝上的面換成苔蘚（亮或中階）。"""
    kit.shade_faces(bm, faces, group, up=0.55, down=-0.3)
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        if f.normal.z >= up and rng.random() < chance:
            f[layer] = kit.cell_index("moss." + ("m" if rng.random() < 0.7 else "d"))


def dark_stone(bm, faces, rng):
    """暗林的岩塊：朝上中階、側面暗階、朝下 stone_dark，再長苔。"""
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        f.normal_update()
        z = f.normal.z
        f[layer] = kit.cell_index("stone.m" if z >= 0.55 else "stone_dark.m" if z < -0.3 else "stone.d")
        if z >= MOSS_UP and rng.random() < MOSS_CHANCE:
            f[layer] = kit.cell_index("moss." + ("m" if rng.random() < 0.6 else "d"))


def mound(bm, rng, center, radius, height, group="mud"):
    """土堆：壓扁的 icosphere，底部壓平埋進地裡；巨根、倒木、岩塊都坐在土堆上，模組之間的縫也靠它填。"""
    verts, faces = kit.add_rock(bm, radius, rng, location=center, squash=height / radius, cuts=1,
                                cell=f"{group}.m", jitter=0.16)
    for v in verts:
        v.co.z -= 0.15
    mossy(bm, faces, group, rng, up=0.75, chance=0.35)
    return verts


def arch_root(bm, rng, x0, x1, depth, height, radius):
    """拱出地面的巨根：兩端插進土裡（z < 0），中間拱到 height；拱頂往後仰（y 加大），不會像一片直立的牆。"""
    pts, radii = [], []
    n = 8
    lean = rng.uniform(0.5, 0.9)
    for i in range(n):
        t = i / (n - 1)
        x = x0 + (x1 - x0) * t
        arc = math.sin(t * math.pi)
        z = (arc ** 0.8) * height - 0.35 * (1 - arc)
        y = depth + lean * arc + rng.uniform(-0.15, 0.15)
        pts.append((x, y, z))
        radii.append(radius * (1.15 - 0.35 * t))
    verts, faces = kit.add_tube(bm, pts, radii, sides=7, cell="bark_dark.m", jitter=0.1, rng=rng)
    mossy(bm, faces, "bark_dark", rng)
    return verts


def crawl_root(bm, rng, start, end, radius):
    """貼地爬出來的小根：從土堆往前爬，末端收尖鑽進地裡。"""
    mid = start.lerp(end, 0.55) + Vector((rng.uniform(-0.3, 0.3), 0, radius * 1.2))
    _, faces = kit.add_tube(bm, [start, mid, end], [radius, radius * 0.7, 0.0], sides=5,
                            cell="bark_dark.m", jitter=0.1, rng=rng)
    mossy(bm, faces, "bark_dark", rng, chance=0.4)


def build_root(p, rng, bm):
    w, h = p["width"], p["height"]
    mound(bm, rng, (0, 1.6, 0), w * 0.42, 1.1)
    for i in range(p["arches"]):
        span = w * rng.uniform(0.8, 1.05)
        cx = rng.uniform(-w * 0.15, w * 0.15)
        depth = 0.6 + i * 0.55
        arch_root(bm, rng, cx - span / 2, cx + span / 2, depth, h * (0.72 + 0.14 * i) * rng.uniform(0.9, 1.0),
                  rng.uniform(0.36, 0.48))
    for _ in range(2):
        x = rng.uniform(-w * 0.35, w * 0.35)
        crawl_root(bm, rng, Vector((x, 1.0, 0.25)), Vector((x + rng.uniform(-0.8, 0.8), 0.25, -0.1)), 0.16)


def log(bm, rng, center, length, radius, yaw, pitch=0.0):
    """倒木：沿 X 的 8 邊樹幹（中段微拱），兩端是年輪截面，中段一根斷枝。"""
    yaw_r, pitch_r = math.radians(yaw), math.radians(pitch)
    axis = Vector((math.cos(yaw_r) * math.cos(pitch_r), math.sin(yaw_r) * math.cos(pitch_r), math.sin(pitch_r)))
    c = Vector(center)
    half = length / 2
    pts = [c - axis * half, c - axis * half * 0.35 + Vector((0, 0, 0.06)), c + axis * half * 0.35 + Vector((0, 0, 0.06)),
           c + axis * half]
    verts, faces = kit.add_tube(bm, pts, [radius * 1.05, radius, radius * 0.97, radius * 0.88], sides=8,
                                cell="bark_dark.m", jitter=0.07, rng=rng)
    mossy(bm, faces, "bark_dark", rng)
    # 兩端的截面：面法線跟軸幾乎平行的就是截面
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        f.normal_update()
        if abs(f.normal.dot(axis)) > 0.9:
            f[layer] = kit.cell_index("wood_cut.d")
    # 斷枝：往上斜
    s = c + axis * rng.uniform(-half * 0.4, half * 0.4) + Vector((0, 0, radius * 0.8))
    tip = s + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.1, 0.4), rng.uniform(0.6, 1.1)))
    _, f2 = kit.add_tube(bm, [s, tip], [radius * 0.32, 0.0], sides=5, cell="bark_dark.m", jitter=0.08, rng=rng)
    kit.shade_faces(bm, f2, "bark_dark", up=0.55, down=-0.3)


def build_log(p, rng, bm):
    w, h = p["width"], p["height"]
    mound(bm, rng, (0, 1.5, 0), w * 0.4, 0.8)
    r0 = 0.75 if p["logs"] > 1 else 0.85
    log(bm, rng, (0, 0.95, r0 * 0.9), w * 1.02, r0, rng.uniform(-4, 4))
    if p["logs"] > 1:
        # 第二根斜靠在第一根後上方，一端著地
        log(bm, rng, (rng.uniform(-0.5, 0.5), 1.75, r0 * 2.1), w * 0.85, r0 * 0.75, rng.uniform(-10, 10), pitch=rng.uniform(8, 14))
    # 兩端墊石頭，把撞牆圈的高低差補起來
    for sx in (-1, 1):
        verts, faces = kit.add_rock(bm, rng.uniform(0.5, 0.75), rng, location=(sx * w * 0.42, 1.3, 0), squash=0.8,
                                    cuts=2, cell="stone.m", jitter=0.12, subdivisions=0)
        dark_stone(bm, faces, rng)
    # 後方一根斷掉的直立殘幹，把剪影拉高到 h
    s = Vector((rng.uniform(-w * 0.3, w * 0.3), 2.2, 0))
    pts = [s, s + Vector((0.1, 0, h * 0.55)), s + Vector((-0.05, 0.05, h + 0.4))]
    _, faces = kit.add_tube(bm, pts, [0.45, 0.38, 0.22], sides=7, cell="bark_dark.m", jitter=0.12, rng=rng)
    mossy(bm, faces, "bark_dark", rng, chance=0.5)


def build_rock(p, rng, bm):
    w, h = p["width"], p["height"]
    mound(bm, rng, (0, 1.5, 0), w * 0.4, 0.7)
    n = p["rocks"]
    # 最大的一塊在後中，其他的往兩側、往前排，越前越矮
    spots = [(0.0, 1.6, h * 0.5)]
    for i in range(1, n):
        side = -1 if i % 2 else 1
        spots.append((side * w * rng.uniform(0.22, 0.36) * (1 + (i > 2) * 0.3), 1.0 + rng.uniform(-0.2, 0.3),
                      h * rng.uniform(0.25, 0.4)))
    for x, y, r in spots:
        verts, faces = kit.add_rock(bm, r, rng, location=(x, y, -0.1), squash=rng.uniform(1.0, 1.3), cuts=3,
                                    cell="stone.m", jitter=0.14, subdivisions=1)
        dark_stone(bm, faces, rng)
    # 一條巨根從岩縫爬出來
    crawl_root(bm, rng, Vector((w * 0.15, 1.6, h * 0.55)), Vector((w * 0.3, 0.3, -0.1)), 0.22)


BUILDERS = {"root": build_root, "log": build_log, "rock": build_rock}


def build(variant, p):
    name = f"SM_Rim_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    BUILDERS[p["kind"]](p, rng, bm)
    # 內面不伸進場地：y < 0 的頂點壓回 y = 0（只有隨機擾動會稍微越界）
    for v in bm.verts:
        if v.co.y < 0:
            v.co.y = 0.0
    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=BUDGET, pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

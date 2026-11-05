"""
扭曲巨樹 SM_Tree_Gnarled_<變體>（ART_STYLE「競技場：風雨交加的黑暗森林」，Phase 7 的新套件）。

暗林近林（離場地中心約 14–30 m）的主角：粗矮、往一邊歪又扭回來的 8 邊樹幹，底部一圈外露的板根插進土裡，
上段分出幾根先往上再往外下垂的粗枝（末端收尖、帶一根小分叉），枝頭只掛稀疏的暗綠葉團。
比序章的樹更歪、更黑：樹皮用 bark_dark，葉團用 pine 的暗階為主。軸心在樹幹底部中心（板根末端略低於 0，插進地裡）。
變體寫在 VARIANTS：A 往一邊歪的標準款、B 矮胖雙主幹、C 高瘦幾乎掉光葉子。
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
    # trunk_h：主幹高；radius：主幹基部半徑（板根上方）；lean：主幹頂偏離中心的距離與方向（度，0 = +X）
    # wobble：主幹左右扭動的幅度；roots：板根數；branches：(離主幹頂的高度比例, 方向角, 長度)；leaders：主幹分叉數
    # clumps：每根枝頭掛葉團的機率
    "A": dict(seed=301, trunk_h=6.2, radius=0.85, lean=(1.3, 30), wobble=0.35, roots=5, leaders=1,
              branches=[(0.72, 200, 3.4), (0.86, 70, 3.0), (1.0, 320, 2.6), (0.92, 140, 2.2)], clumps=0.45),
    "B": dict(seed=302, trunk_h=4.4, radius=1.0, lean=(0.6, 250), wobble=0.3, roots=6, leaders=2,
              branches=[(0.85, 20, 2.8), (1.0, 160, 3.1), (0.9, 280, 2.4)], clumps=0.55),
    "C": dict(seed=303, trunk_h=8.0, radius=0.68, lean=(1.8, 110), wobble=0.45, roots=4, leaders=1,
              branches=[(0.5, 230, 2.2), (0.62, 300, 2.8), (0.72, 80, 2.6), (0.8, 170, 3.2), (0.95, 40, 2.4)], clumps=0.2),
}
BARK = "bark_dark"
LEAF = "pine"
LEAF_UP = 0.72   # 葉團的面朝上到這個程度才用亮階（亮階少，暗林才不會一片亮綠）
MOSS = "moss"    # 從枝條垂下的苔蘚
ROOT_FLAT = 0.5  # 板根左右壓扁的比例（高而薄的板狀根）


def dir2(deg):
    a = math.radians(deg)
    return Vector((math.cos(a), math.sin(a), 0.0))


def trunk_path(p, rng, base_angle=None, scale=1.0):
    """主幹折線：往 lean 方向歪，中段反向扭一下（S 形），頂端再往回勾。回傳 (points, radii)。"""
    h = p["trunk_h"] * scale
    lean_d, lean_a = p["lean"]
    lean_a = lean_a if base_angle is None else base_angle
    lean_v = dir2(lean_a) * lean_d * scale
    side = dir2(lean_a + 90) * p["wobble"]
    r = p["radius"] * (scale ** 0.5)
    pts, radii = [], []
    n = 7
    for i in range(n):
        t = i / (n - 1)
        bend = lean_v * (t ** 1.6) + side * math.sin(t * math.pi * 1.3) * (1 if rng.random() > 0.3 else -1)
        jitter = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.06
        pts.append(Vector((0, 0, h * t)) + bend + jitter)
        # 底部外擴（板根接上去的地方），往上收細
        radii.append(r * (1.45 if i == 0 else 1.0 - 0.5 * t))
    return pts, radii


def add_bark_tube(bm, pts, radii, sides, rng, jitter=0.1):
    _, faces = kit.add_tube(bm, pts, radii, sides=sides, cell=f"{BARK}.m", jitter=jitter, rng=rng)
    kit.shade_faces(bm, faces, BARK, up=0.55, down=-0.3)
    return faces


def add_roots(bm, p, rng):
    """板根：從主幹基部往外、往下拱進土裡，再貼著地面伸出去、末端收尖插到 z < 0；左右壓扁成高而薄的板狀。"""
    r = p["radius"]
    n = p["roots"]
    for i in range(n):
        a = 360 * i / n + rng.uniform(-18, 18)
        d = dir2(a)
        reach = r * rng.uniform(2.4, 3.2)
        pts = [d * r * 0.3 + Vector((0, 0, r * rng.uniform(1.8, 2.4))),
               d * r * 1.05 + Vector((0, 0, r * 1.05)),
               d * r * 1.8 + Vector((0, 0, r * 0.38)),
               d * reach + Vector((0, 0, 0.06)),
               d * reach * 1.3 + Vector((0, 0, -0.15))]
        radii = [r * 0.6, r * 0.48, r * 0.32, r * 0.14, 0.0]
        verts, faces = kit.add_tube(bm, pts, radii, sides=5, cell=f"{BARK}.m", jitter=0.12, rng=rng)
        side = dir2(a + 90)
        for v in verts:
            v.co -= side * v.co.dot(side) * (1 - ROOT_FLAT)
        kit.shade_faces(bm, faces, BARK, up=0.55, down=-0.3)


def add_branch(bm, start, angle, length, radius, rng, clump_chance):
    """粗枝：先往上斜出去，再往外彎、末端往下勾並收尖；中段長一根小分叉，枝上垂幾條苔蘚，枝頭依機率掛一團葉。"""
    d = dir2(angle)
    up = Vector((0, 0, 1))
    pts = [start,
           start + d * length * 0.3 + up * length * 0.32,
           start + d * length * 0.62 + up * length * 0.36,
           start + d * length * 0.9 + up * length * 0.08,
           start + d * length * 1.0 - up * length * 0.38]
    add_bark_tube(bm, pts, [radius, radius * 0.75, radius * 0.5, radius * 0.28, 0.0], 5, rng)
    # 小分叉：從第二點往側面上方，再往下垂
    fork_a = angle + rng.choice((-1, 1)) * rng.uniform(35, 60)
    f0 = pts[1]
    f1 = f0 + dir2(fork_a) * length * 0.35 + up * length * 0.25
    f2 = f1 + dir2(fork_a) * length * 0.25 - up * length * 0.2
    add_bark_tube(bm, [f0, f1, f2], [radius * 0.45, radius * 0.25, 0.0], 4, rng)
    for q in (pts[2], pts[3], f1):
        if rng.random() < 0.8:
            add_moss(bm, q, rng)
    for tip in (pts[2] + up * 0.25, f1 + up * 0.2):
        if rng.random() < clump_chance:
            add_clump(bm, tip, rng.uniform(0.75, 1.1), rng)


def add_moss(bm, top, rng):
    """垂下的苔蘚：一條 3 邊、往下收尖的細條，風雨裡的暗林輪廓。"""
    length = rng.uniform(0.7, 1.5)
    sway = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.12
    pts = [top, top + sway - Vector((0, 0, length * 0.5)), top + sway * 1.6 - Vector((0, 0, length))]
    _, faces = kit.add_tube(bm, pts, [0.09, 0.06, 0.0], sides=3, cell=f"{MOSS}.d", jitter=0.1, rng=rng)
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    tier = "d" if rng.random() < 0.7 else "m"
    for f in faces:
        f[layer] = kit.cell_index(f"{MOSS}.{tier}")


def add_clump(bm, center, radius, rng):
    """稀疏葉團：20 面體（不細分），壓扁，上亮下暗。"""
    _, faces = kit.add_icosphere(bm, radius, 0, location=center, scale=(1.25, 1.1, 0.6),
                                 rotation=(rng.uniform(-10, 10), rng.uniform(-10, 10), rng.uniform(0, 360)),
                                 cell=f"{LEAF}.m", jitter=0.18, rng=rng)
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        f.normal_update()
        z = f.normal.z
        # 暗林的葉子：大多中、暗階，只有最朝上的面亮
        tier = "l" if z >= LEAF_UP else ("m" if z > -0.1 and rng.random() < 0.6 else "d")
        f[layer] = kit.cell_index(f"{LEAF}.{tier}")


def build(variant, p):
    name = f"SM_Tree_Gnarled_{variant}"
    rng = random.Random(p["seed"])
    col = kit.begin(name)
    bm = kit.new_bmesh()
    tops = []
    lean_a = p["lean"][1]
    for k in range(p["leaders"]):
        # 雙主幹：第二根從低一點的地方分出去，往反方向歪
        if k == 0:
            pts, radii = trunk_path(p, rng)
        else:
            sub = dict(p, lean=(p["lean"][0] * 1.4, 0))
            pts, radii = trunk_path(sub, rng, base_angle=lean_a + 150, scale=0.78)
            fork_at = trunk_path(p, random.Random(p["seed"]))[0][2]
            pts = [fork_at + (q - pts[0]) for q in pts[1:]]
            radii = [r * 0.75 for r in radii[1:]]
        add_bark_tube(bm, pts, radii, 9 if k == 0 else 7, rng, jitter=0.1)
        tops.append((pts, radii))
    add_roots(bm, p, rng)
    main_pts, main_r = tops[0]
    h = p["trunk_h"]
    for i, (frac, angle, length) in enumerate(p["branches"]):
        pts, radii = tops[i % len(tops)]
        # 在主幹上依高度比例找起點（線性插值）
        z = h * frac * (0.8 if i % len(tops) else 1.0)
        for a, b, ra, rb in zip(pts, pts[1:], radii, radii[1:]):
            if a.z <= z <= b.z or b is pts[-1]:
                t = 0.0 if b.z == a.z else max(0.0, min(1.0, (z - a.z) / (b.z - a.z)))
                start = a.lerp(b, t)
                r = ra + (rb - ra) * t
                break
        add_branch(bm, start, angle, length, max(r * 0.8, 0.12), rng, p["clumps"])
    # 主幹頂端：收尖的斷梢加一團葉
    tip = main_pts[-1]
    add_bark_tube(bm, [tip, tip + Vector((0.2, -0.1, 0.9))], [main_r[-1], 0.0], 6, rng)
    if rng.random() < p["clumps"] + 0.2:
        add_clump(bm, tip + Vector((0.1, 0, 0.9)), 1.1, rng)

    obj = kit.make_object(name, bm, col)
    kit.finalize(obj)
    report = kit.check([obj], budget=(600, 1200), pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name)
    report["fbx"] = kit.export_fbx([obj], f"Env/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {v: build(v, p) for v, p in VARIANTS.items()}

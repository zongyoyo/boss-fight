"""
休息區的小道具（ART_STYLE「休息區（Phase 6）」選做的那幾項，Nov 5 使用者決定一起做）：
任務告示板 SM_NoticeBoard（兩根柱、橫板、小雨遮、釘幾張紙；放在小屋旁，對應村長的任務）、
木桶 SM_Barrel、木箱 SM_Crate_A／B、柴堆 SM_Woodpile、柵欄一段 SM_Fence。
都沒有碰撞（HubSceneryBuilder 放在不擋路的地方）。座標照 Unity 本地想（hub.U／hub.box），軸心在地面中心，正面 Unity +Z。
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
SEED = 641


def notice_board(bm, rng):
    for x in (-0.85, 0.85):
        _, faces = kit.add_prism(bm, 6, 0.08, 2.3, location=U(x, 0, 0), radius_top=0.07, cell="bark.m", rng=rng, jitter=0.05)
        kit.shade_faces(bm, faces, "bark", up=0.5, down=-0.3)
    for i in range(4):
        hub.box(bm, (1.9, 0.25, 0.05), (rng.uniform(-0.02, 0.02), 1.0 + 0.26 * i, 0.08), cell=hub.tone(rng, "plank"), rng=rng, jitter=0.015)
    # 雨遮：前後兩片斜板，屋脊左右向
    for side in (1, -1):
        hub.box(bm, (2.1, 0.05, 0.36), (0, 2.28, side * 0.14), rotation=(side * 28, 0, 0), cell=hub.tone(rng, "roof", (0.2, 0.6, 0.2)))
    hub.box(bm, (2.15, 0.08, 0.08), (0, 2.38, 0), cell="plank_dark.d")
    # 紙：在板子正面，各自歪一點
    for x, y, w, h in ((-0.55, 1.5, 0.32, 0.42), (-0.1, 1.62, 0.28, 0.36), (0.35, 1.45, 0.34, 0.44), (0.62, 1.7, 0.22, 0.28)):
        hub.box(bm, (w, h, 0.012), (x, y, 0.112), rotation=(0, rng.uniform(-8, 8), 0), cell=rng.choice(("paper.m", "paper.l")))
        hub.box(bm, (0.03, 0.03, 0.03), (x, y + h / 2 - 0.04, 0.125), cell="iron.d")
    # 一張紅色的告示（任務）
    hub.box(bm, (0.26, 0.2, 0.012), (-0.12, 1.25, 0.112), rotation=(0, 4, 0), cell="cloth_red.m")


def barrel(bm, rng):
    pts = [(0, 0, z) for z in (0.0, 0.18, 0.45, 0.72, 0.9)]
    radii = [0.27, 0.31, 0.33, 0.31, 0.27]
    _, faces = kit.add_tube(bm, pts, radii, sides=9, cell="plank.m", jitter=0.0, rng=rng)
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    for f in faces:
        f.normal_update()
        f[layer] = kit.cell_index("wood_cut.d" if f.normal.z > 0.9 else hub.tone(rng, "plank"))
    for z, r in ((0.12, 0.305), (0.75, 0.305)):
        kit.add_prism(bm, 9, r + 0.015, 0.05, location=(0, 0, z), cell="iron.d")
    kit.add_prism(bm, 9, 0.24, 0.015, location=(0, 0, 0.9), cell="plank_dark.m")


def crate(bm, rng, size):
    s = size
    hub.box(bm, (s, s, s), (0, 0, 0), cell=hub.tone(rng, "plank"), base=True, rng=rng, jitter=0.01)
    t = 0.05
    # 每個側面一圈邊框（上下橫條），前後兩面加斜撐
    for face in range(4):
        yaw = face * 90
        m = kit.placement(rotation=(0, 0, yaw))
        for y in (0.06, s - 0.06):
            verts, _ = hub.box(bm, (s + 0.02, 0.1, t), (0, y - 0.05, s / 2 + t / 2 - 0.01), cell="plank_dark.m", base=True)
            _rotate(bm, verts, m)
        if face % 2 == 0:
            ang = math.degrees(math.atan2(s - 0.2, s - 0.1))
            verts, _ = hub.box(bm, (math.hypot(s - 0.2, s - 0.1), 0.09, t), (0, s / 2, s / 2 + t / 2 - 0.01),
                               rotation=(0, ang, 0), cell="plank_dark.m")
            _rotate(bm, verts, m)


def _rotate(bm, verts, matrix):
    import bmesh
    bmesh.ops.transform(bm, matrix=matrix, verts=verts)


def woodpile(bm, rng):
    """短圓木疊成 4-3-2-1 的三角堆，兩端年輪；兩邊各一根木樁擋著。"""
    r = 0.11
    length = 1.1
    rows = [4, 3, 2, 1]
    for level, n in enumerate(rows):
        y = r + level * r * 1.72
        for i in range(n):
            x = (i - (n - 1) / 2) * r * 2.02
            off = rng.uniform(-0.08, 0.08)
            a = Vector(U(x, y, -length / 2 + off))
            b = Vector(U(x, y, length / 2 + off))
            _, faces = kit.add_tube(bm, [a, b], [r * rng.uniform(0.9, 1.05)] * 2, sides=6, cell="bark.m", jitter=0.05, rng=rng)
            layer = bm.faces.layers.int.get(kit.CELL_ATTR)
            for f in faces:
                f.normal_update()
                f[layer] = kit.cell_index("wood_cut.l" if abs(f.normal.y) > 0.9 else "bark." + ("l" if f.normal.z > 0.5 else "m" if f.normal.z > -0.3 else "d"))
    for x in (-0.55, 0.55):
        _, faces = kit.add_prism(bm, 5, 0.05, 0.85, location=U(x, 0, 0), radius_top=0.04, cell="bark.d", rng=rng, jitter=0.05)


def fence(bm, rng):
    """一段 2.4 m 的柵欄：兩根尖頂木樁、兩條橫木。軸心在中間。"""
    for x in (-1.15, 1.15):
        kit.add_prism(bm, 5, 0.07, 0.95, location=U(x, 0, 0), cell="bark.m", rng=rng, jitter=0.05)
        kit.add_prism(bm, 5, 0.07, 0.12, location=U(x, 0.95, 0), radius_top=0.0, cell="bark.l")
    for y in (0.35, 0.75):
        a = Vector(U(-1.25, y + rng.uniform(-0.03, 0.03), 0.07))
        b = Vector(U(1.25, y + rng.uniform(-0.03, 0.03), 0.07))
        _, faces = kit.add_tube(bm, [a, b], [0.05, 0.045], sides=5, cell="bark.m", jitter=0.05, rng=rng)
        kit.shade_faces(bm, faces, "bark", up=0.5, down=-0.3)


MODELS = {
    "SM_NoticeBoard": (notice_board, (150, 400)),
    "SM_Barrel": (barrel, (60, 250)),
    "SM_Crate_A": (lambda bm, rng: crate(bm, rng, 0.7), (40, 200)),
    "SM_Crate_B": (lambda bm, rng: crate(bm, rng, 0.5), (40, 200)),
    "SM_Woodpile": (woodpile, (150, 400)),
    "SM_Fence": (fence, (40, 150)),
}


def build(name, fn, budget, seed):
    rng = hub.rng_for(seed)
    col = kit.begin(name)
    bm = kit.new_bmesh()
    fn(bm, rng)
    obj = hub.make(name, bm, col)
    report = kit.check([obj], budget=budget, pivots={name: (0, 0, 0)})
    report["sheet"] = kit.contact_sheet([obj], name, scale_ref=False)
    report["fbx"] = kit.export_fbx([obj], f"Hub/{name}.fbx")
    report["blend"] = kit.save_blend(name)
    return report


result = {name: build(name, fn, budget, SEED + i) for i, (name, (fn, budget)) in enumerate(MODELS.items())}

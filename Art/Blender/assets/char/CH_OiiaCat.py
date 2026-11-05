"""
OIIA 貓 CH_OiiaCat（ART_STYLE「OIIA 貓」、CHARACTER_INTERFACE 第 3 節）。

Phase 3 使用者決定：
  C4 幻影加發光眼睛；專屬貼圖（T_OiiaCat，不走共用色票）；耳朵有厚度；
  第四輪：坐姿效果不好，改成四腳站立，完全照計畫圖（Art/草圖/OIIA/OIIA.png）做，不管面數與戰鬥動畫設定
  （C1 坐姿、可伸縮手臂、爪子掛點都不再適用）。

做法：
  身體（頭、臉頰、口鼻、脖子、胸、軀幹、臀、四條腿與腳掌）是一團 metaball → 轉網格、切平腳底、減面、
  平面著色（計畫圖的三角切面）；
  眼睛：壓扁的眼球貼在臉上（平滑著色），上半部用一條斜切的上眼皮蓋掉（內側低、外側高 = 不爽，眼皮畫在眼球上，
  顏色接頭上的毛），下眼皮也蓋一點（杏仁眼），兩眼左右鏡像；眼球併進 Body；
  耳朵是凹的三角板加厚；尾巴從臀部垂到地面、尾尖往前勾；鬍鬚是細管（白色材質）；
  花紋、白毛、鼻子、嘴、虹膜都畫在同一張貼圖上（依 3D 位置算顏色）。

部件（物件名 = Unity 節點名，原點 = pivot）：
  Body（原點四腳中間的地面）、EarL／EarR（耳根）、Tail（尾根）、Whiskers（材質 OiiaCat_Whisker）、
  Collar（脖子上項圈的中心，材質 OiiaCat_Collar）、GhostEyes（只給幻影，材質 OiiaCat_GhostEye）。

座標：下面的數字都寫成 Unity 的 Model 空間 (x 右, y 上, z 前)，U() 換成 Blender（x = −x、y = −z、z = y）。
"""
import importlib
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)

NAME = "CH_OiiaCat"
SEED = 31
BUDGET = (2000, 20000)       # 使用者決定不管面數（只防止爆量）
BODY_TRIS = 2800             # 身體減面後的面數（計畫圖的切面大小）
TEX_SIZE = 2048
TEX_NAME = "T_OiiaCat"
UV_ANGLE = 40.0              # Smart UV Project 的角度上限：太大時腳邊的皺褶會投影到同一塊（UV 重疊）
MAT_BODY = "OiiaCat_Body"

# 比例照計畫圖的側面（1 px ≈ 1 cm）：耳尖約 2.75 m、背 2.0 m、肚子下緣 0.8 m、鼻尖到屁股約 3.4 m
HEAD_C = (0.0, 2.05, 1.2)      # 頭的中心
COLLAR_C = (0.0, 1.72, 0.88)    # 項圈的中心（脖子，pivot）
COLLAR_AXIS = (0.0, 0.62, 0.78)  # 脖子的方向（胸口往頭）
EYE_AT = (0.26, 2.12)           # 右眼在臉上的位置 (x, y)
EYE_R = 0.15
EYE_DEPTH = 0.3                # 眼睛前後壓扁的比例：扁的鏡片貼在臉上，不是一顆球
EYE_PROUD = 0.02               # 鏡片中心高出頭皮多少（太少的話眼睛邊緣會被頭吃掉）
LID_SLOPE = 0.55                 # 上眼皮斜切的斜率：外側高、內側低 = 不爽
LID_K = 0.06                    # 上眼皮切線離眼睛中心（眼球半徑的比例；越小蓋越多）
LOW_K = 0.72                    # 下眼皮：眼睛局部 y 低於 −LOW_K 的部分是毛（杏仁眼）

# 面的區域（zone，畫貼圖時依它選配色）
Z_FUR, Z_EYE, Z_EAR_FRONT, Z_TAIL, Z_EAR_BACK = range(5)

# 顏色（sRGB），對照色票 cat_* 與草稿
C_FUR = (0x8A, 0x7F, 0x72)
C_FUR_DARK = (0x6E, 0x64, 0x59)
C_STRIPE = (0x45, 0x3D, 0x36)
C_CREAM = (0xE9, 0xE1, 0xD3)
C_NOSE = (0xA8, 0x64, 0x5C)
C_EAR_IN = (0xD9, 0xA4, 0xA0)
C_PAD = (0xD9, 0x8E, 0x8A)
C_DARK = (0x2B, 0x22, 0x20)
C_IRIS = (0xA8, 0xB8, 0x5C)
C_IRIS_RIM = (0x5E, 0x72, 0x34)
C_SCLERA = (0xD8, 0xD2, 0xB4)
C_WHITE = (0xF5, 0xF2, 0xEA)


def U(x, y, z):
    """Unity Model 空間 → Blender。"""
    return Vector((-x, -z, y))


def to_unity(v):
    """Blender → Unity Model 空間（numpy 陣列 (..., 3) 也可以）。"""
    if isinstance(v, np.ndarray):
        return np.stack([-v[..., 0], v[..., 2], -v[..., 1]], axis=-1)
    return Vector((-v.x, v.z, -v.y))


# ---------------------------------------------------------------- 網格工具

def metaball_bmesh(elems, resolution, threshold=0.6):
    """metaball → bmesh（Blender 座標）。elems：(中心 Unity, 半徑 Unity (x, y, z), 硬度, 負的[, 傾斜 度])；
    半徑是想要的表面半徑（單一元素時），換算成 metaball 的 size。傾斜是繞前方軸轉，正 = 靠外側那端抬高
    （左右依中心的 x 自動鏡像）。"""
    mb = bpy.data.metaballs.new("_mb")
    mb.resolution = mb.render_resolution = resolution
    mb.threshold = threshold
    for c, r, stiff, neg, *tilt in elems:
        e = mb.elements.new(type="ELLIPSOID")
        e.co = U(*c)
        if tilt:
            # 繞 Blender +Y（= Unity 的後方）轉：Blender 局部 x 轉向 (cos θ, 0, −sin θ)
            side = 1 if c[0] >= 0 else -1
            e.rotation = Quaternion(Vector((0, 1, 0)), math.radians(tilt[0]) * side)
        e.radius = 1.0
        # 單一元素的表面在 size × q，q = sqrt(1 − (threshold / stiffness)^(1/3))
        q = math.sqrt(1 - (threshold / stiff) ** (1 / 3))
        e.size_x, e.size_y, e.size_z = r[0] / q, r[2] / q, r[1] / q  # Blender (x, y, z) = Unity (x, z, y)
        e.stiffness = stiff
        e.use_negative = neg
    ob = bpy.data.objects.new("_mb", mb)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.metaballs.remove(mb)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)
    return bm


def apply_modifier(bm, kind, **props):
    """對 bmesh 套一個修改器（減面、加厚），回傳新的 bmesh（舊的釋放）。"""
    mesh = bpy.data.meshes.new("_mod")
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new("_mod", mesh)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("m", kind)
    for k, v in props.items():
        setattr(mod, k, v)
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(mesh)
    bm = bmesh.new()
    bm.from_mesh(out)
    bpy.data.meshes.remove(out)
    return bm


def decimate(bm, tris):
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    ratio = min(1.0, tris / max(len(bm.faces), 1))
    return apply_modifier(bm, "DECIMATE", decimate_type="COLLAPSE", ratio=ratio)


def flatten_bottom(bm, height=0.0):
    """切掉 y < height 的部分並補平（坐在地上）。"""
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=U(0, height, 0), plane_no=U(0, 1, 0), clear_inner=True)
    cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    bmesh.ops.holes_fill(bm, edges=cut, sides=0)


def new_bmesh():
    """先建好 zone、mask 兩個圖層（之後才加圖層的話，已經拿到的面、頂點參照會失效）。"""
    bm = bmesh.new()
    bm.faces.layers.int.new("zone")
    bm.verts.layers.float.new("mask")
    return bm


def set_zone(bm, faces, zone):
    layer = bm.faces.layers.int.get("zone") or bm.faces.layers.int.new("zone")
    for f in faces:
        f[layer] = zone


def set_mask(bm, verts, fn):
    layer = bm.verts.layers.float.get("mask") or bm.verts.layers.float.new("mask")
    for v in verts:
        v[layer] = fn(v)


def tube(bm, path_u, radii, sides, cap=True):
    """沿路徑的圓管（每個點一圈），兩端各收成一點。回傳 (面, {頂點: 長度比例 t})。"""
    pts = [U(*p) for p in path_u]
    n = len(pts)
    lengths = [0.0]
    for a, b in zip(pts, pts[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    total = lengths[-1]
    rings = []
    prev_side = None
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        if prev_side is None:
            side = t.cross(Vector((0, 0, 1)))
            if side.length < 1e-6:
                side = t.orthogonal()
        else:
            side = prev_side - t * prev_side.dot(t)  # 沿路徑平行移動，管子才不會扭
        side.normalize()
        prev_side = side
        up = t.cross(side).normalized()
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * k / sides) +
                                        up * math.sin(2 * math.pi * k / sides)) * radii[i])
                      for k in range(sides)])
    faces = []
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(sides):
            faces.append(bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k])))
    params = {v: lengths[i] / total for i, ring in enumerate(rings) for v in ring}
    if cap:
        for ring, p, d, r, t in ((rings[0], pts[0], pts[0] - pts[1], radii[0], 0.0),
                                 (rings[-1], pts[-1], pts[-1] - pts[-2], radii[-1], 1.0)):
            tip = bm.verts.new(p + d.normalized() * r * 0.8)
            params[tip] = t
            for k in range(sides):
                faces.append(bm.faces.new((ring[(k + 1) % sides], ring[k], tip)))
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return faces, params


def catmull(points, times=2):
    """Catmull-Rom 每兩點插一個中點（端點延伸）。"""
    for _ in range(times):
        out = []
        n = len(points)
        for i in range(n - 1):
            p0, p1, p2, p3 = points[max(i - 1, 0)], points[i], points[i + 1], points[min(i + 2, n - 1)]
            out += [p1, tuple((-a + 9 * b + 9 * c - d) / 16 for a, b, c, d in zip(p0, p1, p2, p3))]
        out.append(points[-1])
        points = out
    return points


def surface_hit(tree, start_u, dir_u):
    d = U(*dir_u)
    hit, normal, _, _ = tree.ray_cast(U(*start_u), d.normalized())
    if hit is None:
        raise RuntimeError(f"射線沒打到身體：{start_u} → {dir_u}")
    return hit, normal


def merge(dst, src):
    """把 src 的幾何（連同 zone、mask）併進 dst，src 釋放。"""
    mesh = bpy.data.meshes.new("_merge")
    src.to_mesh(mesh)
    src.free()
    dst.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)


# ---------------------------------------------------------------- 部件

# 身體 metaball：(中心, 半徑, 硬度, 負的)；硬度 3 = 部位分明，2 = 融得比較開
def _pair(c, r, stiff):
    """左右一對（c 是右邊那個）。"""
    return [((c[0], c[1], c[2]), r, stiff, False), ((-c[0], c[1], c[2]), r, stiff, False)]


BODY_ELEMS = [
    # 頭（寬約 1.3，大頭）與臉頰毛（頭的下緣往外鼓）
    (HEAD_C, (0.62, 0.5, 0.48), 3.0, False),
    *_pair((0.42, 1.84, 1.32), (0.35, 0.3, 0.32), 3.0),
    # 眉骨：眼睛上方往前壓（不爽的眼神，眼睛不會像凸出來的球）
    *_pair((0.24, 2.25, 1.52), (0.2, 0.07, 0.12), 3.0),
    # 口鼻兩團、下巴、鼻樑（往前凸）
    *_pair((0.1, 1.86, 1.68), (0.14, 0.11, 0.12), 3.0),
    ((0, 1.72, 1.62), (0.11, 0.07, 0.09), 3.0, False),
    ((0, 1.99, 1.68), (0.09, 0.07, 0.09), 3.0, False),
    # 脖子、胸、軀幹、臀（融得開，背是一條順的線）
    ((0, 1.72, 0.88), (0.42, 0.42, 0.38), 2.0, False),
    ((0, 1.38, 0.62), (0.5, 0.58, 0.5), 2.0, False),
    ((0, 1.42, -0.15), (0.54, 0.6, 1.0), 2.0, False),
    ((0, 1.47, -0.85), (0.52, 0.58, 0.5), 2.0, False),
    # 肩胛、後腿的大腿（往兩側鼓）
    *_pair((0.32, 1.45, 0.62), (0.25, 0.5, 0.32), 3.0),
    *_pair((0.32, 1.4, -0.8), (0.27, 0.56, 0.48), 3.0),
    # 前腳：上臂、前臂、腳掌（腳掌往前長）
    *_pair((0.39, 0.95, 0.68), (0.19, 0.42, 0.22), 3.0),
    *_pair((0.4, 0.42, 0.74), (0.16, 0.38, 0.17), 3.0),
    *_pair((0.4, 0.1, 0.82), (0.18, 0.11, 0.23), 3.0),
    # 後腳：大腿下段、跗關節（往後斜）、腳掌
    *_pair((0.37, 0.85, -0.7), (0.21, 0.38, 0.29), 3.0),
    *_pair((0.37, 0.42, -0.92), (0.15, 0.35, 0.17), 3.0),
    *_pair((0.37, 0.1, -0.8), (0.17, 0.11, 0.23), 3.0),
]


_EYES = {}


def setup_eyes(tree):
    """從正前方往臉上打射線，眼睛朝那一點的法線、鏡片中心高出頭皮 EYE_PROUD（建身體後呼叫一次）。"""
    for side in (-1, 1):
        hit, normal = surface_hit(tree, (EYE_AT[0] * side, EYE_AT[1], 4.0), (0, 0, -1))
        fwd = to_unity(normal).normalized()
        out = Vector((0, 1, 0)).cross(fwd).normalized()
        if out.x * side < 0:
            out = -out
        up = fwd.cross(out).normalized()
        if up.y < 0:
            up = -up
        c = to_unity(hit) - fwd * (EYE_R * EYE_DEPTH - EYE_PROUD)
        _EYES[side] = (c, fwd, up, out)


def eye_frame(side):
    """眼睛的中心、前方、往上、往外（都是 Unity 向量）。"""
    return _EYES[side]


# 上眼皮的斜切面，用眼睛的局部座標（x 往外、y 往上、z 往前，眼球是單位球）：q·LID_N > LID_K 的是眼皮
LID_N = Vector((-LID_SLOPE, 1, 0)).normalized()


def eye_matrix(side, grow=0.0):
    """眼睛局部座標（單位球）→ Blender：x 往外、y 往上、z 往前，前後壓扁成 EYE_DEPTH。"""
    c, fwd, up, out = eye_frame(side)
    r = EYE_R + grow
    m = Matrix.Identity(4)
    for i, axis in enumerate((U(*out) * r, U(*up) * r, U(*fwd) * (r * EYE_DEPTH))):
        m.col[i][:3] = axis
    m.col[3][:3] = U(*c)
    return m


def eye_local(p, side):
    """Unity 座標 (N,3) → 眼睛局部座標（沒壓扁的單位球方向，已正規化）。"""
    c, fwd, up, out = eye_frame(side)
    d = p - np.array(c)
    q = np.stack([d @ np.array(out) / EYE_R, d @ np.array(up) / EYE_R, d @ np.array(fwd) / (EYE_R * EYE_DEPTH)], 1)
    return q / np.maximum(np.linalg.norm(q, axis=1), 1e-9)[:, None]


def place_eye(part, side, grow=0.0):
    bmesh.ops.transform(part, matrix=eye_matrix(side, grow), verts=part.verts)
    bmesh.ops.recalc_face_normals(part, faces=part.faces[:])  # 換軸有鏡像，繞序交給 recalc
    return part


def add_eyes(bm):
    """壓扁的眼球（Z_EYE）併進 Body；上眼皮畫在眼球上（paint_eye）。"""
    for side in (-1, 1):
        part = new_bmesh()
        bmesh.ops.create_uvsphere(part, u_segments=22, v_segments=10, radius=1.0)  # 邊緣是圓，段數多一點
        set_zone(part, part.faces, Z_EYE)
        merge(bm, place_eye(part, side))


def build_body():
    bm = metaball_bmesh(BODY_ELEMS, 0.03)
    flatten_bottom(bm)
    bm = decimate(bm, BODY_TRIS)
    set_zone(bm, bm.faces, Z_FUR)
    set_mask(bm, bm.verts, lambda v: 0.0)
    tree = BVHTree.FromBMesh(bm)
    setup_eyes(tree)
    add_eyes(bm)
    return bm, tree


def build_ear(side, tree):
    """凹的三角耳：三角形網格（中間往後凹、耳尖往外彎）加厚 0.05；前面（Z_EAR_FRONT）中間畫粉紅（mask = 內耳權重）。"""
    hit, _ = surface_hit(tree, (0.3 * side, 3.2, HEAD_C[2] - 0.05), (0, -1, 0))
    root = to_unity(hit) - Vector((0, 0.07, 0))
    W, H, CUP, ROWS = 0.22, 0.5, 0.08, 6
    # 耳朵的局部軸：往外傾 16°、往後 8°（計畫圖：大耳朵、豎直）
    tilt = Quaternion(Vector((0, 0, 1)), math.radians(-16 * side)) @ Quaternion(Vector((1, 0, 0)), math.radians(-8))
    ax, ay, az = tilt @ Vector((1, 0, 0)), tilt @ Vector((0, 1, 0)), tilt @ Vector((0, 0, 1))
    bm = bmesh.new()
    mask = bm.verts.layers.float.new("mask")
    grid = []
    for i in range(ROWS + 1):
        t = i / ROWS
        count = ROWS - i + 1
        row = []
        for j in range(count):
            s = (j / (count - 1) * 2 - 1) if count > 1 else 0.0   # −1..1 橫向
            x = s * W * (1 - t) + 0.04 * side * t * t                # 耳尖往外彎
            z = -CUP * (1 - s * s) * math.sin(math.pi * min(t * 1.1, 1))
            v = bm.verts.new(U(*(root + ax * x + ay * (H * t) + az * z)))
            v[mask] = max(0.0, 1 - abs(s) / 0.62) * (1.0 if 0.08 < t < 0.82 else 0.0)
            row.append(v)
        grid.append(row)
    for i in range(ROWS):
        a, b = grid[i], grid[i + 1]
        for j in range(len(b)):
            bm.faces.new((a[j], a[j + 1], b[j]))
            if j + 1 < len(b):
                bm.faces.new((a[j + 1], b[j + 1], b[j]))
    front = U(*az)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if sum((f.normal for f in bm.faces), Vector()).dot(front) < 0:
        for f in bm.faces:
            f.normal_flip()
    bm = apply_modifier(bm, "SOLIDIFY", thickness=0.05, offset=-1.0, use_rim=True)
    zone = bm.faces.layers.int.new("zone")
    for f in bm.faces:
        f[zone] = Z_EAR_FRONT if f.normal.dot(front) > 0.3 else Z_EAR_BACK
    return bm, root


def build_tail():
    """尾巴：從臀部往後垂到地面，尾尖貼地往右前勾（計畫圖的側面），尾尖深色。mask = 長度比例。"""
    path = catmull([(0.0, 1.72, -1.22), (0.0, 1.52, -1.58), (0.04, 1.05, -1.72), (0.08, 0.58, -1.72),
                    (0.13, 0.27, -1.62), (0.2, 0.2, -1.42), (0.26, 0.3, -1.3)], 2)
    n = len(path)
    radii = [0.13 - 0.05 * (i / (n - 1)) ** 1.5 for i in range(n)]
    bm = new_bmesh()
    faces, params = tube(bm, path, radii, 8)
    set_zone(bm, faces, Z_TAIL)
    set_mask(bm, bm.verts, lambda v: params.get(v, 0.0))
    return bm, Vector(path[0])


def build_whiskers():
    """鬍鬚：口鼻兩側各三根細管，往外、略往下彎。"""
    bm = bmesh.new()
    for side in (-1, 1):
        for k, (y0, y1) in enumerate(((1.88, 1.96), (1.85, 1.83), (1.82, 1.7))):
            a = Vector((0.17 * side, y0, 1.73 - 0.02 * k))
            b = Vector((0.88 * side, y1, 1.5 - 0.04 * k))
            mid = (a + b) / 2 + Vector((0, 0.03, 0.04))
            path = [tuple(a), tuple((a + mid) / 2), tuple(mid), tuple((mid + b) / 2), tuple(b)]
            tube(bm, path, [0.008, 0.007, 0.006, 0.004, 0.002], 3)
    return bm


def build_collar(tree):
    """項圈：套在脖子上（垂直於脖子的方向），從中心往外打射線找半徑，方形截面的圈。"""
    sides = 20
    c, axis = Vector(COLLAR_C), Vector(COLLAR_AXIS).normalized()
    side = Vector((1, 0, 0))
    up = axis.cross(side).normalized()
    inner = []
    for k in range(sides):
        a = 2 * math.pi * k / sides
        d = side * math.cos(a) + up * math.sin(a)
        # 從脖子裡往外打：第一個打到的就是脖子的表面（從外面打會先打到臉頰）
        hit, _ = surface_hit(tree, tuple(c), tuple(d))
        inner.append(to_unity(hit))
    bm = bmesh.new()
    rings = []
    for dy, grow in ((-0.06, -0.01), (-0.06, 0.045), (0.06, 0.045), (0.06, -0.01)):
        rings.append([bm.verts.new(U(*(p + (p - c - axis * axis.dot(p - c)).normalized() * grow + axis * dy)))
                      for p in inner])
    for i in range(4):
        r0, r1 = rings[i], rings[(i + 1) % 4]
        for k in range(sides):
            bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def build_ghost_eyes():
    """幻影的發光眼睛：比眼球大一點的球的前半，上面照上眼皮切掉（跟本體的眼形一樣）。"""
    bm = bmesh.new()
    for side in (-1, 1):
        part = bmesh.new()
        bmesh.ops.create_uvsphere(part, u_segments=22, v_segments=10, radius=1.0)
        # 在局部座標切：上眼皮以上不要、後半不要，再擺到眼睛的位置（比眼球大一點）
        bmesh.ops.bisect_plane(part, geom=part.verts[:] + part.edges[:] + part.faces[:],
                               plane_co=LID_N * LID_K, plane_no=LID_N, clear_outer=True)
        bmesh.ops.bisect_plane(part, geom=part.verts[:] + part.edges[:] + part.faces[:],
                               plane_co=(0, 0, 0), plane_no=(0, 0, 1), clear_inner=True)
        merge(bm, place_eye(part, side, 0.008))
    return bm


# ---------------------------------------------------------------- 物件與材質

def finish(obj, material):
    """三角化、指定材質（kit.finalize 是色票用的，這隻貓走專屬貼圖）。
    平面著色（計畫圖的三角切面），只有眼球平滑（眼睛是一片亮面，不要切面）。"""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()
    zone = mesh.attributes.get("zone")
    for p in mesh.polygons:
        p.use_smooth = bool(zone) and zone.data[p.index].value == Z_EYE
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    mesh.materials.clear()
    mesh.materials.append(material)


def body_material(image):
    """Blender 端的預覽材質（Workbench 的 Texture 模式讀它的圖）；Unity 端同名材質另外建。"""
    mat = bpy.data.materials.get(MAT_BODY) or bpy.data.materials.new(MAT_BODY)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nodes.active = tex
    return mat


def view3d_override():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "VIEW_3D":
                return dict(window=window, area=area, region=next(r for r in area.regions if r.type == "WINDOW"))
    raise RuntimeError("找不到 3D 視窗（Smart UV Project 要在有視窗的 Blender 裡跑）")


def uv_islands(bm, uv):
    """依 UV 相連分島（面的清單）。"""
    key = {}
    for f in bm.faces:
        for l in f.loops:
            key.setdefault((l.vert.index, round(l[uv].uv.x, 6), round(l[uv].uv.y, 6)), []).append(f)
    seen, islands = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, island = [f], []
        while stack:
            g = stack.pop()
            if g.index in seen:
                continue
            seen.add(g.index)
            island.append(g)
            for l in g.loops:
                for h in key[(l.vert.index, round(l[uv].uv.x, 6), round(l[uv].uv.y, 6))]:
                    if h.index not in seen:
                        stack.append(h)
        islands.append(island)
    return islands


def unwrap(objs):
    """所有貼圖部件一起 Smart UV Project，眼球的島放大 2.5 倍（虹膜、瞳孔要清楚）再一起排。"""
    scene = objs[0].users_scene[0]
    layer = scene.view_layers[0]
    with bpy.context.temp_override(**view3d_override(), scene=scene, view_layer=layer):
        if bpy.context.object and bpy.context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")
        for o in scene.objects:
            o.select_set(False)
        for o in objs:
            o.hide_set(False)
            o.select_set(True)
        layer.objects.active = objs[0]
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(UV_ANGLE), island_margin=0.004, scale_to_bounds=False)
        for o in objs:
            bm = bmesh.from_edit_mesh(o.data)
            bm.faces.ensure_lookup_table()
            zone = bm.faces.layers.int.get("zone")
            uv = bm.loops.layers.uv.active
            for island in uv_islands(bm, uv):
                if zone and any(f[zone] == Z_EYE for f in island):
                    loops = [l for f in island for l in f.loops]
                    c = sum((l[uv].uv for l in loops), Vector((0, 0))) / len(loops)
                    for l in loops:
                        l[uv].uv = c + (l[uv].uv - c) * 2.5
            bmesh.update_edit_mesh(o.data)
        # UV 選取跟網格同步，才會排到每一個島（不然放大過的眼球島會疊在別的島上）
        tool = scene.tool_settings
        sync = tool.use_uv_select_sync
        tool.use_uv_select_sync = True
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
        tool.use_uv_select_sync = sync
        bpy.ops.object.mode_set(mode="OBJECT")


# ---------------------------------------------------------------- 貼圖

def rgb(c):
    return np.array(c, dtype=np.float64) / 255.0


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def vnoise(p, scale, seed=0):
    """3D value noise（numpy），輸出約 −1..1。"""
    q = p * scale + seed * 17.13
    i = np.floor(q)
    f = q - i
    f = f * f * (3 - 2 * f)
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                o = np.array([dx, dy, dz])
                h = np.modf(np.abs(np.sin((i + o) @ np.array([127.1, 311.7, 74.7])) * 43758.5453))[0]
                w = (f[:, 0] if dx else 1 - f[:, 0]) * (f[:, 1] if dy else 1 - f[:, 1]) * (f[:, 2] if dz else 1 - f[:, 2])
                out = out + w * h
    return out * 2 - 1


def band(x, duty):
    """週期 1 的條紋：1 = 條紋上，邊緣抗鋸齒一點。"""
    d = np.abs((x % 1.0) - 0.5)
    return smooth(duty * 0.5 + 0.03, duty * 0.5 - 0.03, 0.5 - d)


def seg_dist(p, a, b):
    """2D 點（N,2）到線段 ab 的距離。"""
    a, b = np.array(a, dtype=np.float64), np.array(b, dtype=np.float64)
    ab = b - a
    t = np.clip(((p - a) @ ab) / (ab @ ab), 0, 1)
    return np.linalg.norm(p - (a + t[:, None] * ab), axis=1)


def mix(color, other, w):
    return color * (1 - w[:, None]) + rgb(other) * w[:, None]


def paint_fur(p, n):
    """身體的顏色（照計畫圖）：灰褐底毛、背線、身側直紋、大腿弧紋、腿上橫紋、額頭的 M 與頭頂直紋、
    臉頰兩條；白毛（口鼻、下巴、胸口的 V、腳掌）、粉紅鼻子、嘴。p、n 是 Unity 座標 (N,3)。"""
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    ax = np.abs(x)
    hx, hy, hz = HEAD_C
    wob = vnoise(p, 6.0, 1) * 0.5 + vnoise(p, 13.0, 2) * 0.25
    broken = smooth(-0.35, 0.05, vnoise(p, 4.0, 5))   # 條紋斷斷續續，不是一圈圈的環
    color = np.tile(rgb(C_FUR), (len(p), 1))
    # 背上略深、肚子下面略淺
    color = mix(color, C_FUR_DARK, smooth(0.3, 0.85, n[:, 1]) * 0.4)
    color = mix(color, C_CREAM, smooth(-0.4, -0.85, n[:, 1]) * smooth(1.2, 0.9, y) * 0.35)

    head = np.hypot(np.hypot(x / 0.75, (y - hy) / 0.6), (z - hz) / 0.6) < 1.0
    leg = (y < 0.95) & ~head
    haunch = (ax > 0.12) & (z < -0.35) & (y > 0.6) & ~head
    torso = ~head & ~leg & ~haunch

    stripes = np.zeros(len(p))
    # 軀幹：繞著身體的直紋（頂上往前彎成 V、往下往後斜），肚子下面淡掉
    s = (z + 0.3 * (ax / 0.5) ** 2 + 0.12 * (2.0 - y)) / 0.27 + wob * 0.35
    stripes = np.where(torso, band(s, 0.34) * smooth(0.9, 1.15, y) * broken, stripes)
    # 背線：沿脊椎一條深色
    spine = smooth(0.13, 0.07, ax + wob * 0.02) * smooth(0.55, 0.8, n[:, 1])
    stripes = np.where(torso | haunch, np.maximum(stripes, spine * 0.85), stripes)
    # 大腿：繞著大腿中心的弧紋
    ring = np.hypot((z + 0.8) / 0.5, (y - 1.3) / 0.55)
    arcs = band(ring / 0.3 + wob * 0.25, 0.3) * smooth(0.45, 0.6, ring) * smooth(1.25, 1.05, ring)
    stripes = np.where(haunch, np.maximum(arcs * broken, spine * 0.85), stripes)
    # 腿：橫紋，腳掌白
    stripes = np.where(leg, band(y / 0.2 + wob * 0.3, 0.36) * smooth(0.22, 0.32, y) * smooth(0.2, 0.0, n[:, 1] - 0.5)
                       * broken, stripes)
    # 頭：額頭的 M（眉心往上越過頭頂的三條直紋）
    up = (y > 2.2) & (z > hz - 0.5)
    m_lines = band((x + 0.065) / 0.13 + wob * 0.15, 0.4) * smooth(0.22, 0.15, ax) * smooth(2.2, 2.27, y)
    stripes = np.where(head & up, np.maximum(stripes, m_lines), stripes)
    # 後腦：橫紋接到脖子
    nape = head & (z < hz - 0.05)
    stripes = np.where(nape, np.maximum(stripes, band((y - 0.6 * z) / 0.22 + wob * 0.3, 0.32) * broken), stripes)
    # 臉頰：外眼角往後兩條
    cheek = head & (ax > 0.38) & (y > 1.76) & (y < 2.21) & (z > hz - 0.3)
    for off in (0.0, -0.11):
        line = np.abs(y - (2.09 + off - 0.32 * (ax - 0.38))) + wob * 0.01
        taper = smooth(0.76, 0.5, ax)
        stripes = np.where(cheek, np.maximum(stripes, smooth(0.032 * taper + 0.004, 0.016 * taper, line) * taper),
                           stripes)
    color = mix(color, C_STRIPE, stripes)

    # 白毛
    edge = wob * 0.03
    face_front = smooth(0.15, 0.35, n[:, 2]) * smooth(hz + 0.2, hz + 0.32, z)
    muzzle = smooth(1.0, 0.85, np.hypot(x / 0.34, (y - 1.82) / 0.19) + edge) * face_front
    chin = smooth(0.2, 0.14, ax + edge) * smooth(1.82, 1.76, y) * (z > 1.3) * (y > 1.55)
    w = 0.06 + 0.28 * smooth(1.0, 1.7, y)   # 胸口白毛的半寬：下巴下面寬、往下收成 V
    bib = (smooth(w + 0.02, w - 0.02, ax + edge) * smooth(0.1, 0.35, n[:, 2]) * (z > 0.6)
           * smooth(0.95, 1.1, y) * smooth(1.85, 1.75, y))
    feet = smooth(0.26, 0.18, y + edge)
    color = mix(color, C_CREAM, np.clip(np.maximum.reduce([muzzle, chin, bib, feet]), 0, 1))

    # 鼻子（倒三角）與嘴（人字往下撇）
    p2 = np.stack([x, y], axis=1)
    on_face = z > hz + 0.3
    nose = on_face & (y > 1.895) & (y < 2.01) & (ax < (y - 1.895) * 1.05 + 0.006)
    color[nose] = rgb(C_NOSE)
    mouth = np.minimum.reduce([seg_dist(p2, (0, 1.895), (0, 1.835)),
                               seg_dist(p2, (0, 1.835), (0.05, 1.815)), seg_dist(p2, (0, 1.835), (-0.05, 1.815)),
                               seg_dist(p2, (0.05, 1.815), (0.1, 1.79)), seg_dist(p2, (-0.05, 1.815), (-0.1, 1.79))])
    color[on_face & (mouth < 0.009)] = rgb(C_DARK)
    # 鬍鬚的毛孔（口鼻兩側三點）
    for dx, dy in ((0.09, 1.87), (0.14, 1.86), (0.115, 1.835)):
        color[on_face & (np.hypot(ax - dx, y - dy) < 0.011)] = rgb(C_STRIPE)
    return color


def paint_eye(p, n):
    """Q 版眼睛：大片黃綠虹膜（外圈一圈深綠）、粗橢圓瞳孔、反光（左右鏡像，兩眼長得一樣）；
    上面一條斜切的上眼皮（內側低、外側高），眼皮的顏色接頭上的毛，切邊畫一條深色。"""
    out = paint_fur(p, n)
    for side in (-1, 1):
        sel = (p[:, 0] * side) > 0
        if not sel.any():
            continue
        q = eye_local(p[sel], side)
        po, py, front = q[:, 0], q[:, 1], q[:, 2]
        col = np.tile(rgb(C_SCLERA), (len(q), 1))
        iris = front > math.cos(math.radians(70))
        t = np.clip((1 - front) / (1 - math.cos(math.radians(70))), 0, 1)[:, None]
        col = np.where(iris[:, None], rgb(C_IRIS) * (1 - t * 0.35) + rgb(C_IRIS_RIM) * t * 0.35, col)
        col[iris & (front < math.cos(math.radians(64)))] = rgb(C_IRIS_RIM)
        col[iris & ((po / 0.3) ** 2 + ((py + 0.1) / 0.42) ** 2 < 1)] = rgb(C_DARK)
        # 反光：一大一小，在瞳孔靠外眼角、眼皮蓋不到的地方
        col[iris & ((po - 0.2) ** 2 + (py - 0.0) ** 2 < 0.11 ** 2)] = rgb(C_WHITE)
        col[iris & ((po + 0.12) ** 2 + (py + 0.36) ** 2 < 0.05 ** 2)] = rgb(C_WHITE)
        # 上眼皮：切線以上是毛，切線下緣一條深色；下眼皮也蓋一點（杏仁眼），邊緣一條細的深色
        cut = q @ np.array(LID_N)
        lid = (cut > LID_K) | (py < -LOW_K)
        col[(cut > LID_K - 0.09) & ~lid] = rgb(C_DARK)
        col[(py < -LOW_K + 0.05) & ~lid] = rgb(C_DARK)
        col[lid] = out[sel][lid]
        out[sel] = col
    return out


def rasterize(obj):
    """這個物件每個三角形在 UV 上蓋到的像素：回傳 (像素 ij, Unity 位置〔Model 空間〕, 法線, mask, zone)。"""
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers.active.data
    zone_attr = mesh.attributes.get("zone")
    mask_attr = mesh.attributes.get("mask")
    co = np.array([v.co for v in mesh.vertices], dtype=np.float64)
    nor = np.array([v.normal for v in mesh.vertices], dtype=np.float64)
    m = np.array(obj.matrix_world)
    co = co @ m[:3, :3].T + m[:3, 3]
    nor = nor @ m[:3, :3].T
    co, nor = to_unity(co), to_unity(nor)
    masks = np.array([d.value for d in mask_attr.data]) if mask_attr else np.zeros(len(mesh.vertices))
    out = {"ij": [], "p": [], "n": [], "m": [], "z": []}
    for tri in mesh.loop_triangles:
        uvs = np.array([uv[li].uv for li in tri.loops]) * TEX_SIZE - 0.5
        lo = np.clip(np.floor(uvs.min(0)).astype(int) - 1, 0, TEX_SIZE - 1)
        hi = np.clip(np.ceil(uvs.max(0)).astype(int) + 1, 0, TEX_SIZE - 1)
        xs, ys = np.meshgrid(np.arange(lo[0], hi[0] + 1), np.arange(lo[1], hi[1] + 1))
        pts = np.stack([xs.ravel(), ys.ravel()], axis=1).astype(np.float64)
        a, b, c = uvs
        v0, v1 = b - a, c - a
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-12:
            continue
        d = pts - a
        w1 = (d[:, 0] * v1[1] - v1[0] * d[:, 1]) / den
        w2 = (v0[0] * d[:, 1] - d[:, 0] * v0[1]) / den
        w0 = 1 - w1 - w2
        eps = 0.6 / max(math.sqrt(abs(den)), 1)
        inside = (w0 > -eps) & (w1 > -eps) & (w2 > -eps)
        if not inside.any():
            continue
        w = np.stack([w0, w1, w2], axis=1)[inside]
        vi = list(tri.vertices)
        out["ij"].append(pts[inside].astype(int))
        out["p"].append(w @ co[vi])
        out["n"].append(w @ nor[vi])
        out["m"].append(w @ masks[vi])
        out["z"].append(np.full(len(w), zone_attr.data[tri.polygon_index].value if zone_attr else Z_FUR))
    if not out["p"]:
        return None
    ij, p, n, m, z = (np.concatenate(out[k]) for k in ("ij", "p", "n", "m", "z"))
    n /= np.maximum(np.linalg.norm(n, axis=1), 1e-9)[:, None]
    return ij, p, n, m, z


def shade(p, n, m, z):
    """依區域上色。"""
    col = np.tile(rgb(C_FUR), (len(p), 1))
    sel = z == Z_FUR
    if sel.any():
        col[sel] = paint_fur(p[sel], n[sel])
    sel = z == Z_EYE
    if sel.any():
        col[sel] = paint_eye(p[sel], n[sel])
    sel = (z == Z_EAR_FRONT) | (z == Z_EAR_BACK)
    if sel.any():
        inner = (z[sel] == Z_EAR_FRONT) * smooth(0.35, 0.6, m[sel])
        col[sel] = mix(np.tile(rgb(C_FUR_DARK), (sel.sum(), 1)), C_EAR_IN, inner)
    sel = z == Z_TAIL
    if sel.any():
        t = m[sel]
        s = np.maximum(band(t * 9.0 + vnoise(p[sel], 8.0, 4) * 0.25, 0.4), smooth(0.88, 0.92, t))
        col[sel] = mix(np.tile(rgb(C_FUR), (sel.sum(), 1)), C_STRIPE, s)
    return col


def uv_overlap(obj, count):
    """這個物件的三角形內部（不含邊緣）蓋到已經被蓋過的像素數 = UV 重疊；count 是累計的覆蓋次數。"""
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    hits = 0
    for tri in mesh.loop_triangles:
        uvs = np.array([uv[li].uv for li in tri.loops]) * TEX_SIZE - 0.5
        lo = np.clip(np.floor(uvs.min(0)).astype(int), 0, TEX_SIZE - 1)
        hi = np.clip(np.ceil(uvs.max(0)).astype(int), 0, TEX_SIZE - 1)
        xs, ys = np.meshgrid(np.arange(lo[0], hi[0] + 1), np.arange(lo[1], hi[1] + 1))
        pts = np.stack([xs.ravel(), ys.ravel()], axis=1).astype(np.float64)
        a, b, c = uvs
        v0, v1 = b - a, c - a
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-12:
            continue
        d = pts - a
        w1 = (d[:, 0] * v1[1] - v1[0] * d[:, 1]) / den
        w2 = (v0[0] * d[:, 1] - d[:, 0] * v0[1]) / den
        inside = (w1 > 0.02) & (w2 > 0.02) & (1 - w1 - w2 > 0.02)
        q = pts[inside].astype(int)
        hits += int((count[q[:, 1], q[:, 0]] > 0).sum())
        count[q[:, 1], q[:, 0]] += 1
    return hits


def paint(objs, path):
    """依 3D 位置算每個貼圖像素的顏色，最後往外擴邊（mipmap 與雙線性取樣不會吃到空白）。"""
    img = np.zeros((TEX_SIZE, TEX_SIZE, 3), dtype=np.float64)
    filled = np.zeros((TEX_SIZE, TEX_SIZE), dtype=bool)
    owner = np.zeros((TEX_SIZE, TEX_SIZE), dtype=np.int32)
    overlap = 0
    for k, obj in enumerate(objs):
        r = rasterize(obj)
        if r is None:
            continue
        ij, p, n, m, z = r
        overlap += uv_overlap(obj, owner)
        img[ij[:, 1], ij[:, 0]] = shade(p, n, m, z)
        filled[ij[:, 1], ij[:, 0]] = True
    paint.overlap = overlap
    for _ in range(12):
        empty = ~filled
        if not empty.any():
            break
        acc = np.zeros_like(img)
        cnt = np.zeros(filled.shape)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            acc += np.roll(np.roll(img * filled[..., None], dx, 1), dy, 0)
            cnt += np.roll(np.roll(filled.astype(np.float64), dx, 1), dy, 0)
        grow = empty & (cnt > 0)
        img[grow] = acc[grow] / cnt[grow][:, None]
        filled |= grow
    img[~filled] = rgb(C_FUR)
    image = bpy.data.images.get(TEX_NAME)
    if image is None or tuple(image.size) != (TEX_SIZE, TEX_SIZE):
        if image is not None:
            bpy.data.images.remove(image)
        image = bpy.data.images.new(TEX_NAME, TEX_SIZE, TEX_SIZE, alpha=False)
    image.colorspace_settings.name = "sRGB"
    rgba = np.concatenate([img, np.ones((TEX_SIZE, TEX_SIZE, 1))], axis=2).astype(np.float32)
    image.pixels.foreach_set(rgba.ravel())
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return image


# ---------------------------------------------------------------- 組裝

def build():
    random.seed(SEED)
    col = kit.begin(NAME)
    objs = {}
    image = bpy.data.images.get(TEX_NAME)
    if image is None:
        image = bpy.data.images.new(TEX_NAME, TEX_SIZE, TEX_SIZE, alpha=False)
    mat = body_material(image)

    bm, tree = build_body()
    objs["Body"] = kit.make_object("Body", bm, col)
    for side, name in ((-1, "EarL"), (1, "EarR")):
        bm, root = build_ear(side, tree)
        objs[name] = kit.make_object(name, bm, col, origin=U(*root))
    bm, root = build_tail()
    objs["Tail"] = kit.make_object("Tail", bm, col, origin=U(*root))
    objs["Whiskers"] = kit.make_object("Whiskers", build_whiskers(), col, origin=U(0, 1.86, 1.73))
    objs["Collar"] = kit.make_object("Collar", build_collar(tree), col, origin=U(*COLLAR_C))
    objs["GhostEyes"] = kit.make_object("GhostEyes", build_ghost_eyes(), col, origin=U(0, EYE_AT[1], HEAD_C[2]))

    textured = [objs[n] for n in ("Body", "EarL", "EarR", "Tail")]
    for o in textured:
        finish(o, mat)
    finish(objs["Whiskers"], kit.special_material("OiiaCat_Whisker", (0.95, 0.94, 0.9)))
    finish(objs["Collar"], kit.special_material("OiiaCat_Collar", (1.0, 0.8, 0.25)))
    finish(objs["GhostEyes"], kit.special_material("OiiaCat_GhostEye", (0.75, 0.65, 1.0)))
    unwrap(textured)
    mat.node_tree.nodes.active.image = paint(textured, os.path.join(kit.TEXTURES_DIR, f"{TEX_NAME}.png"))
    return col, objs


col, objs = build()
cat = [o for n, o in objs.items() if n != "GhostEyes"]
report = kit.check(cat, budget=BUDGET, allow_smooth=True,
                   pivots={"Body": U(0, 0, 0), "Collar": U(*COLLAR_C)})
report["core_size"] = kit.check([objs[n] for n in ("Body", "EarL", "EarR")], allow_smooth=True)["bbox_size"]
# 合成圖只畫平常看得到的部件：項圈平常關著，發光眼睛只在幻影上
hidden = [objs["Collar"], objs["GhostEyes"]]
for o in hidden:
    o.hide_render = True
report["sheet"] = kit.contact_sheet([o for o in cat if o not in hidden], NAME)
for o in hidden:
    o.hide_render = False
result = {k: report[k] for k in ("ok", "issues", "tris", "bbox_size", "core_size", "sheet")}
result["parts"] = {n: i["tris"] for n, i in report["objects"].items()}
result["uv_overlap_px"] = paint.overlap

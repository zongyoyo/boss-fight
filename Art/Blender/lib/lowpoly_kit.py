"""
低面數資產的共用函式庫（docs/Art/ART_STYLE.md）。

每支資產腳本（Art/Blender/assets/<類別>/<名稱>.py）的流程：
    kit = load_kit(__file__)          # 見 assets/test/SM_Test_AxisCube.py 開頭
    col = kit.begin("SM_Xxx")         # 清掉並重建這個資產自己的 scene 與 collection
    bm = kit.new_bmesh()
    kit.add_prism(bm, 8, 0.3, 2.0, cell="bark.m", jitter=0.04, rng=rng)
    obj = kit.make_object("SM_Xxx", bm, col)
    kit.finalize(obj)                 # 三角化、平面著色、UV 縮到色格中心、指定材質槽
    report = kit.check([obj], budget=(150, 700), target_size=(1, 2, 1))
    kit.contact_sheet([obj], "SM_Xxx")
    kit.export_fbx([obj], "Env/SM_Xxx.fbx")
    kit.save_blend("SM_Xxx")

色格：每個面在 int 面屬性 "cell" 存色票索引（row * 16 + col），finalize 時才寫進 UV。
名稱用 palette.json 的「組名.l／.m／.d」（亮、中、暗），例如 "bark.m"。
不走色票的獨立材質（項圈、裂縫…）用 finalize(obj, material="OiiaCat_Collar")：整個物件一個材質，
材質名稱就是 Unity 裡材質資產的名稱（匯入時依名字換成那個資產）。

座標：1 單位 = 1 m，模型正面朝 −Y，上方 +Z；匯出後在 Unity 正面朝 +Z。
"""

import json
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

# ---------------------------------------------------------------- 路徑

LIB_DIR = os.path.dirname(os.path.abspath(__file__))
BLENDER_DIR = os.path.dirname(LIB_DIR)
ROOT = os.path.dirname(os.path.dirname(BLENDER_DIR))
PALETTE_JSON = os.path.join(BLENDER_DIR, "palette", "palette.json")
BLEND_DIR = os.path.join(BLENDER_DIR, "blend")
PREVIEW_DIR = os.path.join(BLENDER_DIR, "preview")
UNITY_ART = os.path.join(ROOT, "Assets", "Art")
MODELS_DIR = os.path.join(UNITY_ART, "Models")
TEXTURES_DIR = os.path.join(UNITY_ART, "Textures")
PALETTE_PNG = os.path.join(TEXTURES_DIR, "T_Palette.png")
EMISSIVE_PNG = os.path.join(TEXTURES_DIR, "T_Palette_Emissive.png")

MAT_PALETTE = "M_Palette"
MAT_EMISSIVE = "M_Palette_Emissive"
CELL_ATTR = "cell"
UV_EPS = 1e-4

# ---------------------------------------------------------------- 色票

_palette = None


def _hex(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def palette(reload=False):
    """讀 palette.json，回傳 {grid, size, emissive_rows, cells: {名稱: (index, rgb)}}。"""
    global _palette
    if _palette is not None and not reload:
        return _palette
    with open(PALETTE_JSON, encoding="utf-8") as f:
        data = json.load(f)
    grid = data["grid"]
    light, dark = data["tiers"]["light"], data["tiers"]["dark"]
    cells, used = {}, {}
    for name, g in data["groups"].items():
        mid = _hex(g["mid"])
        tiers = {
            "l": _hex(g["l"]) if "l" in g else tuple(c + (1.0 - c) * light for c in mid),
            "m": mid,
            "d": _hex(g["d"]) if "d" in g else tuple(c * dark for c in mid),
        }
        for i, t in enumerate(("l", "m", "d")):
            row, col = g["row"], g["col"] + i
            if col >= grid:
                raise ValueError(f"色格 {name}.{t} 超出第 {row} 列")
            index = row * grid + col
            if index in used:
                raise ValueError(f"色格 {name}.{t} 和 {used[index]} 重疊")
            used[index] = f"{name}.{t}"
            cells[f"{name}.{t}"] = (index, tiers[t])
    _palette = {"grid": grid, "size": data["size"], "emissive_rows": set(data["emissive_rows"]), "cells": cells}
    return _palette


def cell_index(name):
    cells = palette()["cells"]
    if name not in cells:
        raise KeyError(f"色票沒有 {name}（看 Art/Blender/palette/palette.json）")
    return cells[name][0]


def cell_uv(index):
    """色格中心的 UV；第 0 列在圖的最上面。"""
    grid = palette()["grid"]
    row, col = divmod(index, grid)
    return Vector(((col + 0.5) / grid, 1.0 - (row + 0.5) / grid))


def is_emissive(index):
    p = palette()
    return index // p["grid"] in p["emissive_rows"]


def build_palette():
    """依 palette.json 寫出 T_Palette.png 與 T_Palette_Emissive.png（沒用到的格子是洋紅色，滲色時一眼看得到）。"""
    p = palette(reload=True)
    size, grid = p["size"], p["grid"]
    px = size // grid
    base = np.zeros((size, size, 4), dtype=np.float32)
    base[:] = (1.0, 0.0, 1.0, 1.0)
    glow = np.zeros((size, size, 4), dtype=np.float32)
    glow[:, :, 3] = 1.0
    for index, rgb in p["cells"].values():
        row, col = divmod(index, grid)
        y0 = size - (row + 1) * px  # Blender 的像素從最下面一列開始
        x0 = col * px
        base[y0:y0 + px, x0:x0 + px, :3] = rgb
        if row in p["emissive_rows"]:
            glow[y0:y0 + px, x0:x0 + px, :3] = rgb
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    for path, pixels in ((PALETTE_PNG, base), (EMISSIVE_PNG, glow)):
        name = os.path.splitext(os.path.basename(path))[0]
        img = bpy.data.images.get(name)
        if img is not None and (img.size[0] != size or img.size[1] != size):
            bpy.data.images.remove(img)
            img = None
        if img is None:
            img = bpy.data.images.new(name, size, size, alpha=False)
        img.colorspace_settings.name = "sRGB"
        img.pixels.foreach_set(pixels.ravel())
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        img.reload()
    return {"cells": len(p["cells"]), "png": [PALETTE_PNG, EMISSIVE_PNG]}


def _palette_image(path):
    name = os.path.splitext(os.path.basename(path))[0]
    img = bpy.data.images.get(name)
    if img is None:
        if not os.path.exists(path):
            build_palette()
            img = bpy.data.images.get(name)
        else:
            img = bpy.data.images.load(path, check_existing=True)
            img.name = name
    return img


def palette_materials():
    """Blender 端的預覽材質（Closest 取樣）。匯出時只用到名稱，Unity 依名稱換成同名的材質資產。"""
    mats = []
    for mat_name, emissive in ((MAT_PALETTE, False), (MAT_EMISSIVE, True)):
        mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = 1.0
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = _palette_image(PALETTE_PNG)
        tex.interpolation = "Closest"
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if emissive:
            glow = nt.nodes.new("ShaderNodeTexImage")
            glow.image = _palette_image(EMISSIVE_PNG)
            glow.interpolation = "Closest"
            nt.links.new(glow.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = 1.0
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        nt.nodes.active = tex  # Workbench 的 Texture 顯示用作用中的圖片節點
        mats.append(mat)
    return mats


def special_material(name, color=(0.8, 0.8, 0.8)):
    """不走色票的獨立材質（名稱要和 Unity 的材質資產相同）；Blender 端只放一個預覽色。"""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = False
    mat.diffuse_color = (*color, 1.0)
    return mat

# ---------------------------------------------------------------- 場景

def begin(name):
    """清掉並重建這個資產的 scene（同名）與 collection，設為目前的 scene；回傳 collection。"""
    scene = bpy.data.scenes.get(name)
    if scene is None:
        scene = bpy.data.scenes.new(name)
    for obj in list(scene.collection.all_objects):
        _delete_object(obj)
    for child in list(scene.collection.children):
        _delete_collection(child)
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    for window in bpy.context.window_manager.windows:
        window.scene = scene
    _purge_orphans()
    return col


def _delete_object(obj):
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if isinstance(data, bpy.types.Mesh) and data.users == 0:
        bpy.data.meshes.remove(data)


def _delete_collection(col):
    for obj in list(col.all_objects):
        _delete_object(obj)
    for child in list(col.children):
        _delete_collection(child)
    bpy.data.collections.remove(col)


def _purge_orphans():
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for cam in list(bpy.data.cameras):
        if cam.users == 0:
            bpy.data.cameras.remove(cam)


def scene_of(col):
    for scene in bpy.data.scenes:
        if col.name in scene.collection.children:
            return scene
    return bpy.context.scene

# ---------------------------------------------------------------- 建模（bmesh）

def new_bmesh():
    bm = bmesh.new()
    bm.faces.layers.int.new(CELL_ATTR)
    return bm


def _layer(bm):
    return bm.faces.layers.int.get(CELL_ATTR) or bm.faces.layers.int.new(CELL_ATTR)


def _paint_faces(bm, faces, cell):
    if cell is None:
        return
    index = cell_index(cell) if isinstance(cell, str) else cell
    layer = _layer(bm)
    for f in faces:
        f[layer] = index


def _jitter_verts(verts, amount, rng, axes=(1, 1, 1)):
    if not amount:
        return
    rng = rng or random.Random(0)
    for v in verts:
        v.co += Vector((rng.uniform(-1, 1) * amount * axes[0],
                        rng.uniform(-1, 1) * amount * axes[1],
                        rng.uniform(-1, 1) * amount * axes[2]))


def _finish_part(bm, verts, matrix, cell, jitter, rng, size):
    """verts 是這個部件新建的頂點（用 bmesh.ops 的回傳值或自己建的清單；不要用「之前有哪些頂點」去比，
    細分時釋放的頂點記憶體會被重用，舊集合會誤認新頂點）。面取這些頂點連到的面。"""
    verts = list(verts)
    faces = list(dict.fromkeys(f for v in verts for f in v.link_faces))
    _jitter_verts(verts, jitter * size if jitter else 0, rng)
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=verts)
    _paint_faces(bm, faces, cell)
    return verts, faces


def placement(location=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1)):
    """位置、Euler 角（度）、縮放 → Matrix。"""
    rot = Matrix.Identity(4)
    for axis, deg in zip("XYZ", rotation):
        if deg:
            rot = Matrix.Rotation(math.radians(deg), 4, axis) @ rot
    return Matrix.Translation(Vector(location)) @ rot @ Matrix.Diagonal((*scale, 1.0))


def add_box(bm, size=(1, 1, 1), location=(0, 0, 0), rotation=(0, 0, 0), cell=None,
            jitter=0.0, rng=None, base=False):
    """方塊；base=True 時 location 是底面中心。jitter 是各邊長的比例。回傳 (verts, faces)。"""
    verts = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    sx, sy, sz = size
    m = placement(location, rotation) @ Matrix.Diagonal((sx, sy, sz, 1.0))
    if base:
        m = m @ Matrix.Translation((0, 0, 0.5))
    return _finish_part(bm, verts, m, cell, jitter, rng, 1.0)


def add_prism(bm, sides, radius, height, location=(0, 0, 0), rotation=(0, 0, 0), radius_top=None,
              cell=None, jitter=0.0, rng=None, caps=True, twist=0.0):
    """n 邊柱（底面中心在 location，沿 +Z 長 height）；radius_top 不同時是錐台、0 是尖錐。jitter 是半徑的比例。"""
    radius_top = radius if radius_top is None else radius_top
    rings = []
    for z, r, tw in ((0.0, radius, 0.0), (height, radius_top, twist)):
        ring = []
        if r <= 0:
            ring.append(bm.verts.new((0, 0, z)))
        else:
            for i in range(sides):
                a = 2 * math.pi * i / sides + math.radians(tw)
                ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)))
        rings.append(ring)
    bottom, top = rings
    if len(top) == 1:
        for i in range(sides):
            bm.faces.new((bottom[i], bottom[(i + 1) % sides], top[0]))
    else:
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
        if caps:
            bm.faces.new(top)
    if caps:
        bm.faces.new(list(reversed(bottom)))
    m = placement(location, rotation)
    return _finish_part(bm, bottom + top, m, cell, jitter, rng, max(radius, radius_top))


def add_icosphere(bm, radius, subdivisions=1, location=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1),
                  cell=None, jitter=0.0, rng=None):
    """icosphere（細分 1 次 80 面、2 次 320 面）；jitter 是半徑的比例。"""
    # bmesh 的 subdivisions=1 是沒細分的 20 面體，所以 +1
    verts = bmesh.ops.create_icosphere(bm, subdivisions=subdivisions + 1, radius=radius)["verts"]
    m = placement(location, rotation, scale)
    return _finish_part(bm, verts, m, cell, jitter, rng, radius)


def add_rock(bm, radius, rng, location=(0, 0, 0), squash=0.7, cuts=2, cell="stone.m", jitter=0.12):
    """變形 icosphere 加幾刀平切面；底部壓平，頂面亮、底部暗由 shade_by_height 處理。"""
    verts, faces = add_icosphere(bm, radius, 1, location=(0, 0, 0), scale=(1, 1, squash),
                                 cell=cell, jitter=jitter, rng=rng)
    for _ in range(cuts):
        n = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.2, 1))).normalized()
        d = radius * rng.uniform(0.55, 0.8)
        for v in verts:
            excess = v.co.dot(n) - d
            if excess > 0:
                v.co -= n * excess
    floor = -radius * squash * 0.6
    for v in verts:
        v.co.z = max(v.co.z, floor)
        v.co.z -= floor
    bmesh.ops.translate(bm, vec=Vector(location), verts=verts)
    bmesh.ops.remove_doubles(bm, verts=verts, dist=radius * 0.02)
    return [v for v in verts if v.is_valid], [f for f in faces if f.is_valid]


def add_plank(bm, length, width, thickness, location=(0, 0, 0), rotation=(0, 0, 0), cell="plank.m",
              jitter=0.015, rng=None):
    """木板（沿 X 長 length），人造物預設偏移各邊長的 1.5%。"""
    return add_box(bm, (length, width, thickness), location, rotation, cell=cell, jitter=jitter, rng=rng)


def add_tube(bm, points, radii, sides=6, cell=None, jitter=0.0, rng=None, caps=True):
    """沿折線的管子（彎樹幹、樹枝、樹根）：每個點一圈 sides 邊，圈面垂直於路徑；半徑 0 的端點收成尖。
    圈的方向用平行移動接續，不會扭轉。jitter 是各點半徑的比例。回傳 (verts, faces)。"""
    pts = [Vector(p) for p in points]
    n = len(pts)
    if n < 2 or len(radii) != n:
        raise ValueError("add_tube 需要至少 2 個點，且每個點一個半徑")
    tangents = []
    for i in range(n):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, n - 1)]
        tangents.append((b - a).normalized())
    ref = Vector((1, 0, 0)) if abs(tangents[0].x) < 0.9 else Vector((0, 1, 0))
    u = (ref - tangents[0] * ref.dot(tangents[0])).normalized()
    rings = []
    for i, (p, r, t) in enumerate(zip(pts, radii, tangents)):
        if i:
            u = (u - t * u.dot(t)).normalized()
        v = t.cross(u)
        if r <= 0:
            vert = bm.verts.new(p)
            _jitter_verts([vert], jitter * max(radii), rng)
            rings.append([vert])
            continue
        ring = [bm.verts.new(p + (u * math.cos(2 * math.pi * k / sides) + v * math.sin(2 * math.pi * k / sides)) * r)
                for k in range(sides)]
        _jitter_verts(ring, jitter * r, rng)
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        if len(b) == 1:
            for k in range(sides):
                bm.faces.new((a[k], a[(k + 1) % sides], b[0]))
        elif len(a) == 1:
            for k in range(sides):
                bm.faces.new((a[0], b[(k + 1) % sides], b[k]))
        else:
            for k in range(sides):
                j = (k + 1) % sides
                bm.faces.new((a[k], a[j], b[j], b[k]))
    if caps:
        if len(rings[0]) > 1:
            bm.faces.new(list(reversed(rings[0])))
        if len(rings[-1]) > 1:
            bm.faces.new(rings[-1])
    return _finish_part(bm, [v for ring in rings for v in ring], None, cell, 0, None, 1.0)


def paint(obj, cell, where=None):
    """把物件上符合 where(face_center: Vector, normal: Vector) 的面改成 cell；where 省略時全部。"""
    index = cell_index(cell) if isinstance(cell, str) else cell
    mesh = obj.data
    attr = mesh.attributes.get(CELL_ATTR) or mesh.attributes.new(CELL_ATTR, "INT", "FACE")
    for poly in mesh.polygons:
        if where is None or where(poly.center, poly.normal):
            attr.data[poly.index].value = index


def shade_by_height(obj, group, low=0.25, high=0.75):
    """把一組顏色依面的高度分成暗／中／亮：底部與凹處用暗階（ART_STYLE「每個顏色做 3 階」）。"""
    zs = [p.center.z for p in obj.data.polygons]
    z0, z1 = min(zs), max(zs)
    span = max(z1 - z0, 1e-6)
    paint(obj, f"{group}.d", lambda c, n: (c.z - z0) / span < low)
    paint(obj, f"{group}.m", lambda c, n: low <= (c.z - z0) / span < high)
    paint(obj, f"{group}.l", lambda c, n: (c.z - z0) / span >= high)


def shade_by_normal(obj, group, up=0.45, down=-0.15, where=None):
    """依面的朝向分亮／中／暗：朝上（normal.z ≥ up）亮、朝下（< down）暗、其餘中階；where 限定範圍。
    附圖的枝片、葉叢、石頭都是「上亮下暗」。"""
    def pick(test):
        return lambda c, n: (where is None or where(c, n)) and test(n.z)
    paint(obj, f"{group}.m", pick(lambda z: down <= z < up))
    paint(obj, f"{group}.l", pick(lambda z: z >= up))
    paint(obj, f"{group}.d", pick(lambda z: z < down))


def shade_faces(bm, faces, group, up=0.45, down=-0.15):
    """shade_by_normal 的 bmesh 版：只處理這個部件的面（建模當下就分好，不用事後靠位置猜是哪個部件）。
    面的繞序要朝外（kit 的 add_* 都是）。"""
    layer = _layer(bm)
    for f in faces:
        f.normal_update()
        z = f.normal.z
        f[layer] = cell_index(f"{group}.{'l' if z >= up else 'd' if z < down else 'm'}")


def make_object(name, bm, collection, origin=(0, 0, 0), parent=None):
    """bmesh → 物件。bmesh 座標是資產空間；origin 是物件原點（軸心）在資產空間的位置。"""
    mesh = bpy.data.meshes.get(name)
    if mesh is not None and mesh.users == 0:
        bpy.data.meshes.remove(mesh)
    mesh = bpy.data.meshes.new(name)
    # 先把法線朝外，paint／shade_by_normal 才能依朝向選面（finalize 會再算一次）
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.translate(bm, vec=-Vector(origin), verts=bm.verts)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.location = Vector(origin)
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def add_empty(name, collection, location=(0, 0, 0), parent=None):
    """空物件（掛點、錨點）；location 是資產空間。"""
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_size = 0.1
    collection.objects.link(obj)
    obj.location = Vector(location)
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj

# ---------------------------------------------------------------- 收尾

def finalize(obj, material=None):
    """三角化、清散落頂點、法線朝外、平面著色、UV 縮到色格中心、指定材質槽。
    material 有給時整個物件用那個獨立材質（不走色票）。"""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    layer = _layer(bm)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        center = cell_uv(f[layer]) if material is None else Vector((0.5, 0.5))
        for loop in f.loops:
            loop[uv].uv = center
    bm.to_mesh(mesh)
    bm.free()

    mesh.materials.clear()
    if material is not None:
        mesh.materials.append(special_material(material) if isinstance(material, str) else material)
        for poly in mesh.polygons:
            poly.material_index = 0
    else:
        base, glow = palette_materials()
        attr = mesh.attributes[CELL_ATTR]
        emissive = [is_emissive(attr.data[p.index].value) for p in mesh.polygons]
        mesh.materials.append(base)
        if any(emissive):
            mesh.materials.append(glow)
        for poly, e in zip(mesh.polygons, emissive):
            poly.material_index = 1 if e else 0
    for poly in mesh.polygons:
        poly.use_smooth = False
    mesh.update()
    return obj


def apply_rotation_scale(obj):
    """把物件的旋轉與縮放烤進網格（位置保留），匯出後在 Unity 是 Rotation 0、Scale 1。"""
    basis = obj.matrix_basis.copy()
    loc = basis.to_translation()
    rs = basis.to_3x3().to_4x4()
    if obj.type == "MESH" and rs != Matrix.Identity(4):
        obj.data.transform(rs)
    obj.matrix_basis = Matrix.Translation(loc)

# ---------------------------------------------------------------- 檢查

def _world_bbox(objs):
    lo = Vector((math.inf,) * 3)
    hi = Vector((-math.inf,) * 3)
    for obj in objs:
        if obj.type != "MESH":
            continue
        for v in obj.data.vertices:
            w = obj.matrix_world @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


def _signed_volume(mesh):
    vol = 0.0
    for tri in mesh.loop_triangles:
        a, b, c = (mesh.vertices[i].co for i in tri.vertices)
        vol += a.dot(b.cross(c)) / 6.0
    return vol


def check(objs, budget=None, target_size=None, size_tol=0.05, pivots=None, pivot_tol=0.01):
    """ART_STYLE「單一資產的工作循環」第 5 步的自動檢查。回傳 dict（ok、issues 與各項數字），不印整個場景。
    budget：(最少, 最多) 三角面；target_size：(寬 X, 深 Y, 高 Z) m；pivots：{物件名: (x, y, z)} 資產空間的軸心。"""
    issues = []
    tris = 0
    objects = {}
    for obj in objs:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        n = len(mesh.loop_triangles)
        tris += n
        info = {"tris": n}
        if any(len(p.vertices) != 3 for p in mesh.polygons):
            issues.append(f"{obj.name}: 還有沒三角化的面（先跑 finalize）")
        if any(p.use_smooth for p in mesh.polygons):
            issues.append(f"{obj.name}: 有平滑著色的面")
        mat_names = [m.name if m else None for m in mesh.materials]
        info["materials"] = mat_names
        attr = mesh.attributes.get(CELL_ATTR)
        uv_layer = mesh.uv_layers.active
        if uv_layer is None:
            issues.append(f"{obj.name}: 沒有 UV")
        elif MAT_PALETTE in mat_names or MAT_EMISSIVE in mat_names:
            bad = 0
            for p in mesh.polygons:
                expect = cell_uv(attr.data[p.index].value) if attr else None
                for li in p.loop_indices:
                    if expect is None or (uv_layer.data[li].uv - expect).length > UV_EPS:
                        bad += 1
                        break
            if bad:
                issues.append(f"{obj.name}: {bad} 個面的 UV 不在色格中心")
        used = {v for e in mesh.edges for v in e.vertices}
        loose = len(mesh.vertices) - len(used)
        if loose:
            issues.append(f"{obj.name}: {loose} 個散落頂點")
        edge_faces = {}
        for p in mesh.polygons:
            for ek in p.edge_keys:
                edge_faces[ek] = edge_faces.get(ek, 0) + 1
        closed = all(c == 2 for c in edge_faces.values())
        info["closed"] = closed
        if closed:
            vol = _signed_volume(mesh)
            if vol <= 0:
                issues.append(f"{obj.name}: 法線朝內（體積 {vol:.4f}）")
        s = obj.matrix_world.to_scale()
        r = obj.matrix_world.to_euler()
        if any(abs(c - 1) > 1e-4 for c in s) or any(abs(c) > 1e-4 for c in r):
            issues.append(f"{obj.name}: 旋轉或縮放沒有歸零（匯出前跑 apply_rotation_scale）")
        objects[obj.name] = info
    report = {"tris": tris, "objects": objects}
    if budget:
        lo_b, hi_b = budget
        if not lo_b <= tris <= hi_b:
            issues.append(f"三角面 {tris} 不在預算 {lo_b}–{hi_b}")
    lo, hi = _world_bbox(objs)
    size = hi - lo
    report["bbox_min"] = [round(c, 3) for c in lo]
    report["bbox_size"] = [round(c, 3) for c in size]
    if target_size:
        for axis, got, want in zip("XYZ", size, target_size):
            if want and abs(got - want) / want > size_tol:
                issues.append(f"{axis} 尺寸 {got:.3f} m，目標 {want:.3f} m（誤差 {abs(got - want) / want:.0%}）")
    if pivots:
        by_name = {o.name: o for o in objs}
        for name, want in pivots.items():
            obj = by_name.get(name)
            if obj is None:
                issues.append(f"缺少接口物件 {name}")
                continue
            got = obj.matrix_world.translation
            if (got - Vector(want)).length > pivot_tol:
                issues.append(f"{name} 軸心在 {tuple(round(c, 3) for c in got)}，規格是 {tuple(want)}")
    report["issues"] = issues
    report["ok"] = not issues
    return report

# ---------------------------------------------------------------- 匯出

def export_fbx(objs, relpath):
    """只匯出這些物件到 Assets/Art/Models/<relpath>：套用修改器、三角化、Forward −Z／Up Y、烤進座標軸轉換，
    在 Unity 裡 Rotation 0、Scale 1、正面朝 +Z（Phase 0 用 SM_Test_AxisCube 驗證）。"""
    path = os.path.join(MODELS_DIR, relpath.replace("/", os.sep))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene = objs[0].users_scene[0]
    view_layer = scene.view_layers[0]
    with bpy.context.temp_override(scene=scene, view_layer=view_layer):
        for obj in scene.objects:
            obj.select_set(False, view_layer=view_layer)
        for obj in objs:
            obj.select_set(True, view_layer=view_layer)
        bpy.ops.export_scene.fbx(
            filepath=path,
            use_selection=True,
            object_types={"MESH", "EMPTY"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            axis_forward="-Z",
            axis_up="Y",
            bake_space_transform=True,
            use_mesh_modifiers=True,
            mesh_smooth_type="FACE",
            use_triangles=True,
            use_tspace=False,
            colors_type="NONE",
            use_custom_props=False,
            add_leaf_bones=False,
            bake_anim=False,
            path_mode="STRIP",
            embed_textures=False,
        )
        for obj in objs:
            obj.select_set(False, view_layer=view_layer)
    return path


def save_blend(name, objs=None):
    """把這個資產的 scene（含物件、網格、材質）存成 Art/Blender/blend/<name>.blend，給人工小修。"""
    os.makedirs(BLEND_DIR, exist_ok=True)
    scene = bpy.data.scenes.get(name)
    path = os.path.join(BLEND_DIR, f"{name}.blend")
    bpy.data.libraries.write(path, {scene}, path_remap="RELATIVE_ALL", fake_user=True)
    return path

# ---------------------------------------------------------------- 合成圖

_VIEWS = (  # (標籤, 相機方向：從資產看出去的方向)
    ("front", Vector((0, -1, 0))),
    ("side", Vector((1, 0, 0))),
    ("back", Vector((0, 1, 0))),
    ("three_quarter", Vector((-1, -1, 0.55)).normalized()),
)


def _bar_box(bm, a, b, thickness):
    d = b - a
    length = d.length
    if length < 1e-6:
        return
    verts = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    m = Matrix.Translation((a + b) / 2) @ rot @ Matrix.Diagonal((thickness, thickness, length, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=verts)


def contact_sheet(objs, name, target_bbox=None, scale_ref=True, size=1024):
    """四視角合成圖（左上正面、右上側面〔從 +X 看〕、左下背面、右下 3/4），寫到 Art/Blender/preview/<name>.png。
    旁邊放 2 m 比例尺（紅）；target_bbox=(min, max) 時畫出舊佔位物的包圍盒線框（藍）。"""
    scene = objs[0].users_scene[0]
    helpers = bpy.data.collections.new("_PREVIEW")
    scene.collection.children.link(helpers)
    made = []
    try:
        lo, hi = _world_bbox(objs)
        if target_bbox:
            lo = Vector(map(min, lo, Vector(target_bbox[0])))
            hi = Vector(map(max, hi, Vector(target_bbox[1])))
        extent = hi - lo
        if scale_ref:
            # 放在前右方（+X、−Y）：四個視角都在模型旁邊，不會擋到模型
            bm = bmesh.new()
            margin = max(extent.x, extent.y) * 0.25 + 0.1
            x, y = hi.x + margin, lo.y - margin
            add_box(bm, (0.1, 0.1, 2.0), location=(x, y, 0), base=True)
            mesh = bpy.data.meshes.new("_ScaleRef_2m")
            bm.to_mesh(mesh)
            bm.free()
            mesh.materials.append(special_material("_PreviewRed", (0.9, 0.15, 0.15)))
            ref = bpy.data.objects.new("_ScaleRef_2m", mesh)
            helpers.objects.link(ref)
            made.append(ref)
            hi = Vector((x + 0.05, hi.y, max(hi.z, 2.0)))
            lo = Vector((lo.x, y - 0.05, lo.z))
        if target_bbox:
            a, b = Vector(target_bbox[0]), Vector(target_bbox[1])
            corners = [Vector((x, y, z)) for x in (a.x, b.x) for y in (a.y, b.y) for z in (a.z, b.z)]
            bm = bmesh.new()
            thick = max((b - a).length * 0.004, 0.005)
            for i, c in enumerate(corners):
                for j in range(i + 1, len(corners)):
                    d = corners[j] - c
                    if sum(1 for k in range(3) if abs(d[k]) > 1e-6) == 1:
                        _bar_box(bm, c, corners[j], thick)
            mesh = bpy.data.meshes.new("_TargetBox")
            bm.to_mesh(mesh)
            bm.free()
            mesh.materials.append(special_material("_PreviewBlue", (0.2, 0.45, 1.0)))
            box = bpy.data.objects.new("_TargetBox", mesh)
            helpers.objects.link(box)
            made.append(box)

        center = (lo + hi) / 2
        radius = max((hi - lo).length / 2, 0.5)
        cam_data = bpy.data.cameras.new("_PreviewCam")
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = radius * 2.1
        cam = bpy.data.objects.new("_PreviewCam", cam_data)
        helpers.objects.link(cam)
        made.append(cam)

        render = scene.render
        old = (scene.camera, render.engine, render.resolution_x, render.resolution_y, render.resolution_percentage,
               render.filepath, render.image_settings.file_format, scene.view_settings.view_transform,
               scene.view_settings.look)
        scene.view_settings.view_transform = "Standard"  # 不讓 AgX 把色票洗淡（Unity 端是 Neutral）
        scene.view_settings.look = "None"
        scene.camera = cam
        render.engine = "BLENDER_WORKBENCH"
        half = size // 2
        render.resolution_x = render.resolution_y = half
        render.resolution_percentage = 100
        render.image_settings.file_format = "PNG"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "TEXTURE"
        shading.background_type = "VIEWPORT"
        shading.background_color = (0.16, 0.16, 0.17)
        shading.show_object_outline = False
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        tiles = []
        for label, direction in _VIEWS:
            cam.location = center + direction * radius * 4
            cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
            tmp = os.path.join(PREVIEW_DIR, f"_{name}_{label}.png")
            render.filepath = tmp
            with bpy.context.temp_override(scene=scene):
                bpy.ops.render.render(write_still=True, scene=scene.name)
            img = bpy.data.images.load(tmp)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(half, half, 4)
            tiles.append(px)
            bpy.data.images.remove(img)
            os.remove(tmp)
        # Blender 的像素從下往上：上排是 tiles[0]、tiles[1]
        sheet = np.vstack([np.hstack([tiles[2], tiles[3]]), np.hstack([tiles[0], tiles[1]])])
        out = os.path.join(PREVIEW_DIR, f"{name}.png")
        img = bpy.data.images.new(f"_{name}_sheet", size, size, alpha=False)
        img.pixels.foreach_set(sheet.ravel())
        img.filepath_raw = out
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
        (scene.camera, render.engine, render.resolution_x, render.resolution_y, render.resolution_percentage,
         render.filepath, render.image_settings.file_format) = old[:7]
        scene.view_settings.view_transform, scene.view_settings.look = old[7], old[8]
        return out
    finally:
        for obj in made:
            data = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)
        bpy.data.collections.remove(helpers)
        for mat_name in ("_PreviewRed", "_PreviewBlue"):
            mat = bpy.data.materials.get(mat_name)
            if mat is not None and mat.users == 0:
                bpy.data.materials.remove(mat)

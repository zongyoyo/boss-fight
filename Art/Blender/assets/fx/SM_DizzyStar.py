"""
頭暈星星 SM_DizzyStar（ART_STYLE「共通規則」：貓、守衛、小樹精共用，取代各自的佔位星星）。

五角星：外圈 10 個點（尖、凹交錯），前後各一個往外凸的中心點，像一顆有稜線的星形餅乾，共 20 個三角面。
星面朝 Blender −Y（Unity +Z）、直立，軸心在星星中心；顏色是發光色格 gold_glow（走 M_Palette_Emissive），
正面亮、背面中階，邊緣不另外做面（前後兩錐直接在外圈接起來）。
"""
import importlib
import math
import os
import sys

from mathutils import Vector

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)

NAME = "SM_DizzyStar"
OUTER = 0.15   # 尖端半徑（m）；舊的佔位方塊轉 45° 後約 0.28 m 寬
INNER = 0.065  # 凹處半徑
BULGE = 0.045  # 前後中心點凸出的距離（星星的厚度是兩倍）

col = kit.begin(NAME)
bm = kit.new_bmesh()
rim = []
for i in range(10):
    a = math.pi / 2 + i * math.pi / 5  # 第一個尖朝上
    r = OUTER if i % 2 == 0 else INNER
    rim.append(bm.verts.new((math.cos(a) * r, 0.0, math.sin(a) * r)))
front = bm.verts.new((0.0, -BULGE, 0.0))
back = bm.verts.new((0.0, BULGE, 0.0))
for i in range(10):
    a, b = rim[i], rim[(i + 1) % 10]
    kit._paint_faces(bm, [bm.faces.new((front, b, a))], "gold_glow.l")
    kit._paint_faces(bm, [bm.faces.new((back, a, b))], "gold_glow.m")
obj = kit.make_object(NAME, bm, col)
kit.finalize(obj)

report = kit.check([obj], budget=(20, 20), target_size=(OUTER * 2 * math.cos(math.radians(18)), BULGE * 2, None),
                   pivots={NAME: (0, 0, 0)})
report["sheet"] = kit.contact_sheet([obj], NAME, scale_ref=False)
report["fbx"] = kit.export_fbx([obj], f"FX/{NAME}.fbx")
report["blend"] = kit.save_blend(NAME)
result = report

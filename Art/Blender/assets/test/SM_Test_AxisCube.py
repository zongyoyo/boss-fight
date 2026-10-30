"""
Phase 0 的軸向測試方塊：驗證 Blender → FBX → Unity 的方向、尺寸、平面著色與色格。

1 m 方塊，軸心在底部中心。正面（Blender −Y）紅色並凸出一個楔形鼻子；方塊自己的右手邊（Blender −X）有一塊金色記號。
進 Unity 後應該是 Rotation 0、Scale 1、高 1 m，鼻子朝 +Z，金色記號在 +X（從背後看是右手邊）。
"""
import importlib
import os
import sys

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)

NAME = "SM_Test_AxisCube"

col = kit.begin(NAME)
bm = kit.new_bmesh()
kit.add_box(bm, (1, 1, 1), base=True, cell="paper.m")
# 鼻子：正面中央往 −Y 凸出 0.3 m 的四角錐
kit.add_prism(bm, 4, 0.2, 0.3, location=(0, -0.5, 0.5), rotation=(90, 45, 0), radius_top=0, cell="cloth_red.m")
# 右手邊（−X）的金色記號
kit.add_box(bm, (0.1, 0.4, 0.4), location=(-0.55, 0, 0.5), cell="gold.m")
obj = kit.make_object(NAME, bm, col)
kit.paint(obj, "cloth_red.l", lambda c, n: n.y < -0.9 and c.y > -0.51 and c.x > -0.51)   # 正面
kit.paint(obj, "cloth_blue.m", lambda c, n: n.y > 0.9 and c.x > -0.51)                 # 背面
kit.paint(obj, "grass.l", lambda c, n: n.z > 0.9 and c.z > 0.99)         # 頂面
kit.paint(obj, "iron.d", lambda c, n: n.z < -0.9 and c.z < 0.01)         # 底面
kit.finalize(obj)

report = kit.check([obj], budget=(12, 60), target_size=(1.1, 1.3, 1.0), size_tol=0.06, pivots={NAME: (0, 0, 0)})
report["sheet"] = kit.contact_sheet([obj], NAME, target_bbox=((-0.5, -0.5, 0), (0.5, 0.5, 1)))
report["fbx"] = kit.export_fbx([obj], f"Test/{NAME}.fbx")
report["blend"] = kit.save_blend(NAME)
result = report

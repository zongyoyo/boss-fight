"""依 palette.json 產生 Assets/Art/Textures/T_Palette.png 與 T_Palette_Emissive.png。改了 palette.json 就重跑。"""
import importlib
import os
import sys

_LIB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")
if _LIB not in sys.path:
    sys.path.insert(0, _LIB)
import lowpoly_kit as kit  # noqa: E402

importlib.reload(kit)
result = kit.build_palette()

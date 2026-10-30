# Art/Blender

低面數模型的原始碼。規格、命名、面數預算與工作循環在 `docs/Art/ART_STYLE.md`，角色的部件與掛點在 `docs/Art/CHARACTER_INTERFACE.md`。

| 路徑 | 內容 |
| --- | --- |
| `lib/lowpoly_kit.py` | 共用函式（說明在檔案開頭） |
| `palette/palette.json` | 色格名稱 → 座標與顏色；`build_palette.py` 依它產生 `Assets/Art/Textures/T_Palette*.png` |
| `assets/<類別>/<名稱>.py` | 一個資產一支腳本，跑完回傳檢查結果，並寫出合成圖、FBX、.blend |
| `assets/env/` | 共用環境套件（針葉樹、闊葉樹、石頭、草叢、樹樁；變體寫在各腳本的 `VARIANTS`） |
| `preview/<名稱>.png` | 四視角合成圖：左上正面、右上側面（從 +X 看）、左下背面、右下 3/4；紅柱是 2 m 比例尺，藍框是舊佔位物的包圍盒 |
| `preview/StyleSlice_*.png` | Phase 1 風格切片的遊戲鏡頭截圖（before／after × game〔遊戲視角〕／wide〔低視角〕） |
| `blend/<名稱>.blend` | 腳本跑完的存檔（資產自己的 scene），給人工小修；改了要回寫進腳本，下次重跑才不會被蓋掉 |
| `refs/` | Hunyuan3D 參考網格（風格參考圖在 `Art/參考圖/`，不進版控） |

## 怎麼跑

經 Blender MCP（`execute_blender_code`）跑一支腳本，只把結果摘要帶回對話：

```python
p = r"D:\Data\Zucknight\Unity Project\Boss Fight\Art\Blender\assets\test\SM_Test_AxisCube.py"
g = {"__file__": p, "__name__": "__main__"}
exec(compile(open(p, encoding="utf-8").read(), p, "exec"), g)
result = g["result"]
```

也可以用命令列（不開 Blender 視窗）：

```bash
blender -b -P "Art/Blender/assets/test/SM_Test_AxisCube.py"
```

## 順序

1. 改了 `palette/palette.json` → 跑 `palette/build_palette.py` → Unity 跑 Tools > Art > Setup Art Assets（色票匯入設定、材質）。
2. 跑資產腳本 → 看 `preview/<名稱>.png` 與回傳的 `issues` → FBX 寫進 `Assets/Art/Models/`，Unity 自動套匯入設定、換材質、登錄 ArtLibrary。
3. Unity 跑 Tools > Art > Validate Art。

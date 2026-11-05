"""
休息區（ART_STYLE「休息區（Phase 6）」）建模腳本共用的小工具。

休息區的模型要對準 HubBuilder 的碰撞與站位點，所以尺寸都照 Unity 站點的本地座標想：
Unity 本地 (x 右, y 上, z 前) → Blender (−x, −z, y)（Blender −Y 是正面、模型的右手邊是 Blender −X；ART_STYLE「Unity 端規則」）。
旋轉一律直接寫 Blender 的角度。
"""
import random

import lowpoly_kit as kit


def U(x, y, z):
    """Unity 站點本地座標 → Blender 座標。"""
    return (-x, -z, y)


def box(bm, size, pos, rotation=(0, 0, 0), cell=None, jitter=0.0, rng=None, base=False):
    """Unity 的尺寸（寬 x, 高 y, 深 z）與 Unity 站點本地位置的方塊；base=True 時 pos 是底面中心。
    rotation 仍是 Blender 的角度（繞 Blender Z 就是水平轉向）。"""
    sx, sy, sz = size
    return kit.add_box(bm, (sx, sz, sy), U(*pos), rotation, cell=cell, jitter=jitter, rng=rng, base=base)


def tone(rng, group, weights=(0.25, 0.5, 0.25)):
    """一個顏色組隨機取亮／中／暗一階（木板一片一片的深淺）。"""
    r = rng.random()
    if r < weights[0]:
        return f"{group}.l"
    if r < weights[0] + weights[1]:
        return f"{group}.m"
    return f"{group}.d"


def paint(bm, faces, cell):
    layer = bm.faces.layers.int.get(kit.CELL_ATTR)
    index = kit.cell_index(cell)
    for f in faces:
        f[layer] = index


def segments(lo, hi, holes, rng, split=2.6):
    """[lo, hi] 扣掉 holes（[(a, b)]）剩下的段；長於 split 的段隨機切成兩片（木板接縫）。"""
    pieces = [(lo, hi)]
    for a, b in holes:
        out = []
        for s, e in pieces:
            if b <= s or a >= e:
                out.append((s, e))
                continue
            if a > s:
                out.append((s, a))
            if b < e:
                out.append((b, e))
        pieces = out
    result = []
    for s, e in pieces:
        if e - s < 0.05:
            continue
        if e - s > split:
            cut = s + (e - s) * rng.uniform(0.35, 0.65)
            result += [(s, cut), (cut, e)]
        else:
            result.append((s, e))
    return result


def make(name, bm, col, material=None, origin=(0, 0, 0)):
    obj = kit.make_object(name, bm, col, origin=origin)
    kit.finalize(obj, material=material)
    return obj


def rng_for(seed):
    return random.Random(seed)

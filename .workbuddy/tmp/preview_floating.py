# -*- coding: utf-8 -*-
"""按场景实际坐标渲染首页布局示意图（1080x1920 画布），用于确认新按钮位置"""
from PIL import Image, ImageDraw, ImageFont

W, H = 1080, 1920
S = 0.5                      # 先按 540x960 画，再放大 2 倍
img = Image.new("RGB", (int(W * S), int(H * S)), (247, 236, 219))
d = ImageDraw.Draw(img)


def R(x0, y0, x1, y1, fill=None, outline=(165, 135, 105), w=1, r=8):
    d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=r, fill=fill, outline=outline, width=w)


def T(x, y, s, size=13, fill=(110, 80, 55), anchor="mm", bold=True):
    path = "C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc"
    f = ImageFont.truetype(path, size)
    d.text((x * S, y * S), s, font=f, fill=fill, anchor=anchor)


# 顶部栏
d.rectangle([0, 0, W * S, 189 * S], fill=(232, 208, 178))
R(265, 66, 515, 154, (255, 255, 255), r=18); T(390, 110, "生命", 12)
R(590, 66, 865, 154, (255, 255, 255), r=18); T(727, 110, "金币", 12)
R(911, 38, 1041, 174, (255, 255, 255), r=24); T(976, 106, "设置", 12)
R(51, 36, 189, 175, (255, 255, 255), r=70); T(120, 105, "头像", 12)

# 主体
R(286, 507, 794, 1031, (255, 248, 232), r=16); T(540, 900, "Level 关卡区", 14)
R(157, 310, 923, 550, (255, 186, 84), (200, 130, 40), 2, 30); T(540, 430, "PlayBtn 开始游戏", 16, (120, 60, 10))

# 底栏
d.rectangle([0, 1690 * S, W * S, H * S], fill=(236, 212, 182))
T(540, 1805, "底栏（3 个标签）", 13)

# ---------- 新增的 Floating 列 ----------
def load(p):
    try:
        return Image.open(p).convert("RGBA")
    except Exception:
        return None

base = "C:/Unity/project/Unity2022_CoffeeSortPuzzle/Assets/AdSDK/Resources/"
bg = load(base + "ui_button_blue.png")
coin = load(base + "ui_coin.png")
side = load(base + "侧边栏 (2).png")

BTNS = [("jiliBtn", "奖励", 205, 329, coin, 62, 62),
        ("cebianBtn", "侧边栏", 351, 475, side, 50, 54)]

for name, text, y0, y1, icon, iw, ih in BTNS:
    x0, x1 = 12, 136
    if bg is not None:
        tile = bg.resize((int((x1 - x0) * S), int((y1 - y0) * S)), Image.LANCZOS)
        img.paste(tile, (int(x0 * S), int(y0 * S)), tile)
        R(x0, y0, x1, y1, None, (40, 110, 180), 2, 10)
    else:
        R(x0, y0, x1, y1, (70, 150, 230), (30, 90, 160), 2, 10)
    if icon is not None:
        ic = icon.resize((int(iw * S), int(ih * S)), Image.LANCZOS)
        img.paste(ic, (int(((x0 + x1) / 2 - iw / 2) * S), int(((y0 + y1) / 2 - ih / 2 - 10) * S)), ic)
    T((x0 + x1) / 2, y1 - 28, text, 11, (255, 255, 255))

# 参考线
d.line([157 * S, 310 * S, 157 * S, 550 * S], fill=(210, 60, 60), width=1)
T(170, 300, "PlayBtn 左边界 x=157", 10, (200, 40, 40), "lm", False)
d.line([136 * S, 340 * S, 157 * S, 340 * S], fill=(40, 150, 90), width=2)
T(140, 330, "21px 间距", 9, (30, 130, 80), "lm", False)
d.line([0, 205 * S, 30 * S, 205 * S], fill=(40, 150, 90), width=2)
T(4, 196, "Floating 顶 205", 10, (30, 130, 80), "lm", False)
T(74, 180, "新增列 124 宽", 10, (30, 130, 80), "mm", False)

out = r"C:/Unity/project/Unity2022_CoffeeSortPuzzle/.workbuddy/tmp/home_floating_preview.png"
img.resize((int(W * S * 1.6), int(H * S * 1.6)), Image.LANCZOS).save(out)
print("OK", out)

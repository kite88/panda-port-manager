# -*- coding: utf-8 -*-
"""渲染 Panda Port Manager 应用图标。

与 images/logo.svg 同源设计（极简熊猫 + RJ45 端口耳 + LED 状态灯眼）。
输出：
  images/logo.png   512x512
  images/logo.ico   16 / 24 / 32 / 48 / 64 / 128 / 256 多尺寸

用法：python tools/draw_logo.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT_PNG = ROOT / "images" / "logo.png"
OUT_ICO = ROOT / "images" / "logo.ico"

BASE = 512
S = 4  # 超采样倍数，缩小后消除锯齿
W = BASE * S

INK = (0x14, 0x14, 0x1F)        # 熊猫黑
LED = (0x2E, 0xE8, 0x8F)        # LED 绿
WAVE = (0x8F, 0xE8, 0xD8)       # 信号波纹青


def background() -> Image.Image:
    """对角渐变（左上 #16406E -> 右下 #0C7C7B）+ 圆角蒙版。"""
    small = Image.new("RGB", (65, 65))
    px = small.load()
    c0 = (0x16, 0x40, 0x6E)
    c1 = (0x0C, 0x7C, 0x7B)
    for y in range(65):
        for x in range(65):
            t = (x + y) / 128
            px[x, y] = tuple(round(c0[i] + (c1[i] - c0[i]) * t) for i in range(3))
    img = small.resize((W, W), Image.BICUBIC).convert("RGBA")
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, W - 1, W - 1], radius=112 * S, fill=255
    )
    img.putalpha(mask)
    return img


def quad(p0, c, p1, steps=24):
    """采样二次贝塞尔曲线点列。"""
    pts = []
    for i in range(steps + 1):
        t = i / steps
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t ** 2 * p1[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t ** 2 * p1[1]
        pts.append((x, y))
    return pts


def waves(img: Image.Image) -> None:
    layer = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for r, alpha in ((84, 115), (108, 56)):
        bbox = [(398 - r) * S, (388 - r) * S, (398 + r) * S, (388 + r) * S]
        d.arc(bbox, -30, 60, fill=WAVE + (alpha,), width=9 * S)
    img.alpha_composite(layer)


def ears(d: ImageDraw.ImageDraw) -> None:
    for cx in (148, 364):
        d.rounded_rectangle(
            [(cx - 32) * S, 130 * S, (cx + 32) * S, 206 * S],
            radius=14 * S, fill=INK,
        )
        d.rounded_rectangle(
            [(cx - 15) * S, 148 * S, (cx + 15) * S, 188 * S],
            radius=7 * S, fill=(0x2B, 0x2B, 0x3D),
        )
        d.rounded_rectangle(
            [(cx - 8) * S, 155 * S, (cx + 8) * S, 161 * S],
            radius=3 * S, fill=(0xE8, 0xB8, 0x4B),
        )


def led_eye(img: Image.Image) -> None:
    layer = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy = 304 * S, 296 * S
    for r, alpha in ((48, 26), (34, 44), (22, 70)):
        d.ellipse(
            [cx - r * S, cy - r * S, cx + r * S, cy + r * S],
            fill=LED + (alpha,),
        )
    d.ellipse([cx - 15 * S, cy - 15 * S, cx + 15 * S, cy + 15 * S], fill=LED + (255,))
    d.ellipse([cx - 6 * S, cy - 6 * S, cx + 6 * S, cy + 6 * S], fill=(0xB4, 0xFF, 0xD8, 255))
    img.alpha_composite(layer)


def mouth(d: ImageDraw.ImageDraw) -> None:
    d.line([256 * S, 348 * S, 256 * S, 360 * S], fill=INK, width=7 * S)
    d.line(
        [(x * S, y * S) for x, y in quad((256, 360), (240, 376), (222, 366))],
        fill=INK, width=7 * S, joint="curve",
    )
    d.line(
        [(x * S, y * S) for x, y in quad((256, 360), (272, 376), (290, 366))],
        fill=INK, width=7 * S, joint="curve",
    )


def main() -> None:
    img = background()
    waves(img)
    d = ImageDraw.Draw(img)

    ears(d)
    d.ellipse([(256 - 128) * S, (312 - 112) * S, (256 + 128) * S, (312 + 112) * S], fill=(255, 255, 255, 255))
    d.ellipse([(208 - 17) * S, (296 - 17) * S, (208 + 17) * S, (296 + 17) * S], fill=INK)
    led_eye(img)
    d = ImageDraw.Draw(img)
    d.ellipse([(256 - 15) * S, (338 - 10) * S, (256 + 15) * S, (338 + 10) * S], fill=INK)
    mouth(d)

    logo = img.resize((BASE, BASE), Image.LANCZOS)
    OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
    logo.save(OUT_PNG)
    logo.save(OUT_ICO, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print(f"OK {OUT_PNG}")
    print(f"OK {OUT_ICO}")


if __name__ == "__main__":
    main()

# -*- coding: utf-8 -*-
# diode.ico 生成器：纯 Python（无第三方依赖）
# 画一个"二极管符号 + 琥珀辉光"的应用图标，输出多尺寸 ICO 和 PNG 预览
import math, struct, zlib, os

W = 1024  # 主画布

# ---------- 几何 SDF（0..1 空间，y 向下） ----------
def sd_round_rect(px, py, cx, cy, hx, hy, r):
    qx = abs(px - cx) - (hx - r)
    qy = abs(py - cy) - (hy - r)
    ox = max(qx, 0.0); oy = max(qy, 0.0)
    return math.hypot(ox, oy) + min(max(qx, qy), 0.0) - r

def sd_box(px, py, x0, y0, x1, y1):
    cx = (x0 + x1) * 0.5; cy = (y0 + y1) * 0.5
    hx = (x1 - x0) * 0.5; hy = (y1 - y0) * 0.5
    qx = abs(px - cx) - hx; qy = abs(py - cy) - hy
    ox = max(qx, 0.0); oy = max(qy, 0.0)
    return math.hypot(ox, oy) + min(max(qx, qy), 0.0)

def sd_triangle(px, py):
    # 凸多边形：三条边的有向距离取 max（外侧为正）
    # 顶点 A(0.28,0.30) B(0.28,0.70) C(0.58,0.50)，内部点 (0.30,0.50)
    A = (0.28, 0.30); B = (0.28, 0.70); C = (0.58, 0.50)
    inside = (0.30, 0.50)
    d = -1e9
    pts = [A, B, C]
    for i in range(3):
        v0 = pts[i]; v1 = pts[(i + 1) % 3]
        ex = v1[0] - v0[0]; ey = v1[1] - v0[1]
        ln = math.hypot(ex, ey)
        nx = ey / ln; ny = -ex / ln
        # 保证法线朝外（对内部点距离为负）
        if (inside[0] - v0[0]) * nx + (inside[1] - v0[1]) * ny > 0:
            nx = -nx; ny = -ny
        d = max(d, (px - v0[0]) * nx + (py - v0[1]) * ny)
    return d

def sd_sym(px, py):
    return min(
        sd_triangle(px, py),
        sd_box(px, py, 0.615, 0.295, 0.685, 0.705),   # 竖条
        sd_box(px, py, 0.135, 0.482, 0.280, 0.518),   # 左引线
        sd_box(px, py, 0.685, 0.482, 0.855, 0.518),   # 右引线
    )

# ---------- 渲染主画布 ----------
img = [None] * (W * W)
step = 1.0 / W
AA = 1.6 / W
SIG = 0.075
for j in range(W):
    py = (j + 0.5) * step
    t = j / (W - 1.0)                       # 渐变参数
    br0 = 36 + (22 - 36) * t                # R
    bg0 = 41 + (26 - 41) * t                # G
    bb0 = 52 + (33 - 52) * t                # B
    row = j * W
    for i in range(W):
        px = (i + 0.5) * step
        d_sym = sd_sym(px, py)
        # 背景圆角矩形裁剪
        d_bg = sd_round_rect(px, py, 0.5, 0.5, 0.478, 0.478, 0.20)
        if d_bg > 0.0015:
            img[row + i] = (0, 0, 0, 0)
            continue
        # 边缘内侧高光/暗部
        edge = 0.012 - d_bg
        r = br0; g0 = bg0; b = bb0
        if edge > 0:
            k = min(edge / 0.012, 1.0)
            r += 14 * k; g0 += 16 * k; b += 22 * k
        # 琥珀辉光
        gl = math.exp(-(d_sym * d_sym) / (SIG * SIG)) if d_sym > 0 else 1.0
        r += 255 * gl * 0.42; g0 += 165 * gl * 0.42; b += 30 * gl * 0.42
        # 二极管符号
        if d_sym < AA:
            cov = max(0.0, min(1.0, (AA - d_sym) / (2 * AA) + 0.5))
            r = r * (1 - cov) + 255 * cov
            g0 = g0 * (1 - cov) + 182 * cov
            b = b * (1 - cov) + 46 * cov
        a = 255
        img[row + i] = (int(max(0, min(255, r))), int(max(0, min(255, g0))), int(max(0, min(255, b))), a)

# ---------- 盒式缩放 ----------
def resize(src, sw, sh, dw, dh):
    out = [0] * (dw * dh)
    for j in range(dh):
        y0 = j * sh / dh; y1 = (j + 1) * sh / dh
        for i in range(dw):
            x0 = i * sw / dw; x1 = (i + 1) * sw / dw
            n = 0; rs = gs = bs = 0
            fy = int(y0)
            while fy < y1:
                fx = int(x0)
                while fx < x1:
                    c = src[fy * sw + fx]
                    if c[3] > 0:
                        rs += c[0]; gs += c[1]; bs += c[2]
                    n += 1
                    fx += 1
                fy += 1
            if n:
                out[j * dw + i] = (rs // n, gs // n, bs // n, 255)
    return out

def write_png(path, pix, w, h):
    raw = bytearray()
    for j in range(h):
        raw.append(0)
        for i in range(w):
            r, g, b, a = pix[j * w + i]
            raw += bytes((r, g, b, a))
    def chunk(tag, data):
        c = struct.pack('>I', len(data)) + tag + data
        return c + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)

def to_dib(pix, w, h):
    # BITMAPINFOHEADER + 自底向上 BGRA + AND 掩码（全 0）
    header = struct.pack('<IiiHHIIiiII', 40, w, h * 2, 1, 32, 0,
                         w * h * 4 + ((w + 31) // 32) * 4 * h, 0, 0, 0, 0)
    body = bytearray()
    for j in range(h - 1, -1, -1):
        for i in range(w):
            r, g, b, a = pix[j * w + i]
            body += bytes((b, g, r, a))
    body += bytes(((w + 31) // 32) * 4 * h)  # AND mask
    return header + bytes(body)

sizes = [16, 24, 32, 48, 64, 128, 256]
entries = []
for s in sizes:
    pix = resize(img, W, W, s, s)
    entries.append((s, to_dib(pix, s, s)))

# ICO 容器
ico_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'diode.ico')
header = struct.pack('<HHH', 0, 1, len(entries))
datas = []
off = 6 + 16 * len(entries)
blob = bytearray(header)
for s, data in entries:
    blob += struct.pack('<BBBBHHII', s if s < 256 else 0, s if s < 256 else 0, 0, 0, 1, 32, len(data), off)
    off += len(data)
    datas.append(data)
for d in datas:
    blob += d
open(ico_path, 'wb').write(bytes(blob))

# 预览图
prev256 = resize(img, W, W, 256, 256)
write_png(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'diode_icon_preview.png'), prev256, 256, 256)

print('ico:', ico_path, os.path.getsize(ico_path), 'bytes,', len(entries), 'sizes')

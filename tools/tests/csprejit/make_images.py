import struct
import sys
import zlib
from pathlib import Path

ADAM7 = [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)]
CHANNELS = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}


def chunk(kind, data):
    body = kind + data
    return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)


def pack_row(samples, depth):
    if depth == 8:
        return bytes(samples)
    if depth == 16:
        return b"".join(struct.pack(">H", s) for s in samples)
    out, acc, bits = bytearray(), 0, 0
    for s in samples:
        acc = (acc << depth) | s
        bits += depth
        if bits == 8:
            out.append(acc)
            acc, bits = 0, 0
    if bits:
        out.append(acc << (8 - bits))
    return bytes(out)


def paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    return a if pa <= pb and pa <= pc else b if pb <= pc else c


def filter_row(kind, row, prev, bpp):
    out = bytearray()
    for i, x in enumerate(row):
        left = row[i - bpp] if i >= bpp else 0
        up = prev[i] if prev else 0
        upleft = prev[i - bpp] if prev and i >= bpp else 0
        base = [0, left, up, (left + up) >> 1, paeth(left, up, upleft)][kind]
        out.append((x - base) & 0xFF)
    return bytes([kind]) + bytes(out)


def encode(width, height, color, depth, pixel, palette=None, trns=None, interlace=False):
    channels = CHANNELS[color]
    bpp = max(1, channels * depth // 8)
    passes = ADAM7 if interlace else [(0, 0, 1, 1)]
    raw = bytearray()
    row_number = 0
    for x0, y0, dx, dy in passes:
        xs = list(range(x0, width, dx))
        ys = list(range(y0, height, dy))
        if not xs or not ys:
            continue
        prev = None
        for y in ys:
            samples = []
            for x in xs:
                samples.extend(pixel(x, y))
            row = pack_row(samples, depth)
            raw += filter_row(row_number % 5, row, prev, bpp)
            prev = row
            row_number += 1
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, depth, color, 0, 0, 1 if interlace else 0))
    if palette:
        png += chunk(b"PLTE", b"".join(bytes(c) for c in palette))
    if trns is not None:
        png += chunk(b"tRNS", trns)
    data = zlib.compress(bytes(raw), 9)
    half = len(data) // 2
    png += chunk(b"IDAT", data[:half]) + chunk(b"IDAT", data[half:])
    png += chunk(b"IEND", b"")
    return png


def main(out):
    out.mkdir(parents=True, exist_ok=True)
    palette = [((i * 37) % 256, (i * 91) % 256, (i * 53) % 256) for i in range(16)]
    images = {
        "Rgba8": encode(37, 23, 6, 8, lambda x, y: (x * 7 % 256, y * 11 % 256, (x + y) * 5 % 256, 255 - x * 6)),
        "Rgb8Key": encode(20, 15, 2, 8, lambda x, y: (40, 80, 120) if (x + y) % 4 == 0 else (x * 12, y * 16, 200),
                          trns=struct.pack(">HHH", 40, 80, 120)),
        "Palette": encode(16, 16, 3, 8, lambda x, y: ((x + y) % 16,), palette=palette,
                          trns=bytes((i * 17) % 256 for i in range(16))),
        "Pal2": encode(13, 7, 3, 2, lambda x, y: ((x * 3 + y) % 4,), palette=palette[:4]),
        "Gray4": encode(19, 11, 0, 4, lambda x, y: ((x + y * 2) % 16,)),
        "Gray1": encode(9, 9, 0, 1, lambda x, y: ((x ^ y) & 1,), trns=struct.pack(">H", 0)),
        "GrayAlpha16": encode(10, 6, 4, 16, lambda x, y: (x * 6553, 65535 - y * 9000)),
        "Rgba16": encode(8, 8, 6, 16, lambda x, y: (x * 8000, y * 8000, 30000, 65535 - x * y * 900)),
        "Interlaced": encode(33, 17, 6, 8, lambda x, y: (x * 7 % 256, 255 - y * 15, x * y % 256, 128 + x % 128), interlace=True),
        "InterlacedPal": encode(11, 5, 3, 4, lambda x, y: ((x + y * 3) % 16,), palette=palette, interlace=True),
        "Asym": encode(32, 32, 6, 8, arrow),
    }
    for name, data in images.items():
        (out / f"{name}.png").write_bytes(data)


def arrow(x, y):
    if x < 6:
        return (255, 255, 255, 255)
    if y < 6 and x < 26:
        return (255, 40, 40, 255)
    if 13 <= y < 18 and x < 20:
        return (40, 200, 40, 255)
    return (0, 0, 0, 0)


if __name__ == "__main__":
    main(Path(sys.argv[1]))

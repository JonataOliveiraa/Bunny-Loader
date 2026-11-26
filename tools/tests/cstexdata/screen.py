import re
import struct
import sys
import zlib

WIDTH, HEIGHT, SCALE = 4, 3, 40
TOLERANCE = 12
DRAW = re.compile(r"cstexdata draw (\w+) (\d+),(\d+),(\d+),(\d+) screen (\d+)x(\d+)")


def expected(x, y):
    return (x * 80, y * 124, 248 - x * 80)


def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a PNG"
    pos, idat, width = 8, b"", 0
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            width, height, depth, color = struct.unpack(">IIBB", body[:10])
            assert depth == 8 and color in (2, 6), f"unsupported PNG {depth}/{color}"
            channels = 4 if color == 6 else 3
        elif kind == b"IDAT":
            idat += body
        pos += 12 + length
    raw = zlib.decompress(idat)
    stride = width * channels
    rows, prev = [], bytearray(stride)
    for y in range(height):
        kind = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            left = line[i - channels] if i >= channels else 0
            up = prev[i]
            corner = prev[i - channels] if i >= channels else 0
            if kind == 1:
                line[i] = (line[i] + left) & 255
            elif kind == 2:
                line[i] = (line[i] + up) & 255
            elif kind == 3:
                line[i] = (line[i] + (left + up) // 2) & 255
            elif kind == 4:
                p = left + up - corner
                pa, pb, pc = abs(p - left), abs(p - up), abs(p - corner)
                line[i] = (line[i] + (left if pa <= pb and pa <= pc else up if pb <= pc else corner)) & 255
        rows.append(bytes(line))
        prev = line
    return width, height, channels, rows


def main(shot, log):
    width, height, channels, rows = read_png(shot)
    draws = [m.groups() for m in map(DRAW.search, open(log, encoding="utf-8", errors="replace")) if m]
    if not draws:
        print("no 'cstexdata draw' line in the log")
        return 1

    bad = 0
    for name, ax, ay, aw, ah, sw, sh in draws:
        ax, ay, sw, sh = int(ax), int(ay), int(sw), int(sh)
        fx, fy = width / sw, height / sh
        for y in range(HEIGHT):
            for x in range(WIDTH):
                px = int((ax + x * SCALE + SCALE / 2) * fx)
                py = int((ay + y * SCALE + SCALE / 2) * fy)
                got = tuple(rows[py][px * channels:px * channels + 3])
                want = expected(x, y)
                if any(abs(g - w) > TOLERANCE for g, w in zip(got, want)):
                    bad += 1
                    print(f"{name} cell {x},{y} at {px},{py}: {got}, expected {want}")
        print(f"{name}: checked {WIDTH * HEIGHT} cells")
    print("screen OK" if bad == 0 else f"screen FAIL: {bad} cells differ")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))

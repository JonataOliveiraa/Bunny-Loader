from pathlib import Path
import struct

root = Path(__file__).resolve().parents[3]
image = (root / "refs/libil2cpp.so").read_bytes()
checks = [
    ("throw", 0x11741C4, (0x1E220100, 0x1E200980, 0xAA1303E0, 0xAA1F03E1)),
    ("shoot", 0x117844C, (0x1E220100, 0x1E200980, 0xAA1303E0, 0xAA1F03E1)),
    ("magic", 0x1178CBC, (0x1E220100, 0x1E200980, 0xAA1303E0, 0xAA1F03E1)),
    ("swing", 0x11792EC, (0x1E220100, 0x1E200980, 0xAA1303E0, 0xAA1F03E1)),
    ("swing rectangle", 0x1179428, (0xB9421A68, 0x3100051F, 0x54000081, 0xB9413BE8)),
    ("gun draw", 0x107E520, (0x4B1303E8, 0x710002BF, 0x1E220100, 0x1A95A6A8)),
    ("swing draw", 0x107F258, (0xF9400360, 0xB940E008, 0x35000068, 0x97DA0109)),
    ("throw random", 0x1174564, (0x1E270100, 0xAA1903E0, 0xAA1F03E1, 0x9416062B)),
    ("shoot random", 0x1178708, (0xAA1903E0, 0x4EA91D21, 0xAA1F03E1, 0x9415F5C2)),
    ("magic random", 0x1179038, (0xAA1703E0, 0x4EA81D01, 0xAA1F03E1, 0x9415F376)),
    ("aura light", 0x117A324, (0x4EA91D23, 0x4EAA1D44, 0xAA1F03E0, 0x97FF214E)),
    ("swing stats", 0x11793BC, (0x2A1B03E4, 0x2A1B03E5, 0xAA1F03E6, 0x9404FEFF)),
]
for name, address, expected in checks:
    assert struct.unpack_from("<4I", image, address) == expected, name
    print(f"{name}: ok")
print(f"cstownnpc native END pass={len(checks)} fail=0")

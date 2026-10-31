from pathlib import Path
import re
import struct

root = Path(__file__).resolve().parents[3]
image = (root / "refs/libil2cpp.so").read_bytes()
checks = [
    ("PreKill", 0x12BD4E8, (0xF94002E0, 0xB940E008, 0x35000068, 0x97D10865)),
    ("OnKill", 0x12BD558, (0xAA1303E0, 0xAA1403E1, 0x94000861, 0xAA1F03E0)),
    ("death return", 0x12BD694, (0xA9457BF3, 0xA94453F5, 0xA9435BF7, 0xA94263F9)),
    ("BossLoot", 0x12C2A14, (0xAA1303E0, 0x97FFA92C, 0xF9400328, 0x2D42A269)),
    ("heart roll", 0x12C2A8C, (0xF9400340, 0xB940E008, 0x35000068, 0x97D0F2FC)),
    ("heart loop", 0x12C2ABC, (0x710006FF, 0x5400044B, 0x52AFF018, 0x52B0001A)),
]
for name, address, expected in checks:
    actual = struct.unpack_from("<4I", image, address)
    assert actual == expected, (name, tuple(hex(x) for x in actual))
    print(f"{name}: ok")

native = (root / "app/src/main/cpp/script/dotnet/NativeApi.cpp").read_text(encoding="utf-8")
managed = (root / "app/src/main/csharp/BunnyLoader/Interop/NativeApi.cs").read_text(encoding="utf-8")
body = native.split("struct NativeApi {", 1)[1].split("};", 1)[0]
fields = re.findall(r"\(\*(\w+)\)\([^;]*;", body)
delegates = re.findall(r"public delegate\*[^;]+?\s+(\w+);", managed)
assert len(fields) == len(delegates), (len(fields), len(delegates))
assert [name.lower() for name in fields] == [name.lower() for name in delegates], (fields, delegates)
assert fields[-2:] == ["newParameterObject", "installNpcLootHooks"]
print(f"NativeApi order: ok ({len(fields)} entries)")
print(f"csnpcloot native END pass={len(checks) + 1} fail=0")

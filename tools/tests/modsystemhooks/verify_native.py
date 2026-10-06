import json
import re
import struct
from pathlib import Path

root = Path(__file__).resolve().parents[3]
source = (root / 'app/src/main/cpp/script/api/SystemHooks.cpp').read_text(encoding='utf-8')
methods = {method['Name']: method['Address'] for method in json.loads((root / 'refs/script.json').read_text(encoding='utf-8'))['ScriptMethod']}
entries = re.findall(r'\{"(\w+)", "void (\w+)\([^"\n]*\)", (0x[0-9a-f]+), \{([^}]+)\}', source)
assert len(entries) == 9, entries
with (root / 'refs/libil2cpp.so').open('rb') as binary:
    for name, method, offset, words in entries:
        address = methods['Terraria.Main$$' + method] + int(offset, 16)
        binary.seek(address)
        actual = struct.unpack('<4I', binary.read(16))
        expected = tuple(int(value.strip(), 16) for value in words.split(','))
        assert actual == expected, (name, hex(address), actual, expected)
        print(name, hex(address), 'verified')
print('9 native guards verified')

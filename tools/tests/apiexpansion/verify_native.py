import json
import re
import struct
from pathlib import Path

root = Path(__file__).resolve().parents[3]
source = (root / 'app/src/main/cpp/script/api/ItemDrawHooks.cpp').read_text(encoding='utf-8')
methods = {m['Name']: m['Address'] for m in json.loads((root / 'refs/script.json').read_text(encoding='utf-8'))['ScriptMethod']}
pairs = re.findall(r'\{"(World|Inventory)", "([^"]+)", "([^"]+)", "[^"\n]+",\s*\{(0x[0-9a-f]+), \{([^}]+)\}, \w+\}, \{(0x[0-9a-f]+), \{([^}]+)\}', source)
assert len(pairs) == 2
with (root / 'refs/libil2cpp.so').open('rb') as binary:
    for name, namespace, owner, pre, pre_words, post, post_words in pairs:
        method = 'DrawItem' if name == 'World' else 'DrawItemIcon'
        entry = methods[namespace + '.' + owner + '$$' + method]
        for stage, offset, words in [('Pre', pre, pre_words), ('Post', post, post_words)]:
            address = entry + int(offset, 16)
            binary.seek(address)
            expected = tuple(int(v.strip(), 16) for v in words.split(','))
            assert struct.unpack('<4I', binary.read(16)) == expected, (name, stage, hex(address))
            print(name, stage, hex(address), 'verified')
print('4 native instruction guards verified; runtime register/stack contracts require the device fixture.')

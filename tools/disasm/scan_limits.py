"""Acha `cmp wN, #imm` + b.cond de ordem na libil2cpp, por metodo.
uso: python scan_limits.py 387 388 389
"""
import bisect, pickle, struct, sys, collections

SO = r"C:\Scripts\Bunny Loader\refs\libil2cpp.so"
CACHE = r"C:\Scripts\Bunny Loader\tools\disasm\script.pkl"
methods, meta = pickle.load(open(CACHE, "rb"))
addrs = [a for a, _ in methods]
data = open(SO, "rb").read()
imms = {int(x) for x in sys.argv[1:]}
CONDS = {0x2: "hs", 0x3: "lo", 0x8: "hi", 0x9: "ls", 0xA: "ge", 0xB: "lt", 0xC: "gt", 0xD: "le"}

lo, hi = addrs[0], addrs[-1] + 0x10000
hits = collections.defaultdict(list)
for off in range(lo & ~3, min(hi, len(data) - 8), 4):
    insn = struct.unpack_from("<I", data, off)[0]
    if (insn & 0xFFC0001F) != 0x7100001F:
        continue
    imm = (insn >> 10) & 0xFFF
    if imm not in imms:
        continue
    nxt = struct.unpack_from("<I", data, off + 4)[0]
    if (nxt & 0xFF000010) != 0x54000000:
        continue
    cond = nxt & 0xF
    if cond not in CONDS:
        continue
    i = bisect.bisect_right(addrs, off) - 1
    name = methods[i][1] if i >= 0 else "?"
    hits[name].append(f"{off:x}:#{imm}/{CONDS[cond]}")

for name, h in sorted(hits.items()):
    print(name, " ".join(h))

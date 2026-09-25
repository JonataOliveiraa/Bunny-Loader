"""Metodos com `mov wN, #imm` (MOVZ 32 bits) — onde arrays de tamanho imm nascem.
uso: python scan_movs.py 389
"""
import bisect, pickle, struct, sys, collections

SO = r"C:\Scripts\Bunny Loader\refs\libil2cpp.so"
CACHE = r"C:\Scripts\Bunny Loader\tools\disasm\script.pkl"
methods, meta = pickle.load(open(CACHE, "rb"))
addrs = [a for a, _ in methods]
data = open(SO, "rb").read()
imm = int(sys.argv[1])
hits = collections.Counter()
lo, hi = addrs[0], addrs[-1] + 0x10000
for off in range(lo & ~3, min(hi, len(data) - 4), 4):
    insn = struct.unpack_from("<I", data, off)[0]
    # MOVZ Wd, #imm16, LSL 0: 0x52800000 | imm16<<5 | Rd
    if (insn & 0xFFE00000) == 0x52800000 and ((insn >> 5) & 0xFFFF) == imm:
        i = bisect.bisect_right(addrs, off) - 1
        hits[methods[i][1] if i >= 0 else "?"] += 1
for name, n in sorted(hits.items()):
    print(n, name)

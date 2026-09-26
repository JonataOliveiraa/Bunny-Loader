"""Acha `lsr wB, wA, #1 ; cmp wB, #imm ; b.cond`: o `0 < x < N` com N par, que o
clang compila como (x-1)>>1 <= (N-2)/2. O `itemId < ItemID.Count` (6147) dos
CommonCode.DropItem* e assim (imm 0xc00); a scan_limits.py nao acha essa forma.

uso: python scan_halved.py 0xc00 [0xc01 ...]
"""
import bisect, pickle, struct, sys, collections
SO = r"C:\Scripts\Bunny Loader\refs\libil2cpp.so"
CACHE = r"C:\Scripts\Bunny Loader\tools\disasm\script.pkl"
methods, meta = pickle.load(open(CACHE, "rb"))
addrs = [a for a, _ in methods]
data = open(SO, "rb").read()
want = {int(x, 0) for x in sys.argv[1:]}
lo, hi = addrs[0], addrs[-1] + 0x10000
for off in range(lo & ~3, min(hi, len(data) - 12), 4):
    insn = struct.unpack_from("<I", data, off)[0]
    # LSR (imm) 32 bits = UBFM wd, wn, #1, #31: 0x53017C00 | rn<<5 | rd
    if (insn & 0xFFFFFC00) != 0x53017C00: continue
    rd = insn & 31
    cmp = struct.unpack_from("<I", data, off + 4)[0]
    if (cmp & 0xFFC0001F) != 0x7100001F or ((cmp >> 5) & 31) != rd: continue
    imm = (cmp >> 10) & 0xFFF
    if imm not in want: continue
    br = struct.unpack_from("<I", data, off + 8)[0]
    cond = br & 0xF if (br & 0xFF000010) == 0x54000000 else None
    prev = struct.unpack_from("<I", data, off - 4)[0]
    i = bisect.bisect_right(addrs, off) - 1
    print(hex(off), methods[i][1], 'imm', hex(imm), 'cond', cond, 'prev', hex(prev))

"""Acha `mov wB, #imm` + `cmp wA, wB` + b.cond de ordem: o limite grande demais
para o `cmp #imm12`. Desde a 1.4.5.8 o `0 < itemId < ItemID.Count` dos
CommonCode.DropItem* e assim (`sub wA, wId, #1; mov wB, #(Count-2)`); ver
patchRegisterLimit (app/src/main/cpp/hook/CodePatch.h).

uso: python scan_reglimit.py 6194 6195
"""
import bisect, pickle, struct, sys

SO = r"C:\Scripts\Bunny Loader\refs\libil2cpp.so"
CACHE = r"C:\Scripts\Bunny Loader\tools\disasm\script.pkl"
methods, meta = pickle.load(open(CACHE, "rb"))
addrs = [a for a, _ in methods]
data = open(SO, "rb").read()
imms = {int(x, 0) for x in sys.argv[1:]}
CONDS = {0x0: "eq", 0x1: "ne", 0x2: "hs", 0x3: "lo", 0x8: "hi", 0x9: "ls", 0xA: "ge", 0xB: "lt", 0xC: "gt", 0xD: "le"}

lo, hi = addrs[0], addrs[-1] + 0x10000
for off in range(lo & ~3, min(hi, len(data) - 32), 4):
    insn = struct.unpack_from("<I", data, off)[0]
    # movz wB, #imm16 (sf=0, hw=0)
    if (insn & 0xFFE00000) != 0x52800000 or ((insn >> 5) & 0xFFFF) not in imms:
        continue
    reg = insn & 31
    for k in range(1, 4):
        c = struct.unpack_from("<I", data, off + 4 * k)[0]
        # cmp wA, wB = SUBS WZR, Wa, Wb
        if (c & 0xFFE0FC1F) != 0x6B00001F or ((c >> 16) & 31) != reg:
            continue
        # o desvio: logo depois ou depois de loads/stores (nao mudam as flags)
        cond = "-"
        for m in range(1, 5):
            b = struct.unpack_from("<I", data, off + 4 * (k + m))[0]
            if (b & 0xFF000010) == 0x54000000:
                cond = CONDS.get(b & 0xF, hex(b & 0xF))
                break
            if (b & 0x0A000000) != 0x08000000:
                break
        i = bisect.bisect_right(addrs, off) - 1
        print(f"{off:#x} {methods[i][1] if i >= 0 else '?'} #{(insn >> 5) & 0xFFFF} {cond}")
        break

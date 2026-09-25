"""Como a scan_limits.py, mas aceita tambem `cmp xN` (64 bits) e qualquer b.cond.
Acha o fim de laco que anda pelo array em bytes (Count + 32):
    python scan_loops.py 728 729 730
"""
import pickle, struct, bisect, collections, sys
SO = r"C:\Scripts\Bunny Loader\refs\libil2cpp.so"
methods, meta = pickle.load(open(r"C:\Scripts\Bunny Loader\tools\disasm\script.pkl", "rb"))
addrs = [a for a, _ in methods]
data = open(SO, "rb").read()
imms = {int(x) for x in sys.argv[1:]}
CONDS = {0x0:"eq",0x1:"ne",0x2:"hs",0x3:"lo",0x8:"hi",0x9:"ls",0xA:"ge",0xB:"lt",0xC:"gt",0xD:"le"}
lo, hi = addrs[0], addrs[-1] + 0x10000
hits = collections.defaultdict(list)
for off in range(lo & ~3, min(hi, len(data) - 8), 4):
    insn = struct.unpack_from("<I", data, off)[0]
    m = insn & 0xFFC0001F
    if m not in (0x7100001F, 0xF100001F): continue
    imm = (insn >> 10) & 0xFFF
    if imm not in imms: continue
    nxt = struct.unpack_from("<I", data, off + 4)[0]
    if (nxt & 0xFF000010) != 0x54000000: continue
    cond = nxt & 0xF
    if cond not in CONDS: continue
    i = bisect.bisect_right(addrs, off) - 1
    hits[methods[i][1]].append(f"{off:x}:{'x' if m==0xF100001F else 'w'}#{imm}/{CONDS[cond]}")
for k in sorted(hits): print(k, " ".join(hits[k]))

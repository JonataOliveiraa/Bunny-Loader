"""Acha quem chama (BL/B) um endereco. uso: python xref.py 0x93409c [0x...]"""
import sys, struct, pickle, bisect, numpy as np, os
b = open(r"C:\Scripts\Bunny Loader\refs\libil2cpp.so", "rb").read()
TEXT_END = 0x2eade74
w = np.frombuffer(b[:TEXT_END - TEXT_END % 4], dtype="<u4")
pcs = np.arange(len(w), dtype=np.int64) * 4
methods, meta = pickle.load(open(os.path.join(os.path.dirname(__file__), "script.pkl"), "rb"))
addrs = [a for a, _ in methods]
for t in sys.argv[1:]:
    t = int(t, 16)
    isbl = (w & 0xFC000000) == 0x94000000
    isb = (w & 0xFC000000) == 0x14000000
    imm = (w & 0x03FFFFFF).astype(np.int64)
    imm = np.where(imm & 0x02000000, imm - 0x04000000, imm)
    dest = pcs + imm * 4
    hits = pcs[(isbl | isb) & (dest == t)]
    print(f"== {t:#x}: {len(hits)} refs")
    for h in hits:
        i = bisect.bisect_right(addrs, int(h)) - 1
        print(f"  {int(h):#x}  {methods[i][1]}")

"""Sobe a cadeia de chamadas no runtime: funcao que contem o endereco, quem a chama."""
import sys, bisect, re, numpy as np
b = open(r"C:\Scripts\Bunny Loader\refs\libil2cpp.so", "rb").read()
HERE = __import__("os").path.dirname(__file__)
starts = sorted(set(int(x, 16) for x in open(__import__("os").path.join(HERE, "fstarts.txt"))))
syms = {}
for l in open(__import__("os").path.join(HERE, "dynsym.txt")):
    p = l.split()
    if len(p) == 3: syms[int(p[0], 16)] = p[2]
w = np.frombuffer(b[:0x2eade74 - 0x2eade74 % 4], dtype="<u4")
pcs = np.arange(len(w), dtype=np.int64) * 4
imm = (w & 0x03FFFFFF).astype(np.int64); imm = np.where(imm & 0x02000000, imm - 0x04000000, imm)
dest = pcs + imm * 4
isb = ((w & 0xFC000000) == 0x94000000) | ((w & 0xFC000000) == 0x14000000)
def fstart(a):
    return starts[bisect.bisect_right(starts, a) - 1]
def name(f):
    return syms.get(f, "")
def callers(f):
    return sorted(set(fstart(int(h)) for h in pcs[isb & (dest == f)]))
seen = set()
def walk(a, depth, maxd):
    f = fstart(a)
    print("  " * depth + f"{a:#x} in fn {f:#x} {name(f)}")
    if depth >= maxd or f in seen: return
    seen.add(f)
    cs = callers(f)
    if len(cs) > 6: print("  " * (depth + 1) + f"... {len(cs)} callers"); cs = cs[:6]
    for c in cs: walk(c, depth + 1, maxd)
walk(int(sys.argv[1], 16), 0, int(sys.argv[2]) if len(sys.argv) > 2 else 6)

"""Acha STR (32 bits, float ou int) com um offset imediato. uso: python strscan.py 0x4bc"""
import sys, pickle, bisect, numpy as np, os
b = open(r"C:\Scripts\Bunny Loader\refs\libil2cpp.so", "rb").read()
w = np.frombuffer(b[:0x2eade74 - 0x2eade74 % 4], dtype="<u4")
methods, meta = pickle.load(open(os.path.join(os.path.dirname(__file__), "script.pkl"), "rb"))
addrs = [a for a, _ in methods]
off = int(sys.argv[1], 16)
imm = (off // 4) << 10
hits = np.nonzero(((w & 0xFFFFFC00) == (0xBD000000 | imm)) | ((w & 0xFFFFFC00) == (0xB9000000 | imm)))[0] * 4
from collections import Counter
c = Counter()
for h in hits:
    i = bisect.bisect_right(addrs, int(h)) - 1
    c[methods[i][1]] += 1
for n, k in c.most_common(60):
    print(k, n)

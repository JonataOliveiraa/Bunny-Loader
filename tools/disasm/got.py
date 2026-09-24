"""Gera got.pkl: endereco da GOT -> alvo (relocacoes R_AARCH64_RELATIVE do .rela.dyn).

Os `ldr` de metadado (Classe_TypeInfo, Method$...) passam pela GOT; sem isto o
da.py nao consegue dizer o que cada um carrega.
"""
import os, pickle, struct
HERE = os.path.dirname(__file__)
b = open(os.path.join(HERE, "..", "..", "refs", "libil2cpp.so"), "rb").read()
shoff = struct.unpack_from("<Q", b, 0x28)[0]
shentsize, shnum, shstrndx = struct.unpack_from("<HHH", b, 0x3a)
secs = [struct.unpack_from("<IIQQQQ", b, shoff + i * shentsize) for i in range(shnum)]
stro = secs[shstrndx][4]
name = lambda n: b[stro + n:b.index(b"\0", stro + n)].decode()
got = {}
for n, typ, flags, addr, off, size in secs:
    if name(n) == ".rela.dyn":
        for j in range(0, size, 24):
            r_off, r_info, r_add = struct.unpack_from("<QQq", b, off + j)
            if r_info & 0xffffffff == 1027:   # R_AARCH64_RELATIVE
                got[r_off] = r_add
pickle.dump(got, open(os.path.join(HERE, "got.pkl"), "wb"))
print(len(got), "relocacoes")

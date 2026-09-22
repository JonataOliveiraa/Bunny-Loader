#!/usr/bin/env python3
"""Adiciona um DT_NEEDED a uma .so ELF64 SEM LIEF e SEM resize.

Estrategia (portavel p/ Kotlin, uso on-device):
 - a .dynamic costuma ter capacidade sobrando (p_filesz > entradas usadas);
 - grava a string (ex.: "libbunny.so\0") na folga zerada da .dynamic (regiao
   ja mapeada por um PT_LOAD legivel);
 - troca o DT_NULL terminador por DT_NEEDED(offset=str_vaddr-strtab_vaddr); a
   folga seguinte (zerada) vira o novo DT_NULL.
O linker le strtab+offset direto, entao a string nao precisa estar dentro da
.dynstr declarada.

Uso: python tools/elfpatch.py <in.so> <out.so> <libname.so>
"""
import struct, sys

DT_NULL, DT_NEEDED, DT_STRTAB = 0, 1, 5
PT_LOAD, PT_DYNAMIC = 1, 2

def add_needed(data: bytearray, libname: str) -> bytearray:
    if data[:4] != b"\x7fELF" or data[4] != 2:
        raise SystemExit("nao e ELF64")
    e_phoff = struct.unpack_from("<Q", data, 0x20)[0]
    e_phentsize, e_phnum = struct.unpack_from("<HH", data, 0x36)

    dyn_off = dyn_vaddr = dyn_filesz = 0
    loads = []
    for i in range(e_phnum):
        o = e_phoff + i * e_phentsize
        p_type, p_flags = struct.unpack_from("<II", data, o)
        p_offset, p_vaddr = struct.unpack_from("<QQ", data, o + 8)
        p_filesz = struct.unpack_from("<Q", data, o + 0x20)[0]
        if p_type == PT_DYNAMIC:
            dyn_off, dyn_vaddr, dyn_filesz = p_offset, p_vaddr, p_filesz
        if p_type == PT_LOAD:
            loads.append((p_offset, p_vaddr, p_filesz))
    if not dyn_off:
        raise SystemExit("sem PT_DYNAMIC")

    # entradas .dynamic + DT_STRTAB
    strtab_vaddr = None
    o = dyn_off
    null_off = None
    n = 0
    while True:
        tag, val = struct.unpack_from("<qQ", data, o)
        if tag == DT_STRTAB:
            strtab_vaddr = val
        if tag == DT_NULL:
            null_off = o
            break
        o += 16
        n += 1
    if strtab_vaddr is None:
        raise SystemExit("sem DT_STRTAB")

    end_entries = null_off + 16          # fim das entradas usadas (inclui DT_NULL)
    cap = dyn_off + dyn_filesz           # limite da folga na .dynamic
    need = 16 + len(libname) + 1         # +1 entrada DT_NULL nova + string
    if end_entries + need > cap:
        raise SystemExit(f"sem folga na .dynamic ({cap-end_entries}B, precisa {need}B)")

    # string no FIM da folga
    str_off = cap - (len(libname) + 1)
    data[str_off:str_off + len(libname)] = libname.encode()
    data[str_off + len(libname)] = 0
    str_vaddr = dyn_vaddr + (str_off - dyn_off)

    # DT_NULL atual -> DT_NEEDED; a folga seguinte (zerada) e o novo DT_NULL
    struct.pack_into("<qQ", data, null_off, DT_NEEDED, str_vaddr - strtab_vaddr)
    return data

def main():
    if len(sys.argv) != 4:
        sys.exit("uso: elfpatch.py <in.so> <out.so> <libname.so>")
    src, dst, lib = sys.argv[1:]
    data = bytearray(open(src, "rb").read())
    out = add_needed(data, lib)
    open(dst, "wb").write(out)
    print(f"OK: {lib} adicionado a {dst}")

if __name__ == "__main__":
    main()

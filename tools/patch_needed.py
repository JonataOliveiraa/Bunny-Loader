#!/usr/bin/env python
"""Adiciona uma dependencia DT_NEEDED a uma lib ELF.

Uso: patch_needed.py <entrada.so> <saida.so> <libX.so>

No Bunny Loader (modo repackage), acrescentamos `libbunny.so` como NEEDED da
`libmain.so` do jogo. Assim o linker dinamico carrega a nossa lib — e roda o
constructor dela — quando a Unity faz System.loadLibrary("main"), antes do
il2cpp_init. Nenhum dex e tocado.
"""
import sys
import lief

def main():
    if len(sys.argv) != 4:
        print("uso: patch_needed.py <entrada.so> <saida.so> <libX.so>", file=sys.stderr)
        return 2
    src, dst, needed = sys.argv[1], sys.argv[2], sys.argv[3]

    bin = lief.ELF.parse(src)
    if bin is None:
        print(f"ERRO: nao parseou {src}", file=sys.stderr)
        return 1

    existing = [e.name for e in bin.dynamic_entries
                if e.tag == lief.ELF.DynamicEntry.TAG.NEEDED]
    if needed in existing:
        print(f"  ja tinha NEEDED {needed}, nada a fazer")
    else:
        bin.add_library(needed)  # DT_NEEDED
        print(f"  add NEEDED {needed}")

    bin.write(dst)
    # confere
    out = lief.ELF.parse(dst)
    now = [e.name for e in out.dynamic_entries
           if e.tag == lief.ELF.DynamicEntry.TAG.NEEDED]
    print("  NEEDED final:", ", ".join(now))
    return 0 if needed in now else 1

if __name__ == "__main__":
    sys.exit(main())

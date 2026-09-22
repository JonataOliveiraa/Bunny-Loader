#!/usr/bin/env python3
"""Converte um arquivo binario num header C com um array de bytes + tamanho.

Uso: python tools/bin2header.py <arquivo> <nome_do_simbolo>  > saida.h
"""
import sys

def main():
    if len(sys.argv) != 3:
        sys.exit("uso: bin2header.py <arquivo> <simbolo>")
    path, sym = sys.argv[1], sys.argv[2]
    with open(path, "rb") as f:
        data = f.read()
    out = []
    out.append("#pragma once")
    out.append("// GERADO por tools/bin2header.py — NAO editar a mao.")
    out.append("#include <cstddef>")
    out.append(f"static const unsigned char {sym}[] = {{")
    for i in range(0, len(data), 12):
        chunk = data[i:i + 12]
        out.append("    " + "".join(f"0x{b:02x}, " for b in chunk).rstrip())
    out.append("};")
    out.append(f"static const size_t {sym}_len = {len(data)};")
    out.append("")
    sys.stdout.write("\n".join(out))

if __name__ == "__main__":
    main()

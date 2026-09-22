#!/usr/bin/env python
"""Repackage do Terraria para carregar a libbunny.so (modo repackage, caminho C).

NAO remove o PairIP (ele e necessario para decifrar as strings do jogo). So
adiciona a nossa lib e faz o linker carrega-la, acrescentando um DT_NEEDED
libbunny.so na libmain.so do jogo. Nenhum dex e alterado por padrao.

Passos:
  1. extrai lib/<abi>/libmain.so do APK original
  2. adiciona NEEDED libbunny.so nela (LIEF)
  3. remonta o APK: troca a libmain.so e adiciona libbunny.so + libshadowhook.so
  4. zipalign
  5. assina com uma debug keystore

Uso:
  python tools/repack.py --apk refs/base.apk \
      --lib <libbunny.so> --dep <libshadowhook.so> \
      --out out/terraria-bunny.apk [--abi arm64-v8a]

O APK de saida e uma copia modificada de um jogo pago: e para uso LOCAL na
sua propria copia, nunca para redistribuir.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile

import lief


def patch_needed(src, dst, needed):
    b = lief.ELF.parse(src)
    if b is None:
        raise SystemExit(f"ERRO: nao parseou {src}")
    have = [e.name for e in b.dynamic_entries
            if e.tag == lief.ELF.DynamicEntry.TAG.NEEDED]
    if needed not in have:
        b.add_library(needed)
    b.write(dst)
    out = lief.ELF.parse(dst)
    now = [e.name for e in out.dynamic_entries
           if e.tag == lief.ELF.DynamicEntry.TAG.NEEDED]
    if needed not in now:
        raise SystemExit(f"ERRO: NEEDED {needed} nao aplicado")
    print(f"  libmain.so NEEDED: {', '.join(now)}")


def rebuild_apk(apk, abi, patched_main, extra_libs, out_unsigned):
    main_entry = f"lib/{abi}/libmain.so"
    add = {f"lib/{abi}/{os.path.basename(p)}": p for p in extra_libs}

    with zipfile.ZipFile(apk) as zin, \
         zipfile.ZipFile(out_unsigned, "w") as zout:
        for item in zin.infolist():
            # Descarta assinatura antiga; sera reassinado.
            if item.filename.startswith("META-INF/") and \
               item.filename.rsplit(".", 1)[-1] in ("RSA", "SF", "MF"):
                continue
            data = patched_main if item.filename == main_entry else zin.read(item.filename)
            # Preserva o tipo de compressao original de cada entrada.
            zi = zipfile.ZipInfo(item.filename, date_time=item.date_time)
            zi.compress_type = item.compress_type
            zi.external_attr = item.external_attr
            zout.writestr(zi, data)
        # Acrescenta nossas libs (DEFLATED, como as demais deste APK).
        for arcname, path in add.items():
            with open(path, "rb") as f:
                zi = zipfile.ZipInfo(arcname)
                zi.compress_type = zipfile.ZIP_DEFLATED
                zout.writestr(zi, f.read())
            print(f"  + {arcname}")


def run(cmd):
    print("  $", " ".join(str(c) for c in cmd))
    subprocess.run(cmd, check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apk", required=True)
    ap.add_argument("--lib", required=True, help="libbunny.so (abi correta)")
    ap.add_argument("--dep", required=True, help="libshadowhook.so (abi correta)")
    ap.add_argument("--out", required=True)
    ap.add_argument("--abi", default="arm64-v8a")
    ap.add_argument("--build-tools", default=os.path.expandvars(
        r"$LOCALAPPDATA/Android/Sdk/build-tools/36.1.0"))
    ap.add_argument("--keystore", default="tools/debug.keystore")
    args = ap.parse_args()

    bt = args.build_tools
    zipalign = os.path.join(bt, "zipalign.exe")
    apksigner = os.path.join(bt, "apksigner.bat")

    os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
    tmp = tempfile.mkdtemp(prefix="bunnyrepack_")
    try:
        # 1. extrai libmain.so
        main_arc = f"lib/{args.abi}/libmain.so"
        with zipfile.ZipFile(args.apk) as z:
            raw_main = os.path.join(tmp, "libmain.orig.so")
            with open(raw_main, "wb") as f:
                f.write(z.read(main_arc))

        # 2. patch NEEDED
        patched = os.path.join(tmp, "libmain.patched.so")
        print("patch DT_NEEDED:")
        patch_needed(raw_main, patched, "libbunny.so")
        with open(patched, "rb") as f:
            patched_bytes = f.read()

        # 3. remonta
        print("remontando APK:")
        unsigned = os.path.join(tmp, "unsigned.apk")
        rebuild_apk(args.apk, args.abi, patched_bytes, [args.lib, args.dep], unsigned)

        # 4. zipalign
        print("zipalign:")
        aligned = os.path.join(tmp, "aligned.apk")
        run([zipalign, "-f", "4", unsigned, aligned])

        # 5. keystore + assinatura
        if not os.path.exists(args.keystore):
            print("gerando debug keystore:")
            run(["keytool", "-genkeypair", "-keystore", args.keystore,
                 "-storepass", "android", "-keypass", "android",
                 "-alias", "bunny", "-keyalg", "RSA", "-keysize", "2048",
                 "-validity", "10000", "-dname", "CN=Bunny Loader Dev"])
        print("assinando:")
        run([apksigner, "sign", "--ks", args.keystore,
             "--ks-pass", "pass:android", "--key-pass", "pass:android",
             "--out", args.out, aligned])
        print(f"\nOK -> {args.out}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())

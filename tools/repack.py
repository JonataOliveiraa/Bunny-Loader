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


def _png_size(data):
    # width/height do cabecalho IHDR do PNG (big-endian em [16:24]).
    import struct
    return struct.unpack(">II", data[16:24])


def _remap_icon(filename, orig_data, icon_img):
    """Se `filename` for uma camada do icone adaptativo, devolve o PNG novo
    (background = nosso icone redimensionado; foreground = transparente),
    mantendo as dimensoes originais. Senao, devolve None."""
    from PIL import Image
    base = os.path.basename(filename)
    if "mipmap" not in filename:
        return None
    w, h = _png_size(orig_data)
    if base == "ic_launcher_background.png":
        # Pixel-art: NEAREST pra nao borrar. Cena quadrada -> full-bleed.
        layer = icon_img.convert("RGBA").resize((w, h), Image.NEAREST)
    elif base == "ic_launcher_foreground.png":
        layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))  # transparente
    else:
        return None
    import io
    buf = io.BytesIO()
    layer.save(buf, format="PNG")
    return buf.getvalue()


def _rename_pkg_bytes(data, old_pkg, new_pkg):
    """Troca o nome do pacote em bytes (AXML/arsc). Exige MESMO comprimento pra
    nao recalcular offsets do string pool. Cobre UTF-8 e UTF-16LE (as duas
    codificacoes possiveis no pool). A mesma troca renomeia tambem a authority
    do ContentProvider (que embute o pacote), evitando conflito ao coexistir."""
    for enc in ("utf-8", "utf-16-le"):
        data = data.replace(old_pkg.encode(enc), new_pkg.encode(enc))
    return data


def rebuild_apk(apk, abi, patched_main, extra_libs, out_unsigned,
                icon_path=None, rename=None):
    main_entry = f"lib/{abi}/libmain.so"
    add = {f"lib/{abi}/{os.path.basename(p)}": p for p in extra_libs}
    OLD_PKG = "com.and.games505.TerrariaPaid"
    if rename and len(rename) != len(OLD_PKG):
        raise SystemExit(
            f"ERRO: --rename precisa ter {len(OLD_PKG)} chars (mesmo tamanho de "
            f"{OLD_PKG}); '{rename}' tem {len(rename)}")

    icon_img = None
    if icon_path:
        from PIL import Image
        icon_img = Image.open(icon_path)

    with zipfile.ZipFile(apk) as zin, \
         zipfile.ZipFile(out_unsigned, "w") as zout:
        for item in zin.infolist():
            # Descarta assinatura antiga; sera reassinado.
            if item.filename.startswith("META-INF/") and \
               item.filename.rsplit(".", 1)[-1] in ("RSA", "SF", "MF"):
                continue
            data = patched_main if item.filename == main_entry else zin.read(item.filename)
            # Renomeia o pacote (AndroidManifest + resources.arsc) pra coexistir
            # com o Terraria original em vez de substitui-lo.
            if rename and item.filename in ("AndroidManifest.xml", "resources.arsc"):
                data = _rename_pkg_bytes(data, OLD_PKG, rename)
                print(f"  rename[{item.filename}]: {OLD_PKG} -> {rename}")
            # Troca as camadas do icone, se pedido.
            if icon_img is not None:
                new_icon = _remap_icon(item.filename, data, icon_img)
                if new_icon is not None:
                    data = new_icon
                    print(f"  icone: {item.filename}")
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
    ap.add_argument("--icon", default=None,
                    help="PNG quadrado; vira o icone (camada de fundo adaptativa)")
    ap.add_argument("--rename", default=None,
                    help="novo nome de pacote (MESMO tamanho de com.and.games505."
                         "TerrariaPaid = 29 chars) pra coexistir com o original")
    ap.add_argument("--elfpatch", action="store_true",
                    help="usa o patcher ELF proprio (sem LIEF), p/ validar o "
                         "algoritmo do patch on-device")
    ap.add_argument("--build-tools", default=os.path.expandvars(
        r"$LOCALAPPDATA/Android/Sdk/build-tools/36.1.0"))
    ap.add_argument("--keystore", default="tools/debug.keystore")
    args = ap.parse_args()

    bt = args.build_tools
    zipalign = os.path.join(bt, "zipalign.exe")
    # Chama o apksigner.jar direto: o apksigner.bat, via subprocess com caminho
    # que tem espaco ("Bunny Loader"), e reparseado errado pelo cmd.exe.
    apksigner_jar = os.path.join(bt, "lib", "apksigner.jar")

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
        if args.elfpatch:
            # Patcher proprio, sem LIEF (portavel p/ on-device). Ver elfpatch.py.
            import elfpatch
            data = bytearray(open(raw_main, "rb").read())
            open(patched, "wb").write(elfpatch.add_needed(data, "libbunny.so"))
            print("  (elfpatch: sem LIEF)")
        else:
            patch_needed(raw_main, patched, "libbunny.so")
        with open(patched, "rb") as f:
            patched_bytes = f.read()

        # 3. remonta
        print("remontando APK:")
        unsigned = os.path.join(tmp, "unsigned.apk")
        rebuild_apk(args.apk, args.abi, patched_bytes, [args.lib, args.dep],
                    unsigned, icon_path=args.icon, rename=args.rename)

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
        run(["java", "-jar", apksigner_jar, "sign", "--ks", args.keystore,
             "--ks-pass", "pass:android", "--key-pass", "pass:android",
             "--out", args.out, aligned])
        print(f"\nOK -> {args.out}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())

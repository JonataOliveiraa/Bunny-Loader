#!/usr/bin/env python3
"""Monta o icone e a capa dos mods de samples/ com texturas do jogo.

Uso: python tools/mod-art.py <pasta Images do jogo>
     (ex.: C:/Users/.../1.4.5-Images/Images)

Sai em samples/<Mod>/icon.png (menos onde o mod tem um feito a mao) e
samples/<Mod>/banner.png. Tudo em pixel nativo: quem amplia e o launcher, sem
filtro, entao nada aqui passa por reamostragem suave.

  icone: o slot de inventario azul (Inventory_Back, esticado em 9 fatias para
         64x64) com o item que resume o mod no meio;
  capa:  384x112 — ceu, camadas de cenario recortadas em 1:1, nuvens e o
         item do mod em 2x (mais que isso destoa do cenario, que fica em 1x).
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SAMPLES = os.path.join(ROOT, "samples")

ICON = 64
BANNER_W, BANNER_H = 384, 112

IMAGES = None


def tex(name):
    return Image.open(os.path.join(IMAGES, name)).convert("RGBA")


def local(path):
    return Image.open(os.path.join(SAMPLES, path)).convert("RGBA")


def scaled(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST) if k != 1 else img


def nine_slice(img, w, h, edge=10):
    """Estica um painel sem deformar o canto: quinas fixas, bordas repetidas."""
    out = Image.new("RGBA", (w, h))
    sw, sh = img.size
    cols = [(0, edge, 0, edge), (edge, sw - edge, edge, w - edge), (sw - edge, sw, w - edge, w)]
    rows = [(0, edge, 0, edge), (edge, sh - edge, edge, h - edge), (sh - edge, sh, h - edge, h)]
    for sx0, sx1, dx0, dx1 in cols:
        for sy0, sy1, dy0, dy1 in rows:
            part = img.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST)
            out.paste(part, (dx0, dy0))
    return out


def make_icon(item):
    slot = nine_slice(tex("Inventory_Back.png"), ICON, ICON)
    # Ampliado em 2x quando cabe: um item de 22px sozinho num slot de 64
    # ficaria perdido. O que nao cabe (a Minishark tem 56 de largura) vai 1:1.
    k = 2 if max(item.size) * 2 <= ICON - 8 else 1
    item = scaled(item, k)
    slot.alpha_composite(item, ((ICON - item.width) // 2, (ICON - item.height) // 2))
    return slot


def sky():
    """O ceu do jogo (Background_0), o trecho de baixo, mais claro."""
    col = tex("Background_0.png").crop((0, 700, 1, 1400))
    return col.resize((BANNER_W, BANNER_H), Image.BILINEAR)


def layer(out, name, x, y):
    """Uma camada de cenario em 1:1, a partir da coluna x da textura."""
    img = tex(name)
    out.alpha_composite(img.crop((x, 0, x + BANNER_W, img.height)), (0, y))


def put(out, img, k, cx, cy):
    img = scaled(img, k)
    out.alpha_composite(img, (cx - img.width // 2, cy - img.height // 2))


def shadow(img, alpha=110):
    """Silhueta escura do sprite: da peso ao item grande sobre o cenario."""
    s = Image.new("RGBA", img.size, (0, 0, 0, 0))
    s.putalpha(img.getchannel("A").point(lambda a: alpha if a else 0))
    return s


def hero(out, img, k, cx, cy):
    put(out, shadow(img), k, cx + k, cy + k)
    put(out, img, k, cx, cy)


def banner_minishark():
    out = sky()
    put(out, tex("Cloud_21.png"), 1, 70, 20)
    put(out, tex("Cloud_3.png"), 1, 300, 14)
    layer(out, "Background_7.png", 300, -6)
    layer(out, "Background_92.png", 80, -40)
    # A arma aponta para a direita (e assim que a textura vem), entao ela fica
    # na esquerda e os tiros saem do cano para a direita.
    gun = tex("Item_98.png")
    hero(out, gun, 2, 100, 62)
    muzzle = 100 + gun.width
    bullet = tex("Projectile_14.png").rotate(-90, expand=True)
    for x, y in [(20, 64), (70, 58), (125, 66), (180, 60), (240, 63)]:
        put(out, bullet, 2, muzzle + x, y)
    return out


def banner_dobro():
    out = sky()
    put(out, tex("Cloud_1.png"), 1, 90, 18)
    layer(out, "Background_207.png", 200, -10)
    layer(out, "Background_208.png", 520, 10)
    hero(out, tex("Item_46.png"), 2, 95, 60)
    hero(out, tex("Item_4956.png"), 1, 165, 64)
    hero(out, tex("Item_935.png"), 2, 280, 58)
    return out


def banner_semqueda():
    out = sky()
    for name, x, y in [("Cloud_0.png", 60, 30), ("Cloud_2.png", 200, 70), ("Cloud_4.png", 340, 40),
                       ("Cloud_13.png", 130, 95), ("Cloud_23.png", 300, 92)]:
        put(out, tex(name), 1, x, y)
    layer(out, "Background_7.png", 100, 70)
    hero(out, tex("Item_158.png"), 2, 196, 52)
    return out


def banner_vida():
    out = sky()
    put(out, tex("Cloud_21.png"), 1, 320, 16)
    layer(out, "Background_99.png", 200, -40)
    layer(out, "Background_298.png", 300, -60)
    heart = tex("Heart.png")
    for i in range(5):
        put(out, heart, 1, 30 + i * 26, 20)
    hero(out, tex("Item_29.png"), 2, 290, 62)
    return out


def banner_example():
    out = sky()
    put(out, tex("Cloud_3.png"), 1, 60, 16)
    put(out, tex("Cloud_23.png"), 1, 330, 18)
    layer(out, "Background_7.png", 0, -10)
    layer(out, "Background_55.png", 300, -20)
    tx = "ExampleMod/content/Textures/Items/"
    hero(out, local(tx + "Weapons/Melee/ExampleMeleeWeapon.png"), 2, 100, 60)
    hero(out, local(tx + "ExampleItem.png"), 2, 195, 62)
    hero(out, local(tx + "Weapons/Ranged/ExampleGun.png"), 2, 290, 62)
    return out


MODS = {
    # pasta: (item do icone, capa)
    "HelloMod": ("Item_98.png", banner_minishark),
    "DobroDeDano": ("Item_935.png", banner_dobro),
    "SemQueda": ("Item_158.png", banner_semqueda),
    "VidaCheia": ("Item_29.png", banner_vida),
    "ExampleMod": (None, banner_example),
}


def main():
    global IMAGES
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    IMAGES = sys.argv[1]
    for folder, (icon_item, banner) in MODS.items():
        base = os.path.join(SAMPLES, folder)
        # Sem item = o mod tem icone feito a mao (o ExampleMod), que fica.
        if icon_item:
            make_icon(tex(icon_item)).save(os.path.join(base, "icon.png"), optimize=True)
        banner().convert("RGB").save(os.path.join(base, "banner.png"), optimize=True)
        print(folder, "ok")


if __name__ == "__main__":
    main()

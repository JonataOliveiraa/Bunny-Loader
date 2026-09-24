#!/usr/bin/env python3
"""Recorta as texturas de interface do jogo que o Mod Menu usa.

Uso: python tools/ui-sprites.py <pasta Images do jogo>
     (ex.: C:/Users/.../1.4.5-Images/Images)

Sai em app/src/main/res/drawable-nodpi/ e em tools/unloaded-icon.png (+ o
header C dele). Os PNGs gerados ficam versionados: rodar de novo so e preciso
se mudar o recorte ou a textura do jogo.
"""
import os
import subprocess
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DRAWABLE = os.path.join(ROOT, "app", "src", "main", "res", "drawable-nodpi")

# UI/Creative/Infinite_Powers: tira de quadros 36x36 (34 de icone + 2 de
# respiro), na ordem dos poderes da Jornada.
POWER_FRAME = 36
POWER_FRAMES = {
    "ic_hora_amanhecer": 11,
    "ic_hora_meiodia": 12,
    "ic_hora_anoitecer": 13,
    "ic_hora_meianoite": 14,
    "ic_poder_spawn": 4,      # taxa de inimigos
}

# UI/TexturePackButtons: 2x2 setas de 32x32 (cima, baixo / esquerda, direita).
ARROW = 32
ARROWS = {
    "ic_seta_esq": (0, 1),
    "ic_seta_dir": (1, 1),
}


# Fundo do launcher (ui/Scenery.kt): camadas de cenario, nuvens, sol, lua e
# estrela, copiadas inteiras. O nome diz o papel; o numero e o do jogo.
SCENERY = {
    "bg_mountains": "Background_7.png",
    "bg_hills": "Background_8.png",
    "bg_forest": "Background_92.png",
    "bg_ocean": "Background_209.png",
    "bg_lake": "Background_252.png",
    "bg_snow_mountains": "Background_99.png",
    "bg_snow": "Background_298.png",
    "bg_cloud_0": "Cloud_0.png",
    "bg_cloud_1": "Cloud_1.png",
    "bg_cloud_2": "Cloud_2.png",
    "bg_cloud_3": "Cloud_3.png",
    "bg_cloud_13": "Cloud_13.png",
    "bg_cloud_21": "Cloud_21.png",
    "bg_cloud_23": "Cloud_23.png",
    "bg_sun": "Sun.png",
    "bg_moon": "Moon_0.png",
    "bg_star": "Star_0.png",
}


def crop_tight(img):
    """Corta a transparencia em volta: o respiro do atlas nao e do icone."""
    box = img.getbbox()
    return img.crop(box) if box else img


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    images = sys.argv[1]
    ui = os.path.join(images, "UI")

    powers = Image.open(os.path.join(ui, "Creative", "Infinite_Powers.png")).convert("RGBA")
    for name, i in POWER_FRAMES.items():
        frame = powers.crop((i * POWER_FRAME, 0, (i + 1) * POWER_FRAME, POWER_FRAME))
        crop_tight(frame).save(os.path.join(DRAWABLE, name + ".png"), optimize=True)

    arrows = Image.open(os.path.join(ui, "TexturePackButtons.png")).convert("RGBA")
    for name, (cx, cy) in ARROWS.items():
        cell = arrows.crop((cx * ARROW, cy * ARROW, (cx + 1) * ARROW, (cy + 1) * ARROW))
        crop_tight(cell).save(os.path.join(DRAWABLE, name + ".png"), optimize=True)

    # Dificuldade do mundo (UI/WorldCreation): o cartao troca de icone com ela.
    for mode in ("Normal", "Expert", "Master", "Creative"):
        icon = Image.open(os.path.join(ui, "WorldCreation", "IconDifficulty%s.png" % mode)).convert("RGBA")
        crop_tight(icon).save(os.path.join(DRAWABLE, "ic_dif_%s.png" % mode.lower()), optimize=True)

    for name, file in SCENERY.items():
        Image.open(os.path.join(images, file)).convert("RGBA").save(
            os.path.join(DRAWABLE, name + ".png"), optimize=True)

    # O "?" do item de mod ausente: o cadeado do Bestiario, embutido no nativo.
    locked = Image.open(os.path.join(ui, "Bestiary", "Icon_Locked.png")).convert("RGBA")
    png = os.path.join(ROOT, "tools", "unloaded-icon.png")
    locked.save(png, optimize=True)
    header = subprocess.run(
        [sys.executable, os.path.join(ROOT, "tools", "bin2header.py"), png, "bl_unloaded_icon_png"],
        check=True, capture_output=True, text=True).stdout
    out = os.path.join(ROOT, "app", "src", "main", "cpp", "runtime", "UnloadedIcon.h")
    with open(out, "w", newline="\n") as f:
        f.write(header)
    print(f"{len(POWER_FRAMES) + len(ARROWS) + 4 + len(SCENERY)} drawables, icone ausente {locked.size} -> {out}")


if __name__ == "__main__":
    main()

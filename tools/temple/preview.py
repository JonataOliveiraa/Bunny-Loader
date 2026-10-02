"""Renderiza os tiles da planta testada; não é uma captura do Terraria."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "out"


def font(size, bold=False):
    name = "segoeuib.ttf" if bold else "segoeui.ttf"
    return ImageFont.truetype(str(Path("C:/Windows/Fonts") / name), size)


def main():
    p = json.loads((OUT / "temple-plan.json").read_text(encoding="utf-8"))
    w, h, left = p["width"], p["height"], p["left"]
    tiles = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pixels = tiles.load()
    for y in range(h):
        for x in range(w):
            c = p["grid"][y * w + x]
            if c == 1:
                n = ((x * 17 + y * 23) % 11) - 5
                pixels[x, y] = (145 + n, 77 + n, 35 + n, 255)
            elif c == 2:
                pixels[x, y] = (31, 23, 19, 255)
            elif c == 3:
                pixels[x, y] = (193, 139, 110, 255)
    draw = ImageDraw.Draw(tiles)
    door, altar = p["door"], p["altar"]
    draw.line((door["x"] - left, door["y"] - 1, door["x"] - left, door["y"] + 1), fill="#efb95e")
    draw.rectangle((altar["x"] - left, altar["y"], altar["x"] - left + 2, altar["y"] + 1), fill="#f7d27a")
    for chest in p["chests"]:
        draw.rectangle((chest["x"] - left - 1, chest["floor"] - 2, chest["x"] - left, chest["floor"] - 1), fill="#ffd173")
    for trap in p["traps"]:
        plate, emitter = trap["plate"], trap["emitter"]
        draw.point((plate["x"] - left, plate["y"]), fill="#a5c8e5")
        draw.point((emitter["x"] - left, emitter["y"]), fill="#e88067")
    scale, padding, top = 2, 48, 104
    image = Image.new("RGB", (w * scale + padding * 2, h * scale + top + 120), "#151a1f")
    draw = ImageDraw.Draw(image)
    draw.text((padding, 22), "TEMPLO PIRÂMIDE", font=font(30, True), fill="#f4e7d3")
    draw.text((padding, 65), "Planta do gerador · mundo médio · oito tipos de sala · margem externa reduzida", font=font(20), fill="#9ca8b1")
    image.paste(tiles.resize((w * scale, h * scale), Image.Resampling.NEAREST), (padding, top),
                tiles.resize((w * scale, h * scale), Image.Resampling.NEAREST))
    draw = ImageDraw.Draw(image)
    arena = p["arena"]
    footer = top + h * scale + 18
    draw.text((padding, footer), f'{p["levels"]} andares · {p["roomCount"]} câmaras · {len(p["chests"])} baús · {len(p["traps"])} circuitos de armadilhas', font=font(21), fill="#f4e7d3")
    draw.text((padding, footer + 35), 'Dourado: baús e altar · azul: placas · vermelho: emissores · claro: espinhos', font=font(19), fill="#9ca8b1")
    preview = OUT / "TemploPiramide-previa.png"
    image.save(preview)
    icon = Image.new("RGBA", (256, 256), "#151a1f")
    small = tiles.copy()
    small.thumbnail((244, 200), Image.Resampling.NEAREST)
    icon.alpha_composite(small, ((256 - small.width) // 2, (256 - small.height) // 2))
    icon.convert("RGB").save(ROOT / "samples/TemploPiramide/icon.png")
    print(f"Prévia: {preview}")


if __name__ == "__main__":
    main()

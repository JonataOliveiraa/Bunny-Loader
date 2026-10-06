"""Gera os GIFs do GifDecoderTest e o que cada quadro deve ser.

O esperado sai do próprio Pillow (seek + convert RGBA, que monta os quadros
com transparência e descarte), não de conta feita à mão: o teste compara o
decodificador do app com um leitor de GIF de verdade.

    python app/src/test/resources/gif/make.py

Cada `<nome>.gif` ganha um `<nome>.rgba`: os quadros montados, um depois do
outro, em RGBA; e um `<nome>.txt` com "largura altura quadros" e os tempos.
"""
import os
import random

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))


def save(name, frames, **kw):
    path = os.path.join(HERE, name + ".gif")
    frames[0].save(path, save_all=True, append_images=frames[1:], **kw)
    img = Image.open(path)
    out = bytearray()
    delays = []
    n = 0
    while True:
        out += img.convert("RGBA").tobytes()
        delays.append(img.info.get("duration", 0))
        n += 1
        try:
            img.seek(img.tell() + 1)
        except EOFError:
            break
    open(os.path.join(HERE, name + ".rgba"), "wb").write(bytes(out))
    with open(os.path.join(HERE, name + ".txt"), "w") as f:
        f.write(f"{img.width} {img.height} {n}\n" + " ".join(str(int(d)) for d in delays) + "\n")


def sprite_frames():
    # Um quadrado andando sobre fundo transparente: quadros parciais (o
    # Pillow corta cada um no que mudou) e transparência.
    frames = []
    for i in range(4):
        im = Image.new("RGBA", (40, 30), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rectangle([4 + i * 7, 6, 14 + i * 7, 16], fill=(230, 60, 40, 255))
        d.rectangle([0, 24, 39, 29], fill=(40, 160, 70, 255))
        frames.append(im)
    return frames


def noise(seed, size):
    r = random.Random(seed)
    im = Image.new("RGB", (size, size))
    im.putdata([(r.randrange(256), r.randrange(256), r.randrange(256)) for _ in range(size * size)])
    return im.quantize(256)


if __name__ == "__main__":
    save("sprite", sprite_frames(), duration=[120, 80, 0, 200], loop=0, disposal=2)
    save("restore", sprite_frames(), duration=100, loop=0, disposal=3)
    # Muitas cores: o LZW passa por todos os tamanhos de código e limpa a tabela.
    save("noise", [noise(1, 96), noise(2, 96)], duration=50, loop=0, interlace=False)
    save("interlaced", [noise(3, 37)], interlace=True)

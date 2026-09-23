"""Gera tools/cheatbridge/bunny/CheatData.java a partir de refs/dump.cs.

Por que gerar em vez de digitar: o menu antigo tinha "Life Crystal = 12", que
e Minerio de Ferro. Aqui cada nome e resolvido contra as constantes de NPCID do
dump; o que nao existir aparece no log e fica de fora, em vez de virar um id
errado silencioso.

    python tools/gen-cheatdata.py
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DUMP = os.path.join(ROOT, "refs", "dump.cs")
OUT = os.path.join(ROOT, "tools", "cheatbridge", "bunny", "CheatData.java")


def consts(lines, header):
    """As constantes inteiras de uma classe do dump, por nome."""
    start = next(i for i, l in enumerate(lines) if l.startswith(header))
    out, depth = {}, 0
    for l in lines[start:]:
        depth += l.count("{") - l.count("}")
        if depth <= 0 and out:
            break
        m = re.match(r"\s*public const (?:short|int|ushort|byte|sbyte) (\w+) = (-?\d+);", l)
        if m:
            out[m.group(1)] = int(m.group(2))
    return out


# Itens NAO sao escolhidos aqui: as secoes de item do menu vem do proprio jogo,
# pelos campos melee/ranged/magic/summon... de cada item (runtime::ItemClass).
# A lista a mao tinha 15 magias num jogo com centenas.
#
# NPC continua a mao porque o jogo nao diz o que da para invocar sozinho: o
# Devorador de Mundos e cabeca + corpo + cauda, e so a cabeca traz o resto.
# (rotulo da secao, nome da constante no Java — em ingles, como todo
# identificador —, NPCs)
NPCS = [
    ("Chefes", "BOSSES", [
        ("Rei Slime", "KingSlime"),
        ("Olho de Cthulhu", "EyeofCthulhu"),
        ("Devorador de Mundos", "EaterofWorldsHead"),
        ("Cerebro de Cthulhu", "BrainofCthulhu"),
        ("Abelha Rainha", "QueenBee"),
        ("Skeletron", "SkeletronHead"),
        ("Mural de Carne", "WallofFlesh"),
        ("Rainha Slime", "QueenSlimeBoss"),
        ("O Destruidor", "TheDestroyer"),
        ("Retinazer", "Retinazer"),
        ("Spazmatism", "Spazmatism"),
        ("Skeletron Prime", "SkeletronPrime"),
        ("Plantera", "Plantera"),
        ("Golem", "Golem"),
        ("Duke Fishron", "DukeFishron"),
        ("Imperatriz da Luz", "HallowBoss"),
        ("Cultista", "CultistBoss"),
        ("Senhor da Lua", "MoonLordCore"),
        ("Deerclops", "Deerclops"),
    ]),
    ("Monstros", "MONSTERS", [
        ("Slime Azul", "BlueSlime"),
        ("Olho Demoniaco", "DemonEye"),
        ("Zumbi", "Zombie"),
        ("Esqueleto", "Skeleton"),
        ("Morcego", "CaveBat"),
        ("Batedor Goblin", "GoblinScout"),
        ("Harpia", "Harpy"),
        ("Vespa", "Hornet"),
        ("Mimico", "Mimic"),
        ("Espectro", "Wraith"),
        ("Lobisomem", "Werewolf"),
        ("Ceifador", "Reaper"),
        ("Medusa", "Medusa"),
        ("Golem de Gelo", "IceGolem"),
        ("Disco Marciano", "MartianSaucer"),
        ("Ninfa", "Nymph"),
        ("Pinky", "Pinky"),
        ("Formiga-leao", "Antlion"),
    ]),
    ("Moradores", "TOWN_NPCS", [
        ("Guia", "Guide"),
        ("Comerciante", "Merchant"),
        ("Enfermeira", "Nurse"),
        ("Vendedor de Armas", "ArmsDealer"),
        ("Dryad", "Dryad"),
        ("Demolidor", "Demolitionist"),
        ("Alfaiate", "Clothier"),
        ("Funileiro Goblin", "GoblinTinkerer"),
        ("Mago", "Wizard"),
        ("Mecanica", "Mechanic"),
        ("Steampunker", "Steampunker"),
        ("Vendedor de Tintas", "DyeTrader"),
        ("Garota da Festa", "PartyGirl"),
        ("Ciborgue", "Cyborg"),
        ("Pintor", "Painter"),
        ("Pescador", "Angler"),
        ("Estilista", "Stylist"),
        ("Cobrador", "TaxCollector"),
        ("Trufa", "Truffle"),
        ("Pirata", "Pirate"),
        ("Papai Noel", "SantaClaus"),
        ("Princesa", "Princess"),
        ("Curandeiro", "WitchDoctor"),
        ("Golfista", "Golfer"),
    ]),
]


def jstr(xs):
    return ", ".join('"%s"' % x for x in xs)


def jint(xs):
    return ", ".join(str(x) for x in xs)


def main():
    lines = io.open(DUMP, encoding="utf-8", errors="ignore").read().split("\n")
    npcs = consts(lines, "public class NPCID //")
    print("dump: %d npcs" % len(npcs))

    missing = []
    blocks = []
    for title, const_name, rows in NPCS:
        names, ids = [], []
        for label, key in rows:
            if key not in npcs:
                missing.append("NPCID." + key)
                continue
            names.append(label)
            ids.append(npcs[key])
        v = const_name
        blocks.append(
            "    // %s\n"
            "    static final String[] %s_N = { %s };\n"
            "    static final int[] %s_I = { %s };"
            % (title, v, jstr(names), v, jint(ids)))
        print("  %-18s %d npcs" % (title, len(ids)))

    if missing:
        print("NAO ENCONTRADOS (ficaram de fora): " + ", ".join(missing))

    header = (
        "package bunny;\n\n"
        "// GERADO por tools/gen-cheatdata.py a partir de refs/dump.cs. Nao editar.\n"
        "//\n"
        "// Cada nome foi resolvido contra as constantes de NPCID do jogo. O\n"
        "// menu antigo trazia \"Life Crystal = 12\", que e Minerio de Ferro: id\n"
        "// digitado de memoria nao sobrevive, entao aqui nenhum e.\n"
        "final class CheatData {\n")
    io.open(OUT, "w", encoding="utf-8", newline="\n").write(
        header + "\n\n".join(blocks) + "\n}\n")
    print("escrito " + OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())

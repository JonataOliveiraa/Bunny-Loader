"""Gera tools/cheatbridge/bunny/CheatData.java a partir de refs/dump.cs.

Por que gerar em vez de digitar: o menu antigo tinha "Life Crystal = 12", que
e Minerio de Ferro. Aqui cada nome e resolvido contra as constantes de ItemID e
NPCID do dump; o que nao existir aparece no log e fica de fora, em vez de virar
um id errado silencioso.

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


# (rotulo em pt-BR, nome no ItemID, quantidade)
# Empilhavel vem com pilha cheia: uma bala de mosquete sozinha nao serve pra nada.
ITENS = [
    ("Corpo a corpo", [
        ("Muramasa", "Muramasa", 1),
        ("Lamina da Noite", "NightsEdge", 1),
        ("Excalibur", "Excalibur", 1),
        ("Excalibur Verdadeira", "TrueExcalibur", 1),
        ("Lamina da Noite Verdadeira", "TrueNightsEdge", 1),
        ("Terra Blade", "TerraBlade", 1),
        ("Influx Waver", "InfluxWaver", 1),
        ("Meowmere", "Meowmere", 1),
        ("Star Wrath", "StarWrath", 1),
        ("Zenith", "Zenith", 1),
        ("Erupcao Solar", "SolarEruption", 1),
        ("Daybreak", "DayBreak", 1),
        ("Terrarian", "Terrarian", 1),
        ("Facas Vampiricas", "VampireKnives", 1),
        ("Machadinha Possuida", "PossessedHatchet", 1),
        ("Flagelo do Corruptor", "ScourgeoftheCorruptor", 1),
        ("Flairon", "Flairon", 1),
        ("Martelo do Paladino", "PaladinsHammer", 1),
        ("Arkhalis", "Arkhalis", 1),
        ("Fetid Baghnakhs", "FetidBaghnakhs", 1),
    ]),
    ("A distancia", [
        ("Minishark", "Minishark", 1),
        ("Megashark", "Megashark", 1),
        ("Phoenix Blaster", "PhoenixBlaster", 1),
        ("Daedalus Stormbow", "DaedalusStormbow", 1),
        ("Tsunami", "Tsunami", 1),
        ("Chlorophyte Shotbow", "ChlorophyteShotbow", 1),
        ("Sniper Rifle", "SniperRifle", 1),
        ("Phantasm", "Phantasm", 1),
        ("Vortex Beater", "VortexBeater", 1),
        ("S.D.M.G.", "SDMG", 1),
        ("Xenopopper", "Xenopopper", 1),
        ("Star Cannon", "StarCannon", 1),
        ("Uzi", "Uzi", 1),
        ("Onyx Blaster", "OnyxBlaster", 1),
        ("Pulse Bow", "PulseBow", 1),
        ("Stake Launcher", "StakeLauncher", 1),
        ("Candy Corn Rifle", "CandyCorn", 1),
        ("Elf Melter", "ElfMelter", 1),
    ]),
    ("Magia", [
        ("Space Gun", "SpaceGun", 1),
        ("Foice Demoniaca", "DemonScythe", 1),
        ("Crystal Storm", "CrystalStorm", 1),
        ("Golden Shower", "GoldenShower", 1),
        ("Magnet Sphere", "MagnetSphere", 1),
        ("Rainbow Gun", "RainbowGun", 1),
        ("Last Prism", "LastPrism", 1),
        ("Nebula Blaze", "NebulaBlaze", 1),
        ("Blizzard Staff", "BlizzardStaff", 1),
        ("Lunar Flare", "LunarFlareBook", 1),
        ("Razorblade Typhoon", "RazorbladeTyphoon", 1),
        ("Bat Scepter", "BatScepter", 1),
        ("Inferno Fork", "InfernoFork", 1),
        ("Shadowbeam Staff", "ShadowbeamStaff", 1),
        ("Sky Fracture", "SkyFracture", 1),
    ]),
    ("Invocacao", [
        ("Cajado de Gosma", "SlimeStaff", 1),
        ("Imp Staff", "ImpStaff", 1),
        ("Optic Staff", "OpticStaff", 1),
        ("Pirate Staff", "PirateStaff", 1),
        ("Deadly Sphere Staff", "DeadlySphereStaff", 1),
        ("Xeno Staff", "XenoStaff", 1),
        ("Stardust Dragon", "StardustDragonStaff", 1),
        ("Stardust Cell", "StardustCellStaff", 1),
        ("Moon Lord Turret", "MoonlordTurretStaff", 1),
        ("Rainbow Crystal", "RainbowCrystalStaff", 1),
        ("Spider Staff", "SpiderStaff", 1),
        ("Queen Spider", "QueenSpiderStaff", 1),
        ("Pygmy Staff", "PygmyStaff", 1),
    ]),
    ("Acessorios", [
        ("Nuvem na Garrafa", "CloudinaBottle", 1),
        ("Botas de Hermes", "HermesBoots", 1),
        ("Band of Regeneration", "BandofRegeneration", 1),
        ("Asas Demoniacas", "DemonWings", 1),
        ("Ankh Shield", "AnkhShield", 1),
        ("Frostspark Boots", "FrostsparkBoots", 1),
        ("Terraspark Boots", "TerrasparkBoots", 1),
        ("Celestial Shell", "CelestialShell", 1),
        ("Master Ninja Gear", "MasterNinjaGear", 1),
        ("Asas Solares", "WingsSolar", 1),
        ("Fire Gauntlet", "FireGauntlet", 1),
        ("Destroyer Emblem", "DestroyerEmblem", 1),
        ("Charm of Myths", "CharmofMyths", 1),
        ("Worm Scarf", "WormScarf", 1),
        ("Lava Waders", "LavaWaders", 1),
        ("Rod of Discord", "RodofDiscord", 1),
    ]),
    ("Blocos e moveis", [
        ("Terra", "DirtBlock", 999),
        ("Pedra", "StoneBlock", 999),
        ("Madeira", "Wood", 999),
        ("Vidro", "Glass", 999),
        ("Tocha", "Torch", 999),
        ("Plataforma", "WoodPlatform", 999),
        ("Bau", "Chest", 99),
        ("Bancada", "WorkBench", 99),
        ("Fornalha", "Furnace", 99),
        ("Bigorna de Ferro", "IronAnvil", 99),
        ("Forja do Inferno", "Hellforge", 99),
        ("Serraria", "Sawmill", 99),
        ("Tear", "Loom", 99),
        ("Mesa de Alquimia", "AlchemyTable", 99),
        ("Bigorna de Mithril", "MythrilAnvil", 99),
        ("Forja de Adamantite", "AdamantiteForge", 99),
        ("Bola de Cristal", "CrystalBall", 99),
        ("Bancada Pesada", "HeavyWorkBench", 99),
        ("Cama", "Bed", 99),
        ("Fogueira", "Campfire", 99),
    ]),
    ("Uteis", [
        ("Cristal de Vida", "LifeCrystal", 1),
        ("Cristal de Mana", "ManaCrystal", 1),
        ("Fruta da Vida", "LifeFruit", 1),
        ("Pocao de Gravidade", "GravitationPotion", 5),
        ("Pocao de Mineracao", "MiningPotion", 5),
        ("Pocao de Retorno", "RecallPotion", 20),
        ("Espelho Magico", "MagicMirror", 1),
        ("Celular", "CellPhone", 1),
        ("Bolsa Infinita de Mosquete", "EndlessMusketPouch", 1),
        ("Aljava Infinita", "EndlessQuiver", 1),
        ("Bala de Mosquete", "MusketBall", 999),
        ("Bala de Prata", "SilverBullet", 999),
        ("Bala de Cristal", "CrystalBullet", 999),
        ("Flecha Sagrada", "HolyArrow", 999),
        ("Dinamite", "Dynamite", 50),
    ]),
]

NPCS = [
    ("Chefes", [
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
    ("Monstros", [
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
    ("Moradores", [
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


def var(title):
    return re.sub(r"[^A-Za-z0-9]+", "_", title).upper()


def jstr(xs):
    return ", ".join('"%s"' % x for x in xs)


def jint(xs):
    return ", ".join(str(x) for x in xs)


def main():
    lines = io.open(DUMP, encoding="utf-8", errors="ignore").read().split("\n")
    items = consts(lines, "public class ItemID //")
    npcs = consts(lines, "public class NPCID //")
    print("dump: %d itens, %d npcs" % (len(items), len(npcs)))

    missing = []
    blocks = []
    for title, rows in ITENS:
        names, ids, stacks = [], [], []
        for label, key, stack in rows:
            if key not in items:
                missing.append("ItemID." + key)
                continue
            names.append(label)
            ids.append(items[key])
            stacks.append(stack)
        v = var(title)
        blocks.append(
            "    // %s\n"
            "    static final String[] %s_N = { %s };\n"
            "    static final int[] %s_I = { %s };\n"
            "    static final int[] %s_S = { %s };"
            % (title, v, jstr(names), v, jint(ids), v, jint(stacks)))
        print("  %-18s %d itens" % (title, len(ids)))

    for title, rows in NPCS:
        names, ids = [], []
        for label, key in rows:
            if key not in npcs:
                missing.append("NPCID." + key)
                continue
            names.append(label)
            ids.append(npcs[key])
        v = var(title)
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
        "// Cada nome foi resolvido contra as constantes de ItemID/NPCID do jogo. O\n"
        "// menu antigo trazia \"Life Crystal = 12\", que e Minerio de Ferro: id\n"
        "// digitado de memoria nao sobrevive, entao aqui nenhum e.\n"
        "final class CheatData {\n")
    io.open(OUT, "w", encoding="utf-8", newline="\n").write(
        header + "\n\n".join(blocks) + "\n}\n")
    print("escrito " + OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())

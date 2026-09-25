// Ajudantes globais dos mods: Vector2, MathHelper, Rand, Color e as tabelas
// de numeros que este Terraria NAO tem (ItemRarityID, ProjAIStyleID,
// NPCAIStyleID — no PC elas vem do tModLoader). Os nomes sao os do
// tModLoader, para codigo de la portar mudando pouco.
//
// Embutido na libbunny como o ModClasses.js (CMakeLists: configure_file) e
// avaliado ANTES dele. Nada de arroba neste arquivo.
//
// Vector2 e Color devolvem STRUCTS DO JOGO (o que os metodos do jogo aceitam);
// a conta e feita em JS, que e mais rapido que ir ao jogo para somar dois
// numeros. Entrada aceita struct do jogo ou objeto { X, Y } comum.
(() => {
'use strict';

// As classes sao resolvidas na primeira chamada, nao aqui: este arquivo roda
// antes de o jogo estar pronto para isso.
let xVector2 = null, xColor = null;
const XVector2 = () => xVector2 || (xVector2 = Microsoft.Xna.Framework.Vector2);
const XColor = () => xColor || (xColor = Microsoft.Xna.Framework.Graphics.Color);

// ============================== MathHelper ==============================

const MathHelper = Object.freeze({
    E: Math.E,
    Log10E: Math.LOG10E,
    Log2E: Math.LOG2E,
    Pi: Math.PI,
    PiOver2: Math.PI / 2,
    PiOver4: Math.PI / 4,
    TwoPi: Math.PI * 2,
    ToRadians: (degrees) => degrees * Math.PI / 180,
    ToDegrees: (radians) => radians * 180 / Math.PI,
    Clamp: (value, min, max) => Math.min(Math.max(value, min), max),
    Lerp: (a, b, amount) => a + (b - a) * amount,
    Min: Math.min,
    Max: Math.max,
    Distance: (a, b) => Math.abs(a - b),
    SmoothStep: (a, b, amount) => {
        const t = Math.min(Math.max(amount, 0), 1);
        return a + (b - a) * (t * t * (3 - 2 * t));
    },
    // O angulo equivalente entre -Pi e Pi.
    WrapAngle: (angle) => {
        let a = angle % (Math.PI * 2);
        if (a <= -Math.PI) a += Math.PI * 2;
        else if (a > Math.PI) a -= Math.PI * 2;
        return a;
    },
});

// ================================= Rand =================================

// O gerador do jogo (Main.rand); sem ele (fora do mundo), o do JS.
function gameRand() {
    try { return Terraria.Main.rand || null; } catch (e) { return null; }
}

const Rand = Object.freeze({
    // Next(max) = 0..max-1; Next(min, max) = min..max-1.
    Next(a, b) {
        const r = gameRand();
        if (b === undefined) {
            return r ? r['int Next(int maxValue)'](a | 0) : Math.floor(Math.random() * a);
        }
        return r ? r['int Next(int minValue, int maxValue)'](a | 0, b | 0)
                 : Math.floor(a + Math.random() * (b - a));
    },
    NextInt(min = 0, max = 1) { return Rand.Next(Math.floor(min), Math.floor(max)); },
    // NextFloat() = 0..1; NextFloat(max); NextFloat(min, max).
    NextFloat(a, b) {
        const r = gameRand();
        const u = r ? r['double NextDouble()']() : Math.random();
        if (a === undefined) return u;
        if (b === undefined) return u * a;
        return a + u * (b - a);
    },
    NextBool(oneIn = 2) { return Rand.Next(oneIn) === 0; },
    NextChance(chance = 0.5) { return Rand.NextFloat() < chance; },
    NextSign() { return Rand.NextBool() ? 1 : -1; },
    NextFromList(list) { return list[Rand.Next(list.length)]; },
    // [[valor, peso], ...]
    NextFromListWeighted(list) {
        let total = 0;
        for (const [, w] of list) total += w;
        let r = Rand.NextFloat() * total;
        for (const [v, w] of list) if ((r -= w) <= 0) return v;
        return list[list.length - 1][0];
    },
    // Um ponto uniforme num circulo de raios rx, ry.
    NextVector2Circular(rx, ry = rx) {
        const angle = Rand.NextFloat(Math.PI * 2);
        const d = Math.sqrt(Rand.NextFloat());
        return Vector2.new(Math.cos(angle) * rx * d, Math.sin(angle) * ry * d);
    },
    // Um vetor de comprimento 1, com angulo em [start, start + range).
    NextVector2Unit(start = 0, range = Math.PI * 2) {
        const angle = start + Rand.NextFloat(range);
        return Vector2.new(Math.cos(angle), Math.sin(angle));
    },
    NextVector2Square(min, max) {
        return Vector2.new(Rand.NextFloat(min, max), Rand.NextFloat(min, max));
    },
});

// ================================ Vector2 ================================

const xOf = (v) => (typeof v === 'number' ? v : v.X);
const yOf = (v) => (typeof v === 'number' ? v : v.Y);

const Vector2 = Object.freeze({
    get Type() { return XVector2(); },
    new(x = 0, y = 0) {
        const v = XVector2().new();
        v['void .ctor(float x, float y)'](x, y);
        return v;
    },
    get Zero() { return Vector2.new(0, 0); },
    get One() { return Vector2.new(1, 1); },
    get UnitX() { return Vector2.new(1, 0); },
    get UnitY() { return Vector2.new(0, 1); },

    // O segundo operando pode ser vetor ou numero.
    Add: (a, b) => Vector2.new(a.X + xOf(b), a.Y + yOf(b)),
    Subtract: (a, b) => Vector2.new(a.X - xOf(b), a.Y - yOf(b)),
    Multiply: (a, b) => Vector2.new(a.X * xOf(b), a.Y * yOf(b)),
    Divide: (a, b) => Vector2.new(a.X / xOf(b), a.Y / yOf(b)),
    Negate: (a) => Vector2.new(-a.X, -a.Y),

    Length: (a) => Math.hypot(a.X, a.Y),
    LengthSquared: (a) => a.X * a.X + a.Y * a.Y,
    Distance: (a, b) => Math.hypot(a.X - b.X, a.Y - b.Y),
    DistanceSquared: (a, b) => (a.X - b.X) ** 2 + (a.Y - b.Y) ** 2,
    Dot: (a, b) => a.X * b.X + a.Y * b.Y,
    Lerp: (a, b, t) => Vector2.new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t),

    Normalize(a) {
        const len = Math.hypot(a.X, a.Y);
        return len > 0 ? Vector2.new(a.X / len, a.Y / len) : Vector2.new(0, 0);
    },
    // Como o Utils.SafeNormalize do jogo: vetor zero vira `fallback`.
    SafeNormalize(a, fallback = { X: 0, Y: 0 }) {
        const len = Math.hypot(a.X, a.Y);
        if (!(len > 0)) return Vector2.new(fallback.X, fallback.Y);
        return Vector2.new(a.X / len, a.Y / len);
    },
    DirectionTo: (from, to) => Vector2.SafeNormalize({ X: to.X - from.X, Y: to.Y - from.Y }),

    ToRotation: (a) => Math.atan2(a.Y, a.X),
    ToRotationVector2: (radians) => Vector2.new(Math.cos(radians), Math.sin(radians)),
    AngleTo: (from, to) => Math.atan2(to.Y - from.Y, to.X - from.X),
    AngleFrom: (to, from) => Math.atan2(to.Y - from.Y, to.X - from.X),
    RotatedBy(a, radians, center = { X: 0, Y: 0 }) {
        const c = Math.cos(radians), s = Math.sin(radians);
        const dx = a.X - center.X, dy = a.Y - center.Y;
        return Vector2.new(center.X + dx * c - dy * s, center.Y + dx * s + dy * c);
    },
    // Como o Utils.RotatedByRandom do jogo: gira ate maxRadians/2 para cada lado.
    RotatedByRandom: (a, maxRadians) =>
        Vector2.RotatedBy(a, Rand.NextFloat() * maxRadians - maxRadians / 2),

    // Pixel -> tile ({ X, Y } inteiros).
    ToTileCoordinates: (a) => ({ X: Math.floor(a.X / 16), Y: Math.floor(a.Y / 16) }),
    // Copia num struct novo (para guardar um vetor que chegou como vista).
    Clone: (a) => Vector2.new(a.X, a.Y),
});

// ================================= Color =================================

const ColorBase = {
    get Type() { return XColor(); },
    new(r = 255, g = 255, b = 255, a = 255) {
        const clamp = (v) => Math.min(Math.max(Math.round(v), 0), 255);
        const c = XColor().new();
        c['void .ctor(int r, int g, int b, int a)'](clamp(r), clamp(g), clamp(b), clamp(a));
        return c;
    },
    Multiply: (c, amount) => ColorBase.new(c.R * amount, c.G * amount, c.B * amount, c.A * amount),
    Lerp: (a, b, t) => ColorBase.new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t,
                                     a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t),
    // { X, Y, Z } de 0 a 1, como o Color.ToVector3() do jogo.
    ToVector3: (c) => ({ X: c.R / 255, Y: c.G / 255, Z: c.B / 255 }),
};

// Color.White, Color.SkyBlue...: as cores do jogo, sempre como COPIA. A vista
// seria o campo estatico do jogo, e `Color.White.A = 0` apagaria o branco de
// todo mundo.
const Color = new Proxy(ColorBase, {
    get(target, key) {
        if (key in target) return target[key];
        if (typeof key !== 'string') return undefined;
        const c = XColor()[key];
        if (c === undefined || c === null || typeof c.R !== 'number') return undefined;
        return ColorBase.new(c.R, c.G, c.B, c.A);
    },
});

// ================================== IDs ==================================
// Tirados de patches/tModLoader/Terraria/ID do tModLoader (MIT).

const ItemRarityID = Object.freeze({
    Master: -13, Expert: -12, Quest: -11, Gray: -1, White: 0, Blue: 1, Green: 2, Orange: 3,
    LightRed: 4, Pink: 5, LightPurple: 6, Lime: 7, Yellow: 8, Cyan: 9, Red: 10, Purple: 11,
    Count: 12,
});

const ProjAIStyleID = Object.freeze({
    Arrow: 1, ThrownProjectile: 2, Boomerang: 3, Vilethorn: 4, FallingStar: 5, Powder: 6,
    Hook: 7, Bounce: 8, MagicMissile: 9, FallingTile: 10, FloatingFollow: 11, Stream: 12,
    Harpoon: 13, GroundProjectile: 14, Flail: 15, Explosive: 16, GraveMarker: 17, Sickle: 18,
    Spear: 19, Drill: 20, MusicNote: 21, IceRod: 22, Flames: 23, CrystalShard: 24, Boulder: 25,
    Pet: 26, Beam: 27, ColdBolt: 28, GemStaffBolt: 29, Mushroom: 30, Spray: 31, BeachBall: 32,
    Flare: 33, FireWork: 34, RopeCoil: 35, SmallFlying: 36, SpearTrap: 37, FlameThrower: 38,
    MechanicalPiranha: 39, Leaf: 40, FlowerPetal: 41, CrystalLeaf: 42, CrystalLeafShot: 43,
    MoveShort: 44, RainCloud: 45, Rainbow: 46, MagnetSphere: 47, Ray: 48, ExplosiveBunny: 49,
    Inferno: 50, LostSoul: 51, Heal: 52, FrostHydra: 53, Raven: 54, FlamingJack: 55,
    FlamingScythe: 56, NorthPoleSpear: 57, Present: 58, SpectreWrath: 59, WaterJet: 60,
    Bobber: 61, Hornet: 62, BabySpider: 63, Nado: 64, SharknadoBolt: 65, MiniTwins: 66,
    CommonFollow: 67, MolotovCocktail: 68, Flairon: 69, FlaironBubble: 70, Typhoon: 71,
    Bubble: 72, FireWorkFountain: 73, ScutlixLaser: 74, HeldProjectile: 75, Crosshair: 76,
    Electrosphere: 77, Xenopopper: 78, MartianDeathRay: 79, MartianRocket: 80, InfluxWaver: 81,
    PhantasmalEye: 82, PhantasmalSphere: 83, ThickLaser: 84, MoonLeech: 85, IceMist: 86,
    CursedFlameWall: 87, LightningOrb: 88, LightningRitual: 89, MagicLantern: 90,
    ShadowFlame: 91, ToxicCloud: 92, Nail: 93, CoinPortal: 94, ToxicBubble: 95, IchorSplash: 96,
    FlyingPiggyBank: 97, MysteriousTablet: 98, Yoyo: 99, MedusaRay: 100, HorizontalRay: 101,
    LunarProjectile: 102, Starmark: 103, BrainofConfusion: 104, SporeTrap: 105, SporeGas: 106,
    NebulaSphere: 107, Vortex: 108, MechanicWrench: 109, NurseSyringe: 110, DryadWard: 111,
    SmallProximityExplosion: 112, StickProjectile: 113, PortalGate: 114, TerrarianBeam: 115,
    DrakomiteFlare: 116, SolarEffect: 117, NebulaArcanum: 118, ArcanumSubShot: 119,
    StardustGuardian: 120, StardustDragon: 121, ReleasedEnergy: 122, LunarSentry: 123,
    FloatInFrontPet: 124, WireKite: 125, Geyser: 126, AncientStorm: 127, AncientStormMark: 128,
    SpiritFlame: 129, DD2FlameBurst: 130, DD2FlameBurstShot: 131, DD2GrimEnd: 132,
    DD2DarkSigil: 133, DD2Ballista: 134, UpwardExpand: 135, DD2BetsysBreath: 136,
    DD2LightningAura: 137, DD2ExplosiveTrap: 138, DD2ExplosiveTrapExplosion: 139,
    SleepyOctopod: 140, PoleSmash: 141, ForwardStab: 142, Ghast: 143, FloatBehindPet: 144,
    WisdomWhirlwind: 145, DD2Victory: 146, CelebrationMk2Shots: 147, FallingStarAnimation: 148,
    GolfBall: 149, GolfClub: 150, SuperStar: 151, SuperStarBeam: 152, ToiletEffect: 153,
    VoidBag: 154, SnakeCoil: 155, Terraprisma: 156, BloodThorn: 157, Finch: 158,
    PaperPlane: 159, Kite: 160, ShortSword: 161, DesertTiger: 162, Chum: 163,
    DesertTigerBall: 164, Whip: 165, ReleasedProjectile: 166, StellarTune: 167,
    FirstFractal: 168, EnchantedDagger: 169, FairyGlowStick: 170, FloatAndFly: 171,
    SplitShotCore: 172, EverlastingRainbow: 173, WormPet: 174, TitaniumShard: 175, Reaping: 176,
    CoolFlake: 177, FireCracker: 178, EtherealLance: 179, SunDance: 180, TwilightLance: 181,
    Zenith: 182, ZoologistStike: 183, TorchGod: 184, LifeDrain: 185, PrincessWeapon: 186,
    ShadowHand: 187, LightsBane: 188, Volcano: 189, NightsEdge: 190, TrueNightsEdge: 191,
    JuminoAnimation: 192, Flamethrower: 193, HorsemanPumpkin: 194, JimsDrone: 195, Petal: 196,
    CeilingAndHoverTurret: 197, Flint: 198, MeteorOre: 199, BirdDroppings: 200,
    ThrownMelee: 201, TorchGodHelper: 202, StormLightning: 203, Digtoise: 204,
    RemoteControlCar: 205, ForbiddenMinion: 206, SnappingStoneUnused: 207, GlacierFangShot: 208,
    ChlorophyteClaymoreBladeSlam: 209, YoyoShots: 210,
});

const NPCAIStyleID = Object.freeze({
    FaceClosestPlayer: 0, Slime: 1, DemonEye: 2, Fighter: 3, EyeOfCthulhu: 4, Flying: 5,
    Worm: 6, Passive: 7, Caster: 8, Spell: 9, CursedSkull: 10, SkeletronHead: 11,
    SkeletronHand: 12, ManEater: 13, Bat: 14, KingSlime: 15, Piranha: 16, Vulture: 17,
    Jellyfish: 18, Antlion: 19, SpikeBall: 20, BlazingWheel: 21, HoveringFighter: 22,
    EnchantedSword: 23, Bird: 24, Mimic: 25, Unicorn: 26, WallOfFleshMouth: 27,
    WallOfFleshEye: 28, TheHungry: 29, Retinazer: 30, Spaazmatism: 31, SkeletronPrimeHead: 32,
    PrimeSaw: 33, PrimeVice: 34, PrimeCannon: 35, PrimeLaser: 36, TheDestroyer: 37, Snowman: 38,
    GiantTortoise: 39, Spider: 40, Herpling: 41, LostGirl: 42, QueenBee: 43, FlyingFish: 44,
    GolemBody: 45, GolemHead: 46, GolemFist: 47, FreeGolemHead: 48, AngryNimbus: 49, Spore: 50,
    Plantera: 51, PlanteraHook: 52, PlanteraTentacle: 53, BrainOfCthulhu: 54, Creeper: 55,
    DungeonSpirit: 56, MourningWood: 57, Pumpking: 58, PumpkingScythe: 59, IceQueen: 60,
    SantaNK1: 61, ElfCopter: 62, Flocko: 63, Firefly: 64, Butterfly: 65, CritterWorm: 66,
    Snail: 67, Duck: 68, DukeFishron: 69, DukeFishronBubble: 70, Sharkron: 71, BubbleShield: 72,
    TeslaTurret: 73, Corite: 74, Rider: 75, MartianSaucer: 76, MoonLordCore: 77,
    MoonLordHand: 78, MoonLordHead: 79, MartianProbe: 80, TrueEyeOfCthulhu: 81,
    MoonLeachClot: 82, LunaticDevote: 83, LunaticCultist: 84, StarCell: 85, AncientVision: 86,
    BiomeMimic: 87, Mothron: 88, MothronEgg: 89, BabyMothron: 90, GraniteElemental: 91,
    TargetDummy: 92, FlyingDutchman: 93, CelestialPillar: 94, SmallStarCell: 95,
    FlowInvader: 96, NebulaFloater: 97, Unused0: 98, SolarFragment: 99, AncientLight: 100,
    AncientDoom: 101, SandElemental: 102, SandShark: 103, Unknown1: 104, DD2EterniaCrystal: 105,
    DD2MysteriousPortal: 106, DD2Fighter: 107, DD2Flying: 108, DD2DarkMage: 109, DD2Betsy: 110,
    DD2LightningBug: 111, Fairy: 112, Balloon: 113, Dragonfly: 114, Ladybug: 115,
    WaterStrider: 116, Dreadnautilus: 117, Seahorse: 118, AngryDandelion: 119,
    EmpressOfLight: 120, QueenSlime: 121, PiratesCurse: 122,
});

globalThis.MathHelper = MathHelper;
globalThis.Rand = Rand;
globalThis.Vector2 = Vector2;
globalThis.Color = Color;
globalThis.ItemRarityID = ItemRarityID;
globalThis.ProjAIStyleID = ProjAIStyleID;
globalThis.NPCAIStyleID = NPCAIStyleID;
})();

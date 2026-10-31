using System;
using System.Linq;
using System.Reflection;
using BunnyLoader.Interop;
using ReLogic.Reflection;

namespace Terraria.ID;

public class NPCAIStyleID
{
    public static readonly IdDictionary Search;
    static readonly GameHandle searchHandle;

    static unsafe NPCAIStyleID()
    {
        var fields = typeof(NPCAIStyleID).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(short)).ToArray();
        Search = IdDictionary.New(fields.Max(field => (short)field.GetRawConstantValue()!) + 1);
        searchHandle = new GameHandle(Search.Pointer);
        var prototype = NPCID.Search._idToName;
        if (prototype.IsNull) throw new InvalidOperationException("NPC AI style reverse index type is unavailable");
        nint indexClass = *(nint*)prototype.Pointer;
        nint index = BunnyLoader.Host.Native()->NewObject(indexClass);
        if (index == 0) throw new OutOfMemoryException("NPC AI style reverse index could not be allocated");
        using (var indexHandle = new GameHandle(index))
        {
            byte* signature = BunnyLoader.Host.Utf8("void .ctor()");
            nint method;
            try { method = BunnyLoader.Host.Native()->FindMethodBySignature(indexClass, signature); }
            finally { BunnyLoader.Host.FreeUtf8(signature); }
            if (method == 0) throw new MissingMethodException("NPC AI style reverse index has no constructor");
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(index);
            call.Arg(method);
            call.Invoke(BunnyLoader.Host.Native()->MethodPointer(method));
            Search._idToName = new GameObject(index);
        }
        foreach (var field in fields) Search.Add(field.Name, (short)field.GetRawConstantValue()!);
    }

    public const short FaceClosestPlayer = 0;
    public const short Slime = 1;
    public const short DemonEye = 2;
    public const short Fighter = 3;
    public const short EyeOfCthulhu = 4;
    public const short Flying = 5;
    public const short Worm = 6;
    public const short Passive = 7;
    public const short Caster = 8;
    public const short Spell = 9;
    public const short CursedSkull = 10;
    public const short SkeletronHead = 11;
    public const short SkeletronHand = 12;
    public const short ManEater = 13;
    public const short Bat = 14;
    public const short KingSlime = 15;
    public const short Piranha = 16;
    public const short Vulture = 17;
    public const short Jellyfish = 18;
    public const short Antlion = 19;
    public const short SpikeBall = 20;
    public const short BlazingWheel = 21;
    public const short HoveringFighter = 22;
    public const short EnchantedSword = 23;
    public const short Bird = 24;
    public const short Mimic = 25;
    public const short Unicorn = 26;
    public const short WallOfFleshMouth = 27;
    public const short WallOfFleshEye = 28;
    public const short TheHungry = 29;
    public const short Retinazer = 30;
    public const short Spaazmatism = 31;
    public const short SkeletronPrimeHead = 32;
    public const short PrimeSaw = 33;
    public const short PrimeVice = 34;
    public const short PrimeCannon = 35;
    public const short PrimeLaser = 36;
    public const short TheDestroyer = 37;
    public const short Snowman = 38;
    public const short GiantTortoise = 39;
    public const short Spider = 40;
    public const short Herpling = 41;
    public const short LostGirl = 42;
    public const short QueenBee = 43;
    public const short FlyingFish = 44;
    public const short GolemBody = 45;
    public const short GolemHead = 46;
    public const short GolemFist = 47;
    public const short FreeGolemHead = 48;
    public const short AngryNimbus = 49;
    public const short Spore = 50;
    public const short Plantera = 51;
    public const short PlanteraHook = 52;
    public const short PlanteraTentacle = 53;
    public const short BrainOfCthulhu = 54;
    public const short Creeper = 55;
    public const short DungeonSpirit = 56;
    public const short MourningWood = 57;
    public const short Pumpking = 58;
    public const short PumpkingScythe = 59;
    public const short IceQueen = 60;
    public const short SantaNK1 = 61;
    public const short ElfCopter = 62;
    public const short Flocko = 63;
    public const short Firefly = 64;
    public const short Butterfly = 65;
    public const short CritterWorm = 66;
    public const short Snail = 67;
    public const short Duck = 68;
    public const short DukeFishron = 69;
    public const short DukeFishronBubble = 70;
    public const short Sharkron = 71;
    public const short BubbleShield = 72;
    public const short TeslaTurret = 73;
    public const short Corite = 74;
    public const short Rider = 75;
    public const short MartianSaucer = 76;
    public const short MoonLordCore = 77;
    public const short MoonLordHand = 78;
    public const short MoonLordHead = 79;
    public const short MartianProbe = 80;
    public const short TrueEyeOfCthulhu = 81;
    public const short MoonLeachClot = 82;
    public const short LunaticDevote = 83;
    public const short LunaticCultist = 84;
    public const short StarCell = 85;
    public const short AncientVision = 86;
    public const short BiomeMimic = 87;
    public const short Mothron = 88;
    public const short MothronEgg = 89;
    public const short BabyMothron = 90;
    public const short GraniteElemental = 91;
    public const short TargetDummy = 92;
    public const short FlyingDutchman = 93;
    public const short CelestialPillar = 94;
    public const short SmallStarCell = 95;
    public const short FlowInvader = 96;
    public const short NebulaFloater = 97;
    public const short Unused0 = 98;
    public const short SolarFragment = 99;
    public const short AncientLight = 100;
    public const short AncientDoom = 101;
    public const short SandElemental = 102;
    public const short SandShark = 103;
    public const short Unknown1 = 104;
    public const short DD2EterniaCrystal = 105;
    public const short DD2MysteriousPortal = 106;
    public const short DD2Fighter = 107;
    public const short DD2Flying = 108;
    public const short DD2DarkMage = 109;
    public const short DD2Betsy = 110;
    public const short DD2LightningBug = 111;
    public const short Fairy = 112;
    public const short Balloon = 113;
    public const short Dragonfly = 114;
    public const short Ladybug = 115;
    public const short WaterStrider = 116;
    public const short Dreadnautilus = 117;
    public const short Seahorse = 118;
    public const short AngryDandelion = 119;
    public const short EmpressOfLight = 120;
    public const short QueenSlime = 121;
    public const short PiratesCurse = 122;
}

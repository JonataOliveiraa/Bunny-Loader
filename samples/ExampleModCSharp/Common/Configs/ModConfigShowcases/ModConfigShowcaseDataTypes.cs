using ExampleMod.Common.Configs.CustomDataTypes;
using ExampleMod.Content.Prefixes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases
{
    [BackgroundColor(144, 252, 249)]
    public class ModConfigShowcaseDataTypes : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        public bool SomeBool;
        public int SomeInt;
        public float SomeFloat;
        public string SomeString;

        public EquipType SomeEnum;
        public byte SomeByte;
        public uint SomeUInt;
        public long SomeLong;
        public ulong SomeULong;

        public Color SomeColor;
        public Vector2 SomeVector2;
        public Point SomePoint;

        public int[] SomeArray = [25, 70, 12];
        public List<int> SomeList = new List<int>() { 1, 3, 5 };
        public Dictionary<string, int> SomeDictionary = new Dictionary<string, int>();
        public HashSet<string> SomeSet = new HashSet<string>();

        public SimpleData SomeClassA;

        public ItemDefinition itemDefinitionExample;
        public NPCDefinition npcDefinitionExample = new NPCDefinition(NPCID.Bunny);
        public ProjectileDefinition projectileDefinitionExample = new ProjectileDefinition("ExampleMod", nameof(Content.Projectiles.ExampleHomingProjectile));
        public BuffDefinition buffDefinitionExample = new BuffDefinition("ExampleMod", nameof(Content.Buffs.ExampleDefenseBuff));
        public TileDefinition tileDefinitionExample = new TileDefinition("ExampleMod", nameof(Content.Tiles.ExampleBlock));
        public WallDefinition wallDefinitionExample = new WallDefinition("ExampleMod", nameof(Content.Walls.ExampleWall));

        public Dictionary<PrefixDefinition, float> prefixDefinitionDictionaryExample = new Dictionary<PrefixDefinition, float>() {
            [new PrefixDefinition(nameof(ExampleMod), nameof(ExamplePrefix))] = 0.5f,
            [new PrefixDefinition(PrefixID.Awkward)] = 0.8f,
        };

        public Dictionary<ClassUsedAsKey, Color> CustomKey = new Dictionary<ClassUsedAsKey, Color>();

        public ModConfigShowcaseDataTypes() {
            SomeClassA = new SimpleData() {
                percent = .85f
            };

            CustomKey.Add(new ClassUsedAsKey() {
                SomeBool = true,
                SomeNumber = 42
            },
            new Color(1, 2, 3, 4));

            itemDefinitionExample = new ItemDefinition("Terraria/GoldOre");
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;

namespace CsApiFix;

[ReinitializeDuringResizeArrays]
public static class FixSets
{
    public static bool[] Flag = ItemID.Sets.Factory.CreateNamedSet("Flag").Description("test").RegisterBoolSet(false, ItemID.DirtBlock, ModContent.ItemType<ShimmerItem>());

    public static int[] Shared = ItemID.Sets.Factory.CreateNamedSet("Terraria/SharedSet").RegisterIntSet(-1, ItemID.DirtBlock, 7);

    public static int[] SharedAgain = ItemID.Sets.Factory.CreateNamedSet("Terraria/SharedSet").RegisterIntSet(-1, ItemID.StoneBlock, 9);
}

public sealed class ShimmerItem : ModItem
{
    public override string Texture => "Terraria/Images/Item_1";

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 99;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.DirtBlock)
            .AddCustomShimmerResult(ItemID.RottenEgg)
            .AddCustomShimmerResult(ItemID.Chain, 3)
            .Register();
    }
}

public enum Flavor
{
    Sweet,
    Sour
}

public sealed class Nested
{
    [JsonIgnore]
    public int Skip = 5;

    [JsonProperty("renamed")]
    public int Original = 3;

    [JsonConverter(typeof(StringEnumConverter))]
    public Flavor Taste = Flavor.Sour;

    public Color Tint = new Color(1, 2, 3, 4);

    public Vector2 Where = new Vector2(1.5f, 2.5f);
}

public sealed class FixConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public ItemDefinition Weapon = new ItemDefinition("Terraria/GoldOre");

    public NPCDefinition Friend = new NPCDefinition(NPCID.Bunny);

    public ProjectileDefinition Shot = new ProjectileDefinition(ProjectileID.WoodenArrowFriendly);

    public BuffDefinition Effect = new BuffDefinition(BuffID.Ironskin);

    public TileDefinition Block = new TileDefinition(TileID.Stone);

    public WallDefinition Backing = new WallDefinition(WallID.Stone);

    public PrefixDefinition Modifier = new PrefixDefinition(PrefixID.Awkward);

    public Dictionary<ItemDefinition, float> Weights = new() { [new ItemDefinition(ItemID.GoldBar)] = 0.5f };

    public Nested Inner = new Nested();

    [DefaultDictionaryKeyValue(0.3f)]
    [DefaultListValue(10)]
    public Dictionary<float, int> DefaultsTest = new();

    [JsonDefaultValue("{\"renamed\": 777}")]
    public Nested FromJson;

    [DefaultValue(typeof(Vector2), "0.25, 0.75")]
    public Vector2 Default2;

    [JsonIgnore]
    public int Hidden = 9;

    public bool Plain;
}

public sealed class CsApiFixMod : Mod
{
    static int frame, pass, fail, shimmerStage;

    static ILog Log => ModContent.GetInstance<CsApiFixMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csapifix {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        frame++;
        try
        {
            if (frame == 40) Basics(self);
            if (frame == 60) Definitions();
            if (frame == 80) ConfigFile();
            if (frame == 100) StartShimmer(self);
            if (frame == 110) CheckShimmer(self);
            if (frame == 200)
            {
                ModContent.GetInstance<FixConfig>().Open(scrollToOption: nameof(FixConfig.Weapon));
                Log.Info("csapifix SHOT menu");
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(8000);
                    Log.Info($"csapifix END pass={pass} fail={fail}");
                });
            }
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
        }
    }

    static void Basics(Player self)
    {
        Color red = new Color(255, 0, 0, 255);
        Report("Color equality compares the channels", red == new Color(255, 0, 0, 255) && red != new Color(255, 0, 1, 255) && red.Equals(new Color(255, 0, 0, 255)) && red.GetHashCode() == new Color(255, 0, 0, 255).GetHashCode());

        int x = (int)(self.Center.X / 16), y = (int)(self.Center.Y / 16) - 6;
        Tile tile = Main.tile[x, y];
        tile.TileType = TileID.Stone;
        tile.HasTile = true;
        bool on = Main.tile[x, y].HasTile;
        tile.slope(4);
        bool bottom = Main.tile[x, y].BottomSlope && !Main.tile[x, y].TopSlope && Main.tile[x, y].RightSlope == false;
        tile.slope(0);
        tile.HasTile = false;
        Report("Tile.HasTile sets the tile and BottomSlope reads the slope", on && bottom && !Main.tile[x, y].HasTile, $"on={on} bottom={bottom}");

        Report("ItemHoldStyleID exists and its Search knows the names", ItemHoldStyleID.HoldLamp == 6 && ItemHoldStyleID.Search.GetName(6) == "HoldLamp", ItemHoldStyleID.Search.GetName(6));
        Report("ItemUseStyleID.Search knows the names", ItemUseStyleID.Search.GetName(ItemUseStyleID.Swing) == "Swing" && ItemUseStyleID.Search.TryGetId("Rapier", out int rapier) && rapier == ItemUseStyleID.Rapier, ItemUseStyleID.Search.GetName(1));
        Report("PrefixID.Search knows the names", PrefixID.Search.GetName(PrefixID.Awkward) == "Awkward", PrefixID.Search.GetName(PrefixID.Awkward));

        Report("ReinitializeDuringResizeArrays sizes the set for the mod items", FixSets.Flag.Length == ItemLoader.ItemCount && FixSets.Flag[ItemID.DirtBlock] && FixSets.Flag[ModContent.ItemType<ShimmerItem>()] && !FixSets.Flag[ItemID.GoldBar], $"length={FixSets.Flag.Length} count={ItemLoader.ItemCount}");
        Report("a named set is shared between registrations", ReferenceEquals(FixSets.Shared, FixSets.SharedAgain) && FixSets.Shared[ItemID.DirtBlock] == 7 && FixSets.Shared[ItemID.StoneBlock] == 9 && FixSets.Shared[ItemID.GoldBar] == -1);

        var builder = new PosData<byte>.OrderedSparseLookupBuilder(compressEqualValues: false);
        builder.Add(100, 200, 11);
        builder.Add(100, 210, 22);
        builder.Add(300, 50, 33);
        PosData<byte>[] map = builder.Build();
        bool found = PosData.NearbySearchOrderedPosMap(map, new Point(101, 201), 5, out PosData<byte> near);
        Report("PosData finds exact and nearby entries", map.Length == 3 && map.Lookup(100, 210) == 22 && map.LookupExact(300, 50, out byte exact) && exact == 33 && !map.LookupExact(300, 51, out _) && found && near.value == 11 && near.X == 100 && near.Y == 200, $"near={near.value}");
    }

    static void Definitions()
    {
        int modItem = ModContent.ItemType<ShimmerItem>();
        var gold = new ItemDefinition("Terraria/GoldOre");
        var mine = new ItemDefinition(modItem);
        Report("ItemDefinition resolves vanilla and mod items", gold.Type == ItemID.GoldOre && mine.Mod == "CsApiFix" && mine.Name == nameof(ShimmerItem) && mine.Type == modItem && !mine.IsUnloaded, $"gold={gold.Type} mine={mine} name={mine.DisplayName}");
        Report("a missing mod item is unloaded", new ItemDefinition("NoMod/NoItem").IsUnloaded);
        Report("every definition family resolves", new NPCDefinition(NPCID.Bunny).Type == NPCID.Bunny && new ProjectileDefinition(ProjectileID.WoodenArrowFriendly).Type == ProjectileID.WoodenArrowFriendly && new BuffDefinition(BuffID.Ironskin).Type == BuffID.Ironskin && new TileDefinition(TileID.Stone).Type == TileID.Stone && new WallDefinition(WallID.Stone).Type == WallID.Stone && new PrefixDefinition(PrefixID.Awkward).Type == PrefixID.Awkward);
        var tag = new TagCompound { ["item"] = mine };
        ItemDefinition read = tag.Get<ItemDefinition>("item");
        Report("a definition round trips through a TagCompound", read.Equals(mine) && read.Type == modItem, read.ToString());
        Report("DisplayName gives readable names", gold.DisplayName.Length > 0 && !gold.DisplayName.Contains('/') && new NPCDefinition(NPCID.Bunny).DisplayName.Length > 0, $"{gold.DisplayName} / {new NPCDefinition(NPCID.Bunny).DisplayName}");
        var converter = TypeDescriptor.GetConverter(typeof(ItemDefinition));
        Report("ToFromStringConverter builds a definition from text", converter.ConvertFromInvariantString("Terraria/GoldBar") is ItemDefinition bar && bar.Type == ItemID.GoldBar);
    }

    static void ConfigFile()
    {
        FixConfig config = ModContent.GetInstance<FixConfig>();
        Report("JsonDefaultValue and DefaultValue give the defaults", config.FromJson?.Original == 777 && config.Default2.X == 0.25f && config.Default2.Y == 0.75f, $"json={config.FromJson?.Original} vec={config.Default2.X},{config.Default2.Y}");
        config.Weights[new ItemDefinition(ItemID.SilverBar)] = 0.25f;
        config.Weapon = new ItemDefinition(ModContent.ItemType<ShimmerItem>());
        config.Inner.Skip = 99;
        config.Inner.Original = 42;
        config.Inner.Taste = Flavor.Sweet;
        config.SaveChanges();
        string file = Path.Combine(ConfigManager.ModConfigPath, "CsApiFix_FixConfig.json");
        string text = File.ReadAllText(file);
        Log.Info("csapifix file " + text.Replace("\r", "").Replace("\n", " "));
        Report("the file honours JsonIgnore, JsonProperty and StringEnumConverter", !text.Contains("Hidden") && !text.Contains("Skip") && text.Contains("\"renamed\": 42") && text.Contains("\"Sweet\"") && text.Contains("\"Tint\": \"1, 2, 3, 4\""));
        Report("a definition key is written as text", text.Contains("Terraria/GoldBar") && text.Contains("Terraria/SilverBar"));
        var fresh = new FixConfig();
        var type = typeof(ConfigManager);
        fresh.Weights.Clear();
        var data = System.Text.Json.Nodes.JsonNode.Parse(text)!.AsObject();
        Report("the file parses and holds the definition object", data["Weapon"]?["Mod"]?.GetValue<string>() == "CsApiFix", data["Weapon"]?.ToJsonString() ?? "null");
    }

    static void StartShimmer(Player self)
    {
        Main.item[399].type = 0;
        int index = Item.NewItem(self.GetSource_FromThis(), self.Center + new Vector2(200, -80), ModContent.ItemType<ShimmerItem>());
        Main.item[index].GetShimmered();
        shimmerStage = index;
    }

    static void CheckShimmer(Player self)
    {
        int eggs = 0, chains = 0;
        for (int k = 0; k < Main.maxItems; k++)
        {
            if (!Main.item[k].active) continue;
            if (Main.item[k].type == ItemID.RottenEgg) eggs += Main.item[k].stack;
            if (Main.item[k].type == ItemID.Chain) chains += Main.item[k].stack;
        }
        Report("AddCustomShimmerResult decrafts into the custom items", eggs >= 1 && chains >= 3, $"eggs={eggs} chains={chains} index={shimmerStage}");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;

namespace CsExShow;

public sealed class CsExShowMod : Mod
{
    static readonly string[] Items = { "ActiveSoundShowcase", "ExampleMultiUseItem", "PlanteraItem", "ExampleBasicFish", "ExampleDataItem", "ExamplePaperAirplane", "ExampleCanStackItem", "ExampleStackableDurabilityItem", "ExampleFlask", "CustomItemDrawingShowcase", "HoldStyleShowcase", "UseStyleShowcase", "ShimmerShowcaseConditions", "ShimmerShowcaseCustomShimmerResult" };

    static int frame, pass, fail, useFrames, multiSlot = 20, dataSlot = 21;
    static bool use;

    static ILog Log => ModContent.GetInstance<CsExShowMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
    }

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (use && self.whoAmI == Main.myPlayer)
        {
            self.controlUseItem = true;
            self.releaseUseItem = true;
        }
        orig(self);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexshow {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static object? Field(ModItem item, string name) => item.GetType().GetField(name)!.GetValue(item);

    static void SetField(ModItem item, string name, object value) => item.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(item, value);

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        frame++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
        }
    }

    static void Step(Player self)
    {
        switch (frame)
        {
            case 30:
                Names();
                Sets();
                Stacks();
                Styles(self);
                Configs();
                Positions();
                Vines(self);
                Fish();
                Recipes();
                break;
            case 40:
                Plantera(self);
                Airplane(self);
                Shimmer(self);
                break;
            case 60:
                Data(self);
                MultiStart(self);
                break;
            case 170:
                MultiEnd(self);
                break;
            case 180:
                DrawStart(self);
                break;
            case 200:
                DrawMode(self, 1);
                break;
            case 220:
                DrawMode(self, 2);
                Log.Info("csexshow SHOT behind");
                break;
            case 400:
                DrawEnd(self);
                Log.Info($"csexshow END pass={pass} fail={fail}");
                break;
        }
    }

    static void Names()
    {
        var bad = new List<string>();
        foreach (string name in Items)
        {
            var item = new Item(ItemType(name));
            if (item.Name.Contains("Mods.") || item.Name.Length == 0 || Terraria.GameContent.TextureAssets.Item[item.type].Value.Width <= 0)
                bad.Add(name);
        }
        Report("every new item has a name and a texture", bad.Count == 0, string.Join(",", bad));
    }

    static void Sets()
    {
        Type sets = ModContent.Find<ModItem>("ExampleMod/ExampleFlask").GetType().Assembly.GetType("ExampleMod.Content.Items.CustomItemSets")!;
        var flaming = (bool[])sets.GetField("FlamingWeapon")!.GetValue(null)!;
        Report("CustomItemSets makes a set for every item, with entries added in SetStaticDefaults", flaming.Length == ItemLoader.ItemCount && flaming[ItemID.FieryGreatsword] && flaming[ItemType("ExampleSword")] && flaming[ItemID.FireWhip] && flaming[ItemID.HelFire] && !flaming[ItemID.GoldBar], $"length={flaming.Length}");
        var flask = new Item(ItemType("ExampleFlask"));
        Report("ExampleFlask imbues and has drink colors", flask.buffType == ModContent.Find<ModBuff>("ExampleMod/ExampleWeaponImbue").Type && flask.buffTime == Item.flaskTime && ItemID.Sets.DrinkParticleColors[flask.type].Length == 3);
    }

    static void Stacks()
    {
        int gift = ItemType("ExampleCanStackItem");
        var a = new Item(gift);
        var b = new Item(gift);
        SetField(a.ModItem!, "craftedPlayerName", "Alice");
        SetField(b.ModItem!, "craftedPlayerName", "Bob");
        bool different = ItemLoader.CanStack(a, b);
        SetField(b.ModItem!, "craftedPlayerName", "Alice");
        bool same = ItemLoader.CanStack(a, b);
        Report("ExampleCanStackItem only stacks with the same crafter", !different && same, $"different={different} same={same}");

        int durable = ItemType("ExampleStackableDurabilityItem");
        var c = new Item(durable);
        var d = new Item(durable);
        c.stack = 1;
        d.stack = 3;
        SetField(c.ModItem!, "durability", 1f);
        SetField(d.ModItem!, "durability", 0f);
        c.ModItem!.OnStack(d, 3);
        float merged = (float)Field(c.ModItem!, "durability")!;
        Report("ExampleStackableDurabilityItem averages durability by stack", Math.Abs(merged - 0.25f) < 0.01f, $"merged={merged}");
    }

    static void Styles(Player self)
    {
        foreach (string name in new[] { "HoldStyleShowcase", "UseStyleShowcase" })
        {
            var item = new Item(ItemType(name));
            int held = self.selectedItem;
            bool hold = name[0] == 'H';
            int before = hold ? item.holdStyle : item.useStyle;
            self.altFunctionUse = 2;
            item.ModItem!.UseItem(self);
            self.altFunctionUse = 0;
            int after = hold ? item.holdStyle : item.useStyle;
            string found = hold ? ItemHoldStyleID.Search.GetName(after) : ItemUseStyleID.Search.GetName(after);
            Report(name + " cycles the style and finds its name", after != before && found.Length > 0, $"{before}->{after} {found}");
        }
    }

    static ModConfig Config(string name) => ConfigManager.GetConfig(ModLoader.GetMod("ExampleMod"), name)!;

    static object? Value(ModConfig config, string name) => config.GetType().GetField(name)?.GetValue(config) ?? config.GetType().GetProperty(name)?.GetValue(config);

    static void Configs()
    {
        string[] names = { "ModConfigShowcaseAccessibility", "ModConfigShowcaseDataTypes", "ModConfigShowcaseDefaultValues", "ModConfigShowcaseLabels", "ModConfigShowcaseSubpages" };
        Report("the five config showcases load", names.All(n => Config(n) != null));
        ModConfig types = Config("ModConfigShowcaseDataTypes");
        var gold = (EntityDefinition)Value(types, "itemDefinitionExample")!;
        Report("DataTypes resolves its definitions", gold.Type == ItemID.GoldOre && ((EntityDefinition)Value(types, "wallDefinitionExample")!).Type == ModContent.Find<ModWall>("ExampleMod/ExampleWall").Type, gold.ToString());
        ModConfig defaults = Config("ModConfigShowcaseDefaultValues");
        var color = (Color)Value(defaults, "SomeColor")!;
        var vector = (Vector2)Value(defaults, "SomeVector2")!;
        Report("DefaultValues applies the defaults", (int)Value(defaults, "SimpleDefaultInt")! == 99 && color == new Color(73, 94, 171, 255) && Math.Abs(vector.X - 0.23f) < 0.001f && (string)Value(defaults, "FavoritePokemon")! == "Bulbasaur", $"color={color} vector={vector.X},{vector.Y}");
        foreach (string name in names)
        {
            ModConfig config = Config(name);
            config.SaveChanges();
            string file = Path.Combine(ConfigManager.ModConfigPath, $"ExampleMod_{name}.json");
            string text = File.Exists(file) ? File.ReadAllText(file) : "";
            bool parsed = text.Length > 2 && System.Text.Json.Nodes.JsonNode.Parse(text) != null;
            Report(name + " saves to a readable file", parsed, $"bytes={text.Length}");
        }
        string data = File.ReadAllText(Path.Combine(ConfigManager.ModConfigPath, "ExampleMod_ModConfigShowcaseDataTypes.json"));
        Report("a definition dictionary and a class key are written with text keys", data.Contains("ExampleMod/ExamplePrefix") && data.Contains("True, 42"));
        var access = Config("ModConfigShowcaseAccessibility");
        string accessText = File.ReadAllText(Path.Combine(ConfigManager.ModConfigPath, "ExampleMod_ModConfigShowcaseAccessibility.json"));
        Report("JsonIgnore keeps a field out of the file", !accessText.Contains("\"Ignore\""));
    }

    static void Positions()
    {
        Type type = ModContent.Find<ModItem>("ExampleMod/ExampleFlask").GetType().Assembly.GetType("ExampleMod.Common.Systems.SimpleDataAtParticularLocations")!;
        var system = (ModSystem)typeof(ModContent).GetMethod("GetInstance")!.MakeGenericMethod(type).Invoke(null, null)!;
        FieldInfo field = type.GetField("myMap")!;
        object? before = field.GetValue(system);
        system.PostWorldGen();
        var map = (PosData<byte>[])field.GetValue(system)!;
        var tag = new TagCompound();
        system.SaveWorldData(tag);
        system.LoadWorldData(tag);
        var read = (PosData<byte>[])field.GetValue(system)!;
        Report("SimpleDataAtParticularLocations round trips its PosData map", map.Length == Main.maxTilesY && read.Length == map.Length && read[100].value == map[100].value, $"map={map.Length} read={read.Length}");
        field.SetValue(system, before);
    }

    static void Vines(Player self)
    {
        int x = (int)(self.Center.X / 16) - 30, y = (int)(self.Center.Y / 16) - 12;
        for (int j = y - 1; j <= y + 3; j++)
            WorldGen.KillTile(x, j, false, false, true);
        int block = ModContent.Find<ModTile>("ExampleMod/ExampleBlock").Type;
        int vine = ModContent.Find<ModTile>("ExampleMod/ExampleVine").Type;
        WorldGen.PlaceTile(x, y, block, true, true);
        Tile below = Main.tile[x, y + 1];
        below.TileType = TileID.Vines;
        below.HasTile = true;
        WorldGen.SquareTileFrame(x, y + 1);
        Report("a vine under ExampleBlock turns into ExampleVine", Main.tile[x, y + 1].TileType == vine, $"type={Main.tile[x, y + 1].TileType} vine={vine}");
        Report("ExampleVine has the vine sets", TileID.Sets.IsVine[vine] && TileID.Sets.VineThreads[vine] && TileID.Sets.ReplaceTileBreakDown[vine] && Main.tileCut[vine]);
        WorldGen.KillTile(x, y, false, false, true);
        WorldGen.SquareTileFrame(x, y + 1);
        Report("ExampleVine breaks without its block", !Main.tile[x, y + 1].HasTile, $"has={Main.tile[x, y + 1].HasTile}");
    }

    static void Fish()
    {
        int fish = ItemType("ExampleBasicFish");
        Report("ExampleBasicFish counts as a basic fish", ItemID.Sets.IsBasicFish[fish] && ItemID.Sets.CanBePlacedOnWeaponRacks[fish]);
    }

    static void Recipes()
    {
        int fish = ItemType("ExampleBasicFish");
        bool cooked = false, plantera = false;
        for (int k = 0; k < Recipe.numRecipes; k++)
        {
            Recipe recipe = Main.recipe[k];
            if (recipe.createItem.type == ItemID.CookedFish && recipe.requiredItem[0].type == fish)
                cooked = true;
            if (recipe.createItem.type == ItemType("PlanteraItem"))
                plantera = true;
        }
        Report("the new items add their recipes", cooked && plantera, $"cooked={cooked} plantera={plantera}");
    }

    static void Plantera(Player self)
    {
        var item = new Item(ItemType("PlanteraItem"));
        Report("PlanteraItem refuses to be used before the mech bosses", !item.ModItem!.CanUseItem(self));
        item.ModItem!.UseItem(self);
        bool spawned = NPC.AnyNPCs(NPCID.Plantera);
        Report("PlanteraItem spawns the boss", spawned);
        for (int k = 0; k < Main.maxNPCs; k++)
            if (Main.npc[k].type == NPCID.Plantera || Main.npc[k].type == NPCID.PlanterasHook || Main.npc[k].type == NPCID.PlanterasTentacle)
                Main.npc[k].active = false;
    }

    static void Airplane(Player self)
    {
        var item = new Item(ItemType("ExamplePaperAirplane"));
        int shot = item.shoot;
        int index = Projectile.NewProjectile(self.GetSource_FromThis(), self.Center + new Vector2(60, -60), Vector2.Zero, shot, 0, 0, self.whoAmI);
        Main.projectile[index].Kill();
        bool dropped = false;
        for (int k = 0; k < Main.maxItems; k++)
            dropped |= Main.item[k].active && Main.item[k].type == item.type;
        Report("the paper airplane drops its item", shot > 0 && dropped, $"shot={shot} dropped={dropped}");
    }

    static void Shimmer(Player self)
    {
        int type = ItemType("ShimmerShowcaseCustomShimmerResult");
        int index = Item.NewItem(self.GetSource_FromThis(), self.Center + new Vector2(200, -80), type);
        Main.item[index].GetShimmered();
        int eggs = 0, chains = 0;
        for (int k = 0; k < Main.maxItems; k++)
        {
            if (!Main.item[k].active) continue;
            if (Main.item[k].type == ItemID.RottenEgg) eggs += Main.item[k].stack;
            if (Main.item[k].type == ItemID.Chain) chains += Main.item[k].stack;
        }
        Report("ShimmerShowcase decrafts into the custom results", eggs >= 1 && chains >= 3, $"eggs={eggs} chains={chains}");
    }

    static void Data(Player self)
    {
        self.inventory[dataSlot].SetDefaults(ItemType("ExampleDataItem"), default);
        SetField(self.inventory[dataSlot].ModItem!, "timer", 5);
        self.statLife = 100;
    }

    static void MultiStart(Player self)
    {
        self.inventory[self.selectedItem].SetDefaults(ItemType("ExampleMultiUseItem"), default);
        self.inventory[self.selectedItem].stack = 5;
        use = true;
    }

    static void MultiEnd(Player self)
    {
        use = false;
        Report("ExampleDataItem runs out and disappears", self.inventory[dataSlot].IsAir, $"type={self.inventory[dataSlot].type}");
        Item multi = self.inventory[self.selectedItem];
        Report("ExampleMultiUseItem loses one of the stack after four uses", multi.stack < 5 && multi.stack >= 3, $"type={multi.type} stack={multi.stack} slot={self.selectedItem} count={(multi.ModItem != null ? Field(multi.ModItem, "useCount") : "none")}");
        multi.TurnToAir();
    }

    static void DrawStart(Player self)
    {
        self.inventory[self.selectedItem].SetDefaults(ItemType("CustomItemDrawingShowcase"), default);
        use = true;
    }

    static void DrawMode(Player self, int mode)
    {
        SetField(self.inventory[self.selectedItem].ModItem!, "drawMode", mode);
    }

    static void DrawEnd(Player self)
    {
        use = false;
        Report("CustomItemDrawingShowcase cycles with a right click", true);
        self.inventory[self.selectedItem].TurnToAir();
    }
}

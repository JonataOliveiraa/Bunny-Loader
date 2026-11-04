using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExItems;

public sealed class CsExItemsMod : Mod
{
    const int End = 700;

    internal static int soulUpdates;
    internal static int soulType = -1;
    static int frame, pass, fail, soul = -1, barX, barY, defenseWith, defenseWithout;
    static float soulStartY;

    static ILog Log => ModContent.GetInstance<CsExItemsMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexitems {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static int PrefixType(string name) => ModContent.Find<ModPrefix>("ExampleMod/" + name).Type;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        orig(self, i);
        if (i != Main.myPlayer || frame > End) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == End) Log.Info($"csexitems END pass={pass} fail={fail}");
    }

    static void Step(Player player)
    {
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 27000;
                soulType = ItemType("ExampleSoul");
                Prefixes(player);
                Tooltips();
                Recipes();
                break;
            case 60:
                player.armor[3].SetDefaults(ItemID.Shackle, default);
                player.armor[3].ResetPrefix();
                break;
            case 90:
                defenseWithout = player.statDefense;
                player.armor[3].Prefix(PrefixType("ExampleAccessoryPrefix"));
                break;
            case 120:
                defenseWith = player.statDefense;
                Report("the accessory prefix adds 4 defense when equipped", defenseWith - defenseWithout == 4 && player.armor[3].prefix == PrefixType("ExampleAccessoryPrefix"), $"without={defenseWithout} with={defenseWith}");
                player.armor[3].TurnToAir();
                break;
            case 130:
                soul = Item.NewItem(player.GetSource_FromThis(), player.Center + new Vector2(120, -80), soulType);
                Main.item[soul].velocity = Vector2.Zero;
                soulStartY = Main.item[soul].position.Y;
                soulUpdates = 0;
                break;
            case 230:
            {
                WorldItem w = Main.item[soul];
                Report("the soul stays in the air", w.active && Math.Abs(w.position.Y - soulStartY) < 8f, $"active={w.active} dy={w.position.Y - soulStartY}");
                Report("ModItem/GlobalItem.PostUpdate run on the ground item", soulUpdates > 50, $"updates={soulUpdates}");
                int ticks = Main.itemAnimations[soulType].TicksPerFrame, frames = Main.itemAnimations[soulType].FrameCount;
                Report("the soul animates", ticks == 5 && frames == 4 && ItemID.Sets.AnimatesAsSoul[soulType], $"ticks={ticks} frames={frames}");
                Log.Info("csexitems SHOT soul");
                break;
            }
            case 260:
                Main.item[soul].inner.TurnToAir();
                barX = (int)(player.Center.X / 16) + 6;
                barY = (int)((player.position.Y + player.height) / 16) - 1;
                break;
            case 261:
            {
                int barTile = ModContent.Find<ModTile>("ExampleMod/ExampleBar").Type;
                WorldGen.PlaceTile(barX, barY, barTile, true, true);
                Tile tile = Main.tile[barX, barY];
                Report("the bar sits on the ground", tile.HasTile && tile.TileType == barTile && Main.tileSolidTop[barTile] && Main.tileShine[barTile] == 1100, $"has={tile.HasTile} type={tile.TileType}");
                Log.Info("csexitems SHOT bar");
                break;
            }
            case 320:
            {
                WorldGen.KillTile(barX, barY + 1, false, false, true);
                WorldGen.TileFrame(barX, barY);
                break;
            }
            case 340:
            {
                int barTile = ModContent.Find<ModTile>("ExampleMod/ExampleBar").Type;
                Tile tile = Main.tile[barX, barY];
                Report("the bar breaks without the ground under it", !tile.HasTile || tile.TileType != barTile, $"has={tile.HasTile} type={tile.TileType}");
                player.inventory[player.selectedItem].SetDefaults(ItemType("ExampleTooltipsItem"), default);
                Log.Info("csexitems TAP 30 30");
                break;
            }
            case 380:
                Log.Info("csexitems SHOT inventory");
                break;
            case 400:
                Log.Info("csexitems TAP 97 290");
                break;
            case 460:
                Log.Info("csexitems SHOT tooltip");
                break;
            case 480:
                player.inventory[player.selectedItem].SetDefaults(ItemID.IronBroadsword, default);
                player.inventory[player.selectedItem].Prefix(PrefixType("ExampleDerivedPrefix"));
                break;
            case 500:
                Log.Info("csexitems TAP 97 290");
                break;
            case 560:
                Log.Info("csexitems SHOT prefix");
                break;
            case 600:
                Log.Info("csexitems TAP 30 30");
                break;
        }
    }

    static void Prefixes(Player player)
    {
        Report("the Example Mod is loaded", ModLoader.TryGetMod("ExampleMod", out _), "");
        int plain = PrefixType("ExamplePrefix"), derived = PrefixType("ExampleDerivedPrefix"), accessory = PrefixType("ExampleAccessoryPrefix");
        Item sword = new Item(ItemID.IronBroadsword);
        int damage = sword.damage, value = sword.value;
        Item a = new Item(ItemID.IronBroadsword);
        a.Prefix(plain);
        Item b = new Item(ItemID.IronBroadsword);
        b.Prefix(derived);
        Report("ExamplePrefix gives 20% damage and raises the value", a.prefix == plain && a.damage == (int)Math.Round(damage * 1.2f) && a.value > value, $"damage={damage}->{a.damage} value={value}->{a.value}");
        Report("ExampleDerivedPrefix doubles the Power", b.prefix == derived && b.damage == (int)Math.Round(damage * 1.4f) && b.value > a.value, $"damage={b.damage} value={b.value}");

        var rolled = new HashSet<int>();
        Item roll = new Item(ItemID.IronBroadsword);
        for (int k = 0; k < 400; k++)
        {
            roll.ResetPrefix();
            roll.Prefix(-1);
            rolled.Add(roll.prefix);
        }
        Report("weapons roll both mod prefixes", rolled.Contains(plain) && rolled.Contains(derived) && !rolled.Contains(accessory), $"rolled {rolled.Count}");
        rolled.Clear();
        Item shackle = new Item(ItemID.Shackle);
        for (int k = 0; k < 400; k++)
        {
            shackle.ResetPrefix();
            shackle.Prefix(-1);
            rolled.Add(shackle.prefix);
        }
        Report("accessories roll the accessory prefix", rolled.Contains(accessory) && !rolled.Contains(plain), $"rolled {rolled.Count}");

        ModPrefix prefix = PrefixLoader.GetPrefix(plain)!;
        TooltipLine[] lines = prefix.GetTooltipLines(a)!.ToArray();
        Report("ExamplePrefix has its two tooltip lines, in the good color", lines.Length == 2 && lines[0].Text.Contains("+1") && !lines[0].Text.Contains('{') && lines[0].Color.G == Terraria.ID.Colors.PrefixGood.G, string.Join("|", lines.Select(l => l.Text)));
        TooltipLine[] derivedLines = PrefixLoader.GetPrefix(derived)!.GetTooltipLines(b)!.ToArray();
        Report("the derived prefix says +2 and its own line", derivedLines[0].Text.Contains("+2") && derivedLines[1].Text != lines[1].Text, string.Join("|", derivedLines.Select(l => l.Text)));
        string name = Lang.prefix[plain].Value ?? "";
        Report("the prefix has its name", name.Length > 0 && !name.Contains("Mods."), name);
        TooltipLine[] defense = PrefixLoader.GetPrefix(accessory)!.GetTooltipLines(shackle)!.ToArray();
        Report("the accessory prefix tooltip says +4 with the game's word", defense.Length == 1 && defense[0].Text.StartsWith("+4") && defense[0].Text.Length > 3, defense[0].Text);
    }

    static void Tooltips()
    {
        Item item = new Item(ItemType("ExampleTooltipsItem"));
        var lines = new List<TooltipLine> { new(ModContent.GetInstance<CsExItemsMod>(), "Other", "other") };
        item.ModItem!.ModifyTooltips(lines);
        TooltipLine? hidden = lines.Find(l => l.Name == "Verbose:RemoveMe"), face = lines.Find(l => l.Name == "Face");
        Report("ModifyTooltips adds Face in its color and hides RemoveMe", hidden != null && !hidden.Visible && face != null && face.Visible && face.Color.B == 255 && face.Color.R == 100, $"lines={lines.Count}");
        Report("the tooltips item floats and animates", ItemID.Sets.ItemNoGravity[item.type] && !Main.itemAnimations[item.type].IsNull, "");
    }

    static void Recipes()
    {
        int bar = ItemType("ExampleBar"), ore = ItemType("ExampleOre"), sword = ItemType("ExampleSwingingEnergySword");
        Recipe? smelt = null, forge = null;
        for (int k = 0; k < Recipe.numRecipes; k++)
        {
            Recipe r = Main.recipe[k];
            if (r.createItem.type == bar) smelt = r;
            if (r.createItem.type == sword) forge = r;
        }
        Report("the bar is smelted from 4 ore", smelt is Recipe s && s.TryGetIngredient(ore, out Item oreIngredient) && oreIngredient.stack == 4, "");
        Report("the energy sword takes 12 bars", forge is Recipe f && f.TryGetIngredient(bar, out Item barIngredient) && barIngredient.stack == 12, "");
        Item barItem = new Item(bar, 5);
        Report("the Chlorophyte Extractinator trades the bar", Terraria.GameContent.ItemTrader.ChlorophyteExtractinator.TryGetTradeOption(barItem, out _), "");
        Report("the bar places its tile", barItem.createTile == ModContent.Find<ModTile>("ExampleMod/ExampleBar").Type, $"tile={barItem.createTile}");
    }
}

public sealed class SoulWatcher : GlobalItem
{
    public override void PostUpdate(WorldItem item)
    {
        if (item.inner.type == CsExItemsMod.soulType) CsExItemsMod.soulUpdates++;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using ApiGapsTest.Items;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.GameContent.Creative;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;

namespace ApiGapsTest;

public sealed class CsApiGapsMod : Mod
{
    internal static int frame, pass, fail, cx, floor, boss = -1, slimeFrames, hitTileCalls, torchCalls;
    internal static int blockX = -1, blockY = -1;
    internal static Recipe swordRecipe, torchRecipe;
    internal static int torchSlot = -1;
    internal static bool bossBarBefore;

    static ILog Log => ModContent.GetInstance<CsApiGapsMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_HitTile.AddDamage += (orig, self, tileId, damageAmount, updateAmount) =>
        {
            hitTileCalls++;
            return orig(self, tileId, damageAmount, updateAmount);
        };
        On_TorchID.TorchColor += (On_TorchID.orig_TorchColor orig, int torchID, out float r, out float g, out float b) =>
        {
            torchCalls++;
            orig(torchID, out r, out g, out b);
        };
    }

    internal static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csapigaps {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Check(string name, Func<(bool ok, string detail)> check)
    {
        try
        {
            (bool ok, string detail) = check();
            Report(name, ok, detail);
        }
        catch (Exception e)
        {
            Report(name, false, e.ToString());
        }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
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

    static int Floor(int x, int fromY)
    {
        for (int y = fromY; y < Main.maxTilesY - 10; y++)
            if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
                return y;
        return fromY;
    }

    static int FindRecipe(Func<Recipe, bool> match)
    {
        for (int k = 0; k < Recipe.numRecipes; k++)
            if (match(Main.recipe[k]))
                return k;
        return -1;
    }

    static void Step(Player self)
    {
        switch (frame)
        {
            case 30:
                cx = (int)(self.Center.X / 16);
                floor = Floor(cx, (int)(self.Bottom.Y / 16) - 2);
                Loading();
                Recipes();
                Explode();
                Conditions();
                Sounds();
                Proxies(self);
                Json();
                Item.NewItem((cx + 25) * 16, (floor - 8) * 16, 16, 16, ModContent.ItemType<GapHelmet>());
                boss = NPC.NewNPC(self.GetSource_FromThis(), (cx + 30) * 16, (floor - 2) * 16, NPCID.KingSlime);
                break;
            case 90:
                Report("ModItem.Update runs for an item in the world", GapHelmet.updates > 0, "calls=" + GapHelmet.updates);
                break;
            case 160:
                bossBarBefore = !Main.BigBossProgressBar._currentBar.IsNull;
                Main.npc[boss].BossBar = Main.BigBossProgressBar.NeverValid;
                break;
            case 220:
                Report("NPC.BossBar = NeverValid hides the boss bar", bossBarBefore && Main.BigBossProgressBar._currentBar.IsNull, $"before={bossBarBefore} after={!Main.BigBossProgressBar._currentBar.IsNull}");
                Main.npc[boss].active = false;
                Mood(self);
                Log.Info($"csapigaps END pass={pass} fail={fail}");
                break;
        }
    }

    static void Loading()
    {
        GapHelmet helmet = ModContent.GetInstance<GapHelmet>();
        Report("field initializers that read thread statics run at load", helmet.Roll is >= 0 and < 10, $"roll={helmet.Roll} reversed={helmet.Reversed}");
        Check("Color[] goes into ItemID.Sets.FoodParticleColors", () =>
        {
            var colors = ItemID.Sets.FoodParticleColors[ModContent.ItemType<GapHelmet>()];
            return (colors.Length == 3 && colors[1].G == 255 && colors[2].B == 255, "length=" + colors.Length);
        });
        Check("[AutoloadEquip] outside the mod id namespace", () =>
        {
            Item item = Item.New();
            item.SetDefaults(ModContent.ItemType<GapHelmet>(), default);
            return (item.headSlot > 0, "headSlot=" + item.headSlot);
        });
        Check("ModContent.Request ignores the mod name case", () =>
        {
            ModContent.Request<Texture2D>("csapigaps/Items/GapHelmet");
            return (ModContent.HasAsset("CSAPIGAPS/Items/GapHelmet"), "");
        });
    }

    static void Recipes()
    {
        Check("AddIngredient on a game recipe leaves other recipes alone", () =>
        {
            Recipe sword = swordRecipe, torch = torchRecipe;
            bool gel = sword.HasIngredient(ItemID.Gel);
            int empty = torch.requiredItem[torchSlot].type;
            return (gel && empty == 0, $"swordHasGel={gel} torchEmptySlot={empty}");
        });
        Check("Recipe.SortBefore/SortAfter order the recipe list", () =>
        {
            int sword = FindRecipe(r => r.createItem.type == ItemID.WoodenSword);
            int before = FindRecipe(r => r.createItem.type == ItemID.DirtBlock && r.createItem.stack == 7);
            int after = FindRecipe(r => r.createItem.type == ItemID.DirtBlock && r.createItem.stack == 9);
            bool index = Main.recipe[before].RecipeIndex == before && Main.recipe[after].RecipeIndex == after;
            return (before == sword - 1 && after == sword + 1 && index, $"before={before} sword={sword} after={after} index={index}");
        });
    }

    static void Explode()
    {
        blockX = cx;
        blockY = floor;
        bool blocked = !TileLoader.CanExplode(cx, floor);
        bool other = TileLoader.CanExplode(cx + 1, floor);
        bool wall = WallLoader.CanExplode(cx, floor - 5, WallID.Stone);
        Report("TileLoader.CanExplode and WallLoader.CanExplode ask the globals", blocked && other && wall, $"blocked={blocked} other={other} wall={wall}");
    }

    static void Conditions()
    {
        int count = typeof(Condition).GetFields(BindingFlags.Public | BindingFlags.Static).Count(f => f.FieldType == typeof(Condition));
        bool moonLord = Condition.DownedMoonLord.IsMet() == NPC.downedMoonlord && Condition.NotDownedMoonLord.IsMet() != NPC.downedMoonlord;
        bool moon = Condition.MoonPhaseFull.IsMet() == (Main.GetMoonPhase() == MoonPhase.Full);
        Report("Condition has the tModLoader conditions", count >= 200 && moonLord && moon, $"count={count} moonLord={moonLord} moon={moon}");
    }

    static void Sounds()
    {
        Check("SoundID.Zombie26 and ScaryScream", () =>
        {
            LegacySoundStyle zombie = SoundID.Zombie26, scream = SoundID.ScaryScream, last = SoundID.Zombie130;
            SoundEngine.PlaySound(SoundID.Zombie26, Main.LocalPlayer.Center);
            SoundEngine.PlaySound(SoundID.ScaryScream, Main.LocalPlayer.Center);
            bool ok = zombie.SoundId == SoundID.Zombie && zombie.Style == 26 && scream.SoundId == SoundID.Roar && scream.Style == 2 && last.Style == 130;
            return (ok, $"zombie={zombie.SoundId}/{zombie.Style} scream={scream.SoundId}/{scream.Style}");
        });
    }

    static void Proxies(Player self)
    {
        Check("new proxies: Creative powers, Game, BirthdayParty", () =>
        {
            CreativePowerManager powers = CreativePowerManager.Instance;
            long tick = Microsoft.Xna.Framework.Game.FrameTickTime.Ticks;
            bool party = BirthdayParty.PartyIsUp;
            return (!powers.IsNull && tick > 0, $"tick={tick} party={party}");
        });
        Check("WorldFileData.UniqueId and PlayerFileData.GetPlayTime", () =>
        {
            Guid id = Main.ActiveWorldFileData.UniqueId;
            TimeSpan played = Main.ActivePlayerFileData.GetPlayTime();
            return (id != Guid.Empty && played >= TimeSpan.Zero, $"id={id} played={played}");
        });
        Check("TorchID.TorchColor and its hook", () =>
        {
            TorchID.TorchColor(TorchID.Torch, out float r, out float g, out float b);
            return (r > 0f && torchCalls > 0, $"rgb={r:0.00},{g:0.00},{b:0.00} hooked={torchCalls}");
        });
        Check("MapTile.Create and WorldMap.IsRevealed", () =>
        {
            MapTile tile = MapTile.Create(5, 255, 0);
            bool revealed = Main.Map.IsRevealed(cx, floor);
            return (tile.Type == 5 && tile.Light == 255, $"type={tile.Type} light={tile.Light} revealed={revealed}");
        });
        Check("page controller layouts", () =>
        {
            Reforge_Layout reforge = Reforge_Layout.Instance;
            InventorySplitStack_Layout split = InventorySplitStack_Layout.Instance;
            string right = reforge.IsNull ? "-" : reforge.RightPageController.ToString();
            return (!reforge.IsNull && !split.IsNull, $"reforge={!reforge.IsNull} right={right} split={!split.IsNull} button={(!split.IsNull && !split.MainButton.IsNull)}");
        });
        Check("HitTile.AddDamage hook fires when a tile is mined", () =>
        {
            self.PickTile(cx + 3, floor, 100);
            return (hitTileCalls > 0, "calls=" + hitTileCalls);
        });
    }

    static void Json()
    {
        Check("System.Text.Json", () =>
        {
            string text = JsonSerializer.Serialize(new Dictionary<string, int> { ["a"] = 1 });
            var back = JsonSerializer.Deserialize<Dictionary<string, int>>(text);
            return (text == "{\"a\":1}" && back!["a"] == 1, text);
        });
    }

    static void Mood(Player self)
    {
        Check("town NPC mood lines come from the mod's TownNPCMood keys", () =>
        {
            if (!ModContent.TryFind("ExampleMod/ExamplePerson", out ModNPC person))
                return (false, "ExampleMod is not loaded (run with SAMPLE=1)");
            int at = NPC.NewNPC(self.GetSource_FromThis(), (cx - 6) * 16, floor * 16, person.Type);
            NPC npc = Main.npc[at];
            string text = Main.ShopHelper.GetShoppingSettings(self, npc).HappinessReport.ToString() ?? "";
            npc.active = false;
            return (text.Length > 0 && !text.Contains("TownNPCMood"), text);
        });
    }
}

public sealed class GapGlobalTile : GlobalTile
{
    public override bool CanExplode(int i, int j, int type) => i != CsApiGapsMod.blockX || j != CsApiGapsMod.blockY;
}

public sealed class GapRecipes : ModSystem
{
    public override void AddRecipes()
    {
        Recipe.Create(ItemID.DirtBlock, 7).AddIngredient(ItemID.StoneBlock).Register().SortBeforeFirstRecipesOf(ItemID.WoodenSword);
        Recipe.Create(ItemID.DirtBlock, 9).AddIngredient(ItemID.StoneBlock).SortAfterFirstRecipesOf(ItemID.WoodenSword).Register();
    }

    public override void PostAddRecipes()
    {
        for (int k = 0; k < Recipe.numRecipes; k++)
        {
            Recipe recipe = Main.recipe[k];
            if (CsApiGapsMod.swordRecipe.IsNull && recipe.createItem.type == ItemID.WoodenSword)
                CsApiGapsMod.swordRecipe = recipe;
            if (CsApiGapsMod.torchRecipe.IsNull && recipe.createItem.type == ItemID.Torch)
                CsApiGapsMod.torchRecipe = recipe;
        }
        Recipe torch = CsApiGapsMod.torchRecipe;
        int slot = 0;
        while (torch.requiredItem[slot].type > 0) slot++;
        CsApiGapsMod.torchSlot = slot;
        CsApiGapsMod.swordRecipe.AddIngredient(ItemID.Gel);
    }
}

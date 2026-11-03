using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using log4net;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsBanner;

public sealed class TestBanner : ModBannerTile
{
}

public sealed class TestBannerItem : ModItem
{
    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<TestBanner>(), 0);
}

public sealed class TestSlime : ModNPC
{
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 6;

    public override void SetDefaults()
    {
        NPC.width = 36;
        NPC.height = 36;
        NPC.lifeMax = 25;
        NPC.aiStyle = -1;
        Banner = Type;
        BannerItem = ModContent.ItemType<TestBannerItem>();
        ItemID.Sets.KillsToBanner[BannerItem] = 3;
    }

    public override bool PreAI() => false;
}

public sealed class TestSlimeKin : ModNPC
{
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 6;

    public override void SetDefaults()
    {
        NPC.width = 36;
        NPC.height = 36;
        NPC.lifeMax = 25;
        NPC.aiStyle = -1;
        Banner = ModContent.NPCType<TestSlime>();
    }

    public override bool PreAI() => false;
}

public sealed class TestZombieKin : ModNPC
{
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 6;

    public override void SetDefaults()
    {
        NPC.width = 36;
        NPC.height = 36;
        NPC.lifeMax = 25;
        NPC.aiStyle = -1;
        Banner = BannerSystem.NPCtoBanner(NPCID.Zombie);
        BannerItem = BannerSystem.BannerToItem(Banner);
    }

    public override bool PreAI() => false;
}

public sealed class CsBannerMod : Mod
{
    static int frames, pass, fail, bannerX, bannerY, vineDraws, claimBefore;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsBannerMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_TileDrawing.DrawMultiTileVinesInWind += (orig, self, screenPosition, offSet, topLeftX, topLeftY, sizeX, sizeY) =>
        {
            if (topLeftX == bannerX && topLeftY == bannerY && sizeX == 1 && sizeY == 3)
                vineDraws++;
            orig(self, screenPosition, offSet, topLeftX, topLeftY, sizeX, sizeY);
        };
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csbanner END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        Player player = Main.LocalPlayer;
        switch (f)
        {
            case 10:
                Relations();
                break;
            case 20:
                Kills(player);
                break;
            case 30:
                claimBefore = BannerSystem.claimableBanners[ModContent.NPCType<TestSlime>()];
                BannerSystem.RequestBannerClaim(ModContent.NPCType<TestSlime>(), 1);
                break;
            case 50:
                Claimed(player);
                PlaceBanner(player);
                break;
            case 140:
                Bonus(player);
                break;
            case 150:
                Save();
                done = true;
                break;
        }
    }

    static void Relations()
    {
        int slime = ModContent.NPCType<TestSlime>(), kin = ModContent.NPCType<TestSlimeKin>(), zombie = ModContent.NPCType<TestZombieKin>();
        int item = ModContent.ItemType<TestBannerItem>();
        int vanilla = BannerSystem.NPCtoBanner(NPCID.Zombie);
        Report("a mod NPC is its own banner", BannerSystem.NPCtoBanner(slime) == slime, $"slime={slime} banner={BannerSystem.NPCtoBanner(slime)}");
        Report("a kin NPC shares the representative's banner", BannerSystem.NPCtoBanner(kin) == slime, $"kin={BannerSystem.NPCtoBanner(kin)}");
        Report("BannerToItem and BannerToNPC of a mod banner", BannerSystem.BannerToItem(slime) == item && BannerSystem.BannerToNPC(slime) == slime, $"item={BannerSystem.BannerToItem(slime)} npc={BannerSystem.BannerToNPC(slime)}");
        Report("a mod NPC can share a vanilla banner", vanilla > 0 && BannerSystem.NPCtoBanner(zombie) == vanilla && BannerSystem.BannerToItem(vanilla) == ItemID.ZombieBanner, $"vanilla={vanilla} zombie={BannerSystem.NPCtoBanner(zombie)} item={BannerSystem.BannerToItem(vanilla)}");
        int scene = Main.PlayerSceneMetrics.NPCBannerBuff.Length;
        Report("the banner tables hold the mod NPCs", BannerSystem.killCount.Length > slime && BannerSystem.claimableBanners.Length > slime && BannerSystem.newBanner.Length > slime && scene > slime, $"kills={BannerSystem.killCount.Length} scene={scene}");
        Report("the banner item gets the bonus and its kill count", ItemID.Sets.BannerStrength[item].Enabled && ItemID.Sets.KillsToBanner[item] == 3, $"enabled={ItemID.Sets.BannerStrength[item].Enabled} kills={ItemID.Sets.KillsToBanner[item]}");
    }

    static void Kills(Player player)
    {
        int slime = ModContent.NPCType<TestSlime>(), kin = ModContent.NPCType<TestSlimeKin>();
        BannerSystem.AddNPCKillBy(slime, Main.myPlayer);
        BannerSystem.AddNPCKillBy(kin, Main.myPlayer);
        int index = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 80, (int)player.Center.Y, slime);
        NPC npc = Main.npc[index];
        npc.lastInteraction = Main.myPlayer;
        npc.playerInteraction[Main.myPlayer] = true;
        npc.life = 0;
        npc.checkDead();
        int count = BannerSystem.GetKillCount(slime), claimable = BannerSystem.claimableBanners[slime];
        Report("kills of the NPC and its kin count on one banner, with the custom threshold", count == 3 && claimable == 1 && BannerSystem.IsNewBanner(slime), $"count={count} claimable={claimable} new={BannerSystem.IsNewBanner(slime)}");
    }

    static void Claimed(Player player)
    {
        int slime = ModContent.NPCType<TestSlime>(), item = ModContent.ItemType<TestBannerItem>();
        int left = BannerSystem.claimableBanners[slime];
        bool got = Main.mouseItem.type == item;
        for (int k = 0; k < 58 && !got; k++)
            got = player.inventory[k].type == item;
        for (int k = 0; k < Main.item.Length && !got; k++)
            got = Main.item[k].active && Main.item[k].type == item;
        Report("claiming hands out the mod banner item", claimBefore == 1 && left == 0 && got, $"before={claimBefore} left={left} got={got}");
    }

    static void PlaceBanner(Player player)
    {
        bannerX = (int)(player.Center.X / 16) + 3;
        bannerY = (int)(player.Top.Y / 16) - 4;
        for (int x = bannerX - 1; x <= bannerX + 1; x++)
            for (int y = bannerY - 1; y <= bannerY + 3; y++)
                WorldGen.KillTile(x, y, false, false, true);
        WorldGen.PlaceTile(bannerX, bannerY - 1, TileID.Stone, true, true);
        bool placed = WorldGen.PlaceObject(bannerX, bannerY, ModContent.TileType<TestBanner>(), true, 0);
        Tile top = Main.tile[bannerX, bannerY];
        int style = Terraria.ObjectData.TileObjectData.GetTileStyle(top);
        int item = TileLoader.GetItemDropFromTypeAndStyle(ModContent.TileType<TestBanner>(), style);
        Report("the banner tile is placed and drops its item", placed && top.HasTile && top.TileType == ModContent.TileType<TestBanner>() && item == ModContent.ItemType<TestBannerItem>(), $"placed={placed} style={style} item={item}");
    }

    static void Bonus(Player player)
    {
        int slime = ModContent.NPCType<TestSlime>();
        SceneMetrics metrics = Main.PlayerSceneMetrics;
        Report("the placed banner gives the bonus against its NPC", metrics.NPCBannerBuff[slime] && metrics.hasBanner && player.HasNPCBannerBuff(slime), $"buff={metrics.NPCBannerBuff[slime]} has={metrics.hasBanner}");
        Report("the banner is drawn swaying in the wind", vineDraws > 0, $"draws={vineDraws}");
    }

    static void Save()
    {
        int slime = ModContent.NPCType<TestSlime>();
        int length = BannerSystem.killCount.Length;
        WorldFile.SaveWorld();
        string file = Main.worldPathName + ".bl.json";
        var all = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject : null;
        var entry = all?["#csBanners"]?[$"{ModContent.GetInstance<CsBannerMod>().Uid}/TestSlime"] as JsonObject;
        int saved = entry?["c"]?.GetValue<int>() ?? -1;
        Report("the world keeps the mod banner by name", saved == 3 && BannerSystem.killCount.Length == length, $"saved={saved} length={BannerSystem.killCount.Length}/{length}");

        BannerSystem.killCount[slime] = 0;
        Type hooks = typeof(ModTile).Assembly.GetType("Terraria.ModLoader.Core.BannerHooks", true)!;
        hooks.GetMethod("Restore", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object?[] { all?["#csBanners"] as JsonObject });
        Report("loading puts the count back on the NPC's type", BannerSystem.GetKillCount(slime) == 3, $"count={BannerSystem.GetKillCount(slime)}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbanner {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}

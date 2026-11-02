using System;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsPageIcons;

[AutoloadHead]
public sealed class ShopNpc : ModNPC
{
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 25;

    public override void SetDefaults()
    {
        NPC.townNPC = NPC.friendly = true;
        NPC.width = 18;
        NPC.height = 40;
        NPC.lifeMax = 250;
        NPC.aiStyle = 7;
        AnimationType = NPCID.Guide;
    }

    public override bool PreAI() => false;

    public override void AddShops() => new NPCShop(Type).Add(ItemID.Torch).Register();
}

public sealed class TestSign : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSign[Type] = true;
        Main.tileFrameImportant[Type] = true;
        TileID.Sets.TileInteractRead[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.addTile(Type);
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY) => Sign.KillSign(i, j);
}

public sealed class TestSignItem : ModItem
{
    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<TestSign>(), 0);
}

public sealed class CsPageIconsMod : Mod
{
    static int frames, pass, fail, signX, floor, signId, signDraws;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPageIconsMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUINPCDialogue.DrawSign += (orig, self) =>
        {
            signDraws++;
            orig(self);
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
        if (done) Log.Info($"cspageicons END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        Player player = Main.LocalPlayer;
        switch (f)
        {
            case 10:
                ShopIcon(player);
                break;
            case 20:
                PlaceSign(player);
                break;
            case 40:
                Report("the open mod sign is drawn with its item", signDraws > 0 && player.sign == signId, $"draws={signDraws} sign={player.sign}");
                done = true;
                break;
        }
    }

    static int FindFloor(int column)
    {
        for (int y = 50; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[column, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) return y;
        }
        return Main.spawnTileY;
    }

    static void ShopIcon(Player player)
    {
        int type = ModContent.NPCType<ShopNpc>();
        int index = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 60, (int)player.Bottom.Y, type);
        NPC npc = Main.npc[index];
        player.talkNPC = index;
        Texture2D icon = GUIInstance.Active.GUIPageIcons.GetCategoryTexture(GUIPageIcons.Category.Shop, false);
        int slot = NPCHeadLoader.GetNPCHeadSlot(type);
        Texture2D head = slot >= 0 ? TextureAssets.NpcHead[slot].Value : default;
        Report("the shop page icon is the mod town NPC's head", slot >= 0 && !icon.IsNull && icon.Pointer == head.Pointer, $"slot={slot} icon={icon.Pointer:X} head={head.Pointer:X}");
        player.talkNPC = -1;
        npc.active = false;
    }

    static void PlaceSign(Player player)
    {
        signX = (int)(player.Center.X / 16) + 1;
        floor = FindFloor(signX);
        for (int x = signX - 1; x <= signX + 2; x++)
            for (int y = floor - 3; y < floor; y++)
                WorldGen.KillTile(x, y, false, false, true);
        bool placed = WorldGen.PlaceObject(signX, floor - 1, ModContent.TileType<TestSign>(), true);
        signId = Sign.ReadSign(signX, floor - 1, true);
        if (signId >= 0)
            Sign.TextSign(signId, "cspageicons");
        Type hooks = typeof(ModTile).Assembly.GetType("Terraria.ModLoader.Core.PageIconHooks", true)!;
        int item = (int)hooks.GetMethod("ItemOf", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { signX, floor - 1 })!;
        Report("the sign's item is the one that places it", placed && signId >= 0 && item == ModContent.ItemType<TestSignItem>(), $"placed={placed} sign={signId} item={item}");
        if (signId >= 0)
            player.OpenSign(signId);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cspageicons {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}

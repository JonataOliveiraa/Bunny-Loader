using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using CsTownNpc.Content;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTownNpc;

public static class Checks
{
    public static bool AllowSpawn, Veto, BlockClick, Stock, Freeze;
    public static bool ActiveNamesValid = true;
    public static int TownCount, LegacyClicks, PreClicks, PostClicks, ModernClicks, ModernPost, OwnActive, GlobalActive, ModifyCalls, GunDraw, SwingDraw;
    public static readonly List<string> Trace = new();
    static int frames, pass, fail, visible = -1;
    static bool done;
    static void Log(string text) => ModContent.GetInstance<CsTownNpcMod>().Logger.Info(text);
    public static void Reset() { frames = pass = fail = 0; done = false; visible = -1; }
    static void Report(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Log($"cstownnpc {name}: {(ok ? "ok" : "FAIL")} {detail}"); }

    public static void Frame(Player player)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 120) Run(player);
            if (frames == 135)
            {
                Report("native gun drawing reaches the filtered callback", GunDraw > 0, $"calls={GunDraw}");
                if ((uint)visible < 200u) { NPC npc = Main.npc[visible]; NPCID.Sets.AttackType[npc.type] = 3; npc.ai[0] = 15; npc.ai[1] = 60; }
            }
            if (frames == 150)
            {
                Report("native swing drawing reaches the filtered callback", SwingDraw > 0, $"calls={SwingDraw}");
                End(player);
            }
        }
        catch (Exception e) { Report("run", false, e.ToString()); End(player); }
    }

    static void End(Player player)
    {
        done = true;
        player.talkNPC = -1;
        AllowSpawn = false;
        if ((uint)visible < 200u) Main.npc[visible].active = false;
        Log($"cstownnpc END pass={pass} fail={fail}");
    }

    static NPC Spawn(Player player, int type, int offset)
    {
        int index = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + offset, (int)player.Bottom.Y, type);
        if ((uint)index >= 200u) throw new InvalidOperationException("No NPC slot for town tests");
        NPC npc = Main.npc[index];
        npc.velocity = Vector2.Zero;
        npc.homeless = false;
        npc.homeTileX = (int)npc.Center.X / 16;
        npc.homeTileY = (int)npc.Bottom.Y / 16;
        return npc;
    }

    static void Run(Player player)
    {
        int type = ModContent.NPCType<TownNpc>();
        Report("autoloaded head is registered with the native housing system", NPC.TypeToDefaultHeadIndex(type) >= 0 && NPCHeadLoader.GetHeadSlot(ModContent.GetInstance<TownNpc>().HeadTexture) == NPC.TypeToDefaultHeadIndex(type));
        AllowSpawn = false;
        Main.checkForSpawns = 7200;
        Main.UpdateTime_SpawnTownNPCs();
        Report("native town selection respects a false spawn gate", !Main.townNPCCanSpawn[type]);
        AllowSpawn = true;
        Main.checkForSpawns = 7200;
        Main.UpdateTime_SpawnTownNPCs();
        Report("native town selection marks an eligible mod NPC", Main.townNPCCanSpawn[type], $"count={TownCount}");
        int l = WorldGen.roomX1, r = WorldGen.roomX2, t = WorldGen.roomY1, b = WorldGen.roomY2;
        try
        {
            WorldGen.roomX1 = 11; WorldGen.roomX2 = 19; WorldGen.roomY1 = 22; WorldGen.roomY2 = 28;
            bool accepted = WorldGen.CheckSpecialTownNPCSpawningConditions(type);
            WorldGen.roomX2 = 20;
            Report("native room selection passes all four bounds", accepted && !WorldGen.CheckSpecialTownNPCSpawningConditions(type));
        }
        finally { WorldGen.roomX1 = l; WorldGen.roomX2 = r; WorldGen.roomY1 = t; WorldGen.roomY2 = b; }
        Report("native name selection applies the global name list", NPC.getNewNPCName(type) == "GlobalName");
        var orderedShop = new NPCShop(type, "Ordering");
        var parent = new NPCShop.Entry(ItemID.StoneBlock).OrderLast();
        var child = new NPCShop.Entry(ItemID.Wood).SortBefore(parent);
        orderedShop.Add(child, new NPCShop.Entry(ItemID.Torch).OrderLast(), parent, new NPCShop.Entry(ItemID.WoodenArrow));
        orderedShop.FinishSetup();
        Report("shop ordering keeps children with an OrderLast target", orderedShop.Entries.Select(e => e.Item.type).SequenceEqual(new[] { (int)ItemID.WoodenArrow, ItemID.Torch, ItemID.Wood, ItemID.StoneBlock }));
        var reservedShop = new NPCShop(type, "Reserved").Add(new NPCShop.Entry(ItemID.Torch, new Condition("Test.Town.Never", () => false)).ReserveSlot()).Add(ItemID.Wood);
        Item[] reserved = new Item[4];
        reservedShop.FillShop(reserved, default, out bool overflow);
        Report("reserved shop slots remain air when a condition fails", !overflow && !reserved[0].IsNull && reserved[0].type == 0 && reserved[1].type == ItemID.Wood && reserved[2].IsNull);
        NPC npc = Spawn(player, type, 80);
        visible = npc.whoAmI;
        Freeze = true;
        npc.TownNPCName = "StoredName";
        Report("TownNPCName reads and writes the native given name", npc.GivenName == "StoredName");
        Report("native chat applies mod and global text", npc.GetChat() == "town-chat:global");
        Veto = true;
        bool vetoed = !npc.CanTalk && !npc.CanBeTalkedTo;
        Veto = false;
        Report("global CanChat false vetoes both native talk queries", vetoed && npc.CanTalk && npc.CanBeTalkedTo);
        player.talkNPC = npc.whoAmI;
        GUINPCDialogue dialog = GUIInstance.Active.GUINPCDialogue;
        GameString button = "", button2 = "";
        Microsoft.Xna.Framework.Graphics.Texture2D icon = default, icon2 = default;
        int cost = 0; bool happy = true;
        dialog.SetupButtonText(ref button, ref icon, ref button2, ref icon2, ref cost, ref happy);
        Report("native dialogue displays both legacy buttons", button.ToString() == "Test shop" && button2.ToString() == "Test action", $"first={button} second={button2}");
        BlockClick = true;
        dialog.Option2Clicked();
        BlockClick = false;
        Report("global pre-click veto prevents action and post callbacks", LegacyClicks == 0 && PostClicks == 0 && PreClicks == 1);
        dialog.Option2Clicked();
        Report("legacy action runs before the global post callback", LegacyClicks == 1 && PostClicks == 1 && Main.npcChatText == "legacy-click");
        Stock = false;
        dialog.Option1Clicked(0);
        int ownShop = Main.npcShop;
        var items = Main.instance.shop[ownShop].item;
        bool arrow = Find(items, ItemID.WoodenArrow, out Item item);
        Report("legacy shop action opens the registered mod shop", ownShop > 25 && arrow, $"index={ownShop}");
        Report("shop conditions hide unavailable items", !Find(items, ItemID.Torch, out _));
        Report("mod and global ModifyActiveShop change live items", arrow && item.shopCustomPrice.HasValue && item.shopCustomPrice.Value == 123 && item.stack == 7 && OwnActive > 0 && GlobalActive > 0 && ActiveNamesValid);
        Stock = true;
        Main.instance.OpenShop(ownShop);
        Report("shop conditions refresh on each OpenShop", Find(Main.instance.shop[ownShop].item, ItemID.Torch, out _));
        NPC merchant = Spawn(player, NPCID.Merchant, 150);
        try
        {
            player.talkNPC = merchant.whoAmI;
            Main.instance.OpenShop(1);
            int calls = ModifyCalls;
            bool added = Find(Main.instance.shop[1].item, ItemID.Amethyst, out _);
            bool removed = !Find(Main.instance.shop[1].item, ItemID.Torch, out _);
            Stock = false;
            Main.instance.OpenShop(1);
            Report("ModifyShop edits the native merchant inventory once", added && removed && ModifyCalls == calls);
            Report("global shop conditions are reevaluated on reopen", !Find(Main.instance.shop[1].item, ItemID.Amethyst, out _));
        }
        finally { merchant.active = false; }
        NPC modern = Spawn(player, ModContent.NPCType<ModernNpc>(), 120);
        try
        {
            player.talkNPC = modern.whoAmI;
            dialog.SetupButtonText(ref button, ref icon, ref button2, ref icon2, ref cost, ref happy);
            bool label = button.ToString() == "Modern action";
            dialog.Option1Clicked(0);
            Report("local tML NPCInteraction buttons work through the mobile dialogue", label && ModernClicks == 1 && ModernPost == 1 && Main.npcChatText == "modern-click");
        }
        finally { modern.active = false; }
        player.talkNPC = npc.whoAmI;
        npc.Happiness.SetBiomeAffection<ForestBiome>(AffectionLevel.Love);
        float love = Main.ShopHelper.GetShoppingSettings(player, npc).PriceAdjustment;
        npc.Happiness.SetBiomeAffection<ForestBiome>(AffectionLevel.Hate);
        float hate = Main.ShopHelper.GetShoppingSettings(player, npc).PriceAdjustment;
        Report("native biome happiness changes the shopping price", player.ShoppingZone_Forest && hate > love, $"forest={player.ShoppingZone_Forest} love={love} hate={hate}");
        npc.Happiness.SetBiomeAffection<ForestBiome>(0);
        float neutral = Main.ShopHelper.GetShoppingSettings(player, npc).PriceAdjustment;
        Report("zero affection removes the previous native relationship", neutral > love && neutral < hate, $"neutral={neutral}");
        npc.Happiness.SetNPCAffection(NPCID.Guide, AffectionLevel.Love).SetNPCAffection(NPCID.Guide, AffectionLevel.Hate).SetNPCAffection(NPCID.Guide, 0);
        var modifiers = new GameList(Main.ShopHelper._database.GetByNPCID(type).ShopModifiers);
        int duplicates = 0;
        for (int i = 0; i < modifiers.Count; i++)
        {
            var value = new GameObject(modifiers.Get<IntPtr>(i));
            if (NPCPreferenceTrait.Is(value) && new NPCPreferenceTrait(value.Pointer).NpcId == NPCID.Guide) duplicates++;
        }
        Report("NPC affection replaces and removes native traits", duplicates == 0);
        player.talkNPC = -1;
        for (int mode = 0; mode < 4; mode++) Attack(npc, mode);
        NPCID.Sets.AttackType[type] = 0;
        for (int i = 0; i < 8; i++) WarmAttack(npc);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) WarmAttack(npc);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Report("100 warm native attack callbacks allocate no managed memory", allocated == 0, $"bytes={allocated}");
        NPCID.Sets.AttackType[type] = 1;
        npc.ai[0] = 12; npc.ai[1] = 60;
        Main.instance.LoadItem(ItemID.WoodenBow);
        Main.instance.LoadItem(ItemID.CopperShortsword);
    }

    static void WarmAttack(NPC npc)
    {
        Trace.Clear();
        npc.ai[0] = 10; npc.ai[1] = 60; npc.localAI[3] = 5;
        npc.AI_007_TownEntities();
    }

    static bool Find(GameArray<Item> items, int type, out Item found)
    {
        for (int i = 0; i < items.Length; i++) if (!items[i].IsNull && items[i].type == type) { found = items[i]; return true; }
        found = default;
        return false;
    }

    static void Attack(NPC npc, int mode)
    {
        Trace.Clear();
        int[] states = { 10, 12, 14, 15 };
        NPCID.Sets.AttackType[npc.type] = mode;
        npc.ai[0] = states[mode]; npc.ai[1] = 60; npc.ai[2] = 0;
        npc.localAI[3] = 0;
        npc.direction = npc.spriteDirection = 1;
        npc.AI_007_TownEntities();
        string expected = mode == 3 ? "strength,global-strength,cooldown,swing" : "strength,global-strength,cooldown,projectile,speed" + (mode == 1 ? ",shoot" : mode == 2 ? ",magic" : "");
        Report($"native attack stage {mode} follows tML callback order", string.Join(",", Trace) == expected, string.Join(",", Trace));
        if (mode == 3) return;
        int shots = 0;
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!projectile.active || !projectile.npcProj || projectile.type != ProjectileID.WoodenArrowFriendly) continue;
            if (projectile.damage >= 21 && Math.Abs(projectile.knockBack - 5) < 0.01f && projectile.velocity.Length() > 11.9f) shots++;
            projectile.active = false;
        }
        Report($"native attack stage {mode} uses the modified projectile and stats", shots == 1, $"shots={shots}");
    }
}

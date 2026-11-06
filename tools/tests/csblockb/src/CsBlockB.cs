using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBlockB;

public sealed class ForgeItem : ModItem
{
    public static int Crafted, Pre, Post, Applied, Auto, Fished;
    public static bool Refuse;

    public int Data;

    public override string Texture => "Terraria/Images/Item_4";

    public override void AutoDefaults() => Auto++;

    public override void SetDefaults() => Item.CloneDefaults(ItemID.IronBroadsword);

    public override void OnCraft(Recipe recipe) => Crafted++;

    public override bool ReforgePrice(ref long reforgePrice, ref bool canApplyDiscount)
    {
        reforgePrice = 12345;
        return false;
    }

    public override bool CanReforge() => !Refuse;

    public override void PreReforge() => Pre++;

    public override void PostReforge() => Post++;

    public override void ApplyPrefix(int pre) => Applied = pre;

    public override void CaughtFishStack(ref int stack)
    {
        Fished++;
        stack = 7;
    }

    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Wood).Register();
}

public sealed class StaffItem : ModItem
{
    public static int Modified;

    public override string Texture => "Terraria/Images/Item_739";

    public override void SetStaticDefaults() => Item.staff[Type] = true;

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.AmethystStaff);
        Item.useStyle = ItemUseStyleID.Shoot;
    }

    public override Vector2? HoldoutOrigin() => new Vector2(5, 3);

    public override bool ModifyItemDraw(ref PlayerDrawSet drawInfo, ref DrawData drawData, ref DrawData? coloredDrawData, ref DrawData? glowMaskDrawData)
    {
        Modified++;
        drawInfo.DrawDataCache.Add(drawData with { scale = new Vector2(3, 3) });
        drawData.rotation += 1;
        return true;
    }
}

public sealed class GlobalForge : GlobalItem
{
    public static int PreDraw, PostDraw, Fished, Applied;

    public override void PreModifyItemDraw(Item item, ref PlayerDrawSet drawInfo, ref DrawData drawData, ref DrawData? coloredDrawData, ref DrawData? glowMaskDrawData) => PreDraw++;

    public override void PostModifyItemDraw(Item item, ref PlayerDrawSet drawInfo, DrawData drawData, DrawData? coloredDrawData, DrawData? glowmaskDrawData) => PostDraw++;

    public override void CaughtFishStack(int type, ref int stack)
    {
        if (type == ItemID.Bass)
        {
            Fished++;
            stack = 9;
        }
    }

    public override void ApplyPrefix(Item item, int pre)
    {
        if (pre > 0) Applied++;
    }
}

public sealed class ControlsPlayer : ModPlayer
{
    public static bool Press;

    public static int Calls;

    public override void SetControls()
    {
        Calls++;
        if (Press) Player.controlLeft = true;
    }
}

public sealed class LoadSystem : ModSystem
{
    public static int OnLoad, PostLoad;

    public static bool Order;

    public override void OnWorldLoad() => OnLoad++;

    public override void PostWorldLoad()
    {
        Order = OnLoad > PostLoad;
        PostLoad++;
    }
}

public sealed class CountingWall : GlobalWall
{
    public static int Kill, Num, Sound, Frame, Place, Converted, Teleport, DropCalls;
    public static bool Block;

    public override void KillWall(int i, int j, int type, ref bool fail) => Kill++;

    public override void NumDust(int i, int j, int type, bool fail, ref int num) => Num++;

    public override bool KillSound(int i, int j, int type, bool fail)
    {
        Sound++;
        return true;
    }

    public override bool WallFrame(int i, int j, int type, bool randomizeFrame, ref int style, ref int frameNumber)
    {
        Frame++;
        return true;
    }

    public override void PlaceInWorld(int i, int j, int type, Item item) => Place++;

    public override bool Drop(int i, int j, int type, ref int dropType)
    {
        DropCalls++;
        dropType = ItemID.Gel;
        return true;
    }

    public override void OnWallConverted(int i, int j, int fromType, int toType, int conversionType) => Converted++;

    public override bool CanBeTeleportedTo(int i, int j, int type, Player player, string context)
    {
        Teleport++;
        return !Block;
    }
}

public sealed class ConvertWall : ModWall
{
    public static int Converted;


    public override void SetStaticDefaults() => AddMapEntry(Color.Gray);

    public override void OnWallConverted(int i, int j, int fromType, int toType, int conversionType) => Converted++;
}

public sealed class ReplaceBlock : ModTile
{
    public static int Replaced, GlobalReplaced;


    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        AddMapEntry(Color.Gray);
    }

    public override void ReplaceTile(int i, int j, int targetType, int targetStyle) => Replaced++;
}

public sealed class ReplaceGlobal : GlobalTile
{
    public override void ReplaceTile(int i, int j, int type, int targetType, int targetStyle) => ReplaceBlock.GlobalReplaced++;
}

public sealed class HappyNPC : GlobalNPC
{
    public static int Calls;

    public override void ModifyNPCHappiness(NPC npc, int primaryPlayerBiome, Terraria.GameContent.ShopHelper shopHelperInstance, bool[] nearbyNPCsByType) => Calls++;
}

public sealed class CsBlockBMod : Mod
{
    static int frame, pass, fail, startX, popupFrames, staffCount, plainOrigin, postBefore, drawIndex;
    static float staffOriginX, staffOriginY, plainOriginX, plainOriginY;
    static bool forceClick, measureStaff, popupDrawing, staffFlag, lastDisabled;

    static ILog Log => ModContent.GetInstance<CsBlockBMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_PlayerDrawLayers.DrawPlayer_27_HeldItem += Measure;
        On_GUITransactionButton.Draw_TransactionButton_Layout_Item_string_bool_reffloat_bool_bool_bool_bool += ForceDraw;
        On_GUIReforgePopup.Draw += PopupDraw;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csblockb {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static GUITransactionButton.InputState ForceDraw(On_GUITransactionButton.orig_Draw_TransactionButton_Layout_Item_string_bool_reffloat_bool_bool_bool_bool orig, TransactionButton_Layout layout, Item item, string? label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)
    {
        var state = orig(layout, item, label, disabled, ref scale, forcedPressed, hasControllerFocus, forceOver, disablePressedState);
        if (!popupDrawing || ++drawIndex != 2)
            return state;
        lastDisabled = disabled;
        if (forceClick && !disabled)
        {
            forceClick = false;
            Log.Info($"csblockb forced the button '{label}' disabled={disabled}");
            return GUITransactionButton.InputState.Clicked;
        }
        return state;
    }

    static void PopupDraw(On_GUIReforgePopup.orig_Draw orig, GUIReforgePopup self)
    {
        popupDrawing = true;
        popupFrames++;
        drawIndex = 0;
        try { orig(self); }
        finally { popupDrawing = false; }
    }

    static void Measure(On_PlayerDrawLayers.orig_DrawPlayer_27_HeldItem orig, PlayerDrawSet drawInfo)
    {
        int before = drawInfo.DrawDataCacheCount;
        orig(drawInfo);
        int added = drawInfo.DrawDataCacheCount - before;
        if (!measureStaff || added <= 0 || drawInfo.drawPlayer.whoAmI != Main.myPlayer)
            return;
        DrawData last = drawInfo.DrawDataCache[drawInfo.DrawDataCacheCount - 1];
        if (drawInfo.heldItem.type == ModContent.ItemType<StaffItem>())
        {
            staffCount = added;
            staffOriginX = last.origin.X;
            staffOriginY = last.origin.Y;
            staffFlag = Item.staff[drawInfo.heldItem.type] && drawInfo.heldItem.useStyle == 5;
        }
        else if (drawInfo.heldItem.type == ItemID.AmethystStaff)
        {
            plainOrigin = added;
            plainOriginX = last.origin.X;
            plainOriginY = last.origin.Y;
        }
    }

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
                Report("PostWorldLoad runs after OnWorldLoad", LoadSystem.PostLoad >= 1 && LoadSystem.Order, $"on={LoadSystem.OnLoad} post={LoadSystem.PostLoad}");
                Report("AutoDefaults runs before SetDefaults", new Item(ModContent.ItemType<ForgeItem>()).useStyle == ItemUseStyleID.Swing && ForgeItem.Auto > 0, $"auto={ForgeItem.Auto}");
                Crafting(self);
                Fishing(self);
                break;
            case 40:
                Prefixes(self);
                break;
            case 50:
                startX = (int)self.position.X;
                ControlsPlayer.Press = true;
                break;
            case 80:
                ControlsPlayer.Press = false;
                Report("SetControls changes the player's controls", ControlsPlayer.Calls > 10 && self.position.X < startX, $"calls={ControlsPlayer.Calls} moved={self.position.X - startX}");
                self.inventory[self.selectedItem].SetDefaults(ItemID.AmethystStaff, default);
                self.itemAnimation = 200;
                self.itemTime = 200;
                measureStaff = true;
                break;
            case 95:
                self.inventory[self.selectedItem].SetDefaults(ModContent.ItemType<StaffItem>(), default);
                self.itemAnimation = 200;
                self.itemTime = 200;
                break;
            case 110:
                measureStaff = false;
                self.itemAnimation = 0;
                self.itemTime = 0;
                Report("HoldoutOrigin moves the staff's origin", plainOrigin > 0 && staffCount > 0 && Math.Abs(staffOriginX - plainOriginX - 5 * self.direction) < 0.01f, $"plain={plainOriginX},{plainOriginY} staff={staffOriginX},{staffOriginY} flag={staffFlag} dir={self.direction}");
                Report("ModifyItemDraw adds behind and keeps the item", StaffItem.Modified > 0 && staffCount >= plainOrigin + 1, $"calls={StaffItem.Modified} entries={staffCount} plain={plainOrigin}");
                Report("Pre and PostModifyItemDraw run", GlobalForge.PreDraw > 0 && GlobalForge.PostDraw > 0, $"pre={GlobalForge.PreDraw} post={GlobalForge.PostDraw}");
                self.inventory[self.selectedItem].TurnToAir();
                Walls(self);
                break;
            case 130:
                Replacing(self);
                Happiness(self);
                break;
            case 150:
                OpenReforge(self);
                break;
            case 170:
                GUIInstance.Active.GUIReforge.Reset();
                GUIInstance.Active.GUIReforgePopup.OpenPopup();
                Log.Info($"csblockb popup open={GUIInstance.Active.GUIReforgePopup.PopupDisplayed} item={GUIInstance.Active.GUIReforge.ReforgeItem.type} frames={popupFrames}");
                Log.Info("csblockb SHOT open");
                break;
            case 180:
                forceClick = true;
                break;
            case 200:
                Report("the popup runs PreReforge and PostReforge", ForgeItem.Pre >= 1 && ForgeItem.Post >= 1, $"pre={ForgeItem.Pre} post={ForgeItem.Post} popupFrames={popupFrames}");
                int kept = -1, forged = ModContent.ItemType<ForgeItem>();
                for (int k = 0; k < 58; k++)
                    if (self.inventory[k].type == forged)
                        kept = Math.Max(kept, (self.inventory[k].ModItem as ForgeItem)!.Data);
                Log.Info($"csblockb forged mouse={Main.mouseItem.type} mouseData={(Main.mouseItem.ModItem as ForgeItem)?.Data} kept={kept}");
                Report("reforging keeps the item's mod data", kept == 42, $"data={kept}");
                Log.Info("csblockb SHOT reforge");
                self.inventory[self.selectedItem].SetDefaults(ModContent.ItemType<ForgeItem>(), default);
                ForgeItem.Refuse = true;
                GUIInstance.Active.GUIReforge.Reset();
                GUIInstance.Active.GUIReforgePopup.OpenPopup();
                break;
            case 215:
                postBefore = ForgeItem.Post;
                forceClick = true;
                break;
            case 230:
                forceClick = false;
                Report("CanReforge false disables the button", lastDisabled && ForgeItem.Post == postBefore, $"disabled={lastDisabled} post={ForgeItem.Post} before={postBefore}");
                GUIInstance.Active.GUIReforgePopup.ClosePopup();
                GUIInstance.Active.GUIPageIcons.Close();
                Main.InReforgeMenu = false;
                Main.playerInventory = false;
                Log.Info($"csblockb END pass={pass} fail={fail}");
                break;
        }
    }

    static void Crafting(Player self)
    {
        Recipe? found = null;
        for (int k = 0; k < Recipe.numRecipes; k++)
            if (Main.recipe[k].createItem.type == ModContent.ItemType<ForgeItem>())
                found = Main.recipe[k];
        Item result = new Item(ModContent.ItemType<ForgeItem>());
        Main.CraftItem_GrantItem(found!.Value, result, false);
        Report("OnCraft runs when crafting", ForgeItem.Crafted == 1, $"crafted={ForgeItem.Crafted}");
    }

    static void Fishing(Player self)
    {
        int index = Projectile.NewProjectile(self.GetSource_FromThis(), self.Center, Vector2.Zero, ProjectileID.WoodenArrowFriendly, 0, 0, self.whoAmI);
        Projectile bobber = Main.projectile[index];
        int before = self.CountItem(ItemID.Bass, 999);
        bobber.AI_061_FishingBobber_GiveItemToPlayer(self, ItemID.Bass);
        int after = self.CountItem(ItemID.Bass, 999);
        Report("GlobalItem.CaughtFishStack changes the caught stack", GlobalForge.Fished == 1 && after - before == 9, $"calls={GlobalForge.Fished} got={after - before}");
        bobber.AI_061_FishingBobber_GiveItemToPlayer(self, ModContent.ItemType<ForgeItem>());
        Report("ModItem.CaughtFishStack runs", ForgeItem.Fished == 1, $"calls={ForgeItem.Fished}");
        bobber.Kill();
    }

    static void Prefixes(Player self)
    {
        var item = new Item(ModContent.ItemType<ForgeItem>());
        bool rolled = item.Prefix(PrefixID.Legendary);
        Report("ApplyPrefix runs after a prefix is applied", rolled && ForgeItem.Applied == PrefixID.Legendary && GlobalForge.Applied > 0, $"rolled={rolled} applied={ForgeItem.Applied} global={GlobalForge.Applied}");
        GUIReforge reforge = GUIInstance.Active.GUIReforge;
        int mine = reforge.ItemReforgeCost(item);
        var iron = new Item(ItemID.IronBroadsword);
        int vanilla = reforge.ItemReforgeCost(iron);
        Report("ReforgePrice replaces the cost and leaves vanilla items alone", mine == 12345 && vanilla > 0 && vanilla != 12345, $"mine={mine} vanilla={vanilla}");
    }

    static void Walls(Player self)
    {
        int x = (int)(self.Center.X / 16) + 12, y = (int)(self.Center.Y / 16) - 8;
        WorldGen.PlaceWall(x, y, WallID.Stone, true);
        Report("GlobalWall.PlaceInWorld runs", CountingWall.Place == 1, $"calls={CountingWall.Place} wall={Main.tile[x, y].wall}");
        Framing.WallFrame(x, y, true);
        Report("GlobalWall.WallFrame runs for a vanilla wall", CountingWall.Frame > 0, $"calls={CountingWall.Frame}");
        WorldGen.Convert(x, y, BiomeConversionID.Corruption, 0);
        Report("GlobalWall.OnWallConverted runs when a wall converts", CountingWall.Converted >= 1, $"calls={CountingWall.Converted} wall={Main.tile[x, y].wall}");
        int gelBefore = CountGround(ItemID.Gel);
        WorldGen.KillWall(x, y);
        Report("GlobalWall kill hooks run on a vanilla wall", CountingWall.Kill == 1 && CountingWall.Num == 1 && CountingWall.Sound == 1 && CountingWall.DropCalls == 1, $"kill={CountingWall.Kill} num={CountingWall.Num} sound={CountingWall.Sound} drop={CountingWall.DropCalls}");
        Report("GlobalWall.Drop replaces the drop", CountGround(ItemID.Gel) > gelBefore, $"gel={CountGround(ItemID.Gel)}");
        int modWall = ModContent.WallType<ConvertWall>();
        WorldGen.PlaceWall(x, y, modWall, true);
        WorldGen.Convert_ActuallyConvertWall(0, x, y, ref Main.tile[x, y], WallID.Stone);
        Report("ModWall.OnWallConverted runs for the wall that left", ConvertWall.Converted == 1, $"calls={ConvertWall.Converted}");
        WorldGen.KillWall(x, y);
        Vector2 start = self.position;
        CountingWall.Block = true;
        WorldGen.PlaceWall(x, y, WallID.Stone, true);
        self.Teleport(new Vector2(x * 16, y * 16), 1);
        bool stayed = Vector2.Distance(self.position, start) < 32;
        CountingWall.Block = false;
        WorldGen.KillWall(x, y);
        Report("GlobalWall.CanBeTeleportedTo can refuse", stayed && CountingWall.Teleport >= 1, $"stayed={stayed} calls={CountingWall.Teleport}");
    }

    static void Replacing(Player self)
    {
        int x = (int)(self.Center.X / 16) + 10, y = (int)(self.Center.Y / 16) - 4;
        WorldGen.PlaceTile(x, y, ModContent.TileType<ReplaceBlock>(), true, true);
        bool placed = Main.tile[x, y].TileType == ModContent.TileType<ReplaceBlock>();
        self.inventory[self.selectedItem].SetDefaults(ItemID.IronPickaxe, default);
        WorldGen.ReplaceTile_DoActualReplacement(TileID.Stone, 0, x, y, Main.tile[x, y]);
        Report("ReplaceTile runs on the ModTile and the GlobalTile", placed && ReplaceBlock.Replaced == 1 && ReplaceBlock.GlobalReplaced == 1, $"placed={placed} mod={ReplaceBlock.Replaced} global={ReplaceBlock.GlobalReplaced}");
        WorldGen.KillTile(x, y, false, false, true);
        self.inventory[self.selectedItem].TurnToAir();
    }

    static void Happiness(Player self)
    {
        int index = NPC.NewNPC(self.GetSource_FromThis(), (int)self.Center.X + 40, (int)self.Center.Y, NPCID.Guide);
        Main.ShopHelper.GetShoppingSettings(self, Main.npc[index]);
        Report("ModifyNPCHappiness runs when the game reads an NPC's mood", HappyNPC.Calls > 0, $"calls={HappyNPC.Calls}");
        Main.npc[index].active = false;
    }

    static int CountGround(int type)
    {
        int total = 0;
        for (int k = 0; k < Main.maxItems; k++)
            if (Main.item[k].active && Main.item[k].type == type)
                total += Main.item[k].stack;
        return total;
    }

    static void OpenReforge(Player self)
    {
        self.inventory[self.selectedItem].SetDefaults(ModContent.ItemType<ForgeItem>(), default);
        ((ForgeItem)self.inventory[self.selectedItem].ModItem).Data = 42;
        self.inventory[49].SetDefaults(ItemID.PlatinumCoin, default);
        self.inventory[49].stack = 9;
        int goblin = NPC.NewNPC(self.GetSource_FromThis(), (int)self.Center.X + 20, (int)self.Center.Y, NPCID.GoblinTinkerer);
        self.SetTalkNPC(goblin);
        Main.npcChatText = "";
        Main.playerInventory = true;
        Main.InReforgeMenu = true;
        GUIInstance.Active.GUIPageIcons.OpenUI(GUIPageIcons.Category.Inventory, GUIPageIcons.Category.Reforge);
    }
}

using System;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CsIssues;

public sealed class HitPlayer : ModPlayer
{
    public static int FriendlyHits, HostileHits;

    public static bool BlockGrapple;

    public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (proj.type == ProjectileID.EyeLaser) HostileHits++;
        if (proj.type == ProjectileID.WoodenArrowFriendly) FriendlyHits++;
    }

    public override bool CanUseItem(Item item) => !(BlockGrapple && item.type == ItemID.GrapplingHook);
}

public sealed class HitGlobal : GlobalNPC
{
    public static int LaserHits, ArrowHits;

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (projectile.type == ProjectileID.EyeLaser) LaserHits++;
        if (projectile.type == ProjectileID.WoodenArrowFriendly) ArrowHits++;
    }
}

public sealed class SpawnMark : GlobalNPC
{
    public static int Calls;

    public override bool InstancePerEntity => true;

    public int Mark;

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        if (npc.type != NPCID.BlueSlime) return;
        Mark = 7;
        Calls++;
    }
}

public sealed class SpearPrefix : GlobalItem
{
    public override int ChoosePrefix(Item item, UnifiedRandom rand) => item.type == ItemID.Spear ? PrefixID.Legendary : -1;
}

public sealed class TestBall : ModProjectile
{
    public static int Collisions;

    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GolfBallDyedBlack;

    public override void SetStaticDefaults() => ProjectileID.Sets.IsAGolfBall[Type] = true;

    public override void SetDefaults() => Projectile.CloneDefaults(ProjectileID.GolfBallDyedBlack);

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Collisions++;
        return false;
    }
}

public sealed class SwingItem : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.WoodenSword;

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 30;
        Item.useAnimation = 30;
    }
}

public sealed class RecipeSystem : ModSystem
{
    public static Recipe Edited;

    public override void AddRecipes()
    {
        Recipe recipe = Recipe.Create(ItemID.Torch, 3);
        recipe.AddIngredient(ItemID.DirtBlock);
        recipe.AddRecipeGroup(RecipeGroups.Wood);
        recipe.Register();
        recipe.RemoveRecipeGroup(RecipeGroups.Wood.RegisteredId);
        Edited = recipe;
    }
}

public sealed class CsIssuesMod : Mod
{
    const int Start = 60;

    static int frame, pass, fail, guide = -1, dummy = -1, slime = -1, ball = -1;
    static bool done, use;
    static Item saved;

    static ILog Log => ModContent.GetInstance<CsIssuesMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csissues {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (!Main.gameMenu && self.whoAmI == Main.myPlayer && frame >= Start)
        {
            self.controlUseItem = use;
            self.releaseUseItem = !use;
        }
        orig(self);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        orig(self, i);
        if (i != Main.myPlayer || done) return;
        self.creativeGodMode = true;
        try { Step(self, ++frame); }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish(self);
        }
    }

    static void Step(Player self, int f)
    {
        if (f == Start) Recipes();
        if (f == Start + 1) Prefixes();
        if (f == Start + 2) SpawnPersists(self);
        if (f == Start + 3) SpawnOnlyOnce();
        if (f == Start + 4) QuickGrapple(self);
        if (f == Start + 32) Hits(self);
        if (f == Start + 40) CheckHits();
        if (f == Start + 41) Golf(self);
        if (f == Start + 140) CheckGolf();
        if (f == Start + 141) HoldSwing(self);
        if (f == Start + 150)
        {
            self.inventory[self.selectedItem].noUseGraphic = true;
            use = true;
        }
        if (f == Start + 152) use = false;
        if (f == Start + 160) CheckVisual(self);
        if (f == Start + 200) Finish(self);
    }

    static void Recipes()
    {
        Recipe recipe = RecipeSystem.Edited;
        Report("RemoveRecipeGroup drops the placeholder ingredient",
            !recipe.IsNull && !recipe.HasIngredient(ItemID.Wood) && recipe.HasIngredient(ItemID.DirtBlock) && !recipe.HasRecipeGroup(RecipeGroups.Wood.RegisteredId),
            recipe.IsNull ? "no recipe" : $"wood={recipe.HasIngredient(ItemID.Wood)} dirt={recipe.HasIngredient(ItemID.DirtBlock)} group={recipe.HasRecipeGroup(RecipeGroups.Wood.RegisteredId)}");
    }

    static void Prefixes()
    {
        Item spear = Item.New();
        spear.SetDefaults(ItemID.Spear, default);
        bool vanillaRoll = spear.CanRollPrefix(PrefixID.Legendary);
        spear.Prefix(-2);
        Report("ChoosePrefix forces a vanilla prefix the item cannot roll", spear.prefix == PrefixID.Legendary,
            $"prefix={spear.prefix} canRollLegendary={vanillaRoll}");
    }

    static void SpawnPersists(Player self)
    {
        int before = SpawnMark.Calls;
        slime = NPC.NewNPC(self.GetSource_FromThis(), (int)self.Center.X + 200, (int)self.Center.Y - 40, NPCID.BlueSlime);
        NPC npc = Main.npc[slime];
        int afterSpawn = SpawnMark.Calls - before;
        npc.SetDefaults(-3, default);
        int mark = npc.GetGlobalNPC<SpawnMark>().Mark;
        Report("OnSpawn survives SetDefaults right after NewNPC", mark == 7 && npc.netID == -3,
            $"mark={mark} netID={npc.netID} callsAtSpawn={afterSpawn} calls={SpawnMark.Calls - before}");
    }

    static void SpawnOnlyOnce()
    {
        NPC npc = Main.npc[slime];
        int before = SpawnMark.Calls;
        npc.SetDefaults(NPCID.BlueSlime, default);
        int mark = npc.GetGlobalNPC<SpawnMark>().Mark;
        Report("a later SetDefaults does not run OnSpawn again", SpawnMark.Calls == before && mark == 0, $"calls+={SpawnMark.Calls - before} mark={mark}");
        npc.active = false;
    }

    static void QuickGrapple(Player self)
    {
        saved = self.miscEquips[4].Clone();
        self.miscEquips[4].SetDefaults(ItemID.GrapplingHook, default);
        self.mount.SetMount(MountID.Bunny, self);
        bool mounted = self.mount.Active;
        HitPlayer.BlockGrapple = true;
        self.QuickGrapple();
        bool kept = self.mount.Active;
        HitPlayer.BlockGrapple = false;
        self.QuickGrapple();
        bool control = self.mount.Active;
        Report("quick grapple keeps the mount when CanUseItem refuses", mounted && kept,
            $"mounted={mounted} kept={kept} allowedStillMounted={control}");
        if (self.mount.Active) self.mount.Dismount(self);
        KillGrapples(self);
        self.miscEquips[4] = saved;
    }

    static void KillGrapples(Player self)
    {
        for (int k = 0; k < Main.maxProjectiles; k++)
        {
            Projectile p = Main.projectile[k];
            if (p.active && p.owner == self.whoAmI && p.aiStyle == 7) p.Kill();
        }
    }

    static void Hits(Player self)
    {
        Vector2 left = self.Center + new Vector2(-160, -24), right = self.Center + new Vector2(160, -24);
        guide = NPC.NewNPC(self.GetSource_FromThis(), (int)left.X, (int)left.Y, NPCID.Guide);
        dummy = NPC.NewNPC(self.GetSource_FromThis(), (int)right.X, (int)right.Y, NPCID.BlueSlime);
        Main.npc[dummy].lifeMax = Main.npc[dummy].life = 5000;
        Projectile.NewProjectile(self.GetSource_FromThis(), Main.npc[guide].Center, new Vector2(0.1f, 0), ProjectileID.EyeLaser, 20, 0, Main.myPlayer);
        Projectile.NewProjectile(self.GetSource_FromThis(), Main.npc[dummy].Center, new Vector2(0.1f, 0), ProjectileID.WoodenArrowFriendly, 20, 0, Main.myPlayer);
    }

    static void CheckHits()
    {
        Report("a hostile projectile skips the owner's OnHitNPCWithProj", HitGlobal.LaserHits > 0 && HitPlayer.HostileHits == 0,
            $"laserHitsNpc={HitGlobal.LaserHits} playerHook={HitPlayer.HostileHits}");
        Report("a friendly projectile still runs the owner's OnHitNPCWithProj", HitPlayer.FriendlyHits > 0 && HitPlayer.FriendlyHits == HitGlobal.ArrowHits,
            $"playerHook={HitPlayer.FriendlyHits} arrowHitsNpc={HitGlobal.ArrowHits} dummyActive={(dummy >= 0 && Main.npc[dummy].active)} dummyType={(dummy >= 0 ? Main.npc[dummy].type : -1)}");
        foreach (int index in new[] { guide, dummy })
            if (index >= 0 && Main.npc[index].active) Main.npc[index].active = false;
        for (int k = 0; k < Main.maxProjectiles; k++)
        {
            Projectile p = Main.projectile[k];
            if (p.active && (p.type == ProjectileID.EyeLaser || p.type == ProjectileID.WoodenArrowFriendly)) p.Kill();
        }
    }

    static void Golf(Player self)
    {
        ball = Projectile.NewProjectile(self.GetSource_FromThis(), self.Center + new Vector2(48, -80), new Vector2(1, 3), ModContent.ProjectileType<TestBall>(), 0, 0, Main.myPlayer);
    }

    static void CheckGolf()
    {
        Projectile p = ball >= 0 ? Main.projectile[ball] : default;
        Report("a golf ball calls OnTileCollide", TestBall.Collisions > 0, $"collisions={TestBall.Collisions} active={(!p.IsNull && p.active)} aiStyle={(p.IsNull ? -1 : p.aiStyle)}");
        if (!p.IsNull && p.active && p.type == ModContent.ProjectileType<TestBall>()) p.Kill();
    }

    static void HoldSwing(Player self)
    {
        saved = self.inventory[self.selectedItem].Clone();
        self.inventory[self.selectedItem].SetDefaults(ModContent.ItemType<SwingItem>(), default);
    }

    static void CheckVisual(Player self)
    {
        Item visual = self.lastVisualizedSelectedItem;
        Report("the held copy takes noUseGraphic set before the use", !visual.IsNull && visual.noUseGraphic,
            $"type={(visual.IsNull ? -1 : visual.type)} noUseGraphic={(!visual.IsNull && visual.noUseGraphic)} animation={self.itemAnimation}");
    }

    static void Finish(Player self)
    {
        done = true;
        use = false;
        if (!saved.IsNull && frame > Start + 141) self.inventory[self.selectedItem] = saved;
        Log.Info($"csissues pass={pass} fail={fail}");
        Log.Info("csissues END");
    }
}

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTownNpc.Content;

[AutoloadHead]
public class TownNpc : ModNPC
{
    public override string Texture => "CsTownNpc/Content/TownNpc";
    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 25;
        NPCID.Sets.ExtraFramesCount[Type] = 9;
        NPCID.Sets.AttackFrameCount[Type] = 4;
        NPCID.Sets.AttackTime[Type] = 90;
        NPCID.Sets.AttackType[Type] = 0;
        NPCID.Sets.DangerDetectRange[Type] = 700;
        NPC.Happiness.SetBiomeAffection<ForestBiome>(AffectionLevel.Like).SetNPCAffection(NPCID.Guide, AffectionLevel.Like);
    }
    public override void SetDefaults()
    {
        NPC.townNPC = NPC.friendly = true;
        NPC.width = 18; NPC.height = 40; NPC.lifeMax = 250;
        NPC.damage = 0; NPC.defense = 15; NPC.aiStyle = 7;
        AnimationType = NPCID.Guide;
    }
    public override bool PreAI() => !Checks.Freeze;
    public override bool CanTownNPCSpawn(int numTownNPCs) { Checks.TownCount = numTownNPCs; return Checks.AllowSpawn; }
    public override bool CheckConditions(int left, int right, int top, int bottom) => left == 11 && right == 19 && top == 22 && bottom == 28;
    public override List<string> SetNPCNameList() => new() { "NativeName" };
    public override bool CanChat() => true;
    public override string GetChat() => "town-chat";
    public override void SetChatButtons(ref string button, ref string button2) { button = "Test shop"; button2 = "Test action"; }
    public override void OnChatButtonClicked(bool firstButton, ref string shopName)
    {
        Checks.LegacyClicks++;
        if (firstButton) shopName = "Shop";
        else Main.npcChatText = "legacy-click";
    }
    public override void AddShops()
    {
        var shop = new NPCShop(Type).Add(ItemID.WoodenArrow).Add(ItemID.Torch, new Condition("Test.Town.Available", () => Checks.Stock));
        shop.Register();
    }
    public override void ModifyActiveShop(string shopName, Item[] items)
    {
        Checks.OwnActive++;
        Checks.ActiveNamesValid &= shopName == NPCShopDatabase.GetShopName(Type);
        foreach (Item item in items) if (!item.IsNull && item.type == ItemID.WoodenArrow) item.shopCustomPrice = 123;
    }
    public override void TownNPCAttackStrength(ref int damage, ref float knockback) { Checks.Trace.Add("strength"); damage = 20; knockback = 4; }
    public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) { Checks.Trace.Add("cooldown"); cooldown = 30; randExtraCooldown = 1; }
    public override void TownNPCAttackProj(ref int projType, ref int attackDelay) { Checks.Trace.Add("projectile"); projType = ProjectileID.WoodenArrowFriendly; attackDelay = 1; }
    public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset)
    { Checks.Trace.Add("speed"); multiplier = 12; gravityCorrection = 0; randomOffset = 0; }
    public override void TownNPCAttackShoot(ref bool inBetweenShots) { Checks.Trace.Add("shoot"); inBetweenShots = false; }
    public override void TownNPCAttackMagic(ref float auraLightMultiplier) { Checks.Trace.Add("magic"); auraLightMultiplier = 0; }
    public override void TownNPCAttackSwing(ref int itemWidth, ref int itemHeight) { Checks.Trace.Add("swing"); itemWidth = 36; itemHeight = 52; }
    public override void DrawTownAttackGun(ref Texture2D item, ref Rectangle itemFrame, ref float scale, ref int horizontalHoldoutOffset)
    {
        Checks.GunDraw++;
        Main.GetItemDrawFrame(ItemID.WoodenBow, out item, out itemFrame);
        scale = 0.8f; horizontalHoldoutOffset = 6;
    }
    public override void DrawTownAttackSwing(ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset)
    {
        Checks.SwingDraw++;
        Main.GetItemDrawFrame(ItemID.CopperShortsword, out item, out itemFrame);
        itemSize = 36; scale = 0.2f; offset = new(2, 4);
    }
}

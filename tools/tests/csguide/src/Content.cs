using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGuide;

public class GuideOre : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.IronOre}";

    public override void SetDefaults()
    {
        Item.width = Item.height = 16;
        Item.maxStack = 9999;
    }
}

public class GuideBar : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.IronBar}";

    public override void SetDefaults()
    {
        Item.width = Item.height = 16;
        Item.maxStack = 9999;
    }
}

public class GuideSword : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.CopperBroadsword}";

    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 20;
        Item.damage = 5;
    }
}

public class GuideRecipes : ModSystem
{
    public static int Sword = -1, Bar = -1;

    public override void AddRecipes()
    {
        Sword = Recipe.Create(ModContent.ItemType<GuideSword>()).AddIngredient(ItemID.Gel, 7).Register().RecipeIndex;
        for (int k = 0; k < 110; k++)
            Recipe.Create(ModContent.ItemType<GuideBar>()).AddIngredient(ItemID.StoneBlock, k + 1).AddIngredient(ItemID.Wood, k + 1).Register();
        Bar = Recipe.Create(ModContent.ItemType<GuideBar>()).AddIngredient<GuideOre>(3).AddTile(TileID.WorkBenches).Register().RecipeIndex;
    }
}

[AutoloadHead]
public class ShopNpc : ModNPC
{
    public override string Texture => "CsGuide/Content/ShopNpc";

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 25;
        NPCID.Sets.ExtraFramesCount[Type] = 9;
        NPCID.Sets.AttackFrameCount[Type] = 4;
    }

    public override void SetDefaults()
    {
        NPC.townNPC = NPC.friendly = true;
        NPC.width = 18;
        NPC.height = 40;
        NPC.lifeMax = 250;
        NPC.defense = 15;
        NPC.aiStyle = 7;
        AnimationType = NPCID.Guide;
    }

    public override bool CanChat() => true;

    public override string GetChat() => "shop-chat";

    public override void RegisterChatButtons(NPCInteractionList interactions) =>
        interactions.InsertBefore(NPCInteractions.Shop(), NPCInteractionDatabase.CloseButton);

    public override void AddShops() => new NPCShop(Type).Add<GuideOre>().Register();
}

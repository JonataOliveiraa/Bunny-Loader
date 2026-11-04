using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools
{

    public class ExampleBugNet : ModItem
    {
        public static readonly int LavaCatchChance = 20;
        public static readonly int WarmthLavaCatchChance = 50;
        public static readonly int BonusCritterChance = 5;

        public override string Texture => $"Terraria/Images/Item_{ItemID.BugNet}";

        public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LavaCatchChance, WarmthLavaCatchChance, BonusCritterChance);

        public override void SetStaticDefaults() {

            ItemID.Sets.CatchingTool[Type] = true;


        }

        public override void SetDefaults() {

            Item.width = 24;
            Item.height = 28;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(0, 0, 40);

            Item.useAnimation = 25;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
        }

        public override bool? CanCatchNPC(NPC target, Player player) {

            if (ItemID.Sets.IsLavaBait[target.catchItem]) {
                if (Main.rand.NextBool(player.resistCold ? WarmthLavaCatchChance : LavaCatchChance, 100)) {
                    return true;
                }
            }

            return null;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }

    public class ExampleCatchItemModification : GlobalItem
    {
        [System.ThreadStatic] private static Player catcher;

        public override void Load()
        {
            On_NPC.CatchNPC += (orig, index, who) =>
            {
                Player outer = catcher;
                catcher = Main.player[who < 0 ? Main.myPlayer : who];
                try { orig(index, who); }
                finally { catcher = outer; }
            };
        }

        public override void OnSpawn(WorldItem item, IEntitySource source) {
            if (catcher.IsNull || !BunnyLoader.Interop.GameInterop.IsInstance(source.Pointer, EntitySource_Parent.Class)) return;
            var parent = new EntitySource_Parent(source.Pointer);
            if (parent.Entity.Pointer != catcher.Pointer) return;
            if (catcher.HeldItem.type == ModContent.ItemType<ExampleBugNet>() && Main.rand.NextBool(ExampleBugNet.BonusCritterChance, 100))
                item.stack *= 2;
        }
    }
}

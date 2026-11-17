using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools
{

    // This is an example bug net designed to demonstrate the use cases for various hooks related to catching NPCs such as critters with items.
    public class ExampleBugNet : ModItem
    {
        public static readonly int LavaCatchChance = 20;
        public static readonly int WarmthLavaCatchChance = 50;
        public static readonly int BonusCritterChance = 5;

        public override string Texture => $"Terraria/Images/Item_{ItemID.BugNet}";

        public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LavaCatchChance, WarmthLavaCatchChance, BonusCritterChance);

        public override void SetStaticDefaults() {

            // This set is needed to define an item as a tool for catching NPCs at all.
            ItemID.Sets.CatchingTool[Type] = true;


        }

        public override void SetDefaults() {

            // These are, with a few modifications, the properties applied to the base Bug Net; they're provided here so that you can mess with them as you please.
            // Explanations on them will be glossed over here, as they're not the primary point of the lesson.
            // Common Properties
            Item.width = 24;
            Item.height = 28;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(0, 0, 40);

            // Use Properties
            Item.useAnimation = 25;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
        }

        public override bool? CanCatchNPC(NPC target, Player player) {

            // This hook is used to determine whether or not your catching tool can catch a given NPC.
            // This returns null by default, which allows vanilla to decide whether or not the NPC should be caught.
            // Returning true forces the NPC to be caught, while returning false forces the NPC to not be caught.
            // If you're unsure what to return, return null.
            // For this example, we'll give our example bug net a 20% chance to catch lava critters successfully (50% with a Warmth Potion buff active).
            if (ItemID.Sets.IsLavaBait[target.catchItem]) {
                if (Main.rand.NextBool(player.resistCold ? WarmthLavaCatchChance : LavaCatchChance, 100)) {
                    return true;
                }
            }

            // For all cases where true isn't explicitly returned, we'll return null so that vanilla catching rules and effects can take place.
            return null;
        }

        // Please see Content/ExampleRecipes.cs for a detailed explanation of recipe creation.
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }

    // This class is included here as a demonstration of how to use OnSpawn to modify the item spawned from catching an NPC or other entity.
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
            // Gives a 5% chance for the Example Bug Net to duplicate caught NPCs.
            if (catcher.HeldItem.type == ModContent.ItemType<ExampleBugNet>() && Main.rand.NextBool(ExampleBugNet.BonusCritterChance, 100))
                item.stack *= 2;
        }
    }
}

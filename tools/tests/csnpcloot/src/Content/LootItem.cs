using Terraria;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class LootItem : ModItem
{
    public override string Texture => "Content/LootItem";
    public override void SetDefaults() { Item.width = Item.height = 16; Item.maxStack = Item.CommonMaxStack; }
}

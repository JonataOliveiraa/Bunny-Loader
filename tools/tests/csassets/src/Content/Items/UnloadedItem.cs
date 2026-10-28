using Terraria.ModLoader;

namespace CsAssets.Content.Items;

// Nunca carrega: GetInstance devolve null e ItemType, 0.
[Autoload(false)]
public class UnloadedItem : ModItem
{
}

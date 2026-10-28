using Terraria.ModLoader;

namespace CsLoad.Content.Items;

// So no servidor dedicado, que o celular nao tem: fica de fora.
[Autoload(Side = ModSide.Server)]
public class ServerItem : ModItem
{
}

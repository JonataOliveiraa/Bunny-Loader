using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Main
{
    public void OpenShop(string shopName) => NPCShopDatabase.OpenShop(shopName);
}

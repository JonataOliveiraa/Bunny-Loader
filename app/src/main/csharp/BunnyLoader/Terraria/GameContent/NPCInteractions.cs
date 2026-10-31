using Terraria.Localization;
using Terraria.ModLoader;

namespace Terraria.GameContent;

public static class NPCInteractions
{
    public static NPCInteraction Shop(string shopName = "Shop", string? customTextKey = null) => new Actions.OpenShop(shopName, customTextKey);

    public static class Actions
    {
        public class OpenShop(string shopName, string? customTextKey = null) : NPCInteraction
        {
            public override bool Condition() => true;
            public override string GetText() => Language.GetTextValue(customTextKey ?? "LegacyInterface.28") ?? "Shop";
            public override void Interact()
            {
                string fullName = shopName.Contains('/') ? shopName : NPCShopDatabase.GetShopName(TalkNPCType, shopName);
                NPCShopDatabase.OpenShop(fullName);
            }
        }
    }
}

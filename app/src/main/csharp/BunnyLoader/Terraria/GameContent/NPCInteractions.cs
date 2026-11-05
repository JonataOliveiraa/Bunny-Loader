using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria.GameContent;

public static class NPCInteractions
{
    public static NPCInteraction Shop(string shopName = "Shop", string? customTextKey = null) => new Actions.OpenShop(shopName, customTextKey);

    public static class Actions
    {
        public sealed class CloseChat : NPCInteractionDatabase.DedicatedButton
        {
            public CloseChat() : base("CloseChat") { }
        }

        public abstract class VanillaOption(bool first) : NPCInteraction
        {
            public bool First => first;
            public override bool Condition() => true;
            public override string GetText() => TownNPCHooks.VanillaText(first);
            public override void Interact() => TownNPCHooks.VanillaClick(first);
        }

        public sealed class GuideTip() : VanillaOption(true);

        public sealed class GuideReverseCrafting() : VanillaOption(false);

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

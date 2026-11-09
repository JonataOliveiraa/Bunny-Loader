using System;
using Terraria.GameContent.Golf;

namespace Terraria.ModLoader.Core;

internal static class ItemExampleHooks
{
    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.CanCatchNPC))) PlayerWorldHooks.WantCatching();
        if (has(nameof(ModItem.GetGolfClubProperties)))
            ItemHooks.Once("GolfClubProperties", () => On_GolfHelper.GetClubProperties += GetClubProperties);
    }

    internal static void WantCatchingTools() => ItemHooks.Once("ModCatchingTools", () => On_Player.ItemCheck_GetMeleeHitbox += CatchHitbox);

    internal static void WantGolfItems() => ItemHooks.Once("ModGolfItems", () => On_Item.IsAGolfingItem += IsAGolfingItem);

    static bool IsAGolfingItem(On_Item.orig_IsAGolfingItem orig, Item item)
    {
        if (item.IsNull) return orig(item);
        bool[] clubs = Terraria.ID.ItemID.Sets.IsAGolfClub;
        return (uint)item.type < (uint)clubs.Length && clubs[item.type] || orig(item);
    }

    static void CatchHitbox(On_Player.orig_ItemCheck_GetMeleeHitbox orig, Player self, Item item, Microsoft.Xna.Framework.Rectangle frame, out bool dontAttack, out Microsoft.Xna.Framework.Rectangle hitbox)
    {
        orig(self, item, frame, out dontAttack, out hitbox);
        if (dontAttack || self.itemAnimation <= 0 || self.whoAmI != Main.myPlayer || item.type < Terraria.ID.ItemID.Count) return;
        bool[] tools = Terraria.ID.ItemID.Sets.CatchingTool;
        if ((uint)item.type < (uint)tools.Length && tools[item.type]) self.ItemCheck_CatchCritters(item, hitbox);
    }

    static GolfHelper.ClubProperties GetClubProperties(On_GolfHelper.orig_GetClubProperties orig, short itemId)
    {
        if (ItemLoader.GetItem(itemId) is ModItem m)
        {
            try
            {
                if (m.GetGolfClubProperties() is { } properties) return properties;
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.GetGolfClubProperties", e); }
        }
        return orig(itemId);
    }
}

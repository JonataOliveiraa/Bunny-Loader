using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTownNpc.Content;

public sealed class TownGlobal : GlobalNPC
{
    public override bool AppliesToEntity(NPC npc, bool lateInstantiation) => npc.type == ModContent.NPCType<TownNpc>() || npc.type == ModContent.NPCType<ModernNpc>() || npc.type == NPCID.Merchant;
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) => maxSpawns = 0;
    public override void ModifyNPCNameList(NPC npc, List<string> names) { names.Clear(); names.Add("GlobalName"); }
    public override bool? CanChat(NPC npc) => Checks.Veto ? false : null;
    public override void GetChat(NPC npc, ref string chat) => chat += ":global";
    public override bool PreChatButtonClicked(NPC npc, NPCInteraction interaction) { Checks.PreClicks++; return !Checks.BlockClick; }
    public override void OnChatButtonClicked(NPC npc, NPCInteraction interaction) => Checks.PostClicks++;
    public override void ModifyShop(NPCShop shop)
    {
        Checks.ModifyCalls++;
        if (shop.NpcType == NPCID.Merchant)
        {
            shop.Add(ItemID.Amethyst, new Condition("Test.Town.Stock", () => Checks.Stock));
            if (shop.TryGetEntry(ItemID.Torch, out var entry)) entry.Disable();
        }
    }
    public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
    {
        Checks.GlobalActive++;
        Checks.ActiveNamesValid &= shopName == NPCShopDatabase.GetShopName(npc.type);
        foreach (Item item in items) if (!item.IsNull && item.type == ItemID.WoodenArrow) item.stack = 7;
    }
    public override void TownNPCAttackStrength(NPC npc, ref int damage, ref float knockback)
    { Checks.Trace.Add("global-strength"); damage++; knockback++; }
}

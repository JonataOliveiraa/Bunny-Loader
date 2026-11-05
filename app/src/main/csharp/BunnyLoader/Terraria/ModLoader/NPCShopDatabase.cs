using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class NPCShopDatabase
{
    static readonly Dictionary<string, AbstractNPCShop> Shops = new(StringComparer.Ordinal);
    internal static readonly Dictionary<int, AbstractNPCShop> ByIndex = new();
    static int nextIndex = 99;
    public static IEnumerable<AbstractNPCShop> AllShops => Shops.Values;
    public static readonly ISet<string> NoPylons = new HashSet<string>
    {
        GetShopName(ID.NPCID.TravellingMerchant), GetShopName(ID.NPCID.SkeletonMerchant), GetShopName(ID.NPCID.DD2Bartender), GetShopName(ID.NPCID.SantaClaus)
    };
    public static string GetShopName(int npcType, string shopName = "Shop") =>
        $"{(NPCLoader.GetNPC(npcType) is ModNPC own ? own.FullName : "Terraria/" + NPCID.Search.GetName(npcType))}/{shopName}";

    public static AbstractNPCShop GetShop(string fullName) => Shops[fullName];
    public static bool TryGetNPCShop(string fullName, out NPCShop shop)
    {
        shop = Shops.TryGetValue(fullName, out AbstractNPCShop? found) ? found as NPCShop ?? null! : null!;
        return shop != null;
    }

    public static void AddShop(AbstractNPCShop shop)
    {
        ArgumentNullException.ThrowIfNull(shop);
        if (Shops.ContainsKey(shop.FullName)) throw new ArgumentException($"Shop '{shop.FullName}' is already registered");
        if (nextIndex <= 25) throw new InvalidOperationException("Mobile NPC shop slots are exhausted");
        shop.Index = nextIndex--;
        Shops.Add(shop.FullName, shop);
        ByIndex.Add(shop.Index, shop);
        TownShopHooks.Install();
    }

    internal static void Initialize()
    {
        if (ByIndex.ContainsKey(1)) return;
        int[] types = { 17, 19, 20, 38, 54, 107, 108, 124, 142, 160, 178, 207, 208, 209, 227, 228, 229, 353, 368, 453, 550, 588, 633, 663, 227 };
        for (int i = 0; i < types.Length; i++)
        {
            var shop = new NPCShop(types[i], i == 24 ? "Decor" : "Shop") { Index = i + 1, Vanilla = true };
            Shops.Add(shop.FullName, shop);
            ByIndex.Add(shop.Index, shop);
        }
        for (int type = 697; type < NPCLoader.NPCCount; type++) NPCLoader.AddShops(type);
        foreach (AbstractNPCShop custom in ByIndex.Values)
        {
            if (custom is not NPCShop shop)
            {
                custom.FinishSetup();
                continue;
            }
            if (shop.Vanilla) continue;
            NPCLoader.ModifyShop(shop);
            Core.PylonHooks.AddShopEntries(shop);
            shop.Modified = true;
            shop.FinishSetup();
        }
    }

    public static void OpenShop(string fullName)
    {
        if (!Shops.TryGetValue(fullName, out AbstractNPCShop? shop) || shop.Index < 0) throw new ArgumentException($"Shop '{fullName}' is not registered", nameof(fullName));
        Main.instance.OpenShop(shop.Index);
        if (!GUIInstance.Active.IsNull) GUIInstance.Active.GUIPageIcons.OpenUI((GUIPageIcons.Category)2, (GUIPageIcons.Category)4);
    }
}

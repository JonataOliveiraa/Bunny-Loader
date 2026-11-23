using System;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsShopSavings;

public sealed class SavingsShop : GlobalNPC
{
    public override void ModifyShop(NPCShop shop)
    {
        if (shop.NpcType != NPCID.Dryad) return;
        shop.Add(new Item(ItemID.Wood) { shopCustomPrice = 2, shopSpecialCurrency = CustomCurrencyID.DefenderMedals });
        shop.Add(new Item(ItemID.Torch) { shopCustomPrice = 3, shopSpecialCurrency = CsShopSavingsMod.GelCurrency });
    }
}

public sealed class SavingsCurrency : ModSystem
{
    public override void PostSetupContent()
    {
        CustomCurrencySingleCoin coin = CustomCurrencySingleCoin.New(ItemID.Gel, 999);
        coin.CurrencyTextKey = "Mods.CsShopSavings.Gel";
        coin.CurrencyTextColor = Microsoft.Xna.Framework.Graphics.Color.CornflowerBlue;
        CsShopSavingsMod.GelCurrency = CustomCurrencyManager.RegisterCurrency(new CustomCurrencySystem(coin.Pointer));
    }
}

public sealed class CsShopSavingsMod : Mod
{
    public static int GelCurrency = -1;

    static int frames, pass, fail, counted, lastCurrency = -2, medals, shopDuring, shopAfter;
    static IntPtr medalTexture;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsShopSavingsMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_CustomCurrencyManager.GetCurrencyCount += (orig, currency) =>
        {
            counted++;
            lastCurrency = currency;
            shopDuring = Main.npcShop;
            return orig(currency);
        };
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csshopsavings END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        Player player = Main.LocalPlayer;
        switch (f)
        {
            case 10:
                Open(player);
                break;
            case 30:
                counted = 0;
                ItemSlot._customCurrencyForSavings = -1;
                break;
            case 90:
                Report("the Dryad shop draws its first special currency (medals)", counted > 0 && lastCurrency == CustomCurrencyID.DefenderMedals && shopDuring == 21,
                    $"calls={counted} currency={lastCurrency} shop={shopDuring}");
                Restored("after the medal savings");
                Log.Info("csshopsavings SHOT medals");
                break;
            case 900:
                counted = 0;
                ItemSlot._customCurrencyForSavings = GelCurrency;
                break;
            case 960:
                Report("the touched item's mod currency is drawn instead", counted > 0 && lastCurrency == GelCurrency, $"calls={counted} currency={lastCurrency} gel={GelCurrency}");
                Restored("after the mod currency savings");
                Log.Info("csshopsavings SHOT gel");
                break;
            case 1500:
                done = true;
                break;
        }
    }

    static void Open(Player player)
    {
        medals = CustomCurrencyID.DefenderMedals;
        medalTexture = ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>.ToGame(TextureAssets.Item_[ItemID.DefenderMedal]);
        int index = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 80, (int)player.Bottom.Y, NPCID.Dryad);
        Item gel = new Item(ItemID.Gel);
        gel.stack = 57;
        Item medal = new Item(ItemID.DefenderMedal);
        medal.stack = 9;
        player.bank.item[0] = gel;
        player.bank.item[1] = medal;
        player.talkNPC = index;
        GUIInstance.Active.GUINPCDialogue.Option1Clicked(0);
        shopAfter = Main.npcShop;
        bool listed = false;
        foreach (Item item in Main.instance.shop[shopAfter].item)
            if (!item.IsNull && item.type == ItemID.Torch && item.shopSpecialCurrency == GelCurrency) listed = true;
        Report("the Dryad shop opens with the mod currency entry", shopAfter > 0 && shopAfter != 21 && listed && GelCurrency >= 0,
            $"shop={shopAfter} listed={listed} gel={GelCurrency} inventory={Main.playerInventory}");
    }

    static void Restored(string when)
    {
        bool ok = Main.npcShop == shopAfter && CustomCurrencyID.DefenderMedals == medals && ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>.ToGame(TextureAssets.Item_[ItemID.DefenderMedal]) == medalTexture;
        Report($"shop, medal currency and medal texture are restored {when}", ok,
            $"shop={Main.npcShop}/{shopAfter} medals={CustomCurrencyID.DefenderMedals}/{medals} texture={ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>.ToGame(TextureAssets.Item_[ItemID.DefenderMedal]):X}/{medalTexture:X}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csshopsavings {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}

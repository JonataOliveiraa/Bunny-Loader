using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace ExampleMod.Content.Currencies
{

    public class ExampleCustomCurrency
    {
        public CustomCurrencySingleCoin Native { get; }
        public static implicit operator CustomCurrencySystem(ExampleCustomCurrency currency) => new CustomCurrencySystem(currency.Native.Pointer);

        public ExampleCustomCurrency(int coinItemId, long currencyCap, string currencyTextKey, Color currencyTextColor) {
            CustomCurrencySingleCoin coin = CustomCurrencySingleCoin.New(coinItemId, currencyCap);
            coin.CurrencyTextKey = currencyTextKey;
            coin.CurrencyTextColor = currencyTextColor;
            Native = coin;
        }

    }

    public sealed class ExampleCustomCurrencies : ModSystem
    {

        public static int ExampleItemCurrency { get; set; }

        public override void PostSetupContent() {
            
            ExampleItemCurrency = CustomCurrencyManager.RegisterCurrency(new ExampleCustomCurrency(
                
                coinItemId: ModContent.ItemType<Items.ExampleItem>(),

                currencyCap: 999,
                
                currencyTextKey: "Mods.ExampleMod.Currencies.ExampleCustomCurrency",
                currencyTextColor: Color.BlueViolet
            ));
        }
    }
}
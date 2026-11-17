using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace ExampleMod.Content.Currencies
{

    // A custom currency type. This class is not autoloaded, an instance of it is registered in the system below.
    public class ExampleCustomCurrency
    {
        // The game's currency classes can't be inherited from on the phone, so this class wraps a CustomCurrencySingleCoin instead.
        public CustomCurrencySingleCoin Native { get; }
        // Lets this class be passed to CustomCurrencyManager.RegisterCurrency.
        public static implicit operator CustomCurrencySystem(ExampleCustomCurrency currency) => new CustomCurrencySystem(currency.Native.Pointer);

        public ExampleCustomCurrency(int coinItemId, long currencyCap, string currencyTextKey, Color currencyTextColor) {
            CustomCurrencySingleCoin coin = CustomCurrencySingleCoin.New(coinItemId, currencyCap);
            coin.CurrencyTextKey = currencyTextKey;
            coin.CurrencyTextColor = currencyTextColor;
            Native = coin;
        }

    }

    // This system showcases registration of custom currencies, similar to the Defenders Medal.
    // For usage, see ExampleNPCShop.cs.
    public sealed class ExampleCustomCurrencies : ModSystem
    {

        // A static property where we store the ID of our currency.
        // This is what Item.shopSpecialCurrency will be set to to make a shop entry use our currency.
        public static int ExampleItemCurrency { get; set; }

        // We use PostSetupContent to be sure that items' types have been initialized and are accessible.
        public override void PostSetupContent() {
            
            // This call actually registers our custom currency.
            ExampleItemCurrency = CustomCurrencyManager.RegisterCurrency(new ExampleCustomCurrency(
                
                // We assign ExampleItem as the item used for this currency, there is no "ExampleCustomCurrency" item.
                coinItemId: ModContent.ItemType<Items.ExampleItem>(),

                // The cap is the max amount of held currency that will be counted and displayed.
                // Item prices in shops should not be higher than this value if you want them to be ever affordable.
                currencyCap: 999,
                
                // Localization string and color that are used for this currency's name.
                currencyTextKey: "Mods.ExampleMod.Currencies.ExampleCustomCurrency",
                currencyTextColor: Color.BlueViolet
            ));
        }
    }
}
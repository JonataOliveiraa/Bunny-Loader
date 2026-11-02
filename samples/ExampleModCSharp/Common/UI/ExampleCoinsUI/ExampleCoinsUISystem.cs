using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace ExampleMod.Common.UI.ExampleCoinsUI;

[Autoload(Side = ModSide.Client)]
public class ExampleCoinsUISystem : ModSystem
{
    private UserInterface exampleCoinUserInterface;
    internal ExampleCoinsUIState exampleCoinsUI;

    public void ShowMyUI()
    {
        exampleCoinUserInterface?.SetState(exampleCoinsUI);
    }

    public void HideMyUI()
    {
        exampleCoinUserInterface?.SetState(null);
    }

    public override void PostSetupContent()
    {
        exampleCoinUserInterface = new UserInterface();
        exampleCoinsUI = new ExampleCoinsUIState();
        exampleCoinsUI.Activate();
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (exampleCoinUserInterface?.CurrentState != null)
            exampleCoinUserInterface?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (mouseTextIndex != -1)
        {
            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "ExampleMod: Coins Per Minute",
                delegate
                {
                    if (exampleCoinUserInterface?.CurrentState != null)
                        exampleCoinUserInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI)
            );
        }
    }
}

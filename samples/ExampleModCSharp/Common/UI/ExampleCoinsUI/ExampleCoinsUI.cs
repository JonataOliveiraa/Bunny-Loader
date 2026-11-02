using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace ExampleMod.Common.UI.ExampleCoinsUI;

internal class ExampleCoinsUIState : UIState
{
    public ExampleDraggableUIPanel CoinCounterPanel;
    public UIMoneyDisplay MoneyDisplay;

    public override void OnInitialize()
    {
        CoinCounterPanel = new ExampleDraggableUIPanel();
        CoinCounterPanel.SetPadding(0);
        SetRectangle(CoinCounterPanel, left: 400f, top: 100f, width: 170f, height: 70f);
        CoinCounterPanel.BackgroundColor = new Color(73, 94, 171);

        Asset<Texture2D> buttonPlayTexture = ModContent.Request<Texture2D>("ExampleMod/Common/UI/ExampleCoinsUI/ButtonPlay");
        ExampleUIHoverImageButton playButton = new ExampleUIHoverImageButton(buttonPlayTexture, Language.GetTextValue("Mods.ExampleMod.UI.ExampleCoinsUIState.Reset"));
        SetRectangle(playButton, left: 110f, top: 10f, width: 22f, height: 22f);
        playButton.OnLeftClick += new MouseEvent(PlayButtonClicked);
        CoinCounterPanel.Append(playButton);

        Asset<Texture2D> buttonDeleteTexture = ModContent.Request<Texture2D>("ExampleMod/Common/UI/ExampleCoinsUI/ButtonDelete");
        ExampleUIHoverImageButton closeButton = new ExampleUIHoverImageButton(buttonDeleteTexture, Language.GetTextValue("LegacyInterface.52"));
        SetRectangle(closeButton, left: 140f, top: 10f, width: 22f, height: 22f);
        closeButton.OnLeftClick += new MouseEvent(CloseButtonClicked);
        CoinCounterPanel.Append(closeButton);

        MoneyDisplay = new UIMoneyDisplay();
        SetRectangle(MoneyDisplay, 15f, 20f, 100f, 40f);
        CoinCounterPanel.Append(MoneyDisplay);

        Append(CoinCounterPanel);
    }

    private void SetRectangle(UIElement uiElement, float left, float top, float width, float height)
    {
        uiElement.Left.Set(left, 0f);
        uiElement.Top.Set(top, 0f);
        uiElement.Width.Set(width, 0f);
        uiElement.Height.Set(height, 0f);
    }

    private void PlayButtonClicked(UIMouseEvent evt, UIElement listeningElement)
    {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        MoneyDisplay.ResetCoins();
    }

    private void CloseButtonClicked(UIMouseEvent evt, UIElement listeningElement)
    {
        SoundEngine.PlaySound(SoundID.MenuClose);
        ModContent.GetInstance<ExampleCoinsUISystem>().HideMyUI();
    }

    public void UpdateValue(int pickedUp)
    {
        MoneyDisplay.AddCoinsPerMinute(pickedUp);
    }
}

public class UIMoneyDisplay : UIElement
{
    public long collectedCoins;
    private DateTime? startTime;
    private readonly Texture2D[] coinsTextures = new Texture2D[4];
    private readonly SpriteFont exampleFont;

    public UIMoneyDisplay()
    {
        startTime = null;
        exampleFont = ModContent.Request<SpriteFont>("ExampleMod/Assets/Fonts/ExampleFont").Value;

        for (int j = 0; j < 4; j++)
        {
            Main.instance.LoadItem(74 - j);
            coinsTextures[j] = TextureAssets.Item[74 - j].Value;
        }

        IgnoresMouseInteraction = true;
    }

    public void AddCoinsPerMinute(int coins)
    {
        collectedCoins += coins;
        if (startTime == null)
            startTime = DateTime.Now;
    }

    public int GetCoinsPerMinute()
    {
        if (collectedCoins == 0)
            return 0;
        return (int)(collectedCoins / Math.Max(1, (DateTime.Now - startTime.Value).TotalMinutes));
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle innerDimensions = GetInnerDimensions();
        float shopx = innerDimensions.X;
        float shopy = innerDimensions.Y;

        DrawCoins(spriteBatch, shopx, shopy, Utils.CoinsSplit(collectedCoins));
        DrawCoins(spriteBatch, shopx, shopy, Utils.CoinsSplit(GetCoinsPerMinute()), 0, 25);
        Utils.DrawBorderStringFourWay(spriteBatch, exampleFont, "CPM", shopx + (float)(24 * 4), shopy + 22f, Color.Gold, Color.Black, new Vector2(0.3f), 0.9f);
    }

    private void DrawCoins(SpriteBatch spriteBatch, float shopx, float shopy, int[] coinsArray, int xOffset = 0, int yOffset = 0)
    {
        for (int j = 0; j < 4; j++)
        {
            spriteBatch.Draw(coinsTextures[j], new Vector2(shopx + 11f + 24 * j + xOffset, shopy + yOffset), null, Color.White, 0f, coinsTextures[j].Size() / 2f, 1f, SpriteEffects.None, 0f);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, coinsArray[3 - j].ToString(), shopx + 24 * j + xOffset, shopy + yOffset, Color.White, Color.Black, new Vector2(0.3f), 0.75f);
        }
    }

    public void ResetCoins()
    {
        collectedCoins = 0;
        startTime = DateTime.Now;
    }
}

public class MoneyCounterGlobalItem : GlobalItem
{
    public override bool AppliesToEntity(Item item, bool lateInstantiation)
    {
        return item.type >= ItemID.CopperCoin && item.type <= ItemID.PlatinumCoin;
    }

    public override bool OnPickup(Item item, Player player)
    {
        ModContent.GetInstance<ExampleCoinsUISystem>().exampleCoinsUI.UpdateValue(item.stack * (item.value / 5));
        return base.OnPickup(item, player);
    }
}

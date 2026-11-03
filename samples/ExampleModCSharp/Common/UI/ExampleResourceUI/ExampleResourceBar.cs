using System.Collections.Generic;
using ExampleMod.Common.Players;
using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace ExampleMod.Common.UI.ExampleResourceUI;

internal class ExampleResourceBar : UIState
{
    private UIText text;
    private UIElement area;
    private UIImage barFrame;
    private Color gradientA;
    private Color gradientB;

    public override void OnInitialize()
    {
        area = new UIElement();
        area.Left.Set(-area.Width.Pixels - 600, 1f);
        area.Top.Set(30, 0f);
        area.Width.Set(182, 0f);
        area.Height.Set(60, 0f);

        barFrame = new UIImage(ModContent.Request<Texture2D>("ExampleMod/Common/UI/ExampleResourceUI/ExampleResourceFrame"));
        barFrame.Left.Set(22, 0f);
        barFrame.Top.Set(0, 0f);
        barFrame.Width.Set(138, 0f);
        barFrame.Height.Set(34, 0f);

        text = new UIText("0/0", 0.8f);
        text.Width.Set(138, 0f);
        text.Height.Set(34, 0f);
        text.Top.Set(40, 0f);
        text.Left.Set(0, 0f);

        gradientA = new Color(123, 25, 138);
        gradientB = new Color(187, 91, 201);

        area.Append(text);
        area.Append(barFrame);
        Append(area);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (Main.LocalPlayer.HeldItem.ModItem is not ExampleCustomResourceWeapon)
            return;

        base.Draw(spriteBatch);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        var modPlayer = Main.LocalPlayer.GetModPlayer<ExampleResourcePlayer>();
        float quotient = (float)modPlayer.exampleResourceCurrent / modPlayer.exampleResourceMax2;
        quotient = Utils.Clamp(quotient, 0f, 1f);

        Rectangle hitbox = barFrame.GetInnerDimensions().ToRectangle();
        hitbox.X += 12;
        hitbox.Width -= 24;
        hitbox.Y += 8;
        hitbox.Height -= 16;

        int left = hitbox.Left;
        int right = hitbox.Right;
        int steps = (int)((right - left) * quotient);
        for (int i = 0; i < steps; i += 1)
        {
            float percent = (float)i / (right - left);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(left + i, hitbox.Y, 1, hitbox.Height), Color.Lerp(gradientA, gradientB, percent));
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (Main.LocalPlayer.HeldItem.ModItem is not ExampleCustomResourceWeapon)
            return;

        var modPlayer = Main.LocalPlayer.GetModPlayer<ExampleResourcePlayer>();
        text.SetText(ExampleResourceUISystem.ExampleResourceText.Format(modPlayer.exampleResourceCurrent, modPlayer.exampleResourceMax2));
        base.Update(gameTime);
    }
}

[Autoload(Side = ModSide.Client)]
internal class ExampleResourceUISystem : ModSystem
{
    private UserInterface ExampleResourceBarUserInterface;
    internal ExampleResourceBar ExampleResourceBar;

    public static LocalizedText ExampleResourceText { get; private set; }

    public override void PostSetupContent()
    {
        ExampleResourceBar = new();
        ExampleResourceBarUserInterface = new();
        ExampleResourceBarUserInterface.SetState(ExampleResourceBar);

        ExampleResourceText = Mod.GetLocalization("UI.ExampleResource");
    }

    public override void UpdateUI(GameTime gameTime)
    {
        ExampleResourceBarUserInterface?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int resourceBarIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));
        if (resourceBarIndex != -1)
        {
            layers.Insert(resourceBarIndex, new LegacyGameInterfaceLayer(
                "ExampleMod: Example Resource Bar",
                delegate
                {
                    ExampleResourceBarUserInterface?.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI)
            );
        }
    }
}

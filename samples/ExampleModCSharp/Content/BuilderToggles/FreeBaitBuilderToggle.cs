using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.BuilderToggles;

public class FreeBaitBuilderToggle : BuilderToggle
{
    public static LocalizedText NameText { get; private set; }

    public override string HoverTexture => Texture;

    public override bool Active() => !Main.LocalPlayer.HeldItem.IsAir && Main.LocalPlayer.HeldItem.fishingPole > 0;

    public override int NumberOfStates => 4;

    public override Position OrderPosition => new After(TorchBiome);

    public override bool OnLeftClick(ref SoundStyle? sound) {

        sound = SoundID.DrumTomHigh;
        return true;
    }

    public override void OnRightClick() {

        SoundEngine.PlaySound(Main.rand.NextBool() ? SoundID.DrumCymbal1 : SoundID.DrumCymbal2);
        int itemType = CurrentState switch {
            0 => ItemID.ApprenticeBait,
            1 => ItemID.JourneymanBait,
            2 => ItemID.MasterBait,
            3 => ItemID.TruffleWorm,
            _ => throw new ArgumentOutOfRangeException()
        };

        Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_FromThis(), itemType, 10);
    }

    public override bool Draw(SpriteBatch spriteBatch, ref BuilderToggleDrawParams drawParams) {
        drawParams.Frame = drawParams.Texture.Frame(4, 2, CurrentState % 4);
        return true;
    }

    public override bool DrawHover(SpriteBatch spriteBatch, ref BuilderToggleDrawParams drawParams) {
        int column = CurrentState == 3 ? 1 : 0;
        drawParams.Frame = drawParams.Texture.Frame(4, 2, column, 1);
        return true;
    }

    public override void SetStaticDefaults() {
        NameText = this.GetLocalization(nameof(NameText));
    }

    public override string DisplayValue() {
        string itemName = CurrentState switch {
            0 => Lang.GetItemNameValue(ItemID.ApprenticeBait),
            1 => Lang.GetItemNameValue(ItemID.JourneymanBait),
            2 => Lang.GetItemNameValue(ItemID.MasterBait),
            3 => Lang.GetItemNameValue(ItemID.TruffleWorm),
            _ => "Unknown (How did you get here?)"
        };
        return NameText.Format(itemName);
    }
}

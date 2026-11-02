using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items;

public class ExampleQuestFish : ModItem
{
    public static LocalizedText DescriptionText { get; private set; }
    public static LocalizedText CatchLocationText { get; private set; }

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 2;
        ItemID.Sets.CanBePlacedOnWeaponRacks[Type] = true;

        DescriptionText = this.GetLocalization("Description");
        CatchLocationText = this.GetLocalization("CatchLocation");
    }

    public override void SetDefaults()
    {
        Item.DefaultToQuestFish();
    }

    public override bool IsQuestFish() => true;

    public override bool IsAnglerQuestAvailable() => Main.hardMode;

    public override void AnglerQuestChat(ref string description, ref string catchLocation)
    {
        description = DescriptionText.Value;
        catchLocation = CatchLocationText.Value;
    }
}

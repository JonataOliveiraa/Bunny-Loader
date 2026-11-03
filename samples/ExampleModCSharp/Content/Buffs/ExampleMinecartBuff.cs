using ExampleMod.Content.Items.Mounts;
using ExampleMod.Content.Mounts;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs;

public class ExampleMinecartBuff : ModBuff
{
    public override LocalizedText DisplayName => ModContent.GetInstance<ExampleMinecart>().DisplayName;

    public override LocalizedText Description => Language.GetText("BuffDescription.MinecartLeft");

    public override void SetStaticDefaults()
    {
        BuffID.Sets.MountType[Type] = ModContent.MountType<ExampleMinecartMount>();
    }
}

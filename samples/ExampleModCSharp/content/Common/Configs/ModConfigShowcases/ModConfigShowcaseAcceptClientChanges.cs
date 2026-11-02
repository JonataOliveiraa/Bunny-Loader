using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases;

public class ModConfigShowcaseAcceptClientChanges : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    public bool OnlyChangeableDuringNight;

    public int SomeNumber;

    public static LocalizedText RejectChangesDaytime { get; private set; }

    public override void OnLoaded()
    {
        RejectChangesDaytime = this.GetLocalization(nameof(RejectChangesDaytime));
    }

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        if (Main.dayTime && ((ModConfigShowcaseAcceptClientChanges)pendingConfig).OnlyChangeableDuringNight != OnlyChangeableDuringNight)
        {
            message = RejectChangesDaytime.ToNetworkText();
            return false;
        }

        if (!NetMessage.DoesPlayerSlotCountAsAHost(whoAmI))
        {
            message = NetworkText.FromKey("tModLoader.ModConfigRejectChangesNotHost");
            return false;
        }

        return true;
    }

    public override void HandleAcceptClientChangesReply(bool success, int player, NetworkText message)
    {
        if (player == Main.myPlayer)
            SoundEngine.PlaySound(success ? SoundID.CoinPickup : SoundID.Duck);
    }
}

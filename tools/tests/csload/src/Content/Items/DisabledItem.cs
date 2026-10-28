using Terraria.ModLoader;

namespace CsLoad.Content.Items;

public class DisabledItem : ModItem
{
    public override bool IsLoadingEnabled(Mod mod) => false;

    public override void Load() => Checks.Note("DisabledItem.Load");
}

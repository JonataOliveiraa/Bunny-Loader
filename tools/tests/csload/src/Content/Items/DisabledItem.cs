using Terraria.ModLoader;

namespace CsLoad.Content.Items;

// Desligado por IsLoadingEnabled: nem Load, nem tipo (por isso nao tem textura).
public class DisabledItem : ModItem
{
    public override bool IsLoadingEnabled(Mod mod) => false;

    public override void Load() => Checks.Note("DisabledItem.Load");
}

using System.Linq;
using CsSave.Content.Items;
using Terraria;
using Terraria.ModLoader;

namespace CsSave;

public static class WorldCheck
{
    public static void Run(Player player)
    {
        int type = ModContent.ItemType<SavedGem>();
        for (int slot = 0; slot < 50; slot++)
        {
            Item item = player.inventory[slot];
            if (item.type != type || item.ModItem is not SavedGem gem) continue;
            bool ok = SavedGem.Loads > 0 && gem.Uses >= 1 && gem.Label == $"gem-{gem.Uses}" && gem.Flag && gem.Ratio == 0.25f &&
                gem.Numbers.SequenceEqual(new[] { 3, 1, 4 }) && gem.Inner == "deep";
            Report.Check("reloaded", ok, $"slot={slot} uses={gem.Uses} label={gem.Label} loads={SavedGem.Loads}");
            gem.Uses++;
            gem.Label = $"gem-{gem.Uses}";
            Report.Info($"uses is now {gem.Uses}: save and enter again to see it");
            return;
        }
        for (int slot = 0; slot < 50; slot++)
        {
            Item item = player.inventory[slot];
            if (item.type > 0 && item.stack > 0) continue;
            item.SetDefaults(type, default);
            var gem = (SavedGem)item.ModItem!;
            gem.Uses = 1;
            gem.Label = "gem-1";
            gem.Flag = true;
            gem.Ratio = 0.25f;
            gem.Numbers = new() { 3, 1, 4 };
            gem.Inner = "deep";
            Report.Info($"placed a gem in slot {slot}: save, exit and enter the world again");
            return;
        }
        Report.Info("no free inventory slot for the gem");
    }
}

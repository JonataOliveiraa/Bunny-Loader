using ExampleMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalBuffs;

public class ExampleGlobalBuff : GlobalBuff
{
    public static LocalizedText RemainingTimeText { get; private set; }
    public static LocalizedText CannotCancelText { get; private set; }

    public override void SetStaticDefaults()
    {
        RemainingTimeText = Mod.GetLocalization($"{nameof(ExampleGlobalBuff)}.RemainingTime");
        CannotCancelText = Mod.GetLocalization($"{nameof(ExampleGlobalBuff)}.CannotCancel");
    }

    public override void Update(int type, Player player, ref int buffIndex)
    {
        if (type == BuffID.Chilled && buffIndex >= 5)
        {
            int limit = 3 * 60;
            if (player.buffTime[buffIndex] > limit)
            {
                player.buffTime[buffIndex] = limit;
            }
        }
    }

    private static string randomBuffTextCache;
    private static int randomBuffTypeCache;

    public override void ModifyBuffText(int type, ref string buffName, ref string tip, ref int rare)
    {
        Player player = Main.LocalPlayer;
        int buffIndex = player.FindBuffIndex(type);
        if (buffIndex < 0 || buffIndex >= player.buffTime.Length)
        {
            return;
        }

        if (Main.TryGetBuffTime(buffIndex, out int buffTimeValue) && buffTimeValue > 2)
        {
            string text = Lang.LocalizedDuration(new System.TimeSpan(0, 0, buffTimeValue / 60), abbreviated: false, showAllAvailableUnits: true);
            tip += "\n" + RemainingTimeText.Format(text);
        }

        if (player.HasBuff(BuffID.SugarRush) && buffName.Length > 2)
        {
            if (Main.GameUpdateCount % 10 == 0 || randomBuffTypeCache != type)
            {
                if (randomBuffTypeCache != type)
                {
                    randomBuffTextCache = buffName;
                    randomBuffTypeCache = type;
                }
                char[] characters = randomBuffTextCache.ToCharArray();
                int n = characters.Length;
                int swaps = Main.rand.Next(1, randomBuffTextCache.Length / 2);
                for (int swap = 0; swap < swaps; swap++)
                {
                    int a = Main.rand.Next(n - 1);
                    int b = Main.rand.Next(a + 1, n);
                    Utils.Swap(ref characters[a], ref characters[b]);
                }
                randomBuffTextCache = new string(characters);
            }
            buffName = randomBuffTextCache;
        }
    }

    public override bool RightClick(int type, int buffIndex)
    {
        if (type == ModContent.BuffType<ExampleDefenseBuff>() && Main.LocalPlayer.velocity == Vector2.Zero)
        {
            Main.NewText(CannotCancelText.Value);
            return false;
        }
        return base.RightClick(type, buffIndex);
    }
}

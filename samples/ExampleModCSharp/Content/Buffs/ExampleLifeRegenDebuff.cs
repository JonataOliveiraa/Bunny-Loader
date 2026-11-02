using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs;

public class ExampleLifeRegenDebuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = true;
        Main.buffNoSave[Type] = true;
        BuffID.Sets.BuffTimeIsExtendedWithGameDifficulty[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<ExampleLifeRegenDebuffPlayer>().lifeRegenDebuff = true;
    }
}

public class ExampleLifeRegenDebuffPlayer : ModPlayer
{
    public bool lifeRegenDebuff;

    public override void ResetEffects()
    {
        lifeRegenDebuff = false;
    }

    public override void UpdateBadLifeRegen()
    {
        if (lifeRegenDebuff)
        {
            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;
            Player.lifeRegenTime = 0;
            Player.lifeRegen -= 16;
        }
    }
}

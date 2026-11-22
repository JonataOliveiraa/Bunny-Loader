using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs;

public class AnimatedBuff : ModBuff
{
    public override string Texture => $"Terraria/Images/Buff_{BuffID.Ironskin}";
}

public class ExampleGravityDebuff : ModBuff
{
    public override string Texture => $"Terraria/Images/Buff_{BuffID.Gravitation}";

    public override void SetStaticDefaults() => Main.debuff[Type] = true;
}

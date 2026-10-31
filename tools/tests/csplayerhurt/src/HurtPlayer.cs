using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CsPlayerHurt;

public class HurtPlayer : ModPlayer
{
    public static string Mode = "";
    public static readonly Dictionary<string, int> Calls = new();
    public static Player.HurtInfo LastInfo;
    public static int LastSlot;

    static void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => Mode == "immune";

    public override bool FreeDodge(Player.HurtInfo info) => Mode == "dodge";

    public override bool ConsumableDodge(Player.HurtInfo info) => Mode == "consumable";

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        Count(nameof(ModifyHurt));
        if (Mode == "double") modifiers.SourceDamage *= 2f;
        if (Mode == "half") modifiers.FinalDamage *= 0.5f;
        if (Mode == "cancel") modifiers.Cancel();
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        Count(nameof(OnHurt));
        LastInfo = info;
    }

    public override void PostHurt(Player.HurtInfo info)
    {
        Count(nameof(PostHurt));
        if (Mode == "immunity") Player.AddImmuneTime(info.CooldownCounter, 60);
    }

    public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
    {
        Count(nameof(CanBeHitByNPC));
        LastSlot = cooldownSlot;
        return Mode != "noNpc";
    }

    public override bool CanBeHitByProjectile(Projectile proj) => Mode != "noProjectile";

    public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) => Count(nameof(ModifyHitByNPC));

    public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) => Count(nameof(ModifyHitByProjectile));

    public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) => Count(nameof(OnHitByNPC));

    public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) => Count(nameof(OnHitByProjectile));
}

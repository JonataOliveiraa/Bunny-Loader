using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsPlayer;

public class TestPlayer : ModPlayer
{
    public static int Resets, PreUpdates, PostUpdates, PreBuffs, PostBuffs, Equips, PostEquips, BadRegen, Regen, Frames, Dead, Respawns,
        EnterWorlds, PreKills;

    public static int ExtraLife;
    public static bool Lucky, Blocked, RefuseDeath;

    public bool Initialized;
    public int Saved = -1;

    public override void Initialize() => Initialized = true;

    public override void ResetEffects() => Resets++;

    public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        base.ModifyMaxStats(out health, out mana);
        health.Flat += ExtraLife;
    }

    public override void PreUpdate() => PreUpdates++;

    public override void PostUpdate() => PostUpdates++;

    public override void PreUpdateBuffs() => PreBuffs++;

    public override void PostUpdateBuffs() => PostBuffs++;

    public override void UpdateEquips() => Equips++;

    public override void PostUpdateEquips() => PostEquips++;

    public override void UpdateBadLifeRegen() => BadRegen++;

    public override void UpdateLifeRegen() => Regen++;

    public override void FrameEffects() => Frames++;

    public override void UpdateDead() => Dead++;

    public override void OnRespawn() => Respawns++;

    public override void OnEnterWorld() => EnterWorlds++;

    public override void ModifyLuck(ref float luck)
    {
        if (Lucky) luck += 0.25f;
    }

    public override bool CanUseItem(Item item) => !(Blocked && item.type == ItemID.Torch);

    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
    {
        PreKills++;
        return !RefuseDeath;
    }

    public override void SaveData(TagCompound tag) => tag["saved"] = 7;

    public override void LoadData(TagCompound tag) => Saved = tag.GetInt("saved");
}

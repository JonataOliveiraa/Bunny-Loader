using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public abstract class ModPlayer : ModType<Player, ModPlayer>
{
    public ushort Index { get; internal set; }

    public Player Player => Entity;

    protected override Player CreateTemplateEntity() => default;

    public override ModPlayer NewInstance(Player entity)
    {
        ModPlayer instance = base.NewInstance(entity);
        instance.Index = Index;
        return instance;
    }

    public bool TypeEquals(ModPlayer other) => Mod == other.Mod && Name == other.Name;

    protected sealed override void Register() => PlayerLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void Initialize() { }

    public virtual void ResetEffects() { }

    public virtual void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        health = StatModifier.Default;
        mana = StatModifier.Default;
    }

    public virtual void UpdateDead() { }

    public virtual void PreSaveCustomData() { }

    public virtual void SaveData(TagCompound tag) { }

    public virtual void LoadData(TagCompound tag) { }

    public virtual void PreSavePlayer() { }

    public virtual void PostSavePlayer() { }

    public virtual void UpdateBadLifeRegen() { }

    public virtual void UpdateLifeRegen() { }

    public virtual void PreUpdate() { }

    public virtual void PreUpdateBuffs() { }

    public virtual void PostUpdateBuffs() { }

    public virtual void UpdateEquips() { }

    public virtual void PostUpdateEquips() { }

    public virtual void PostUpdate() { }

    public virtual void FrameEffects() { }

    public virtual bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) => true;

    public virtual void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) { }

    public virtual bool PreModifyLuck(ref float luck) => true;

    public virtual void ModifyLuck(ref float luck) { }

    public virtual bool CanUseItem(Item item) => true;

    public virtual void OnEnterWorld() { }

    public virtual void OnRespawn() { }
}

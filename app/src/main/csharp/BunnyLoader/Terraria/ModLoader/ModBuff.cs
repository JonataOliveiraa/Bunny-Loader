using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class ModBuff : ModTexturedType, ILocalizedModType
{
    public int Type { get; internal set; }

    public virtual string LocalizationCategory => "Buffs";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public virtual LocalizedText Description => this.GetLocalization(nameof(Description), () => "");

    protected sealed override void Register() => BuffLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void Update(Player player, ref int buffIndex) { }

    public virtual void Update(NPC npc, ref int buffIndex) { }

    public virtual bool ReApply(Player player, int time, int buffIndex) => false;

    public virtual bool ReApply(NPC npc, int time, int buffIndex) => false;

    public virtual void ModifyBuffText(ref string buffName, ref string tip, ref int rare) { }

    public virtual bool PreDraw(SpriteBatch spriteBatch, int buffIndex, ref BuffDrawParams drawParams) => true;

    public virtual void PostDraw(SpriteBatch spriteBatch, int buffIndex, BuffDrawParams drawParams) { }

    public virtual bool RightClick(int buffIndex) => true;
}

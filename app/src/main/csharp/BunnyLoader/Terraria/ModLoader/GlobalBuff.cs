using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

public abstract class GlobalBuff : ModType
{
    protected sealed override void Register() => BuffLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void Update(int type, Player player, ref int buffIndex) { }

    public virtual void Update(int type, NPC npc, ref int buffIndex) { }

    public virtual bool ReApply(int type, Player player, int time, int buffIndex) => false;

    public virtual bool ReApply(int type, NPC npc, int time, int buffIndex) => false;

    public virtual void ModifyBuffText(int type, ref string buffName, ref string tip, ref int rare) { }

    public virtual bool PreDraw(SpriteBatch spriteBatch, int type, int buffIndex, ref BuffDrawParams drawParams) => true;

    public virtual void PostDraw(SpriteBatch spriteBatch, int type, int buffIndex, BuffDrawParams drawParams) { }

    public virtual bool RightClick(int type, int buffIndex) => true;
}

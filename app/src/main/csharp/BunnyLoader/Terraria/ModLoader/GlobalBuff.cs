using System.Collections.Generic;
using Microsoft.Xna.Framework;
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

    public virtual void CustomBuffTipSize(string buffTip, List<Vector2> sizes) { }

    public virtual void DrawCustomBuffTip(string buffTip, SpriteBatch spriteBatch, int originX, int originY) { }

    public virtual bool PreDraw(SpriteBatch spriteBatch, int type, int buffIndex, ref BuffDrawParams drawParams) => true;

    public virtual void PostDraw(SpriteBatch spriteBatch, int type, int buffIndex, BuffDrawParams drawParams) { }

    public virtual bool RightClick(int type, int buffIndex) => true;
}

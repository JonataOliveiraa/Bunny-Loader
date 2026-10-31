using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

public abstract class GlobalBossBar : ModType
{
    protected sealed override void Register() => BossBarLoader.AddGlobalBossBar(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams) => true;

    public virtual void PostDraw(SpriteBatch spriteBatch, NPC npc, BossBarDrawParams drawParams) { }
}

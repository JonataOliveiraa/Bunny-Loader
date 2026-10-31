using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.GameContent.UI.BigProgressBar;

namespace Terraria.ModLoader;

public abstract class ModBossBar : ModTexturedType
{
    internal int index;
    float life, lifeMax, shield, shieldMax;

    public int Type => index;

    public float Life => life;

    public float LifeMax => lifeMax;

    public float Shield => shield;

    public float ShieldMax => shieldMax;

    protected sealed override void Register() => BossBarLoader.AddBossBar(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual Asset<Texture2D>? GetIconTexture(ref Rectangle? iconFrame) => null;

    public virtual bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax) => null;

    public virtual bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams) => true;

    public virtual void PostDraw(SpriteBatch spriteBatch, NPC npc, BossBarDrawParams drawParams) { }

    public bool ValidateAndCollectNecessaryInfo(ref BigProgressBarInfo info)
    {
        if (info.npcIndexToAimAt < 0 || info.npcIndexToAimAt >= Main.maxNPCs) return false;
        bool? modify = ModifyInfo(ref info, ref life, ref lifeMax, ref shield, ref shieldMax);
        if (modify.HasValue) return modify.Value && (uint)info.npcIndexToAimAt < (uint)Main.maxNPCs;
        NPC npc = Main.npc[info.npcIndexToAimAt];
        if (!npc.active) return false;
        lifeMax = npc.lifeMax;
        life = System.Math.Clamp(npc.life, 0, npc.lifeMax);
        return true;
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBossBar.Content;

public sealed class TestBar : ModBossBar
{
    public static bool Hide, Tint;
    public static int Infos, Pres, Posts, IconCalls;
    public static Rectangle LastFrame;

    public override Asset<Texture2D>? GetIconTexture(ref Rectangle? iconFrame)
    {
        IconCalls++;
        iconFrame = new Rectangle(0, 0, 13, 13);
        return ModContent.Request<Texture2D>("CsBossBar/Content/BarIcon");
    }

    public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)
    {
        Infos++;
        return Hide ? false : null;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
    {
        Pres++;
        Checks.Mark("PreN");
        LastFrame = drawParams.IconFrame;
        if (Tint) drawParams.BarColor = new Color(255, 0, 0);
        Checks.InBar = true;
        return true;
    }

    public override void PostDraw(SpriteBatch spriteBatch, NPC npc, BossBarDrawParams drawParams)
    {
        Checks.InBar = false;
        Posts++;
        Checks.Mark("PostN");
    }
}

public sealed class TestGlobalBar : GlobalBossBar
{
    public static int Vanilla;

    public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
    {
        if (npc.type == NPCID.KingSlime) Vanilla++;
        else Checks.Mark("PreG");
        return true;
    }

    public override void PostDraw(SpriteBatch spriteBatch, NPC npc, BossBarDrawParams drawParams)
    {
        if (npc.type != NPCID.KingSlime) Checks.Mark("PostG");
    }
}

public sealed class BarNpc : ModNPC
{
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 5000;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        NPC.friendly = true;
        NPC.townNPC = true;
        NPC.noGravity = NPC.noTileCollide = true;
        NPC.BossBar = ModContent.GetInstance<TestBar>();
    }

    public override bool CheckDead() => false;

    public override bool CheckActive() => false;
}

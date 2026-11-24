using BunnyLoader;
using ExampleMod.Content.NPCs.MinionBoss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ExampleMod.Content.Effects;

// An FxcScreenEffect draws a compiled HLSL effect (.fxc), the kind of file tModLoader mods ship, over the screen.
// This one is the aura of the Minion Boss: rings leave the boss and the world around it loses its color, more and more as the boss gets hurt.
// ExampleBossAuraSystem spawns it while a Minion Boss is alive. The shader source is ExampleBossAura.fx, next to this file.
// Support for .fxc effects is new: only screen effects work for now, and this API may still change.
public class ExampleBossAuraEffect : FxcScreenEffect
{
    // The boss this aura belongs to, as an index in Main.npc.
    public int Boss { get; set; } = -1;

    public override void SetDefaults()
    {
        // FxcEffect.Load keeps one effect per file, so loading it here, once per spawned aura, does not read the file again.
        Effect = FxcEffect.Load(Mod, "Content/Effects/ExampleBossAura");
        // The pass to draw, as named in the technique of the .fx file. A wrong name throws right here.
        PassName = "BossAura";

        // These become the shader's uColor and uSecondaryColor.
        Color = new Color(170, 110, 255);
        SecondaryColor = new Vector3(1f, 0.6f, 1f);

        // The aura fades in over one second. uOpacity carries the fade.
        FadeIn = 60;
    }

    public override void Update()
    {
        if (Boss < 0 || !Main.npc[Boss].active)
            return;

        // uIntensity grows from 0.35 at full life to 1 when the boss is nearly dead.
        NPC npc = Main.npc[Boss];
        float lifeLost = 1f - npc.life / (float)npc.lifeMax;
        Intensity = 0.35f + 0.65f * lifeLost;

        // In its second stage the boss turns the world red.
        if (npc.ModNPC is MinionBossBody body && body.SecondStage)
            Color = new Color(255, 90, 120);
    }
}

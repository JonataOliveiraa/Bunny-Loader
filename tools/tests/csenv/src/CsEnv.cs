using Terraria;
using Terraria.ModLoader;

namespace CsEnv;

public sealed class CsEnvMod : Mod
{
    static int frames;

    public override void Load()
    {
        On_Player.ResetEffects += ResetEffects;
        On_Player.Update += Update;
        On_NPC.SpawnNPC += orig => { };
        On_NPC_Spawner.SpawnNPC += (orig, self) => { };
    }

    static void ResetEffects(On_Player.orig_ResetEffects orig, Player self)
    {
        orig(self);
        if (Main.gameMenu || self.whoAmI != Main.myPlayer) return;
        self.creativeGodMode = true;
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frames = 0;
            orig(self, i);
            return;
        }
        if (i == Main.myPlayer)
        {
            self.creativeGodMode = true;
            self.statLife = self.statLifeMax2;
            self.breath = self.breathMax;
        }
        orig(self, i);
        if (i != Main.myPlayer || ++frames % 30 != 1) return;
        var npcs = Main.npc;
        int removed = 0;
        for (int k = 0; k < npcs.Length; k++)
        {
            NPC n = npcs[k];
            if (n.IsNull || !n.active || n.friendly || n.townNPC) continue;
            n.active = false;
            removed++;
        }
        if (frames == 1) ModContent.GetInstance<CsEnvMod>().Logger.Info($"csenv ready: god mode on, spawns off, {removed} hostile npc(s) removed");
    }
}

using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs
{

    public class ExampleCrowdControlledDebuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {

            player.SetCCed();

            player.GetModPlayer<ExampleCrowdControlledDebuffPlayer>().exampleCrowdControlledDebuff = true;

            player.controlJump = false;
            player.controlDown = false;
            player.controlLeft = false;
            player.controlRight = false;
            player.controlUp = false;
            player.controlUseItem = false;
            player.controlUseTile = false;
            player.controlThrow = false;
            player.gravDir = 1f;
        }
    }

    public class ExampleCrowdControlledDebuffPlayer : ModPlayer
    {
        public bool exampleCrowdControlledDebuff;

        public override void ResetEffects() {
            exampleCrowdControlledDebuff = false;
        }

        public override void PostUpdateMiscEffects() {
            if (!exampleCrowdControlledDebuff)
                return;

            Player.pulley = false;

            if (Player.mount.Active) {
                Player.mount.Dismount(Player);
            }
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright) {
            if (exampleCrowdControlledDebuff) {
                g *= 0.2f;
                r *= 0.2f;
            }
        }
    }
}

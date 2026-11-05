using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs
{
    public class ExampleDrawBehindNPC : ModNPC
    {
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 6;
        }

        public override void SetDefaults() {
            NPC.width = 30;
            NPC.height = 40;
            NPC.aiStyle = -1;
            NPC.damage = 0;
            NPC.defense = 2;
            NPC.lifeMax = 100;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
        }

        private int CurrentLayer => (int)(NPC.ai[0] / 40);

        public override void FindFrame(int frameHeight) {
            NPC.frame.Y = CurrentLayer * frameHeight;
        }

        public override void AI() {
            NPC.ai[0] = (NPC.ai[0] + 1) % 240;

            NPC.hide = false;
            NPC.behindTiles = false;

            switch (CurrentLayer) {
                case 0:
                case 1:
                case 4:
                case 5:
                    NPC.hide = true;
                    break;
                case 2:
                    NPC.behindTiles = true;
                    break;
                case 3:
                    break;
            }
        }

        public override void DrawBehind(int index) {
            switch (CurrentLayer) {
                case 0:
                    Main.instance.DrawCacheNPCsMoonMoon.Add(index);
                    break;
                case 1:
                    Main.instance.DrawCacheNPCsBehindNonSolidTiles.Add(index);
                    break;
                case 2:
                case 3:
                    break;
                case 4:
                    Main.instance.DrawCacheNPCProjectiles.Add(index);
                    break;
                case 5:
                    Main.instance.DrawCacheNPCsOverPlayers.Add(index);
                    break;
            }
        }

        public override bool PreHoverInteract(bool mouseIntersects) {
            if (CurrentLayer < 3) {
                return true;
            }

            Player player = Main.LocalPlayer;
            int keyItem = ModContent.ItemType<Items.Placeable.ExampleBlock>();
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = keyItem;
            player.cursorItemIconText = "";
            player.noThrow = 2;

            if (!player.dead) {
                PlayerInput.SetZoom_MouseInWorld();
                if (Main.mouseRight && Main.npcChatRelease) {
                    Main.npcChatRelease = false;
                    if (PlayerInput.UsingGamepad) {
                        player.releaseInventory = false;
                    }

                    if (player.talkNPC != NPC.whoAmI && !player.tileInteractionHappened) {
                        if (player.HasItem(keyItem) && player.ConsumeItem(keyItem)) {
                            SoundEngine.PlaySound(SoundID.Item14);
                            NPC.SimpleStrikeNPC(1000, 0);
                        }
                        else {
                            SoundEngine.PlaySound(SoundID.MenuClose);
                        }
                    }
                }
            }

            return false;
        }

        public override bool CanChat() {
            if (CurrentLayer < 3) {
                return base.CanChat();
            }

            return true;
        }
    }
}

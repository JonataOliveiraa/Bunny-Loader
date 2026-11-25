using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs
{
    // This example is very advanced. It shows how to use shaders, manual NPC drawing, CheckDead usage, and making a death animation. It also has a fairly basic custom AI that acts fairly similar to the Dungeon Guardian.
    // The shader is Assets/Effects/ExampleEffectDeath.fx, compiled to ExampleEffectDeath.fxc. It erases the sprite where a noise image (uImage1) is brighter than uOpacity, with a pink edge, so lowering uOpacity dissolves the NPC.
    // Spawn this NPC with something like Cheat Sheet or Hero's Mod to view the effect.
    public class ExampleDeathAnimation : ModNPC
    {
        public const string ShaderName = "ExampleMod:DeathAnimation";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 2;

            // Avoid loading assets on dedicated servers. They don't use graphics cards.
            if (!Main.dedServ) {
                // A MiscShaderData is a shader any code can apply by name. UseImage1 gives the shader its uImage1 texture: a noise image from the game.
                GameShaders.Misc[ShaderName] = new MiscShaderData(Mod.Assets.Request<Effect>("Assets/Effects/ExampleEffectDeath"), "DeathAnimation").UseImage1("Images/Misc/Perlin");
            }
        }

        public override void SetDefaults() {
            NPC.width = 120;
            NPC.height = 120;
            NPC.aiStyle = -1;
            NPC.damage = 10;
            NPC.defense = 2;
            NPC.lifeMax = 100;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;

            // Because our width and height don't match the texture size, we use DrawOffsetY to attempt to center the drawing of the NPC. This lets the hitbox better conform to the shape of our NPC. Hitboxes don't rotate, so this approach is needed to let the hitbox better represent the position of the damageable portion of the NPC.
            DrawOffsetY = 30;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            // Spawn this NPC with something like Cheat Sheet or Hero's Mod
            return 0f;
        }

        // PreDraw and PostDraw are responsible for applying and then removing the shader. If you omit PostDraw, the following NPC to be drawn will inherit the shader, so don't do that.
        // Basically, we need to End the previous spriteBatch, start it again, apply our shader, draw the NPC, and finally End and Start a fresh spriteBatch.
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);

            // Retrieve reference to shader
            var deathShader = GameShaders.Misc[ShaderName];

            // Reset back to default value.
            deathShader.UseOpacity(1f);
            // We use NPC.ai[3] as a counter since the real death.
            if (NPC.ai[3] > 30f) {
                // Our shader uses the Opacity register to drive the effect. See ExampleEffectDeath.fx to see how the Opacity parameter factors into the shader math.
                deathShader.UseOpacity(1f - (NPC.ai[3] - 30f) / 150f);
            }
            // Call Apply to apply the shader to the SpriteBatch. Only 1 shader can be active at a time.
            deathShader.Apply(null);
            return true;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // As mentioned above, be sure not to forget this step.
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // We use CheckDead to delay death providing time for our death drama to happen. The logic here is a bit complicated, if you are curious, please step through AI and CheckDead in Visual Studio to see how CheckDead prevents death the first time but allows it after the death drama has finished.
        public override bool CheckDead() {
            if (NPC.ai[3] == 0f) {
                NPC.ai[3] = 1f;
                NPC.damage = 0;
                NPC.life = NPC.lifeMax;
                NPC.dontTakeDamage = true;
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        public override void FindFrame(int frameHeight) {
            if (NPC.ai[3] > 0f) {
                // If our ModNPC is currently dying, show the sad face part of the sprite.
                NPC.frame.Y = frameHeight;
            }
        }

        // This AI was adapted from the Dungeon Guardian AI and then modified. Below are some notes I used while figuring out what each ai value represented. It is a good skill to develop if you wish to make AI.
        // NPC.ai[0]: Just Spawned
        //		0: just spawned, needs target
        //		1: has target
        // NPC.ai[1]:
        //		0: No roar yet
        //		2: played roar, chasing
        //		3: Retreat Down
        // NPC.ai[2]: Not Used
        // NPC.ai[3]:
        //		0: death drama not activated
        //		>0: dying animation timer
        public override void AI() {
            // death drama
            if (NPC.ai[3] > 0f) {
                NPC.dontTakeDamage = true;
                NPC.ai[3] += 1f; // increase our death timer.
                NPC.velocity.X *= 0.95f; // lose inertia
                if (NPC.velocity.Y < 0.5f) {
                    NPC.velocity.Y = NPC.velocity.Y + 0.02f;
                }
                if (NPC.velocity.Y > 0.5f) {
                    NPC.velocity.Y = NPC.velocity.Y - 0.02f;
                }
                if (Main.rand.NextBool(5) && NPC.ai[3] < 120f) {
                    // This dust spawn adapted from the Pillar death code in vanilla.
                    for (int dustNumber = 0; dustNumber < 3; dustNumber++) {
                        Dust dust = Main.dust[Dust.NewDust(NPC.Left, NPC.width, NPC.height / 2, DustID.PinkTorch, 0f, 0f, 0, default(Color), 1f)];
                        dust.position = NPC.Center + Vector2.UnitY.RotatedByRandom(4.1887903213500977) * new Vector2(NPC.width * 1.5f, NPC.height * 1.1f) * 0.8f * (0.8f + Main.rand.NextFloat() * 0.2f);
                        dust.velocity.X = 0f;
                        dust.velocity.Y = -Math.Abs(dust.velocity.Y - (float)dustNumber + NPC.velocity.Y - 4f) * 3f;
                        dust.noGravity = true;
                        dust.fadeIn = 1f;
                        dust.scale = 1f + Main.rand.NextFloat() + (float)dustNumber * 0.3f;
                    }
                }

                if (NPC.ai[3] % 60f == 1f) {
                    SoundEngine.PlaySound(SoundID.NPCDeath22, NPC.Center); // every second while dying, play a sound
                }
                if (NPC.ai[3] >= 180f) {
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.checkDead(); // This will trigger ModNPC.CheckDead the second time, causing the real death.
                }
                return;
            }

            // Below this point is the normal AI code.
            if (NPC.ai[0] == 0f && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.TargetClosest(true);
                NPC.ai[0] = 1f;
            }
            if (NPC.ai[1] != 3f && NPC.ai[1] != 2f) {
                SoundEngine.PlaySound(SoundID.Roar, NPC.position);
                NPC.ai[1] = 2f;
            }
            Player target = Main.player[NPC.target];
            if (target.dead || Math.Abs(NPC.position.X - target.position.X) > 2000f || Math.Abs(NPC.position.Y - target.position.Y) > 2000f) {
                NPC.TargetClosest(true);
                target = Main.player[NPC.target];
                if (target.dead || Math.Abs(NPC.position.X - target.position.X) > 2000f || Math.Abs(NPC.position.Y - target.position.Y) > 2000f) {
                    NPC.ai[1] = 3f;
                }
            }
            if (NPC.ai[1] == 2f) {
                NPC.rotation += NPC.direction * 0.03f;
                if (Vector2.Distance(target.Center, NPC.Center) > 250) {
                    NPC.velocity += Vector2.Normalize(target.Center - NPC.Center) * new Vector2(.3f, .1f);
                }

                NPC.velocity *= 0.98f;
                NPC.velocity.X = Utils.Clamp(NPC.velocity.X, -4, 4);
                NPC.velocity.Y = Utils.Clamp(NPC.velocity.Y, -2, 2);
            }
            else if (NPC.ai[1] == 3f) {
                NPC.velocity.Y = NPC.velocity.Y + 0.1f;
                if (NPC.velocity.Y < 0f) {
                    NPC.velocity.Y = NPC.velocity.Y * 0.95f;
                }
                NPC.velocity.X = NPC.velocity.X * 0.95f;
                if (NPC.timeLeft > 50) {
                    NPC.timeLeft = 50;
                }
            }
        }
    }
}

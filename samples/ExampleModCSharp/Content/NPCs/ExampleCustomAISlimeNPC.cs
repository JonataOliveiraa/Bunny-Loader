using Terraria.ModLoader.Utilities;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs
{

	public class ExampleCustomAISlimeNPC : ModNPC
	{

		private enum ActionState
		{
			Asleep,
			Notice,
			Jump,
			Hover,
			Fall
		}

		private enum Frame
		{
			Asleep,
			Notice,
			Falling,
			Flutter1,
			Flutter2,
			Flutter3
		}

		public ref float AI_State => ref NPC.ai[0];
		public ref float AI_Timer => ref NPC.ai[1];
		public ref float AI_FlutterTime => ref NPC.ai[2];

		public static LocalizedText GotStompedText { get; private set; }

		public override void SetStaticDefaults() {
			Main.npcFrameCount[Type] = 6;

			NPCID.Sets.ShimmerTransformToNPC[Type] = NPCID.ShimmerSlime;

			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;

			GotStompedText = this.GetLocalization("GotStomped");
		}

		public override void SetDefaults() {
			NPC.width = 36;
			NPC.height = 36;
			NPC.aiStyle = -1;
			NPC.damage = 7;
			NPC.defense = 2;
			NPC.lifeMax = 25;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.value = 25f;

		}

		public override void AI() {

			switch (AI_State) {
				case (float)ActionState.Asleep:
					FallAsleep();
					break;
				case (float)ActionState.Notice:
					Notice();
					break;
				case (float)ActionState.Jump:
					Jump();
					break;
				case (float)ActionState.Hover:
					Hover();
					break;
				case (float)ActionState.Fall:
					if (NPC.velocity.Y == 0) {
						NPC.velocity.X = 0;
						AI_State = (float)ActionState.Asleep;
						AI_Timer = 0;
					}

					break;
			}
		}

		public override void FindFrame(int frameHeight) {

			NPC.spriteDirection = NPC.direction;

			switch (AI_State) {
				case (float)ActionState.Asleep:

					NPC.frame.Y = (int)Frame.Asleep * frameHeight;
					break;
				case (float)ActionState.Notice:

					if (AI_Timer < 10) {
						NPC.frame.Y = (int)Frame.Notice * frameHeight;
					}
					else {
						NPC.frame.Y = (int)Frame.Asleep * frameHeight;
					}

					break;
				case (float)ActionState.Jump:
					NPC.frame.Y = (int)Frame.Falling * frameHeight;
					break;
				case (float)ActionState.Hover:

					NPC.frameCounter++;

					if (NPC.frameCounter < 10) {
						NPC.frame.Y = (int)Frame.Flutter1 * frameHeight;
					}
					else if (NPC.frameCounter < 20) {
						NPC.frame.Y = (int)Frame.Flutter2 * frameHeight;
					}
					else if (NPC.frameCounter < 30) {
						NPC.frame.Y = (int)Frame.Flutter3 * frameHeight;
					}
					else {
						NPC.frameCounter = 0;
					}

					break;
				case (float)ActionState.Fall:
					NPC.frame.Y = (int)Frame.Falling * frameHeight;
					break;
			}
		}

		private void FallAsleep() {

			NPC.TargetClosest(true);

			if (NPC.HasValidTarget && Main.player[NPC.target].Distance(NPC.Center) < 500f) {

				AI_State = (float)ActionState.Notice;
				AI_Timer = 0;
			}
		}

		private void Notice() {

			if (Main.player[NPC.target].Distance(NPC.Center) < 250f) {

				AI_Timer++;

				if (AI_Timer >= 20) {
					AI_State = (float)ActionState.Jump;
					AI_Timer = 0;
				}
			}
			else {
				NPC.TargetClosest(true);

				if (!NPC.HasValidTarget || Main.player[NPC.target].Distance(NPC.Center) > 500f) {

					AI_State = (float)ActionState.Asleep;
					AI_Timer = 0;
				}
			}
		}

		private void Jump() {
			AI_Timer++;

			if (AI_Timer == 1) {

				NPC.velocity = new Vector2(NPC.direction * 2, -10f);
			}
			else if (AI_Timer > 40) {

				AI_State = (float)ActionState.Hover;
				AI_Timer = 0;
			}
		}

		private void Hover() {
			AI_Timer++;

			if (AI_Timer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {

				AI_FlutterTime = Main.rand.NextBool() ? 100 : 50;

				NPC.netUpdate = true;
			}

			NPC.velocity += new Vector2(0, -.35f);

			if (Math.Abs(NPC.velocity.X) < 2) {
				NPC.velocity += new Vector2(NPC.direction * .05f, 0);
			}

			if (AI_Timer > AI_FlutterTime) {
				AI_State = (float)ActionState.Fall;
				AI_Timer = 0;
			}
		}

		public override float SpawnChance(NPC.Spawner spawner) => SpawnCondition.OverworldDaySlime.Chance * 0.1f;
	}
}

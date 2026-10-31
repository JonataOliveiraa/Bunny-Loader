using System;
using System.Collections.Generic;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace Terraria.ModLoader.Utilities;

internal static class NPCSpawnHelper
{
	internal static List<SpawnCondition> conditions = new List<SpawnCondition>();

	internal static void Reset()
	{
		foreach (SpawnCondition cond in conditions) {
			cond.Reset();
		}
	}

	internal static void DoChecks(NPC.Spawner spawner)
	{
		float weight = 1f;
		foreach (SpawnCondition cond in conditions) {
			cond.Check(spawner, ref weight);
			if (Math.Abs(weight) < 5E-6) {
				break;
			}
		}
	}
}

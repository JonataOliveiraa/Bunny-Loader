using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace ExampleMod.Common.Systems
{

    public class ModIntegrationsSystem : ModSystem
    {
        public override void PostSetupContent() {

            DoBossChecklistIntegration();

        }

        private void DoBossChecklistIntegration() {

            if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklistMod)) {
                return;
            }

            if (bossChecklistMod.Version < new Version(1, 6)) {
                return;
            }

            string internalName = "MinionBoss";

            float weight = 0.7f;

            Func<bool> downed = () => DownedBossSystem.downedMinionBoss;

            if (!ModContent.TryFind<ModNPC>("ExampleMod/MinionBossBody", out var boss)) return;
            int bossType = boss.Type;

            if (!ModContent.TryFind<ModItem>("ExampleMod/MinionBossSummonItem", out var summon)) return;
            int spawnItem = summon.Type;

            var collectibles = new List<int>();
            foreach (string name in new[] { "MinionBossRelic", "MinionBossPetItem", "MinionBossTrophy", "MinionBossMask" })
                if (ModContent.TryFind<ModItem>("ExampleMod/" + name, out var collectible)) collectibles.Add(collectible.Type);

            var customPortrait = (SpriteBatch sb, Rectangle rect, Color color) => {
                Texture2D texture = ModContent.Request<Texture2D>("ExampleMod/Assets/Textures/Bestiary/MinionBoss_Preview").Value;
                Vector2 centered = new Vector2(rect.X + (rect.Width / 2) - (texture.Width / 2), rect.Y + (rect.Height / 2) - (texture.Height / 2));
                sb.Draw(texture, centered, color);
            };

            bossChecklistMod.Call(
                "LogBoss",
                Mod,
                internalName,
                weight,
                downed,
                bossType,
                new Dictionary<string, object>() {
                    ["spawnItems"] = spawnItem,
                    ["collectibles"] = collectibles,
                    ["customPortrait"] = customPortrait
                    
                }
            );

        }
    }
}

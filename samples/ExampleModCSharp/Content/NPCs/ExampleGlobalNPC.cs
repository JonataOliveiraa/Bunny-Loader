using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Content.NPCs
{
    public class ExampleGlobalNPC : GlobalNPC
    {
        public bool HasBeenHitByPlayer;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return lateInstantiation && entity.townNPC;
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner != 255 && projectile.friendly) {
                HasBeenHitByPlayer = true;
            }
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            HasBeenHitByPlayer = true;
        }

        public override void ModifyActiveShop(NPC npc, string shopName, Item[] items) {
            if (!npc.GetGlobalNPC<ExampleGlobalNPC>().HasBeenHitByPlayer) {
                return;
            }

            foreach (Item item in items) {
                if (item.IsNull || item.type == ItemID.None) {
                    continue;
                }

                int value = (int?)item.shopCustomPrice ?? item.value;
                item.shopCustomPrice = value * 2;
            }
        }

        public override void SaveData(NPC npc, TagCompound tag) {
            if (HasBeenHitByPlayer) {
                tag.Add("HasBeenHitByPlayer", true);
            }
        }

        public override void LoadData(NPC npc, TagCompound tag) {
            HasBeenHitByPlayer = tag.ContainsKey("HasBeenHitByPlayer");
        }
    }
}

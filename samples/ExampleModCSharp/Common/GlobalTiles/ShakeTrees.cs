using Microsoft.Xna.Framework;
using ExampleMod.Content.Items;
using ExampleMod.Content.NPCs;
using ExampleMod.Content.Projectiles;
using ExampleMod.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalTiles;

public class GlobalShakeTrees : GlobalTile
{
    public override bool ShakeTree(int x, int y, TreeTypes treeType)
    {
        if (treeType == TreeTypes.Forest && WorldGen.genRand.NextBool(20))
        {
            Item.NewItem(WorldGen.GetItemSource_FromTreeShake(x, y), new Point(x, y).ToWorldCoordinates(), ModContent.ItemType<ExampleItem>());

            return true;
        }

        if (treeType == TreeTypes.Mushroom && WorldGen.genRand.NextBool(10))
        {
            Item.NewItem(WorldGen.GetItemSource_FromTreeShake(x, y), new Point(x, y).ToWorldCoordinates(), ItemID.MushroomTorch, WorldGen.genRand.Next(3, 11));
            return true;
        }

        if (treeType == TreeTypes.Snow && WorldGen.genRand.NextBool(20))
        {
            Projectile.NewProjectile(new EntitySource_ShakeTree(x, y), x * 16, y * 16, Main.rand.Next(-16, 16), 0f, ModContent.ProjectileType<Content.Projectiles.ExamplePaperAirplaneProjectile>(), Damage: 4, KnockBack: 1f, Owner: Main.myPlayer);
            return true;
        }

        if (treeType == TreeTypes.Jungle && WorldGen.genRand.NextBool(7) && y < Main.worldSurface && Main.hardMode)
        {
            NPC.NewNPC(WorldGen.GetNPCSource_ShakeTree(x, y), x * 16, y * 16, NPCID.GiantFlyingFox);
            return true;
        }

        if (treeType == TreeTypes.Custom && WorldGen.genRand.NextBool(2) && !Main.dayTime)
        {
            NPC.NewNPC(WorldGen.GetNPCSource_ShakeTree(x, y), x * 16, y * 16, ModContent.NPCType<PartyZombie>());
            return true;
        }

        if (treeType == TreeTypes.Custom)
        {
            WorldGen.GetTreeBottom(x, y, out int baseX, out int baseY);

            if (Main.tile[baseX, baseY].TileType == ModContent.TileType<ExampleBlock>() && WorldGen.genRand.NextBool(2))
            {
                Item.NewItem(WorldGen.GetItemSource_FromTreeShake(x, y), new Point(x, y).ToWorldCoordinates(), ItemID.Coconut);
                return true;
            }
        }

        return false;
    }

    public override void PreShakeTree(int x, int y, TreeTypes treeType)
    {
        if (WorldGen.genRand.NextBool(5) && Main.noTrapsWorld)
        {
            Projectile.NewProjectile(new EntitySource_ShakeTree(x, y), x * 16, y * 16, Main.rand.Next(-100, 101) * 0.002f, 8f, ProjectileID.WoodenArrowHostile, Damage: 10, KnockBack: 0f, Owner: Main.myPlayer);
        }

        if (treeType == TreeTypes.Forest && !Main.dayTime)
        {
            NPCLoader.blockLoot.Add(ItemID.Acorn);
        }
    }
}

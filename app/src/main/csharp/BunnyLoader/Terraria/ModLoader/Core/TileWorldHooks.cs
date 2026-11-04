using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe partial class TileHooks
{
    internal static void InstallWorld(ModTile tile)
    {
        if (TileLoader.Overrides(tile, nameof(ModTile.FloorVisuals)))
            Once("FloorVisuals", () => On_Player.FloorVisuals += FloorVisuals);
        if (TileLoader.Overrides(tile, nameof(ModTile.HasWalkDust)))
            Once("WalkDust", () => On_Player.MakeFloorDust += WalkDust);
        if (TileLoader.Overrides(tile, nameof(ModTile.DropCritterChance)))
            Once("Bait", () => On_WorldGen.KillTile_DropBait += DropBait);
        if (TileLoader.Overrides(tile, nameof(ModTile.SwitchTiles)))
            Once("SwitchTiles", () => On_Collision.SwitchTiles += SwitchTiles);
        if (TileLoader.Overrides(tile, nameof(ModTile.HitSwitch)))
            Once("HitSwitch", () => On_Wiring.HitSwitch += HitSwitch);
        if (TileLoader.Overrides(tile, nameof(ModTile.Convert)))
            TileConversionHooks.Install();
    }

    internal static void InstallWorld(GlobalTile global)
    {
        if (TileLoader.Overrides(global, nameof(GlobalTile.PreShakeTree)) || TileLoader.Overrides(global, nameof(GlobalTile.ShakeTree)))
        {
            Once("ShakeTree", () => On_WorldGen.ShakeTree += ShakeTree);
            Once("BlockLoot", () => On_Item.NewItem_IEntitySource_int_int_int_int_int_int_bool_int_NewItemOwnership_Nullable_NewItemModifier += BlockLoot);
        }
        if (TileLoader.Overrides(global, nameof(GlobalTile.OnTileConverted)))
            TileConversionHooks.Install();
    }

    static void FloorVisuals(On_Player.orig_FloorVisuals orig, Player self, bool falling)
    {
        orig(self, falling);
        int x = (int)((self.position.X + self.width / 2) / 16f), y = (int)((self.position.Y + self.height) / 16f);
        if (self.gravDir == -1f)
            y = (int)(self.position.Y - 0.1f) / 16;
        if (!WorldGen.InWorld(x, y, 1))
            return;
        Tile tile = Main.tile[x, y];
        if (tile.active() && (Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type]))
            TileLoader.GetTile(tile.type)?.FloorVisuals(self);
    }

    static void WalkDust(On_Player.orig_MakeFloorDust orig, Player self, bool falling, int type, int frameX, int frameY, int paintColor)
    {
        if (TileLoader.GetTile(type) is not ModTile tile || !tile.HasWalkDust())
        {
            orig(self, falling, type, frameX, frameY, paintColor);
            return;
        }

        int dustType = -1;
        bool make = true;
        Color color = default;
        tile.WalkDust(ref dustType, ref make, ref color);
        if (dustType < 0 || !make)
            return;
        int count = falling ? 20 : 1;
        float speed = MathHelper.Clamp(self.velocity.X, -6f, 6f);
        for (int k = 0; k < count; k++)
        {
            if (!falling && Main.rand.Next(100) > Math.Abs(self.velocity.X) / 3f * 100f)
                continue;
            if (!self.CanSpawnWalkingEffects() && !falling)
                continue;
            int index = Dust.NewDust(new Vector2(self.position.X, self.position.Y + self.height - 2f), self.width, 6, dustType, 0f, 0f, 50, color);
            Dust dust = Main.dust[index];
            if (self.gravDir == -1f)
                dust.position.Y -= self.height + 4;
            dust.noGravity = true;
            if (count > 1)
            {
                dust.velocity.X *= 1.2f;
                dust.velocity.Y *= 0.8f;
                dust.velocity.Y -= 1f;
                dust.velocity *= 0.8f;
                dust.scale += Main.rand.Next(3) * 0.1f;
                dust.velocity.X = (dust.position.X - (self.position.X + self.width / 2)) * 0.2f;
                if (dust.velocity.Y > 0f)
                    dust.velocity.Y *= -1f;
                dust.velocity.X += speed * 0.3f;
            }
            else
                dust.velocity *= 0.2f;
            dust.position.X -= speed;
            if (self.gravDir == -1f)
                dust.velocity.Y *= -1f;
        }
    }

    static void DropBait(On_WorldGen.orig_KillTile_DropBait orig, int i, int j, Tile tileCache)
    {
        if (TileLoader.GetTile(tileCache.type) is not ModTile tile)
        {
            orig(i, j, tileCache);
            return;
        }

        int worm = -1, grasshopper = -1, grub = -1;
        tile.DropCritterChance(i, j, ref worm, ref grasshopper, ref grub);
        int closest = Player.FindClosest(new Vector2(i * 16, j * 16), 16, 16);
        int wormType = Main.player[closest].ZoneGraveyard ? NPCID.Maggot : NPCID.Worm;
        IEntitySource source = new EntitySource_TileBreak(i, j);
        if (worm > 0 && NPC.CountNPCS(wormType) < 5 && WorldGen.genRand.Next(worm) == 0)
            NPC.NewNPC(source, i * 16 + 10, j * 16, wormType);
        if (grasshopper > 0 && NPC.CountNPCS(NPCID.Grasshopper) < 5 && WorldGen.genRand.Next(grasshopper) == 0)
            NPC.NewNPC(source, i * 16 + 10, j * 16, NPCID.Grasshopper);
        if (grub > 0 && NPC.CountNPCS(NPCID.Grubby) < 5 && WorldGen.genRand.Next(grub) == 0)
            NPC.NewNPC(source, i * 16 + 10, j * 16, NPCID.Grubby);
    }

    static bool SwitchTiles(On_Collision.orig_SwitchTiles orig, Entity entity, Vector2 position, int width, int height, Vector2 oldPosition, int objType)
    {
        int left = Math.Max((int)(position.X / 16f) - 1, 0), right = Math.Min((int)((position.X + width) / 16f) + 2, Main.maxTilesX - 1);
        int top = Math.Max((int)(position.Y / 16f) - 1, 0), bottom = Math.Min((int)((position.Y + height) / 16f) + 2, Main.maxTilesY - 1);
        for (int i = left; i < right; i++)
        {
            for (int j = top; j < bottom; j++)
            {
                Tile tile = Main.tile[i, j];
                if (tile.active() && TileLoader.GetTile(tile.type) is ModTile mod && mod.SwitchTiles(i, j, entity, position, width, height, oldPosition, objType))
                    return true;
            }
        }

        return orig(entity, position, width, height, oldPosition, objType);
    }

    static void HitSwitch(On_Wiring.orig_HitSwitch orig, int i, int j)
    {
        if (WorldGen.InWorld(i, j))
            TileLoader.GetTile(Main.tile[i, j].type)?.HitSwitch(i, j);
        orig(i, j);
    }

    static int BlockLoot(On_Item.orig_NewItem_IEntitySource_int_int_int_int_int_int_bool_int_NewItemOwnership_Nullable_NewItemModifier orig, global::Terraria.DataStructures.IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, global::Terraria.NewItemOwnership ownership, global::BunnyLoader.Interop.GameNullable<global::Microsoft.Xna.Framework.Vector2> velocity, global::Terraria.Item.NewItemModifier modifier)
    {
        if (NPCLoader.blockLoot.Count > 0 && NPCLoader.blockLoot.Contains(type))
            return Main.maxItems;
        return orig(source, X, Y, Width, Height, type, stack, noBroadcast, prefix, ownership, velocity, modifier);
    }

    static void ShakeTree(On_WorldGen.orig_ShakeTree orig, int i, int j)
    {
        if (WorldGen.numTreeShakes == WorldGen.maxTreeShakes)
            return;
        TreeTypes treeType = WorldGen.GetTreeTypeAndTreeBottom(i, j, out int x, out int y);
        if (treeType == TreeTypes.None)
            return;
        for (int k = 0; k < WorldGen.numTreeShakes; k++)
        {
            if (WorldGen.treeShakeX[k] == x && WorldGen.treeShakeY[k] == y)
                return;
        }

        int ground = y, top = y - 1;
        while (top > 10 && Main.tile[x, top].active() && TileID.Sets.IsShakeable[Main.tile[x, top].type])
            top--;
        top++;
        if (!WorldGen.IsTileALeafyTreeTop(x, top) || Collision.SolidTiles(x - 2, x + 2, top - 2, top + 2))
        {
            orig(i, j);
            return;
        }

        foreach (GlobalTile global in TileLoader.Hooks.PreShakeTree)
            global.PreShakeTree(x, top, treeType);
        bool handled = false;
        foreach (GlobalTile global in TileLoader.Hooks.ShakeTree)
            handled |= global.ShakeTree(x, top, treeType);
        if (!handled)
        {
            orig(i, j);
            NPCLoader.blockLoot.Clear();
            return;
        }

        NPCLoader.blockLoot.Clear();
        WorldGen.treeShakeX[WorldGen.numTreeShakes] = x;
        WorldGen.treeShakeY[WorldGen.numTreeShakes] = y;
        WorldGen.numTreeShakes++;
        int treeHeight = 0;
        WorldGen.GetTreeLeaf(x, Main.tile[x, top], Main.tile[x, ground], ref treeHeight, out int _, out int passStyle);
        if (passStyle == -1)
            return;
        if ((Main.netMode & NetmodeID.Server) != 0)
            NetMessage.SendData(MessageID.SpecialFX, -1, -1, default, 1, x, top, 1f, passStyle);
        if (Main.netMode != NetmodeID.MultiplayerClient)
            WorldGen.TreeGrowFX(x, top, 1, passStyle, true);
    }
}

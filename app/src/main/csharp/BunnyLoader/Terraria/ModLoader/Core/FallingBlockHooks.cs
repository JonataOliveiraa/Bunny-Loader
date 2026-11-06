using System;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class FallingBlockHooks
{
    internal static void WantTiles() => ItemHooks.Once("FallingTiles", () => On_WorldGen.GetSandfallProjData += GetSandfallProjData);

    internal static void WantProjectiles() => ItemHooks.Once("FallingProjectiles", () => On_Projectile.Kill += Kill);

    internal static void WantAmmo() => ItemHooks.Once("SandgunAmmo", () => On_Player.PickAmmo += PickAmmo);

    static void GetSandfallProjData(On_WorldGen.orig_GetSandfallProjData orig, int type, out int projType, out int dmg)
    {
        if (type >= TileID.Count && TileID.Sets.FallingBlockProjectile[type] is TileID.Sets.FallingBlockProjectileInfo info)
        {
            projType = info.FallingProjectileType;
            dmg = info.FallingProjectileDamage;
            return;
        }
        orig(type, out projType, out dmg);
    }

    static void Kill(On_Projectile.orig_Kill orig, Projectile self)
    {
        if (!self.active || self.aiStyle != 10 || self.type < ProjectileID.Count || ProjectileID.Sets.FallingBlockTileItem[self.type] is not ProjectileID.Sets.FallingBlockTileItemInfo info)
        {
            orig(self);
            return;
        }
        if (self.owner == Main.myPlayer)
            Land(self, info);
        self.aiStyle = 0;
        orig(self);
        self.aiStyle = 10;
    }

    static void Land(Projectile self, ProjectileID.Sets.FallingBlockTileItemInfo info)
    {
        int x = (int)(self.position.X + self.width / 2) / 16;
        int y = (int)(self.position.Y + self.height / 2) / 16;
        int tileType = info.TileType, itemType = info.ItemType;
        if (Main.tile[x, y].nactive() && Main.tile[x, y].halfBrick() && self.velocity.Y > 0f && Math.Abs(self.velocity.Y) > Math.Abs(self.velocity.X))
            y--;
        if (Main.tile[x, y].active() || tileType < 0)
        {
            if (itemType > 0)
                Item.RequestNewItem(self.GetItemSource_DropAsItem(), self.Center, itemType);
            return;
        }
        bool placed = false, blocked = false;
        if (y < Main.maxTilesY - 2)
        {
            Tile below = Main.tile[x, y + 1];
            if (below.active())
            {
                if (below.type == TileID.MinecartTrack || below.type == TileID.ConveyorBeltLeft || below.type == TileID.ConveyorBeltRight || WorldGen.BlockBelowMakesSandFall(x, y))
                    blocked = true;
            }
        }
        if (!blocked)
            placed = WorldGen.PlaceTile(x, y, tileType, forced: true);
        if (!blocked && Main.tile[x, y].active() && Main.tile[x, y].type == tileType)
        {
            if (Main.tile[x, y + 1].halfBrick() || Main.tile[x, y + 1].slope() != 0)
            {
                WorldGen.SlopeTile(x, y + 1);
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendData(MessageID.TileManipulation, -1, -1, default, 14, x, y + 1);
            }
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, default, 1, x, y, tileType);
        }
        else if (!placed && itemType > 0)
        {
            Item.RequestNewItem(self.GetItemSource_DropAsItem(), self.Center + new Vector2(0f, -2f), itemType);
        }
    }

    static void PickAmmo(On_Player.orig_PickAmmo orig, Player self, Item sItem, ref int projToShoot, ref float speed, ref bool canShoot, ref int totalDamage, ref float knockBack, out int usedAmmoItemId, bool dontConsume)
    {
        orig(self, sItem, ref projToShoot, ref speed, ref canShoot, ref totalDamage, ref knockBack, out usedAmmoItemId, dontConsume);
        if (usedAmmoItemId >= ItemID.Count && ItemID.Sets.SandgunAmmoProjectileData[usedAmmoItemId] is ItemID.Sets.SandgunAmmoInfo info)
        {
            projToShoot = info.ProjectileType;
            totalDamage += info.BonusDamage;
        }
    }
}

using System;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace Terraria;

public readonly partial struct Projectile
{
    public IEntitySource GetSource_DropAsItem(string? context = null) => GetItemSource_DropAsItem();
    public IEntitySource GetSource_Death(string? context = null) => GetItemSource_FromThis();

    public static void KillOldestJavelin(int protectedProjectileIndex, int projectileType, int targetNPCIndex, Point[] bufferForScan)
    {
        ArgumentNullException.ThrowIfNull(bufferForScan);
        if (bufferForScan.Length == 0) return;
        int count = 0;
        for (int index = 0; index < Main.maxProjectiles; index++)
        {
            Projectile projectile = Main.projectile[index];
            if (index == protectedProjectileIndex || !projectile.active || projectile.owner != Main.myPlayer || projectile.type != projectileType || projectile.ai[0] != 1f || projectile.ai[1] != targetNPCIndex) continue;
            bufferForScan[count++] = new Point(index, projectile.timeLeft);
            if (count >= bufferForScan.Length) break;
        }
        if (count < bufferForScan.Length) return;
        int oldest = 0;
        for (int i = 1; i < count; i++) if (bufferForScan[i].Y < bufferForScan[oldest].Y) oldest = i;
        Main.projectile[bufferForScan[oldest].X].Kill();
    }
}

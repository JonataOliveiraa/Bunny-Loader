using System;
using System.Collections.Generic;
using Terraria.ModLoader.Core;

namespace Terraria.ID;

public sealed class ContentTable<T> where T : class
{
    readonly Dictionary<int, T> values = new();

    readonly Action? onWrite;

    internal ContentTable(Action? onWrite = null)
    {
        this.onWrite = onWrite;
    }

    public T? this[int type]
    {
        get => values.TryGetValue(type, out T? value) ? value : null;
        set
        {
            onWrite?.Invoke();
            if (value == null)
                values.Remove(type);
            else
                values[type] = value;
        }
    }

    internal void Seed(int type, T value) => values[type] = value;
}

public sealed class ContentBoolTable : IEnumerable<int>
{
    readonly HashSet<int> values = new();

    public bool this[int type]
    {
        get => values.Contains(type);
        set
        {
            if (value)
                values.Add(type);
            else
                values.Remove(type);
        }
    }

    public void Add(int type) => values.Add(type);

    public IEnumerator<int> GetEnumerator() => values.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => values.GetEnumerator();
}

public sealed class ContentIntTable
{
    readonly Dictionary<int, int> values = new();

    readonly int fallback;

    readonly Action? onWrite;

    internal ContentIntTable(int fallback, Action? onWrite = null)
    {
        this.fallback = fallback;
        this.onWrite = onWrite;
    }

    public int this[int type]
    {
        get => values.TryGetValue(type, out int value) ? value : fallback;
        set
        {
            onWrite?.Invoke();
            values[type] = value;
        }
    }

    internal void Seed(int type, int value) => values[type] = value;

    internal IEnumerable<KeyValuePair<int, int>> All => values;
}

public readonly partial struct TileID
{
    public static unsafe partial class Sets
    {
        public sealed class FallingBlockProjectileInfo
        {
            public FallingBlockProjectileInfo(int FallingProjectileType, int FallingProjectileDamage = 10)
            {
                this.FallingProjectileType = FallingProjectileType;
                this.FallingProjectileDamage = FallingProjectileDamage;
            }

            public int FallingProjectileType { get; set; }

            public int FallingProjectileDamage { get; set; }
        }

        public static readonly ContentTable<FallingBlockProjectileInfo> FallingBlockProjectile = NewFalling();

        static ContentTable<FallingBlockProjectileInfo> NewFalling()
        {
            var table = new ContentTable<FallingBlockProjectileInfo>(FallingBlockHooks.WantTiles);
            table.Seed(TileID.Sand, new FallingBlockProjectileInfo(ProjectileID.SandBallFalling));
            table.Seed(TileID.Ebonsand, new FallingBlockProjectileInfo(ProjectileID.EbonsandBallFalling));
            table.Seed(TileID.Mud, new FallingBlockProjectileInfo(ProjectileID.MudBall));
            table.Seed(TileID.Pearlsand, new FallingBlockProjectileInfo(ProjectileID.PearlSandBallFalling));
            table.Seed(TileID.Silt, new FallingBlockProjectileInfo(ProjectileID.SiltBall));
            table.Seed(TileID.Slush, new FallingBlockProjectileInfo(ProjectileID.SlushBall));
            table.Seed(TileID.Crimsand, new FallingBlockProjectileInfo(ProjectileID.CrimsandBallFalling));
            table.Seed(TileID.CopperCoinPile, new FallingBlockProjectileInfo(ProjectileID.CopperCoinsFalling, 0));
            table.Seed(TileID.SilverCoinPile, new FallingBlockProjectileInfo(ProjectileID.SilverCoinsFalling, 0));
            table.Seed(TileID.GoldCoinPile, new FallingBlockProjectileInfo(ProjectileID.GoldCoinsFalling, 0));
            table.Seed(TileID.PlatinumCoinPile, new FallingBlockProjectileInfo(ProjectileID.PlatinumCoinsFalling, 0));
            table.Seed(TileID.ShellPile, new FallingBlockProjectileInfo(ProjectileID.ShellPileFalling));
            return table;
        }

        public static readonly ContentBoolTable Infectable = new()
        {
            1, 2, 53, 60, 69, 161, 179, 180, 181, 182, 183, 381, 396, 397, 534, 536, 539, 625, 627
        };

        public static readonly ContentIntTable PressurePlate = NewPlates();

        static ContentIntTable NewPlates()
        {
            var table = new ContentIntTable(-1, PressurePlateHooks.Want);
            table.Seed(TileID.PressurePlates, -2);
            table.Seed(TileID.WeightedPressurePlate, -3);
            table.Seed(TileID.ProjectilePressurePad, -4);
            return table;
        }
    }
}

public readonly partial struct ProjectileID
{
    public static unsafe partial class Sets
    {
        public sealed class FallingBlockTileItemInfo
        {
            public FallingBlockTileItemInfo(int TileType, int ItemType = 0)
            {
                this.TileType = TileType;
                this.ItemType = ItemType;
            }

            public int TileType { get; set; }

            public int ItemType { get; set; }
        }

        public static readonly ContentTable<FallingBlockTileItemInfo> FallingBlockTileItem = new(FallingBlockHooks.WantProjectiles);
    }
}

public readonly partial struct ItemID
{
    public readonly unsafe partial struct Sets
    {
        public sealed class SandgunAmmoInfo
        {
            public SandgunAmmoInfo(int ProjectileType, int BonusDamage = 0)
            {
                this.ProjectileType = ProjectileType;
                this.BonusDamage = BonusDamage;
            }

            public int ProjectileType { get; set; }

            public int BonusDamage { get; set; }
        }

        public static readonly ContentTable<SandgunAmmoInfo> SandgunAmmoProjectileData = new(FallingBlockHooks.WantAmmo);
    }
}

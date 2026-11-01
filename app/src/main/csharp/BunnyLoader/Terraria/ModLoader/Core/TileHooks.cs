using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ObjectData;

namespace Terraria.ModLoader.Core;

internal static partial class TileHooks
{
    static readonly HashSet<string> installed = new();

    [ThreadStatic]
    static int dustBudget;

    [ThreadStatic]
    static int killing;

    [ThreadStatic]
    static int placing;

    static Adjacency[] adjacency = Array.Empty<Adjacency>();

    internal static void AddMapName(int type, LocalizedText name, Func<string, int, int, string> callback)
    {
        throw new NotSupportedException("Coordinate-dependent map entry names are not supported by the mobile map legend");
    }

    static void Once(string name, Action action)
    {
        if (installed.Add(name))
            action();
    }

    internal static void Install(uint flags, bool own)
    {
        bool Has(int bit) => (flags & (1u << bit)) != 0;
        if (own || Has(0) || Has(4))
            Once("Kill", () => On_WorldGen.KillTile += Kill);
        if (own || Has(1))
            Once("CanKill", () => On_WorldGen.CanKillTile_int_int_outbool += CanKill);
        if (own || Has(6))
        {
            Once("Drop", () => On_WorldGen.KillTile_DropItems += Drop);
            Once("Drops", () => On_WorldGen.KillTile_GetItemDrops += Drops);
        }

        if (own || Has(5) || Has(4))
            Once("Dust", () => On_WorldGen.KillTile_MakeTileDust += MakeDust);
        if (own || Has(22))
            Once("Sound", () => On_WorldGen.KillTile_PlaySounds += Sound);
        if (own)
        {
            Once("Pick", () => On_Player.GetPickaxeDamage += Pick);
            Once("Resting", InstallResting);
        }

        if (Has(2))
            Once("CanPlace", () => On_Player.PlaceThing_Tiles_BlockPlacementForAssortedThings += CanPlace);
        if (Has(3))
        {
            Once("Placing", () => On_Player.PlaceThing_Tiles += Placing);
            Once("Place", () => On_WorldGen.PlaceTile += Place);
            Once("PlaceObject", () => On_TileObjectData.CallPostPlacementPlayerHook += PlaceObject);
        }

        if (Has(7))
            Once("Wire", () => On_Wiring.HitWireSingle += Wire);
        if (Has(18))
        {
            Once("Slope", () => On_WorldGen.SlopeTile += Slope);
            Once("Pound", () => On_WorldGen.PoundTile += Pound);
        }

        if (own || Has(25))
            Once("Explode", () => On_Projectile.CanExplodeTile += Explode);
        if (Has(21))
            Once("Replace", () => On_WorldGen.ReplaceTile += Replace);
        if (Has(11))
            Once("Animate", () => On_Main.AnimateTiles += Animate);
        if (Has(14))
            Once("Click", () => On_Player.TileInteractionsUse += Click);
        if (Has(14) || Has(15))
            Once("Mouse", () => On_Player.TileInteractionsMouseOver += Mouse);
        if (Has(16))
            Once("Far", () => On_Player.TileInteractionsCheckLongDistance += Far);
        InstallDrawing(flags);
    }

    internal static void InstallMusicBoxes()
    {
        Once("Click", () => On_Player.TileInteractionsUse += Click);
        Once("Mouse", () => On_Player.TileInteractionsMouseOver += Mouse);
        Once("Wire", () => On_Wiring.HitWireSingle += Wire);
    }

    static bool Explode(On_Projectile.orig_CanExplodeTile orig, Projectile projectile, int i, int j, bool ores)
    {
        if (!orig(projectile, i, j, ores))
            return false;
        int type = Main.tile[i, j].type;
        if (TileLoader.GetTile(type)?.CanExplode(i, j) == false)
            return false;
        foreach (GlobalTile g in TileLoader.Hooks.CanExplode)
            if (!g.CanExplode(i, j, type))
                return false;
        return true;
    }

    static bool CanKill(On_WorldGen.orig_CanKillTile_int_int_outbool orig, int i, int j, out bool damaged)
    {
        damaged = false;
        int type = Main.tile[i, j].type;
        if (TileLoader.GetTile(type)?.CanKillTile(i, j, ref damaged) == false)
            return false;
        foreach (GlobalTile g in TileLoader.Hooks.CanKillTile)
            if (!g.CanKillTile(i, j, type, ref damaged))
                return false;
        return orig(i, j, out damaged);
    }

    static void Kill(On_WorldGen.orig_KillTile orig, int i, int j, bool fail, bool effects, bool noItem)
    {
        Tile tile = Main.tile[i, j];
        int type = tile.type;
        if (!tile.active())
        {
            orig(i, j, fail, effects, noItem);
            return;
        }

        ModTile? mod = TileLoader.GetTile(type);
        mod?.KillTile(i, j, ref fail, ref effects, ref noItem);
        foreach (GlobalTile g in TileLoader.Hooks.KillTile)
            g.KillTile(i, j, type, ref fail, ref effects, ref noItem);
        int previous = dustBudget, count = fail ? 3 : 10;
        mod?.NumDust(i, j, fail, ref count);
        foreach (GlobalTile g in TileLoader.Hooks.NumDust)
            g.NumDust(i, j, type, fail, ref count);
        dustBudget = Math.Max(0, count);
        killing++;
        try
        {
            orig(i, j, fail, effects, noItem);
        }
        finally
        {
            dustBudget = previous;
            killing--;
        }
    }

    static bool AllowDrop(int i, int j, int type)
    {
        if (TileLoader.GetTile(type)?.CanDrop(i, j) == false)
            return false;
        foreach (GlobalTile g in TileLoader.Hooks.CanDrop)
            if (!g.CanDrop(i, j, type))
                return false;
        return true;
    }

    static void Drop(On_WorldGen.orig_KillTile_DropItems orig, int i, int j, Tile tile, bool large)
    {
        int type = tile.type;
        if (!AllowDrop(i, j, type))
            return;
        if (TileLoader.GetTile(type) is ModTile mod)
        {
            if (!large && TileLoader.Multi(type))
                return;
            foreach (Item item in mod.GetItemDrops(i, j))
                if (item.type > 0 && item.stack > 0)
                    Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j), new Vector2(i * 16 + 8, j * 16 + 8), item.type, item.stack);
        }
        else
            orig(i, j, tile, large);
        foreach (GlobalTile g in TileLoader.Hooks.Drop)
            g.Drop(i, j, type);
    }

    static void Drops(On_WorldGen.orig_KillTile_GetItemDrops orig, int i, int j, Tile tile, out int item, out int stack, out int second, out int secondStack, out bool noPrefix, bool large)
    {
        orig(i, j, tile, out item, out stack, out second, out secondStack, out noPrefix, large);
        int type = tile.type;
        if (!AllowDrop(i, j, type))
        {
            item = second = 0;
            return;
        }

        if (TileLoader.GetTile(type) is not ModTile mod)
            return;
        item = second = 0;
        stack = secondStack = 1;
        if (!large && TileLoader.Multi(type))
            return;
        using var list = mod.GetItemDrops(i, j).GetEnumerator();
        if (list.MoveNext())
        {
            item = list.Current.type;
            stack = list.Current.stack;
        }

        if (list.MoveNext())
        {
            second = list.Current.type;
            secondStack = list.Current.stack;
        }
    }

    static int MakeDust(On_WorldGen.orig_KillTile_MakeTileDust orig, int i, int j, Tile tile)
    {
        if (killing > 0 && dustBudget <= 0)
            return 6000;
        if (killing > 0)
            dustBudget--;
        int type = tile.type;
        ModTile? mod = TileLoader.GetTile(type);
        if (mod == null)
        {
            int index = orig(i, j, tile);
            if ((uint)index >= 6000u)
                return index;
            Dust d = Main.dust[index];
            int dust = d.type;
            foreach (GlobalTile g in TileLoader.Hooks.CreateDust)
                if (!g.CreateDust(i, j, type, ref dust))
                {
                    d.active = false;
                    return 6000;
                }

            d.type = dust;
            return index;
        }

        int kind = mod.DustType;
        if (!mod.CreateDust(i, j, ref kind))
            return 6000;
        foreach (GlobalTile g in TileLoader.Hooks.CreateDust)
            if (!g.CreateDust(i, j, type, ref kind))
                return 6000;
        return Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, kind, 0, 0, 0, default, 1);
    }

    static void Sound(On_WorldGen.orig_KillTile_PlaySounds orig, int i, int j, bool fail, Tile tile)
    {
        int type = tile.type;
        ModTile? mod = TileLoader.GetTile(type);
        if (mod?.KillSound(i, j, fail) == false)
            return;
        foreach (GlobalTile g in TileLoader.Hooks.KillSound)
            if (!g.KillSound(i, j, type, fail))
                return;
        if (mod == null)
        {
            orig(i, j, fail, tile);
            return;
        }

        if (mod.HitSound is not SoundStyle sound)
            return;
        SoundEngine.PlaySound(sound.Legacy, new Vector2(i * 16, j * 16), 0, 1);
    }

    static int Pick(On_Player.orig_GetPickaxeDamage orig, Player player, int i, int j, int power, int buffer, Tile tile)
    {
        int damage = orig(player, i, j, power, buffer, tile);
        ModTile? mod = TileLoader.GetTile(tile.type);
        return mod == null ? damage : power < mod.MinPick ? 0 : mod.MineResist > 0 ? (int)(damage / mod.MineResist) : damage;
    }

    static bool CanPlace(On_Player.orig_PlaceThing_Tiles_BlockPlacementForAssortedThings orig, Player player, bool canPlace)
    {
        if (!orig(player, canPlace))
            return false;
        int type = player.HeldItem.createTile, i = Player.tileTargetX, j = Player.tileTargetY;
        if (type < 0)
            return true;
        if (TileLoader.GetTile(type)?.CanPlace(i, j) == false)
            return false;
        foreach (GlobalTile g in TileLoader.Hooks.CanPlace)
            if (!g.CanPlace(i, j, type))
                return false;
        return true;
    }

    static void Placing(On_Player.orig_PlaceThing_Tiles orig, Player player, bool action)
    {
        placing++;
        try
        {
            orig(player, action);
        }
        finally
        {
            placing--;
        }
    }

    static bool Place(On_WorldGen.orig_PlaceTile orig, int i, int j, int type, bool mute, bool force, int player, int style)
    {
        bool result = orig(i, j, type, mute, force, player, style);
        if (result && placing > 0 && TileObjectData.GetTileData(type, style, 0).IsNull)
            Placed(i, j, type);
        return result;
    }

    static bool PlaceObject(On_TileObjectData.orig_CallPostPlacementPlayerHook orig, int i, int j, int type, int style, int dir, int alternate, TileObject data)
    {
        bool result = orig(i, j, type, style, dir, alternate, data);
        Placed(i, j, type);
        return result;
    }

    static void Placed(int i, int j, int type)
    {
        Item item = Main.LocalPlayer.HeldItem;
        TileLoader.GetTile(type)?.PlaceInWorld(i, j, item);
        foreach (GlobalTile g in TileLoader.Hooks.PlaceInWorld)
            g.PlaceInWorld(i, j, type, item);
    }

    [ThreadStatic]
    static int wireDepth;

    [ThreadStatic]
    static int wireX;

    [ThreadStatic]
    static int wireY;

    internal readonly struct WireScope : IDisposable
    {
        internal readonly bool Allowed;

        readonly bool owner;

        readonly int x;

        readonly int y;

        readonly int type;

        readonly int previousDepth;

        readonly int previousX;

        readonly int previousY;

        internal WireScope(int i, int j)
        {
            this = default;
            if (wireDepth > 0 && wireX == i && wireY == j)
            {
                Allowed = true;
                return;
            }

            type = Main.tile[i, j].type;
            foreach (GlobalTile global in TileLoader.Hooks.PreHitWire)
            {
                if (!global.PreHitWire(i, j, type))
                    return;
            }

            Allowed = true;
            owner = true;
            x = i;
            y = j;
            previousDepth = wireDepth;
            previousX = wireX;
            previousY = wireY;
            wireDepth++;
            wireX = i;
            wireY = j;
        }

        public void Dispose()
        {
            if (!owner)
                return;
            try
            {
                TileLoader.GetTile(type)?.HitWire(x, y);
                foreach (GlobalTile global in TileLoader.Hooks.HitWire)
                    global.HitWire(x, y, type);
            }
            finally
            {
                wireDepth = previousDepth;
                wireX = previousX;
                wireY = previousY;
            }
        }
    }

    internal static WireScope BeginWire(int i, int j) => new(i, j);

    static void Wire(On_Wiring.orig_HitWireSingle orig, int i, int j)
    {
        using WireScope scope = BeginWire(i, j);
        if (!scope.Allowed)
            return;
        orig(i, j);
        int type = Main.tile[i, j].type;
        if (MusicLoader.IsModMusicBox(type) && TileLoader.GetTile(type) is ModTile box && !TileLoader.Has(box, 7))
            MusicBoxHooks.Toggle(i, j, true);
    }

    static bool AllowSlope(int i, int j)
    {
        int type = Main.tile[i, j].type;
        if (TileLoader.GetTile(type)?.Slope(i, j) == false)
            return false;
        foreach (GlobalTile g in TileLoader.Hooks.Slope)
            if (!g.Slope(i, j, type))
                return false;
        return true;
    }

    static bool Slope(On_WorldGen.orig_SlopeTile orig, int i, int j, int slope, bool effects, bool quiet)
    {
        return AllowSlope(i, j) && orig(i, j, slope, effects, quiet);
    }

    static bool Pound(On_WorldGen.orig_PoundTile orig, int i, int j) => AllowSlope(i, j) && orig(i, j);

    static bool Replace(On_WorldGen.orig_ReplaceTile orig, int i, int j, int target, int style)
    {
        int type = Main.tile[i, j].type;
        if (TileLoader.GetTile(type)?.CanReplace(i, j, target) == false)
            return false;
        foreach (GlobalTile g in TileLoader.Hooks.CanReplace)
            if (!g.CanReplace(i, j, type, target))
                return false;
        return orig(i, j, target, style);
    }

    static void Animate(On_Main.orig_AnimateTiles orig)
    {
        orig();
        TileLoader.Animate();
    }

    readonly struct Adjacency
    {
        internal readonly int Type;

        internal readonly int[] Extras;

        internal Adjacency(int type, int[] extras)
        {
            Type = type;
            Extras = extras;
        }
    }

    static void Adj(On_Player.orig_AdjTiles orig, Player player)
    {
        orig(player);
        foreach (Adjacency entry in adjacency)
        {
            if (!player.adjTile[entry.Type])
                continue;
            foreach (int extra in entry.Extras)
            {
                if ((uint)extra < (uint)player.adjTile.Length)
                    player.adjTile[extra] = true;
            }
        }
    }

    internal static void SetupAdjacency()
    {
        var entries = new List<Adjacency>();
        for (int type = 0; type < TileLoader.TileCount; type++)
        {
            var extras = new List<int>();
            if (TileLoader.GetTile(type) is ModTile tile)
                extras.AddRange(tile.AdjTiles);
            foreach (GlobalTile global in TileLoader.Hooks.AdjTiles)
                extras.AddRange(global.AdjTiles(type));
            if (extras.Count > 0)
                entries.Add(new Adjacency(type, extras.ToArray()));
        }

        adjacency = entries.ToArray();
        if (adjacency.Length > 0)
            Once("Adj", () => On_Player.AdjTiles += Adj);
    }

    static readonly ulong[] lastPress = new ulong[256];

    static readonly bool[] hadPress = new bool[256];

    static bool Fresh(Player player)
    {
        if (!player.tileInteractAttempted)
            return false;
        int id = player.whoAmI;
        if ((uint)id >= 256u)
            return false;
        ulong frame = Main.GameUpdateCount, last = lastPress[id];
        bool had = hadPress[id];
        lastPress[id] = frame;
        hadPress[id] = true;
        return (!had || last != frame) && (player.releaseUseTile || !had || frame - last > 1);
    }

    static void Click(On_Player.orig_TileInteractionsUse orig, Player player, int i, int j)
    {
        Tile cell = Main.tile[i, j];
        ModTile? mod = TileLoader.GetTile(cell.type);
        bool fresh = Fresh(player);
        if (mod != null && !fresh)
            return;
        int type = cell.type;
        bool chest = TileID.Sets.BasicChest[type], dresser = TileID.Sets.BasicDresser[type], fire = TileID.Sets.Campfires[type];
        bool right = mod != null && TileLoader.Has(mod, 14);
        if (right)
        {
            TileID.Sets.BasicChest[type] = false;
            TileID.Sets.BasicDresser[type] = false;
            TileID.Sets.Campfires[type] = false;
        }

        try
        {
            orig(player, i, j);
        }
        finally
        {
            if (right)
            {
                TileID.Sets.BasicChest[type] = chest;
                TileID.Sets.BasicDresser[type] = dresser;
                TileID.Sets.Campfires[type] = fire;
            }
        }

        if (!fresh)
            return;
        if (mod?.RightClick(i, j) == true)
            player.tileInteractionHappened = true;
        else if (mod != null && !right && MusicLoader.IsModMusicBox(type))
        {
            MusicBoxHooks.Toggle(i, j, false);
            player.tileInteractionHappened = true;
        }
        foreach (GlobalTile g in TileLoader.Hooks.RightClick)
            g.RightClick(i, j, type);
    }

    static void Mouse(On_Player.orig_TileInteractionsMouseOver orig, Player player, int i, int j)
    {
        orig(player, i, j);
        if (i != Player.tileTargetX || j != Player.tileTargetY)
            return;
        int type = Main.tile[i, j].type;
        ModTile? mod = TileLoader.GetTile(type);
        mod?.MouseOver(i, j);
        foreach (GlobalTile g in TileLoader.Hooks.MouseOver)
            g.MouseOver(i, j, type);
        if (mod != null && TileLoader.Has(mod, 14) && !player.cursorItemIconEnabled)
        {
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = TileLoader.DropType(type, TileLoader.StyleAt(i, j));
        }
    }

    static void Far(On_Player.orig_TileInteractionsCheckLongDistance orig, Player player, int i, int j)
    {
        orig(player, i, j);
        int type = Main.tile[i, j].type;
        TileLoader.GetTile(type)?.MouseOverFar(i, j);
        foreach (GlobalTile g in TileLoader.Hooks.MouseOverFar)
            g.MouseOverFar(i, j, type);
    }
}

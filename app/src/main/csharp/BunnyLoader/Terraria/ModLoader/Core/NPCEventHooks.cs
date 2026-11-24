using System;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

[Flags]
internal enum NPCMethod : ulong
{
    PreAI = 1, AI = 2, PostAI = 4, FindFrame = 8, ResetEffects = 16, CheckActive = 32,
    CheckDead = 64, HitEffect = 128, UpdateLifeRegen = 256, OnSpawn = 512,
    ApplyDifficultyAndPlayerScaling = 1024, SetDefaultsFromNetId = 2048,
    CanBeHitByItem = 4096, CanCollideWithPlayerMeleeAttack = 8192, ModifyIncomingHit = 16384,
    ModifyHitByItem = 32768, OnHitByItem = 65536, CanBeHitByProjectile = 131072, ModifyHitByProjectile = 262144,
    OnHitByProjectile = 524288, CanHitPlayer = 1048576, ModifyHitPlayer = 2097152, OnHitPlayer = 4194304,
    CanHitNPC = 8388608, CanBeHitByNPC = 16777216, ModifyHitNPC = 33554432, OnHitNPC = 67108864,
    GetAlpha = 1UL << 40, DrawEffects = 1UL << 41, PreDraw = 1UL << 42, PostDraw = 1UL << 43, DrawBehind = 1UL << 44,
    BossHeadSlot = 1UL << 45, BossHeadRotation = 1UL << 46, BossHeadSpriteEffects = 1UL << 47,
    SendExtraAI = 1UL << 48, ReceiveExtraAI = 1UL << 49, SetBestiary = 1UL << 60,
    PickEmote = 1UL << 61, EmoteBubblePosition = 1UL << 62,
    SpawnChance = 1UL << 37, SpawnNPC = 1UL << 38, EditSpawnRate = 1UL << 39,
    EditSpawnRange = 1UL << 50, EditSpawnPool = 1UL << 51,
    TownSpawn = 1UL << 52, TownRoom = 1UL << 53, TownNames = 1UL << 54, TownChat = 1UL << 55,
    TownButtons = 1UL << 56, TownShops = 1UL << 57, TownAttack = 1UL << 58, TownAttackDraw = 1UL << 59,
    PreKill = 1UL << 32, OnKill = 1UL << 33, BossLoot = 1UL << 34,
    ModifyNPCLoot = 1UL << 35, ModifyGlobalLoot = 1UL << 36,
}

internal static unsafe class NPCEventHooks
{
    internal const uint PerType = 1 | 2 | 4 | 8 | 16;
    const int EmptyType = 0;

    sealed class FrameSize
    {
        internal readonly IntPtr Texture;
        internal readonly int Frames, Height;
        internal readonly GameHandle Pin;

        internal FrameSize(IntPtr texture, int frames, int height)
        {
            Texture = texture;
            Frames = frames;
            Height = height;
            Pin = new GameHandle(texture);
        }
    }

    static readonly object Gate = new();
    static uint[] wanted = Array.Empty<uint>();
    static uint all;
    static FrameSize?[] frameSizes = Array.Empty<FrameSize?>();
    internal struct BorrowContext
    {
        public IntPtr Pointer;
        public ModNPC? Instance;
    }

    [ThreadStatic] internal static BorrowContext Borrowing;
    [ThreadStatic] static (IntPtr Pointer, NPC.HitInfo Hit)? hitting;

    internal static ModNPC? Borrowed(NPC npc) => Borrowing.Pointer == npc.Pointer ? Borrowing.Instance : null;

    internal static NPCMethod Install(Type type, Type baseType, int npcType)
    {
        NPCMethod flags = 0;
        foreach (NPCMethod method in Enum.GetValues<NPCMethod>())
            if (ItemHooks.Overrides(type, baseType, method.ToString())) flags |= method;
        uint events = Events(flags);
        if (npcType < 0)
        {
            if ((events & PerType) != 0) Want(EmptyType, events & PerType);
            events &= ~PerType;
        }
        if (events != 0) Want(npcType, events);
        if ((flags & NPCMethod.HitEffect) != 0)
            ItemHooks.Once("NPCHitEffect", () =>
            {
                NPCGates.Borrowable<On_NPC.hook_HitEffect>(0, NPCMethod.HitEffect);
                On_NPC.HitEffect += HitEffect;
            });
        if ((flags & NPCMethod.OnSpawn) != 0) ItemHooks.Once("NPCSpawn", () =>
            {
                NPCLoader.WantDefaults();
                On_NPC.NewNPC += NewNPC;
            });
        if ((flags & NPCMethod.SetDefaultsFromNetId) != 0) ItemHooks.Once("NPCVariantDefaults", () => On_NPC.SetDefaultsFromNetId += VariantDefaults);
        ItemHitHooks.InstallNPC(flags);
        ProjectileHitHooks.InstallNPC(flags);
        NPCContactHooks.Install(flags);
        NPCDrawHooks.Install(flags);
        NPCNetHooks.Install(flags);
        EmoteBubbleHooks.InstallNPC(flags);
        NPCLootHooks.Install(flags);
        NPCSpawnHooks.Install(type, baseType, npcType, ref flags);
        return flags;
    }

    internal static uint Events(NPCMethod flags)
    {
        uint events = 0;
        if ((flags & (NPCMethod.PreAI | NPCMethod.AI | NPCMethod.PostAI)) != 0) events |= 1;
        if ((flags & NPCMethod.FindFrame) != 0) events |= 2;
        if ((flags & NPCMethod.ResetEffects) != 0) events |= 4;
        if ((flags & NPCMethod.CheckActive) != 0) events |= 8;
        if ((flags & NPCMethod.UpdateLifeRegen) != 0) events |= 16;
        if ((flags & NPCMethod.ApplyDifficultyAndPlayerScaling) != 0) events |= 32;
        if ((flags & NPCMethod.CheckDead) != 0) events |= 128;
        return events;
    }

    static int FrameHeight(int type)
    {
        int frames = Math.Max(1, Main.npcFrameCount[type]);
        if (Main.dedServ) return 1;
        IntPtr texture = TextureOf(type);
        FrameSize?[] sizes = Volatile.Read(ref frameSizes);
        if (texture != IntPtr.Zero && (uint)type < (uint)sizes.Length && sizes[type] is { } known && known.Texture == texture && known.Frames == frames)
            return known.Height;
        Texture2D value = TextureAssets.Npc[type].Value;
        int height = value.Height / frames;
        if (type >= 0 && value.Pointer != IntPtr.Zero && value.Pointer == TextureOf(type)) Remember(type, new FrameSize(value.Pointer, frames, height));
        return height;
    }

    static IntPtr TextureOf(int type)
    {
        GameArray<GameObject> assets = TextureAssets.Npc_.Raw;
        if ((uint)type >= (uint)assets.Length) return IntPtr.Zero;
        IntPtr asset = assets[type].Pointer;
        return asset == IntPtr.Zero ? IntPtr.Zero : Host.Native()->TextureAssetValue(asset);
    }

    static void Remember(int type, FrameSize size)
    {
        FrameSize? old;
        lock (Gate)
        {
            FrameSize?[] sizes = frameSizes;
            if (type >= sizes.Length)
            {
                var grown = new FrameSize?[Math.Max(type + 1, Math.Max(NPCLoader.NPCCount, sizes.Length * 2))];
                Array.Copy(sizes, grown, sizes.Length);
                sizes = grown;
            }
            old = sizes[type];
            sizes[type] = size;
            Volatile.Write(ref frameSizes, sizes);
        }
        old?.Pin.Dispose();
    }

    internal static void Want(int type, uint events)
    {
        uint[] masks = Volatile.Read(ref wanted);
        uint have = type < 0 ? Volatile.Read(ref all) : (uint)type < (uint)masks.Length ? masks[type] | Volatile.Read(ref all) : Volatile.Read(ref all);
        if ((have & events) == events) return;
        lock (Gate)
        {
            if (Host.Native()->NPCEvents(type, events, &Event) == 0)
                throw new InvalidOperationException($"NPC events: cannot install events {events} for type {type}");
            if (type < 0) Volatile.Write(ref all, all | events);
            else
            {
                if (type >= wanted.Length)
                {
                    var grown = new uint[Math.Max(type + 1, wanted.Length * 2)];
                    Array.Copy(wanted, grown, wanted.Length);
                    Volatile.Write(ref wanted, grown);
                }
                wanted[type] |= events;
            }
        }
    }

    [UnmanagedCallersOnly]
    static int Event(int kind, IntPtr pointer, void* args)
    {
        NPC npc = new(pointer);
        try
        {
            switch (kind)
            {
                case 0: NPCLoader.NPCAI(npc); break;
                case 1: NPCLoader.FindFrame(npc, FrameHeight(npc.type)); break;
                case 2: NPCLoader.ResetEffects(npc); break;
                case 3: return NPCLoader.CheckActive(npc) ? 1 : 0;
                case 4: NPCLoader.UpdateLifeRegen(npc, ref *(int*)args); break;
                case 5:
                    NPCLoader.ApplyDifficultyAndPlayerScaling(npc, ((int*)args)[0], ((float*)args)[1], ((float*)args)[2]);
                    break;
                case 6: NPCDefaultsHooks.BeforeVanillaTail(npc); break;
                case 7: return NPCLoader.CheckDead(npc) ? 1 : 0;
            }
        }
        catch (Exception e) { ItemHooks.Report("NPC lifecycle", e); }
        return 1;
    }

    static void HitEffect(On_NPC.orig_HitEffect orig, NPC npc, int hitDirection, double dmg)
    {
        NPC.HitInfo hit = hitting is { } context && context.Pointer == npc.Pointer ? context.Hit : LegacyHit(hitDirection, dmg, null);
        orig(npc, hitDirection, dmg);
        NPCLoader.HitEffect(npc, in hit);
    }

    internal static NPC.HitInfo LegacyHit(int direction, double damage, bool? instantKill)
    {
        bool instant = instantKill ?? damage == 9999;
        int value = instant ? 1 : (int)damage;
        return new NPC.HitInfo { DamageType = DamageClass.Default, SourceDamage = value, Damage = value, HitDirection = direction, InstantKill = instant };
    }

    internal readonly struct HitContext : IDisposable
    {
        readonly (IntPtr Pointer, NPC.HitInfo Hit)? previous;

        internal HitContext(NPC npc, NPC.HitInfo hit)
        {
            previous = hitting;
            hitting = (npc.Pointer, hit);
        }

        public void Dispose() => hitting = previous;
    }

    internal static void Hit(NPC npc, NPC.HitInfo hit)
    {
        using var context = new HitContext(npc, hit);
        npc.HitEffect_(hit.HitDirection, hit.InstantKill ? 9999 : hit.Damage);
    }

    static int NewNPC(On_NPC.orig_NewNPC orig, IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)
    {
        ForgetSpawn();
        int index = orig(source, X, Y, Type, Start, ai0, ai1, ai2, ai3, Target);
        if ((uint)index >= 200u || !Main.npc[index].active) return index;
        NPC npc = Main.npc[index];
        NPCLoader.OnSpawn(npc, source);
        ForgetSpawn();
        if (npc.active && !source.IsNull)
        {
            spawned = npc.Pointer;
            spawnedTick = Main.GameUpdateCount;
            spawnedSource = new GameHandle(source.Pointer);
        }
        return index;
    }

    static IntPtr spawned;
    static uint spawnedTick;
    static GameHandle? spawnedSource;

    static void ForgetSpawn()
    {
        spawned = IntPtr.Zero;
        spawnedSource?.Dispose();
        spawnedSource = null;
    }

    internal static void AfterDefaults(NPC npc)
    {
        if (spawned == IntPtr.Zero || npc.Pointer != spawned) return;
        GameHandle? handle = spawnedSource;
        spawnedSource = null;
        bool same = Main.GameUpdateCount == spawnedTick && npc.active && npc.type > 0;
        spawned = IntPtr.Zero;
        try
        {
            IntPtr target = handle?.Target ?? IntPtr.Zero;
            if (same && target != IntPtr.Zero) NPCLoader.OnSpawn(npc, new IEntitySource(target));
        }
        finally { handle?.Dispose(); }
    }

    static void VariantDefaults(On_NPC.orig_SetDefaultsFromNetId orig, NPC npc, int id, NPCSpawnParams spawnparams)
    {
        orig(npc, id, spawnparams);
        foreach (GlobalNPC? global in NPCLoader.Slots(npc))
        {
            if (global == null || (global.Hooks & NPCMethod.SetDefaultsFromNetId) == 0) continue;
            try { global.SetDefaultsFromNetId(npc); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.SetDefaultsFromNetId", e); }
        }
    }
}

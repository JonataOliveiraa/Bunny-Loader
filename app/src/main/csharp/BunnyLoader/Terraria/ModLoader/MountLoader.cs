using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class MountLoader
{
    internal static readonly Dictionary<int, ModMount> mountDatas = new();
    static int vanillaCount = -1;

    public static int MountCount => MountID.Count;

    internal static int Register(ModMount mount)
    {
        lock (mountDatas)
        {
            if (vanillaCount < 0) vanillaCount = MountID.Count;
            int type = MountID.Count;
            MountID.Count = type + 1;
            mountDatas[type] = mount;
            MountHooks.Install(mount.GetType());
            return type;
        }
    }

    public static ModMount GetMount(int type) => mountDatas.TryGetValue(type, out ModMount? mount) ? mount : null!;

    public static bool IsModMount(Mount.MountData m) => !m.IsNull && Find(m) != null;

    internal static ModMount? Find(Mount.MountData data)
    {
        foreach (ModMount mount in mountDatas.Values)
        {
            if (mount.MountData == data) return mount;
        }
        return null;
    }

    internal static void ResizeArrays()
    {
        int total = MountID.Count;
        if (Mount.mounts.Length < total) Mount.mounts = Mount.mounts.Resized(total);
        Grow(ref MountID.Sets.CanUseHooks, total);
        Grow(ref MountID.Sets.CanDash, total);
        Grow(ref MountID.Sets.DoesNotOverrideBodyFrames, total);
        Grow(ref MountID.Sets.DoesNotOverrideLegFrames, total);
        Grow(ref MountID.Sets.DoesNotOverrideBackpackDraw, total);
        Grow(ref MountID.Sets.DoesNotOverrideWings, total);
        Grow(ref MountID.Sets.IsRollerSkates, total);
        Grow(ref MountID.Sets.Cart, total);
        Grow(ref MountID.Sets.IsTransformationMount, total);
        Grow(ref MountID.Sets.PlayerIsHidden, total);
        Grow(ref MountID.Sets.DontHoldItems, total);
        Grow(ref MountID.Sets.DontDismountWhenCCed, total);
    }

    static void Grow(ref GameArray<bool> set, int total)
    {
        if (!set.IsNull && set.Length < total) set = set.Resized(total);
    }
}

using System;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemWorldHooks
{
    static bool globalUpdate, globalExtract, globalMove, wantPostUpdate;
    static Hook? updateItem;

    public static void Install(Func<string, bool> has, bool global)
    {
        if (has(nameof(ModItem.ExtractinatorUse)))
        {
            if (global) globalExtract = true;
            ItemHooks.Once("ItemExtractinator", () => On_Player.ExtractinatorUse += ExtractinatorUse);
        }
        ItemSpawnHooks.Install(has);
        if (has(nameof(ModItem.Update)))
        {
            if (global) globalMove = true;
            ItemHooks.Once("ItemUpdate", () => On_WorldItem.MoveInWorld += MoveInWorld);
        }
        if (!has(nameof(ModItem.PostUpdate))) return;
        if (global) globalUpdate = true;
        wantPostUpdate = true;
    }

    internal static void ContentReady()
    {
        if (!wantPostUpdate) return;
        ItemHooks.Once("ItemPostUpdate", () => updateItem = ItemHooks.Gated(WorldItem.Class, "void UpdateItem(int i)",
            (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, void>)&UpdateItem, 0,
            ItemHooks.FieldOffset(WorldItem.Class, "inner"), ItemHooks.FieldOffset(Item.Class, "type"), 4));
    }

    static void ExtractinatorUse(On_Player.orig_ExtractinatorUse orig, Player self, int extractType, int extractinatorBlockType)
    {
        ExtractinatorHelper.RollExtractinatorDrop(extractType, extractinatorBlockType, out int resultType, out int resultStack);
        ID.ContentSamples.ItemsByType.TryGetValue(extractType, out Item sample);
        if (ItemLoader.GetItem(extractType) is ModItem m)
        {
            try { m.ExtractinatorUse(extractinatorBlockType, ref resultType, ref resultStack); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ExtractinatorUse", e); }
        }
        if (globalExtract && !sample.IsNull)
        {
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(sample))
            {
                if (g == null) continue;
                try { g.ExtractinatorUse(extractType, extractinatorBlockType, ref resultType, ref resultStack); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.ExtractinatorUse", e); }
            }
        }
        if (resultType > 0) self.DropItemFromExtractinator(resultType, resultStack);
    }

    static void MoveInWorld(On_WorldItem.orig_MoveInWorld orig, WorldItem self, float gravity, float maxFallSpeed, ref Microsoft.Xna.Framework.Vector2 wetVelocity)
    {
        Item item = self.inner;
        if (!item.IsNull)
        {
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { m.Update(self, ref gravity, ref maxFallSpeed); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.Update", e); }
            }
            if (globalMove)
            {
                foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
                {
                    if (g == null) continue;
                    try { g.Update(self, ref gravity, ref maxFallSpeed); }
                    catch (Exception e) { ItemHooks.Report($"{g.FullName}.Update", e); }
                }
            }
        }
        orig(self, gravity, maxFallSpeed, ref wetVelocity);
    }

    [UnmanagedCallersOnly]
    static void UpdateItem(IntPtr pointer, int i, IntPtr method)
    {
        Hook? hook = updateItem;
        if (hook == null) return;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(pointer);
        call.Arg(i);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "WorldItem.UpdateItem");
        try { PostUpdate(new WorldItem(pointer)); }
        catch (Exception e) { ItemHooks.Report("ModItem.PostUpdate", e); }
    }

    static void PostUpdate(WorldItem self)
    {
        Item item = self.inner;
        if (item.IsNull || item.type == 0) return;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.PostUpdate(self); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PostUpdate", e); }
        }
        if (!globalUpdate) return;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.PostUpdate(self); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PostUpdate", e); }
        }
    }
}

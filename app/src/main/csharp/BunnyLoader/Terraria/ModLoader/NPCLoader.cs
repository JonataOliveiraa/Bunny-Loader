using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.GameContent;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe partial class NPCLoader
{
    const int VanillaCount = 697;

    static readonly object Gate = new();
    static readonly EntityInstances<ModNPC> Instances = new();
    static readonly List<GameHandle> TemplatePins = new();
    static ModNPC?[] npcs = Array.Empty<ModNPC?>();
    static bool hooked, cloneHooked;

    internal static readonly GlobalList<NPC, GlobalNPC> Globals = new();

    public static int NPCCount => Host.Native()->NPCCount();

    public static ModNPC? GetNPC(int type)
    {
        ModNPC?[] all = Volatile.Read(ref npcs);
        uint index = (uint)(type - VanillaCount);
        return index < (uint)all.Length ? all[index] : null;
    }

    internal static ModNPC? InstanceOf(NPC npc)
    {
        if (npc.IsNull) return null;
        if (NPCEventHooks.Borrowed(npc) is ModNPC borrowed) return borrowed;
        ModNPC? template = GetNPC(npc.type);
        if (template == null) return null;
        ModNPC? current = Instances.Get(npc.Pointer);
        return current != null && current.RegisteredType == template.RegisteredType ? current : Bind(template, npc);
    }

    static ModNPC Bind(ModNPC template, NPC npc)
    {
        ModNPC instance = template.Entity == npc ? template : template.NewInstance(npc);
        instance.Type = template.Type;
        instance.Hooks = template.Hooks;
        Instances.Set(npc.Pointer, instance);
        return instance;
    }

    internal static void Register(ModNPC npc)
    {
        string texture = ItemLoader.ResolveTexture(npc, npc.Texture);
        string displayName = DisplayNames(npc);
        int type;
        byte* mod = Host.Utf8(npc.Mod.Uid), name = Host.Utf8(npc.Name), tex = Host.Utf8(texture), dn = Host.Utf8(displayName);
        try { type = Host.Native()->RegisterNPC(mod, name, tex, dn); }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
            Host.FreeUtf8(tex);
            Host.FreeUtf8(dn);
        }
        if (type == -1) throw new InvalidOperationException($"{npc.FullName}: the mod already has an NPC named '{npc.Name}'");
        if (type < 0) throw new InvalidOperationException($"{npc.FullName}: registration failed ({type})");
        npc.Type = type;
        npc.Hooks = NPCEventHooks.Install(npc.GetType(), typeof(ModNPC), type);
        ReserveBossHead(npc);
        lock (Gate)
        {
            int index = type - VanillaCount;
            ModNPC?[] all = npcs;
            if (index >= all.Length)
            {
                var grown = new ModNPC?[index + 1];
                Array.Copy(all, grown, all.Length);
                all = grown;
            }
            all[index] = npc;
            Volatile.Write(ref npcs, all);
            EnsureHooked();
        }
    }

    internal static void RegisterGlobal(GlobalNPC global)
    {
        Globals.Add(global);
        global.Hooks = NPCEventHooks.Install(global.GetType(), typeof(GlobalNPC), -1);
        NPCEventHooks.Want(-1, 64);
        ItemHooks.Once("NPCDefaultsScaling", () => On_NPC.ScaleStats += NPCDefaultsHooks.BeforeScaling);
        EnsureHooked();
    }

    internal static void WantDefaults() => EnsureHooked(false);

    static void EnsureHooked(bool clones = true)
    {
        lock (Gate)
        {
            if (!hooked)
            {
                On_NPC.SetDefaults += NPCDefaultsHooks.SetDefaults;
                Host.Native()->OnNPCsInstalled(&Installed);
                hooked = true;
            }
            if (clones && !cloneHooked)
            {
                On_NPC.Clone += Clone;
                cloneHooked = true;
            }
        }
    }

    static string DisplayNames(ModNPC npc)
    {
        if (npc.GetType().GetProperty(nameof(ModNPC.DisplayName))!.DeclaringType == typeof(ModNPC))
        {
            var texts = LocalizationLoader.Texts(npc.Mod, npc.GetLocalizationKey(nameof(ModNPC.DisplayName)));
            if (texts.Count > 0) return LocalizationLoader.Pack(texts);
        }
        return npc.DisplayName.IsNull ? "" : npc.DisplayName.Value ?? "";
    }

    internal static void SetDefaults(NPC npc, ModNPC? template)
    {
        ModNPC? instance = null;
        if (template != null) instance = Bind(template, npc);
        else if (Instances.Count > 0) Instances.Set(npc.Pointer, null);
        if (npc.type <= 0)
        {
            Globals.Clear(npc);
            return;
        }
        NPCImmunity.Apply(npc);
        GlobalNPC?[]? slots = Globals.Begin(npc);
        bool[] early = slots == null ? Array.Empty<bool>() : new bool[slots.Length];
        for (int i = 0; i < early.Length; i++)
        {
            early[i] = slots![i] != null;
            if (slots[i] is GlobalNPC global) global.Hooks = Globals.Templates[i].Hooks;
        }
        if (instance != null)
        {
            try { instance.SetDefaults(); }
            catch (Exception e) { ItemHooks.Report($"{instance.FullName}.SetDefaults", e); }
        }
        for (int i = 0; i < early.Length; i++)
        {
            if (early[i]) SetGlobalDefaults(npc, slots![i]!);
        }
        Globals.Finish(npc, slots);
        for (int i = 0; i < early.Length; i++)
        {
            if (!early[i] && slots![i] is GlobalNPC global)
            {
                global.Hooks = Globals.Templates[i].Hooks;
                SetGlobalDefaults(npc, global);
            }
        }
        if (instance?.AIType is > 0) NPCEventHooks.Want(npc.type, 1);
        if (instance?.AnimationType is > 0) NPCEventHooks.Want(npc.type, 2);
    }

    static void SetGlobalDefaults(NPC npc, GlobalNPC global)
    {
        try { global.SetDefaults(npc); }
        catch (Exception e) { ItemHooks.Report($"{global.FullName}.SetDefaults", e); }
    }

    internal static void Restore(NPC npc, ModNPC? instance, GlobalNPC?[]? globals)
    {
        Instances.Set(npc.Pointer, instance);
        Globals.Restore(npc, globals);
    }

    static GameObject Clone(On_NPC.orig_Clone orig, NPC self)
    {
        GameObject result = orig(self);
        var copy = new NPC(result.Pointer);
        if (copy.IsNull || copy == self) return result;
        ModNPC? source = Instances.Get(self.Pointer);
        if (source != null && source.RegisteredType == copy.type)
        {
            try
            {
                ModNPC cloned = source.Clone(copy);
                cloned.Type = source.Type;
                Instances.Set(copy.Pointer, cloned);
            }
            catch (Exception e) { ItemHooks.Report($"{source.FullName}.Clone", e); }
        }
        Globals.Clone(self, copy);
        return result;
    }

    [UnmanagedCallersOnly]
    static void Installed(int first, int last)
    {
        NPCImmunity.Resize();
        for (int type = first; type <= last; type++)
        {
            ModNPC? npc = GetNPC(type);
            if (npc == null) continue;
            try
            {
                if (npc.Entity.IsNull)
                {
                    NPC template = NPC.New();
                    TemplatePins.Add(new GameHandle(template.Pointer));
                    npc.Entity = template;
                    template.SetDefaults(type, default);
                }
                npc.SetupContent();
                int frames = Math.Max(1, Main.npcFrameCount[type]);
                Host.Native()->SetNPCFrames(type, frames);
                var texture = TextureAssets.Npc[type].Value;
                npc.NPC.frame = new Rectangle(0, 0, texture.Width, texture.Height / frames);
            }
            catch (Exception e) { Log.Error($"NPC {npc.FullName}: SetStaticDefaults threw: {e}"); }
        }
    }
}

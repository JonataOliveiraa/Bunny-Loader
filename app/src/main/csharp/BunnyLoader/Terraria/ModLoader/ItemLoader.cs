using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Terraria.GameContent.Items;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

internal static unsafe class ItemLoader
{
    static readonly object Gate = new();

    static ModItem?[] items = Array.Empty<ModItem?>();

    static bool hooked;

    static readonly EntityInstances<ModItem> Instances = new();

    internal static readonly GlobalList<Item, GlobalItem> Globals = new();

    public static void RegisterGlobal(GlobalItem global)
    {
        Globals.Add(global);
        EnsureHooked();
        ItemHooks.Install(global.GetType(), typeof(GlobalItem));
    }

    static void EnsureHooked()
    {
        lock (Gate)
        {
            if (hooked) return;
            On_Item.SetDefaults += SetDefaults;
            On_Item.Clone += Clone;
            On_Item.DeepClone += DeepClone;
            On_Item.clientClone += ClientClone;
            Host.Native()->OnItemsInstalled(&ItemsInstalled);
            Host.Native()->OnItemData(&SaveItemData, &LoadItemData);
            hooked = true;
        }
    }

    public static ModItem? InstanceOf(Item item)
    {
        if (item.IsNull) return null;
        int type = item.type;
        ModItem? template = Get(type);
        if (template == null) return null;
        ModItem? current = Instances.Get(item.Pointer);
        return current != null && current.Type == type ? current : Bind(template, item);
    }

    static ModItem Bind(ModItem template, Item item)
    {
        ModItem instance = template.NewInstance(item);
        instance.Type = template.Type;
        Instances.Set(item.Pointer, instance);
        return instance;
    }

    internal static void OnCreated(Item item, ItemCreationContext context)
    {
        if (item.IsNull) return;
        if (ItemHooks.ModItemOf(item) is ModItem modItem)
        {
            try { modItem.OnCreated(context); }
            catch (Exception e) { ItemHooks.Report($"{modItem.FullName}.OnCreated", e); }
        }
        foreach (GlobalItem? global in ItemHooks.GlobalsOf(item))
        {
            if (global == null) continue;
            try { global.OnCreated(item, context); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.OnCreated", e); }
        }
    }

    public static ModItem? Get(int type)
    {
        ModItem?[] all = Volatile.Read(ref items);
        uint index = (uint)(type - ItemID.Count);
        return index < (uint)all.Length ? all[index] : null;
    }

    public static void Register(ModItem item)
    {
        string texture = ResolveTexture(item);
        int type = Native(item, texture, DisplayNames(item), Tooltips(item));
        if (type == -1) throw new InvalidOperationException($"{item.FullName}: the mod already has an item named '{item.Name}'");
        if (type < 0) throw new InvalidOperationException($"{item.FullName}: registration failed ({type})");
        item.Type = type;

        EnsureHooked();
        EquipLoader.Autoload(item);
        ItemHooks.Install(item.GetType(), typeof(ModItem));
        PrefixLoader.WantRollable();
        lock (Gate)
        {
            int index = type - ItemID.Count;
            ModItem?[] all = items;
            if (index >= all.Length)
            {
                var grown = new ModItem?[Math.Max(index + 1, all.Length * 2)];
                Array.Copy(all, grown, all.Length);
                all = grown;
            }
            all[index] = item;
            Volatile.Write(ref items, all);
        }
    }

    static string DisplayNames(ModItem item) => Localized(item, item.DisplayName, nameof(ModItem.DisplayName));

    static string Tooltips(ModItem item) => Localized(item, item.Tooltip, nameof(ModItem.Tooltip));

    static string Localized(ModItem item, LocalizedText text, string property)
    {
        if (item.GetType().GetProperty(property)!.DeclaringType == typeof(ModItem))
        {
            var texts = LocalizationLoader.Texts(item.Mod, item.GetLocalizationKey(property));
            if (texts.Count > 0) return LocalizationLoader.Pack(texts);
        }
        return text.IsNull ? "" : text.Value ?? "";
    }

    static int Native(ModItem item, string texture, string displayName, string tooltip)
    {
        byte* mod = Host.Utf8(item.Mod.Uid), name = Host.Utf8(item.Name), tex = Host.Utf8(texture);
        byte* dn = Host.Utf8(displayName), tip = Host.Utf8(tooltip);
        try { return Host.Native()->RegisterItem(mod, name, tex, dn, tip); }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
            Host.FreeUtf8(tex);
            Host.FreeUtf8(dn);
            Host.FreeUtf8(tip);
        }
    }

    static string ResolveTexture(ModItem item)
    {
        string texture = item.Texture.Replace('\\', '/');
        if (!texture.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) texture += ".png";
        if (Path.IsPathRooted(texture))
        {
            if (File.Exists(texture)) return texture;
        }
        else
        {
            string direct = Path.Combine(item.Mod.Folder, texture);
            if (File.Exists(direct)) return direct;
            int slash = texture.IndexOf('/');
            if (slash > 0)
            {
                string withoutMod = Path.Combine(item.Mod.Folder, texture.Substring(slash + 1));
                if (File.Exists(withoutMod)) return withoutMod;
            }
        }
        throw new FileNotFoundException($"{item.FullName}: texture '{item.Texture}' not found in {item.Mod.Folder}");
    }

    static void SetDefaults(On_Item.orig_SetDefaults orig, Item self, int type, ItemVariant variant)
    {
        ModItem? template = Get(type);
        if (template == null)
        {
            orig(self, type, variant);
            if (Instances.Count > 0) Instances.Set(self.Pointer, null);
            if (Globals.Count == 0) return;
            Globals.Finish(self, Globals.Begin(self));
            IntPtr previous = DamageClassLoader.Defaulting;
            DamageClassLoader.Defaulting = self.Pointer;
            try { GlobalSetDefaults(self); }
            finally { DamageClassLoader.Defaulting = previous; }
            return;
        }
        NativeApi* api = Host.Native();
        api->PrepareItem(self.Pointer, type);
        self.maxStack = 1;
        EquipLoader.Install();
        EquipLoader.Apply(self, type);
        ModItem instance = Bind(template, self);
        GlobalItem?[]? slots = Globals.Begin(self);
        IntPtr outer = DamageClassLoader.Defaulting;
        DamageClassLoader.Defaulting = self.Pointer;
        try
        {
            try
            {
                instance.SetDefaults();
            }
            catch (Exception e)
            {
                Log.Error($"item {template.FullName}: SetDefaults threw: {e}");
            }
            Globals.Finish(self, slots);
            api->FinishItem(self.Pointer, type);
            GlobalSetDefaults(self);
        }
        finally { DamageClassLoader.Defaulting = outer; }
    }

    static void GlobalSetDefaults(Item item)
    {
        GlobalItem?[]? slots = Globals.Slots(item);
        if (slots == null) return;
        foreach (GlobalItem? global in slots)
        {
            if (global == null) continue;
            try
            {
                global.SetDefaults(item);
            }
            catch (Exception e)
            {
                Log.Error($"{global.FullName}: SetDefaults threw: {e}");
            }
        }
    }

    static Item Clone(On_Item.orig_Clone orig, Item self) => CopyInstances(self, orig(self));

    static Item DeepClone(On_Item.orig_DeepClone orig, Item self) => CopyInstances(self, orig(self));

    static Item ClientClone(On_Item.orig_clientClone orig, Item self, Item cloneDestination) => CopyInstances(self, orig(self, cloneDestination));

    static Item CopyInstances(Item self, Item copy)
    {
        if (copy.IsNull || copy == self) return copy;
        ModItem? source = Instances.Get(self.Pointer);
        if (source != null && source.Type == copy.type)
        {
            try
            {
                ModItem cloned = source.Clone(copy);
                cloned.Type = source.Type;
                Instances.Set(copy.Pointer, cloned);
            }
            catch (Exception e)
            {
                Log.Error($"item {source.FullName}: Clone threw: {e}");
            }
        }
        Globals.Clone(self, copy);
        return copy;
    }

    [UnmanagedCallersOnly]
    static int SaveItemData(IntPtr itemPointer, byte* current, IntPtr sink)
    {
        ModItem? instance = null;
        try
        {
            instance = InstanceOf(new Item(itemPointer));
            if (instance == null) return 1;
            string text = SavedText(instance) ?? Marshal.PtrToStringUTF8((IntPtr)current) ?? "";
            byte* utf8 = Host.Utf8(text);
            try { Host.Native()->WriteString(sink, utf8); }
            finally { Host.FreeUtf8(utf8); }
            return 1;
        }
        catch (Exception e)
        {
            Log.Error($"item {instance?.FullName ?? "?"}: SaveData threw: {e}");
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    static int LoadItemData(IntPtr itemPointer, byte* data)
    {
        ModItem? instance = null;
        try
        {
            instance = InstanceOf(new Item(itemPointer));
            if (instance == null) return 1;
            LoadText(instance, Marshal.PtrToStringUTF8((IntPtr)data) ?? "");
            return 1;
        }
        catch (Exception e)
        {
            Log.Error($"item {instance?.FullName ?? "?"}: LoadData threw (its saved data is kept as it was): {e}");
            return 0;
        }
    }

    internal static string? SavedText(ModItem instance)
    {
        if (instance.LoadFailed || !Overrides(instance, nameof(ModItem.SaveData))) return instance.SavedData;
        var tag = new TagCompound();
        instance.SaveData(tag);
        string text = tag.Count == 0 ? "" : TagJson.Write(tag);
        instance.SavedData = text;
        return text;
    }

    internal static void LoadText(ModItem instance, string text)
    {
        instance.SavedData = text;
        instance.LoadFailed = true;
        TagCompound tag = text.Length == 0 ? new TagCompound() : TagJson.Read(text);
        if (Overrides(instance, nameof(ModItem.LoadData))) instance.LoadData(tag);
        instance.LoadFailed = false;
    }

    internal static bool Overrides(ModItem item, string method) =>
        item.GetType().GetMethod(method, new[] { typeof(TagCompound) })!.DeclaringType != typeof(ModItem);

    static readonly System.Collections.Generic.List<GameHandle> TemplatePins = new();

    [UnmanagedCallersOnly]
    static void ItemsInstalled(int first, int last)
    {
        EquipLoader.Install();
        for (int type = first; type <= last; type++)
        {
            ModItem? item = Get(type);
            if (item == null) continue;
            try
            {
                if (item.Entity.IsNull)
                {
                    Item template = Item.New();
                    TemplatePins.Add(new GameHandle(template.Pointer));
                    template.SetDefaults(type, default);
                    item.Entity = template;
                }
                item.SetupContent();
            }
            catch (Exception e)
            {
                Log.Error($"item {item.FullName}: SetStaticDefaults threw: {e}");
            }
        }
    }
}

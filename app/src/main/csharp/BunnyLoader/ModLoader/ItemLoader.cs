using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent.Items;
using Terraria.ID;
using Terraria.Localization;

namespace Terraria.ModLoader;

internal static unsafe class ItemLoader
{
    static readonly object Gate = new();

    static ModItem?[] items = Array.Empty<ModItem?>();

    static bool hooked;

    static readonly EntityInstances<ModItem> Instances = new();

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

        lock (Gate)
        {
            if (!hooked)
            {
                On_Item.SetDefaults += SetDefaults;
                On_Item.Clone += Clone;
                Host.Native()->OnItemsInstalled(&ItemsInstalled);
                hooked = true;
            }
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
            return;
        }
        NativeApi* api = Host.Native();
        api->PrepareItem(self.Pointer, type);
        try
        {
            Bind(template, self).SetDefaults();
        }
        catch (Exception e)
        {
            Log.Error($"item {template.FullName}: SetDefaults threw: {e}");
        }
        api->FinishItem(self.Pointer, type);
    }

    static Item Clone(On_Item.orig_Clone orig, Item self)
    {
        Item copy = orig(self);
        ModItem? source = copy.IsNull ? null : Instances.Get(self.Pointer);
        if (source == null || source.Type != copy.type) return copy;
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
        return copy;
    }

    [UnmanagedCallersOnly]
    static void ItemsInstalled(int first, int last)
    {
        for (int type = first; type <= last; type++)
        {
            ModItem? item = Get(type);
            if (item == null) continue;
            try
            {
                item.SetupContent();
            }
            catch (Exception e)
            {
                Log.Error($"item {item.FullName}: SetStaticDefaults threw: {e}");
            }
        }
    }
}

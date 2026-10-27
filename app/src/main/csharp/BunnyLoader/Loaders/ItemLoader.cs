using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader.Interop;
using Terraria;
using Terraria.GameContent.Items;
using Terraria.ID;

namespace BunnyLoader.Loaders;

// Os ModItem do C#. O nativo (content/items/ModItems.cpp) da o tipo, aumenta
// as tabelas do jogo, carrega a textura e salva o item pelo nome; daqui saem
// o SetDefaults e o SetStaticDefaults de cada um.
internal static unsafe class ItemLoader
{
    static readonly object Gate = new();

    // Indice = tipo - ItemID.Count. Os tipos de mod sao divididos com o resto
    // do loader (a reserva "?"), entao ha buracos. So cresce, na carga; o
    // jogo le sem trava.
    static ModItem?[] items = Array.Empty<ModItem?>();

    static bool hooked;

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
        ModContent.Add(item);
    }

    // O DisplayName do item vale para todo idioma; sem ele, o ItemName das
    // traducoes do mod; sem nenhum, o nome da classe separado.
    static string DisplayNames(ModItem item)
    {
        if (item.DisplayName != null) return item.DisplayName;
        var texts = LocalizationLoader.Texts(item.Mod, "ItemName", item.Name);
        return texts.Count > 0 ? LocalizationLoader.Pack(texts) : item.PrettyPrintName();
    }

    static string Tooltips(ModItem item) =>
        item.Tooltip ?? LocalizationLoader.Pack(LocalizationLoader.Texts(item.Mod, "ItemTooltip", item.Name));

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

    // O PNG do Texture: caminho absoluto, ou relativo a pasta da entrada,
    // com ou sem o primeiro trecho (o nome do mod, no jeito do tModLoader).
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

    // Para tipo de mod o original NAO roda: o SetDefaults do jogo zera o tipo
    // de quem passa de ItemID.Count. O nativo faz o que ele faria (ResetStats,
    // tipo) e o ModItem faz o resto. Nenhum erro sai daqui: o hook gerado
    // chamaria o original como protecao, e o item viraria "nada".
    static void SetDefaults(On_Item.orig_SetDefaults orig, Item self, int type, ItemVariant variant)
    {
        ModItem? template = Get(type);
        if (template == null)
        {
            orig(self, type, variant);
            return;
        }
        NativeApi* api = Host.Native();
        api->PrepareItem(self.Pointer, type);
        try
        {
            template.NewInstance(self).SetDefaults();
        }
        catch (Exception e)
        {
            Log.Error($"item {template.FullName}: SetDefaults threw: {e}");
        }
        api->FinishItem(self.Pointer, type);
    }

    // Um lote de tipos acabou de entrar nas tabelas do jogo (thread do jogo).
    [UnmanagedCallersOnly]
    static void ItemsInstalled(int first, int last)
    {
        for (int type = first; type <= last; type++)
        {
            ModItem? item = Get(type);
            if (item == null) continue;
            try
            {
                item.SetStaticDefaults();
            }
            catch (Exception e)
            {
                Log.Error($"item {item.FullName}: SetStaticDefaults threw: {e}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Initializers;

namespace Terraria.ModLoader;

public static unsafe class EquipLoader
{
    public static readonly EquipType[] EquipTypes = (EquipType[])Enum.GetValues(typeof(EquipType));

    const int MaxSbyteSlots = 128;

    static readonly object Gate = new();

    static readonly List<EquipTexture> Pending = new();

    static readonly Dictionary<(EquipType, int), EquipTexture> Textures = new();

    static readonly Dictionary<(EquipType, string), EquipTexture> ByName = new();

    static readonly Dictionary<int, Dictionary<EquipType, int>> SlotsOfItem = new();

    static readonly Dictionary<EquipType, int> Vanilla = new();

    static readonly List<EquipType> InstalledTypes = new();

    static bool installed;

    public static int AddEquipTexture(Mod mod, string texture, EquipType type, ModItem? item = null, string? name = null, EquipTexture? equipTexture = null)
    {
        if (!mod.loading) throw new Exception("AddEquipTexture can only be called during loading");
        if (item == null && name == null) throw new ArgumentException("Either item or name must be set");
        string file = FileOf(mod, texture) ?? throw new FileNotFoundException($"{mod.Name}: equip texture '{texture}' not found");
        Add(mod, file, type, item, name ?? item!.Name, equipTexture);
        return -1;
    }

    public static EquipTexture? GetEquipTexture(EquipType type, int slot)
    {
        lock (Gate) return Textures.TryGetValue((type, slot), out EquipTexture? texture) ? texture : null;
    }

    public static EquipTexture? GetEquipTexture(Mod mod, string name, EquipType type)
    {
        lock (Gate) return ByName.TryGetValue((type, $"{mod.Name}/{name}"), out EquipTexture? texture) ? texture : null;
    }

    public static int GetEquipSlot(Mod mod, string name, EquipType type) => GetEquipTexture(mod, name, type)?.Slot ?? -1;

    internal static int VanillaCount(EquipType type)
    {
        lock (Gate) return Vanilla.TryGetValue(type, out int count) ? count : 0;
    }

    internal static EquipType[] Installed()
    {
        lock (Gate) return InstalledTypes.ToArray();
    }

    internal static void Autoload(ModItem item)
    {
        if (item.GetType().GetCustomAttribute<AutoloadEquip>(true) is not AutoloadEquip autoload) return;
        foreach (EquipType type in autoload.equipTypes)
        {
            string texture = $"{item.Texture}_{type}";
            string file = FileOf(item.Mod, texture) ?? throw new FileNotFoundException($"{item.FullName}: [AutoloadEquip] needs the texture '{texture}'");
            Add(item.Mod, file, type, item, item.Name, null);
        }
    }

    static void Add(Mod mod, string file, EquipType type, ModItem? item, string name, EquipTexture? equipTexture)
    {
        EquipTexture texture = equipTexture ?? new EquipTexture();
        texture.Texture = file;
        texture.Name = $"{mod.Name}/{name}";
        texture.Type = type;
        texture.Item = item;
        lock (Gate)
        {
            if (installed) throw new InvalidOperationException($"{mod.Name}: equip texture '{name}' added after the equip slots were installed");
            Pending.Add(texture);
        }
        Core.ItemEquipHooks.WantTexture(texture.GetType());
    }

    static string? FileOf(Mod mod, string texture)
    {
        string name = texture.Replace('\\', '/');
        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name += ".png";
        string? direct = mod.ResolveFile(name);
        if (direct != null && File.Exists(direct)) return direct;
        int slash = name.IndexOf('/');
        if (slash > 0 && name[..slash] == mod.Name)
        {
            string? inner = mod.ResolveFile(name[(slash + 1)..]);
            if (inner != null && File.Exists(inner)) return inner;
        }
        return null;
    }

    internal static void Install()
    {
        List<EquipTexture> list;
        lock (Gate)
        {
            if (installed) return;
            installed = true;
            list = new List<EquipTexture>(Pending);
            Pending.Clear();
        }
        foreach (EquipType type in EquipTypes)
        {
            List<EquipTexture> ofType = list.FindAll(t => t.Type == type);
            if (ofType.Count == 0) continue;
            try { InstallType(type, ofType); }
            catch (Exception e) { Log.Error($"equip textures ({type}): {e}"); }
        }
    }

    internal static void Apply(Item item, int type)
    {
        Dictionary<EquipType, int>? slots;
        lock (Gate)
        {
            if (!SlotsOfItem.TryGetValue(type, out slots)) return;
        }
        foreach ((EquipType equip, int slot) in slots) SetItemSlot(item, equip, slot);
    }

    static void InstallType(EquipType type, List<EquipTexture> list)
    {
        int vanilla = GetCount(type);
        if (vanilla <= 0) throw new InvalidOperationException($"ArmorIDs count for {type} is not set yet");
        int total = vanilla + list.Count;
        if (!HasItemTypes(type) && total > MaxSbyteSlots)
            throw new InvalidOperationException($"{list.Count} {type} texture(s) go past the game's limit of {MaxSbyteSlots}");

        byte* kind = Host.Utf8(IdsName(type));
        try { Host.Native()->GrowEquipSets(kind, vanilla, total); }
        finally { Host.FreeUtf8(kind); }
        SetCount(type, total);
        lock (Gate)
        {
            Vanilla[type] = vanilla;
            InstalledTypes.Add(type);
        }
        if (type == EquipType.Wings) PrepareWingStats(vanilla, total);

        foreach ((Func<GameArray<GameObject>> get, Action<GameArray<GameObject>> set) in TextureArrays(type))
        {
            GameArray<GameObject> array = get();
            if (array.Length < total) set(array.Resized(total));
        }
        if (HasItemTypes(type))
        {
            GameArray<int> items = GetItemTypes(type);
            if (items.Length < total) SetItemTypes(type, items.Resized(total));
        }

        for (int i = 0; i < list.Count; i++)
        {
            EquipTexture texture = list[i];
            int slot = vanilla + i;
            texture.Slot = slot;
            IntPtr asset = RequestTexture(texture);
            foreach ((Func<GameArray<GameObject>> get, _) in TextureArrays(type)) get()[slot] = new GameObject(asset);
            if (NewFraming(type) is Func<GameArray<bool>> framing) framing()[slot] = true;
            if (type == EquipType.Wings) ArmorIDs.Wing.Sets.Stats[slot] = WingStats.Default;
            lock (Gate)
            {
                Textures[(type, slot)] = texture;
                ByName[(type, texture.Name)] = texture;
            }
            if (texture.Item is not ModItem item) continue;
            lock (Gate)
            {
                if (!SlotsOfItem.TryGetValue(item.Type, out Dictionary<EquipType, int>? slots)) SlotsOfItem[item.Type] = slots = new Dictionary<EquipType, int>();
                slots.TryAdd(type, slot);
            }
            if (HasItemTypes(type)) GetItemTypes(type)[slot] = item.Type;
        }
        Log.Info($"equip textures: {list.Count} {type} (slots {vanilla}..{total - 1})");
    }

    static IntPtr RequestTexture(EquipTexture texture)
    {
        byte* file = Host.Utf8(texture.Texture), name = Host.Utf8(texture.Name);
        try
        {
            IntPtr asset = Host.Native()->RequestModTexture(file, name);
            if (asset == IntPtr.Zero) throw new InvalidOperationException($"{texture.Texture} is not an image the game can load");
            return asset;
        }
        finally
        {
            Host.FreeUtf8(file);
            Host.FreeUtf8(name);
        }
    }

    static void PrepareWingStats(int first, int total)
    {
        Core.ItemHooks.Once("EquipWingStats", () => On_WingStatsInitializer.Load += orig =>
        {
            GameArray<WingStats> old = ArmorIDs.Wing.Sets.Stats;
            orig();
            GameArray<WingStats> now = ArmorIDs.Wing.Sets.Stats;
            if (old.IsNull || now.IsNull || old.Pointer == now.Pointer) return;
            for (int slot = first; slot < total && slot < old.Length && slot < now.Length; slot++) now[slot] = old[slot];
        });
        if (ArmorIDs.Wing.Sets.Stats.IsNull) WingStatsInitializer.Load();
    }

    internal static int PlayerSlot(Player player, EquipType type) => type switch
    {
        EquipType.Head => player.head,
        EquipType.Body => player.body,
        EquipType.Legs => player.legs,
        EquipType.HandsOn => player.handon,
        EquipType.HandsOff => player.handoff,
        EquipType.Back => player.back,
        EquipType.Front => player.front,
        EquipType.Shoes => player.shoe,
        EquipType.Waist => player.waist,
        EquipType.Wings => player.wings,
        EquipType.Shield => player.shield,
        EquipType.Neck => player.neck,
        EquipType.Face => player.face,
        EquipType.Balloon => player.balloon,
        EquipType.Beard => player.beard,
        _ => -1,
    };

    static string IdsName(EquipType type) => type switch
    {
        EquipType.HandsOn => "HandOn",
        EquipType.HandsOff => "HandOff",
        EquipType.Shoes => "Shoe",
        EquipType.Wings => "Wing",
        _ => type.ToString(),
    };

    static int GetCount(EquipType type) => type switch
    {
        EquipType.Head => ArmorIDs.Head.Count,
        EquipType.Body => ArmorIDs.Body.Count,
        EquipType.Legs => ArmorIDs.Legs.Count,
        EquipType.HandsOn => ArmorIDs.HandOn.Count,
        EquipType.HandsOff => ArmorIDs.HandOff.Count,
        EquipType.Back => ArmorIDs.Back.Count,
        EquipType.Front => ArmorIDs.Front.Count,
        EquipType.Shoes => ArmorIDs.Shoe.Count,
        EquipType.Waist => ArmorIDs.Waist.Count,
        EquipType.Wings => ArmorIDs.Wing.Count,
        EquipType.Shield => ArmorIDs.Shield.Count,
        EquipType.Neck => ArmorIDs.Neck.Count,
        EquipType.Face => ArmorIDs.Face.Count,
        EquipType.Balloon => ArmorIDs.Balloon.Count,
        EquipType.Beard => ArmorIDs.Beard.Count,
        _ => 0,
    };

    static void SetCount(EquipType type, int count)
    {
        switch (type)
        {
            case EquipType.Head: ArmorIDs.Head.Count = count; break;
            case EquipType.Body: ArmorIDs.Body.Count = count; break;
            case EquipType.Legs: ArmorIDs.Legs.Count = count; break;
            case EquipType.HandsOn: ArmorIDs.HandOn.Count = count; break;
            case EquipType.HandsOff: ArmorIDs.HandOff.Count = count; break;
            case EquipType.Back: ArmorIDs.Back.Count = count; break;
            case EquipType.Front: ArmorIDs.Front.Count = count; break;
            case EquipType.Shoes: ArmorIDs.Shoe.Count = count; break;
            case EquipType.Waist: ArmorIDs.Waist.Count = count; break;
            case EquipType.Wings: ArmorIDs.Wing.Count = count; break;
            case EquipType.Shield: ArmorIDs.Shield.Count = count; break;
            case EquipType.Neck: ArmorIDs.Neck.Count = count; break;
            case EquipType.Face: ArmorIDs.Face.Count = (sbyte)count; break;
            case EquipType.Balloon: ArmorIDs.Balloon.Count = count; break;
            case EquipType.Beard: ArmorIDs.Beard.Count = (sbyte)count; break;
        }
    }

    static (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] TextureArrays(EquipType type) => type switch
    {
        EquipType.Head => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.ArmorHead, a => TextureAssets.ArmorHead = a) },
        EquipType.Body => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[]
        {
            (() => TextureAssets.ArmorBodyComposite, a => TextureAssets.ArmorBodyComposite = a), (() => TextureAssets.ArmorBody, a => TextureAssets.ArmorBody = a),
            (() => TextureAssets.FemaleBody, a => TextureAssets.FemaleBody = a), (() => TextureAssets.ArmorArm, a => TextureAssets.ArmorArm = a),
        },
        EquipType.Legs => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.ArmorLeg, a => TextureAssets.ArmorLeg = a) },
        EquipType.HandsOn => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[]
        {
            (() => TextureAssets.AccHandsOnComposite, a => TextureAssets.AccHandsOnComposite = a), (() => TextureAssets.AccHandsOn, a => TextureAssets.AccHandsOn = a),
        },
        EquipType.HandsOff => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[]
        {
            (() => TextureAssets.AccHandsOffComposite, a => TextureAssets.AccHandsOffComposite = a), (() => TextureAssets.AccHandsOff, a => TextureAssets.AccHandsOff = a),
        },
        EquipType.Back => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccBack, a => TextureAssets.AccBack = a) },
        EquipType.Front => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccFront, a => TextureAssets.AccFront = a) },
        EquipType.Shoes => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccShoes, a => TextureAssets.AccShoes = a) },
        EquipType.Waist => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccWaist, a => TextureAssets.AccWaist = a) },
        EquipType.Wings => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.Wings, a => TextureAssets.Wings = a) },
        EquipType.Shield => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccShield, a => TextureAssets.AccShield = a) },
        EquipType.Neck => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccNeck, a => TextureAssets.AccNeck = a) },
        EquipType.Face => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccFace, a => TextureAssets.AccFace = a) },
        EquipType.Balloon => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccBalloon, a => TextureAssets.AccBalloon = a) },
        EquipType.Beard => new (Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)[] { (() => TextureAssets.AccBeard, a => TextureAssets.AccBeard = a) },
        _ => Array.Empty<(Func<GameArray<GameObject>>, Action<GameArray<GameObject>>)>(),
    };

    static Func<GameArray<bool>>? NewFraming(EquipType type) => type switch
    {
        EquipType.Body => () => ArmorIDs.Body.Sets.UsesNewFramingCode,
        EquipType.HandsOn => () => ArmorIDs.HandOn.Sets.UsesNewFramingCode,
        EquipType.HandsOff => () => ArmorIDs.HandOff.Sets.UsesNewFramingCode,
        _ => null,
    };

    static bool HasItemTypes(EquipType type) => type is EquipType.Head or EquipType.Body or EquipType.Legs;

    static GameArray<int> GetItemTypes(EquipType type) => type switch
    {
        EquipType.Head => Item.headType,
        EquipType.Body => Item.bodyType,
        _ => Item.legType,
    };

    static void SetItemTypes(EquipType type, GameArray<int> array)
    {
        switch (type)
        {
            case EquipType.Head: Item.headType = array; break;
            case EquipType.Body: Item.bodyType = array; break;
            case EquipType.Legs: Item.legType = array; break;
        }
    }

    static void SetItemSlot(Item item, EquipType type, int slot)
    {
        switch (type)
        {
            case EquipType.Head: item.headSlot = slot; break;
            case EquipType.Body: item.bodySlot = slot; break;
            case EquipType.Legs: item.legSlot = slot; break;
            case EquipType.HandsOn: item.handOnSlot = (sbyte)slot; break;
            case EquipType.HandsOff: item.handOffSlot = (sbyte)slot; break;
            case EquipType.Back: item.backSlot = (sbyte)slot; break;
            case EquipType.Front: item.frontSlot = (sbyte)slot; break;
            case EquipType.Shoes: item.shoeSlot = (sbyte)slot; break;
            case EquipType.Waist: item.waistSlot = (sbyte)slot; break;
            case EquipType.Wings: item.wingSlot = (sbyte)slot; break;
            case EquipType.Shield: item.shieldSlot = (sbyte)slot; break;
            case EquipType.Neck: item.neckSlot = (sbyte)slot; break;
            case EquipType.Face: item.faceSlot = (sbyte)slot; break;
            case EquipType.Balloon: item.balloonSlot = (sbyte)slot; break;
            case EquipType.Beard: item.beardSlot = (sbyte)slot; break;
        }
    }
}

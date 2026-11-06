using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader.Config;

public abstract class EntityDefinition : TagSerializable
{
    [DefaultValue("Terraria")]
    public string Mod;

    [DefaultValue("None")]
    public string Name;

    [JsonIgnore]
    public virtual bool IsUnloaded => Type <= 0 && !(Mod == "Terraria" && Name == "None" || Mod == "" && Name == "");

    [JsonIgnore]
    public abstract int Type { get; }

    [JsonIgnore]
    public virtual string DisplayName => ToString();

    internal abstract int MinType { get; }

    internal abstract int MaxType { get; }

    internal abstract EntityDefinition AtType(int type);

    public EntityDefinition()
    {
        Mod = "Terraria";
        Name = "None";
    }

    public EntityDefinition(string mod, string name)
    {
        Mod = mod;
        Name = name;
    }

    public EntityDefinition(string key)
    {
        Mod = "Terraria";
        Name = key;
        string[] parts = key.Split('/', 2);
        if (parts.Length == 2)
        {
            Mod = parts[0];
            Name = parts[1];
        }
    }

    protected string Key => Mod != "Terraria" ? $"{Mod}/{Name}" : Name;

    protected string Unloaded => Language.GetTextValue("Mods.ModLoader.Unloaded");

    public override bool Equals(object? obj) => obj is EntityDefinition other && Mod == other.Mod && Name == other.Name;

    public override string ToString() => $"{Mod}/{Name}";

    public override int GetHashCode() => HashCode.Combine(Mod, Name);

    public TagCompound SerializeData() => new()
    {
        ["mod"] = Mod,
        ["name"] = Name,
    };

    protected static int ModType<T>(string mod, string name, Func<T, int> type) where T : IModType =>
        ModContent.TryFind(mod, name, out T content) ? type(content) : -1;
}

[TypeConverter(typeof(ToFromStringConverter<ItemDefinition>))]
public class ItemDefinition : EntityDefinition
{
    public static readonly Func<TagCompound, ItemDefinition> DESERIALIZER = Load;

    public override int Type => Mod != "Terraria" ? ModType<ModItem>(Mod, Name, m => m.Type) : ItemID.Search.TryGetId(Name, out int id) ? id : -1;

    public ItemDefinition() : base()
    {
    }

    public ItemDefinition(int type) : base(ItemLoader.GetItem(type)?.FullName ?? ItemID.Search.GetName(type))
    {
    }

    public ItemDefinition(string key) : base(key)
    {
    }

    public ItemDefinition(string mod, string name) : base(mod, name)
    {
    }

    public static ItemDefinition FromString(string s) => new(s);

    public static ItemDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));

    public override string DisplayName => IsUnloaded ? Unloaded : Lang.GetItemNameValue(Type);

    internal override int MinType => 0;

    internal override int MaxType => ItemLoader.ItemCount - 1;

    internal override EntityDefinition AtType(int type) => new ItemDefinition(type);
}

[TypeConverter(typeof(ToFromStringConverter<ProjectileDefinition>))]
public class ProjectileDefinition : EntityDefinition
{
    public static readonly Func<TagCompound, ProjectileDefinition> DESERIALIZER = Load;

    public override int Type => Mod != "Terraria" ? ModType<ModProjectile>(Mod, Name, m => m.Type) : ProjectileID.Search.TryGetId(Name, out int id) ? id : -1;

    public ProjectileDefinition() : base()
    {
    }

    public ProjectileDefinition(int type) : base(ProjectileLoader.GetProjectile(type)?.FullName ?? ProjectileID.Search.GetName(type))
    {
    }

    public ProjectileDefinition(string key) : base(key)
    {
    }

    public ProjectileDefinition(string mod, string name) : base(mod, name)
    {
    }

    public static ProjectileDefinition FromString(string s) => new(s);

    public static ProjectileDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));

    public override string DisplayName => IsUnloaded ? Unloaded : Lang.GetProjectileName(Type).Value;

    internal override int MinType => 0;

    internal override int MaxType => ProjectileLoader.ProjectileCount - 1;

    internal override EntityDefinition AtType(int type) => new ProjectileDefinition(type);
}

[TypeConverter(typeof(ToFromStringConverter<NPCDefinition>))]
public class NPCDefinition : EntityDefinition
{
    public static readonly Func<TagCompound, NPCDefinition> DESERIALIZER = Load;

    public override bool IsUnloaded => Type <= NPCID.NegativeIDCount && !(Mod == "Terraria" && Name == "None" || Mod == "" && Name == "");

    public override int Type
    {
        get
        {
            if (Mod != "Terraria")
            {
                int type = ModType<ModNPC>(Mod, Name, m => m.Type);
                return type >= 0 ? type : NPCID.NegativeIDCount;
            }
            return NPCID.Search.TryGetId(Name, out int id) ? id : NPCID.NegativeIDCount;
        }
    }

    public NPCDefinition() : base()
    {
    }

    public NPCDefinition(int type) : base(NPCLoader.GetNPC(type)?.FullName ?? NPCID.Search.GetName(type))
    {
    }

    public NPCDefinition(string key) : base(key)
    {
    }

    public NPCDefinition(string mod, string name) : base(mod, name)
    {
    }

    public static NPCDefinition FromString(string s) => new(s);

    public static NPCDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));

    public override string DisplayName => IsUnloaded ? Unloaded : Lang.GetNPCNameValue(Type);

    internal override int MinType => NPCID.NegativeIDCount + 1;

    internal override int MaxType => NPCLoader.NPCCount - 1;

    internal override EntityDefinition AtType(int type) => new NPCDefinition(type);
}

[TypeConverter(typeof(ToFromStringConverter<PrefixDefinition>))]
public class PrefixDefinition : EntityDefinition
{
    public static readonly Func<TagCompound, PrefixDefinition> DESERIALIZER = Load;

    public override int Type
    {
        get
        {
            if (Mod == "Terraria" && Name == "None") return 0;
            return Mod != "Terraria" ? ModType<ModPrefix>(Mod, Name, m => m.Type) : PrefixID.Search.TryGetId(Name, out int id) ? id : -1;
        }
    }

    public PrefixDefinition() : base()
    {
    }

    public PrefixDefinition(int type) : base(PrefixLoader.GetPrefix(type)?.FullName ?? PrefixID.Search.GetName(type))
    {
    }

    public PrefixDefinition(string key) : base(key)
    {
    }

    public PrefixDefinition(string mod, string name) : base(mod, name)
    {
    }

    public static PrefixDefinition FromString(string s) => new(s);

    public static PrefixDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));

    public override string DisplayName
    {
        get
        {
            if (IsUnloaded) return Unloaded;
            return Type == 0 ? Lang.inter[23].Value : Lang.prefix[Type].Value;
        }
    }

    internal override int MinType => 0;

    internal override int MaxType => PrefixLoader.PrefixCount - 1;

    internal override EntityDefinition AtType(int type) => new PrefixDefinition(type);
}

[TypeConverter(typeof(ToFromStringConverter<BuffDefinition>))]
public class BuffDefinition : EntityDefinition
{
    public static readonly Func<TagCompound, BuffDefinition> DESERIALIZER = Load;

    public override int Type
    {
        get
        {
            if (Mod == "Terraria" && Name == "None") return 0;
            return Mod != "Terraria" ? ModType<ModBuff>(Mod, Name, m => m.Type) : BuffID.Search.TryGetId(Name, out int id) ? id : -1;
        }
    }

    public BuffDefinition() : base()
    {
    }

    public BuffDefinition(int type) : base(BuffLoader.GetBuff(type)?.FullName ?? BuffID.Search.GetName(type))
    {
    }

    public BuffDefinition(string key) : base(key)
    {
    }

    public BuffDefinition(string mod, string name) : base(mod, name)
    {
    }

    public static BuffDefinition FromString(string s) => new(s);

    public static BuffDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));

    public override string DisplayName => IsUnloaded ? Unloaded : Lang.GetBuffName(Type);

    internal override int MinType => 0;

    internal override int MaxType => BuffLoader.BuffCount - 1;

    internal override EntityDefinition AtType(int type) => new BuffDefinition(type);
}

[TypeConverter(typeof(ToFromStringConverter<TileDefinition>))]
public class TileDefinition : EntityDefinition
{
    public static readonly Func<TagCompound, TileDefinition> DESERIALIZER = Load;

    public override bool IsUnloaded => Type < 0 && !(Mod == "Terraria" && Name == "None" || Mod == "" && Name == "");

    public override int Type => Mod != "Terraria" ? ModType<ModTile>(Mod, Name, m => m.Type) : TileID.Search.TryGetId(Name, out int id) ? id : -1;

    public TileDefinition() : base()
    {
    }

    public TileDefinition(int type) : base(TileLoader.GetTile(type)?.FullName ?? TileID.Search.GetName(type))
    {
    }

    public TileDefinition(string key) : base(key)
    {
    }

    public TileDefinition(string mod, string name) : base(mod, name)
    {
    }

    public static TileDefinition FromString(string s) => new(s);

    public static TileDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));

    public override string DisplayName => IsUnloaded ? Unloaded : Name;

    internal override int MinType => 0;

    internal override int MaxType => TileLoader.TileCount - 1;

    internal override EntityDefinition AtType(int type) => new TileDefinition(type);
}

[TypeConverter(typeof(ToFromStringConverter<WallDefinition>))]
public class WallDefinition : EntityDefinition
{
    public static readonly Func<TagCompound, WallDefinition> DESERIALIZER = Load;

    public override int Type => Mod != "Terraria" ? ModType<ModWall>(Mod, Name, m => m.Type) : WallID.Search.TryGetId(Name, out int id) ? id : -1;

    public WallDefinition() : base()
    {
    }

    public WallDefinition(int type) : base(WallLoader.GetWall(type)?.FullName ?? WallID.Search.GetName(type))
    {
    }

    public WallDefinition(string key) : base(key)
    {
    }

    public WallDefinition(string mod, string name) : base(mod, name)
    {
    }

    public static WallDefinition FromString(string s) => new(s);

    public static WallDefinition Load(TagCompound tag) => new(tag.GetString("mod"), tag.GetString("name"));

    public override string DisplayName => IsUnloaded ? Unloaded : Name;

    internal override int MinType => 0;

    internal override int MaxType => WallLoader.WallCount - 1;

    internal override EntityDefinition AtType(int type) => new WallDefinition(type);
}

public class ToFromStringConverter<T> : TypeConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType != typeof(string);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string)
        {
            MethodInfo? parse = typeof(T).GetMethod("FromString", new[] { typeof(string) });
            if (parse != null && parse.IsStatic && parse.ReturnType == typeof(T))
                return parse.Invoke(null, new[] { value });
            throw new JsonException($"The {typeof(T).Name} type does not have a public static FromString(string) method that returns a {typeof(T).Name}.");
        }

        return base.ConvertFrom(context, culture, value);
    }
}

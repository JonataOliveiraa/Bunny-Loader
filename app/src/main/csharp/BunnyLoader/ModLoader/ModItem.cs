using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public abstract class ModItem : ModType<Item, ModItem>, ILocalizedModType
{
    public Item Item => Entity;

    public int Type { get; internal set; }

    public virtual string Texture => GetType().Namespace is { Length: > 0 } ns
        ? (ns + "." + Name).Replace('.', '/')
        : Name;

    public virtual string LocalizationCategory => "Items";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public virtual LocalizedText Tooltip => this.GetLocalization(nameof(Tooltip), () => "");

    protected sealed override Item CreateTemplateEntity() => default;

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults() { }

    public virtual void AddRecipes() { }

    public virtual void SaveData(TagCompound tag) { }

    public virtual void LoadData(TagCompound tag) { }

    internal string? SavedData;

    internal bool LoadFailed;

    protected sealed override void Register() => ItemLoader.Register(this);
}

using Microsoft.Xna.Framework;
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

    public virtual bool CanUseItem(Player player) => true;

    public virtual bool? UseItem(Player player) => null;

    public virtual void UseAnimation(Player player) { }

    public virtual void HoldItem(Player player) { }

    public virtual void UseStyle(Player player, Rectangle heldItemFrame) { }

    public virtual void HoldStyle(Player player, Rectangle heldItemFrame) { }

    public virtual Vector2? HoldoutOffset() => null;

    public virtual void UpdateInventory(Player player) { }

    public virtual void GetHealLife(Player player, bool quickHeal, ref int healValue) { }

    public virtual void GetHealMana(Player player, bool quickHeal, ref int healValue) { }

    public virtual void ModifyPotionDelay(Player player, ref int baseDelay) { }

    public virtual bool ApplyPotionDelay(Player player, int potionDelay) => true;

    public virtual void GrabRange(Player player, ref int grabRange) { }

    public virtual bool GrabStyle(Player player) => false;

    public virtual bool CanPickup(Player player) => true;

    public virtual bool OnPickup(Player player) => true;

    public virtual bool ItemSpace(Player player) => false;

    public virtual void SaveData(TagCompound tag) { }

    public virtual void LoadData(TagCompound tag) { }

    internal string? SavedData;

    internal bool LoadFailed;

    protected sealed override void Register() => ItemLoader.Register(this);
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using Terraria.Utilities;

namespace Terraria.ModLoader;

public abstract class ModItem : ModType<Item, ModItem>, ILocalizedModType
{
    public ref Item Item => ref entity;

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

    public Recipe CreateRecipe(int amount = 1) => Recipe.Create(Type, amount);

    public virtual void OnCreated(ItemCreationContext context) { }

    public virtual bool CanUseItem(Player player) => true;

    public virtual bool? UseItem(Player player) => null;

    public virtual void UseAnimation(Player player) { }

    public virtual void HoldItem(Player player) { }

    public virtual void UseStyle(Player player, Rectangle heldItemFrame) { }

    public virtual void HoldStyle(Player player, Rectangle heldItemFrame) { }

    public virtual Vector2? HoldoutOffset() => null;

    public virtual void UpdateInventory(Player player) { }

    public virtual void UpdateEquip(Player player) { }

    public virtual void UpdateAccessory(Player player, bool hideVisual) { }

    public virtual void UpdateVanity(Player player) { }

    public virtual void EquipFrameEffects(Player player, EquipType type) { }

    public virtual bool IsArmorSet(Item head, Item body, Item legs) => false;

    public virtual void UpdateArmorSet(Player player) { }

    public virtual bool IsVanitySet(int head, int body, int legs) =>
        IsArmorSet(Core.ItemEquipHooks.Sample(Item.headType, head), Core.ItemEquipHooks.Sample(Item.bodyType, body), Core.ItemEquipHooks.Sample(Item.legType, legs));

    public virtual void PreUpdateVanitySet(Player player) { }

    public virtual void UpdateVanitySet(Player player) { }

    public virtual void ArmorSetShadows(Player player) { }

    public virtual void SetMatch(bool male, ref int equipSlot, ref bool robes) { }

    public virtual void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
                                           ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend) { }

    public virtual void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration) { }

    public virtual bool WingUpdate(Player player, bool inUse) => false;

    public virtual bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) => true;

    public virtual void GetHealLife(Player player, bool quickHeal, ref int healValue) { }

    public virtual void GetHealMana(Player player, bool quickHeal, ref int healValue) { }

    public virtual void ModifyPotionDelay(Player player, ref int baseDelay) { }

    public virtual bool ApplyPotionDelay(Player player, int potionDelay) => true;

    public virtual void GrabRange(Player player, ref int grabRange) { }

    public virtual bool GrabStyle(Player player) => false;

    public virtual bool CanPickup(Player player) => true;

    public virtual bool OnPickup(Player player) => true;

    public virtual bool ItemSpace(Player player) => false;

    public virtual bool MeleePrefix() => Item.melee && !Item.noUseGraphic;

    public virtual bool WeaponPrefix() => Item.melee && Item.noUseGraphic;

    public virtual bool RangedPrefix() => Item.ranged;

    public virtual bool MagicPrefix() => Item.magic;

    public virtual bool SummonPrefix() => Item.summon;

    public virtual bool AllowPrefix(int pre) => true;

    public virtual int ChoosePrefix(UnifiedRandom rand) => -1;

    public virtual bool? PrefixChance(int pre, UnifiedRandom rand) => null;

    public virtual bool CanRightClick() => false;

    public virtual void RightClick(Player player) { }

    public virtual bool ConsumeItem(Player player) => true;

    public virtual void ModifyTooltips(List<TooltipLine> tooltips) { }

    public virtual bool PreDrawTooltip(ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y) => true;

    public virtual void PostDrawTooltip(ReadOnlyCollection<DrawableTooltipLine> lines) { }

    public virtual bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) => true;

    public virtual void PostDrawTooltipLine(DrawableTooltipLine line) { }

    public virtual Color? GetAlpha(Color lightColor) => null;

    public virtual bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) => true;

    public virtual void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) { }

    public virtual bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) => true;

    public virtual void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) { }

    public virtual void NetSend(BinaryWriter writer) { }

    public virtual void NetReceive(BinaryReader reader) { }

    public virtual void SaveData(TagCompound tag) { }

    public virtual void LoadData(TagCompound tag) { }

    internal string? SavedData;

    internal bool LoadFailed;

    protected sealed override void Register() => ItemLoader.Register(this);
}

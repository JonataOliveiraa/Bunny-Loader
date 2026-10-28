using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Utilities;

namespace Terraria.ModLoader;

public abstract class GlobalItem : GlobalType<Item, GlobalItem>
{
    protected sealed override void Register() => ItemLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults(Item entity) { }

    public virtual void AddRecipes() { }

    public virtual void OnCreated(Item item, ItemCreationContext context) { }

    public virtual bool CanUseItem(Item item, Player player) => true;

    public virtual bool? UseItem(Item item, Player player) => null;

    public virtual void UseAnimation(Item item, Player player) { }

    public virtual void HoldItem(Item item, Player player) { }

    public virtual void UseStyle(Item item, Player player, Rectangle heldItemFrame) { }

    public virtual void HoldStyle(Item item, Player player, Rectangle heldItemFrame) { }

    public virtual Vector2? HoldoutOffset(int type) => null;

    public virtual void UpdateInventory(Item item, Player player) { }

    public virtual void UpdateEquip(Item item, Player player) { }

    public virtual void UpdateAccessory(Item item, Player player, bool hideVisual) { }

    public virtual void UpdateVanity(Item item, Player player) { }

    public virtual string IsArmorSet(Item head, Item body, Item legs) => "";

    public virtual void UpdateArmorSet(Player player, string set) { }

    public virtual string IsVanitySet(int head, int body, int legs) =>
        IsArmorSet(Core.ItemEquipHooks.Sample(Item.headType, head), Core.ItemEquipHooks.Sample(Item.bodyType, body), Core.ItemEquipHooks.Sample(Item.legType, legs));

    public virtual void PreUpdateVanitySet(Player player, string set) { }

    public virtual void UpdateVanitySet(Player player, string set) { }

    public virtual void ArmorSetShadows(Player player, string set) { }

    public virtual void SetMatch(int armorSlot, int type, bool male, ref int equipSlot, ref bool robes) { }

    public virtual void VerticalWingSpeeds(Item item, Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
                                           ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend) { }

    public virtual void HorizontalWingSpeeds(Item item, Player player, ref float speed, ref float acceleration) { }

    public virtual bool WingUpdate(int wings, Player player, bool inUse) => false;

    public virtual bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) => true;

    public virtual void GetHealLife(Item item, Player player, bool quickHeal, ref int healValue) { }

    public virtual void GetHealMana(Item item, Player player, bool quickHeal, ref int healValue) { }

    public virtual void ModifyPotionDelay(Item item, Player player, ref int baseDelay) { }

    public virtual bool ApplyPotionDelay(Item item, Player player, int potionDelay) => true;

    public virtual void GrabRange(Item item, Player player, ref int grabRange) { }

    public virtual bool GrabStyle(Item item, Player player) => false;

    public virtual bool CanPickup(Item item, Player player) => true;

    public virtual bool OnPickup(Item item, Player player) => true;

    public virtual bool ItemSpace(Item item, Player player) => false;

    public virtual void NetSend(Item item, BinaryWriter writer) { }

    public virtual void NetReceive(Item item, BinaryReader reader) { }

    public virtual bool AllowPrefix(Item item, int pre) => true;

    public virtual int ChoosePrefix(Item item, UnifiedRandom rand) => -1;

    public virtual bool? PrefixChance(Item item, int pre, UnifiedRandom rand) => null;

    public virtual bool CanRightClick(Item item) => false;

    public virtual void RightClick(Item item, Player player) { }

    public virtual bool ConsumeItem(Item item, Player player) => true;

    public virtual void ModifyTooltips(Item item, List<TooltipLine> tooltips) { }

    public virtual bool PreDrawTooltip(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y) => true;

    public virtual void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines) { }

    public virtual bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset) => true;

    public virtual void PostDrawTooltipLine(Item item, DrawableTooltipLine line) { }

    public virtual Color? GetAlpha(Item item, Color lightColor) => null;

    public virtual bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) => true;

    public virtual void PostDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) { }

    public virtual bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) => true;

    public virtual void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) { }
}

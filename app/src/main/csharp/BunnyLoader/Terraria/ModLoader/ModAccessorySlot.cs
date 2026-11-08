using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.Default;
using Terraria.UI;

namespace Terraria.ModLoader;

public abstract class ModAccessorySlot : ModType
{
    public int Type { get; internal set; }

    public static Player Player => Main.CurrentPlayer;

    public ModAccessorySlotPlayer ModSlotPlayer => AccessorySlotLoader.ModSlotPlayer(Player);

    public virtual Vector2? CustomLocation => null;

    public virtual string? DyeBackgroundTexture => null;
    public virtual string? VanityBackgroundTexture => null;
    public virtual string? FunctionalBackgroundTexture => null;

    public virtual string? DyeTexture => null;
    public virtual string? VanityTexture => null;
    public virtual string? FunctionalTexture => null;

    public virtual bool DrawFunctionalSlot => true;
    public virtual bool DrawVanitySlot => true;
    public virtual bool DrawDyeSlot => true;

    public virtual bool HasEquipmentLoadoutSupport => true;

    public Item FunctionalItem
    {
        get => ModSlotPlayer.exAccessorySlot[Type];
        set => ModSlotPlayer.exAccessorySlot[Type] = value.IsNull ? Item.New() : value;
    }

    public Item VanityItem
    {
        get => ModSlotPlayer.exAccessorySlot[Type + ModSlotPlayer.SlotCount];
        set => ModSlotPlayer.exAccessorySlot[Type + ModSlotPlayer.SlotCount] = value.IsNull ? Item.New() : value;
    }

    public Item DyeItem
    {
        get => ModSlotPlayer.exDyesAccessory[Type];
        set => ModSlotPlayer.exDyesAccessory[Type] = value.IsNull ? Item.New() : value;
    }

    public bool HideVisuals
    {
        get => ModSlotPlayer.exHideAccessory[Type];
        set => ModSlotPlayer.exHideAccessory[Type] = value;
    }

    public bool IsEmpty => FunctionalItem.IsAir && VanityItem.IsAir && DyeItem.IsAir;

    protected sealed override void Register() => Type = AccessorySlotLoader.Instance.Register(this);

    public virtual bool PreDraw(AccessorySlotType context, Item item, Vector2 position, bool isHovered) => true;

    public virtual void PostDraw(AccessorySlotType context, Item item, Vector2 position, bool isHovered) { }

    public virtual void ApplyEquipEffects()
    {
        Item functional = ModSlotPlayer.GetSharedLoadoutItem(ItemSlot.Context.ModdedAccessorySlot, Type, out _);
        Item vanity = ModSlotPlayer.GetSharedLoadoutItem(ItemSlot.Context.ModdedVanityAccessorySlot, Type + ModSlotPlayer.SlotCount, out _);

        if (!functional.IsAir)
        {
            if (functional.accessory)
                Player.GrantPrefixBenefits(functional);
            Player.GrantArmorBenefits(functional);
            AccessorySlotHooks.ApplyEquipFunctional(Player, functional, HideVisuals);
        }
        if (!vanity.IsAir)
            AccessorySlotHooks.ApplyEquipVanity(Player, vanity);
    }

    public virtual bool CanAcceptItem(Item checkItem, AccessorySlotType context) => true;

    public virtual bool ModifyDefaultSwapSlot(Item item, int accSlotToSwapTo) => false;

    public virtual bool IsHidden() => false;

    public virtual bool IsEnabled() => true;

    public virtual bool IsVisibleWhenNotEnabled() => !IsEmpty;

    public virtual void OnMouseHover(AccessorySlotType context) { }

    public virtual void BackgroundDrawColor(AccessorySlotType context, ref Color color) { }
}

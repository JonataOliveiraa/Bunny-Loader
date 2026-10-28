namespace Terraria.ModLoader;

public class EquipTexture
{
    public string Texture { get; internal set; } = "";

    public string Name { get; internal set; } = "";

    public EquipType Type { get; internal set; }

    public int Slot { get; internal set; } = -1;

    public ModItem? Item { get; internal set; }

    public virtual void FrameEffects(Player player, EquipType type) => Item?.EquipFrameEffects(player, type);

    public virtual bool IsVanitySet(int head, int body, int legs) => Item != null && Item.IsVanitySet(head, body, legs);

    public virtual void PreUpdateVanitySet(Player player) => Item?.PreUpdateVanitySet(player);

    public virtual void UpdateVanitySet(Player player) => Item?.UpdateVanitySet(player);

    public virtual void ArmorSetShadows(Player player) => Item?.ArmorSetShadows(player);

    public virtual void SetMatch(bool male, ref int equipSlot, ref bool robes) => Item?.SetMatch(male, ref equipSlot, ref robes);

    public virtual void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
                                           ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend) =>
        Item?.VerticalWingSpeeds(player, ref ascentWhenFalling, ref ascentWhenRising, ref maxCanAscendMultiplier, ref maxAscentMultiplier, ref constantAscend);

    public virtual void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration) =>
        Item?.HorizontalWingSpeeds(player, ref speed, ref acceleration);

    public virtual bool WingUpdate(Player player, bool inUse) => Item != null && Item.WingUpdate(player, inUse);
}

using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Item
{
    public ModItem? ModItem => ItemLoader.InstanceOf(this);
}

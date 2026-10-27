using Terraria;

namespace CsSource.Items;

public static class MinisharkTuning
{
    public const int UseTime = 5;
    public const int Damage = 42;

    public static void Apply(Item item)
    {
        item.useTime = UseTime;
        item.useAnimation = UseTime;
        item.damage = Damage;
    }
}

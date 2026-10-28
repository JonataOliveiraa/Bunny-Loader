using Terraria;
using Terraria.ModLoader;

namespace CsPickup.Content.Items;

public abstract class TestGem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 1;
    }
}

public class Plain : TestGem
{
}

public class Untouchable : TestGem
{
    public static string Seen = "";

    public override bool CanPickup(Player player)
    {
        Seen = $"{Item.type} {player.whoAmI}";
        return false;
    }
}

public class Orb : TestGem
{
    public static int Calls;

    public override bool OnPickup(Player player)
    {
        Calls++;
        return false;
    }
}

public class FullOrb : TestGem
{
    public static int Calls;

    public override bool ItemSpace(Player player) => true;

    public override bool OnPickup(Player player)
    {
        Calls++;
        return false;
    }
}

public class Magnet : TestGem
{
    public override void GrabRange(Player player, ref int grabRange) => grabRange += 400;
}

public class Styled : TestGem
{
    public static int Calls;

    public override void GrabRange(Player player, ref int grabRange) => grabRange += 400;

    public override bool GrabStyle(Player player)
    {
        WorldItem w = Item.GetWorldItem();
        if (w.IsNull) return false;
        Calls++;
        var at = player.position;
        var to = w.position;
        float dx = at.X + player.width / 2f - (to.X + w.width / 2f);
        float dy = at.Y + player.height / 2f - (to.Y + w.height / 2f);
        float length = System.MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1) length = 1;
        w.velocity = new Microsoft.Xna.Framework.Vector2(dx / length * 8, dy / length * 8);
        return true;
    }
}

public class Shy : TestGem
{
    public override void GrabRange(Player player, ref int grabRange) => grabRange -= 1000;
}

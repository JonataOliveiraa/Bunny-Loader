using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsVanillaTex.Content;

public static class Counts
{
    public static readonly Dictionary<string, int> Calls = new();

    public static void Add(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public static int Of(string name) => Calls.GetValueOrDefault(name);
}

public class VanillaPrism : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.LastPrism;

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
        Item.maxStack = 1;
    }

    public override void PostDrawInWorld(WorldItem item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) =>
        Counts.Add("ItemDrawInWorld");
}

public class MissingGameItem : ModItem
{
    public override string Texture => "Terraria/Images/Item_NoSuchImage";

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
    }
}

public class MissingFileItem : ModItem
{
    public override string Texture => "CsVanillaTex/Content/NoSuchFile";

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
    }
}

public class VanillaPrismHoldout : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LastPrism;

    public override void SetStaticDefaults() => Main.projFrames[Type] = 5;

    public override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = 600;
    }

    public override void AI() => Projectile.velocity = Vector2.Zero;

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        Counts.Add("ProjectilePreDraw");
        return true;
    }
}

public class OwnBolt : ModProjectile
{
    public override string Texture => "CsVanillaTex/Content/OwnBolt";

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
        Projectile.timeLeft = 600;
    }
}

public class VanillaBuff : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_" + BuffID.Ironskin;
}

public class VanillaSlime : ModNPC
{
    public override string Texture => "Terraria/Images/NPC_" + NPCID.BlueSlime;

    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 2;

    public override void SetDefaults()
    {
        NPC.width = 24;
        NPC.height = 18;
        NPC.lifeMax = 50;
        NPC.aiStyle = -1;
        NPC.friendly = true;
        NPC.noGravity = true;
    }
}

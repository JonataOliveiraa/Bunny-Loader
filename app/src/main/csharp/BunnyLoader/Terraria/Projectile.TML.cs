using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Projectile : IEntityWithGlobals<GlobalProjectile>
{
    public IReadOnlyList<GlobalProjectile> EntityGlobals => ProjectileLoader.Globals.Of(this);

    public T GetGlobalProjectile<T>() where T : GlobalProjectile => ProjectileLoader.Globals.Get<T>(this);

    public T GetGlobalProjectile<T>(T baseInstance) where T : GlobalProjectile => ProjectileLoader.Globals.Get(this, baseInstance);

    public bool TryGetGlobalProjectile<T>(out T result) where T : GlobalProjectile => ProjectileLoader.Globals.TryGet(this, out result);

    public bool TryGetOwner(out Player player)
    {
        player = default;
        if (npcProj || trap || (uint)owner > 255u) return false;
        player = Main.player[owner];
        return !player.IsNull && player.active;
    }

    public DamageClass DamageType
    {
        get => DamageClassLoader.ProjectileClass(this);
        set => DamageClassLoader.SetProjectileClass(this, value);
    }

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());

    public bool CountsAsClass(DamageClass damageClass) => DamageType.CountsAsClass(damageClass);

    public static int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner = -1, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) =>
        NewProjectile(spawnSource, position.X, position.Y, velocity.X, velocity.Y, Type, Damage, KnockBack, Owner, ai0, ai1, ai2);

    public static int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner = -1, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) =>
        NewProjectile(spawnSource, X, Y, SpeedX, SpeedY, Type, Damage, KnockBack, Owner == -1 ? Main.myPlayer : Owner, ai0, ai1, ai2, default);

    public static Projectile NewProjectileDirect(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int type, int damage, float knockback, int owner = -1, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) =>
        Main.projectile[NewProjectile(spawnSource, position, velocity, type, damage, knockback, owner, ai0, ai1, ai2)];

    public ModProjectile ModProjectile => ProjectileLoader.InstanceOf(this)!;

    public DrawData GetDefaultDrawData(Player player, Microsoft.Xna.Framework.Graphics.Color lightColor)
    {
        Microsoft.Xna.Framework.Graphics.Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[type].Value;
        int drawOffsetX = 0;
        int originOffsetY = 0;
        float originX = (texture.Width - width) * 0.5f + width * 0.5f;
        if (ModProjectile is ModProjectile m)
        {
            drawOffsetX = m.DrawOffsetX;
            originOffsetY = -m.DrawOriginOffsetY;
            originX += m.DrawOriginOffsetX;
        }
        var effects = spriteDirection == -1 ? Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally : Microsoft.Xna.Framework.Graphics.SpriteEffects.None;
        Rectangle sourceRectangle;
        int frames = Main.projFrames[type];
        if (frames > 1)
        {
            int frameHeight = texture.Height / frames;
            sourceRectangle = new Rectangle(0, frameHeight * frame, texture.Width, frameHeight - 1);
        }
        else
        {
            sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
            if (ownerHitCheck && player.gravDir == -1f)
            {
                if (player.direction == 1) effects = Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally;
                else if (player.direction == -1) effects = Microsoft.Xna.Framework.Graphics.SpriteEffects.None;
            }
        }
        return new DrawData(texture,
            new Vector2(position.X - Main.screenPosition.X + originX + drawOffsetX, position.Y - Main.screenPosition.Y + height / 2 + gfxOffY),
            sourceRectangle, GetAlpha(lightColor), rotation, new Vector2(originX, height / 2 + originOffsetY), scale, effects);
    }
}

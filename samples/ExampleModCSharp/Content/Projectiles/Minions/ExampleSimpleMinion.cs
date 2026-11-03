using System;
using ExampleMod.Content.Items;
using ExampleMod.Content.Tiles.Furniture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles.Minions;

public class ExampleSimpleMinionBuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
        BuffID.Sets.BuffTextHandlers.Add(Type, new CachedProjectileCounterBuffTextHandler(ModContent.ProjectileType<ExampleSimpleMinion>()));
    }

    public override void Update(Player player, ref int buffIndex)
    {
        if (player.ownedProjectileCounts[ModContent.ProjectileType<ExampleSimpleMinion>()] > 0)
        {
            player.buffTime[buffIndex] = 18000;
        }
        else
        {
            player.DelBuff(buffIndex);
            buffIndex--;
        }
    }
}

public class ExampleSimpleMinionItem : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
        ItemID.Sets.LockOnIgnoresCollision[Type] = true;

        ItemID.Sets.StaffMinionSlotsRequired[Type] = 1;
    }

    public override void SetDefaults()
    {
        Item.damage = 30;
        Item.knockBack = 3f;
        Item.mana = 10;
        Item.width = 32;
        Item.height = 32;
        Item.useTime = 36;
        Item.useAnimation = 36;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.value = Item.sellPrice(gold: 30);
        Item.rare = ItemRarityID.Cyan;
        Item.UseSound = SoundID.Item44;

        Item.noMelee = true;
        Item.DamageType = DamageClass.Summon;
        Item.buffType = ModContent.BuffType<ExampleSimpleMinionBuff>();
        Item.shoot = ModContent.ProjectileType<ExampleSimpleMinion>();
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        position = Main.MouseWorld;
        player.LimitPointToPlayerReachableArea(ref position);
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        player.AddBuff(Item.buffType, 2);
        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ModContent.ItemType<ExampleItem>())
            .AddTile(ModContent.TileType<ExampleWorkbench>())
            .Register();
    }
}

public class ExampleSimpleMinion : ModProjectile
{
    public override void SetStaticDefaults()
    {
        Main.projFrames[Type] = 4;
        ProjectileID.Sets.MinionTargetingFeature[Type] = true;

        Main.projPet[Type] = true;
        ProjectileID.Sets.MinionSacrificable[Type] = true;
        ProjectileID.Sets.CultistIsResistantTo[Type] = true;
    }

    public sealed override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 28;
        Projectile.tileCollide = false;

        Projectile.friendly = true;
        Projectile.minion = true;
        Projectile.DamageType = DamageClass.Summon;
        Projectile.minionSlots = 1f;
        Projectile.penetrate = -1;
    }

    public override bool? CanCutTiles() => false;

    public override bool MinionContactDamage() => true;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];

        if (!CheckActive(owner))
        {
            return;
        }

        GeneralBehavior(owner, out Vector2 vectorToIdlePosition, out float distanceToIdlePosition);
        SearchForTargets(owner, out bool foundTarget, out float distanceFromTarget, out Vector2 targetCenter);
        Movement(foundTarget, distanceFromTarget, targetCenter, distanceToIdlePosition, vectorToIdlePosition);
        Visuals();
    }

    private bool CheckActive(Player owner)
    {
        if (owner.dead || !owner.active)
        {
            owner.ClearBuff(ModContent.BuffType<ExampleSimpleMinionBuff>());
            return false;
        }

        if (owner.HasBuff(ModContent.BuffType<ExampleSimpleMinionBuff>()))
        {
            Projectile.timeLeft = 2;
        }

        return true;
    }

    private void GeneralBehavior(Player owner, out Vector2 vectorToIdlePosition, out float distanceToIdlePosition)
    {
        Vector2 idlePosition = owner.Center;
        idlePosition.Y -= 48f;

        float minionPositionOffsetX = (10 + Projectile.minionPos * 40) * -owner.direction;
        idlePosition.X += minionPositionOffsetX;

        vectorToIdlePosition = idlePosition - Projectile.Center;
        distanceToIdlePosition = vectorToIdlePosition.Length();

        if (Main.myPlayer == owner.whoAmI && distanceToIdlePosition > 2000f)
        {
            Projectile.position = idlePosition;
            Projectile.velocity *= 0.1f;
            Projectile.netUpdate = true;
        }

        float overlapVelocity = 0.04f;

        foreach (var other in Main.ActiveProjectiles)
        {
            if (other.whoAmI != Projectile.whoAmI && other.owner == Projectile.owner && Math.Abs(Projectile.position.X - other.position.X) + Math.Abs(Projectile.position.Y - other.position.Y) < Projectile.width)
            {
                if (Projectile.position.X < other.position.X)
                {
                    Projectile.velocity.X -= overlapVelocity;
                }
                else
                {
                    Projectile.velocity.X += overlapVelocity;
                }

                if (Projectile.position.Y < other.position.Y)
                {
                    Projectile.velocity.Y -= overlapVelocity;
                }
                else
                {
                    Projectile.velocity.Y += overlapVelocity;
                }
            }
        }
    }

    private void SearchForTargets(Player owner, out bool foundTarget, out float distanceFromTarget, out Vector2 targetCenter)
    {
        distanceFromTarget = 700f;
        targetCenter = Projectile.position;
        foundTarget = false;

        if (owner.HasMinionAttackTargetNPC)
        {
            NPC npc = Main.npc[owner.MinionAttackTargetNPC];
            float between = Vector2.Distance(npc.Center, Projectile.Center);

            if (between < 2000f)
            {
                distanceFromTarget = between;
                targetCenter = npc.Center;
                foundTarget = true;
            }
        }

        if (!foundTarget)
        {
            foreach (var npc in Main.ActiveNPCs)
            {
                if (npc.CanBeChasedBy())
                {
                    float between = Vector2.Distance(npc.Center, Projectile.Center);
                    bool closest = Vector2.Distance(Projectile.Center, targetCenter) > between;
                    bool inRange = between < distanceFromTarget;
                    bool lineOfSight = Collision.CanHitLine(Projectile.position, Projectile.width, Projectile.height, npc.position, npc.width, npc.height);
                    bool closeThroughWall = between < 100f;

                    if (((closest && inRange) || !foundTarget) && (lineOfSight || closeThroughWall))
                    {
                        distanceFromTarget = between;
                        targetCenter = npc.Center;
                        foundTarget = true;
                    }
                }
            }
        }

        Projectile.friendly = foundTarget;
    }

    private void Movement(bool foundTarget, float distanceFromTarget, Vector2 targetCenter, float distanceToIdlePosition, Vector2 vectorToIdlePosition)
    {
        float speed = 8f;
        float inertia = 20f;

        if (foundTarget)
        {
            if (distanceFromTarget > 40f)
            {
                Vector2 direction = targetCenter - Projectile.Center;
                direction.Normalize();
                direction *= speed;

                Projectile.velocity = (Projectile.velocity * (inertia - 1) + direction) / inertia;
            }
        }
        else
        {
            if (distanceToIdlePosition > 600f)
            {
                speed = 12f;
                inertia = 60f;
            }
            else
            {
                speed = 4f;
                inertia = 80f;
            }

            if (distanceToIdlePosition > 20f)
            {
                vectorToIdlePosition.Normalize();
                vectorToIdlePosition *= speed;
                Projectile.velocity = (Projectile.velocity * (inertia - 1) + vectorToIdlePosition) / inertia;
            }
            else if (Projectile.velocity == Vector2.Zero)
            {
                Projectile.velocity.X = -0.15f;
                Projectile.velocity.Y = -0.05f;
            }
        }
    }

    private void Visuals()
    {
        Projectile.rotation = Projectile.velocity.X * 0.05f;

        int frameSpeed = 5;

        Projectile.frameCounter++;

        if (Projectile.frameCounter >= frameSpeed)
        {
            Projectile.frameCounter = 0;
            Projectile.frame++;

            if (Projectile.frame >= Main.projFrames[Type])
            {
                Projectile.frame = 0;
            }
        }

        Lighting.AddLight(Projectile.Center, Color.White.ToVector3() * 0.78f);
    }
}

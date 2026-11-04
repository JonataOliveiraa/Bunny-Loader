using ExampleMod.Content.Buffs;
using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Items;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{
    public class ExampleWhipAdvanced : ModItem
    {
        public static readonly int ExampleWhipAdvancedTagDamagePercent = 30;

        public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(ExampleWhipAdvancedTagDamagePercent);

        public override void SetStaticDefaults() {

            ItemID.Sets.UniqueTagEffects[Type] = new WhipTagEffect_ExampleWhipAdvanced() {
                TagDamagePercent = ExampleWhipAdvancedTagDamagePercent,
                ProcDamageMultiplier = 1.75f,

                PlayerBuffId = ModContent.BuffType<ExampleWhipAdvancedPlayerBuff>(),
                PlayerBuffTime = 180
            };
        }

        public override void SetDefaults() {

            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.damage = 20;
            Item.knockBack = 2;
            Item.rare = ItemRarityID.Orange;

            Item.shoot = ModContent.ProjectileType<ExampleWhipProjectileAdvanced>();
            Item.shootSpeed = 4;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.UseSound = SoundID.Item152;
            Item.value = Item.buyPrice(gold: 2);
            Item.channel = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            float swingDirection = 0.6f + (0.4f * Main.rand.NextFloat());

            if (Main.rand.NextBool(3)) {
                swingDirection *= -2.5f;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0f, swingDirection);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }

        public override bool MeleePrefix() {
            return true;
        }
    }

    public class WhipTagEffect_ExampleWhipAdvanced
    {
        private readonly WhipTagEffect effect = new WhipTagEffect();
        public int TagDamagePercent;
        public float TagDamageMultiplier => TagDamagePercent / 100f;
        public float ProcDamageMultiplier;
        public int PlayerBuffId { get => effect.PlayerBuffId; set => effect.PlayerBuffId = value; }
        public int PlayerBuffTime { get => effect.PlayerBuffTime; set => effect.PlayerBuffTime = value; }

        public static implicit operator UniqueTagEffect(WhipTagEffect_ExampleWhipAdvanced value) => value.effect;

        public WhipTagEffect_ExampleWhipAdvanced()
        {
            On_WhipTagEffect.ModifyTaggedHit += (On_WhipTagEffect.orig_ModifyTaggedHit orig, WhipTagEffect self, Player owner, Projectile projectile, NPC target, ref TagDamageChanges changes) =>
            {
                orig(self, owner, projectile, target, ref changes);
                if (self.Pointer != effect.Pointer || projectile.IsNull) return;
                changes.TotalDamageMultiplier += TagDamageMultiplier * ProjectileID.Sets.SummonTagDamageMultiplier[projectile.type];
            };
            On_UniqueTagEffect.OnTaggedHit += (orig, self, owner, projectile, target, damage) =>
            {
                orig(self, owner, projectile, target, damage);
                if (self.Pointer != effect.Pointer) return;
                ParticleOrchestrator.RequestParticleSpawn(false, ParticleOrchestraType.BlackLightningHit, new ParticleOrchestraSettings { PositionInWorld = target.Center }, default);
            };
            On_UniqueTagEffect.OnProcHit += (orig, self, owner, projectile, target, damage) =>
            {
                if (self.Pointer != effect.Pointer || projectile.IsNull) return orig(self, owner, projectile, target, damage);
                CombatText.NewText(projectile.Hitbox, Color.Purple, "BAM!");
                int index = Projectile.NewProjectile(projectile.GetSource_FromThis(), target.Center, Vector2.Zero, ProjectileID.FireWhipProj, (int)(damage * ProcDamageMultiplier), 0f, projectile.owner);
                if ((uint)index < (uint)Main.maxProjectiles) Main.projectile[index].localNPCImmunity[target.whoAmI] = -1;
                return true;
            };
            On_UniqueTagEffect.ModifyProcHit += (On_UniqueTagEffect.orig_ModifyProcHit orig, UniqueTagEffect self, Player owner, Projectile projectile, NPC target, ref TagDamageChanges changes) =>
            {
                orig(self, owner, projectile, target, ref changes);
                if (self.Pointer != effect.Pointer || projectile.IsNull) return;
                changes.TotalDamageMultiplier += ProcDamageMultiplier * ProjectileID.Sets.SummonTagDamageMultiplier[projectile.type];
            };
        }
    }
}

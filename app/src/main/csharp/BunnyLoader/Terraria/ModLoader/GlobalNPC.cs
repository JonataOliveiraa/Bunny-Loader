using Terraria.GameContent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent.Bestiary;
using Terraria.ModLoader.IO;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class GlobalNPC : GlobalType<NPC, GlobalNPC>
{
    internal NPCMethod Hooks;
    protected sealed override void Register() => NPCLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults(NPC entity) { }

    public virtual void SetDefaultsFromNetId(NPC npc) { }

    public virtual void OnSpawn(NPC npc, IEntitySource source) { }

    public virtual void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment) { }

    public virtual void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) { }

    public virtual void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) { }

    public virtual void BossHeadSlot(NPC npc, ref int index) { }

    public virtual void BossHeadRotation(NPC npc, ref float rotation) { }

    public virtual void BossHeadSpriteEffects(NPC npc, ref SpriteEffects spriteEffects) { }

    public virtual Color? GetAlpha(NPC npc, Color drawColor) => null;

    public virtual void DrawEffects(NPC npc, ref Color drawColor) { }

    public virtual bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => true;

    public virtual void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { }

    public virtual void DrawBehind(NPC npc, int index) { }

    public virtual void SetBestiary(NPC npc, BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }

    public virtual void ResetEffects(NPC npc) { }

    public virtual bool PreAI(NPC npc) => true;

    public virtual void AI(NPC npc) { }

    public virtual void PostAI(NPC npc) { }

    public virtual void FindFrame(NPC npc, int frameHeight) { }

    public virtual void HitEffect(NPC npc, NPC.HitInfo hit) { }

    public virtual void UpdateLifeRegen(NPC npc, ref int damage) { }

    public virtual bool CheckActive(NPC npc) => true;

    public virtual bool CheckDead(NPC npc) => true;

    public virtual bool? CanBeHitByItem(NPC npc, Player player, Item item) => null;

    public virtual bool? CanCollideWithPlayerMeleeAttack(NPC npc, Player player, Item item, Microsoft.Xna.Framework.Rectangle meleeAttackHitbox) => null;

    public virtual void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) { }

    public virtual void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) { }

    public virtual bool? CanBeHitByProjectile(NPC npc, Projectile projectile) => null;

    public virtual void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) { }

    public virtual bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => true;

    public virtual void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) { }

    public virtual void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) { }

    public virtual bool CanHitNPC(NPC npc, NPC target) => true;

    public virtual bool CanBeHitByNPC(NPC npc, NPC attacker) => true;

    public virtual void ModifyHitNPC(NPC npc, NPC target, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitNPC(NPC npc, NPC target, NPC.HitInfo hit) { }

    public virtual void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) { }

    public virtual void ModifyGlobalLoot(GlobalLoot globalLoot) { }

    public virtual bool PreKill(NPC npc) => true;

    public virtual void OnKill(NPC npc) { }

    public virtual void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) { }

    public virtual void EditSpawnRange(Player player, ref int spawnRangeX, ref int spawnRangeY, ref int safeRangeX, ref int safeRangeY) { }

    public virtual void EditSpawnFlags(NPC.Spawner spawner) { }

    public virtual void EditSpawnInfo(NPC.Spawner spawner) { }

    public virtual void EditSpawnPool(IDictionary<int, float> pool, NPC.Spawner spawner) => EditSpawnPool(pool, Core.NPCSpawnHooks.InfoOf(spawner));

    public virtual void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) { }

    public virtual void SpawnNPC(int npc, int tileX, int tileY) { }

    public virtual bool? CanChat(NPC npc) => null;

    public virtual void GetChat(NPC npc, ref string chat) { }

    public virtual void ModifyNPCNameList(NPC npc, List<string> nameList) { }

    public virtual void RegisterChatButtons(NPC npc, NPCInteractionList interactions) { }

    public virtual bool PreChatButtonClicked(NPC npc, NPCInteraction interaction) => true;

    public virtual void OnChatButtonClicked(NPC npc, NPCInteraction interaction) { }

    public virtual void ModifyShop(NPCShop shop) { }

    public virtual void ModifyActiveShop(NPC npc, string shopName, Item[] items) { }

    public virtual void SetupTravelShop(int[] shop, ref int nextSlot) { }

    public virtual void TownNPCAttackStrength(NPC npc, ref int damage, ref float knockback) { }

    public virtual void TownNPCAttackCooldown(NPC npc, ref int cooldown, ref int randExtraCooldown) { }

    public virtual void TownNPCAttackProj(NPC npc, ref int projType, ref int attackDelay) { }

    public virtual void TownNPCAttackProjSpeed(NPC npc, ref float multiplier, ref float gravityCorrection, ref float randomOffset) { }

    public virtual void TownNPCAttackShoot(NPC npc, ref bool inBetweenShots) { }

    public virtual void TownNPCAttackMagic(NPC npc, ref float auraLightMultiplier) { }

    public virtual void TownNPCAttackSwing(NPC npc, ref int itemWidth, ref int itemHeight) { }

    public virtual void DrawTownAttackGun(NPC npc, ref Texture2D item, ref Rectangle itemFrame, ref float scale, ref int horizontalHoldoutOffset) { }

    public virtual void DrawTownAttackSwing(NPC npc, ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset) { }
}

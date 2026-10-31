using System.Collections.Generic;
using Terraria.GameContent;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class ModNPC : ModType<NPC, ModNPC>, ILocalizedModType
{
    public ref NPC NPC => ref entity;

    int registeredType, aiType, animationType;

    internal NPCMethod Hooks;
    internal int RegisteredType => registeredType;

    public int Type { get => entity.IsNull ? registeredType : entity.type; internal set => registeredType = value; }

    public int AIType
    {
        get => aiType;
        set { aiType = value; if (value > 0 && Type >= Terraria.ID.NPCID.Count) NPCEventHooks.Want(Type, 1); }
    }

    public int AnimationType
    {
        get => animationType;
        set { animationType = value; if (value > 0 && Type >= Terraria.ID.NPCID.Count) NPCEventHooks.Want(Type, 2); }
    }

    public virtual string LocalizationCategory => "NPCs";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');

    public virtual string BossHeadTexture => Texture + "_Head_Boss";

    protected sealed override NPC CreateTemplateEntity() => default;

    protected sealed override void Register() => NPCLoader.Register(this);

    public sealed override void SetupContent()
    {
        AutoStaticDefaults();
        SetStaticDefaults();
        if (!Terraria.ID.NPCID.Search.ContainsName(FullName)) Terraria.ID.NPCID.Search.Add(FullName, Type);
    }

    public virtual void SetDefaults() { }

    public virtual void OnSpawn(IEntitySource source) { }

    public virtual void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) { }

    public virtual void ResetEffects() { }

    public virtual bool PreAI() => true;

    public virtual void AI() { }

    public virtual void PostAI() { }

    public virtual void FindFrame(int frameHeight) { }

    public virtual void HitEffect(NPC.HitInfo hit) { }

    public virtual void UpdateLifeRegen(ref int damage) { }

    public virtual bool CheckActive() => true;

    public virtual bool CheckDead() => true;

    public virtual bool? CanBeHitByItem(Player player, Item item) => null;

    public virtual bool? CanCollideWithPlayerMeleeAttack(Player player, Item item, Microsoft.Xna.Framework.Rectangle meleeAttackHitbox) => null;

    public virtual void ModifyIncomingHit(ref NPC.HitModifiers modifiers) { }

    public virtual void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) { }

    public virtual bool? CanBeHitByProjectile(Projectile projectile) => null;

    public virtual void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) { }

    public virtual bool CanHitPlayer(Player target, ref int cooldownSlot) => true;

    public virtual void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) { }

    public virtual void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) { }

    public virtual bool CanHitNPC(NPC target) => true;

    public virtual bool CanBeHitByNPC(NPC attacker) => true;

    public virtual void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitNPC(NPC target, NPC.HitInfo hit) { }

    public virtual void SendExtraAI(BinaryWriter writer) { }

    public virtual void ReceiveExtraAI(BinaryReader reader) { }

    public virtual void BossHeadSlot(ref int index) { }

    public virtual void BossHeadRotation(ref float rotation) { }

    public virtual void BossHeadSpriteEffects(ref SpriteEffects spriteEffects) { }

    public virtual Color? GetAlpha(Color drawColor) => null;

    public virtual void DrawEffects(ref Color drawColor) { }

    public virtual bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => true;

    public virtual void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { }

    public virtual void DrawBehind(int index) { }

    public virtual void AutoStaticDefaults() =>
        TextureAssets.Npc[Type] = new Asset<Texture2D>(Texture, ItemLoader.ResolveTexture(this, Texture));

    public virtual void ModifyNPCLoot(NPCLoot npcLoot) { }

    public virtual bool PreKill() => true;

    public virtual void OnKill() { }

    public virtual void BossLoot(ref int potionType) { }

    public virtual void BossLoot(ref int potionType, ref int potionStack, ref int heartStack) => BossLoot(ref potionType);

    public virtual float SpawnChance(NPC.Spawner spawner) => SpawnChance(NPCSpawnHooks.InfoOf(spawner));

    public virtual float SpawnChance(NPCSpawnInfo spawnInfo) => 0f;

    public virtual int SpawnNPC(int tileX, int tileY) => Terraria.NPC.NewNPC(Terraria.NPC.GetSpawnSourceForNaturalSpawn(), tileX * 16 + 8, tileY * 16, Type);

    public virtual string HeadTexture => Texture + "_Head";

    public virtual bool CanTownNPCSpawn(int numTownNPCs) => false;

    public virtual bool CheckConditions(int left, int right, int top, int bottom) => true;

    public virtual List<string> SetNPCNameList() => new();

    public virtual bool CanChat() => NPC.townNPC;

    public virtual string GetChat() => Language.GetTextValue("LegacyDialog.1") ?? "";

    public virtual void RegisterChatButtons(NPCInteractionList interactions) { }

    public virtual void OnChatButtonClicked(NPCInteraction interaction) { }

    public virtual void SetChatButtons(ref string button, ref string button2) { }

    public virtual void OnChatButtonClicked(bool firstButton, ref string shopName) { }

    public virtual void AddShops() { }

    public virtual void ModifyActiveShop(string shopName, Item[] items) { }

    public virtual void TownNPCAttackStrength(ref int damage, ref float knockback) { }

    public virtual void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) { }

    public virtual void TownNPCAttackProj(ref int projType, ref int attackDelay) { }

    public virtual void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset) { }

    public virtual void TownNPCAttackShoot(ref bool inBetweenShots) { }

    public virtual void TownNPCAttackMagic(ref float auraLightMultiplier) { }

    public virtual void TownNPCAttackSwing(ref int itemWidth, ref int itemHeight) { }

    public virtual void DrawTownAttackGun(ref Texture2D item, ref Rectangle itemFrame, ref float scale, ref int horizontalHoldoutOffset) { }

    public virtual void DrawTownAttackSwing(ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset) { }
}

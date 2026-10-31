using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsNpcHit.Content;

public sealed class HitItem : ModItem
{
    public override bool IsLoadingEnabled(Mod mod) => !mod.FileExists("npc-only.flag");
    public override string Texture => "CsNpcHit/Content/HitItem";
    public override void SetDefaults()
    {
        Item.width = Item.height = 24;
        Item.damage = 40;
        Item.DamageType = DamageClass.Melee;
        Item.useStyle = 1;
        Item.useTime = Item.useAnimation = 20;
    }
    public override bool? CanHitNPC(Player player, NPC target) { Checks.Mark("ICan"); return Checks.ItemDecision; }
    public override bool? CanMeleeAttackCollideWithNPC(Rectangle hitbox, Player player, NPC target) { Checks.Mark("ICollide"); return Checks.ItemCollision; }
    public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
    {
        Checks.Mark("ModifyI");
        if (Checks.Arithmetic) modifiers.SourceDamage.Flat += 5;
    }
    public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) { Checks.SourceHits++; Checks.Mark("OnI"); }
}

public sealed class HitGlobalItem : GlobalItem
{
    public override bool IsLoadingEnabled(Mod mod) => !mod.FileExists("npc-only.flag");
    public override bool? CanHitNPC(Item item, Player player, NPC target) { Checks.Mark("ICanG"); return null; }
    public override bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle hitbox, Player player, NPC target) { Checks.Mark("ICollideG"); return null; }
    public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
    {
        Checks.Mark("ModifyIG");
        if (Checks.Arithmetic) modifiers.SourceDamage *= 2;
    }
    public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) => Checks.Mark("OnIG");
}

public sealed class HitPlayer : ModPlayer
{
    public override bool IsLoadingEnabled(Mod mod) => !mod.FileExists("npc-only.flag");
    public override bool CanHitNPC(NPC target) { Checks.Mark("PCan"); return !Checks.PlayerVeto; }
    public override bool? CanHitNPCWithItem(Item item, NPC target) { Checks.Mark("PCanItem"); return Checks.PlayerDecision; }
    public override bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle hitbox, NPC target) { Checks.Mark("PCollide"); return Checks.PlayerCollision; }
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        Checks.Mark("ModifyP");
        if (Checks.Arithmetic) modifiers.SourceDamage.Base += 1;
    }
    public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
    {
        Checks.Mark("ModifyPI");
        if (Checks.Arithmetic) modifiers.FinalDamage *= 0.5f;
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Checks.Mark("OnP");
    public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => Checks.Mark("OnPI");
}

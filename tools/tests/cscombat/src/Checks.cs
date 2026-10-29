using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using CsCombat.Content.Items;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsCombat;

public static class Checks
{
    const int AmmoSlot = 54, SecondAmmoSlot = 55;

    public static bool On, OwnShot, Veto, ForceCollide, BlockShoot, SkipShot, NoAmmo, KeepAmmo, RefuseAmmo;

    public static int HitboxCalls, EffectsCalls, ModifyCalls, OnHitCalls, ModifyShootCalls, ShootCalls, ConsumeCalls, ConsumedCalls, PickCalls, ShotAmmo, OwnIndex = -1;

    public static NPC.HitInfo LastHit;

    public static int LastDone;

    public static float SpeedIn, SpeedOut;

    static int pass, fail, frames;
    static bool done;
    static readonly List<GameHandle> Pins = new();

    static ILog Log => ModContent.GetInstance<CsCombatMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        if (++frames != 120) return;
        done = true;
        try { Stats(p); }
        catch (Exception e) { Report("stats", false, e.ToString()); }
        try { Shooting(p); }
        catch (Exception e) { Report("shooting", false, e.ToString()); }
        try { Hits(p); }
        catch (Exception e) { Report("hits", false, e.ToString()); }
        On = false;
        Log.Info($"cscombat END pass={pass} fail={fail}");
    }

    static Item Make(int type, int stack = 1)
    {
        Item item = Item.New();
        Pins.Add(new GameHandle(item.Pointer));
        item.SetDefaults(type, default);
        if (stack > 1) item.stack = stack;
        return item;
    }

    static void Stats(Player p)
    {
        Item sword = Make(ModContent.ItemType<TestCombatSword>()), copper = Make(ItemID.CopperShortsword);
        var frame = new Rectangle(0, 0, 40, 40);

        On = false;
        int damage0 = p.GetWeaponDamage(sword), copper0 = p.GetWeaponDamage(copper), crit0 = p.GetWeaponCrit(sword);
        float knockback0 = p.GetWeaponKnockback(sword, sword.knockBack), scale0 = p.GetAdjustedItemScale(sword);
        p.ItemCheck_GetMeleeHitbox(sword, frame, out _, out Rectangle box0);

        On = true;
        int damage1 = p.GetWeaponDamage(sword), copper1 = p.GetWeaponDamage(copper), crit1 = p.GetWeaponCrit(sword);
        float knockback1 = p.GetWeaponKnockback(sword, sword.knockBack), scale1 = p.GetAdjustedItemScale(sword);
        p.ItemCheck_GetMeleeHitbox(sword, frame, out _, out Rectangle box1);

        Report("ModifyWeaponDamage adds to the total additive and flat", damage1 == damage0 + 23, $"{damage0} -> {damage1}");
        Report("GlobalItem.ModifyWeaponDamage on a game item", copper1 == (int)(copper0 * 1.5f + 1e-3f), $"{copper0} -> {copper1}");
        Report("ModifyWeaponCrit", crit1 == crit0 + 20, $"{crit0} -> {crit1}");
        Report("ModifyWeaponKnockback", MathF.Abs(knockback1 - 2 * knockback0) < 1e-3f, $"{knockback0} -> {knockback1}");
        Report("ModifyItemScale in GetAdjustedItemScale", MathF.Abs(scale1 - 1.5f * scale0) < 1e-4f, $"{scale0} -> {scale1}");
        Report("ModifyItemScale and UseItemHitbox in the melee hitbox", HitboxCalls == 2 && Math.Abs(box1.Width - box0.Width * 1.5f) <= 2 && Math.Abs(box1.Height - box0.Height * 1.5f) <= 2,
            $"calls={HitboxCalls} {box0.Width}x{box0.Height} -> {box1.Width}x{box1.Height}");

        p.ItemCheck_EmitUseVisuals(sword, box1);
        Report("MeleeEffects", EffectsCalls == 1, $"calls={EffectsCalls}");
    }

    static List<int> Fire(Player p, Item gun)
    {
        var before = new HashSet<int>();
        var all = Main.projectile;
        for (int i = 0; i < 1000; i++) if (all[i].active) before.Add(i);
        p.ItemCheck_Shoot(p.whoAmI, gun, p.GetWeaponDamage(gun), false);
        var spawned = new List<int>();
        for (int i = 0; i < 1000; i++) if (all[i].active && !before.Contains(i)) spawned.Add(i);
        return spawned;
    }

    static void Clear(List<int> spawned)
    {
        foreach (int i in spawned) Main.projectile[i].active = false;
    }

    static void Shooting(Player p)
    {
        On = true;
        Item gun = Make(ModContent.ItemType<TestCombatGun>());
        int ammoType = ModContent.ItemType<TestCombatAmmo>();
        var inventory = p.inventory;
        Item saved = inventory[AmmoSlot], saved2 = inventory[SecondAmmoSlot];
        if (!saved.IsNull) Pins.Add(new GameHandle(saved.Pointer));
        if (!saved2.IsNull) Pins.Add(new GameHandle(saved2.Pointer));
        Item ammo = Make(ammoType, 50), musket = Make(ItemID.MusketBall, 50);
        inventory[AmmoSlot] = ammo;
        inventory[SecondAmmoSlot] = musket;
        try
        {
            List<int> shot = Fire(p, gun);
            Projectile proj = shot.Count == 1 ? Main.projectile[shot[0]] : default;
            bool ok = shot.Count == 1 && proj.type == ProjectileID.Bullet && proj.owner == p.whoAmI && proj.damage >= 1000;
            Report("ModifyShootStats and Shoot around the shot", ok && ModifyShootCalls == 1 && ShootCalls == 1 && ShotAmmo == ammoType && MathF.Abs(SpeedOut - 2 * SpeedIn) < 0.05f,
                $"spawned={shot.Count} type={(proj.IsNull ? -1 : proj.type)} damage={(proj.IsNull ? -1 : proj.damage)} speed={SpeedIn}->{SpeedOut} modify={ModifyShootCalls} shoot={ShootCalls} ammo={ShotAmmo}");
            Report("PickAmmo, OnConsumeAmmo and OnConsumedAsAmmo", PickCalls == 1 && ammo.stack == 49 && ConsumeCalls == 1 && ConsumedCalls == 1,
                $"pick={PickCalls} stack={ammo.stack} consume={ConsumeCalls} consumed={ConsumedCalls}");
            Clear(shot);

            KeepAmmo = true;
            shot = Fire(p, gun);
            KeepAmmo = false;
            Report("CanConsumeAmmo keeps the ammo", shot.Count == 1 && ammo.stack == 49 && ConsumeCalls == 1, $"spawned={shot.Count} stack={ammo.stack} consume={ConsumeCalls}");
            Clear(shot);

            BlockShoot = true;
            int shoots = ShootCalls;
            shot = Fire(p, gun);
            BlockShoot = false;
            Report("CanShoot false stops the shot", shot.Count == 0 && ShootCalls == shoots && ammo.stack == 49, $"spawned={shot.Count} stack={ammo.stack}");
            Clear(shot);

            SkipShot = true;
            shot = Fire(p, gun);
            SkipShot = false;
            Report("Shoot false spawns nothing", shot.Count == 0 && ShootCalls == shoots + 1, $"spawned={shot.Count} shoot={ShootCalls}");
            Clear(shot);

            OwnShot = true;
            int ownShoots = ShootCalls, ownModifies = ModifyShootCalls;
            shot = Fire(p, gun);
            OwnShot = false;
            proj = shot.Count == 1 ? Main.projectile[shot[0]] : default;
            float speed = proj.IsNull ? 0 : MathF.Sqrt(proj.velocity.X * proj.velocity.X + proj.velocity.Y * proj.velocity.Y);
            Report("Shoot makes its own projectile and stops the game's", shot.Count == 1 && shot[0] == OwnIndex && proj.type == ProjectileID.WoodenArrowFriendly && MathF.Abs(speed - SpeedOut) < 0.05f,
                $"spawned={shot.Count} own={OwnIndex} type={(proj.IsNull ? -1 : proj.type)} speed={speed} want={SpeedOut} shoot={ShootCalls - ownShoots} modify={ModifyShootCalls - ownModifies} stack={ammo.stack}");
            Clear(shot);

            Item chosen = p.PickAmmo_PickAmmoItem(gun);
            RefuseAmmo = true;
            Item refused = p.PickAmmo_PickAmmoItem(gun);
            RefuseAmmo = false;
            Report("CanBeChosenAsAmmo false skips the ammo", chosen == ammo && refused == musket, $"chosen={(chosen.IsNull ? -1 : chosen.type)} refused={(refused.IsNull ? -1 : refused.type)}");

            inventory[AmmoSlot] = Make(0);
            inventory[SecondAmmoSlot] = Make(0);
            NoAmmo = true;
            bool has = p.HasAmmo(gun, true);
            shot = Fire(p, gun);
            NoAmmo = false;
            proj = shot.Count == 1 ? Main.projectile[shot[0]] : default;
            Report("NeedsAmmo false shoots without ammo", has && shot.Count == 1 && proj.type == ProjectileID.Bullet,
                $"has={has} spawned={shot.Count} type={(proj.IsNull ? -1 : proj.type)}");
            Clear(shot);
        }
        finally
        {
            inventory[AmmoSlot] = saved;
            inventory[SecondAmmoSlot] = saved2;
        }
    }

    static void Hits(Player p)
    {
        Item sword = Make(ModContent.ItemType<TestCombatSword>());
        int x = (int)(p.position.X + p.width + 8), y = (int)(p.position.Y + p.height);
        int index = NPC.NewNPC(default, x, y, NPCID.BlueSlime);
        if (index < 0 || index >= 200)
        {
            Report("an NPC to hit", false, $"index={index}");
            return;
        }
        NPC npc = Main.npc[index];
        try
        {
            npc.lifeMax = 5000;
            npc.life = 5000;
            npc.defense = 10;
            npc.position = new Vector2(p.position.X + p.width + 8, p.position.Y + p.height - npc.height);
            Rectangle hitbox = npc.Hitbox;

            int life = npc.life;
            p.ProcessHitAgainstNPC(sword, hitbox, 40, 4f, index);
            int taken = life - npc.life;
            Report("ModifyHitNPC and OnHitNPC around the strike", ModifyCalls == 1 && OnHitCalls == 1 && taken == LastDone && LastHit.Damage == LastDone && LastDone >= 101 && !LastHit.Crit,
                $"modify={ModifyCalls} onHit={OnHitCalls} taken={taken} done={LastDone} hit={LastHit.Damage} crit={LastHit.Crit}");

            npc.immune[p.whoAmI] = 0;
            Veto = true;
            life = npc.life;
            p.ProcessHitAgainstNPC(sword, hitbox, 40, 4f, index);
            Veto = false;
            Report("CanHitNPC false stops the hit", npc.life == life && OnHitCalls == 1, $"life {life}->{npc.life} onHit={OnHitCalls}");

            var far = new Rectangle(hitbox.X - 400, hitbox.Y - 400, 10, 10);
            npc.immune[p.whoAmI] = 0;
            life = npc.life;
            p.ProcessHitAgainstNPC(sword, far, 40, 4f, index);
            bool missed = npc.life == life && OnHitCalls == 1;
            npc.immune[p.whoAmI] = 0;
            ForceCollide = true;
            p.ProcessHitAgainstNPC(sword, far, 40, 4f, index);
            ForceCollide = false;
            Report("CanMeleeAttackCollideWithNPC true hits out of reach", missed && npc.life < life && OnHitCalls == 2, $"missed={missed} life {life}->{npc.life} onHit={OnHitCalls}");

            var modifiers = new NPC.HitModifiers();
            modifiers.Defense.Base = 20;
            modifiers.ArmorPenetration += 10;
            modifiers.SourceDamage *= 2;
            int expected = (int)Math.Max(100 * 2 - 10 * 0.5f, 1);
            NPC.HitInfo info = modifiers.ToHitInfo(100, false, 5);
            modifiers.SetMaxDamage(50);
            modifiers.SetCrit();
            Report("HitModifiers does tModLoader's math", info.Damage == expected && info.SourceDamage == 200 && modifiers.GetDamage(100, false) == 50,
                $"damage={info.Damage} want={expected} source={info.SourceDamage} capped={modifiers.GetDamage(100, false)}");
        }
        finally { npc.active = false; }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cscombat {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}

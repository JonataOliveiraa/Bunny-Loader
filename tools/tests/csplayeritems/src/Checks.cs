using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPlayerItems;

public static class Checks
{
    const int AmmoSlot = 54;

    static int pass, fail, frames;
    static bool done;
    static readonly List<GameHandle> Pins = new();

    static ILog Log => ModContent.GetInstance<CsPlayerItemsMod>().Logger;

    static int Calls(string name) => ItemsPlayer.Calls.GetValueOrDefault(name);

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        try
        {
            Report("PostItemCheck runs every frame", Calls(nameof(ItemsPlayer.PostItemCheck)) >= 100, $"calls={Calls(nameof(ItemsPlayer.PostItemCheck))}");
            Stats(p);
            Timing(p);
            Mana(p);
            Heal(p);
            Shooting(p);
            Hits(p);
        }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally
        {
            ItemsPlayer.On = ItemsPlayer.NoShoot = ItemsPlayer.KeepAmmo = ItemsPlayer.Veto = ItemsPlayer.AutoReuse = false;
            foreach (GameHandle pin in Pins) pin.Dispose();
            Pins.Clear();
        }
        Log.Info($"csplayeritems END pass={pass} fail={fail}");
    }

    static Item Make(int type, int stack = 1)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        item.stack = stack;
        Pins.Add(new GameHandle(item.Pointer));
        return item;
    }

    static void Stats(Player p)
    {
        Item sword = Make(ItemID.CopperShortsword);
        ItemsPlayer.On = false;
        int damage0 = p.GetWeaponDamage(sword), crit0 = p.GetWeaponCrit(sword);
        float knockback0 = p.GetWeaponKnockback(sword, sword.knockBack), scale0 = p.GetAdjustedItemScale(sword);
        ItemsPlayer.On = true;
        int damage1 = p.GetWeaponDamage(sword), crit1 = p.GetWeaponCrit(sword);
        float knockback1 = p.GetWeaponKnockback(sword, sword.knockBack), scale1 = p.GetAdjustedItemScale(sword);
        ItemsPlayer.On = false;
        Report("ModifyWeaponDamage adds to the damage", damage1 == damage0 + sword.damage, $"{damage0} -> {damage1} (base {sword.damage})");
        Report("ModifyWeaponCrit adds to the crit", crit1 == crit0 + 10, $"{crit0} -> {crit1}");
        Report("ModifyWeaponKnockback adds to the knockback", MathF.Abs(knockback1 - knockback0 - sword.knockBack) < 1e-3f, $"{knockback0} -> {knockback1} (base {sword.knockBack})");
        Report("ModifyItemScale scales the item", MathF.Abs(scale1 - 1.5f * scale0) < 1e-4f, $"{scale0} -> {scale1}");
        int effects = Calls(nameof(ItemsPlayer.MeleeEffects));
        p.ItemCheck_EmitUseVisuals(sword, new Rectangle((int)p.position.X, (int)p.position.Y, 20, 20));
        Report("MeleeEffects runs", Calls(nameof(ItemsPlayer.MeleeEffects)) == effects + 1, $"calls={Calls(nameof(ItemsPlayer.MeleeEffects)) - effects}");
    }

    static void Timing(Player p)
    {
        Item sword = Make(ItemID.CopperShortsword);
        int time = p.itemTime, timeMax = p.itemTimeMax;
        p.ApplyItemTime(sword);
        int plain = p.itemTime;
        ItemsPlayer.On = true;
        p.ApplyItemTime(sword);
        int doubled = p.itemTime;
        ItemsPlayer.On = false;
        p.itemTime = time;
        p.itemTimeMax = timeMax;
        Report("UseTimeMultiplier scales the item time", doubled == plain * 2, $"{plain} -> {doubled}");

        bool reuse = sword.autoReuse, release = p.releaseUseItem;
        sword.autoReuse = false;
        p.releaseUseItem = false;
        ItemsPlayer.AutoReuse = true;
        p.ItemCheck_AutoReuseLogic(sword);
        bool forced = p.releaseUseItem;
        ItemsPlayer.AutoReuse = false;
        sword.autoReuse = reuse;
        p.releaseUseItem = release;
        Report("CanAutoReuseItem true forces the reuse", forced, $"released={forced}");
    }

    static void Mana(Player p)
    {
        var inventory = p.inventory;
        int slot = p.selectedItem;
        Item held = inventory[slot];
        if (!held.IsNull) Pins.Add(new GameHandle(held.Pointer));
        int mana = p.statMana;
        try
        {
            inventory[slot] = Make(ItemID.WandofSparking);
            float cost = p.manaCost;
            ItemsPlayer.On = true;
            p.statMana = 100;
            bool paid = p.CheckMana(10, true, true);
            int spent = 100 - p.statMana;
            ItemsPlayer.On = false;
            int expected = (int)(10 * cost * 0.5f);
            Report("ModifyManaCost and OnConsumeMana", paid && spent == expected && ItemsPlayer.Consumed == expected && p.manaCost == cost,
                $"paid={paid} spent={spent} expected={expected} consumed={ItemsPlayer.Consumed}");
        }
        finally
        {
            inventory[slot] = held;
            p.statMana = mana;
        }
    }

    static void Heal(Player p)
    {
        Item potion = Make(ItemID.LesserHealingPotion);
        int life = p.statLife, max = p.statLifeMax2;
        p.statLifeMax2 = Math.Max(max, 400);
        p.statLife = 1;
        ItemsPlayer.On = true;
        p.ApplyLifeAndOrMana(potion);
        ItemsPlayer.On = false;
        int healed = p.statLife - 1;
        p.statLife = life;
        p.statLifeMax2 = max;
        Report("GetHealLife adds to the heal", healed == potion.healLife + 20, $"healed={healed} base={potion.healLife}");
    }

    static List<int> Fire(Player p, Item gun)
    {
        var before = new HashSet<int>();
        var all = Main.projectile;
        for (int i = 0; i < 1000; i++) if (all[i].active) before.Add(i);
        p.ItemCheck_Shoot(p.whoAmI, gun, p.GetWeaponDamage(gun), false);
        var spawned = new List<int>();
        for (int i = 0; i < 1000; i++) if (all[i].active && !before.Contains(i)) spawned.Add(i);
        foreach (int i in spawned) all[i].active = false;
        return spawned;
    }

    static void Shooting(Player p)
    {
        Item gun = Make(ItemID.FlintlockPistol);
        var inventory = p.inventory;
        Item saved = inventory[AmmoSlot];
        if (!saved.IsNull) Pins.Add(new GameHandle(saved.Pointer));
        Item ammo = Make(ItemID.MusketBall, 50);
        inventory[AmmoSlot] = ammo;
        try
        {
            ItemsPlayer.NoShoot = true;
            int refused = Fire(p, gun).Count;
            ItemsPlayer.NoShoot = false;
            Report("CanShoot false stops the shot", refused == 0, $"spawned={refused}");

            int modify = Calls(nameof(ItemsPlayer.ModifyShootStats)), shoot = Calls(nameof(ItemsPlayer.Shoot)), consume = Calls(nameof(ItemsPlayer.OnConsumeAmmo));
            int shot = Fire(p, gun).Count;
            Report("ModifyShootStats and Shoot around the shot", shot == 1 && Calls(nameof(ItemsPlayer.ModifyShootStats)) == modify + 1 &&
                Calls(nameof(ItemsPlayer.Shoot)) == shoot + 1 && MathF.Abs(ItemsPlayer.SpeedOut - 2 * ItemsPlayer.SpeedIn) < 0.05f,
                $"spawned={shot} speed={ItemsPlayer.SpeedIn}->{ItemsPlayer.SpeedOut}");
            Report("OnConsumeAmmo runs when the ammo is spent", ammo.stack == 49 && Calls(nameof(ItemsPlayer.OnConsumeAmmo)) == consume + 1,
                $"stack={ammo.stack} calls={Calls(nameof(ItemsPlayer.OnConsumeAmmo)) - consume}");

            ItemsPlayer.KeepAmmo = true;
            Fire(p, gun);
            ItemsPlayer.KeepAmmo = false;
            Report("CanConsumeAmmo false keeps the ammo", ammo.stack == 49, $"stack={ammo.stack}");
        }
        finally { inventory[AmmoSlot] = saved; }
    }

    static void Hits(Player p)
    {
        Item sword = Make(ItemID.CopperShortsword);
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
            npc.position = new Vector2(p.position.X + p.width + 8, p.position.Y + p.height - npc.height);
            Rectangle hitbox = npc.Hitbox;
            int modify = Calls(nameof(ItemsPlayer.ModifyHitNPC)), modifyItem = Calls(nameof(ItemsPlayer.ModifyHitNPCWithItem));
            int onHit = Calls(nameof(ItemsPlayer.OnHitNPC)), onHitItem = Calls(nameof(ItemsPlayer.OnHitNPCWithItem)), anything = Calls(nameof(ItemsPlayer.OnHitAnything));
            int life = npc.life;
            p.ProcessHitAgainstNPC(sword, hitbox, 10, 1f, index);
            Report("ModifyHitNPC, ModifyHitNPCWithItem, OnHitNPC and OnHitNPCWithItem on an item hit", npc.life < life &&
                Calls(nameof(ItemsPlayer.ModifyHitNPC)) == modify + 1 && Calls(nameof(ItemsPlayer.ModifyHitNPCWithItem)) == modifyItem + 1 &&
                Calls(nameof(ItemsPlayer.OnHitNPC)) == onHit + 1 && Calls(nameof(ItemsPlayer.OnHitNPCWithItem)) == onHitItem + 1,
                $"life {life}->{npc.life} modify={Calls(nameof(ItemsPlayer.ModifyHitNPC)) - modify}/{Calls(nameof(ItemsPlayer.ModifyHitNPCWithItem)) - modifyItem} onHit={Calls(nameof(ItemsPlayer.OnHitNPC)) - onHit}/{Calls(nameof(ItemsPlayer.OnHitNPCWithItem)) - onHitItem}");
            Report("OnHitAnything runs on the hit", Calls(nameof(ItemsPlayer.OnHitAnything)) > anything, $"calls={Calls(nameof(ItemsPlayer.OnHitAnything)) - anything}");

            npc.immune[p.whoAmI] = 0;
            ItemsPlayer.Veto = true;
            life = npc.life;
            p.ProcessHitAgainstNPC(sword, hitbox, 10, 1f, index);
            ItemsPlayer.Veto = false;
            Report("CanHitNPC false stops the hit", npc.life == life, $"life {life}->{npc.life}");
        }
        finally { npc.active = false; }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csplayeritems {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}

using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Items;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExMelee;

public sealed class CsExMeleeMod : Mod
{
    const int Start = 200, Window = 170, Hold = 70;

    sealed record Weapon(string Item, string Projectile, float Distance, bool Channel, string Dust);

    static readonly Weapon[] Weapons =
    {
        new("ExampleShortsword", "ExampleShortswordProjectile", 40, false, ""),
        new("ExampleSpear", "ExampleSpearProjectile", 70, false, ""),
        new("ExampleFlail", "ExampleFlailProjectile", 60, true, ""),
        new("ExampleYoyo", "ExampleYoyoProjectile", 110, true, ""),
        new("ExampleWhip", "ExampleWhipProjectile", 90, false, ""),
        new("ExampleSwingingEnergySword", "ExampleSwingingEnergySwordProjectile", 60, false, ""),
        new("ExampleDrill", "ExampleDrillProjectile", 40, true, ""),
        new("ExamplePickaxe", "", 40, false, "ExampleCustomDrawDust"),
        new("ExampleHamaxe", "", 40, false, "Sparkle"),
    };

    internal static readonly Dictionary<int, int> Hits = new();
    internal static readonly HashSet<int> Burning = new();
    static readonly List<int> Emotes = new();

    static int frame, pass, fail, dummy = -1, peak, peakDust, pressed;
    static bool use, whipChecked;
    static Vector2 aim;

    static ILog Log => ModContent.GetInstance<CsExMeleeMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
        On_EmoteBubble.NewBubble += NewBubble;
    }

    static int NewBubble(On_EmoteBubble.orig_NewBubble orig, int emoticon, WorldUIAnchor bubbleAnchor, int time)
    {
        Emotes.Add(emoticon);
        return orig(emoticon, bubbleAnchor, time);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexmelee {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static int ProjectileType(string name) => ModContent.Find<ModProjectile>("ExampleMod/" + name).Type;

    static int Owned(int type)
    {
        int count = 0;
        for (int k = 0; k < Main.maxProjectiles; k++)
        {
            Projectile p = Main.projectile[k];
            if (p.active && p.owner == Main.myPlayer && p.type == type) count++;
        }
        return count;
    }

    static int Find(int type)
    {
        for (int k = 0; k < Main.maxProjectiles; k++)
        {
            Projectile p = Main.projectile[k];
            if (p.active && p.owner == Main.myPlayer && p.type == type) return k;
        }
        return -1;
    }

    static int Dusts(int type)
    {
        int count = 0;
        for (int k = 0; k < Main.maxDust; k++)
            if (Main.dust[k].active && Main.dust[k].type == type) count++;
        return count;
    }

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (!Main.gameMenu && self.whoAmI == Main.myPlayer && frame >= Start)
        {
            self.controlUseItem = use;
            self.releaseUseItem = !use || frame == pressed;
            if (aim != Vector2.Zero)
            {
                Main.mouseX = (int)(aim.X - Main.screenPosition.X);
                Main.mouseY = (int)(aim.Y - Main.screenPosition.Y);
            }
        }
        orig(self);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        orig(self, i);
        if (i != Main.myPlayer) return;
        int end = Start + Weapons.Length * Window;
        if (frame > end) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == end) Log.Info($"csexmelee END pass={pass} fail={fail}");
    }

    static void Step(Player player)
    {
        if (frame == 30)
        {
            Main.dayTime = true;
            Main.time = 27000;
            Statics(player);
            return;
        }
        if (frame < Start) return;
        int index = (frame - Start) / Window, t = (frame - Start) % Window;
        if (index >= Weapons.Length) return;
        Weapon w = Weapons[index];
        int projectile = w.Projectile.Length > 0 ? ProjectileType(w.Projectile) : -1;
        int dust = w.Dust.Length > 0 ? ModContent.Find<ModDust>("ExampleMod/" + w.Dust).Type : -1;

        if (t == 0)
        {
            player.inventory[player.selectedItem].SetDefaults(ItemType(w.Item), default);
            player.direction = 1;
            Hits.Clear();
            Burning.Clear();
            peak = peakDust = 0;
            whipChecked = false;
            KeepDummy(player, w.Distance);
            aim = Main.npc[dummy].Center;
        }
        KeepDummy(player, w.Distance);
        if (t == 5)
        {
            use = true;
            pressed = frame;
        }
        if (t == 5 + Hold) use = false;
        if (projectile >= 0) peak = Math.Max(peak, Owned(projectile));
        if (dust >= 0) peakDust = Math.Max(peakDust, Dusts(dust));
        if (t == 5 + Hold / 2) Log.Info($"csexmelee SHOT {w.Item}");
        if (w.Item == "ExampleWhip" && !whipChecked && projectile >= 0 && Find(projectile) is int whip and >= 0) WhipSettings(Main.projectile[whip]);

        if (t == Window - 1)
        {
            int hits = projectile >= 0 && Hits.TryGetValue(projectile, out int h) ? h : 0;
            if (projectile >= 0)
            {
                Report($"{w.Item} puts out its projectile", peak > 0, $"peak={peak}");
                Report($"{w.Item} hits the dummy", hits > 0, $"hits={hits}");
                Report($"{w.Item} projectile is gone after letting go", Owned(projectile) == 0, $"left={Owned(projectile)}");
            }
            if (w.Item == "ExampleFlail") Report("the flail sets the dummy on fire", Burning.Count > 0, $"burning={Burning.Count}");
            if (dust >= 0) Report($"{w.Item} swings throw {w.Dust} dust", peakDust > 0, $"peak={peakDust}");
            aim = Vector2.Zero;
        }
    }

    static void KeepDummy(Player player, float distance)
    {
        Vector2 at = player.Center + new Vector2(distance, -8);
        if (dummy < 0 || !Main.npc[dummy].active || Main.npc[dummy].type != NPCID.BlueSlime)
        {
            dummy = NPC.NewNPC(player.GetSource_FromThis(), (int)at.X, (int)at.Y + 24, NPCID.BlueSlime);
            Main.npc[dummy].lifeMax = 100000;
            Main.npc[dummy].damage = 0;
        }
        NPC n = Main.npc[dummy];
        n.Center = at;
        n.velocity = Vector2.Zero;
        n.life = n.lifeMax;
    }

    static void WhipSettings(Projectile whip)
    {
        whipChecked = true;
        int segments = whip.WhipSettings.Segments;
        var points = new List<Vector2>();
        Projectile.FillWhipControlPoints(whip, points, Main.player[whip.owner]);
        Report("a mod whip reads the vanilla whip settings", segments == 20 && points.Count == segments + 1, $"segments={segments} points={points.Count}");
        whip.WhipSettings.Segments = 24;
        Projectile.GetWhipSettings(whip, out _, out int changed, out _);
        points.Clear();
        Projectile.FillWhipControlPoints(whip, points, Main.player[whip.owner]);
        Report("WhipSettings changes the game's whip settings", changed == 24 && points.Count == 25, $"segments={changed} points={points.Count}");
        whip.WhipSettings.Segments = 20;
    }

    static void Statics(Player player)
    {
        Report("the Example Mod is loaded", ModLoader.TryGetMod("ExampleMod", out _), "");
        var missing = new List<string>();
        foreach (Weapon w in Weapons)
        {
            if (!ModContent.TryFind<ModItem>("ExampleMod/" + w.Item, out _)) missing.Add(w.Item);
            if (w.Projectile.Length > 0 && !ModContent.TryFind<ModProjectile>("ExampleMod/" + w.Projectile, out _)) missing.Add(w.Projectile);
        }
        foreach (string r in new[] { "ExampleModRarity", "ExampleHigherTierModRarity" })
            if (!ModContent.TryFind<ModRarity>("ExampleMod/" + r, out _)) missing.Add(r);
        if (!ModContent.TryFind<ModDust>("ExampleMod/ExampleCustomDrawDust", out _)) missing.Add("ExampleCustomDrawDust");
        Report("every block 1 item, projectile, rarity and dust is registered", missing.Count == 0, string.Join(",", missing));

        Item item = new Item(ItemType("ExampleShortsword"));
        Report("the shortsword is a rapier that shoots its projectile", item.useStyle == ItemUseStyleID.Rapier && item.shoot == ProjectileType("ExampleShortswordProjectile") && item.noMelee, $"style={item.useStyle} shoot={item.shoot}");
        item = new Item(ItemType("ExampleSpear"));
        Report("the spear is in ItemID.Sets.Spears and skips the first use sound", ItemID.Sets.Spears[item.type] && ItemID.Sets.SkipsInitialUseSound[item.type] && ItemID.Sets.Spears[ItemID.Spear], "");
        item = new Item(ItemType("ExampleYoyo"));
        int rarity = ModContent.Find<ModRarity>("ExampleMod/ExampleModRarity").Type;
        int higher = ModContent.Find<ModRarity>("ExampleMod/ExampleHigherTierModRarity").Type;
        Report("the yoyo is a yoyo with the Example rarity", ItemID.Sets.Yoyo[item.type] && item.rare == rarity && item.channel, $"rare={item.rare} expected={rarity}");
        Report("the Example rarity moves up a tier with a good prefix", ModContent.Find<ModRarity>("ExampleMod/ExampleModRarity").GetPrefixedRarity(1, 1.2f) == higher, $"higher={higher}");
        Report("the yoyo refuses bad prefixes", !item.ModItem!.AllowPrefix(PrefixID.Broken) && item.ModItem.AllowPrefix(PrefixID.Godly), "");
        item = new Item(ItemType("ExampleWhip"));
        UniqueTagEffect tag = ItemID.Sets.UniqueTagEffects[item.type];
        string tooltip = item.ModItem!.Tooltip.Value ?? "";
        Report("the whip has its tag effect and tag damage tooltip", !tag.IsNull && new WhipTagEffect(tag.Pointer).TagDamage == 5 && tooltip.Contains('5'), $"tooltip='{tooltip.Replace('\n', '|')}'");
        item = new Item(ItemType("ExampleDrill"));
        Report("the drill is a drill with 190 pickaxe power", ItemID.Sets.IsDrill[item.type] && item.pick == 190 && item.tileBoost == 10, $"pick={item.pick}");
        item = new Item(ItemType("ExamplePickaxe"));
        Item hamaxe = new Item(ItemType("ExampleHamaxe"));
        Report("pickaxe and hamaxe powers, attack speed only on the animation", item.pick == 220 && hamaxe.axe == 30 && hamaxe.hammer == 100 && item.attackSpeedOnlyAffectsWeaponAnimation && hamaxe.attackSpeedOnlyAffectsWeaponAnimation, $"pick={item.pick} axe={hamaxe.axe} hammer={hamaxe.hammer}");
        item = new Item(ItemType("ExampleFlail"));
        Report("the flail tooltip doubles its damage", ItemID.Sets.ToolTipDamageMultiplier[item.type] == 2f && item.channel, "");

        float point = 0f;
        bool hit = Collision.CheckAABBvLineCollision(new Vector2(100, -8), new Vector2(16, 16), Vector2.Zero, new Vector2(200, 0), 4f, ref point);
        float missPoint = 0f;
        bool miss = Collision.CheckAABBvLineCollision(new Vector2(100, 50), new Vector2(16, 16), Vector2.Zero, new Vector2(200, 0), 4f, ref missPoint);
        Report("CheckAABBvLineCollision with a line width", hit && Math.Abs(point - 100f) < 0.5f && !miss, $"hit={hit} point={point} miss={miss}");

        int emote = ModContent.Find<ModEmoteBubble>("ExampleMod/ExamplePickaxeEmote").Type;
        Emotes.Clear();
        EmoteBubble.MakePlayerEmote(player, emote);
        Report("MakePlayerEmote shows the emote over the player", Emotes.Contains(emote), $"emotes={string.Join(",", Emotes)}");
    }
}

public sealed class HitCounter : GlobalNPC
{
    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        CsExMeleeMod.Hits[projectile.type] = CsExMeleeMod.Hits.GetValueOrDefault(projectile.type) + 1;
    }

    public override void PostAI(NPC npc)
    {
        if (npc.type == NPCID.BlueSlime && npc.onFire) CsExMeleeMod.Burning.Add(npc.whoAmI);
    }
}

using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsPvp;

public sealed class CsPvpMod : Mod
{
    static int pass, fail, frames, client = -1, readyAt = -1, hits;
    static bool done;

    public override void Load() => On_Player.Update += Update;

    static CsPvpMod Self => ModContent.GetInstance<CsPvpMod>();

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (done || i != Main.myPlayer || Main.gameMenu || Main.netMode == 0) return;
        frames++;
        if (frames == 60)
        {
            self.hostile = true;
            NetMessage.SendData(30, -1, -1, default(NetworkText), Main.myPlayer, 0, 0, 0, 0, 0, 0);
        }
        if (Main.netMode == 1) Client(self);
        else Host(self);
    }

    static void Client(Player p)
    {
        p.creativeGodMode = false;
        p.immune = false;
        p.immuneTime = 0;
        p.statLife = p.statLifeMax2;
        if (PvpBlade.OnHitCalls == 0)
        {
            if (frames > 60 * 60 * 3)
            {
                Report("the host's hit arrives", false, "none in 3 minutes");
                Finish("client");
            }
            return;
        }
        Report("ModifyHitPvp runs on the victim with the attacker's item", PvpBlade.ModifyCalls >= 1 && PvpBlade.LastInfo.PvP,
            $"modify={PvpBlade.ModifyCalls} pvp={PvpBlade.LastInfo.PvP}");
        Report("the item's NetSend data arrives with the hit", PvpBlade.LastMark == 99, $"mark={PvpBlade.LastMark}");
        Report("OnHitPvp sees the doubled source damage", PvpBlade.LastInfo.SourceDamage >= 2 && PvpBlade.LastInfo.SourceDamage % 2 == 0 && PvpBlade.LastInfo.Damage >= 1,
            $"source={PvpBlade.LastInfo.SourceDamage} damage={PvpBlade.LastInfo.Damage}");
        Finish("client");
    }

    static void Host(Player p)
    {
        if (client < 0)
        {
            for (int k = 0; k < 255; k++)
            {
                Player other = Main.player[k];
                if (k != Main.myPlayer && !other.IsNull && other.active) client = k;
            }
            if (client < 0 && frames > 60 * 60 * 3)
            {
                Report("a client joined", false, "none in 3 minutes");
                Finish("host");
            }
            return;
        }
        Player target = Main.player[client];
        if (readyAt < 0)
        {
            if (target.hostile && p.hostile) readyAt = frames;
            return;
        }
        int since = frames - readyAt;
        if (since > 120 && since % 120 == 0 && hits < 10)
        {
            hits++;
            Item again = Item.New();
            again.SetDefaults(ModContent.ItemType<PvpBlade>(), default);
            ((PvpBlade)again.ModItem!).Mark = 99;
            target.immune = false;
            target.immuneTime = 0;
            Swing(p, again, target);
            if (hits == 10) Finish("host");
            return;
        }
        if (since != 120) return;
        Item blade = Item.New();
        blade.SetDefaults(ModContent.ItemType<PvpBlade>(), default);
        ((PvpBlade)blade.ModItem!).Mark = 99;
        target.immune = false;
        target.immuneTime = 0;
        int life = target.statLife, calls = PvpBlade.CanHitCalls;
        PvpBlade.Veto = true;
        Swing(p, blade, target);
        PvpBlade.Veto = false;
        Report("CanHitPvp false stops the melee hit", PvpBlade.CanHitCalls == calls + 1 && target.statLife == life,
            $"calls={PvpBlade.CanHitCalls - calls} life {life}->{target.statLife}");
        int players = PvpPlayer.CanHitCalls;
        Swing(p, blade, target);
        Report("the melee hit lands with CanHitPvp true", target.statLife < life && PvpPlayer.CanHitCalls > players,
            $"life {life}->{target.statLife} player calls={PvpPlayer.CanHitCalls - players}");
        Self.Logger.Info($"cspvp host checks done pass={pass} fail={fail}; hitting again for the client");
    }

    static void Swing(Player p, Item blade, Player target)
    {
        var inventory = p.inventory;
        int slot = p.selectedItem;
        Item held = inventory[slot];
        using var pin = held.IsNull ? null : new BunnyLoader.Interop.GameHandle(held.Pointer);
        inventory[slot] = blade;
        try { p.ItemCheck_MeleeHitPVP(blade, target.Hitbox, 20, 1f); }
        finally { inventory[slot] = held; }
    }

    static void Finish(string side)
    {
        done = true;
        Self.Logger.Info($"cspvp {side} END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Self.Logger.Info($"cspvp {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}

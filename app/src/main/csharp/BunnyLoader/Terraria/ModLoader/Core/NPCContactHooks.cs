using System;

namespace Terraria.ModLoader.Core;

internal static class NPCContactHooks
{
    struct Contact
    {
        public NPC Attacker, Victim;
        public bool Struck;
    }

    [ThreadStatic] static Contact contact;
    [ThreadStatic] static bool active;

    internal static void Install(NPCMethod methods)
    {
        if ((methods & NPCLoader.PlayerContactMethods) != 0) PlayerHurtHooks.Want();
        if ((methods & NPCLoader.NPCContactMethods) != 0)
            ItemHooks.Once("NPCContact", () =>
            {
                On_NPC.BeHurtByOtherNPC += BeHurtByOtherNPC;
                On_NPC.StrikeNPC += StrikeNPC;
            });
    }

    static void BeHurtByOtherNPC(On_NPC.orig_BeHurtByOtherNPC orig, NPC self, int npcIndex, NPC thatNPC)
    {
        if (!NPCLoader.CanHitNPC(thatNPC, self)) return;
        Contact outer = contact;
        bool outerActive = active;
        contact = new Contact { Attacker = thatNPC, Victim = self };
        active = true;
        try { orig(self, npcIndex, thatNPC); }
        finally
        {
            contact = outer;
            active = outerActive;
        }
    }

    static int StrikeNPC(On_NPC.orig_StrikeNPC orig, NPC self, int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)
    {
        if (fromNet || !active || contact.Struck || contact.Victim != self) return orig(self, Damage, knockBack, hitDirection, crit, fromNet, owner);
        contact.Struck = true;
        NPC attacker = contact.Attacker;
        NPC.HitInfo hit;
        try
        {
            var modifiers = new NPC.HitModifiers { DamageType = DamageClass.Default, HitDirection = hitDirection };
            modifiers.Defense.Base = self.defense;
            NPCLoader.ModifyIncomingHit(self, ref modifiers);
            NPCLoader.ModifyHitNPC(attacker, self, ref modifiers);
            hit = modifiers.ToHitInfo(Damage, crit, knockBack);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModifyHitNPC (NPC contact)", e);
            return orig(self, Damage, knockBack, hitDirection, crit, fromNet, owner);
        }
        int raw = ItemHitHooks.RawDamage(self, hit);
        bool hide = hit.HideCombatText;
        if (hide) ItemHitHooks.HideText();
        int done;
        using (var context = new NPCEventHooks.HitContext(self, hit))
        {
            try { done = orig(self, raw, hit.Knockback, hit.HitDirection, hit.Crit && !hit.InstantKill, false, owner); }
            finally
            {
                if (hide) ItemHitHooks.ShowText();
            }
        }
        if (done <= 0) return done;
        if (!hit.InstantKill) hit.Damage = done;
        NPCLoader.OnHitNPC(attacker, self, hit);
        return done;
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Chat;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

[Flags]
internal enum NPCHook : ulong
{
    CanFallThroughPlatforms = 1, ModifyCollisionData = 2, SpecialOnKill = 4, ModifyDeathMessage = 8, ModifyTypeName = 16,
    CanGoToStatue = 32, OnGoToStatue = 64, CanBeCaughtBy = 128, OnCaughtBy = 256, UsesPartyHat = 512, BuffTownNPC = 1024,
    DrawHealthBar = 2048, PartyHatPosition = 4096, ChatBubblePosition = 8192, ModifyHoverBoundingBox = 16384, PreHoverInteract = 32768,
    SaveData = 65536, LoadData = 131072, NeedSaving = 262144, TownNPCProfile = 524288, ModifyTownNPCProfile = 1048576,
}

internal static unsafe class NPCExtraHooks
{
    const NPCHook Statue = NPCHook.CanGoToStatue | NPCHook.OnGoToStatue;
    const NPCHook Catch = NPCHook.CanBeCaughtBy | NPCHook.OnCaughtBy;
    const int StatueTile = 105, KingStatue = 40, QueenStatue = 41;

    static readonly int[] KingStatueTypes = { 17, 19, 22, 38, 54, 107, 108, 142, 160, 207, 209, 227, 228, 229, 368, 369, 550, 441, 588 };
    static readonly int[] QueenStatueTypes = { 18, 20, 124, 178, 208, 353, 633, 663 };
    static readonly Color TownDeathColor = new(255, 25, 25), BossDeathColor = new(175, 75, 255);

    [ThreadStatic] static NPC dying;
    [ThreadStatic] static bool deathMessageDone;
    [ThreadStatic] static bool drawingHealthBars;
    [ThreadStatic] static int healthBarCursor;
    static readonly float[] townDamage = new float[200];

    internal static NPCHook Install(Type type, Type baseType)
    {
        NPCHook flags = 0;
        foreach (NPCHook hook in Enum.GetValues<NPCHook>())
            if (ItemHooks.Overrides(type, baseType, hook.ToString())) flags |= hook;
        if (type.GetProperty(nameof(ModNPC.DeathMessage)) is { } deathMessage && deathMessage.DeclaringType != baseType && deathMessage.DeclaringType != typeof(ModNPC))
            flags |= NPCHook.ModifyDeathMessage;
        if ((flags & NPCHook.CanFallThroughPlatforms) != 0)
            ItemHooks.Once("NPCFallThroughPlatforms", () => On_NPC.Collision_DecideFallThroughPlatforms += FallThroughPlatforms);
        if ((flags & NPCHook.ModifyCollisionData) != 0) ItemHooks.Once("NPCCollisionData", () => On_NPC.GetMeleeCollisionData += CollisionData);
        if ((flags & NPCHook.SpecialOnKill) != 0)
            ItemHooks.Once("NPCSpecialOnKill", () =>
            {
                On_NPC.NPCLoot += NPCLoot;
                On_NPC.DropEoWLoot += DropEoWLoot;
            });
        if ((flags & NPCHook.ModifyDeathMessage) != 0)
            ItemHooks.Once("NPCDeathMessage", () =>
            {
                On_NPC.checkDead += CheckDead;
                On_NPC.DoDeathEvents_CelebrateBossDeath += CelebrateBossDeath;
                On_Main.NewText_string_Color_bool += NewTextColor;
                On_Main.NewText_string_byte_byte_byte_bool += NewTextBytes;
                On_ChatHelper.BroadcastChatMessage += Broadcast;
            });
        if ((flags & NPCHook.ModifyTypeName) != 0) ItemHooks.Once("NPCTypeName", () => On_NPC.get_TypeName += TypeName);
        if ((flags & Statue) != 0) ItemHooks.Once("NPCStatue", () => On_Wiring.HitWireSingle += HitWireSingle);
        if ((flags & Catch) != 0) ItemHooks.Once("NPCCatch", () => On_Player.ItemCheck_CatchCritters += CatchCritters);
        if ((flags & NPCHook.UsesPartyHat) != 0) ItemHooks.Once("NPCPartyHat", () => On_NPC.UsesPartyHat += UsesPartyHat);
        if ((flags & NPCHook.BuffTownNPC) != 0)
            ItemHooks.Once("NPCBuffTown", () =>
            {
                On_NPC.AI_007_TownEntities += TownEntities;
                TownAttackHooks.InstallAll();
            });
        if ((flags & NPCHook.DrawHealthBar) != 0)
            ItemHooks.Once("NPCHealthBar", () =>
            {
                On_Main.DrawInterface_14_EntityHealthBars += HealthBars;
                On_Main.DrawHealthBar += DrawHealthBar;
            });
        if ((flags & (NPCHook.SaveData | NPCHook.LoadData | NPCHook.NeedSaving)) != 0)
            ItemHooks.Once("NPCData", () => Host.Native()->OnNpcData(&NeedsData, &SaveData, &LoadData));
        NPCUIHooks.Install(flags);
        return flags;
    }

    [UnmanagedCallersOnly]
    static int NeedsData(IntPtr npc)
    {
        try { return NPCLoader.NeedSaving(new NPC(npc)) ? 1 : 0; }
        catch (Exception e)
        {
            ItemHooks.Report("NPC NeedSaving", e);
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    static int SaveData(IntPtr npc, IntPtr sink)
    {
        try
        {
            byte* utf8 = Host.Utf8(NPCLoader.SaveText(new NPC(npc)));
            try { Host.Native()->WriteString(sink, utf8); }
            finally { Host.FreeUtf8(utf8); }
            return 1;
        }
        catch (Exception e)
        {
            ItemHooks.Report("NPC SaveData", e);
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    static void LoadData(IntPtr npc, byte* data)
    {
        try { NPCLoader.LoadText(new NPC(npc), Marshal.PtrToStringUTF8((IntPtr)data) ?? ""); }
        catch (Exception e) { ItemHooks.Report("NPC LoadData", e); }
    }

    static bool FallThroughPlatforms(On_NPC.orig_Collision_DecideFallThroughPlatforms orig, NPC self) =>
        NPCLoader.HasHooks(self, NPCHook.CanFallThroughPlatforms) && NPCLoader.CanFallThroughPlatforms(self) is bool result ? result : orig(self);

    static void CollisionData(On_NPC.orig_GetMeleeCollisionData orig, ref Rectangle victimHitbox, int enemyIndex, ref int specialHitSetter,
        ref float damageMultiplier, ref Rectangle npcRect)
    {
        if ((uint)enemyIndex < 200u && NPCLoader.HasHooks(Main.npc[enemyIndex], NPCHook.ModifyCollisionData) &&
            !NPCLoader.ModifyCollisionData(Main.npc[enemyIndex], victimHitbox, ref specialHitSetter, ref damageMultiplier, ref npcRect))
            return;
        orig(ref victimHitbox, enemyIndex, ref specialHitSetter, ref damageMultiplier, ref npcRect);
    }

    static void NPCLoot(On_NPC.orig_NPCLoot orig, NPC self)
    {
        if (NPCLoader.HasHooks(self, NPCHook.SpecialOnKill) && NPCLoader.SpecialOnKill(self)) return;
        orig(self);
    }

    static void DropEoWLoot(On_NPC.orig_DropEoWLoot orig, NPC self, bool fromCheckDead)
    {
        if (NPCLoader.HasHooks(self, NPCHook.SpecialOnKill) && NPCLoader.SpecialOnKill(self)) return;
        orig(self, fromCheckDead);
    }

    static bool WantsDeathMessage(NPC npc) => NPCLoader.HasHooks(npc, NPCHook.ModifyDeathMessage) || NPCLoader.AnyGlobal(NPCHook.ModifyDeathMessage);

    static void CheckDead(On_NPC.orig_checkDead orig, NPC self)
    {
        if (!WantsDeathMessage(self))
        {
            orig(self);
            return;
        }
        NPC outer = dying;
        bool outerDone = deathMessageDone;
        dying = self;
        deathMessageDone = false;
        try { orig(self); }
        finally
        {
            dying = outer;
            deathMessageDone = outerDone;
        }
    }

    static void CelebrateBossDeath(On_NPC.orig_DoDeathEvents_CelebrateBossDeath orig, NPC self)
    {
        if (!WantsDeathMessage(self))
        {
            orig(self);
            return;
        }
        NPC outer = dying;
        bool outerDone = deathMessageDone;
        dying = self;
        deathMessageDone = false;
        try { orig(self); }
        finally
        {
            dying = outer;
            deathMessageDone = outerDone;
        }
    }

    static bool DeathMessage(ref NetworkText text, ref Color color)
    {
        if (dying.IsNull || deathMessageDone || !(color.Equals(TownDeathColor) || color.Equals(BossDeathColor))) return true;
        deathMessageDone = true;
        NetworkText custom = default;
        ModNPC? own = NPCLoader.InstanceOf(dying);
        if (own != null && !own.DeathMessage.IsNull) custom = NetworkText.FromLiteral(own.DeathMessage.Format(dying.GetFullNetName().ToString()));
        if (NPCLoader.ModifyDeathMessage(dying, ref custom, ref color)) return false;
        if (!custom.IsNull) text = custom;
        return true;
    }

    static void NewTextColor(On_Main.orig_NewText_string_Color_bool orig, string? newText, Color color, bool onlyCurrentPlayer)
    {
        NetworkText text = default;
        if (!dying.IsNull && !DeathMessage(ref text, ref color)) return;
        orig(text.IsNull ? newText : text.ToString(), color, onlyCurrentPlayer);
    }

    static void NewTextBytes(On_Main.orig_NewText_string_byte_byte_byte_bool orig, string? newText, byte R, byte G, byte B, bool onlyCurrentPlayer)
    {
        if (dying.IsNull)
        {
            orig(newText, R, G, B, onlyCurrentPlayer);
            return;
        }
        NetworkText text = default;
        var color = new Color(R, G, B);
        if (!DeathMessage(ref text, ref color)) return;
        orig(text.IsNull ? newText : text.ToString(), color.R, color.G, color.B, onlyCurrentPlayer);
    }

    static void Broadcast(On_ChatHelper.orig_BroadcastChatMessage orig, NetworkText text, Color color, int excludedPlayer)
    {
        if (!dying.IsNull && !DeathMessage(ref text, ref color)) return;
        orig(text, color, excludedPlayer);
    }

    static string? TypeName(On_NPC.orig_get_TypeName orig, NPC self)
    {
        string? name = orig(self);
        return NPCLoader.HasHooks(self, NPCHook.ModifyTypeName) ? NPCLoader.ModifyTypeName(self, name ?? "") : name;
    }

    static void HitWireSingle(On_Wiring.orig_HitWireSingle orig, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (!tile.HasTile || tile.TileType != StatueTile)
        {
            orig(i, j);
            return;
        }
        int left = i - tile.frameX % 36 / 18, top = j - tile.frameY % 54 / 18;
        int style = tile.frameX / 36 + tile.frameY / 54 % 3 * 55;
        if (style != KingStatue && style != QueenStatue)
        {
            orig(i, j);
            return;
        }
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
                Wiring.SkipWire(left + x, top + y);
        if (!Wiring.CheckMech(left, top, 300)) return;
        bool king = style == KingStatue;
        int[] vanilla = king ? KingStatueTypes : QueenStatueTypes;
        var candidates = new List<int>();
        for (int n = 0; n < 200; n++)
        {
            NPC npc = Main.npc[n];
            if (!npc.active) continue;
            bool? decision = NPCLoader.HasHooks(npc, NPCHook.CanGoToStatue) ? NPCLoader.CanGoToStatue(npc, king) : null;
            if (decision ?? Array.IndexOf(vanilla, npc.type) >= 0) candidates.Add(n);
        }
        if (candidates.Count == 0) return;
        NPC chosen = Main.npc[candidates[Main.rand.Next(candidates.Count)]];
        int x0 = left * 16 + 16, y0 = (top + 3) * 16;
        chosen.Teleport(new Vector2(x0 - chosen.width / 2, y0 - chosen.height - 1), 14);
        if (NPCLoader.HasHooks(chosen, NPCHook.OnGoToStatue)) NPCLoader.OnGoToStatue(chosen, king);
    }

    static void CatchCritters(On_Player.orig_ItemCheck_CatchCritters orig, Player self, Item sItem, Rectangle itemRectangle)
    {
        bool lavaProof = sItem.type == 3183 || sItem.type == 4821;
        for (int n = 0; n < 200; n++)
        {
            NPC npc = Main.npc[n];
            if (!npc.active || !itemRectangle.Intersects(new Rectangle((int)npc.position.X, (int)npc.position.Y, npc.width, npc.height))) continue;
            bool hooked = NPCLoader.HasHooks(npc, Catch);
            bool? decision = hooked ? NPCLoader.CanBeCaughtBy(npc, sItem, self) : null;
            if (decision == false) continue;
            if (decision == true)
            {
                NPC.CatchNPC(n, self.whoAmI);
                if (hooked) NPCLoader.OnCaughtBy(npc, self, sItem, false);
                continue;
            }
            if (npc.catchItem <= 0) continue;
            if (!lavaProof && Terraria.ID.ItemID.Sets.IsLavaBait[npc.catchItem])
            {
                if (Main.myPlayer == self.whoAmI && self.Hurt(Terraria.DataStructures.PlayerDeathReason.ByNPC(n), 1, npc.Center.X < self.Center.X ? 1 : -1,
                        cooldownCounter: Terraria.ID.ImmunityCooldownID.WrongBugNet) > 0.0 && !self.dead)
                    self.AddBuff(24, 300);
                if (hooked) NPCLoader.OnCaughtBy(npc, self, sItem, true);
                continue;
            }
            if ((npc.type == 583 || npc.type == 584 || npc.type == 585) && npc.ai[2] > 1f) continue;
            NPC.CatchNPC(n, self.whoAmI);
            if (hooked) NPCLoader.OnCaughtBy(npc, self, sItem, false);
        }
    }

    static bool UsesPartyHat(On_NPC.orig_UsesPartyHat orig, NPC self) =>
        (!NPCLoader.HasHooks(self, NPCHook.UsesPartyHat) || NPCLoader.UsesPartyHat(self)) && orig(self);

    static void TownEntities(On_NPC.orig_AI_007_TownEntities orig, NPC self)
    {
        orig(self);
        float damage = 1f;
        int defense = 0;
        NPCLoader.BuffTownNPC(ref damage, ref defense);
        self.defense += defense;
        if ((uint)self.whoAmI < 200u) townDamage[self.whoAmI] = damage;
    }

    internal static float TownDamage(NPC npc) => (uint)npc.whoAmI < 200u && townDamage[npc.whoAmI] > 0f ? townDamage[npc.whoAmI] : 1f;

    static void HealthBars(On_Main.orig_DrawInterface_14_EntityHealthBars orig, Main self)
    {
        bool outer = drawingHealthBars;
        drawingHealthBars = true;
        healthBarCursor = 0;
        try { orig(self); }
        finally { drawingHealthBars = outer; }
    }

    static void DrawHealthBar(On_Main.orig_DrawHealthBar orig, Main self, float X, float Y, int Health, int MaxHealth, float alpha, float scale, bool noFlip, bool inUISpace)
    {
        NPC npc = drawingHealthBars ? FindHealthBarOwner(X, Health, MaxHealth) : default;
        if (npc.IsNull || !NPCLoader.HasHooks(npc, NPCHook.DrawHealthBar))
        {
            orig(self, X, Y, Health, MaxHealth, alpha, scale, noFlip, inUISpace);
            return;
        }
        var position = new Vector2(X, Y);
        bool? result = NPCLoader.DrawHealthBar(npc, Main.HealthBarDrawSettings, ref scale, ref position);
        if (result == false) return;
        if (result == true) orig(self, position.X, position.Y, Health, MaxHealth, alpha, scale, noFlip, inUISpace);
        else orig(self, X, Y, Health, MaxHealth, alpha, scale, noFlip, inUISpace);
    }

    static NPC FindHealthBarOwner(float x, int life, int lifeMax)
    {
        for (int k = 0; k < 200; k++)
        {
            int n = (healthBarCursor + k) % 200;
            NPC npc = Main.npc[n];
            if (!npc.active || npc.lifeMax != lifeMax || Math.Abs(npc.position.X + npc.width / 2 - x) > 1f) continue;
            if (npc.life != life && !((uint)npc.realLife < 200u && Main.npc[npc.realLife].life == life)) continue;
            healthBarCursor = n + 1;
            return npc;
        }
        return default;
    }
}

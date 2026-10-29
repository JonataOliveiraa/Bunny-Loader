using System;
using System.IO;
using System.Text.Json.Nodes;
using BunnyLoader;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader.Core;

internal static class PlayerHooks
{
    internal static void Install(Type type)
    {
        bool Has(string name) => ItemHooks.Overrides(type, typeof(ModPlayer), name);
        if (Has(nameof(ModPlayer.ResetEffects)) || Has(nameof(ModPlayer.ModifyMaxStats)))
            ItemHooks.Once("PlayerResetEffects", () => On_Player.ResetEffects += ResetEffects);
        if (Has(nameof(ModPlayer.PreUpdate)) || Has(nameof(ModPlayer.PostUpdate))) ItemHooks.Once("PlayerUpdate", () => On_Player.Update += Update);
        if (Has(nameof(ModPlayer.PreUpdateBuffs)) || Has(nameof(ModPlayer.PostUpdateBuffs)))
            ItemHooks.Once("PlayerUpdateBuffs", () => On_Player.UpdateBuffs += UpdateBuffs);
        if (Has(nameof(ModPlayer.UpdateEquips))) ItemHooks.Once("PlayerUpdateEquips", () => On_Player.UpdateEquips += UpdateEquips);
        if (Has(nameof(ModPlayer.PostUpdateEquips))) ItemEquipHooks.WantArmorSets();
        if (Has(nameof(ModPlayer.FrameEffects))) ItemEquipHooks.WantFrame();
        if (Has(nameof(ModPlayer.UpdateBadLifeRegen)) || Has(nameof(ModPlayer.UpdateLifeRegen)))
            ItemHooks.Once("PlayerLifeRegen", () => On_Player.UpdateLifeRegen += UpdateLifeRegen);
        if (Has(nameof(ModPlayer.PreModifyLuck)) || Has(nameof(ModPlayer.ModifyLuck)))
            ItemHooks.Once("PlayerLuck", () => On_Player.RecalculateLuck += RecalculateLuck);
        if (Has(nameof(ModPlayer.UpdateDead))) ItemHooks.Once("PlayerUpdateDead", () => On_Player.UpdateDead += UpdateDead);
        if (Has(nameof(ModPlayer.PreKill)) || Has(nameof(ModPlayer.Kill))) ItemHooks.Once("PlayerKill", () => On_Player.KillMe += KillMe);
        if (Has(nameof(ModPlayer.OnEnterWorld))) ItemHooks.Once("PlayerEnterWorld", () => On_Player_Hooks.EnterWorld += EnterWorld);
        if (Has(nameof(ModPlayer.OnRespawn))) ItemHooks.Once("PlayerSpawn", () => On_Player.Spawn += Spawn);
        if (Has(nameof(ModPlayer.CanUseItem))) ItemHooks.WantCanUse();
        if (Has(nameof(ModPlayer.PreItemCheck)) || Has(nameof(ModPlayer.PostItemCheck))) ItemHooks.Once("PlayerItemCheck", () => On_Player.ItemCheck += ItemCheck);
        if (Has(nameof(ModPlayer.OnHitAnything))) ItemHooks.Once("PlayerOnHit", () => On_Player.OnHit += OnHit);
        ItemCombatHooks.Install(Has, true);
        ItemShootHooks.Install(Has, true);
        ItemTimingHooks.Install(Has, true);
        ItemManaHooks.Install(Has, true);
        ItemHealHooks.Install(Has);
        ItemHitHooks.Install(name => Has(name) || Has(name + "WithItem"), true);
        if (Has(nameof(ModPlayer.SaveData)) || Has(nameof(ModPlayer.LoadData)) || Has(nameof(ModPlayer.PreSavePlayer)) ||
            Has(nameof(ModPlayer.PostSavePlayer)) || Has(nameof(ModPlayer.PreSaveCustomData)))
            ItemHooks.Once("PlayerSave", () =>
            {
                On_Player.InternalSavePlayerFile += InternalSavePlayerFile;
                On_Player.LoadPlayer += LoadPlayer;
            });
    }

    static void ResetEffects(On_Player.orig_ResetEffects orig, Player self)
    {
        orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.ResetEffects), m => m.ResetEffects());
        StatModifier health = StatModifier.Default, mana = StatModifier.Default;
        bool any = false;
        PlayerLoader.Each(self, nameof(ModPlayer.ModifyMaxStats), m =>
        {
            m.ModifyMaxStats(out StatModifier h, out StatModifier n);
            health = health.CombineWith(h);
            mana = mana.CombineWith(n);
            any = true;
        });
        if (!any) return;
        try
        {
            int life = self.statLifeMax, magic = self.statManaMax;
            self.statLifeMax2 = Math.Max(1, self.statLifeMax2 + (int)health.ApplyTo(life) - life);
            self.statManaMax2 = Math.Max(0, self.statManaMax2 + (int)mana.ApplyTo(magic) - magic);
        }
        catch (Exception e) { ItemHooks.Report("ModifyMaxStats", e); }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        PlayerLoader.Each(self, nameof(ModPlayer.PreUpdate), m => m.PreUpdate());
        orig(self, i);
        PlayerLoader.Each(self, nameof(ModPlayer.PostUpdate), m => m.PostUpdate());
    }

    static void UpdateBuffs(On_Player.orig_UpdateBuffs orig, Player self, int i)
    {
        PlayerLoader.Each(self, nameof(ModPlayer.PreUpdateBuffs), m => m.PreUpdateBuffs());
        orig(self, i);
        PlayerLoader.Each(self, nameof(ModPlayer.PostUpdateBuffs), m => m.PostUpdateBuffs());
    }

    static void UpdateEquips(On_Player.orig_UpdateEquips orig, Player self, int i)
    {
        orig(self, i);
        PlayerLoader.Each(self, nameof(ModPlayer.UpdateEquips), m => m.UpdateEquips());
    }

    static void UpdateLifeRegen(On_Player.orig_UpdateLifeRegen orig, Player self)
    {
        PlayerLoader.Each(self, nameof(ModPlayer.UpdateBadLifeRegen), m => m.UpdateBadLifeRegen());
        orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.UpdateLifeRegen), m => m.UpdateLifeRegen());
    }

    static void RecalculateLuck(On_Player.orig_RecalculateLuck orig, Player self)
    {
        float luck = self.luck;
        bool vanilla = PlayerLoader.All(self, nameof(ModPlayer.PreModifyLuck), m => m.PreModifyLuck(ref luck));
        if (vanilla)
        {
            orig(self);
            luck = self.luck;
        }
        PlayerLoader.Each(self, nameof(ModPlayer.ModifyLuck), m => m.ModifyLuck(ref luck));
        self.luck = luck;
    }

    static void UpdateDead(On_Player.orig_UpdateDead orig, Player self)
    {
        orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.UpdateDead), m => m.UpdateDead());
    }

    static void KillMe(On_Player.orig_KillMe orig, Player self, PlayerDeathReason damageSource, double dmg, int hitDirection, bool pvp)
    {
        bool dies = !self.dead && !self.creativeGodMode;
        if (dies)
        {
            bool playSound = true, genDust = true;
            PlayerDeathReason source = damageSource;
            if (!PlayerLoader.All(self, nameof(ModPlayer.PreKill), m => m.PreKill(dmg, hitDirection, pvp, ref playSound, ref genDust, ref source))) return;
            damageSource = source;
        }
        orig(self, damageSource, dmg, hitDirection, pvp);
        if (dies && self.dead) PlayerLoader.Each(self, nameof(ModPlayer.Kill), m => m.Kill(dmg, hitDirection, pvp, damageSource));
    }

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (PlayerLoader.All(self, nameof(ModPlayer.PreItemCheck), self, static (m, _) => m.PreItemCheck())) orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.PostItemCheck), m => m.PostItemCheck());
    }

    static void OnHit(On_Player.orig_OnHit orig, Player self, float x, float y, Entity victim)
    {
        orig(self, x, y, victim);
        PlayerLoader.Each(self, nameof(ModPlayer.OnHitAnything), (x, y, victim), static (m, s) => m.OnHitAnything(s.x, s.y, s.victim));
    }

    static void EnterWorld(On_Player_Hooks.orig_EnterWorld orig, int playerIndex)
    {
        orig(playerIndex);
        Player player = Main.player[playerIndex];
        if (!player.IsNull) PlayerLoader.Each(player, nameof(ModPlayer.OnEnterWorld), m => m.OnEnterWorld());
    }

    static void Spawn(On_Player.orig_Spawn orig, Player self, PlayerSpawnContext context)
    {
        bool dead = self.dead;
        orig(self, context);
        if (dead) PlayerLoader.Each(self, nameof(ModPlayer.OnRespawn), m => m.OnRespawn());
    }

    static void InternalSavePlayerFile(On_Player.orig_InternalSavePlayerFile orig, PlayerFileData playerFile)
    {
        Player saving = playerFile.IsNull ? default : playerFile.Player;
        if (!saving.IsNull) PlayerLoader.Each(saving, nameof(ModPlayer.PreSavePlayer), m => m.PreSavePlayer());
        try
        {
            orig(playerFile);
            if (saving.IsNull || DataFile(playerFile) is not string file) return;
            PlayerLoader.Each(saving, nameof(ModPlayer.PreSaveCustomData), m => m.PreSaveCustomData());
            JsonObject all = Read(file);
            ModPlayer[] instances = PlayerLoader.Of(saving);
            foreach (ModPlayer template in PlayerLoader.Hooked(nameof(ModPlayer.SaveData)))
            {
                ModPlayer m = instances[template.Index];
                string key = PlayerLoader.KeyOf(template);
                try
                {
                    var tag = new TagCompound();
                    m.SaveData(tag);
                    if (tag.Count > 0) all[key] = JsonNode.Parse(TagJson.Write(tag));
                    else all.Remove(key);
                }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.SaveData", e); }
            }
            if (all.Count > 0) File.WriteAllText(file, all.ToJsonString());
            else File.Delete(file);
        }
        catch (Exception e) { ItemHooks.Report("ModPlayer save", e); }
        finally
        {
            if (!saving.IsNull) PlayerLoader.Each(saving, nameof(ModPlayer.PostSavePlayer), m => m.PostSavePlayer());
        }
    }

    static PlayerFileData LoadPlayer(On_Player.orig_LoadPlayer orig, string? playerPath, bool cloudSave)
    {
        PlayerFileData data = orig(playerPath, cloudSave);
        try
        {
            if (data.IsNull || DataFile(data) is not string file) return data;
            Player player = data.Player;
            if (player.IsNull) return data;
            JsonObject all = Read(file);
            ModPlayer[] instances = PlayerLoader.Of(player);
            foreach (ModPlayer template in PlayerLoader.Hooked(nameof(ModPlayer.LoadData)))
            {
                if (all[PlayerLoader.KeyOf(template)] is not JsonObject saved) continue;
                ModPlayer m = instances[template.Index];
                try { m.LoadData(TagJson.Read(saved.ToJsonString())); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.LoadData", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("ModPlayer load", e); }
        return data;
    }

    static string? DataFile(PlayerFileData data)
    {
        FileData file = data;
        if (file.IsCloudSave) return null;
        string? path = file.Path;
        return string.IsNullOrEmpty(path) ? null : path + ".bl.json";
    }

    static JsonObject Read(string file)
    {
        if (!File.Exists(file)) return new JsonObject();
        try { return JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? new JsonObject(); }
        catch (Exception e)
        {
            Log.Error($"ModPlayer: {file} is broken ({e.Message}); the saved data was ignored");
            return new JsonObject();
        }
    }
}

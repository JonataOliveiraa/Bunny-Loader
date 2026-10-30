using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static class MountHooks
{
    sealed class Setting
    {
        public ModMount Mount = null!;
        public bool Called;
    }

    static Setting? setting;
    static bool? dismountSkip;

    internal static void Install(Type type)
    {
        bool Has(string name) => ItemHooks.Overrides(type, typeof(ModMount), name);
        ItemHooks.Once("MountSpecificData", () => On_Mount.Dismount += ClearData);
        if (Has(nameof(ModMount.SetMount)) || Has(nameof(ModMount.Dismount)))
            ItemHooks.Once("MountSet", () =>
            {
                On_Mount.SetMount += SetMount;
                On_Mount.DoSpawnDust += DoSpawnDust;
                On_Mount.Dismount += Dismount;
            });
        if (Has(nameof(ModMount.UpdateEffects))) ItemHooks.Once("MountEffects", () => On_Mount.UpdateEffects += UpdateEffects);
        if (Has(nameof(ModMount.UpdateFrame))) ItemHooks.Once("MountFrame", () => On_Mount.UpdateFrame += UpdateFrame);
        if (Has(nameof(ModMount.JumpHeight))) ItemHooks.Once("MountJumpHeight", () => On_Mount.JumpHeight += JumpHeight);
        if (Has(nameof(ModMount.JumpSpeed))) ItemHooks.Once("MountJumpSpeed", () => On_Mount.JumpSpeed += JumpSpeed);
        if (Has(nameof(ModMount.UseAbility))) ItemHooks.Once("MountUseAbility", () => On_Mount.UseAbility += UseAbility);
        if (Has(nameof(ModMount.AimAbility))) ItemHooks.Once("MountAimAbility", () => On_Mount.AimAbility += AimAbility);
        if (Has(nameof(ModMount.Draw))) ItemHooks.Once("MountDraw", () => On_Mount.Draw += Draw);
    }

    static ModMount? Of(Mount mount) => MountLoader.GetMount(mount._type);

    static Player OwnerOf(Mount mount)
    {
        Player local = Main.LocalPlayer;
        if (!local.IsNull && local.mount == mount) return local;
        var players = Main.player;
        for (int i = 0; i < 255; i++)
        {
            Player p = players[i];
            if (!p.IsNull && p.active && p.mount == mount) return p;
        }
        return default;
    }

    static void ClearData(On_Mount.orig_Dismount orig, Mount self, Player mountedPlayer, bool ignoreEffect)
    {
        orig(self, mountedPlayer, ignoreEffect);
        self._mountSpecificData = null;
    }

    static void SetMount(On_Mount.orig_SetMount orig, Mount self, int m, Player mountedPlayer, bool ignoreEffect)
    {
        ModMount? mount = MountLoader.GetMount(m);
        if (mount == null || self._type == m)
        {
            orig(self, m, mountedPlayer, ignoreEffect);
            return;
        }
        self._mountSpecificData = null;
        Setting? outer = setting;
        var current = new Setting { Mount = mount };
        setting = current;
        try { orig(self, m, mountedPlayer, ignoreEffect); }
        finally { setting = outer; }
        if (current.Called || self._type != m) return;
        bool skip = false;
        try { mount.SetMount(mountedPlayer, ref skip); }
        catch (Exception e) { ItemHooks.Report($"{mount.FullName}.SetMount", e); }
    }

    static void DoSpawnDust(On_Mount.orig_DoSpawnDust orig, Mount self, Player mountedPlayer, bool isDismounting)
    {
        Setting? current = setting;
        if (!isDismounting && current != null && !current.Called)
        {
            current.Called = true;
            bool skip = false;
            try { current.Mount.SetMount(mountedPlayer, ref skip); }
            catch (Exception e) { ItemHooks.Report($"{current.Mount.FullName}.SetMount", e); }
            if (skip) return;
        }
        if (isDismounting && dismountSkip == true) return;
        orig(self, mountedPlayer, isDismounting);
    }

    static void Dismount(On_Mount.orig_Dismount orig, Mount self, Player mountedPlayer, bool ignoreEffect)
    {
        bool skip = false;
        if (Of(self) is ModMount mount)
        {
            try { mount.Dismount(mountedPlayer, ref skip); }
            catch (Exception e) { ItemHooks.Report($"{mount.FullName}.Dismount", e); }
        }
        bool? outer = dismountSkip;
        dismountSkip = skip;
        try { orig(self, mountedPlayer, ignoreEffect); }
        finally { dismountSkip = outer; }
    }

    static void UpdateEffects(On_Mount.orig_UpdateEffects orig, Mount self, Player mountedPlayer)
    {
        if (Of(self) is ModMount mount)
        {
            try { mount.UpdateEffects(mountedPlayer); }
            catch (Exception e) { ItemHooks.Report($"{mount.FullName}.UpdateEffects", e); }
        }
        orig(self, mountedPlayer);
    }

    static void UpdateFrame(On_Mount.orig_UpdateFrame orig, Mount self, Player mountedPlayer, int state, Vector2 velocity)
    {
        if (Of(self) is ModMount mount)
        {
            bool go = true;
            try { go = mount.UpdateFrame(mountedPlayer, state, velocity); }
            catch (Exception e) { ItemHooks.Report($"{mount.FullName}.UpdateFrame", e); }
            if (!go) return;
        }
        orig(self, mountedPlayer, state, velocity);
    }

    static int JumpHeight(On_Mount.orig_JumpHeight orig, Mount self, float xVelocity)
    {
        int value = orig(self, xVelocity);
        if (Of(self) is not ModMount mount) return value;
        Player player = OwnerOf(self);
        if (player.IsNull) return value;
        try { mount.JumpHeight(player, ref value, xVelocity); }
        catch (Exception e) { ItemHooks.Report($"{mount.FullName}.JumpHeight", e); }
        return value;
    }

    static float JumpSpeed(On_Mount.orig_JumpSpeed orig, Mount self, float xVelocity)
    {
        float value = orig(self, xVelocity);
        if (Of(self) is not ModMount mount) return value;
        Player player = OwnerOf(self);
        if (player.IsNull) return value;
        try { mount.JumpSpeed(player, ref value, xVelocity); }
        catch (Exception e) { ItemHooks.Report($"{mount.FullName}.JumpSpeed", e); }
        return value;
    }

    static void UseAbility(On_Mount.orig_UseAbility orig, Mount self, Player mountedPlayer, Vector2 mousePosition, bool toggleOn)
    {
        orig(self, mountedPlayer, mousePosition, toggleOn);
        if (Of(self) is not ModMount mount) return;
        try { mount.UseAbility(mountedPlayer, mousePosition, toggleOn); }
        catch (Exception e) { ItemHooks.Report($"{mount.FullName}.UseAbility", e); }
    }

    static bool AimAbility(On_Mount.orig_AimAbility orig, Mount self, Player mountedPlayer, Vector2 mousePosition)
    {
        if (Of(self) is ModMount mount)
        {
            try { mount.AimAbility(mountedPlayer, mousePosition); }
            catch (Exception e) { ItemHooks.Report($"{mount.FullName}.AimAbility", e); }
        }
        return orig(self, mountedPlayer, mousePosition);
    }

    static (ReLogic.Content.Asset<Texture2D>? Texture, ReLogic.Content.Asset<Texture2D>? Glow) Layer(Mount.MountData data, int drawType) => drawType switch
    {
        0 => (data.backTexture, data.backTextureGlow),
        1 => (data.backTextureExtra, data.backTextureExtraGlow),
        2 => (data.frontTexture, data.frontTextureGlow),
        3 => (data.frontTextureExtra, data.frontTextureExtraGlow),
        _ => (null, null),
    };

    static void Draw(On_Mount.orig_Draw orig, Mount self, PlayerDrawSet playerDrawData, int drawType, Player drawPlayer, Vector2 Position, Color drawColor, SpriteEffects playerEffect, float shadow)
    {
        if (Of(self) is not ModMount mount)
        {
            orig(self, playerDrawData, drawType, drawPlayer, Position, drawColor, playerEffect, shadow);
            return;
        }
        bool handled = false;
        try { handled = DrawMod(mount, self, playerDrawData, drawType, drawPlayer, Position, drawColor, playerEffect, shadow); }
        catch (Exception e) { ItemHooks.Report($"{mount.FullName}.Draw", e); }
        if (!handled) orig(self, playerDrawData, drawType, drawPlayer, Position, drawColor, playerEffect, shadow);
    }

    static bool DrawMod(ModMount mount, Mount self, PlayerDrawSet drawInfo, int drawType, Player player, Vector2 position, Color drawColor, SpriteEffects effect, float shadow)
    {
        Mount.MountData data = self._data;
        (ReLogic.Content.Asset<Texture2D>? textureAsset, ReLogic.Content.Asset<Texture2D>? glowAsset) = Layer(data, drawType);
        if (textureAsset == null) return false;
        Texture2D texture = textureAsset.Value;
        Texture2D glowTexture = glowAsset != null ? glowAsset.Value : default;
        Vector2 screen = Main.screenPosition;
        int xOffset = self.XOffset * (player.direction <= 0 ? -1 : 1);
        int yOffset = self.YOffset + self.PlayerOffset;
        int frameHeight = data.totalFrames > 0 ? data.textureHeight / data.totalFrames : data.textureHeight;
        var drawPosition = new Vector2((int)(position.X - screen.X + player.width / 2 + xOffset), (int)(position.Y - screen.Y + player.height / 2 + yOffset));
        var frame = new Rectangle(0, frameHeight * self._frame, data.textureWidth, frameHeight - 2);
        var glowColor = new Color((byte)(drawColor.R * 0.25f + 191.25f), (byte)(drawColor.G * 0.25f + 191.25f), (byte)(drawColor.B * 0.25f + 191.25f), (byte)(drawColor.A * 0.25f + 191.25f));
        SpriteEffects spriteEffects = effect;
        if (self.Cart && Math.Sign(player.velocity.X) == -player.direction) spriteEffects ^= SpriteEffects.FlipHorizontally;
        float rotation = 0f, drawScale = 1f;
        Vector2 drawOrigin = self.Origin;
        Color color = drawColor;
        var extra = new List<DrawData>();
        bool draw = mount.Draw(extra, drawType, player, ref texture, ref glowTexture, ref drawPosition, ref frame, ref color, ref glowColor, ref rotation,
            ref spriteEffects, ref drawOrigin, ref drawScale, shadow);
        DrawDataList cache = drawInfo.DrawDataCache;
        foreach (DrawData added in extra) cache.Add(added);
        if (draw)
        {
            if (!texture.IsNull) cache.Add(Shaded(new DrawData(texture, drawPosition, frame, color, rotation, drawOrigin, drawScale, spriteEffects)));
            if (!glowTexture.IsNull)
            {
                float alpha = color.A / 255f;
                var glow = new Color((byte)(glowColor.R * alpha), (byte)(glowColor.G * alpha), (byte)(glowColor.B * alpha), (byte)(glowColor.A * alpha));
                cache.Add(Shaded(new DrawData(glowTexture, drawPosition, frame, glow, rotation, drawOrigin, drawScale, spriteEffects)));
            }
        }
        return true;
    }

    static DrawData Shaded(DrawData data)
    {
        data.shader = Mount.currentShader;
        return data;
    }
}

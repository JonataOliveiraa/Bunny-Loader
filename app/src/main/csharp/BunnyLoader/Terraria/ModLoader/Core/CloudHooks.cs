using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;

namespace Terraria.ModLoader.Core;

internal static class CloudHooks
{
    static readonly HashSet<Type> Drawers = new();
    static readonly List<DrawData> Cache = new();

    static bool rolledRare;
    static int rareType, rareSlot = -1;

    internal static void Install(Type type)
    {
        ItemHooks.Once("Clouds", () =>
        {
            On_Cloud.addCloud += AddCloud;
            On_Cloud.RollRareCloud += RollRareCloud;
        });
        if (!ItemHooks.Overrides(type, typeof(ModCloud), nameof(ModCloud.Draw))) return;
        lock (Drawers) Drawers.Add(type);
        ItemHooks.Once("CloudDraw", () => On_NextHorizonRenderer.DrawCloud += DrawCloud);
    }

    static int FreeSlot()
    {
        GameArray<Cloud> clouds = Main.cloud;
        int count = Math.Min(200, clouds.Length);
        for (int i = 0; i < count; i++)
        {
            if (!clouds[i].active) return i;
        }
        return -1;
    }

    static int RollRareCloud(On_Cloud.orig_RollRareCloud orig)
    {
        rolledRare = true;
        int type = 0;
        try { type = CloudLoader.ChooseCloud(Main.tenthAnniversaryWorld ? 15 : 18, true) ?? 0; }
        catch (Exception e) { ItemHooks.Report("ModCloud (rare choice)", e); }
        rareType = type;
        if (type != 0) rareSlot = FreeSlot();
        return type != 0 ? type : orig();
    }

    static void AddCloud(On_Cloud.orig_addCloud orig)
    {
        int common = 0;
        try { common = CloudLoader.ChooseCloud(22, false) ?? 0; }
        catch (Exception e) { ItemHooks.Report("ModCloud (choice)", e); }
        int slot = common != 0 ? FreeSlot() : -1;
        rolledRare = false;
        rareType = 0;
        rareSlot = -1;
        orig();
        if (rolledRare) slot = rareType != 0 ? rareSlot : -1;
        if (slot < 0) return;
        Cloud cloud = Main.cloud[slot];
        if (!cloud.active || (rolledRare && cloud.type != rareType)) return;
        if (!rolledRare)
        {
            cloud.type = common;
            Texture2D texture = TextureAssets.Cloud[common].Value;
            cloud.width = (int)(texture.Width * cloud.scale);
            cloud.height = (int)(texture.Height * cloud.scale);
        }
        if (CloudLoader.GetModCloud(cloud.type) is not ModCloud modCloud) return;
        try { modCloud.OnSpawn(cloud); }
        catch (Exception e) { ItemHooks.Report($"{modCloud.FullName}.OnSpawn", e); }
    }

    static int IndexOf(Cloud cloud)
    {
        GameArray<Cloud> clouds = Main.cloud;
        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i].Pointer == cloud.Pointer) return i;
        }
        return -1;
    }

    static void DrawCloud(On_NextHorizonRenderer.orig_DrawCloud orig, NextHorizonRenderer self, float globalCloudAlpha, Cloud theCloud, int cloudPass, float cY)
    {
        ModCloud? modCloud = CloudLoader.GetModCloud(theCloud.type);
        bool drawer;
        lock (Drawers) drawer = modCloud != null && Drawers.Contains(modCloud.GetType());
        if (!drawer)
        {
            orig(self, globalCloudAlpha, theCloud, cloudPass, cY);
            return;
        }
        bool vanilla = true;
        try { vanilla = Draw(modCloud!, globalCloudAlpha, theCloud, cloudPass, cY); }
        catch (Exception e) { ItemHooks.Report($"{modCloud!.FullName}.Draw", e); }
        if (vanilla) orig(self, globalCloudAlpha, theCloud, cloudPass, cY);
    }

    static bool Draw(ModCloud modCloud, float globalCloudAlpha, Cloud cloud, int cloudPass, float cY)
    {
        Texture2D texture = TextureAssets.Cloud[cloud.type].Value;
        var size = new Vector2(texture.Width, texture.Height);
        Color color = cloud.cloudColor(Main.ColorOfTheSkies);
        if (cloudPass == 1)
        {
            float red = cloud.scale * 0.8f, green = (cloud.scale + 1f) / 2f * 0.9f;
            color.R = (byte)(color.R * red);
            color.G = (byte)(color.G * green);
        }
        if (Main.atmo < 1f) color *= Main.atmo;
        var original = new DrawData(texture, new Vector2(cloud.position.X, cY) + size / 2f, new Rectangle(0, 0, texture.Width, texture.Height),
            color * globalCloudAlpha, cloud.rotation, size / 2f, cloud.scale, cloud.spriteDir);
        DrawData drawData = original;
        Cache.Clear();
        bool keep = modCloud.Draw(Cache, cloud, IndexOf(cloud), ref drawData);
        foreach (DrawData extra in Cache) Blit(extra);
        if (!keep) return false;
        if (Same(drawData, original)) return true;
        Blit(drawData);
        return false;
    }

    static bool Same(in DrawData a, in DrawData b) =>
        a.texture.Pointer == b.texture.Pointer && a.position == b.position && a.sourceRect.Equals(b.sourceRect) && a.color.Equals(b.color) &&
        a.rotation == b.rotation && a.origin == b.origin && a.scale == b.scale && a.effect == b.effect;

    static void Blit(in DrawData d) =>
        Main.cloudBatch.Draw(d.texture, d.position, d.sourceRect, d.color, d.rotation, d.origin, d.scale, d.effect, 0f);
}

using System;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static unsafe class SubworldScreens
{
    const int SaveAndExitLabel = 79;
    const string PlanetId = "bunny/planeta";

    static Texture_Layout planet;
    static GameHandle? planetHandle;
    static int exitButton;

    internal static void Install()
    {
        On_GUIStatusMenu.Draw += StatusDraw;
        On_GUINetPlayStatusMenu.Draw += NetStatusDraw;
        On_GUISettingsPauseMenu.Draw += PauseDraw;
        On_WorldGen.SaveAndQuit += SaveAndQuit;
    }

    static void Loading()
    {
        if (SubworldTravel.Busy) Main.statusText = SubworldTravel.LoadingText;
        else if (SubworldNet.Rejoining) Main.statusText = SubworldNet.RejoinText;
    }

    static void StatusDraw(On_GUIStatusMenu.orig_Draw orig, GUIStatusMenu self)
    {
        Loading();
        orig(self);
    }

    static void NetStatusDraw(On_GUINetPlayStatusMenu.orig_Draw orig, GUINetPlayStatusMenu self)
    {
        Loading();
        orig(self);
    }

    static bool ShowExit() => SubworldLoader.InSubworld && !SubworldTravel.Busy && !SubworldNet.HostTraveling && !SubworldTransition.Active && (Main.netMode == 0 || SubworldNet.IsHost);

    static void PauseDraw(On_GUISettingsPauseMenu.orig_Draw orig, GUISettingsPauseMenu self)
    {
        SettingsPauseMenu_Layout layout = SettingsPauseMenu_Layout.Instance;
        var labels = Lang.mobile;
        if (!ShowExit() || layout.IsNull || labels.IsNull || labels.Length <= SaveAndExitLabel)
        {
            orig(self);
            return;
        }

        TransactionButton_Layout home = layout.Home;
        LocalizedText label = labels[SaveAndExitLabel];
        Texture_Layout icon = home.IconTexture, highlight = home.IconTextureHighlight;
        bool forceScale = home.ForceIconScale;
        float forcedScale = home.ForcedIconScale;
        string? text = label.Value;
        Texture_Layout ours = Planet(home, out float scale);
        label.SetValue(Language.GetTextValue("BunnyLoader.ExitDimension"));
        if (!ours.IsNull)
        {
            home.IconTexture = ours;
            if (!highlight.IsNull) home.IconTextureHighlight = ours;
            home.ForceIconScale = true;
            home.ForcedIconScale = scale;
        }

        exitButton++;
        try { orig(self); }
        finally
        {
            exitButton--;
            label.SetValue(text);
            home.IconTexture = icon;
            home.IconTextureHighlight = highlight;
            home.ForceIconScale = forceScale;
            home.ForcedIconScale = forcedScale;
        }
    }

    static void SaveAndQuit(On_WorldGen.orig_SaveAndQuit orig)
    {
        if (exitButton > 0 && SubworldLoader.InSubworld)
        {
            SubworldLoader.Exit();
            return;
        }

        orig();
    }

    static Texture_Layout Planet(TransactionButton_Layout home, out float scale)
    {
        scale = 1;
        try
        {
            if (planet.IsNull)
            {
                IntPtr asset = Host.Native()->PlanetTexture();
                if (asset == IntPtr.Zero) return default;
                Texture2D texture = Asset<Texture2D>.ForGameAsset(asset, "bunny:planet").Value;
                Texture_Layout icon = Texture_Layout.New();
                planetHandle = new GameHandle(icon.Pointer);
                icon.TextureId = PlanetId;
                icon._lastTextureId = PlanetId;
                icon._texture = texture;
                planet = icon;
            }

            Texture2D theirs = home.IconTexture.IsNull ? default : home.IconTexture.Texture;
            float iconPx = (theirs.IsNull ? 32 : theirs.Height) * (home.ForceIconScale ? home.ForcedIconScale : 1);
            scale = iconPx / Math.Max(1, planet._texture.Height);
            return planet;
        }
        catch (Exception e)
        {
            ItemHooks.Report("Subworld exit icon", e);
            return default;
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsModSystem2;

public sealed class CsModSystem2Mod : Mod
{
    static int frames, pass, fail;
    static bool done;
    static int sun0, brightness0, screen0, transforms0, tiles0, interface0;

    static ILog Log => ModContent.GetInstance<CsModSystem2Mod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csmodsystem2 END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        switch (f)
        {
            case 30:
                Network();
                break;
            case 60:
                sun0 = DrawSystem.Sun;
                brightness0 = DrawSystem.Brightness;
                screen0 = DrawSystem.Screen;
                transforms0 = DrawSystem.Transforms;
                tiles0 = DrawSystem.Tiles;
                interface0 = DrawSystem.Interface;
                DrawSystem.Tint = DrawSystem.Dim = DrawSystem.Shift = true;
                break;
            case 120:
                Drawing();
                DrawSystem.Tint = DrawSystem.Dim = DrawSystem.Shift = false;
                DrawSystem.HideLast = true;
                GUIInstance.Active.GUIMap.OpenFullscreenMap();
                break;
            case 200:
                Map();
                GUIInstance.Active.GUIMap.CloseFullscreenMap();
                DrawSystem.HideLast = false;
                done = true;
                break;
        }
    }

    static void Network()
    {
        Assembly api = typeof(ModSystem).Assembly;
        Type hooks = api.GetType("Terraria.ModLoader.Core.SystemNetHooks", true)!;
        var write = hooks.GetMethod("WriteWorld", BindingFlags.NonPublic | BindingFlags.Static)!;
        var read = hooks.GetMethod("ReadWorld", BindingFlags.NonPublic | BindingFlags.Static)!;
        DrawSystem.Number = 4242;
        byte[] data = (byte[])write.Invoke(null, null)!;
        DrawSystem.Number = 0;
        using (var reader = new BinaryReader(new MemoryStream(data)))
        {
            reader.ReadByte();
            read.Invoke(null, new object[] { reader });
        }
        Report("NetSend writes the world envelope and NetReceive reads it back by system key",
            DrawSystem.Flag1 && DrawSystem.Flag2 && DrawSystem.Number == 4242, $"bytes={data.Length} flags={DrawSystem.Flag1},{DrawSystem.Flag2} number={DrawSystem.Number}");

        var message = GameArray<byte>.New(new GameClass("System", "Byte"), 4);
        message[0] = 250;
        message[1] = 11;
        message[2] = 22;
        message[3] = 33;
        NetMessage.buffer[256].ProcessData(message, 4, out int type);
        Report("HijackGetData sees the message type and the payload, and a hijack skips the game",
            DrawSystem.HijackGets == 1 && DrawSystem.HijackType == 250 && DrawSystem.HijackPayload == "11,22,33" && type == 250,
            $"gets={DrawSystem.HijackGets} type={DrawSystem.HijackType} payload={DrawSystem.HijackPayload} out={type}");

        NetMessage.SendData(250);
        Report("HijackSendData is not called in single player, as in tModLoader", DrawSystem.HijackSends == 0, $"sends={DrawSystem.HijackSends}");
    }

    static void Drawing()
    {
        int frames = DrawSystem.Tiles - tiles0;
        Report("ModifySunLightColor runs every frame and its colors stay",
            DrawSystem.Sun - sun0 >= 10 && Main.tileColor.PackedValue == new Color(10, 20, 30).PackedValue && Main.ColorOfTheSkies.PackedValue == new Color(40, 50, 60).PackedValue,
            $"calls={DrawSystem.Sun - sun0} tile={Main.tileColor} sky={Main.ColorOfTheSkies}");
        float now = Lighting.GlobalBrightness, expected = DrawSystem.BrightnessBefore * 0.5f;
        Report("ModifyLightingBrightness scales the global brightness",
            DrawSystem.Brightness - brightness0 >= 10 && MathF.Abs(now - expected) < 0.001f,
            $"calls={DrawSystem.Brightness - brightness0} before={DrawSystem.BrightnessBefore} now={now}");
        Report("ModifyScreenPosition runs every frame and the tiles are drawn from the moved camera",
            DrawSystem.Screen - screen0 >= 10 && DrawSystem.ShiftSeen, $"calls={DrawSystem.Screen - screen0} seen={DrawSystem.ShiftSeen}");
        Report("ModifyTransformMatrix gets Main.GameViewMatrix every frame",
            DrawSystem.Transforms - transforms0 >= 10 && DrawSystem.SameMatrix, $"calls={DrawSystem.Transforms - transforms0} same={DrawSystem.SameMatrix}");
        Report("PostDrawTiles runs every frame", frames >= 10, $"calls={frames}");
        Report("PostDrawInterface runs every frame with the sprite batch",
            DrawSystem.Interface - interface0 >= 10 && DrawSystem.BatchSet, $"calls={DrawSystem.Interface - interface0} batch={DrawSystem.BatchSet}");
    }

    static void Map()
    {
        var layers = new GameList(Main.MapIcons._layers);
        bool restored = layers.Count == DrawSystem.LayerCount && DrawSystem.LayerCount > 0 && layers[layers.Count - 1] == DrawSystem.HiddenLayer;
        Report("PreDrawMapIconOverlay gets the map layers and a hidden layer comes back afterwards",
            DrawSystem.MapIcons > 0 && DrawSystem.HiddenWas == 1 && DrawSystem.HiddenLayer != 0 && restored,
            $"calls={DrawSystem.MapIcons} layers={DrawSystem.LayerCount} now={layers.Count} visibleBefore={DrawSystem.HiddenWas} restored={restored}");
        Report("PostDrawFullscreenMap runs while the fullscreen map is open", DrawSystem.FullscreenMap > 0, $"calls={DrawSystem.FullscreenMap}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csmodsystem2 {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}

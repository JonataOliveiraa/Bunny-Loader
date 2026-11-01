using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsModSystem2;

public sealed class DrawSystem : ModSystem
{
    public static int Sun, Brightness, Screen, Transforms, Tiles, Interface, MapIcons, FullscreenMap, HijackSends, HijackGets;
    public static bool Tint, Dim, Shift, HideLast, SameMatrix, BatchSet, ShiftSeen;
    public static float BrightnessBefore;
    public static Vector2 ShiftedTo;
    public static int LayerCount, HiddenWas = -1;
    public static nint HiddenLayer;
    public static byte HijackType;
    public static string HijackPayload = "";
    public static bool Flag1, Flag2;
    public static int Number;

    public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
    {
        Sun++;
        if (!Tint) return;
        tileColor = new Color(10, 20, 30);
        backgroundColor = new Color(40, 50, 60);
    }

    public override void ModifyLightingBrightness(ref float scale)
    {
        Brightness++;
        if (!Dim) return;
        BrightnessBefore = Lighting.GlobalBrightness;
        scale = 0.5f;
    }

    public override void ModifyScreenPosition()
    {
        Screen++;
        if (!Shift) return;
        Main.screenPosition = Main.screenPosition + new Vector2(16f, 0f);
        ShiftedTo = Main.screenPosition;
    }

    public override void ModifyTransformMatrix(ref SpriteViewMatrix Transform)
    {
        Transforms++;
        SameMatrix = !Transform.IsNull && Transform == Main.GameViewMatrix;
    }

    public override void PostDrawTiles()
    {
        Tiles++;
        if (Shift) ShiftSeen = Main.screenPosition == ShiftedTo;
    }

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        Interface++;
        BatchSet = !spriteBatch.IsNull;
    }

    public override void PreDrawMapIconOverlay(IReadOnlyList<IMapLayer> layers, MapOverlayDrawContext mapOverlayDrawContext)
    {
        MapIcons++;
        LayerCount = layers.Count;
        if (!HideLast || layers.Count == 0) return;
        IMapLayer last = layers[layers.Count - 1];
        HiddenWas = last.Visible ? 1 : 0;
        last.Visible = false;
        HiddenLayer = last.Visible ? 0 : last.Pointer;
    }

    public override void PostDrawFullscreenMap(ref string mouseText)
    {
        FullscreenMap++;
        mouseText = "csmodsystem2";
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.WriteFlags(true, false, true);
        writer.Write(Number);
    }

    public override void NetReceive(BinaryReader reader)
    {
        reader.ReadFlags(out Flag1, out bool unused, out Flag2);
        Number = reader.ReadInt32();
    }

    public override bool HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber)
    {
        if (messageType != 250) return false;
        HijackGets++;
        HijackType = messageType;
        HijackPayload = $"{reader.ReadByte()},{reader.ReadByte()},{reader.ReadByte()}";
        return true;
    }

    public override bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
    {
        HijackSends++;
        return false;
    }
}

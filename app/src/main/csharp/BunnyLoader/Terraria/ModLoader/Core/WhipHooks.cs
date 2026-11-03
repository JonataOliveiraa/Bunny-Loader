using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent.ItemDropRules;
using Microsoft.Xna.Framework;

namespace Terraria.ModLoader.Core;

internal static unsafe class WhipHooks
{
    internal sealed class Box
    {
        public WhipSettings Value;
    }

    static readonly EntityInstances<Box> Settings = new();
    static readonly object Gate = new();
    static GameHandle? points;
    static bool installed;

    internal static Box Of(Projectile projectile)
    {
        Box? box = Settings.Get(projectile.Pointer);
        if (box != null) return box;
        Projectile.GetWhipSettings(projectile, out _, out int segments, out float rangeMultiplier);
        box = new Box { Value = new WhipSettings { Segments = segments, RangeMultiplier = rangeMultiplier } };
        Settings.Set(projectile.Pointer, box);
        lock (Gate)
        {
            if (!installed)
            {
                installed = true;
                On_Projectile.GetWhipSettings += GetWhipSettings;
            }
        }
        return box;
    }

    internal static void Reset(Projectile projectile)
    {
        if (Settings.Count > 0) Settings.Set(projectile.Pointer, null);
    }

    static void GetWhipSettings(On_Projectile.orig_GetWhipSettings orig, Projectile proj, out float timeToFlyOut, out int segments, out float rangeMultiplier)
    {
        orig(proj, out timeToFlyOut, out segments, out rangeMultiplier);
        if (Settings.Get(proj.Pointer) is not Box box) return;
        segments = box.Value.Segments;
        rangeMultiplier = box.Value.RangeMultiplier;
    }

    internal static void Fill(Projectile proj, List<Vector2> controlPoints, Player owner, bool getActualCollisionPoints, int frameOffset)
    {
        lock (Gate)
        {
            GameObject list = Points();
            DropRuleInterop.Clear(list);
            Projectile.FillWhipControlPoints(proj, list, owner, getActualCollisionPoints, frameOffset);
            var game = new GameList(list);
            int count = game.Count;
            for (int i = 0; i < count; i++) controlPoints.Add(game.Get<Vector2>(i));
        }
    }

    static GameObject Points()
    {
        if (points != null) return new GameObject(points.Target);
        IntPtr template = Main.ribbonPointsCache.Pointer;
        if (template == IntPtr.Zero) throw new InvalidOperationException("the game has no List<Vector2> to copy the class from");
        IntPtr klass = *(IntPtr*)template;
        NativeApi* api = Host.Native();
        IntPtr list = api->NewObject(klass);
        byte* name = Host.Utf8(".ctor");
        IntPtr ctor;
        try { ctor = api->FindMethod(klass, name, 0); }
        finally { Host.FreeUtf8(name); }
        if (list == IntPtr.Zero || ctor == IntPtr.Zero) throw new InvalidOperationException("could not create a game List<Vector2>");
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(list);
        call.Arg(ctor);
        call.Invoke(api->MethodPointer(ctor));
        points = new GameHandle(list);
        return new GameObject(list);
    }
}

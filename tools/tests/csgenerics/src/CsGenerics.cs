using System;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Creative;
using Terraria.GameContent.NetModules;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.Net;

namespace CsGenerics;

public sealed class CsGenericsMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsGenericsMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = pass = fail = 0;
            done = false;
            return;
        }
        if (done || i != Main.myPlayer || ++frames != 120) return;

        done = true;
        Run();
        Log.Info($"csgenerics END pass={pass} fail={fail}");
    }

    static void Check(string name, Func<string?> test)
    {
        string? problem;
        try { problem = test(); }
        catch (Exception e) { problem = e.GetType().Name + ": " + e.Message; }

        if (problem == null) pass++;
        else fail++;
        Log.Info($"csgenerics {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static void Run()
    {
        var repository = new AssetRepository(Main.Assets.Pointer);

        Check("request_texture", () =>
        {
            Asset<Texture2D> asset = repository.Request<Texture2D>("Images\\Item_1", AssetRequestMode.ImmediateLoad);
            Texture2D expected = TextureAssets.Item[1].Value;
            Texture2D got = asset.Value;
            if (got.IsNull) return "Value is null";
            return got.Pointer == expected.Pointer ? null : $"texture {got.Width}x{got.Height} differs from TextureAssets.Item[1] {expected.Width}x{expected.Height}";
        });

        Check("request_default_mode", () =>
        {
            Asset<Texture2D> first = repository.Request<Texture2D>("Images\\Item_2", AssetRequestMode.DoNotLoad);
            Asset<Texture2D> second = repository.Request<Texture2D>("Images\\Item_2");
            if (Asset<Texture2D>.ToGame(first) != Asset<Texture2D>.ToGame(second)) return "two requests gave two game assets";
            return second.Value.Width > 0 ? null : "default mode left the texture empty";
        });

        Check("net_module", () =>
        {
            NetModule text = NetManager.Instance.GetModule<NetTextModule>();
            NetModule liquid = NetManager.Instance.GetModule<NetLiquidModule>();
            if (text.IsNull || liquid.IsNull) return "a module is null";
            if (!NetTextModule.Is(text)) return $"GetModule<NetTextModule> returned {new GameObject(text.Pointer).ClassName}";
            ushort a = NetManager.Instance.GetId<NetTextModule>(), b = NetManager.Instance.GetId<NetLiquidModule>();
            return a != b ? null : $"both ids are {a}";
        });

        Check("creative_power", () =>
        {
            var god = CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>();
            if (god.IsNull) return "GetPower returned null";
            ushort id = CreativePowerManager.Instance.GetPowerId<CreativePowers.GodmodePower>();
            return god.PowerId == id ? null : $"PowerId {god.PowerId} != GetPowerId {id}";
        });

        Check("preferences_value", () =>
        {
            Preferences config = Main.Configuration;
            int i = config.Get("BL_CsGenerics_Missing", 7);
            bool b = config.Get("BL_CsGenerics_Missing", true);
            float f = config.Get("BL_CsGenerics_Missing", 2.5f);
            return i == 7 && b && f == 2.5f ? null : $"got {i} {b} {f}";
        });

        Check("preferences_ref", () =>
        {
            int value = 41;
            Main.Configuration.Get("BL_CsGenerics_Missing", ref value);
            return value == 41 ? null : $"value changed to {value}";
        });

        Check("value_array", () =>
        {
            int picked = Utils.SelectRandom<int>(Main.rand, new[] { 5, 5, 5 });
            float max = Utils.Max<float>(new[] { 1f, 3f, 2f });
            return picked == 5 && max == 3f ? null : $"picked {picked}, max {max}";
        });

        Check("shared_array", () =>
        {
            WorldItem item = Utils.SelectRandom<WorldItem>(Main.rand, Main.item);
            return !item.IsNull && WorldItem.Is(new GameObject(item.Pointer)) ? null : "no WorldItem came back";
        });

        Check("out_shared", () =>
        {
            bool found = TileEntity.TryGet<TileEntity>(-12345, out TileEntity entity);
            return !found && entity.IsNull ? null : "found an entity for id -12345";
        });

        Check("missing_instance", () =>
        {
            try
            {
                Main.Configuration.Get("BL_CsGenerics_Missing", 1.5d);
                return "no exception for Get<double>";
            }
            catch (NotSupportedException e)
            {
                return e.Message.Contains("is not compiled") ? null : e.Message;
            }
        });
    }
}

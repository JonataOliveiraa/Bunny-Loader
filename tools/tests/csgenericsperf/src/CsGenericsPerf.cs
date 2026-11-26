using System;
using System.Collections.Generic;
using System.Diagnostics;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Reflection;
using Terraria;
using Terraria.Chat;
using Terraria.Chat.Commands;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Creative;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.NetModules;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Net;
using Terraria.Testing;
using Terraria.Testing.Cloning;
using Terraria.Utilities;

namespace CsGenericsPerf;

public sealed class CsGenericsPerfMod : Mod
{
    const int StartFrame = 240, Rounds = 5;

    sealed record Case(string Name, int N, Func<int, long> Loop);

    static int frames, errors;
    static bool done;
    static long sink;
    static readonly List<GameHandle> kept = new();

    static ILog Log => ModContent.GetInstance<CsGenericsPerfMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = errors = 0;
            done = false;
            return;
        }
        if (done || i != Main.myPlayer || ++frames != StartFrame) return;

        done = true;
        List<Case> cases;
        try
        {
            cases = Cases(self);
            cases.AddRange(EffectCases());
        }
        catch (Exception e)
        {
            Log.Error($"csgenericsperf setup: {e}");
            Log.Info("csgenericsperf END cases=0 errors=1");
            return;
        }

        double empty = 0;
        foreach (Case c in cases)
        {
            Measure(c, empty, out double hot);
            if (c.Name == "baseline/empty_loop") empty = hot;
        }
        Log.Info($"csgenericsperf END cases={cases.Count} errors={errors} sink={sink & 1}");
    }

    static T Keep<T>(T value) where T : IGameObject
    {
        kept.Add(new GameHandle(value.Pointer));
        return value;
    }

    static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;

    static void Measure(Case c, double empty, out double hot)
    {
        hot = 0;
        try
        {
            long t0 = Stopwatch.GetTimestamp();
            sink += c.Loop(1);
            double cold = Seconds(Stopwatch.GetTimestamp() - t0) * 1e6;

            sink += c.Loop(Math.Min(c.N, 64));

            var runs = new double[Rounds];
            for (int r = 0; r < Rounds; r++)
            {
                long start = Stopwatch.GetTimestamp();
                sink += c.Loop(c.N);
                runs[r] = Seconds(Stopwatch.GetTimestamp() - start) * 1e9 / c.N;
            }
            Array.Sort(runs);
            hot = Math.Max(0, runs[Rounds / 2] - empty);
            double min = Math.Max(0, runs[0] - empty);

            long before = GC.GetAllocatedBytesForCurrentThread();
            sink += c.Loop(c.N);
            double alloc = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)c.N;

            Log.Info($"csgenericsperf case={c.Name} cold_us={cold:0.0} hot_ns={hot:0.0} min_ns={min:0.0} alloc_b={alloc:0.0} n={c.N}");
        }
        catch (Exception e)
        {
            errors++;
            Log.Info($"csgenericsperf case={c.Name} ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    static List<Case> Cases(Player player)
    {
        const int N = 20000, Slow = 4000;
        UnifiedRandom rand = Main.rand;
        Preferences config = Main.Configuration;
        NetManager net = NetManager.Instance;
        CreativePowerManager powers = CreativePowerManager.Instance;
        var repository = new AssetRepository(Main.Assets.Pointer);

        GameArray<int> ints = Keep<GameArray<int>>(new[] { 3, 1, 2, 5, 4 });
        GameArray<short> shorts = Keep<GameArray<short>>(new short[] { 3, 1, 2 });
        GameArray<ushort> ushorts = Keep<GameArray<ushort>>(new ushort[] { 3, 1, 2 });
        GameArray<float> floats = Keep<GameArray<float>>(new[] { 1f, 3f, 2f });
        GameArray<double> doubles = Keep<GameArray<double>>(new[] { 1d, 3d, 2d });
        GameArray<byte> bytes = Keep<GameArray<byte>>(new byte[] { 1, 3, 2 });
        GameArray<bool> bools = Keep<GameArray<bool>>(new[] { true, false, true });
        GameArray<Color> colors = Keep<GameArray<Color>>(new[] { new Color(255, 0, 0), new Color(0, 255, 0) });
        GameArray<Point> points = Keep<GameArray<Point>>(new[] { new Point(1, 2), new Point(3, 4) });
        GameArray<WorldItem> items = Main.item;
        GameArray<ItemID.BannerEffect> banners = ItemID.Sets.BannerStrength;
        WorldItem firstItem = items[0];
        int[] managedInts = { 3, 1, 2, 5, 4 };

        return new List<Case>
        {
            new("baseline/empty_loop", N, n => { long s = 0; for (int k = 0; k < n; k++) s += k; return s; }),
            new("baseline/static_ToInt", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.ToInt(true); return s; }),
            new("baseline/instance_CountBuffs", N, n => { long s = 0; for (int k = 0; k < n; k++) s += player.CountBuffs(); return s; }),
            new("baseline/instance_Contains_string", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += config.Contains("BL_GenericsPerf_Missing") ? 1 : 0; return s; }),
            new("baseline/fast_rand_Next", N, n => { long s = 0; for (int k = 0; k < n; k++) s += rand.Next(5); return s; }),
            new("baseline/managed_pick_GameArray", N, n => { long s = 0; for (int k = 0; k < n; k++) s += ints[rand.Next(ints.Length)]; return s; }),
            new("baseline/managed_pick_int_array", N, n => { long s = 0; for (int k = 0; k < n; k++) s += managedInts[rand.Next(managedInts.Length)]; return s; }),
            new("baseline/managed_Clamp_int", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.Clamp(k, 10, 100); return s; }),
            new("baseline/IsReference_int", N, n => { long s = 0; for (int k = 0; k < n; k++) s += GameGeneric.IsReference<int>() ? 1 : k; return s; }),
            new("baseline/IsReference_WorldItem", N, n => { long s = 0; for (int k = 0; k < n; k++) s += GameGeneric.IsReference<WorldItem>() ? k : 1; return s; }),
            new("baseline/int_array_to_GameArray", Slow, n => { long s = 0; for (int k = 0; k < n; k++) { GameArray<int> a = managedInts; s += a.Length; } return s; }),

            new("SelectRandom/Color_branch1", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.SelectRandom(rand, colors).R; return s; }),
            new("SelectRandom/Point_branch2", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.SelectRandom(rand, points).X; return s; }),
            new("SelectRandom/short_branch3", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.SelectRandom(rand, shorts); return s; }),
            new("SelectRandom/int_branch4", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.SelectRandom(rand, ints); return s; }),
            new("SelectRandom/float_branch5", N, n => { long s = 0; for (int k = 0; k < n; k++) s += (long)Utils.SelectRandom(rand, floats); return s; }),
            new("SelectRandom/ushort_branch6", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.SelectRandom(rand, ushorts); return s; }),
            new("SelectRandom/WorldItem_shared", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.SelectRandom(rand, items).Pointer != 0 ? 1 : 0; return s; }),
            new("SelectRandom/int_from_int_array", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.SelectRandom<int>(rand, managedInts); return s; }),
            new("SelectRandom/alternate_int_float", N, n => { long s = 0; for (int k = 0; k < n; k++) s += (k & 1) == 0 ? Utils.SelectRandom(rand, ints) : (long)Utils.SelectRandom(rand, floats); return s; }),

            new("NextFromList/short", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.NextFromList(rand, shorts); return s; }),
            new("NextFromList/int", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.NextFromList(rand, ints); return s; }),
            new("NextFromList/ushort", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.NextFromList(rand, ushorts); return s; }),
            new("NextFromList/WorldItem_shared", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.NextFromList(rand, items).Pointer != 0 ? 1 : 0; return s; }),

            new("IndexInRange/bool", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.IndexInRange(bools, k & 3) ? 1 : 0; return s; }),
            new("IndexInRange/float", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.IndexInRange(floats, k & 3) ? 1 : 0; return s; }),
            new("IndexInRange/BannerEffect", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.IndexInRange(banners, k & 3) ? 1 : 0; return s; }),
            new("IndexInRange/WorldItem_shared", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.IndexInRange(items, k & 3) ? 1 : 0; return s; }),

            new("Count/bool_T_param", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.Count(bools, true); return s; }),
            new("Count/WorldItem_shared_T_param", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.Count(items, firstItem); return s; }),

            new("Max/byte", N, n => { long s = 0; for (int k = 0; k < n; k++) s += Utils.Max(bytes); return s; }),
            new("Max/double_return", N, n => { long s = 0; for (int k = 0; k < n; k++) s += (long)Utils.Max(doubles); return s; }),
            new("Max/float_return", N, n => { long s = 0; for (int k = 0; k < n; k++) s += (long)Utils.Max(floats); return s; }),

            new("PreferencesGet/bool", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += config.Get("BL_GenericsPerf_Missing", true) ? 1 : 0; return s; }),
            new("PreferencesGet/int", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += config.Get("BL_GenericsPerf_Missing", 7); return s; }),
            new("PreferencesGet/float", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += (long)config.Get("BL_GenericsPerf_Missing", 2.5f); return s; }),
            new("PreferencesGet/GameString_shared", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += config.Get("BL_GenericsPerf_Missing", default(GameString)).Length; return s; }),
            new("PreferencesGetRef/bool", Slow, n => { long s = 0; bool v = true; for (int k = 0; k < n; k++) { config.Get("BL_GenericsPerf_Missing", ref v); s += v ? 1 : 0; } return s; }),
            new("PreferencesGetRef/int", Slow, n => { long s = 0; int v = 3; for (int k = 0; k < n; k++) { config.Get("BL_GenericsPerf_Missing", ref v); s += v; } return s; }),
            new("PreferencesGetRef/GameString_shared", Slow, n => { long s = 0; GameString v = default; for (int k = 0; k < n; k++) { config.Get("BL_GenericsPerf_Missing", ref v); s += v.Length; } return s; }),

            new("NoTParams/GetModule_NetTextModule", N, n => { long s = 0; for (int k = 0; k < n; k++) s += net.GetModule<NetTextModule>().Pointer != 0 ? 1 : 0; return s; }),
            new("NoTParams/GetModule_NetLiquidModule", N, n => { long s = 0; for (int k = 0; k < n; k++) s += net.GetModule<NetLiquidModule>().Pointer != 0 ? 1 : 0; return s; }),
            new("NoTParams/GetModule_alternate", N, n => { long s = 0; for (int k = 0; k < n; k++) s += ((k & 1) == 0 ? net.GetModule<NetTextModule>() : net.GetModule<NetLiquidModule>()).Pointer != 0 ? 1 : 0; return s; }),
            new("NoTParams/GetId_ushort_return", N, n => { long s = 0; for (int k = 0; k < n; k++) s += net.GetId<NetTextModule>(); return s; }),
            new("NoTParams/GetPower_T_return", N, n => { long s = 0; for (int k = 0; k < n; k++) s += powers.GetPower<CreativePowers.GodmodePower>().Pointer != 0 ? 1 : 0; return s; }),
            new("NoTParams/GetPowerId", N, n => { long s = 0; for (int k = 0; k < n; k++) s += powers.GetPowerId<CreativePowers.GodmodePower>(); return s; }),

            new("OutT/TryGet_shared", N, n => { long s = 0; for (int k = 0; k < n; k++) s += TileEntity.TryGet(-12345, out TileEntity e) || !e.IsNull ? 1 : 0; return s; }),
            new("OutT/TryGetAt_shared", N, n => { long s = 0; for (int k = 0; k < n; k++) s += TileEntity.TryGetAt(1, 1, out TileEntity e) || !e.IsNull ? 1 : 0; return s; }),

            new("AssetT/Request_Texture2D_game", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += repository.Request<Texture2D>("Images\\Item_1", AssetRequestMode.ImmediateLoad).Name.Length; return s; }),
            new("AssetT/ModContent_Request_vanilla", Slow, n => { long s = 0; for (int k = 0; k < n; k++) s += ModContent.Request<Texture2D>("Terraria/Images/Item_1", AssetRequestMode.ImmediateLoad).Name.Length; return s; }),
            new("AssetT/TextureAssets_Item_index", N, n => { long s = 0; for (int k = 0; k < n; k++) s += TextureAssets.Item[1].Name.Length; return s; }),

            new("Error/missing_instance_double", 200, n => { long s = 0; for (int k = 0; k < n; k++) { try { s += (long)config.Get("BL_GenericsPerf_Missing", 1.5d); } catch (NotSupportedException) { s++; } } return s; }),
        };
    }

    static List<Case> EffectCases()
    {
        const int N = 4000, Few = 400, Heavy = 4;
        var objectClass = new GameClass("System", "Object");
        GameArray<GameObject> noInputs = Keep(GameArray<GameObject>.New(objectClass, 0));
        GameArray<GameObject> scratch = Keep(GameArray<GameObject>.New(objectClass, 8));
        GameArray<WorldItem> items = Main.item;
        WorldItem a = items[0], b = items[1];
        GameObject text = Keep(new GameObject(GameString.New("value")));
        SetFactory factory = Keep(SetFactory.New(16));
        var pool = new ParticlePool(ParticleOrchestrator._poolPrettySparkle.Pointer);
        var repository = new AssetRepository(Main.Assets.Pointer);
        GameArray<LocalizedText> names = Keep(GameArray<LocalizedText>.New(LocalizedText.Class, ItemID.Count + 16));
        DeepCloneContext lookup = Keep(DeepCloneContext.New());
        lookup.AddClone(a, b);

        return new List<Case>
        {
            new("EffectBaseline/SetFactory_New16", N, n => { long s = 0; for (int k = 0; k < n; k++) s += SetFactory.New(16).Pointer != 0 ? 1 : 0; return s; }),
            new("EffectBaseline/ChatCommandProcessor_New", Few, n => { long s = 0; for (int k = 0; k < n; k++) s += ChatCommandProcessor.New().Pointer != 0 ? 1 : 0; return s; }),
            new("EffectBaseline/ChatMessage_New", Few, n => { long s = 0; for (int k = 0; k < n; k++) s += ChatMessage.New("bl").Pointer != 0 ? 1 : 0; return s; }),
            new("EffectBaseline/DeepCloneContext_New", Few, n => { long s = 0; for (int k = 0; k < n; k++) s += DeepCloneContext.New().Pointer != 0 ? 1 : 0; return s; }),

            new("CreateCustomSet/Color", N, n => { long s = 0; for (int k = 0; k < n; k++) s += factory.CreateCustomSet(new Color(1, 2, 3), noInputs).Length; return s; }),
            new("CreateCustomSet/Vector2_hfa", N, n => { long s = 0; for (int k = 0; k < n; k++) s += factory.CreateCustomSet(new Vector2(1f, 2f), noInputs).Length; return s; }),
            new("CreateCustomSet/float", N, n => { long s = 0; for (int k = 0; k < n; k++) s += factory.CreateCustomSet(1.5f, noInputs).Length; return s; }),
            new("CreateCustomSet/PlacementDetails", N, n => { long s = 0; for (int k = 0; k < n; k++) s += factory.CreateCustomSet(default(PlacementDetails), noInputs).Length; return s; }),
            new("CreateCustomSet/IncludeCapeFrontAndBackInfo", N, n => { long s = 0; for (int k = 0; k < n; k++) s += factory.CreateCustomSet(default(ArmorIDs.Body.Sets.IncludeCapeFrontAndBackInfo), noInputs).Length; return s; }),
            new("CreateCustomSet/BannerEffect", N, n => { long s = 0; for (int k = 0; k < n; k++) s += factory.CreateCustomSet(default(ItemID.BannerEffect), noInputs).Length; return s; }),
            new("CreateCustomSet/object_shared", N, n => { long s = 0; for (int k = 0; k < n; k++) s += factory.CreateCustomSet(text, noInputs).Length; return s; }),
            new("CreateCustomSet/object_then_Recycle", N, n => { long s = 0; for (int k = 0; k < n; k++) { GameArray<GameObject> set = factory.CreateCustomSet(text, noInputs); s += set.Length; factory.Recycle(set); } return s; }),

            new("Shift/object_scratch_array", N, n => { long s = 0; for (int k = 0; k < n; k++) { Utils.Shift(scratch, 3); s += scratch.Length; } return s; }),

            new("Particle/Request_then_Release", N, n => { long s = 0; for (int k = 0; k < n; k++) { PrettySparkleParticle p = pool.RequestParticle<PrettySparkleParticle>(); pool.ReleaseParticle(p); s += p.Pointer != 0 ? 1 : 0; } return s; }),

            new("Packet/CreatePacket_then_Recycle", Few, n => { long s = 0; for (int k = 0; k < n; k++) { NetPacket packet = NetModule.CreatePacket<NetTextModule>(64); s += packet.Length; packet.Recycle(); } return s; }),

            new("Chat/ChatCommandId_FromType", N, n => { long s = 0; for (int k = 0; k < n; k++) s += ChatCommandId.FromType<SayChatCommand>().GetHashCode() & 1; return s; }),
            new("Chat/new_processor_AddCommand", Few, n => { long s = 0; for (int k = 0; k < n; k++) s += ChatCommandProcessor.New().AddCommand<SayChatCommand>().Pointer != 0 ? 1 : 0; return s; }),
            new("Chat/new_processor_AddDefaultCommand", Few, n => { long s = 0; for (int k = 0; k < n; k++) s += ChatCommandProcessor.New().AddDefaultCommand<SayChatCommand>().Pointer != 0 ? 1 : 0; return s; }),
            new("Chat/new_message_SetCommand", Few, n => { long s = 0; for (int k = 0; k < n; k++) { ChatMessage m = ChatMessage.New("bl"); m.SetCommand<SayChatCommand>(); s += m.Pointer != 0 ? 1 : 0; } return s; }),

            new("Assert/one_type_arg", N, n => { long s = 0; for (int k = 0; k < n; k++) { Invariant.Assert(true, "bl", a); s++; } return s; }),
            new("Assert/two_type_args", N, n => { long s = 0; for (int k = 0; k < n; k++) { Invariant.Assert(true, "bl", a, text); s++; } return s; }),
            new("Assert/three_type_args", N, n => { long s = 0; for (int k = 0; k < n; k++) { Invariant.Assert(true, "bl", a, text, b); s++; } return s; }),

            new("Clone/ShallowClone_WorldItem", Few, n => { long s = 0; for (int k = 0; k < n; k++) s += DeepCloning.ShallowClone(a).Pointer != 0 ? 1 : 0; return s; }),
            new("Clone/AddImmutableType_repeat", N, n => { long s = 0; for (int k = 0; k < n; k++) { DeepCloning.AddImmutableType<WorldItem>(); s++; } return s; }),
            new("Clone/new_context_AddClone", Few, n => { long s = 0; for (int k = 0; k < n; k++) { DeepCloneContext c = DeepCloneContext.New(); c.AddClone(a, b); s += c.Pointer != 0 ? 1 : 0; } return s; }),
            new("Clone/TryGetClone_out", N, n => { long s = 0; for (int k = 0; k < n; k++) s += lookup.TryGetClone(a, out WorldItem c) && c.Pointer == b.Pointer ? 1 : 0; return s; }),

            new("Assets/EnsureReloadActionExistsForType", N, n => { long s = 0; for (int k = 0; k < n; k++) { repository.EnsureReloadActionExistsForType<Texture2D>(); s++; } return s; }),

            new("IdDictionary/Create_ItemID_short", Heavy, n => { long s = 0; for (int k = 0; k < n; k++) s += IdDictionary.Create<ItemID, short>().Pointer != 0 ? 1 : 0; return s; }),
            new("IdDictionary/Create_BuffID_int", Heavy, n => { long s = 0; for (int k = 0; k < n; k++) s += IdDictionary.Create<BuffID, int>().Pointer != 0 ? 1 : 0; return s; }),
            new("IdDictionary/Create_TileID_ushort", Heavy, n => { long s = 0; for (int k = 0; k < n; k++) s += IdDictionary.Create<TileID, ushort>().Pointer != 0 ? 1 : 0; return s; }),
            new("Lang/FillNameCacheArray_ItemID_short", Heavy, n => { long s = 0; for (int k = 0; k < n; k++) { Lang.FillNameCacheArray<ItemID, short>("ItemName", names, true); s += names.Length; } return s; }),
        };
    }
}

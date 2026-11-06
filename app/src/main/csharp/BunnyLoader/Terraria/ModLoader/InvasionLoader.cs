using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Chat;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public static class InvasionLoader
{
    internal const byte KindInvasion = 12;

    const byte OpRequest = 0, OpState = 1;

    const int IconId = 3, BarTime = 160;

    static readonly GameClass ObjectClass = new("System", "Object");

    static readonly List<ModInvasion> invasions = new();

    static readonly Dictionary<string, Asset<Texture2D>?> icons = new();

    static readonly HashSet<string> downed = new();

    static readonly HashSet<Mod> systems = new();

    static ModInvasion? active, shown;

    static int wave, progress, progressMax = 1;

    public static IReadOnlyList<ModInvasion> All => invasions;

    public static ModInvasion? Active => active;

    public static int Wave => wave;

    public static int Progress => progress;

    public static int ProgressMax => progressMax;

    internal static InvasionWave? CurrentWave => active != null && wave < active.Waves.Count ? active.Waves[wave] : null;

    static bool IsClient => Main.netMode == NetmodeID.MultiplayerClient;

    static bool IsServer => (Main.netMode & NetmodeID.Server) != 0;

    internal static void Register(ModInvasion invasion)
    {
        invasion.Type = invasions.Count;
        invasions.Add(invasion);
        if (invasions.Count == 1)
        {
            invasion.Mod.AddContent(new InvasionNPC());
            invasion.Mod.AddContent(new InvasionScene());
            InstallHooks();
        }
        if (systems.Add(invasion.Mod))
            invasion.Mod.AddContent(new InvasionSystem());
    }

    public static bool IsDowned(ModInvasion invasion) => downed.Contains(invasion.FullName);

    static bool Blocked() => active != null || Main.invasionType > 0 || Main.pumpkinMoon || Main.snowMoon || DD2Event.Ongoing;

    public static bool CanStart(ModInvasion invasion) => !Blocked() && Guard(invasion, "CanStart", invasion.CanStart, false);

    public static bool Start(ModInvasion invasion)
    {
        if (IsClient)
        {
            var writer = Packet(OpRequest);
            writer.Write(invasion.FullName);
            Send(writer, -1);
            return true;
        }
        if (Blocked())
        {
            Broadcast(NetworkText.FromKey("BunnyLoader.InvasionAlreadyActive", NoArguments()), new Color(255, 240, 20));
            return false;
        }
        if (!Guard(invasion, "CanStart", invasion.CanStart, false))
            return false;
        active = invasion;
        BeginWave(0, true);
        Run(invasion, "OnStart", invasion.OnStart);
        Sync();
        return true;
    }

    static void BeginWave(int index, bool first)
    {
        ModInvasion invasion = active!;
        wave = index;
        progress = 0;
        progressMax = Math.Max(1, invasion.ScalePoints(invasion.Waves[index].Points, PlayerCount()));
        int total = invasion.Waves.Count;
        if (first)
            Broadcast(Message(invasion, "Start", "BunnyLoader.InvasionStart"), invasion.MessageColor);
        if (total > 1)
            Broadcast(Message(invasion, "Wave", "BunnyLoader.InvasionWave", index + 1, total), invasion.MessageColor);
        Run(invasion, "OnWaveStart", () => invasion.OnWaveStart(index));
    }

    public static void AddPoints(int points)
    {
        ModInvasion? invasion = active;
        if (invasion == null || IsClient || points <= 0)
            return;
        progress = Math.Min(progressMax, progress + points);
        if (progress >= progressMax)
        {
            int finished = wave;
            Run(invasion, "OnWaveComplete", () => invasion.OnWaveComplete(finished));
            if (finished + 1 >= invasion.Waves.Count)
            {
                Stop(true);
                return;
            }
            BeginWave(finished + 1, false);
        }
        Sync();
    }

    public static void Stop(bool success)
    {
        ModInvasion? invasion = active;
        if (invasion == null || IsClient)
            return;
        active = null;
        if (success)
        {
            downed.Add(invasion.FullName);
            Broadcast(Message(invasion, "Complete", "BunnyLoader.InvasionComplete"), invasion.MessageColor);
            Run(invasion, "OnComplete", invasion.OnComplete);
        }
        else
        {
            Broadcast(Message(invasion, "Fail", "BunnyLoader.InvasionFail"), invasion.MessageColor);
            Run(invasion, "OnFail", invasion.OnFail);
        }
        Sync();
    }

    internal static void OnKill(NPC npc)
    {
        ModInvasion? invasion = active;
        if (invasion == null || IsClient || npc.SpawnedFromStatue || npc.friendly || CurrentWave is not InvasionWave current)
            return;
        int points = Guard(invasion, "PointsFor", () => invasion.PointsFor(npc, current), 0);
        if (points > 0)
            AddPoints(points);
    }

    internal static void Update()
    {
        ModInvasion? invasion = active;
        if (invasion != null && !IsClient)
        {
            if (Guard(invasion, "ShouldFail", invasion.ShouldFail, false))
                Stop(false);
            else
                Run(invasion, "Update", invasion.Update);
        }
        if (Main.netMode != NetmodeID.Server)
            UpdateBar();
    }

    static void UpdateBar()
    {
        if (active != null)
        {
            shown = active;
            Main.invasionProgress = progress;
            Main.invasionProgressMax = progressMax;
            Main.invasionProgressWave = wave + 1;
            Main.invasionWaveMax = active.Waves.Count;
            Main.invasionProgressIcon = IconId;
            Main.invasionProgressDisplayLeft = BarTime;
            return;
        }
        if (shown == null)
            return;
        if (Main.invasionProgressIcon == IconId && Main.invasionProgressDisplayLeft > 0)
            Main.invasionProgressDisplayLeft = 0;
        if (Main.invasionProgressAlpha <= 0)
            shown = null;
    }

    static int PlayerCount()
    {
        int count = 0;
        for (int k = 0; k < Main.maxPlayers; k++)
            if (Main.player[k].active)
                count++;
        return Math.Max(1, count);
    }

    static GameArray<GameObject> NoArguments() => GameArray<GameObject>.New(ObjectClass, 0);

    static NetworkText Message(ModInvasion invasion, string kind, string fallback, params int[] numbers)
    {
        string own = invasion.GetLocalizationKey(kind);
        bool custom = Language.Exists(own);
        GameArray<GameObject> arguments = GameArray<GameObject>.New(ObjectClass, numbers.Length + 1);
        using var pin = new GameHandle(arguments.Pointer);
        arguments[0] = new GameObject(NetworkText.FromKey(invasion.DisplayName.Key, NoArguments()).Pointer);
        for (int k = 0; k < numbers.Length; k++)
            arguments[k + 1] = new GameObject(NetworkText.FromLiteral(numbers[k].ToString()).Pointer);
        return NetworkText.FromKey(custom ? own : fallback, arguments);
    }

    static void Broadcast(NetworkText text, Color color)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            Main.NewText(text.ToString(), color.R, color.G, color.B);
        else
            ChatHelper.BroadcastChatMessage(text, color, -1);
    }

    static void Run(ModInvasion invasion, string hook, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{invasion.FullName}.{hook}", e);
        }
    }

    static T Guard<T>(ModInvasion invasion, string hook, Func<T> action, T fallback)
    {
        try
        {
            return action();
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{invasion.FullName}.{hook}", e);
            return fallback;
        }
    }

    static BinaryWriter Packet(byte op)
    {
        var writer = new BinaryWriter(new MemoryStream());
        writer.Write(KindInvasion);
        writer.Write(op);
        return writer;
    }

    static void Send(BinaryWriter writer, int toClient)
    {
        writer.Flush();
        var stream = (MemoryStream)writer.BaseStream;
        if (!ModNet.Send(stream.GetBuffer(), (int)stream.Length, toClient, -1))
            Log.Error("ModInvasion: the state packet was not sent");
    }

    internal static void Sync()
    {
        if (!IsServer || Main.netMode == NetmodeID.SinglePlayer)
            return;
        BinaryWriter writer = Packet(OpState);
        WriteState(writer);
        Send(writer, -1);
    }

    internal static void WriteState(BinaryWriter writer)
    {
        writer.Write(active?.FullName ?? "");
        writer.Write(wave);
        writer.Write(progress);
        writer.Write(progressMax);
        writer.Write(downed.Count);
        foreach (string name in downed)
            writer.Write(name);
    }

    internal static void ReadState(BinaryReader reader)
    {
        string name = reader.ReadString();
        active = invasions.FirstOrDefault(i => i.FullName == name);
        wave = reader.ReadInt32();
        progress = reader.ReadInt32();
        progressMax = Math.Max(1, reader.ReadInt32());
        downed.Clear();
        int count = reader.ReadInt32();
        for (int k = 0; k < count; k++)
            downed.Add(reader.ReadString());
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        byte op = reader.ReadByte();
        if (op == OpRequest)
        {
            string name = reader.ReadString();
            if (IsServer && invasions.FirstOrDefault(i => i.FullName == name) is ModInvasion invasion)
                Start(invasion);
            return;
        }
        if (op == OpState && IsClient)
            ReadState(reader);
    }

    internal static void Save(Mod mod, TagCompound tag)
    {
        if (active?.Mod == mod)
            tag["active"] = new TagCompound
            {
                ["name"] = active.Name,
                ["wave"] = wave,
                ["progress"] = progress,
                ["max"] = progressMax,
            };
        List<string> mine = invasions.Where(i => i.Mod == mod && downed.Contains(i.FullName)).Select(i => i.Name).ToList();
        if (mine.Count > 0)
            tag["downed"] = mine;
    }

    internal static void Load(Mod mod, TagCompound tag)
    {
        foreach (string name in tag.GetList<string>("downed"))
            downed.Add($"{mod.Name}/{name}");
        if (!tag.ContainsKey("active"))
            return;
        var saved = tag.Get<TagCompound>("active");
        string wanted = saved.GetString("name");
        ModInvasion? invasion = invasions.FirstOrDefault(i => i.Mod == mod && i.Name == wanted);
        int savedWave = saved.GetInt("wave");
        if (invasion == null || savedWave >= invasion.Waves.Count)
            return;
        active = invasion;
        wave = savedWave;
        progress = saved.GetInt("progress");
        progressMax = Math.Max(1, saved.GetInt("max"));
    }

    internal static void Clear()
    {
        active = null;
        shown = null;
        wave = 0;
        progress = 0;
        progressMax = 1;
        downed.Clear();
    }

    static Texture2D? Icon(ModInvasion invasion)
    {
        if (!icons.TryGetValue(invasion.FullName, out Asset<Texture2D>? asset))
        {
            try
            {
                asset = ModContent.Request<Texture2D>(invasion.IconTexture);
            }
            catch (Exception e)
            {
                Log.Warn($"{invasion.FullName}: the icon could not be loaded: {e.Message}");
            }
            icons[invasion.FullName] = asset;
        }
        return asset?.Value;
    }

    static void InstallHooks()
    {
        On_GUIEvents.DrawInvasionInfo += DrawInvasionInfo;
        On_Main.CanStartInvasion += CanStartInvasion;
        On_Main.StartInvasion += StartInvasion;
        On_Main.startPumpkinMoon += StartPumpkinMoon;
        On_Main.startSnowMoon += StartSnowMoon;
        On_DD2Event.StartInvasion += StartOldOnesArmy;
    }

    static void DrawInvasionInfo(On_GUIEvents.orig_DrawInvasionInfo orig, GUIEvents self, Color panel, string? title, string? waveProgressString, Texture2D icon, float progressScale)
    {
        ModInvasion? invasion = shown;
        if (invasion == null || Main.invasionProgressIcon != IconId || DD2Event.Ongoing)
        {
            orig(self, panel, title, waveProgressString, icon, progressScale);
            return;
        }
        Texture2D? own = Icon(invasion);
        orig(self, panel, invasion.DisplayName.Value, waveProgressString, own ?? icon, progressScale);
    }

    static bool CanStartInvasion(On_Main.orig_CanStartInvasion orig, int type, bool ignoreDelay) => active == null && orig(type, ignoreDelay);

    static void StartInvasion(On_Main.orig_StartInvasion orig, int type)
    {
        if (active == null)
            orig(type);
    }

    static void StartPumpkinMoon(On_Main.orig_startPumpkinMoon orig)
    {
        if (active == null)
            orig();
    }

    static void StartSnowMoon(On_Main.orig_startSnowMoon orig)
    {
        if (active == null)
            orig();
    }

    static void StartOldOnesArmy(On_DD2Event.orig_StartInvasion orig, int difficultyOverride)
    {
        if (active == null)
            orig(difficultyOverride);
    }

    sealed class InvasionNPC : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (active == null || !Guard(active, "CanSpawn", () => active.CanSpawn(player), false))
                return;
            spawnRate = active.SpawnRate;
            maxSpawns = active.MaxSpawns;
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (active == null || CurrentWave is not InvasionWave current || current.Enemies.Length == 0 || !Guard(active, "CanSpawn", () => active.CanSpawn(spawnInfo.Player), false))
                return;
            pool.Clear();
            foreach (InvasionEnemy enemy in current.Enemies)
                pool[enemy.Type] = enemy.Weight;
        }

        public override void OnKill(NPC npc) => InvasionLoader.OnKill(npc);
    }

    sealed class InvasionScene : ModSceneEffect
    {
        public override int Music => active?.Music ?? -1;

        public override SceneEffectPriority Priority => SceneEffectPriority.Event;

        public override bool IsSceneEffectActive(Player player) => active != null && active.Music >= 0;
    }

    sealed class InvasionSystem : ModSystem
    {
        bool First => systems.First() == Mod;

        public override void PostUpdateEverything()
        {
            if (First)
                InvasionLoader.Update();
        }

        public override void SaveWorldData(TagCompound tag) => InvasionLoader.Save(Mod, tag);

        public override void LoadWorldData(TagCompound tag) => InvasionLoader.Load(Mod, tag);

        public override void ClearWorld()
        {
            if (First)
                InvasionLoader.Clear();
        }

        public override void NetSend(BinaryWriter writer)
        {
            if (First)
                WriteState(writer);
        }

        public override void NetReceive(BinaryReader reader)
        {
            if (First)
                ReadState(reader);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace Terraria.ID;

public readonly partial struct SetFactory
{
    public sealed class NamedSetKey
    {
        readonly SetFactory factory;

        internal readonly string fullKey;

        internal string? description;

        internal NamedSetKey(SetFactory factory, string fullKey)
        {
            if (fullKey.Contains(' ')) throw new ArgumentException("Set names may not contain spaces");
            this.factory = factory;
            this.fullKey = fullKey.Contains('/') ? fullKey : $"{reinitializing?.Name ?? "Terraria"}/{fullKey}";
        }

        public NamedSetKey Description(string description)
        {
            this.description = description;
            return this;
        }

        public T[] RegisterCustomSet<T>(T defaultState, params object[] inputs)
        {
            T[] set = factory.NewSet(defaultState);
            for (int i = 0; i + 1 < inputs.Length; i += 2)
                set[Convert.ToInt32(inputs[i])] = (T)inputs[i + 1];
            return factory.RegisterNamed(this, defaultState, set);
        }

        public float[] RegisterFloatSet(float defaultState, params float[] inputs)
        {
            float[] set = factory.NewSet(defaultState);
            for (int i = 0; i + 1 < inputs.Length; i += 2)
                set[(int)inputs[i]] = inputs[i + 1];
            return factory.RegisterNamed(this, defaultState, set);
        }

        public ushort[] RegisterUshortSet(ushort defaultState, params ushort[] inputs)
        {
            ushort[] set = factory.NewSet(defaultState);
            for (int i = 0; i + 1 < inputs.Length; i += 2)
                set[inputs[i]] = inputs[i + 1];
            return factory.RegisterNamed(this, defaultState, set);
        }

        public int[] RegisterIntSet(int defaultState, params int[] inputs)
        {
            int[] set = factory.NewSet(defaultState);
            for (int i = 0; i + 1 < inputs.Length; i += 2)
                set[inputs[i]] = inputs[i + 1];
            return factory.RegisterNamed(this, defaultState, set);
        }

        public int[] RegisterIntSet(params int[] types) => RegisterIntSet(-1, types);

        public bool[] RegisterBoolSet(params int[] types) => RegisterBoolSet(false, types);

        public bool[] RegisterBoolSet(bool defaultState, params int[] types)
        {
            bool[] set = factory.NewSet(defaultState);
            foreach (int type in types)
                set[type] = !defaultState;
            return factory.RegisterNamed(this, defaultState, set);
        }
    }

    static readonly ConcurrentDictionary<(nint Factory, string Key, Type Type), Array> namedSets = new();

    static bool grown;

    static Mod? reinitializing;

    internal static void GrowToContent()
    {
        ItemID.Sets.Factory._size = ItemLoader.ItemCount;
        ProjectileID.Sets.Factory._size = ProjectileLoader.ProjectileCount;
        NPCID.Sets.Factory._size = NPCLoader.NPCCount;
        BuffID.Sets.Factory._size = BuffLoader.BuffCount;
        TileID.Sets.Factory._size = TileLoader.TileCount;
        WallID.Sets.Factory._size = WallLoader.WallCount;
        grown = true;
    }

    internal static void ReinitializeSets(Mod mod)
    {
        reinitializing = mod;
        try
        {
            foreach (Type type in mod.GetType().Assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<ReinitializeDuringResizeArraysAttribute>() != null)
                .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
                Reinitialize(type);
        }
        finally
        {
            reinitializing = null;
        }
    }

    static void Reinitialize(Type type)
    {
        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        type.TypeInitializer?.Invoke(null, null);
        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            if (!nested.ContainsGenericParameters)
                Reinitialize(nested);
    }

    T[] NewSet<T>(T defaultState)
    {
        var set = new T[_size];
        if (!EqualityComparer<T>.Default.Equals(defaultState, default!))
            Array.Fill(set, defaultState);
        return set;
    }

    T[] RegisterNamed<T>(NamedSetKey key, T defaultState, T[] input)
    {
        if (!grown)
            return input;
        var id = (Pointer, key.fullKey, typeof(T));
        if (!namedSets.TryGetValue(id, out Array? existing) || ((T[])existing).Length != input.Length)
        {
            namedSets[id] = input;
            return input;
        }

        var shared = (T[])existing;
        for (int i = 0; i < input.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(input[i], defaultState))
                shared[i] = input[i];
        return shared;
    }

    public NamedSetKey CreateNamedSet(string fullKey) => new(this, fullKey);

    public NamedSetKey CreateNamedSet(string modName, string key) => new(this, $"{modName}/{key}");

    public NamedSetKey CreateNamedSet(Mod mod, string key) => new(this, $"{mod.Name}/{key}");

    public GameArray<int> CreateIntSet(int defaultState, params int[] inputs)
    {
        var values = GameArrayBuilder.Create<int>(inputs);
        using var pin = new GameHandle(values.Pointer);
        return CreateIntSet(defaultState, values);
    }
}

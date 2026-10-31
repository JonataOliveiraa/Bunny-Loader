using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.GameContent.Personalities;

namespace Terraria.ModLoader;

public readonly struct NPCHappiness
{
    public readonly int NpcType;
    NPCHappiness(int npcType) => NpcType = npcType;
    public static NPCHappiness Get(int npcType) => new(npcType);

    public NPCHappiness SetNPCAffection<T>(AffectionLevel affectionLevel) where T : ModNPC =>
        SetNPCAffection(ModContent.GetInstance<T>().Type, affectionLevel);

    public NPCHappiness SetNPCAffection(int npcId, AffectionLevel affectionLevel)
    {
        var database = Main.ShopHelper._database;
        var profile = database.GetByNPCID(NpcType);
        if (!profile.IsNull && profile != database._trashEntry)
        {
            var list = new GameList(profile.ShopModifiers);
            for (int i = 0; i < list.Count; i++)
            {
                var value = new GameObject(list.Get<IntPtr>(i));
                if (!NPCPreferenceTrait.Is(value)) continue;
                var entry = new NPCPreferenceTrait(value.Pointer);
                if (entry.NpcId != npcId) continue;
                if (affectionLevel == 0) list.RemoveAt(i);
                else entry.Level = affectionLevel;
                return this;
            }
        }
        if (affectionLevel == 0) return this;
        var trait = NPCPreferenceTrait.New();
        trait.NpcId = npcId;
        trait.Level = affectionLevel;
        database.Register(NpcType, trait);
        return this;
    }

    public NPCHappiness SetBiomeAffection<T>(AffectionLevel affectionLevel) where T : struct, IGameObject
    {
        var factory = typeof(T).GetMethod("New", Type.EmptyTypes) ?? throw new ArgumentException($"{typeof(T).Name} is not a native shopping biome");
        if (factory.Invoke(null, null) is not IGameObject biome) throw new InvalidOperationException("Shopping biome construction failed");
        if (!AShoppingBiome.Is(new GameObject(biome.Pointer))) throw new ArgumentException($"{typeof(T).Name} is not a native shopping biome");
        return SetBiomeAffection(new AShoppingBiome(biome.Pointer), affectionLevel);
    }

    public unsafe NPCHappiness SetBiomeAffection(AShoppingBiome biome, AffectionLevel affectionLevel)
    {
        if (biome.IsNull) throw new ArgumentException("Shopping biome cannot be null", nameof(biome));
        var database = Main.ShopHelper._database;
        var profile = database.GetByNPCID(NpcType);
        BiomePreferenceListTrait trait = default;
        if (!profile.IsNull && profile != database._trashEntry)
        {
            var list = new GameList(profile.ShopModifiers);
            for (int i = 0; i < list.Count; i++)
            {
                var value = new GameObject(list.Get<IntPtr>(i));
                if (!BiomePreferenceListTrait.Is(value)) continue;
                var candidate = new BiomePreferenceListTrait(value.Pointer);
                var preferences = new GameList(candidate._preferences);
                for (int j = 0; j < preferences.Count; j++)
                {
                    var entry = new BiomePreferenceListTrait.BiomePreference(preferences.Get<IntPtr>(j));
                    if (entry.Biome.IsNull || *(IntPtr*)entry.Biome.Pointer != *(IntPtr*)biome.Pointer) continue;
                    if (affectionLevel == 0) preferences.RemoveAt(j);
                    else entry.Affection = affectionLevel;
                    return this;
                }
                if (trait.IsNull) trait = candidate;
            }
        }
        if (affectionLevel == 0) return this;
        if (trait.IsNull)
        {
            trait = BiomePreferenceListTrait.New();
            database.Register(NpcType, trait);
        }
        trait.Add(affectionLevel, biome);
        return this;
    }
}

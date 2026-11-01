using System;
using System.Collections.Generic;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class SceneEffectLoader
{
    public const int VanillaSceneEffectCount = 0;

    public class SceneEffectInstance
    {
        public struct PrioritizedPair
        {
            public static readonly PrioritizedPair Default = new() { value = -1 };

            public int value;
            public SceneEffectPriority priority;
        }

        public bool anyActive;
        public PrioritizedPair waterStyle;
        public PrioritizedPair undergroundBackground;
        public PrioritizedPair surfaceBackground;
        public PrioritizedPair music;
        public string? mapBackground;
        public ModSceneEffect? mapBackgroundSceneEffect;
        public PrioritizedPair biomeTorchItemType;
        public PrioritizedPair biomeCampfireItemType;

        public SceneEffectInstance()
        {
            waterStyle = undergroundBackground = surfaceBackground = music = biomeTorchItemType = biomeCampfireItemType = PrioritizedPair.Default;
        }
    }

    static readonly List<ModSceneEffect> Effects = new();
    static readonly SceneEffectInstance[] Scenes = new SceneEffectInstance[256];
    static readonly SceneEffectInstance Empty = new();
    static readonly List<(float Weight, int Order, ModSceneEffect Effect)> Ranked = new();
    static readonly Comparison<(float Weight, int Order, ModSceneEffect Effect)> ByWeight =
        (a, b) => b.Weight != a.Weight ? b.Weight.CompareTo(a.Weight) : a.Order.CompareTo(b.Order);

    public static int Count => Effects.Count;

    internal static IReadOnlyList<ModSceneEffect> All => Effects;

    internal static int Register(ModSceneEffect effect)
    {
        Effects.Add(effect);
        BiomeHooks.Install();
        MusicHooks.Install();
        return Effects.Count - 1;
    }

    internal static SceneEffectInstance Of(Player player)
    {
        int who = player.whoAmI;
        return (uint)who < (uint)Scenes.Length ? Scenes[who] ?? Empty : Empty;
    }

    internal static void Clear(int who)
    {
        if ((uint)who < (uint)Scenes.Length) Scenes[who] = null!;
    }

    public static void UpdateSceneEffect(Player player)
    {
        var result = new SceneEffectInstance();
        Ranked.Clear();
        for (int i = 0; i < Effects.Count; i++)
        {
            ModSceneEffect effect = Effects[i];
            bool active;
            try { active = effect.IsSceneEffectActive(player); }
            catch (Exception e)
            {
                ItemHooks.Report($"{effect.FullName}.IsSceneEffectActive", e);
                active = false;
            }
            try { effect.SpecialVisuals(player, active); }
            catch (Exception e) { ItemHooks.Report($"{effect.FullName}.SpecialVisuals", e); }
            if (!active) continue;
            float weight;
            try { weight = effect.GetCorrWeight(player); }
            catch (Exception e)
            {
                ItemHooks.Report($"{effect.FullName}.GetWeight", e);
                continue;
            }
            Ranked.Add((weight, i, effect));
        }
        if (Ranked.Count > 0)
        {
            result.anyActive = true;
            Ranked.Sort(ByWeight);
            foreach (var (_, _, effect) in Ranked)
            {
                try { Fill(result, effect); }
                catch (Exception e) { ItemHooks.Report($"{effect.FullName} (scene)", e); }
            }
        }
        int who = player.whoAmI;
        if ((uint)who < (uint)Scenes.Length) Scenes[who] = result;
    }

    static void Fill(SceneEffectInstance result, ModSceneEffect effect)
    {
        SceneEffectPriority priority = effect.Priority;
        if (result.music.value == -1)
        {
            int music = effect.Music;
            if (music != -1) result.music = new SceneEffectInstance.PrioritizedPair { value = music, priority = priority };
        }
        if (effect is ModBiome biome)
        {
            if (result.biomeTorchItemType.value == -1 && biome.BiomeTorchItemType != -1)
                result.biomeTorchItemType = new SceneEffectInstance.PrioritizedPair { value = biome.BiomeTorchItemType, priority = priority };
            if (result.biomeCampfireItemType.value == -1 && biome.BiomeCampfireItemType != -1)
                result.biomeCampfireItemType = new SceneEffectInstance.PrioritizedPair { value = biome.BiomeCampfireItemType, priority = priority };
        }
        if (result.mapBackground == null && effect.MapBackground is { Length: > 0 } map)
        {
            result.mapBackground = map;
            result.mapBackgroundSceneEffect = effect;
        }
    }

    public static void UpdateMusic(ref int music, ref SceneEffectPriority priority)
    {
        SceneEffectInstance.PrioritizedPair current = Of(Main.LocalPlayer).music;
        if (current.value > -1 && current.priority > priority)
        {
            music = current.value;
            priority = current.priority;
        }
    }
}

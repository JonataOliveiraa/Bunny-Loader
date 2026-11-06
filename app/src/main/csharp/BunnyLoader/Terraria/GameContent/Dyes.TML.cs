using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.Dyes;
using Terraria.ModLoader.Core;

namespace Terraria.Graphics.Shaders
{
    internal static class ShaderHandles
    {
        static readonly List<GameHandle> handles = new();

        internal static void Keep(nint pointer) => handles.Add(new GameHandle(pointer));
    }

    public readonly unsafe partial struct ArmorShaderData
    {
        public ArmorShaderData(ReLogic.Content.Asset<Effect> shader, string passName) : this(New(shader, passName).Pointer)
        {
        }
    }

    public readonly unsafe partial struct ArmorShaderDataSet
    {
        public T BindShader<T>(int itemId, T shaderData) where T : struct, IGameObject
        {
            ShaderHandles.Keep(shaderData.Pointer);
            int id = ++_shaderDataCount;
            new GameList(_shaderData).Add(shaderData.Pointer);
            new GameDictionary(_shaderLookupDictionary).Set(itemId, id);
            return shaderData;
        }
    }

    public readonly unsafe partial struct HairShaderDataSet
    {
        public T BindShader<T>(int itemId, T shaderData) where T : struct, IGameObject
        {
            ShaderHandles.Keep(shaderData.Pointer);
            byte id = ++_shaderDataCount;
            new GameList(_shaderData).Add(shaderData.Pointer);
            new GameDictionary(_shaderLookupDictionary).Set(itemId, (short)id);
            return shaderData;
        }
    }
}

namespace Terraria.GameContent.Dyes
{
    public delegate Color HairColorMethod(Player player, Color color, ref bool lighting);

    public readonly unsafe partial struct LegacyHairShaderData
    {
        static readonly Dictionary<nint, HairColorMethod> methods = new();

        public LegacyHairShaderData() : this(New().Pointer)
        {
        }

        public LegacyHairShaderData UseLegacyMethod(HairColorMethod colorProcessor)
        {
            methods[Pointer] = colorProcessor;
            ItemHooks.Once("LegacyHairColor", () => On_LegacyHairShaderData.GetColor += GetColor);
            return this;
        }

        static Color GetColor(On_LegacyHairShaderData.orig_GetColor orig, LegacyHairShaderData self, Player player, Color lightColor)
        {
            if (!methods.TryGetValue(self.Pointer, out HairColorMethod? method))
                return orig(self, player, lightColor);
            bool lighting = true;
            Color color = method(player, player.hairColor, ref lighting);
            return lighting ? new Color((int)(color.R * lightColor.R / 255f), (int)(color.G * lightColor.G / 255f), (int)(color.B * lightColor.B / 255f), color.A) : color;
        }
    }
}

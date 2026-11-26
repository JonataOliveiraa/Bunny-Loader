using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;

namespace BunnyLoader;

internal static unsafe class FxcGameEffect
{
    static readonly string[] Standard =
    {
        "uColor", "uSecondaryColor", "uOpacity", "uSaturation", "uRotation", "uTime", "uSourceRect", "uWorldPosition", "uDirection",
        "uLightSource", "uImageSize0", "uImageSize1", "uImageSize2", "uTargetPosition", "uLegacyArmorSourceRect", "uLegacyArmorSheetSize",
        "uShaderSpecificData", "uDrawPosition", "uMatrixTransform0", "uImageOffset", "uIntensity", "uProgress", "uScreenResolution",
        "uScreenPosition", "uZoom",
    };

    static readonly string[] TextureBases = { "ArmorGel", "ArmorPhase", "ArmorFog" };

    const string ColorBase = "ArmorColored";

    static readonly Dictionary<string, IntPtr> assets = new(StringComparer.Ordinal);

    static readonly Dictionary<int, IntPtr> bases = new();

    internal static IntPtr CreateAsset(string file, string name)
    {
        lock (assets)
        {
            if (assets.TryGetValue(file, out IntPtr known)) return known;
            Effect effect = Create(FxcEffect.LoadFile(file, name));
            IntPtr template = Asset<Effect>.ToGame(Main.PixelShaderRef);
            IntPtr klass = *(IntPtr*)template;
            IntPtr asset = Host.Native()->NewObject(klass);
            *(IntPtr*)(asset + Offset(klass, "Value")) = effect.Pointer;
            *(int*)(asset + Offset(klass, "<State>k__BackingField")) = (int)AssetState.Loaded;
            *(IntPtr*)(asset + Offset(klass, "<Name>k__BackingField")) = GameString.New(name);
            Host.Native()->GcHandleNew(asset, 0);
            assets[file] = asset;
            return asset;
        }
    }

    static int Offset(IntPtr klass, string field)
    {
        byte* name = Host.Utf8(field);

        try
        {
            int offset = Host.Native()->FieldOffset(klass, name);
            if (offset <= 0) throw new MissingFieldException($"Asset<Effect>: the game's asset has no {field} field");
            return offset;
        }
        finally { Host.FreeUtf8(name); }
    }

    internal static Effect Create(FxcEffect fx)
    {
        Effect effect = Effect.New(Main.instance.GraphicsDevice);

        var names = new List<string>(fx.Parameters.OrderBy(p => p.Index).Select(p => p.Name));
        foreach (string name in Standard)
            if (!names.Contains(name)) names.Add(name);

        EffectParameter[] parameters = names.Select(EffectParameter.New).ToArray();
        effect.Parameters = EffectParameterCollection.New(parameters);

        var registers = new Dictionary<string, int>();
        foreach (FxcEffect.Pass pass in fx.Passes)
            foreach (var sampler in pass.Samplers)
                registers.TryAdd(sampler.Value, sampler.Key);

        lock (samplers)
            foreach (EffectParameter parameter in parameters)
                if (parameter.Name != null && registers.TryGetValue(parameter.Name, out int register))
                    samplers[parameter.Pointer] = (effect.Pointer, register);

        var techniques = new List<EffectTechnique>();
        byte* error = stackalloc byte[1024];

        foreach (IGrouping<int, FxcEffect.Pass> group in fx.Passes.GroupBy(p => p.Technique).OrderBy(g => g.Key))
        {
            var passes = new List<EffectPass>();

            foreach (FxcEffect.Pass pass in group)
            {
                int images = pass.Samplers.Count == 0 ? 1 : pass.Samplers.Keys.Max() + 1;
                if (images > 4) Log.Warn($"fxc: {fx.Name}: pass {pass} samples uImage{images - 1}; sprite shaders get uImage0 to uImage3");

                IntPtr material = Host.Native()->SpriteFxMaterial(BaseMaterial(images > 1 ? 2 : 1));
                if (material == IntPtr.Zero) throw new InvalidOperationException($"fxc: {fx.Name}: the sprite material could not be copied");
                EffectPass gamePass = EffectPass.New(effect, passes.Count, pass.Name, new GameObject(material));
                passes.Add(gamePass);
                if (pass.Error != null) continue;

                error[0] = 0;
                if (Host.Native()->SpriteFxRegister(gamePass.Pointer, fx.NativeId, pass.Technique, pass.Index, error, 1024) <= 0)
                    Log.Warn($"fxc: {fx.Name}: pass {pass} cannot draw sprites: {new string((sbyte*)error)}");
            }

            techniques.Add(EffectTechnique.New(effect, group.First().TechniqueName, EffectPassCollection.New(passes.ToArray())));
        }

        if (techniques.Count == 0) throw new InvalidOperationException($"fxc: {fx.Name} has no technique");
        effect.CurrentTechnique = techniques[0];

        EffectTechniqueCollection collection = EffectTechniqueCollection.New(techniques.ToArray());
        Host.Native()->GcHandleNew(collection.Pointer, 0);
        lock (techniqueSets) techniqueSets[effect.Pointer] = collection;

        Host.Native()->GcHandleNew(effect.Pointer, 0);
        lock (sources) sources[effect.Pointer] = fx;
        return effect;
    }

    static readonly Dictionary<IntPtr, FxcEffect> sources = new();

    internal static FxcEffect? SourceOf(Effect effect)
    {
        lock (sources) return sources.TryGetValue(effect.Pointer, out FxcEffect? fx) ? fx : null;
    }

    static readonly Dictionary<IntPtr, EffectTechniqueCollection> techniqueSets = new();

    static readonly Dictionary<IntPtr, (IntPtr Effect, int Register)> samplers = new();

    internal static EffectTechniqueCollection TechniquesOf(Effect effect)
    {
        lock (techniqueSets)
        {
            if (techniqueSets.TryGetValue(effect.Pointer, out EffectTechniqueCollection known)) return known;
            EffectTechniqueCollection single = EffectTechniqueCollection.New(new[] { effect.CurrentTechnique });
            Host.Native()->GcHandleNew(single.Pointer, 0);
            techniqueSets[effect.Pointer] = single;
            return single;
        }
    }

    internal static void SetTexture(EffectParameter parameter, Texture2D texture)
    {
        (IntPtr Effect, int Register) sampler;
        lock (samplers)
            if (!samplers.TryGetValue(parameter.Pointer, out sampler))
                throw new NotSupportedException($"EffectParameter {parameter.Name}: textures can only be set on the samplers of a mod's .fxc effect");
        if (sampler.Register == 0) return;
        if (sampler.Register > 3)
            throw new NotSupportedException($"EffectParameter {parameter.Name}: sprite shaders take textures in uImage1 to uImage3, not s{sampler.Register}");
        Host.Native()->SpriteFxSetTexture(sampler.Effect, sampler.Register, texture.Pointer);
    }

    static IntPtr BaseMaterial(int textures)
    {
        if (bases.TryGetValue(textures, out IntPtr known)) return known;
        var byName = new Dictionary<string, IntPtr>();
        foreach (EffectPass pass in PixelShader.Effect.CurrentTechnique.Passes._passes)
            if (!pass.IsNull && pass.Name != null) byName.TryAdd(pass.Name, pass._material.Pointer);
        IntPtr found = IntPtr.Zero;
        if (textures == 1) byName.TryGetValue(ColorBase, out found);
        else
        {
            foreach (string name in TextureBases)
                if (byName.TryGetValue(name, out IntPtr material) && Has(material, "_Color") && Has(material, "_SecondaryTex"))
                {
                    found = material;
                    break;
                }
        }

        if (found == IntPtr.Zero) throw new InvalidOperationException($"fxc: the game has no pixel shader to carry a sprite effect with {textures} image(s)");
        bases[textures] = found;
        return found;
    }

    static bool Has(IntPtr material, string property)
    {
        byte* name = Host.Utf8(property);
        try { return Host.Native()->SpriteFxHasProperty(material, name) != 0; }
        finally { Host.FreeUtf8(name); }
    }

    internal static long[] Stats()
    {
        long* values = stackalloc long[16];
        int count = Math.Min(Host.Native()->SpriteFxStats(values, 16), 16);
        long[] result = new long[count];
        for (int i = 0; i < count; i++) result[i] = values[i];
        return result;
    }
}

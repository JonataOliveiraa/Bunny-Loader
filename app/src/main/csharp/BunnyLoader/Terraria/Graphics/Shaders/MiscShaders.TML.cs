using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.Graphics.Shaders
{
    public readonly unsafe partial struct MiscShaderData
    {
        public MiscShaderData(ReLogic.Content.Asset<Effect> shader, string passName) : this(New(shader, passName).Pointer)
        {
            ShaderHandles.Keep(Pointer);
        }

        public void Apply(GameNullable<DataStructures.DrawData> drawData = default)
        {
            Apply_(drawData);
            KeepMatrix();
        }

        public void Apply(Entity entity, GameNullable<DataStructures.DrawData> drawData)
        {
            Apply_(entity, drawData);
            KeepMatrix();
        }

        static readonly Dictionary<nint, EffectParameter> matrixParameters = new();

        void KeepMatrix()
        {
            GameNullable<Microsoft.Xna.Framework.Matrix> transform = _transformMatrix;
            if (!transform.HasValue && !_useProjectionMatrix) return;
            Effect shader = Shader;
            EffectParameter parameter;

            lock (matrixParameters)
                if (!matrixParameters.TryGetValue(shader.Pointer, out parameter))
                    matrixParameters[shader.Pointer] = parameter = shader.Parameters["MatrixTransform"];

            if (parameter.IsNull) return;
            parameter.SetValue(transform.HasValue ? transform.GetValueOrDefault() : Main.GameViewMatrix.NormalizedTransformationMatrix);
        }
    }

    public readonly unsafe partial struct GameShaders
    {
        static MiscShaderDictionary? misc;

        public static MiscShaderDictionary Misc => misc ??= new MiscShaderDictionary(() => Misc_.Pointer);
    }

    public sealed unsafe class MiscShaderDictionary
    {
        readonly Func<IntPtr> source;

        internal MiscShaderDictionary(Func<IntPtr> source) => this.source = source;

        IntPtr dictionary => source();

        public MiscShaderData this[string key]
        {
            get => TryGetValue(key, out MiscShaderData data) ? data : throw new KeyNotFoundException($"GameShaders.Misc has no shader '{key}'");
            set
            {
                ShaderHandles.Keep(value.Pointer);
                new GameDictionary(dictionary).Set(GameString.New(key), value.Pointer);
            }
        }

        public bool ContainsKey(string key) => new GameDictionary(dictionary).ContainsKey(GameString.New(key));

        public bool TryGetValue(string key, out MiscShaderData value)
        {
            bool found = new GameDictionary(dictionary).TryGetValue(GameString.New(key), out IntPtr pointer);
            value = new MiscShaderData(pointer);
            return found;
        }
    }
}
